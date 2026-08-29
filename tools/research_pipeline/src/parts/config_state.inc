static int stage_index(const fkst_config *cfg, const char *id) {
    for (size_t i = 0; i < cfg->stage_count; ++i) if (strcmp(cfg->stages[i].id, id) == 0) return (int)i;
    return -1;
}

static void config_defaults(fkst_config *cfg) {
    memset(cfg, 0, sizeof(*cfg));
    copy_text(cfg->root, sizeof(cfg->root), ".", "root");
    copy_text(cfg->state_path, sizeof(cfg->state_path), ".fkst-pipeline/state", "state path");
    copy_text(cfg->event_log_path, sizeof(cfg->event_log_path), ".fkst-pipeline/events.jsonl", "event log path");
    copy_text(cfg->run_log_dir, sizeof(cfg->run_log_dir), ".fkst-pipeline/runs", "run log directory");
    cfg->max_transitions = 128;
}

static void parse_directive(fkst_config *cfg, char *line) {
    char *eq = strchr(line, '=');
    if (!eq) die("invalid config directive: %s", line);
    *eq = '\0';
    char *key = trim(line + 1), *value = trim(eq + 1);
    if (strcmp(key, "root") == 0) copy_text(cfg->root, sizeof(cfg->root), value, "root");
    else if (strcmp(key, "state") == 0) copy_text(cfg->state_path, sizeof(cfg->state_path), value, "state path");
    else if (strcmp(key, "event_log") == 0) copy_text(cfg->event_log_path, sizeof(cfg->event_log_path), value, "event log path");
    else if (strcmp(key, "run_log_dir") == 0) copy_text(cfg->run_log_dir, sizeof(cfg->run_log_dir), value, "run log directory");
    else if (strcmp(key, "max_transitions") == 0) cfg->max_transitions = (unsigned)strtoul(value, NULL, 10);
    else die("unknown config directive: %s", key);
}

static void load_config(const char *path, fkst_config *cfg) {
    config_defaults(cfg);
    FILE *fp = fopen(path, "r");
    if (!fp) die("cannot open config %s: %s", path, strerror(errno));
    char *line = NULL;
    size_t cap = 0;
    unsigned lineno = 0;
    while (getline(&line, &cap, fp) >= 0) {
        ++lineno;
        char *s = trim(line);
        if (!*s || *s == '#') continue;
        if (*s == '@') { parse_directive(cfg, s); continue; }
        if (cfg->stage_count >= FKST_MAX_STAGES) die("too many stages at line %u", lineno);
        char *f[7];
        int n = split_tabs(s, f, ARRAY_LEN(f));
        if (n != 7) die("config line %u needs 7 tab-separated fields, got %d", lineno, n);
        fkst_stage *st = &cfg->stages[cfg->stage_count++];
        memset(st, 0, sizeof(*st));
        copy_text(st->id, sizeof(st->id), f[0], "stage id");
        copy_text(st->success_next, sizeof(st->success_next), f[1], "success transition");
        copy_text(st->failure_next, sizeof(st->failure_next), f[2], "failure transition");
        st->timeout_seconds = (unsigned)strtoul(f[3], NULL, 10);
        st->max_retries = (unsigned)strtoul(f[4], NULL, 10);
        copy_text(st->command, sizeof(st->command), f[5], "stage command");
        copy_text(st->required_outputs, sizeof(st->required_outputs), f[6], "required outputs");
        if (!*st->id || !*st->success_next || !*st->failure_next || !*st->command) die("empty required field at line %u", lineno);
    }
    free(line);
    fclose(fp);
    if (cfg->stage_count == 0) die("config has no stages");
    for (size_t i = 0; i < cfg->stage_count; ++i) {
        if (stage_index(cfg, cfg->stages[i].id) != (int)i) die("duplicate stage id: %s", cfg->stages[i].id);
        const char *nexts[] = {cfg->stages[i].success_next, cfg->stages[i].failure_next};
        for (size_t k = 0; k < ARRAY_LEN(nexts); ++k) {
            if (strcmp(nexts[k], "DONE") != 0 && strcmp(nexts[k], "STOP") != 0 && stage_index(cfg, nexts[k]) < 0)
                die("stage %s points to missing stage %s", cfg->stages[i].id, nexts[k]);
        }
    }
}

