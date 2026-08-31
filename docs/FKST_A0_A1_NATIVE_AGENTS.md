# FKST-native A0 scope and A1 inventory agents

## Purpose

This lane turns the first two theory phases from passive contracts into executable FKST work. The Paper package receives one immutable domain dispatch, creates a phase-owned Codex task, runs it through the native agent runtime, and re-admits the returned draft through the existing deterministic A0 or A1 validator.

```text
paper_theory_scope_requested
        or
paper_theory_inventory_requested
        |
        v
dispatch-theory-foundation-agent
        |
        | stage-foundation-task
        | register-task
        v
paper_agent_task_requested
        |
        v
run-codex-agent
        |
        | spawn_codex_sync
        v
paper_agent_task_completed
        |
        v
admit-theory-foundation-agent
        |
        +--> paper_theory_scope_ready
        |
        +--> paper_theory_inventory_ready
```

No-progress and blocked results use a separate fail-closed route:

```text
paper_agent_task_no_progress | paper_agent_task_blocked
        |
        v
route-theory-foundation-agent-failure
        |
        +--> paper_theory_scope_no_progress
        +--> paper_theory_scope_blocked
        +--> paper_theory_inventory_no_progress
        +--> paper_theory_inventory_blocked
```

## Immutable dispatch

`paper-theory-foundation-agent-dispatch.v1` binds one of two kinds:

```text
scope
inventory
```

It carries:

- the paper ID;
- the exact theory-program reference;
- the exact A0 or A1 request reference;
- every content-addressed input required by that request contract;
- the request timestamp.

The program, request, approved scope, candidate evidence, literature evidence, Intuition evidence, and exact Paper research input are passed as canonical content files. The bridge recomputes every digest and verifies that the dispatch input-ref set equals the domain request's closed `exact_input_refs` set plus the request itself.

A worker cannot add a convenient source, drop negative evidence, substitute another program, or move the request to a different paper.

## Phase-owned task construction

The dispatch Department does not accept a free-form agent task. Repository code reconstructs it from the validated domain request.

A0 is fixed to:

```text
phase         theory-scope
agent_role    paper-theory-scope-author
context_mode  exact-program-scope
output        outputs/scope-draft.json
route         theory-inventory | theory-scope | blocked
```

A1 is fixed to:

```text
phase         theory-inventory
agent_role    paper-theory-inventory-auditor
context_mode  scope-bound-review
output        outputs/inventory-draft.json
route         theory-deepening | theory-inventory | blocked
```

The scientific task, pass conditions, fail conditions, and forbidden shortcuts are copied from the validated `PaperCodexPhaseContract`. The generated task also tells the model that final domain IDs belong to repository validation.

## Draft and final artifact separation

Codex may write only one intermediate domain draft:

```text
paper-theory-scope-draft.v1
paper-theory-inventory-draft.v1
```

The draft contains scientific content and exact parent references. It does not contain a self-declared `scope_id` or `inventory_id`.

After the generic Paper agent runtime validates the task-level result, the foundation admission bridge:

1. re-reads the immutable task, result, output, and dispatch;
2. reconstructs the expected task from the domain request and compares canonical bytes;
3. checks the completed route;
4. deserializes the draft with strict JSON rules;
5. binds it to the exact program, request, and approved scope;
6. invokes `PaperTheoryFoundationService.CreateScope` or `CreateInventory`;
7. stores canonical domain content under its computed SHA-256 ID;
8. stores the full domain envelope separately;
9. writes a replayable admission cursor.

Thus the model proposes the theory content. Repository code decides whether it is a valid A0 or A1 object and owns canonical identity.

## A0 domain gate

The final `paper-theory-scope.v1` must contain:

- a nonempty research question;
- a canonical abstraction target;
- a publication floor;
- explicit in-scope theorem obligations;
- supporting and out-of-scope boundaries;
- a split policy;
- at least one counterexample obligation.

Changing the paper, program, request, or timestamp ordering fails admission.

## A1 domain gate

The final `paper-theory-inventory.v1` must contain at least three claims, including at least two theorem-like claims and one internal dependency edge. Every dependency must resolve, the graph must be acyclic, and every main theorem ID must resolve to a theorem item.

The inventory must also preserve:

- missing proof interfaces;
- stronger variants;
- weaker variants;
- counterexample obligations;
- an actionable next operation for every claim.

A single isolated theorem, unresolved dependency, cycle, or relabelled proof gap is rejected after Codex returns.

## Replay and concurrency

The generic agent cursor prevents the same task from launching Codex twice. The foundation admission cursor prevents the same task result from being rebound to another scope or inventory.

Different papers have different dispatch and task references and may execute concurrently. A0 and A1 remain ordered within one paper because A1 requires the exact admitted A0 scope reference.

```text
Paper A A0  | Paper B A1 | Paper C A0
     run concurrently under FKST backpressure
```

## Boundary after this PR

This lane closes native execution for A0 and A1. A2 theory deepening, multi-agent A3 audit, live literature acquisition, journal research, manuscript writing, editing, proofreading, and SSHX review remain separate subsequent integrations. The next scientific integration should connect `paper-theory-deepening-request.v1` to the same native runtime while retaining the anti-fake progress gate and theorem-package validator.
