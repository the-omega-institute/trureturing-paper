# Dependency-aware paper formalization frontier

A passed theorem package is formalized as a dependency frontier rather than as one isolated lemma. The portfolio may promote several papers in one cycle, producing several independent frontiers. Each frontier contains the complete claim DAG of one paper.

## Promotion boundary

A frontier can be created only from one exact combination:

```text
paper-theory-program.v1
paper-theorem-package.v1 with maturity audit-candidate
paper-theory-audit.v1 with passed = true
paper-candidate-scorecard.v1 with promotion_eligible = true
paper-portfolio-decision.v1 with action = promote-to-frontier
```

The frontier also preserves the program's exact truth-release digest, topology digest, and Paper research-input reference. A held, deepening, split, merge, parked, archived, or failed paper cannot create a frontier.

## Complete theorem DAG

Every theorem-package claim receives exactly one frontier node. A node contains:

- the informal theorem-package statement;
- the proposed formal statement;
- dependency node IDs;
- a formalization kind;
- priority;
- target Lean package and module;
- an explicit acceptance criterion.

The service derives `parallel_wave` from the theorem dependencies:

```text
wave 0  definitions and independent primitives
wave 1  prerequisite lemmas
wave 2  main theorem and independent sharpness theorem
wave 3  corollaries depending on the main and sharpness results
```

`critical_path_depth` records the longest dependency chain. `maximum_wave_width` records how many independent claims can be worked on together inside the paper lease.

This retains the distinction between two concurrency layers:

```text
portfolio concurrency: several papers advance at once
frontier concurrency: independent claims inside each paper advance by wave
```

The portfolio still grants at most one top-level lease per paper. A paper worker may internally dispatch independent nodes from its current wave.

## Existing governed lifecycle

Every frontier node advances through the existing downstream families:

```text
selection-pending
  -> governed-selection
  -> canonical-formalization-request
  -> formalize-transport
  -> formalization-outcome
  -> truth-release-certification
  -> certified-claim-manifest
```

The stable artifact-family adapter allows the frontier to bind the exact content-addressed artifacts produced by the existing selection, canonical request, Formalize transport, outcome classification, certification join, and manifest contracts.

The event carries both `artifact_family` and the concrete `artifact_schema`. This preserves compatibility with the already implemented downstream contracts while keeping the frontier independent of transport-specific C# types.

## Dependency gate

Governed selection may be recorded for any frontier node. A canonical formalization request is allowed only when every dependency node is already `certified` or `manifested`.

Consequently:

```text
definition certified
  -> prerequisite request becomes legal
prerequisite certified
  -> main and sharpness requests become legal
main + sharpness certified
  -> classification corollary request becomes legal
```

The proof dependency graph therefore controls the actual Formalize schedule.

## Outcome feedback to theory

A Formalize outcome is classified into one of the existing research routes:

```text
candidate-produced       -> certification-pending
counterexample           -> theory-revision-required
missing-prerequisite     -> frontier-revision-required
already-known            -> novelty-reaudit-required
proof-search-exhausted   -> proof-architecture-revision
```

Only `candidate-produced` can enter truth-release certification. Certification must join the exact truth release bound by the paper program. A certification from another release is rejected.

A certified node may then be linked to the certified claim manifest. The immutable event chain preserves every downstream artifact reference and the predecessor event for that node.

## Parallel-safe state reduction

`paper-formalization-frontier-state.v1` is a content-addressed reduction of applied node events. Independent nodes may each emit one event against their own predecessor and be applied in a single deterministic batch. Two events for the same node cannot occupy one batch.

The state version equals the number of applied events. Node states and event references are canonically sorted, so replaying the same independent event set produces the same state identity.

## Paper completion

A paper is ready for manuscript assembly only after all load-bearing main, sharpness, and corollary frontier nodes are certified or manifested in the exact truth release. The final supervisor PR consumes this state across several paper frontiers and continues scheduling other papers while one frontier waits on Formalize or certification.
