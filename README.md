# trureturing-paper

`trureturing-paper` is the research-opportunity organ. Its primary pipeline combines a
certified `PaperTruthIndex` with proved bridges from `PaperIntuitionIndex`, applies the
claim gate, performs a library-before-proof novelty check, and emits content-addressed
research-candidate data. Rendering and publication belong to `trureturing-pages`.

## Candidate pipeline

Run the deterministic pipeline from the repository root:

```sh
dotnet run --project src/Trureturing.Paper.Cli -- propose-candidates \
  --release Papers/example/paper-truth-release-port.v1.json \
  --intuition Papers/example/paper-intuition-port.v1.json \
  --out Papers/candidates
```

For every `proved` Intuition bridge, the command requires every bridge input to resolve
through `PaperTruthIndex`, then writes three canonical JSON contracts:

- `candidate-paper.v1.<sha256>.json` contains the thesis, outline, grounding and typed
  key claims;
- `literature-research.v1.<sha256>.json` records the real queries, verified sources,
  novelty assessment and rationale;
- `candidate-journal.v1.<sha256>.json` records possible venues and links to the paper
  candidate by its SHA-256 content identity.

The filename digest is computed over the exact canonical JSON bytes. Re-running with the
same ports produces the same paths and bytes. There is no network dependency at generation
time: verified research metadata is reviewed into the deterministic catalog. An unknown
central claim receives no invented citation and is explicitly reported as unverified with
a `partial` novelty assessment.

The claim gate is asymmetric by design. A certified key claim can be constructed only from
an entry returned by `PaperTruthIndex`. A bridge from Intuition is always emitted as
`conjectured`, even when its bridge status is `proved`; that status does not give Intuition
the authority of the certified truth release.

Candidate generation can additionally consume a local `certified-topology.v1` artifact with
`--topology`, `--algorithm-profile-digest`, and `--topology-producer-commit`. Candidate-paper and
literature records then carry exact depth, degree, ancestor/descendant, descendant-cost,
normalized-reach, and dependency-betweenness context for their certified key nodes. The context
is structural data only and does not enter the claim gate. Without a live publication the fields
are explicitly unavailable; a present malformed, floating-point, unreduced, or mismatched
artifact fails closed.

The output schemas live in `contracts/`. Candidate output contains data only: this
repository has no candidate renderer, site, Pages workflow, or HTML output.

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

## Legacy assembly slice

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
`truth-release.v1` intake, general topic selection, proof automation, PDF production, and
arXiv packaging. Supervise/daemon concurrency hardening
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

## Local example: consume and assemble data

The example cycle is deliberately local and deterministic. Its mock adapter validates the
checked-in real subset in `Papers/frozen-bundle`, then emits the exact Paper-owned truth and
Intuition port contracts. It demonstrates the consumption mechanism; it is not the upstream
truth-release verifier.

Run the complete data assembly from the repository root:

```sh
dotnet run --project src/Trureturing.Paper.Cli -- assemble-example \
  --frozen-bundle Papers/frozen-bundle \
  --output-root .
```

The legacy command writes the typed ports and reproducible example LaTeX under `Papers/example/`.
The certified theorem is selected through `PaperTruthIndex` and must still pass the existing
frozen claim gate. Research candidates are read only from `PaperIntuitionIndex` and cannot be
retrieved or claimed as certified declarations. Presentation and visualization are owned by
`trureturing-pages`, which consumes these data artifacts.

For consumption-only development, emit just the mock ports:

```sh
dotnet run --project src/Trureturing.Paper.Cli -- emit-local-ports \
  --frozen-bundle Papers/frozen-bundle \
  --output Papers/example
```
