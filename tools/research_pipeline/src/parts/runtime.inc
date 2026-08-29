static int execute_with_timeout(const char *command, const char *cwd, const char *log_path, unsigned timeout_seconds, bool dry_run) {
    if (dry_run) {
        printf("DRY-RUN cwd=%s timeout=%u command=%s\n", cwd, timeout_seconds, command);
        return 0;
    }
    pid_t pid = fork();
    if (pid < 0) return 125;
    if (pid == 0) {
        if (setsid() < 0) _exit(126);
        if (chdir(cwd) != 0) _exit(126);
        int fd = open(log_path, O_WRONLY | O_CREAT | O_TRUNC, 0644);
        if (fd < 0) _exit(126);
        if (dup2(fd, STDOUT_FILENO) < 0 || dup2(fd, STDERR_FILENO) < 0) _exit(126);
        close(fd);
        execl("/bin/sh", "sh", "-lc", command, (char *)NULL);
        _exit(127);
    }
    time_t start = time(NULL);
    int status = 0;
    for (;;) {
        pid_t r = waitpid(pid, &status, WNOHANG);
        if (r == pid) break;
        if (r < 0 && errno != EINTR) return 125;
        if (g_stop) {
            kill(-pid, SIGTERM);
            sleep(1);
            kill(-pid, SIGKILL);
            waitpid(pid, &status, 0);
            return 130;
        }
        if (timeout_seconds > 0U && (unsigned)(time(NULL) - start) >= timeout_seconds) {
            kill(-pid, SIGTERM);
            struct timespec grace = {.tv_sec = 2, .tv_nsec = 0};
            nanosleep(&grace, NULL);
            if (waitpid(pid, &status, WNOHANG) == 0) {
                kill(-pid, SIGKILL);
                waitpid(pid, &status, 0);
            }
            return 124;
        }
        struct timespec pause = {.tv_sec = 0, .tv_nsec = 100000000L};
        nanosleep(&pause, NULL);
    }
    if (WIFEXITED(status)) return WEXITSTATUS(status);
    if (WIFSIGNALED(status)) return 128 + WTERMSIG(status);
    return 125;
}

static void absolute_self(const char *argv0, char out[FKST_MAX_PATH]) {
    char resolved[FKST_MAX_PATH];
    if (realpath(argv0, resolved)) {
        copy_text(out, FKST_MAX_PATH, resolved, "executable path");
        return;
    }
    copy_text(out, FKST_MAX_PATH, argv0, "executable path");
}

static void plan_pipeline(const char *argv0, const char *config_path, const char *from) {
    fkst_config cfg;
    fkst_state state;
    char self[FKST_MAX_PATH];
    absolute_self(argv0, self);
    load_config(config_path, &cfg);
    load_state(&cfg, &state);
    if (from) {
        if (stage_index(&cfg, from) < 0) die("unknown --from stage: %s", from);
        copy_text(state.current, sizeof(state.current), from, "from stage");
    }
    for (unsigned step = 0; step < cfg.max_transitions; ++step) {
        if (strcmp(state.current, "DONE") == 0) {
            puts("PLAN complete: success path reaches DONE");
            return;
        }
        if (strcmp(state.current, "STOP") == 0) die("planned success path reaches STOP");
        int idx = stage_index(&cfg, state.current);
        if (idx < 0) die("state references unknown stage: %s", state.current);
        const fkst_stage *st = &cfg.stages[idx];
        char command[FKST_MAX_COMMAND];
        expand_command(command, sizeof(command), st->command, &cfg, self, st->id, 1);
        printf("PLAN stage=%s timeout=%u retries=%u pass=%s fail=%s\n",
               st->id, st->timeout_seconds, st->max_retries, st->success_next, st->failure_next);
        printf("  command: %s\n", command);
        printf("  outputs: %s\n", st->required_outputs);
        copy_text(state.current, sizeof(state.current), st->success_next, "planned next stage");
    }
    die("planned success path exceeded max transition limit (%u)", cfg.max_transitions);
}

static void run_pipeline(const char *argv0, const char *config_path, const char *from) {
    fkst_config cfg;
    fkst_state state;
    char self[FKST_MAX_PATH];
    absolute_self(argv0, self);
    load_config(config_path, &cfg);
    acquire_lock(&cfg);
    load_state(&cfg, &state);
    if (from) {
        if (stage_index(&cfg, from) < 0) die("unknown --from stage: %s", from);
        copy_text(state.current, sizeof(state.current), from, "from stage");
        copy_text(state.last_outcome, sizeof(state.last_outcome), "manual-reset", "outcome");
        save_state(&cfg, &state);
    }
    while (!g_stop) {
        if (strcmp(state.current, "DONE") == 0) {
            puts("pipeline already completed");
            return;
        }
        if (strcmp(state.current, "STOP") == 0) die("pipeline is stopped; inspect the event log and reset with --from");
        if (state.transitions >= cfg.max_transitions) die("max transition limit reached (%u)", cfg.max_transitions);
        int idx = stage_index(&cfg, state.current);
        if (idx < 0) die("state references unknown stage: %s", state.current);
        const fkst_stage *st = &cfg.stages[idx];
        bool passed = false;
        int exit_code = 125;
        char artifact_hash[65] = "";
        char last_log[FKST_MAX_PATH] = "";
        for (unsigned attempt = 1; attempt <= st->max_retries + 1U; ++attempt) {
            char log_dir[FKST_MAX_PATH], stamp[32], command[FKST_MAX_COMMAND];
            join_path(log_dir, sizeof(log_dir), cfg.root, cfg.run_log_dir);
            if (mkdir_p(log_dir) != 0) die("cannot create run-log directory: %s", strerror(errno));
            compact_time(stamp);
            if (snprintf(last_log, sizeof(last_log), "%s/%s-%s-a%u.log", log_dir, stamp, st->id, attempt) >= (int)sizeof(last_log))
                die("run log path too long");
            expand_command(command, sizeof(command), st->command, &cfg, self, st->id, attempt);
            append_event(&cfg, "stage_start", st->id, attempt, 0, "running", "", last_log);
            exit_code = execute_with_timeout(command, cfg.root, last_log, st->timeout_seconds, false);
            if (exit_code == 0 && hash_required_outputs(&cfg, st->required_outputs, artifact_hash) == 0) {
                passed = true;
                append_event(&cfg, "stage_finish", st->id, attempt, exit_code, "pass", artifact_hash, last_log);
                break;
            }
            if (exit_code == 0) exit_code = 66;
            append_event(&cfg, "stage_finish", st->id, attempt, exit_code, "fail", "", last_log);
        }
        copy_text(state.last_stage, sizeof(state.last_stage), st->id, "last stage");
        copy_text(state.last_outcome, sizeof(state.last_outcome), passed ? "pass" : "fail", "last outcome");
        copy_text(state.last_artifact_hash, sizeof(state.last_artifact_hash), passed ? artifact_hash : "", "artifact hash");
        copy_text(state.current, sizeof(state.current), passed ? st->success_next : st->failure_next, "next stage");
        ++state.transitions;
        save_state(&cfg, &state);
        printf("%s: %s -> %s\n", st->id, passed ? "pass" : "fail", state.current);
        if (strcmp(state.current, "DONE") == 0) {
            append_event(&cfg, "pipeline_finish", st->id, 0, 0, "pass", state.last_artifact_hash, last_log);
            puts("pipeline completed");
            return;
        }
        if (strcmp(state.current, "STOP") == 0) die("stage %s failed and transitioned to STOP", st->id);
    }
    die("pipeline interrupted");
}
