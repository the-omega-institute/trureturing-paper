# trureturing-paper

`trureturing-paper` is an independent paper-assembly organ. Its core turns a fixed
`recipe.v1` plus human-blessed, frozen truth inputs into byte-reproducible LaTeX.
It is a greenfield assembler; the retired `papergen` shell is not a dependency.

## Boundaries

The Core project is pure assembly logic. It consumes truth through read-only ports and
does not inspect a source checkout, `formalize`, a harness, or another organ's ledger.
The only cross-organ entry signal is a content-addressed, blessed
`source-snapshot.v1`. The reader verifies its content digest before parsing it and then
checks the vendored canonical contract fields fail-closed.

A claim enters a paper only when one frozen declaration is jointly bound to the
blessed Lean report, its frozen axiom whitelist, a truth anchor, and exactly one stable
blueprint describe anchor. Prose such as "Machine-checked" and truth-graph module status
are not declaration-level proof signals.

Formula emission reuses the read-only vendored Scribe `Formula` AST and `LatexWriter`
from the pinned `trureturing` commit documented in `vendor/scribe/README.md`. Document
assembly is local to this repository.

### FKST boundary

`fkst-ops` and the FKST engine know only generic deployment, package, event, and
lifecycle mechanics. They do not know what a paper, theorem, claim, or formalization is.
The repository-local package at `.fkst/local-packages/trureturing-paper/` owns only this
organ's orchestration: it watches files inside this repository, invokes the local
`Trureturing.Paper.Cli`, and writes the local `Papers/paper.tex` plus publication receipt.
It never reads a sibling checkout, the base frozen ledger, a base skill, or a GitHub/network
control plane. The architecture test `FkstOrganBoundaryTests` keeps that separation
machine-visible.

## First slice

The walking skeleton proves only this path:

1. Read one fixed synthetic recipe and one self-contained frozen fixture bundle.
2. Verify the blessed snapshot digest and resolve one frozen declaration.
3. Gate the claim and create a typed `PaperDocument`.
4. Emit `paper.tex` with fixed UTF-8 (no BOM), LF newlines, no time, locale, timezone,
   working-directory, or machine-dependent content.

Build and run the tests offline with:

```sh
dotnet restore Trureturing.Paper.slnx --ignore-failed-sources
dotnet build Trureturing.Paper.slnx --no-restore
dotnet test tests/Trureturing.Paper.Tests/Trureturing.Paper.Tests.csproj --no-restore
```

The thin CLI filesystem adapter is:

```sh
dotnet run --project src/Trureturing.Paper.Cli -- assemble \
  --recipe Papers/recipes/synthetic-minimal.recipe.v1.json \
  --frozen-bundle tests/fixtures \
  --output out/paper.tex
```

## Real acceptance (now live)

The real acceptance test **runs and passes** (`Real_blessed_snapshot_acceptance`). It consumes a
real blessed `source-snapshot.v1` and the real `truth-graph.v1.json` for `trureturing@90059eb`
(670 kernel-frozen theorems), verifies full content-addressing / provenance closure, and
assembles a byte-reproducible `paper.tex` citing a real closed theorem
(`D5/S0/Carrier/TraceConjugation.trace_conj`, real axiom closure `{propext}`); a fabricated
frozen GID fails the claim gate against the real graph. Fixtures live under `tests/fixtures-real/`;
`TruthGraphReader` parses the canonical lower_snake_case truth-graph schema.

## Repository-local FKST lifecycle

The host package is a live on-demand `observe → act` chain. It detects an unpublished
blessed input, invokes the local assembler, fails loud when the claim gate rejects, and
records the generated paper idempotently. It is not a directory-shape placeholder. The
package remains local because its event names, input paths, CLI, output, and publication
receipt are paper-domain concerns.

## Explicitly deferred

Still deferred: migration from the manually pinned frozen bundle to the shared
`truth-release.v1` intake, topic selection, research-loop automation, citation/evidence
rendering, PDF production, and arXiv packaging. Supervise/daemon concurrency hardening
(build-key dedup over the full input closure and serialized install+record) remains deferred
until concurrent delivery becomes real.


## Truth and Intuition research indexes

The next-generation intake is split by authority:

- `PaperTruthIndex` contains certified declaration identity, exact frozen prerequisite
  closure, axiom closure and mdBook anchors from a Pages-independent
  `paper-truth-release-port.v1`.
- `PaperIntuitionIndex` contains advisory candidate bridges, evidence, falsifiers and
  predicted reachability/pruning from `paper-intuition-port.v1`.

An upstream adapter will verify the shared release and produce the Paper-owned truth
port. The Paper core does not own the upstream wire parser. Intuition proposals can
identify research gaps, but they cannot be retrieved as certified declarations or pass
the existing claim gate. See
[`docs/TRUTH_AND_INTUITION_INDEXES.md`](docs/TRUTH_AND_INTUITION_INDEXES.md).

## Local example: consume, assemble, publish

The example cycle is deliberately local and deterministic. Its mock adapter validates the
checked-in real subset in `Papers/frozen-bundle`, then emits the exact Paper-owned truth and
Intuition port contracts. It demonstrates the consumption mechanism; it is not the upstream
truth-release verifier.

Run the complete cycle from the repository root:

```sh
dotnet run --project src/Trureturing.Paper.Cli -- example-cycle \
  --frozen-bundle Papers/frozen-bundle \
  --output-root .
```

The command writes the typed ports and reproducible LaTeX under `Papers/example/`, then
publishes the reading site to `site/index.html`. The certified theorem is selected through
`PaperTruthIndex` and must still pass the existing frozen claim gate. Research candidates are
read only from `PaperIntuitionIndex`, rendered as advisory, and cannot be retrieved or claimed
as certified declarations.

For consumption-only development, emit just the mock ports:

```sh
dotnet run --project src/Trureturing.Paper.Cli -- emit-local-ports \
  --frozen-bundle Papers/frozen-bundle \
  --output Papers/example
```

On pushes to `dev`, the Pages workflow uploads `site/` and deploys it. Repository Pages
settings remain an operator responsibility.
