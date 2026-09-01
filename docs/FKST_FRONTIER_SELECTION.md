# FKST-native governed frontier selection

## Purpose

A promoted theorem package reaches formalization through a dependency-aware frontier. The frontier planner decides how every admitted claim should be represented as a formalization node, while the repository computes node identity, dependencies, parallel waves, and the initial ready set.

This boundary begins after frontier planning. It consumes one exact `paper_frontier_node_selection_requested` event and creates the canonical Paper selection and Formalize request for the admitted frontier node.

```text
paper_frontier_node_selection_requested
  -> reopen frontier-planning admission cursor
  -> verify the original frontier-planning dispatch and all exact inputs
  -> reopen the admitted frontier and initial state
  -> locate the exact released wave-zero node
  -> create repository-owned authorization
  -> create repository-owned verification budget
  -> derive paper-research-selection.v1
  -> derive formalization-request.v1
  -> record governed-selection event
  -> record canonical-formalization-request event
  -> advance one serialized frontier state lineage
  -> emit formalization_request_ready
```

No agent runs at this boundary. The theorem package, frontier, selection, and request already contain enough admitted information for deterministic repository computation.

## Source reconstruction

The route event is a trigger. It is not accepted as the source of scientific truth.

The admission service reopens:

1. the content-addressed frontier-planning admission cursor;
2. the content-addressed frontier-planning dispatch;
3. every one of the dispatch's thirteen exact inputs;
4. the admitted formalization frontier;
5. the admitted initial frontier state;
6. the exact theory program;
7. the exact theorem package;
8. the content-addressed Paper research input.

It then verifies one continuous identity chain:

```text
portfolio promotion
  -> frontier-planning task and result
  -> frontier-planning dispatch
  -> admitted program and theorem package
  -> admitted formalization frontier
  -> admitted initial state
  -> released node route
  -> exact Paper research input
  -> exact truth release and topology
```

The node must appear in `initial_node_routes`, have `parallel_wave = 0`, have no dependencies, and retain `next_route = governed-selection`. A dependent node cannot be selected merely by constructing a plausible event payload.

Every exact input file is re-read and checked against its recorded SHA-256 reference. A stale, omitted, replaced, or modified source artifact fails before selection creation.

## Repository authorization

Each admitted node receives a content-addressed `paper-frontier-node-selection-authorization.v1`. It binds:

- the frontier-planning task, result, and dispatch;
- the formalization frontier and initial state;
- the paper, theory program, theorem package, and portfolio decision;
- the released route order;
- the node ID and claim ID;
- the formalization role, parallel wave, and priority.

The authorization ID becomes `selected_by` in the canonical Paper selection. Approval is therefore a reproducible repository artifact rather than free-form event metadata.

## Verification budget

The repository creates one `paper-frontier-verification-budget.v1` for the node. Version 1 fixes the following policy:

```text
maximum formalization rounds       8
exact truth release required       true
certified dependencies required    true
counterexample is useful           true
missing prerequisite is reportable true
```

The budget ID becomes `verification_budget_ref` in the Paper selection. An event producer cannot silently weaken failure semantics or remove the exact-release requirement.

## Deterministic selection

The service constructs `paper-research-selection.v1` from admitted artifacts. It does not copy scientific prose from the route event.

The selection binds:

- truth release digest;
- topology digest;
- Paper research input reference;
- intuition proposal reference;
- candidate-paper reference;
- literature-research reference;
- paper ID;
- exact frontier formal statement;
- dependency GIDs;
- admitted known-result assumptions;
- statement and dependency digests in the claim boundary;
- the frontier acceptance criterion as expected contribution;
- verification budget and authorization identities.

The selection refuses values that exceed the existing canonical selection contract. It does not truncate statements, assumptions, or acceptance criteria.

## GID derivation

The preferred GID is derived from the admitted target Lean module and claim ID.

For modules already written as a canonical `D*/S*` path, that path is retained. Other module names are placed under the neutral Paper namespace:

```text
Trureturing.Base.DescentObject + def:object
  -> D0/S0/Paper/Trureturing/Base/DescentObject.def_object
```

Only characters accepted by the existing Formalize GID contract survive normalization. The generated GID is included in the selection, canonical request, binding, and admission cursor.

