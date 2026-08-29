#define _XOPEN_SOURCE 700
#define _POSIX_C_SOURCE 200809L

#include "fkst_pipeline.h"

#include <ctype.h>
#include <dirent.h>
#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

#ifndef PATH_MAX
#define PATH_MAX 4096
#endif

#define ARRAY_LEN(a) (sizeof(a) / sizeof((a)[0]))
#define SHA256_BLOCK_SIZE 32


/*
 * Internal implementation fragments remain in one translation unit so the
 * command has a single portable C11 build target while keeping each concern
 * reviewable in isolation.
 */
#include "parts/sha256.inc"
#include "parts/util.inc"
#include "parts/config_state.inc"
#include "parts/runtime.inc"
#include "parts/gates.inc"
#include "parts/hardening.inc"
#include "parts/cli.inc"
