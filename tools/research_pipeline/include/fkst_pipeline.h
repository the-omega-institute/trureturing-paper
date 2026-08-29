#ifndef FKST_PIPELINE_H
#define FKST_PIPELINE_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define FKST_MAX_STAGES 64
#define FKST_MAX_ID 64
#define FKST_MAX_PATH 4096
#define FKST_MAX_COMMAND 8192
#define FKST_MAX_OUTPUTS 4096

typedef struct {
    char id[FKST_MAX_ID];
    char success_next[FKST_MAX_ID];
    char failure_next[FKST_MAX_ID];
    unsigned timeout_seconds;
    unsigned max_retries;
    char command[FKST_MAX_COMMAND];
    char required_outputs[FKST_MAX_OUTPUTS];
} fkst_stage;

typedef struct {
    char root[FKST_MAX_PATH];
    char state_path[FKST_MAX_PATH];
    char event_log_path[FKST_MAX_PATH];
    char run_log_dir[FKST_MAX_PATH];
    unsigned max_transitions;
    fkst_stage stages[FKST_MAX_STAGES];
    size_t stage_count;
} fkst_config;

typedef struct {
    char current[FKST_MAX_ID];
    unsigned transitions;
    char last_outcome[32];
    char last_stage[FKST_MAX_ID];
    char last_artifact_hash[65];
} fkst_state;

#endif
