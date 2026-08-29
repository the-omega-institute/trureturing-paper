static int tsv_rows(const char *path, int min_fields, int (*check)(char **, int, void *), void *ctx) {
    FILE *fp = fopen(path, "r");
    if (!fp) { fprintf(stderr, "cannot open %s: %s\n", path, strerror(errno)); return -1; }
    char *line = NULL;
    size_t cap = 0;
    int rows = 0;
    bool header = true;
    while (getline(&line, &cap, fp) >= 0) {
        char *s = trim(line);
        if (!*s || *s == '#') continue;
        char *f[32];
        int n = split_tabs(s, f, ARRAY_LEN(f));
        if (header) { header = false; continue; }
        if (n < min_fields || check(f, n, ctx) != 0) { free(line); fclose(fp); return -1; }
        ++rows;
    }
    free(line);
    fclose(fp);
    return rows;
}

static int days_since(const char *date);

static bool is_sha256(const char *value) {
    if (!value) return false;
    const char *p = value;
    if (strncmp(p, "sha256:", 7) == 0) p += 7;
    if (strlen(p) != 64) return false;
    for (size_t i = 0; i < 64; ++i) if (!isxdigit((unsigned char)p[i])) return false;
    return true;
}

static bool starts_with_https(const char *value) {
    return value && strncmp(value, "https://", 8) == 0;
}

static int check_novelty(char **f, int n, void *ctx) {
    (void)ctx;
    if (n < 9) return -1;
    for (int i = 0; i < 8; ++i) if (contains_placeholder(f[i])) return -1;
    if (days_since(f[1]) > 365) return -1;
    if (strcasecmp(f[8], "supported") != 0) return -1;
    return 0;
}

static int gate_novelty(const char *path) {
    int rows = tsv_rows(path, 9, check_novelty, NULL);
    if (rows < 2) { fprintf(stderr, "novelty gate requires at least two fully checked nearest-prior-result rows\n"); return 1; }
    printf("novelty gate: pass (%d prior-result rows)\n", rows);
    return 0;
}

typedef struct { const char *root; int theorem_count; } claims_ctx;

static bool file_contains_token(const char *path, const char *token) {
    FILE *fp = fopen(path, "r");
    if (!fp) return false;
    char *line = NULL;
    size_t cap = 0;
    bool found = false;
    while (getline(&line, &cap, fp) >= 0) if (strstr(line, token)) { found = true; break; }
    free(line);
    fclose(fp);
    return found;
}

static int check_claim(char **f, int n, void *vctx) {
    claims_ctx *ctx = vctx;
    if (n < 8) return -1;
    for (int i = 0; i < 5; ++i) if (contains_placeholder(f[i])) return -1;
    bool formal = strcasecmp(f[4], "theorem") == 0 || strcasecmp(f[4], "lemma") == 0 || strcasecmp(f[4], "corollary") == 0;
    if (formal) {
        char full[FKST_MAX_PATH];
        if (strcasecmp(f[5], "verified") != 0 || !is_sha256(f[6])) return -1;
        join_path(full, sizeof(full), ctx->root, f[3]);
        if (!regular_nonempty(full) || !file_contains_token(full, f[2])) return -1;
        ++ctx->theorem_count;
    } else if (strcasecmp(f[4], "informal") == 0) {
        if (strcasecmp(f[7], "explicitly_informal") != 0) return -1;
    } else return -1;
    return 0;
}

static int gate_claims(const char *root, const char *path) {
    claims_ctx ctx = {.root = root, .theorem_count = 0};
    int rows = tsv_rows(path, 8, check_claim, &ctx);
    if (rows < 1 || ctx.theorem_count < 1) {
        fprintf(stderr, "claim gate requires at least one verified formal claim mapped to a Lean declaration\n");
        return 1;
    }
    printf("claim gate: pass (%d claims, %d formal)\n", rows, ctx.theorem_count);
    return 0;
}

static int days_since(const char *date) {
    int y, m, d;
    char tail;
    if (sscanf(date, "%d-%d-%d%c", &y, &m, &d, &tail) != 3) return 1000000;
    if (y < 1970 || m < 1 || m > 12 || d < 1 || d > 31) return 1000000;
    struct tm tmv;
    memset(&tmv, 0, sizeof(tmv));
    tmv.tm_year = y - 1900;
    tmv.tm_mon = m - 1;
    tmv.tm_mday = d;
    tmv.tm_isdst = -1;
    time_t then = mktime(&tmv), now = time(NULL);
    if (then == (time_t)-1 || then > now + 86400) return 1000000;
    struct tm checked;
    if (!localtime_r(&then, &checked)) return 1000000;
    if (checked.tm_year != y - 1900 || checked.tm_mon != m - 1 || checked.tm_mday != d) return 1000000;
    return (int)((now - then) / 86400);
}

typedef struct { int eligible; } journals_ctx;

