# trureturing-paper (host package)

The `trureturing-paper` host package is the paper organ's fkst lifecycle: it
assembles the paper for the blessed frozen bundle the repo pins, as an
`observe → act` event chain. It is a read-and-assemble consumer — the paper's
statement faithfulness and non-fabrication are enforced by the assembler's claim
gate, and it never authors upstream truth.

## Event chain

```
raisers/blessed_input.lua   file_watch Papers/frozen-bundle/source-snapshot.v1.json
      │  paper_snapshot_seen { path }
      ▼
departments/observe         compare the blessed truth_graph_sha256 against the
      │                     publications ledger; raise only if unpublished
      │  paper_reproject     (dispatch folded in — one assembly per snapshot)
      ▼
departments/act (terminal)  dotnet run … assemble (claim gate inside) → Papers/paper.tex,
                            then append a publication receipt — assembly and its
                            durable record are one atomic step
```

`act` is terminal: the assembly **and** its receipt happen in one department, so a
superseding blessing cannot cause a downstream lane to lose a receipt.

## Faithfulness (第Ⅵ节) and the claim gate

The assembler (`src/Trureturing.Paper.Cli assemble`) builds the theorem statement
from the frozen ledger's own structured AST, so the paper reproduces the theorem
verbatim (`\operatorname{trace}(\operatorname{conj}(x)) = \operatorname{trace}(x)`),
never a degraded `x = x`. Its claim gate rejects a recipe that claims a GID absent
from the frozen ledger (nonzero exit). act treats that nonzero exit as a real
failure (fail-loud), so a fabricated claim never produces a receipt.

## Fact-source discipline (engine §6)

Durable truth is only ever an explicit host filesystem file, never `<RT>/marks` or
`cache`:

- **Input** is the committed frozen bundle `Papers/frozen-bundle/` (CLI-named
  `source-snapshot.v1.json` + `.sha256`, `frozen-truth.v1.json`, `blueprints.v1.json`)
  and `Papers/recipe.v1.json`.
- **Dedup key** is the source-derived `truth_graph_sha256`, checked against the
  append-only publications ledger `Papers/publications.jsonl` (an explicit host
  file; `file.write` does not itself create a git commit). `Papers/paper.tex` is a
  regenerable assembly output and is gitignored.

## Reliability under at-least-once delivery

- **Fail loud.** A nonzero assemble exit (incl. claim-gate rejection) or a missing/
  empty tex raises, so the child exits nonzero for reliable retry / DLQ.
- **Obsolete trigger → ack-drop.** If the bundle blessing has advanced past the
  event digest, act drops the trigger (the current blessing has its own trigger).
- **No stale record.** act re-checks the bundle digest *after* the (slow) assemble;
  if the bundle moved during it, act records nothing for the stale trigger.
- **Idempotent receipt.** The ledger append is dedup'd by digest under `with_lock`.

## Gates

Both green as committed (14 unit tests over `core.lua`; 7/7 conformance). An
end-to-end `fkst-framework run` smoke asserted, against the real frozen bundle:
observe raises when unpublished, act assembles a faithful `\operatorname{trace}`
tex and records exactly one receipt, a double delivery stays at one receipt, an
obsolete trigger acks without a receipt, and a fabricated-GID recipe makes act exit
nonzero with no receipt (claim-gate fail-loud). `dotnet test` for the assembler
remains 3/3.
