# A0 scope and A1 multi-theorem inventory

Every active paper lease owns an independent theory program. The portfolio may schedule several such programs concurrently, while each program develops a paper-level theorem system.

## A0 scope

`paper-theory-scope-request.v1` is the executable Codex contract for A0. It binds the exact theory program, candidate artifact, literature record, Intuition proposal, and Paper research input.

Codex must:

```text
state the research question
select the canonical abstraction
fix the publication floor
enumerate in-scope theorem obligations
separate supporting and out-of-scope material
define the split policy
name counterexample and sharpness duties
```

Codex is forbidden from running Lean, dispatching Formalize, writing journal prose, weakening the question for convenience, or replacing exact inputs.

The result is `paper-theory-scope.v1`.

## A1 inventory

A1 reads the exact program and A0 scope. It inventories the entire paper-level proof architecture:

```text
definitions
lemmas
propositions
main theorems
corollaries
conjectures
counterexamples
unfinished proof interfaces
stronger and weaker variants
```

The result is `paper-theory-inventory.v1`. It is required to contain at least three claims, at least two theorem-like claims, and at least one internal dependency edge. Every main theorem identifier resolves to an actual theorem item. Internal dependencies must resolve and form an acyclic graph.

This deliberately rules out treating one easy lemma as a paper candidate. A paper begins as a theorem system with a main result, supporting structure, proof interfaces, possible stronger routes, weaker fallback routes, and counterexample obligations.

## Parallel application

A portfolio cycle may lease papers A, B, C, and D concurrently. Each worker executes A0 or A1 only for its own theory program:

```text
worker 1 -> paper A -> A0 scope
worker 2 -> paper B -> A1 inventory
worker 3 -> paper C -> A0 scope
worker 4 -> paper D -> A1 inventory
```

One paper never receives two portfolio slots in the same cycle. Within its slot, its inventory can still contain a large theorem DAG.

After A0, the paper state advances from `scope-pending` to `inventory-pending`. After a valid A1 inventory, it advances to `theory-deepening`. The next layer strengthens the inventory into a mature theorem package.
