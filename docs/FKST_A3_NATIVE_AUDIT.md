# FKST-native A3 independent theory audit

A3 is a clean-room multi-agent review stage for an immutable `audit-candidate` theorem package. It does not edit theory and it does not promote a paper by itself.

## Execution graph

```text
paper_theory_audit_requested
    -> stage-audit-tasks
    -> mathematical-referee task ----+
    -> novelty-referee task ----------+--> run-codex-agent / spawn_codex_sync
                                      |
                                      +--> opinion admission
                                           -> waiting until all planned tasks arrive
                                           -> coordinate-wise minimum aggregate
                                           -> paper-theory-audit.v1
                                           -> paper-candidate-scorecard.v1
```

Every reviewer is a separate `paper-agent-task.v1` and therefore a separate FKST reliable delivery. Reviewers may run concurrently with each other and with reviewers for other papers.

## Clean-room context closure

The domain audit request binds the theory program, A0 scope, A1 inventory, and A2 theorem package. The native reviewer task additionally materializes the candidate paper, literature research, originating Intuition proposal, exact Paper research input, and immutable audit request. No prior audit opinion, aggregate verdict, scorecard, or portfolio decision may enter the task.

The dispatch input set is compared exactly against this closure. Missing literature and hidden prior-review context both fail closed.

## Required reviewer roles

Every plan contains distinct, contiguous reviewer slots and at least:

```text
mathematical-referee
novelty-referee
```

Optional scope and formalization referees may be added. Reviewer roles cannot repeat inside one plan.

The mathematical referee reconstructs load-bearing proof interfaces and tests assumptions, converses, counterexamples, and logical closure. The novelty referee compares theorem-level hypotheses and conclusions against supplied literature and sibling evidence, then assesses significance and overlap.

## Freshness and independence

A reviewer task is fixed to:

```text
phase         theory-audit
agent_role    paper-theory-independent-referee
context_mode  fresh-theory-review
```

Admission requires a nonempty Codex `run_id`. The repository derives `reviewer_run_ref` from that run ID and derives a separate `review_session_ref` from the task plus run ID. Aggregation rejects reused run IDs, run references, session references, reviewer roles, and any run identity equal to the theory-author run reference.

Generic task replay is allowed. It reuses the already validated result of the same reviewer task and does not create a second opinion.

## Conservative aggregation

Each opinion scores:

```text
abstraction quality
theorem depth
logical closure
proof plausibility
novelty
significance
formalization readiness
journal floor
overlap hygiene
```

The aggregate takes the coordinate-wise minimum. Blockers and required revisions are unioned. A paper passes only when every opinion says `pass`, the blocker ledger is empty, and every minimum metric reaches its repository threshold.

One skeptical reviewer therefore controls the weak dimension. Scores are never averaged upward.

## Outputs and routing

A completed plan produces:

```text
paper-theory-audit.v1
paper-candidate-scorecard.v1
```

Routes are typed:

```text
pass     -> portfolio-judgment
deepen   -> theory-deepening
split    -> portfolio-split
merge    -> portfolio-merge
park     -> parked
archive  -> archived
```

`promotion_eligible` is computed by the scorecard service and is true only for a passed audit-candidate package. The next portfolio stage still compares several papers and applies promotion capacity.

A reviewer `no-progress` or `blocked` result carries no opinion artifact. FKST emits a replacement request while the aggregate remains incomplete.

## Replay and concurrency

The review plan, each admitted opinion, and the final aggregate have independent immutable cursors. Reviewer results may arrive in any order. The first opinion yields `waiting`; the last required opinion deterministically creates the audit and scorecard. Concurrent attempts to finalize the same plan converge on one aggregate cursor.

This stage can review several papers at once because plans, tasks, workspaces, and cursors are keyed by paper-specific content hashes. It does not serialize the portfolio behind one paper.

## Identity and filesystem boundaries

Domain objects and raw files use separate identity functions. Canonical object content is hashed after canonical JSON serialization. Exact evidence files, staged tasks, and immutable envelopes are verified against the SHA-256 digest of their existing bytes. This prevents overload resolution or reserialization from changing the meaning of a stored artifact reference.

Reviewer task paths may point only into the deployment-owned `inbox/agent-tasks` tree. Evidence paths remain restricted to approved repository roots, and every traversed path is checked for symbolic links before reading.
