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

## Explicitly deferred

Still deferred: recipe-validation-first, topic selection, research-loop automation,
citation/evidence rendering, PDF production, and arXiv packaging. The fkst package below is
directory shape only; its lifecycle programs and trusted verifier remain TBD.