## Canonical Formalize request

The existing `PaperResearchSelectionService.BuildFormalizationRequest` remains the only request constructor. It joins the deterministic selection to the exact Paper research input and produces `formalization-request.v1` with:

- exact source repository;
- source commit and tree;
- truth release digest;
- paper and research-candidate context;
- statement, desired generality, dependencies, assumptions, and forbidden weakenings;
- reuse APIs;
- explicit failure semantics.

The existing `formalization_request_ready` event and `dispatch-formalization` department are reused. This PR does not create a second Formalize transport.

## Semantic identity and blob identity

Paper selection and Formalize request identities are semantic content IDs. Their complete JSON envelopes have separate blob IDs because the envelopes also contain their semantic IDs.

The admission cursor records both:

```text
selection_ref       semantic selection identity
selection_blob_ref  exact JSON bytes
request_ref         semantic Formalize request identity
request_blob_ref    exact JSON bytes
```

The downstream Formalize transport receives both canonical files and verifies semantic identity, blob bytes, truth release, source commit, source tree, paper context, candidate context, and GID.

## Frontier lifecycle

Successful admission creates two immutable `paper-formalization-frontier-event.v1` artifacts:

1. `governed-selection`, addressing the exact selection ID;
2. `canonical-formalization-request`, addressing the exact request ID and naming the selection event as predecessor.

The two events are applied through `PaperFormalizationFrontierLifecycleService`. The selected node therefore advances:

```text
selection-pending
  -> selection-recorded
  -> request-recorded
```

The service cannot directly mark the node transported, proved, certified, or manifested.

## Concurrent wave-zero nodes

A frontier may release several independent nodes in one wave. Each node event therefore cannot update an isolated copy of the initial state.

The boundary uses two layers of serialization:

- an FKST `with_lock` keyed by frontier ID;
- a repository file lock keyed by frontier ID.

A `paper-frontier-current-state-cursor.v1` points to the latest immutable frontier state. Each node admission reads that state, appends its two events, stores a new content-addressed state, and atomically advances the pointer.

For two independent wave-zero nodes, the versions advance as follows:

```text
initial state                     version 0
node A governed selection         version 1
node A canonical request          version 2
node B governed selection         version 3
node B canonical request          version 4
```

This prevents parallel nodes from creating divergent state branches.

## Replay and crash recovery

Each node has one immutable admission cursor keyed by frontier ID and node ID. Replay reconstructs the full source closure and regenerates:

- authorization;
- verification budget;
- Paper selection;
- canonical Formalize request;
- lifecycle event bindings;
- frontier formalization binding.

Canonical bytes must match the first admission.

If a node cursor was written before the mutable current-state pointer advanced, replay repairs the pointer after checking event-lineage inclusion. If later nodes have already advanced the frontier, replay of an earlier node leaves the later state intact. Equal-version states with different identities are rejected.

## Formalization binding

`paper-frontier-formalization-binding.v1` joins the downstream request back to:

- frontier-planning task and result;
- frontier and node;
- theorem claim;
- authorization and verification budget;
- selection and request;
- both lifecycle events;
- truth release, source commit, source tree, and GID.

A request-indexed lookup is written at:

```text
work/paper-frontier-formalization-bindings/by-request/<request>.json
```

The next boundary can therefore admit a Formalize dispatch or result without trusting event prose to identify the originating frontier node.

## Failure boundary

Selection admission fails closed when any of the following occurs:

- the node was not released by the admitted frontier planner;
- the node has dependencies in this wave-zero boundary;
- route, node, theorem claim, or formalization role differs;
- program, theorem package, frontier, state, or portfolio decision differs;
- any exact input no longer matches its digest;
- Paper research input and frontier truth or topology differ;
- canonical selection constraints would require truncation;
- an existing node cursor differs from deterministic replay;
- frontier state histories diverge.

No selection, request, or lifecycle advance is emitted from a rejected admission.

## Deliberate next boundary

The next independent PR should consume the existing Formalize dispatch and result together with the request-indexed frontier binding. It should record `formalize-transport` and `formalization-outcome` events on the exact node, classify candidate, counterexample, missing-prerequisite, already-known, and proof-search-exhausted outcomes, and release later waves only after exact truth-release certification and certified-claim manifestation.
