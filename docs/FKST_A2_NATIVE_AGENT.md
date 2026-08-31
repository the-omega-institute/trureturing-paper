# FKST-native A2 abstract-theory deepening

## Purpose

A2 is the first phase that changes the mathematical content of a paper candidate. It receives one exact theory program, its admitted A0 scope, its admitted A1 theorem inventory, and at most one immediately prior theorem package. It asks one FKST-owned Codex run to deepen the abstract theory for one bounded round.

The phase precedes Lean and manuscript writing. Its product is a stronger informal theorem package with a checkable proof architecture, novelty boundary, sharpness or counterexample analysis, and portfolio research routes.

## Event chain

```text
paper_theory_deepening_requested
  -> dispatch-theory-deepening-agent
  -> paper-theory-deepening-agent-dispatch.v1
  -> phase-owned paper-agent-task.v1
  -> paper_agent_task_requested
  -> run-codex-agent
  -> spawn_codex_sync
  -> paper-theory-deepening-draft.v1
  -> generic Paper agent result admission
  -> admit-theory-deepening-agent
  -> domain validation and repository-computed delta
  -> paper_theory_deepening_ready
```

The success lane may also publish content-addressed split proposals, merge-research requests, and research-ledger entries. No-progress and blocked results use separate typed queues.

## Exact input closure

Round one binds exactly:

```text
paper-theory-program.v1
paper-theory-scope.v1
paper-theory-inventory.v1
paper-theory-deepening-request.v1
```

A later round adds exactly one immediately prior `paper-theorem-package.v1`. The repository reconstructs the A2 request with `PaperTheoryDeepeningService.CreateDeepeningRequest` and requires canonical equality with the supplied request. The dispatch input-reference set must equal the request contract closure plus the request itself.

This rejects stale scopes, substituted inventories, skipped package versions, cross-paper evidence, hidden extra context, and missing negative evidence.

## Phase-owned Codex role

The bridge constructs a generic task with fixed settings:

```text
phase         theory-deepening
agent_role    paper-theory-developer
context_mode  contextual-theory-execution
sandbox       workspace-write
output        paper-theory-deepening-draft.v1
path          outputs/theory-deepening-draft.json
timeout       repository profile, currently 7200 seconds
```

The agent cannot change these settings. It cannot run Lean, invoke Formalize, certify claims, write Base, select a journal, or write manuscript prose.

## Draft bundle

The one draft contains:

```text
iteration draft
post-iteration theorem-package draft
zero or more split-proposal drafts
one or more research-ledger drafts
```

The iteration identifies changed, new, strengthened, and retired claims. It supplies a multi-step proof spine, novel increment, prior-work boundary, counterexample findings, and portfolio split or merge candidates.

The theorem package contains the complete post-round theorem DAG. An `audit-candidate` must have no open proof obligations, at least one corollary, at least one sharpness claim, and complete informal or certified proofs for every load-bearing claim.

## Repository-computed anti-fake delta

Agent-provided progress counters are treated as claims to verify. The repository compares the returned theorem package against the exact A1 inventory in round one, or against the immediately prior theorem package in later rounds.

It computes:

```text
actual new theorem-like claims
actual materially strengthened theorem-like claims
actual retired claims
actual changed claim set
actual dependency edges added
actual proof obligations closed
actual counterexample or sharpness resolutions
actual abstraction change
actual novelty-boundary change
```

The computed values must agree with the iteration. Admission also requires progress in at least three substantive dimensions, including at least one new or strengthened theorem-like claim, at least one proof closure, and at least one structural, abstraction, novelty, or counterexample change.

The following cannot pass as A2 progress:

```text
renaming claims
rewriting prose while preserving the theorem system
changing only status or priority fields
adding an isolated easy lemma
reporting counters unsupported by the returned package
claiming proof closure without a complete proof outline
changing novelty wording while retaining the same mathematical boundary
```

A successful comparison produces `paper-theory-deepening-delta.v1`, whose identity is computed by repository code.

## Portfolio routes

A split proposal is admitted only when the extracted claims exist in the new theorem package, contain a theorem-like result, and form an independent question with a proof spine. The iteration's `split_candidate_claim_ids` must equal the claims covered by admitted split proposals.

A merge candidate is emitted as a request for separate cross-paper research. A2 cannot construct a canonical merge from one paper's output because validation requires both theorem packages.

Every A2 round must record a `prior-work-boundary` ledger entry. Split, merge, and counterexample routes require matching typed ledger entries. This prevents an agent from silently expanding or changing the portfolio.

## Typed routes

```text
developing theorem package
  -> paper_theory_deepening_ready
  -> next_route = theory-deepening

audit-candidate theorem package
  -> paper_theory_deepening_ready
  -> next_route = theory-audit

no-progress
  -> paper_theory_deepening_no_progress
  -> next_route = theory-deepening

blocked
  -> paper_theory_deepening_blocked
  -> next_route = blocked
```

The completed route is derived from admitted maturity. Event text cannot promote a developing package to A3.

## Replay and parallelism

The generic agent cursor makes Codex execution idempotent. The A2 admission cursor makes the mapping from task and result to iteration, theorem package, computed delta, split proposals, ledger entries, and route immutable.

Different paper programs have distinct dispatch and task references and may execute concurrently under FKST backpressure. Each paper retains its own round sequence. A later round cannot skip or replace its immediately prior theorem package.

## Boundary after this PR

A2 produces an `audit-candidate`. It does not approve that package. The next Paper PR should execute at least two fresh A3 reviewer agents, aggregate their independent scorecards conservatively, and allow portfolio promotion only after every hard audit threshold and blocker rule passes.
