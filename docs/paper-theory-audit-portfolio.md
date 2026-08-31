# Fresh theory audit and cross-paper competition

A paper theorem package does not enter formalization immediately after its authoring worker declares it mature. It first receives independent clean-room review, then competes with the other audited papers in the active portfolio.

## Independent theory audit

```text
paper-theorem-package.v1
  + paper-theory-audit-request.v1
      |
      +-> fresh mathematical referee
      +-> fresh novelty referee
      +-> optional fresh scope or formalization referee
      |
      v
paper-theory-audit.v1
```

The audit request binds the exact theory program, A0 scope, A1 inventory, A2 theorem package, and theory-author run. Every opinion must use:

- a reviewer run distinct from the theory-author run;
- a reviewer run distinct from every other opinion;
- a review session distinct from every other opinion;
- `context_mode = fresh-theory-review`;
- exactly the authorized program, scope, inventory, and theorem-package evidence;
- no previous audit verdict or acceptance history.

At least two opinions are required. The audit does not average them. Every aggregate metric is the coordinate-wise minimum across the opinions, so one optimistic reviewer cannot erase another reviewer's theorem-level concern.

## Calibrated gate

The aggregate thresholds are:

```text
abstraction_quality       >= 8
theorem_depth             >= 8
logical_closure           >= 8
proof_plausibility        >= 8
novelty                   >= 7
significance              >= 7
formalization_readiness   >= 7
journal_floor             >= 7
overlap_hygiene           >= 8
```

The audit passes only when every opinion says `pass`, no blocker or required revision remains, and all aggregate metrics reach their threshold. Other possible routes are `deepen`, `split`, `merge`, `park`, and `archive`.

The review remains upstream of Lean. It assesses whether a coherent theorem package deserves formalization effort. It does not run Lean, dispatch Formalize, certify claims, or write the manuscript.

## Candidate scorecard

Every audited paper receives `paper-candidate-scorecard.v1`. The score is:

```text
abstraction_quality
+ 2 * theorem_depth
+ 2 * logical_closure
+ proof_plausibility
+ 2 * novelty
+ 2 * significance
+ formalization_readiness
+ journal_floor
+ overlap_hygiene
```

The maximum is 130. The score ranks papers only after the hard theory gate. A high weighted score cannot make a failed audit eligible for promotion.

## Portfolio competition

`paper-portfolio-decision.v1` compares at least two audit-pending papers from the same portfolio. It sorts them by composite score, then by stable paper identity. The policy supplies a bounded promotion capacity.

For example:

```text
rank 1  paper A  passed  -> promote-to-frontier
rank 2  paper B  passed  -> promote-to-frontier
rank 3  paper C  passed  -> hold
rank 4  paper D  failed  -> continue-deepening
rank 5  paper E  failed  -> split
```

Passed papers beyond the capacity remain `hold` and can compete again in the next cycle. Failed papers preserve their audit route instead of being collapsed into a generic rejection.

Actions update paper state as follows:

```text
promote-to-frontier -> frontier-pending
hold                -> audit-pending
continue-deepening  -> theory-deepening
split               -> theory-deepening with split work required
merge               -> theory-deepening with merge work required
park                -> parked
archive             -> archived
```

## Two-dimensional architecture

The resulting research system has two independent dimensions:

```text
within each paper:
  definition -> lemmas -> structural theorem -> main theorem
             -> sharpness theorem -> corollaries

across papers:
  paper A | paper B | paper C | paper D | paper E
       concurrent theory work and periodic portfolio competition
```

The first dimension produces mathematical depth. The second allocates research capacity across multiple papers, prevents one manuscript from monopolizing all workers, and promotes the strongest mature theorem packages first.
