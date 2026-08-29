# C-first research-to-paper control plane

This directory provides a C11 orchestration layer for the `trureturing-paper`
research workflow. It controls legal stage transitions, exclusive execution,
subprocess deadlines, retries, resumable state, per-stage logs, content hashes,
and fail-closed release gates. Mathematical reasoning remains in Intuition and
Lean workers. Bibliographic research, journal checks, LaTeX compilation, and
SSHX review remain explicit adapters.

The tool is checked in as development infrastructure. It is not yet connected
to the repository-local FKST `observe -> act` publication lifecycle and it does
not submit manuscripts or send correspondence.

Build and test from the repository root:

```sh
make -C tools/research_pipeline test
```

Create blocking research records and a journal-neutral LaTeX draft tree:

```sh
make -C tools/research_pipeline bootstrap
```

Inspect the success path without running workers or mutating state:

```sh
make -C tools/research_pipeline plan
```

Run the configured pipeline after the worker environment variables documented
in `docs/INTEGRATION.md` have been supplied:

```sh
tools/research_pipeline/bin/fkst-pipeline run \
  --config tools/research_pipeline/config/pipeline.tsv
```

The default adapters exit with a blocking status when project-specific commands
are absent. A blocked stage is evidence of incomplete integration. It must never
be reported as a completed theorem, review, manuscript, or venue decision.
