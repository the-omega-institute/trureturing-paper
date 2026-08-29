static void die(const char *fmt, ...) {
    va_list ap;
    va_start(ap, fmt);
    fprintf(stderr, "fkst-pipeline: ");
    vfprintf(stderr, fmt, ap);
    fprintf(stderr, "\n");
    va_end(ap);
    exit(EXIT_FAILURE);
}

static void copy_text(char *dst, size_t cap, const char *src, const char *label) {
    if (snprintf(dst, cap, "%s", src ? src : "") >= (int)cap) die("%s is too long", label);
}

static char *trim(char *s) {
    while (*s && isspace((unsigned char)*s)) ++s;
    char *end = s + strlen(s);
    while (end > s && isspace((unsigned char)end[-1])) --end;
    *end = '\0';
    return s;
}

static bool path_is_absolute(const char *p) { return p && p[0] == '/'; }

static void join_path(char *out, size_t cap, const char *root, const char *path) {
    if (path_is_absolute(path)) copy_text(out, cap, path, "path");
    else if (snprintf(out, cap, "%s/%s", root, path) >= (int)cap) die("joined path is too long");
}

static int mkdir_p(const char *path) {
    char buf[FKST_MAX_PATH];
    copy_text(buf, sizeof(buf), path, "directory path");
    size_t len = strlen(buf);
    if (len == 0) return 0;
    if (buf[len - 1] == '/') buf[len - 1] = '\0';
    for (char *p = buf + 1; *p; ++p) {
        if (*p == '/') {
            *p = '\0';
            if (mkdir(buf, 0775) != 0 && errno != EEXIST) return -1;
            *p = '/';
        }
    }
    if (mkdir(buf, 0775) != 0 && errno != EEXIST) return -1;
    return 0;
}

static void parent_dir(const char *path, char *out, size_t cap) {
    copy_text(out, cap, path, "path");
    char *slash = strrchr(out, '/');
    if (!slash) { copy_text(out, cap, ".", "parent directory"); return; }
    if (slash == out) { slash[1] = '\0'; return; }
    *slash = '\0';
}

static bool regular_nonempty(const char *path) {
    struct stat st;
    return stat(path, &st) == 0 && S_ISREG(st.st_mode) && st.st_size > 0;
}

static bool contains_placeholder(const char *s) {
    if (!s || !*s) return true;
    char lower[2048];
    size_t n = strlen(s);
    if (n >= sizeof(lower)) n = sizeof(lower) - 1;
    for (size_t i = 0; i < n; ++i) lower[i] = (char)tolower((unsigned char)s[i]);
    lower[n] = '\0';
    const char *bad[] = {"todo", "tbd", "placeholder", "unknown", "fill me", "fixme", "n/a"};
    for (size_t i = 0; i < ARRAY_LEN(bad); ++i) if (strstr(lower, bad[i])) return true;
    return false;
}

static void iso_time(char out[32]) {
    time_t now = time(NULL);
    struct tm tmv;
    gmtime_r(&now, &tmv);
    strftime(out, 32, "%Y-%m-%dT%H:%M:%SZ", &tmv);
}

static void compact_time(char out[32]) {
    time_t now = time(NULL);
    struct tm tmv;
    gmtime_r(&now, &tmv);
    strftime(out, 32, "%Y%m%dT%H%M%SZ", &tmv);
}

static void json_string(FILE *fp, const char *s) {
    fputc('"', fp);
    for (; s && *s; ++s) {
        unsigned char c = (unsigned char)*s;
        switch (c) {
            case '"': fputs("\\\"", fp); break;
            case '\\': fputs("\\\\", fp); break;
            case '\n': fputs("\\n", fp); break;
            case '\r': fputs("\\r", fp); break;
            case '\t': fputs("\\t", fp); break;
            default:
                if (c < 0x20U) fprintf(fp, "\\u%04x", c);
                else fputc(c, fp);
        }
    }
    fputc('"', fp);
}

static void append_event(const fkst_config *cfg, const char *event, const char *stage,
                         unsigned attempt, int exit_code, const char *outcome,
                         const char *artifact_hash, const char *run_log) {
    char path[FKST_MAX_PATH], dir[FKST_MAX_PATH], ts[32];
    join_path(path, sizeof(path), cfg->root, cfg->event_log_path);
    parent_dir(path, dir, sizeof(dir));
    if (mkdir_p(dir) != 0) die("cannot create event-log directory: %s", strerror(errno));
    FILE *fp = fopen(path, "a");
    if (!fp) die("cannot open event log %s: %s", path, strerror(errno));
    iso_time(ts);
    fputc('{', fp);
    fputs("\"timestamp\":", fp); json_string(fp, ts);
    fputs(",\"event\":", fp); json_string(fp, event);
    fputs(",\"stage\":", fp); json_string(fp, stage ? stage : "");
    fprintf(fp, ",\"attempt\":%u,\"exit_code\":%d", attempt, exit_code);
    fputs(",\"outcome\":", fp); json_string(fp, outcome ? outcome : "");
    fputs(",\"artifact_hash\":", fp); json_string(fp, artifact_hash ? artifact_hash : "");
    fputs(",\"run_log\":", fp); json_string(fp, run_log ? run_log : "");
    fputs("}\n", fp);
    fclose(fp);
}

static void sha256_to_hex(const uint8_t hash[32], char out[65]) {
    static const char hex[] = "0123456789abcdef";
    for (size_t i = 0; i < 32; ++i) {
        out[i * 2] = hex[hash[i] >> 4U];
        out[i * 2 + 1] = hex[hash[i] & 0x0fU];
    }
    out[64] = '\0';
}

static int hash_file_into(sha256_ctx *ctx, const char *path) {
    FILE *fp = fopen(path, "rb");
    if (!fp) return -1;
    uint8_t buf[32768];
    size_t n;
    while ((n = fread(buf, 1, sizeof(buf), fp)) > 0) sha256_update(ctx, buf, n);
    int ok = ferror(fp) ? -1 : 0;
    fclose(fp);
    return ok;
}

static int hash_required_outputs(const fkst_config *cfg, const char *list, char hex[65]) {
    sha256_ctx ctx;
    sha256_init(&ctx);
    if (!list || strcmp(list, "-") == 0 || *list == '\0') {
        uint8_t hash[32];
        sha256_final(&ctx, hash);
        sha256_to_hex(hash, hex);
        return 0;
    }
    char copy[FKST_MAX_OUTPUTS];
    copy_text(copy, sizeof(copy), list, "required outputs");
    char *save = NULL;
    for (char *tok = strtok_r(copy, ";", &save); tok; tok = strtok_r(NULL, ";", &save)) {
        char full[FKST_MAX_PATH];
        tok = trim(tok);
        join_path(full, sizeof(full), cfg->root, tok);
        if (!regular_nonempty(full)) return -1;
        sha256_update(&ctx, (const uint8_t *)tok, strlen(tok));
        const uint8_t separator = 0;
        sha256_update(&ctx, &separator, 1);
        if (hash_file_into(&ctx, full) != 0) return -1;
    }
    uint8_t hash[32];
    sha256_final(&ctx, hash);
    sha256_to_hex(hash, hex);
    return 0;
}

static int split_tabs(char *line, char **fields, size_t cap) {
    size_t n = 0;
    char *p = line;
    while (n < cap) {
        fields[n++] = p;
        char *tab = strchr(p, '\t');
        if (!tab) break;
        *tab = '\0';
        p = tab + 1;
    }
    for (size_t i = 0; i < n; ++i) fields[i] = trim(fields[i]);
    return (int)n;
}
