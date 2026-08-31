# A2 abstract-theory deepening

A2 is the research layer between paper-level theorem inventory and formalization. Every active portfolio lease advances one paper program through one bounded theory iteration. Several papers can execute A2 concurrently, while each paper develops its own multi-theorem dependency graph.

## Per-paper A2 flow

```text
paper-theory-program.v1
  + paper-theory-scope.v1
  + paper-theory-inventory.v1
  + optional prior paper-theorem-package.v1
      |
      v
paper-theory-deepening-request.v1
      |
      v
Codex abstract-theory iteration
      |
      +-> paper-theory-iteration.v1
      +-> paper-theorem-package.v1
      +-> optional split proposal
      +-> optional merge proposal
      +-> optional research-ledger entry
```

The first round has no prior theorem package. Every later round binds exactly one immediately preceding package version. A request, iteration, and package cannot move to another paper, scope, inventory, or prior package.

## What Codex must do

The A2 request fixes the research task rather than giving Codex an open-ended writing instruction. Codex must:

1. find or stabilize the canonical abstraction;
2. strengthen the central theorem and its supporting dependency chain;
3. close an explicit informal proof spine;
4. derive meaningful converses, classifications, bounds, rigidity results, corollaries, or applications;
5. test hypotheses through sharpness constructions and counterexamples;
6. distinguish known cited tools from the manuscript's novel increment;
7. identify independently coherent split candidates;
8. identify paper programs that should be merged around a shared theorem core.

It is forbidden from running Lean, dispatching Formalize, certifying claims, assembling journal prose, counting renaming as progress, adding isolated easy lemmas for volume, weakening the central theorem for convenience, or claiming proof closure without a checkable proof spine.

## Anti-fake progress gate

Every `paper-theory-iteration.v1` records explicit progress evidence. A valid round requires:

```text
new theorem-like claims + strengthened theorem-like claims >= 1
proof obligations closed >= 1
and at least one of:
  dependency edge added
  counterexample resolved
  abstraction changed
  novelty boundary changed
```

The iteration also requires a proof spine of at least three steps, a concrete novel increment, an explicit prior-work boundary, and at least one counterexample finding. Wording, notation, section order, and presentation changes cannot pass this gate.

A failed round does not update `last_progress_at`. It increments `consecutive_no_progress_cycles`, which lowers the paper's score in the next portfolio cycle and rotates capacity toward other viable papers.

## Theorem-package maturity

A theorem package is a versioned acyclic claim DAG. It contains at least three claims and at least two theorem-like claims. Every main theorem must be a load-bearing theorem node.

A package can remain `developing`, which returns the paper to another A2 cycle. It becomes `audit-candidate` only when:

- every load-bearing claim has a complete informal or certified proof;
- no open proof obligation remains;
- at least one corollary is present;
- at least one sharpness or counterexample result is present;
- the novelty summary separates new results from known cited tools;
- the publication significance is stated at the theorem-package level.

An audit-candidate advances the paper from `theory-deepening` to `audit-pending`.

## Parallel paper research

The portfolio can assign, for example:

```text
worker 1 -> paper A -> A2 round 4
worker 2 -> paper B -> A2 round 2
worker 3 -> paper C -> A2 round 7
worker 4 -> paper D -> A1 inventory
```

Each worker receives one paper lease. Its paper may contain many theorem nodes, and the worker can reason over the whole theorem DAG. The portfolio never allocates two worker slots to the same paper in one cycle.

This gives two distinct forms of structure:

```text
inside one paper: dependent theorem graph
across the program: parallel portfolio of papers
```

The first gives mathematical depth. The second prevents a single manuscript from monopolizing research capacity and lets stronger candidates emerge through competition.

## Split, merge, and ledger outcomes

A strong result that no longer belongs to the source scope may become a split proposal only when it carries an independent research question, at least one theorem-like claim, a three-step proof spine, a publication rationale, and an explicit overlap risk.

Two paper programs may be proposed for merge only when their theorem packages have concrete claim pairs related by equivalence, generalization, specialization, shared core, or incompatible framing. The proposal must state a unified abstraction and select one canonical paper identity.

Reusable discoveries are recorded in the research ledger. The ledger feeds future candidate batches and prevents the system from repeatedly rediscovering the same split, merge, stronger route, counterexample, or prior-work boundary.

Formalization remains downstream. No theorem package enters the existing Formalize and truth-release certification pipeline until the independent theory audit passes.
