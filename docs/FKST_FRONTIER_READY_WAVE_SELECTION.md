# FKST dependency-ready frontier-wave selection

This boundary consumes the dependency-ready evidence produced by the frontier certification lifecycle and turns every newly ready claim into a repository-governed Paper selection and canonical Formalize request.

## Scope

PR #32 creates the complete theorem frontier. PR #33 admits the initial independent roots. PR #34 records Formalize outcomes, descendant-release certification, claim manifests, and content-addressed ready sets. This boundary closes the scheduling loop for later dependency waves.

```text
frontier claim manifested
  -> paper-frontier-ready-set.v1
  -> paper_frontier_ready_set_ready
  -> ready-wave admission
  -> governed Paper selection per node
  -> canonical Formalize request per node
  -> existing Formalize transport and certification
  -> next ready set
```

No new scientific agent runs here. The theorem package, frontier DAG, formal statements, dependency relations, target modules, priorities, and acceptance criteria have already passed A0, A1, A2, A3, portfolio judgment, and frontier planning.

## Ready-set authority

A later-wave node can be selected only when one immutable `paper-frontier-ready-set.v1` lists it. The ready set must be backed by exactly one frontier certification cursor. The service reopens and validates:

- the certification cursor;
- the certified frontier manifest that triggered the ready set;
- the frontier state recorded by that certification;
- the request-indexed binding of the trigger node;
- the original frontier-planning admission and promotion evidence closure.

The event payload is only a trigger. It cannot add a node, alter its claim, change its wave, or substitute a state.

For each listed node, the release state must prove:

```text
node status = selection-pending
node has at least one dependency
every dependency status = manifested
route = governed-selection
route identity = frontier node identity
```

An initial-wave route and a dependency-ready-set route are distinct authorization kinds. Wave-zero roots continue to use the original frontier-planning admission. Every later node uses the exact ready set that first released it.

## Batch admission

One ready set may contain several independent claims. The repository processes the set in its deterministic order:

```text
parallel wave ascending
priority descending
node identity ascending
```

For every node, the existing governed selection service creates:

- `paper-frontier-node-selection-authorization.v1`;
- `paper-frontier-verification-budget.v1`;
- `paper-research-selection.v1`;
- `formalization-request.v1`;
- the governed-selection frontier event;
- the canonical-request frontier event;
- the request-indexed frontier binding.

The resulting node state is `request-recorded`. Each canonical request is then raised through the existing `formalization_request_ready` queue.

## Dependency APIs

For a later-wave node, the selection derives machine-addressable dependency GIDs from the certified frontier dependencies.

```text
frontier dependency nodes
  -> deterministic dependency GIDs
  -> selection.target.known_dependencies
  -> selection.reuse_api
  -> Formalize request
```

Literature citations remain allowed assumptions. They are not presented to Formalize as reusable Base declarations.

The canonical-request lifecycle gate independently rechecks that every dependency is certified or manifested in the current frontier state. Ready-set evidence therefore cannot bypass a later state regression or scientific backroute.

## Successive waves

The loop is recursive across the theorem DAG.

```text
wave 0 definition manifested
  -> wave 1 reduction lemma ready
  -> wave 1 selection and request
  -> wave 1 manifested
  -> wave 2 main theorem ready
  -> wave 2 selection and request
  -> wave 2 manifested
  -> wave 3 corollary ready
```

A separate paper frontier can progress concurrently. Inside one paper, several claims from the same ready set can also be admitted as one batch while frontier state mutation remains serialized.

## Replay and partial recovery

`paper-frontier-ready-wave-selection-cursor.v1` binds one ready set to the complete ordered list of admitted node selections and Formalize requests.

A replay:

1. reopens the original certification and ready-set authority;
2. revalidates the current dependency state;
3. replays every node selection through its immutable node cursor;
4. compares authorization, budget, selection, request, binding, GID, and resulting state references;
5. returns the same batch admission.

If a process stops after some node cursors are committed but before the batch cursor is written, the next delivery replays those nodes, admits the remaining nodes, and writes the unique batch cursor. Current-state recovery from PR #33 and PR #34 prevents the batch from forking frontier history.

A ready set cannot be rebound to another frontier, paper, theory program, theorem package, trigger manifest, source state, or list of requests.

## FKST wiring

The `select-frontier-ready-wave` department consumes:

```text
paper_frontier_ready_set_ready
```

It invokes the repository-local command:

```text
admit-frontier-ready-wave
  --repository-root <path>
  --frontier-ref <sha256:...>
  --ready-set-ref <sha256:...>
```

The department emits one batch receipt:

```text
paper_frontier_ready_wave_selection_ready
```

and, for every admitted node, the existing events:

```text
paper_frontier_node_selection_ready
formalization_request_ready
```

The department does not invoke Codex, Lean, Git, or `dotnet run`. FKST owns delivery, locking, retry, and backpressure. The repository owns ready-set validation, deterministic selection, canonical request construction, state transitions, and replay.

## Authority boundary

```text
A0-A3 and portfolio judgment
  establish a publishable theorem package

frontier planning
  establishes the complete claim DAG

truth-release certification
  establishes which dependencies are manifested

ready-set service
  establishes which later claims may now be selected

selection service
  establishes exact Paper and Formalize request artifacts
```

No event, agent, or transport response can invent a later-wave claim or release it before its certified dependencies are manifested.