static void load_state(const fkst_config *cfg, fkst_state *state) {
    memset(state, 0, sizeof(*state));
    copy_text(state->current, sizeof(state->current), cfg->stages[0].id, "initial stage");
    copy_text(state->last_outcome, sizeof(state->last_outcome), "new", "outcome");
    char path[FKST_MAX_PATH];
    join_path(path, sizeof(path), cfg->root, cfg->state_path);
    FILE *fp = fopen(path, "r");
    if (!fp) {
        if (errno == ENOENT) return;
        die("cannot open state %s: %s", path, strerror(errno));
    }
    char line[512];
    while (fgets(line, sizeof(line), fp)) {
        char *s = trim(line), *eq = strchr(s, '=');
        if (!eq) continue;
        *eq = '\0';
        char *key = trim(s), *value = trim(eq + 1);
        if (strcmp(key, "current") == 0) copy_text(state->current, sizeof(state->current), value, "current stage");
        else if (strcmp(key, "transitions") == 0) state->transitions = (unsigned)strtoul(value, NULL, 10);
        else if (strcmp(key, "last_outcome") == 0) copy_text(state->last_outcome, sizeof(state->last_outcome), value, "last outcome");
        else if (strcmp(key, "last_stage") == 0) copy_text(state->last_stage, sizeof(state->last_stage), value, "last stage");
        else if (strcmp(key, "last_artifact_hash") == 0) copy_text(state->last_artifact_hash, sizeof(state->last_artifact_hash), value, "artifact hash");
    }
    fclose(fp);
}

static void save_state(const fkst_config *cfg, const fkst_state *state) {
    char path[FKST_MAX_PATH], tmp[FKST_MAX_PATH], dir[FKST_MAX_PATH];
    join_path(path, sizeof(path), cfg->root, cfg->state_path);
    parent_dir(path, dir, sizeof(dir));
    if (mkdir_p(dir) != 0) die("cannot create state directory: %s", strerror(errno));
    if (snprintf(tmp, sizeof(tmp), "%s.tmp.%ld", path, (long)getpid()) >= (int)sizeof(tmp)) die("state temp path too long");
    FILE *fp = fopen(tmp, "w");
    if (!fp) die("cannot write state temp file: %s", strerror(errno));
    fprintf(fp, "current=%s\ntransitions=%u\nlast_outcome=%s\nlast_stage=%s\nlast_artifact_hash=%s\n",
            state->current, state->transitions, state->last_outcome, state->last_stage, state->last_artifact_hash);
    if (fflush(fp) != 0 || fsync(fileno(fp)) != 0 || fclose(fp) != 0) die("failed to flush state file");
    if (rename(tmp, path) != 0) die("cannot replace state file: %s", strerror(errno));
}

static void release_lock(void) {
    if (g_lock_fd >= 0) close(g_lock_fd);
    g_lock_fd = -1;
}

static void signal_handler(int sig) { (void)sig; g_stop = 1; }

static void acquire_lock(const fkst_config *cfg) {
    char state_full[FKST_MAX_PATH], lock_path[FKST_MAX_PATH], dir[FKST_MAX_PATH];
    join_path(state_full, sizeof(state_full), cfg->root, cfg->state_path);
    parent_dir(state_full, dir, sizeof(dir));
    if (mkdir_p(dir) != 0) die("cannot create lock directory: %s", strerror(errno));
    if (snprintf(lock_path, sizeof(lock_path), "%s.lock", state_full) >= (int)sizeof(lock_path)) die("lock path too long");
    g_lock_fd = open(lock_path, O_RDWR | O_CREAT, 0644);
    if (g_lock_fd < 0) die("cannot open pipeline lock %s: %s", lock_path, strerror(errno));
    struct flock lock;
    memset(&lock, 0, sizeof(lock));
    lock.l_type = F_WRLCK;
    lock.l_whence = SEEK_SET;
    if (fcntl(g_lock_fd, F_SETLK, &lock) != 0) {
        close(g_lock_fd);
        g_lock_fd = -1;
        die("pipeline is already locked: %s", lock_path);
    }
    if (ftruncate(g_lock_fd, 0) != 0 || dprintf(g_lock_fd, "%ld\n", (long)getpid()) < 0 || fsync(g_lock_fd) != 0)
        die("cannot record pipeline lock owner: %s", strerror(errno));
    atexit(release_lock);
    signal(SIGINT, signal_handler);
    signal(SIGTERM, signal_handler);
}

static void replace_all(char *out, size_t cap, const char *input, const char *needle, const char *replacement) {
    size_t used = 0, needle_len = strlen(needle), repl_len = strlen(replacement);
    const char *p = input;
    while (*p) {
        const char *hit = strstr(p, needle);
        size_t chunk = hit ? (size_t)(hit - p) : strlen(p);
        if (used + chunk + 1 > cap) die("expanded command too long");
        memcpy(out + used, p, chunk); used += chunk;
        if (!hit) break;
        if (used + repl_len + 1 > cap) die("expanded command too long");
        memcpy(out + used, replacement, repl_len); used += repl_len;
        p = hit + needle_len;
    }
    out[used] = '\0';
}

static void expand_command(char *out, size_t cap, const char *input, const fkst_config *cfg,
                           const char *self, const char *stage, unsigned attempt) {
    char a[FKST_MAX_COMMAND], b[FKST_MAX_COMMAND], attempt_s[32];
    snprintf(attempt_s, sizeof(attempt_s), "%u", attempt);
    replace_all(a, sizeof(a), input, "${ROOT}", cfg->root);
    replace_all(b, sizeof(b), a, "${SELF}", self);
    replace_all(a, sizeof(a), b, "${STAGE}", stage);
    replace_all(out, cap, a, "${ATTEMPT}", attempt_s);
}
