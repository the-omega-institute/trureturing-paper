CC ?= cc
CPPFLAGS ?= -Iinclude -D_POSIX_C_SOURCE=200809L
CFLAGS ?= -std=c11 -O2 -Wall -Wextra -Wpedantic -Werror
LDFLAGS ?=
BIN := bin/fkst-pipeline
SRC := src/fkst_pipeline.c
HDR := include/fkst_pipeline.h
REPO_ROOT ?= $(abspath ../..)
CONFIG := tools/research_pipeline/config/pipeline.tsv

.PHONY: all clean test sanitize bootstrap plan graph

all: $(BIN)

$(BIN): $(SRC) $(HDR) | bin
	$(CC) $(CPPFLAGS) $(CFLAGS) $(SRC) $(LDFLAGS) -o $@

bin:
	mkdir -p $@

test: $(BIN)
	BIN=$(abspath $(BIN)) sh tests/test_cli.sh

sanitize: CFLAGS += -O1 -g -fsanitize=address,undefined -fno-omit-frame-pointer
sanitize: LDFLAGS += -fsanitize=address,undefined
sanitize: clean all test

bootstrap:
	cd $(REPO_ROOT) && tools/research_pipeline/scripts/bootstrap.sh

plan: $(BIN)
	cd $(REPO_ROOT) && tools/research_pipeline/bin/fkst-pipeline run --config $(CONFIG) --dry-run

graph: $(BIN)
	cd $(REPO_ROOT) && tools/research_pipeline/bin/fkst-pipeline graph --config $(CONFIG)

clean:
	rm -f $(BIN)
