# FKST-native formalization-frontier planning

This boundary turns one exact `promote-to-frontier` portfolio result into a complete dependency-aware formalization frontier without allowing an agent to redefine the admitted theorem package or decide mathematical truth.

## Position in the research pipeline

```text
A3-independent audits and scorecards
  -> cross-paper portfolio judgment
  -> deterministic promote-to-frontier route
  -> FKST-native frontier-planning agent
  -> repository-admitted paper-formalization-frontier.v1
  -> initial paper-formalization-frontier-state.v1
  -> wave-zero governed-selection requests
  -> existing selection, Formalize, outcome, certification, and manifest pipeline
```

Several promoted papers create distinct frontier-planning tasks and can progress concurrently under FKST backpressure. The theorem dependency graph controls concurrency within each paper.

## Promotion-bound source reconstruction

The FKST event is a trigger. It is not trusted as the frontier's evidence source. `stage-frontier-planning-task` accepts only the portfolio judgment task reference and paper identifier, then reopens the immutable portfolio admission cursor at:

```text
work/paper-portfolio-judgments/cursors/<portfolio-task>.json
```

The repository verifies that the cursor contains exactly one route for the paper with:

```text
action     = promote-to-frontier
next_route = frontier-planning
```

It then reopens the original portfolio dispatch and derives the complete frontier evidence closure.

## Exact evidence closure

One frontier-planning dispatch contains exactly thirteen source artifacts:

```text
1.  paper-portfolio-judgment-agent-cursor.v1
2.  paper-portfolio-judgment-agent-dispatch.v1
3.  paper-theory-program.v1
4.  paper-theory-scope.v1
5.  paper-theory-inventory.v1
6.  paper-theorem-package.v1
7.  paper-theory-audit.v1
8.  paper-candidate-scorecard.v1
9.  candidate-paper evidence
10. literature-research evidence
11. paper-portfolio-judgment-evidence.v1
12. paper-portfolio-decision.v1
13. updated paper-research-portfolio.v1
```

The generic `paper-agent-task.v1` additionally binds the immutable frontier-planning dispatch itself. Missing artifacts, digest drift, path traversal, symbolic links, substituted papers, stale portfolio cycles, a held paper, an unpassed A3 audit, a non-promoted scorecard, or an updated portfolio that has not advanced the paper to `frontier-pending` fail closed.

## Agent task

The staged native task is fixed to:

```text
phase         frontier-planning
agent_role    paper-formalization-frontier-planner
context_mode  promotion-bound-planning
sandbox       workspace-write
output        paper-formalization-frontier-draft.v1
path          outputs/formalization-frontier-draft.json
next_route    formalization-frontier
```

The planner must provide one `PaperFormalizationFrontierNodeSpec` for every admitted theorem-package claim. Each specification contains:

- the unchanged claim identifier;
- one formalization role;
- priority from 0 to 100;
- proposed target Lean package and module;
- proposed formal statement;
- a machine-checkable acceptance criterion.

The planner also records a portfolio-bound planning rationale and a risk ledger for missing APIs, hidden assumptions, over-general statements, and likely prerequisite gaps.

The task forbids claim creation, deletion, renaming, weakening, strengthening, merging, splitting, dependency changes, score changes, portfolio-decision changes, Lean execution, Formalize invocation, Base writeback, Git access, and manuscript generation.

## Deterministic authority boundary

The draft is a proposal for how to formalize each already-admitted claim. Repository code remains authoritative for the formalization DAG.

Admission calls the existing `PaperFormalizationFrontierService.CreateFrontier`, which recomputes:

```text
claim set
claim-to-node identity
claim dependencies
node dependency references
parallel wave of every node
main-theorem, sharpness, and corollary role constraints
critical-path depth
maximum wave width
frontier content identity
```

The planner cannot flatten the paper into one isolated lemma, reorder a dependent claim into an earlier wave, replace a theorem with another statement, or relabel the admitted main theorem, sharpness theorem, or corollary.

## Initial state and ready set

After the frontier validates, the repository calls `PaperFormalizationFrontierLifecycleService.CreateInitialState`. Every node begins in:

```text
selection-pending
```

Only nodes with repository-computed `parallel_wave = 0` are emitted as `paper_frontier_node_selection_requested`. Each request carries the exact frontier and initial-state artifacts plus the portfolio promotion closure.

Later waves are not released by the planning agent. They become eligible only through the existing frontier lifecycle after their dependency nodes reach certified or manifested states.

## Storage and replay

Frontier-planning artifacts use content-addressed content and envelope objects below:

```text
artifacts/paper-frontier-planning/
```

The first admitted terminal result creates one immutable cursor:

```text
work/paper-frontier-planning/cursors/<task>.json
```

It fixes the mapping:

```text
task and result
  -> promotion closure
  -> paper-formalization-frontier.v1
  -> paper-formalization-frontier-state.v1
  -> ordered wave-zero selection routes
  -> Codex run identity and provenance
```

At-least-once redelivery reopens and validates the cursor, stored frontier, stored initial state, and initial routes. It does not invoke another planner or create a different frontier.

## Failure routes

A generic `no-progress` or `blocked` result creates no frontier and changes no paper or node state. FKST emits:

```text
paper_frontier_planning_no_progress
paper_frontier_planning_blocked
paper_frontier_planning_retry_requested
```

The typed blocker remains attached to the exact task, result, paper, program, and source portfolio task.

## Contracts

This PR adds strict schemas for:

- `paper-frontier-planning-agent-dispatch.v1`;
- `paper-formalization-frontier-draft.v1`;
- `paper-frontier-planning-agent-task-staged.v1`;
- `paper-frontier-planning-agent-cursor.v1`;
- `paper-frontier-planning-agent-result-admitted.v1`;
- `paper-formalization-frontier-ready.v1`;
- `paper-frontier-node-selection-requested.v1`;
- `paper-frontier-planning-agent-failure.v1`.

## Next boundary

The next independent work item should consume each `paper_frontier_node_selection_requested` event, construct a governed `paper-research-selection.v1` for the exact node, and enter the existing canonical Formalize request path. That adapter must preserve the frontier node, theorem-package claim, dependencies, acceptance criterion, truth-release coordinate, and portfolio promotion evidence without allowing event prose to invent a selection.
