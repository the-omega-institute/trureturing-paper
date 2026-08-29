# C-first research-to-paper architecture

## Authority boundary

The C binary owns orchestration mechanics: legal state transitions, exclusive
execution, process-group lifecycle, deadlines, retries, resumable state,
per-stage logs, deterministic artifact hashing, and release gates. It does not
author mathematical truth. Intuition proposes research directions. Lean workers
produce or verify declarations. Existing `trureturing-paper` claim gates remain
the authority for certified claims. LaTeX and SSHX are workers with explicit
receipts.

The checked-in control plane is a development tool. The repository-local FKST
package remains the narrow `observe -> act` publication projection. This PR does
not connect the new control plane to that package, start a daemon, inspect an
implicit sibling checkout, or alter the existing content-addressed truth flow.
An operator must explicitly provide `FKST_FORMALIZATION_ROOT` when local Lean
source correspondence is checked.

## Scientific state graph

```text
inventory
  -> claim_ledger
  -> novelty_gap
       fail -> intuition -> candidate_theorem -> lean_writeback
       pass -------------------------------> candidate_theorem
  -> lean_verify
       fail -> intuition
  -> manuscript_sync
       fail -> lean_writeback
  -> journal_fit
       fail -> intuition
  -> journal_render -> cover_letter_render
  -> review_round_1 -> revision_1
  -> review_round_2 -> revision_2
  -> review_round_3 -> revision_3
  -> journal_rescreen
       fail -> intuition
  -> release_gate
       fail -> intuition
  -> DONE
```

A failed novelty, formalization, or venue decision returns to theorem
development. It cannot be converted into success by changing only the title,
abstract, or target journal.

## Persistent and regenerable state

Persistent research evidence lives under `Papers/research/`: the candidate
statement, claim manifest, nearest-prior-result ledger, journal ledger, review
summaries, and revision receipts. Regenerable run state, logs, intermediate
receipts, and compiled PDFs live under `.fkst-pipeline/` and are ignored by Git.
The event log records stage, attempt, exit code, outcome, output hash, and log
path. Output hashes bind logical repository-relative paths and bytes, so the
same outputs have the same digest on different machines.

## Release conditions

The terminal gate requires:

1. At least two recently checked nearest-prior results with supported theorem
   deltas and boundary analysis.
2. At least one verified formal claim mapped to a declaration in the explicitly
   selected formalization root.
3. At least two recently checked, internally Tier-2-or-better eligible venues.
4. Three distinct SSHX reviewer identities and distinct report hashes.
5. A manuscript hash change between every review round.
6. Round 3 marked `pass` with zero blocking issues.
7. Canonical Lean writeback and verification receipts that report `status=pass`.
8. No unresolved blocking markers in research ledgers, manuscript sources,
   section sources, or the cover letter.
9. Valid, nontrivial manuscript and cover-letter PDFs.

## Safety boundary

The pipeline prepares and audits artifacts. It never submits a manuscript,
sends a cover letter, merges a branch, or changes a remote default branch.
Submission and promotion remain explicit human actions.
