# Integration plan

## Current status

The C control plane and deterministic local gates are live and covered by unit
and transition tests. The scientific workers are fail-closed adapters. This is
intentional. The repository currently has certified Truth and advisory
Intuition ports, while automatic theorem discovery, cross-organ Lean writeback,
real SSHX review, and venue research still require explicit worker contracts.

## Bootstrap

From the repository root:

```sh
make -C tools/research_pipeline bootstrap
make -C tools/research_pipeline test
make -C tools/research_pipeline plan
```

`bootstrap` creates `Papers/research/` records and `Papers/draft/` from blocking
templates only when the destination does not already exist. It never overwrites
research evidence or manuscript text.

## Worker contracts

The following environment variables activate project-specific stages:

```text
FKST_INTUITION_COMMAND
FKST_LEAN_WRITEBACK_COMMAND
FKST_LEAN_VERIFY_COMMAND
FKST_FORMALIZATION_ROOT
FKST_SSHX_REVIEW_COMMAND
FKST_REVISION_COMMAND
FKST_MANUSCRIPT_TEX
FKST_COVER_LETTER_TEX
```

Each command runs from the `trureturing-paper` repository root. The scripts pass
input and output locations through additional `FKST_*` environment variables.
Every worker must write the exact required output and exit nonzero on an
incomplete or unverifiable result.

## Cross-organ rule

No default command guesses a sibling checkout. Lean writeback and verification
must be routed through an explicit formalization worktree or, later, a
content-addressed FKST contract. The existing Paper Core and repository-local
FKST package continue to consume only their declared inputs.

## Activation sequence

1. Bind the Intuition worker to the exact-release joined research input.
2. Define a versioned formalization request and proof receipt shared with the
   formalization organ.
3. Bind Lean verification to a pinned worktree and record toolchain, commit,
   declaration, statement digest, and axiom closure.
4. Bind literature and journal research to dated, source-carrying evidence.
5. Bind three independent SSHX reviewer contexts and revision workers.
6. Run the whole graph locally and preserve the event ledger.
7. Add an FKST package only after parity tests show that at-least-once delivery,
   deduplication, and supersession cannot corrupt the research ledger.

The older Python automation may be used temporarily as a worker behind these
contracts. It should not remain the owner of retries, state transitions, or
release admission once parity is established.