static int check_journal(char **f, int n, void *vctx) {
    journals_ctx *ctx = vctx;
    if (n < 10) return -1;
    for (int i = 0; i < 10; ++i) if (contains_placeholder(f[i])) return -1;
    char *end = NULL;
    long tier = strtol(f[1], &end, 10);
    if (!end || *end || tier < 1 || tier > 4) return -1;
    if (!starts_with_https(f[4]) || days_since(f[5]) > 180) return -1;
    if (strcasecmp(f[8], "yes") != 0 && strcasecmp(f[8], "no") != 0) return -1;
    if (strcasecmp(f[8], "yes") == 0 && tier <= 2) ++ctx->eligible;
    return 0;
}

static int gate_journals(const char *path) {
    journals_ctx ctx = {.eligible = 0};
    int rows = tsv_rows(path, 10, check_journal, &ctx);
    if (rows < 2 || ctx.eligible < 2) {
        fprintf(stderr, "journal gate requires two current, evidence-carrying, internally Tier-2-or-better eligible targets\n");
        return 1;
    }
    printf("journal gate: pass (%d candidates, %d eligible Tier-2-or-better)\n", rows, ctx.eligible);
    return 0;
}

typedef struct {
    int expected_round;
    char reviewer[128];
    char verdict[32];
    int blocking;
    char report_hash[129];
    char manuscript_hash[129];
} review_row;

static int read_review(const char *path, int expected, review_row *row) {
    FILE *fp = fopen(path, "r");
    if (!fp) return -1;
    char line[2048];
    bool header = true;
    int seen = 0;
    while (fgets(line, sizeof(line), fp)) {
        char *s = trim(line);
        if (!*s || *s == '#') continue;
        char *f[7];
        int n = split_tabs(s, f, ARRAY_LEN(f));
        if (header) { header = false; continue; }
        if (n < 7 || seen++) { fclose(fp); return -1; }
        char *round_end = NULL, *blocking_end = NULL;
        long parsed_round = strtol(f[1], &round_end, 10);
        long parsed_blocking = strtol(f[4], &blocking_end, 10);
        copy_text(row->reviewer, sizeof(row->reviewer), f[0], "reviewer id");
        row->expected_round = (int)parsed_round;
        copy_text(row->verdict, sizeof(row->verdict), f[3], "review verdict");
        row->blocking = (int)parsed_blocking;
        copy_text(row->report_hash, sizeof(row->report_hash), f[5], "report hash");
        copy_text(row->manuscript_hash, sizeof(row->manuscript_hash), f[6], "manuscript hash");
        bool verdict_ok = strcasecmp(row->verdict, "pass") == 0 || strcasecmp(row->verdict, "revise") == 0 ||
                          strcasecmp(row->verdict, "reject") == 0;
        if (!round_end || *round_end || !blocking_end || *blocking_end || parsed_blocking < 0 ||
            row->expected_round != expected || contains_placeholder(row->reviewer) || contains_placeholder(f[2]) ||
            !verdict_ok || !is_sha256(row->report_hash) || !is_sha256(row->manuscript_hash)) { fclose(fp); return -1; }
    }
    fclose(fp);
    return seen == 1 ? 0 : -1;
}

static int gate_reviews(int argc, char **argv) {
    if (argc != 3) { fprintf(stderr, "gate reviews requires exactly three report paths\n"); return 2; }
    review_row r[3];
    memset(r, 0, sizeof(r));
    for (int i = 0; i < 3; ++i) if (read_review(argv[i], i + 1, &r[i]) != 0) {
        fprintf(stderr, "invalid review report: %s\n", argv[i]); return 1;
    }
    if (!strcmp(r[0].reviewer, r[1].reviewer) || !strcmp(r[0].reviewer, r[2].reviewer) || !strcmp(r[1].reviewer, r[2].reviewer)) {
        fprintf(stderr, "review personas must be independent\n"); return 1;
    }
    if (!strcmp(r[0].report_hash, r[1].report_hash) || !strcmp(r[0].report_hash, r[2].report_hash) ||
        !strcmp(r[1].report_hash, r[2].report_hash)) {
        fprintf(stderr, "review report hashes must be distinct\n"); return 1;
    }
    if (!strcmp(r[0].manuscript_hash, r[1].manuscript_hash) || !strcmp(r[1].manuscript_hash, r[2].manuscript_hash)) {
        fprintf(stderr, "manuscript hash must change after each review-driven revision\n"); return 1;
    }
    if (strcasecmp(r[2].verdict, "pass") != 0 || r[2].blocking != 0) {
        fprintf(stderr, "third review must pass with zero blocking issues\n"); return 1;
    }
    puts("review gate: pass (three independent rounds with intervening revisions)");
    return 0;
}

static int gate_pdf(const char *path) {
    struct stat st;
    if (stat(path, &st) != 0 || !S_ISREG(st.st_mode) || st.st_size < 1024) {
        fprintf(stderr, "PDF missing or too small: %s\n", path); return 1;
    }
    FILE *fp = fopen(path, "rb");
    if (!fp) return 1;
    char header[6] = {0};
    size_t n = fread(header, 1, 5, fp);
    fclose(fp);
    if (n != 5 || memcmp(header, "%PDF-", 5) != 0) { fprintf(stderr, "invalid PDF header\n"); return 1; }
    printf("PDF gate: pass (%lld bytes)\n", (long long)st.st_size);
    return 0;
}
