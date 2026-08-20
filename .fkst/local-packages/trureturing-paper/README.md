# trureturing-paper (host package)

The repository-local `trureturing-paper` package is the paper organ's FKST lifecycle.
It consumes the blessed local bundle through an `observe → act` chain and never authors
upstream truth.

## Runtime boundary

```text
CI / repository preflight
    restore + Release build
        ↓
src/Trureturing.Paper.Cli/bin/Release/net10.0/Trureturing.Paper.Cli.dll
        ↓
repository-local FKST Lua
    dotnet <prebuilt local DLL> assemble ...

deployment composition
    selects exact target, platform, engine, machine roots, and activation policy
```

Runtime Lua never calls `dotnet run`, restore, or build. A missing prebuilt local DLL is
a fail-loud repository-preflight defect. `fkst-ops` and the FKST engine remain generic;
all paper paths, event names, CLI arguments, output, and receipt logic stay in this repository.

The package does not know the engine repository, engine revision, deployment set, lock
file, machine profile, or platform checkout. Those facts belong exclusively to deployment
composition. The deployment activation gate validates the selected engine against this
opaque local package.

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
files after repository preflight rebuilds the C# solution.

## Remaining runtime hardening

- deduplicate on a build key over the full input/recipe/renderer closure;
- assemble to an event-unique temporary output, then serialize install + receipt for real
  concurrent delivery;
- migrate the pinned bundle to the shared truth release after its upstream contract closes.

## Gates

The business-repository merge gate builds the Release solution with warnings as errors,
runs the full paper test suite, and enforces the repository-local FKST boundary. These
gates prove the local package and local CLI without selecting an engine or deployment.

The exact deployment-selected engine must still run package `test`, `conformance`, and an
end-to-end `run` before activation. That evidence belongs to the deployment composition
and activation PR. Its absence blocks activation and deployment-readiness claims; it does
not make this business repository own an engine pin.
