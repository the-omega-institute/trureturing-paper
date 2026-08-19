# trureturing-paper (host package)

The repository-local `trureturing-paper` package is the paper organ's FKST lifecycle.
It consumes the blessed local bundle through an `observe → act` chain and never authors
upstream truth.

## Runtime boundary

```text
CI / preflight
    restore + Release build
        ↓
src/Trureturing.Paper.Cli/bin/Release/net10.0/Trureturing.Paper.Cli.dll
        ↓
repository-local FKST Lua
    dotnet <prebuilt local DLL> assemble ...
```

Runtime Lua never calls `dotnet run`, restore, or build. A missing prebuilt local DLL is
a fail-loud deployment/preflight defect. `fkst-ops` and the FKST engine remain generic;
all paper paths, event names, CLI arguments, output, and receipt logic stay in this repository.

## Event chain

```text
raisers/blessed_input.lua
    paper_snapshot_seen { path }
        ↓
departments/observe
    compare the local blessed digest with the local publications ledger
        ↓
departments/act
    invoke the prebuilt local assembler
    require a non-empty local paper.tex
    append an idempotent local publication receipt
```

## Faithfulness

The C# assembler owns paper semantics and the claim gate. It verifies the pinned input
bundle, requires each claimed declaration to bind to the blessed truth graph, and emits
deterministic LaTeX. Lua only treats the CLI's exit code and output existence as lifecycle
signals.

The current manually pinned bundle still has one declared residual: a statement AST under
a genuinely closed GID is not yet bound to the base-owned statement ID. The new shared
truth-export reader is being developed separately; production intake does not switch in
this PR.

## Facts and recovery

- input: `Papers/frozen-bundle/` and `Papers/recipe.v1.json`;
- output: `Papers/paper.tex`;
- publication history: `Papers/publications.jsonl`;
- local executable: `src/Trureturing.Paper.Cli/bin/Release/net10.0/Trureturing.Paper.Cli.dll`.

No runtime marker or cache is authoritative. A wiped runtime can recover from these local
files after preflight rebuilds the C# solution.

## Remaining runtime hardening

- deduplicate on a build key over the full input/recipe/renderer closure;
- assemble to an event-unique temporary output, then serialize install + receipt for real
  concurrent delivery;
- migrate the pinned bundle to the shared truth release after its upstream contract closes.

## Gates

Repository CI builds the Release solution with warnings as errors and runs the full paper
test suite. The previous FKST package `test`, `conformance`, and end-to-end `run` readings
were taken before this invocation change. Before this PR merges, rerun all three with the
exact deployment-selected engine and attach the receipts. Until then, keep the PR Draft
and make no deployment-readiness claim.
