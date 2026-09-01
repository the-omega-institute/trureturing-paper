# FKST frontier formalization lifecycle

This document freezes the Paper-side boundary that joins an admitted formalization frontier to the existing Formalize and truth-release certification pipelines.

## Scope

The lifecycle begins after a frontier node has passed repository-governed selection and has produced one canonical `formalization-request.v1`.

```text
paper_frontier_node_selection_ready
  -> formalization_request_ready
  -> canonical Formalize dispatch
  -> Formalize result classification
  -> exact descendant-release certification
  -> frontier certified-claim manifest
  -> dependency-ready frontier set
```

This layer does not run a new scientific agent, edit Base, or create a second Formalize transport. It reuses the existing transport, result classifier, certification join, and claim evidence while recording their effects on the exact frontier node.

## Request-indexed source join

PR #33 creates one `paper-frontier-formalization-binding.v1` and a lookup keyed by `formalization_request_ref`.

```text
work/paper-frontier-formalization-bindings/
  by-request/<request-ref>.json
```

Every progress operation starts from this lookup and reopens:

- the immutable frontier-planning admission;
- the original promotion evidence closure;
- the exact theorem package and frontier node;
- the governed selection and verification budget;
- the canonical Formalize request;
- the original Paper research input and base truth release.

Event prose is never used to infer node ownership.

## Formalize transport

When `dispatch-formalization` creates the canonical `paper-formalization-dispatch.v1`, the frontier lifecycle CLI validates the dispatch against the request-indexed binding and appends a `formalize-transport` frontier event.

```text
request-recorded
  -> transport-recorded
```

The immutable transport cursor binds:

- request, selection, and dispatch;
- frontier, node, and claim;
- transport event;
- resulting frontier state.

Requests without a frontier binding continue through the legacy Formalize path and return `not-frontier-bound` from the progress recorder.

## Typed formalization outcomes

The existing Paper outcome classifier remains authoritative for the semantic result. The frontier layer maps classified scientific outcomes to the closed lifecycle vocabulary:

| Paper outcome class | Frontier disposition | Frontier status |
| --- | --- | --- |
| `candidate-produced` | `candidate-produced` | `certification-pending` |
| `counterexample` | `counterexample` | `theory-revision-required` |
| `statement-inconsistent` | `counterexample` | `theory-revision-required` |
| `generality-too-strong` | `counterexample` | `theory-revision-required` |
| `missing-prerequisite` | `missing-prerequisite` | `frontier-revision-required` |
| `already-implied-by-library` | `already-known` | `novelty-reaudit-required` |
| `proof-search-exhausted` | `proof-search-exhausted` | `proof-architecture-revision` |
| `candidate-invalid` | `proof-search-exhausted` | `proof-architecture-revision` |

Infrastructure failures, request rejection, and unclassified outcomes remain outside the scientific frontier transition. Their existing Paper route is preserved, while the node stays at `transport-recorded`.

## Exact descendant-release certification

A produced candidate can become certified only through the existing certification service. The frontier join verifies:

- the evaluation resolved to `certified`;
- the certified claim, wait, result, decision, request, selection, budget, paper, candidate, and GID agree;
- the declaration appears in the certifying release;
- statement correspondence is exact;
- the declaration kind and axiom closure pass the existing policy;
- the certifying release differs from the base release;
- the certifying release names the base release in its ancestry.

The frontier state records the actual descendant release digest.

```text
certification-pending
  -> certified
```

## Frontier certified-claim manifest

After certification, the repository creates one `paper-frontier-certified-claim-manifest.v1`. It binds the exact frontier node to:

- Formalize request and result;
- Paper decision and selection;
- certification evaluation and certified claim;
- certifying release and digest;
- Lean declaration, declaration kind, statement identity, and axiom closure.

The manifest is then admitted as the next lifecycle event.

```text
certified
  -> manifested
```

A dependency is used for downstream frontier scheduling only after this manifest transition.

## Dependency-ready sets

Each certification computes a content-addressed `paper-frontier-ready-set.v1` from the full current frontier state.

A node is included exactly when:

1. its current status is `selection-pending`;
2. it was not already released by the initial wave-zero plan or an earlier ready set;
3. it has at least one dependency;
4. every dependency is `manifested`.

Routes are ordered by:

```text
parallel wave ascending
priority descending
node identity ascending
```

The ready set records evidence only. A following independent PR will consume its routes and construct governed selections for later-wave nodes.

## Serialization and recovery

All frontier mutations are serialized by frontier identity in both FKST and repository code. Immutable progress cursors are stored under:

```text
work/paper-frontier-formalization-progress/
  transports/<frontier>/<node>.json
  outcomes/<frontier>/<node>.json
  certifications/<frontier>/<node>.json
```

Before a new transition, recovery scans the current-state pointer and every admitted progress cursor. It validates event and state correspondence, requires all earlier histories to be subsets of the selected history, chooses one unique maximum-version state, and repairs a missing or lagging current-state pointer.

Equal-version states with different identities fail closed.

## FKST events

The package adds these evidence events:

```text
paper_frontier_formalize_transport_ready
paper_frontier_formalization_outcome_ready
paper_frontier_certified_claim_manifest_ready
paper_frontier_ready_set_ready
```

The existing Paper events continue to be emitted. This preserves compatibility for certification, research backroutes, manuscript refresh, and non-frontier Formalize requests.

## Authority boundary

```text
Formalize
  owns proof search and its terminal result

Paper outcome classifier
  owns semantic classification and research backroute

Certification service
  owns exact declaration and release-lineage certification

Frontier lifecycle
  owns node identity, state transition, manifest visibility,
  and dependency-ready scheduling evidence
```

No component can mark a node manifested from a successful process exit, an unverified Lean patch, event text, or a same-release observation.
