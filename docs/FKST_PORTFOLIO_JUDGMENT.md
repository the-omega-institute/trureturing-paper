# FKST-native cross-paper portfolio judgment

Portfolio judgment is the first stage that evaluates several independently audited papers together. It allocates scarce formalization capacity across the batch. It does not alter theorem packages, audit metrics, or mathematical truth.

## Workflow

```text
paper A A3 audit -> scorecard A --+
paper B A3 audit -> scorecard B --+--> paper_portfolio_judgment_requested
paper C A3 audit -> scorecard C --+              |
                                                  v
                                  one FKST-native portfolio judge
                                                  |
                                                  v
                                  pairwise theorem and novelty evidence
                                                  |
                                                  v
                                  deterministic repository admission
                                                  |
                   +------------------------------+------------------+
                   |                              |                  |
                   v                              v                  v
          promote-to-frontier             continue-deepening     hold/split/merge/
                                                               park/archive
```

The agent performs comparative reasoning. Repository code owns every final state transition.

## Exact comparison closure

One judgment compares two to five papers from one immutable portfolio cycle. The task receives:

- the exact research portfolio and candidate batch;
- each paper's theory program;
- admitted A0 scope;
- admitted A1 inventory;
- A2 theorem package;
- independent A3 audit;
- calibrated scorecard;
- candidate-paper evidence;
- literature-research evidence.

Every referenced paper must be `audit-pending` in the same portfolio. Program, scope, inventory, theorem package, audit, scorecard, candidate, literature, batch, truth release, and topology coordinates must close exactly. Prior portfolio decisions are excluded.

## Agent responsibility

The FKST `portfolio-judgment` profile uses:

```text
agent_role   paper-portfolio-judge
context      cross-paper-comparison
sandbox      workspace-write
output       paper-portfolio-judgment-draft.v1
```

The judge must:

1. rank all papers without placing a lower calibrated score above a higher score;
2. use theorem-level evidence to resolve exact-score ties;
3. explain each paper's comparative advantage and principal risk;
4. classify every unordered pair as `distinct`, `complementary`, `overlapping`, or `duplicate`;
5. cite pairwise evidence only from the two papers being compared;
6. preserve the A3 route of every failed paper;
7. mark only the first eligible papers inside the declared promotion capacity as `promote`;
8. mark eligible overflow as `hold`.

The judge cannot invent a theorem, edit a score, introduce external literature, or decide truth.

## Deterministic admission

The draft is evidence, not authority. Repository code reopens all scorecards and computes:

```text
eligible and inside capacity -> promote-to-frontier
eligible overflow            -> hold
failed A3                     -> scorecard's typed route
```

The repository then creates the canonical `paper-portfolio-decision.v1`, applies each decision to the matching `paper-candidate-state.v1`, increments the portfolio cycle, and stores the updated `paper-research-portfolio.v1`.

Promotion capacity is therefore a hard bound. A high-scoring failed paper cannot be promoted. Agent prose cannot change a scorecard route. Stable ranking is required across unequal composite scores, while exact ties may use admitted comparative evidence.

## Pairwise overlap evidence

For `n` papers the draft must contain exactly `n(n-1)/2` pairwise relations. Each unordered pair appears once.

```text
distinct       separate theorem and novelty increments
complementary  independent papers with reusable interfaces
 overlapping    material theorem or novelty overlap, with one preferred owner
 duplicate      one paper should own the duplicated contribution
```

Overlap and duplicate findings are recorded for later split or merge governance. They do not silently merge papers inside the judgment stage.

## Routes

```text
promote-to-frontier -> paper_formalization_frontier_requested
hold                -> paper_candidate_held
continue-deepening  -> paper_theory_deepening_requested
split               -> paper_candidate_split_requested
merge               -> paper_candidate_merge_requested
park                -> paper_candidate_parked
archive             -> paper_candidate_archived
```

Every route carries the same decision, updated portfolio, judgment-evidence, paper, program, and scorecard references.

## Replay and failure

The generic Paper agent cursor prevents duplicate Codex execution. A second immutable admission cursor fixes:

```text
task + agent result
    -> comparison evidence
    -> portfolio decision
    -> updated portfolio
    -> per-paper routes
```

A repeated delivery replays the same artifacts. A different decision cannot replace the first admitted decision for the same task.

`no-progress` and `blocked` results produce no decision and no paper route. They emit a typed failure and a portfolio-judgment retry request. Existing A3 scorecards remain unchanged.

## Parallel portfolio semantics

A portfolio judge compares a bounded batch after A3. Other FKST workers remain free to advance unrelated papers through A0, A1, A2, another A3 review, formalization, or certification. The judgment stage does not serialize the whole research organization behind one paper.
