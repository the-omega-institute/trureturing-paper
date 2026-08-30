# Parallel paper portfolio contracts

The Paper research unit is a portfolio of independent paper programs. A paper may contain a dependency graph of many theorems, while a portfolio cycle schedules several papers at the same time.

## Boundary

```text
paper-research-input.v1
  -> paper-candidate-batch.v1
  -> one paper-theory-program.v1 per candidate
  -> paper-research-portfolio.v1
  -> paper-portfolio-cycle.v1
  -> distinct per-paper workers
```

The batch, every theory program, and the portfolio preserve the same truth-release digest, certified topology digest, and Paper research-input reference. A program cannot substitute another candidate, literature record, Intuition proposal, or exact release.

## Parallelism is across papers

A candidate batch contains at least two papers. The normal operating capacity is five papers, while the contract permits an explicit capacity from two to thirty-two. `max_parallel_papers` controls the number of paper programs that may receive work in one cycle.

Every cycle has `execution_mode = parallel-paper-batch`. Its leases satisfy:

1. each lease names one paper and one theory program;
2. paper identifiers are unique inside the cycle;
3. theory-program references are unique inside the cycle;
4. worker slots are contiguous and one-based;
5. `per_paper_lease_limit` is exactly one.

A worker may deepen several dependent theorems inside its own paper program, but a cycle never spends multiple portfolio slots on the same paper. This prevents one manuscript from consuming the whole research pool.

## Scheduling

Runnable phases are:

```text
scope-pending
inventory-pending
theory-deepening
audit-pending
frontier-pending
formalizing
certification-pending
manuscript-pending
```

`parked`, `archived`, and `done` are terminal for scheduling.

The deterministic score is:

```text
priority
+ min(30, floor(hours_since_last_progress / 6))
- min(50, consecutive_no_progress_cycles * 10)
```

The age term prevents a viable paper from waiting indefinitely behind recently active papers. Repeated cycles without mathematical progress lower a paper's scheduling score so the portfolio can rotate effort to other candidates. Ties are resolved by the oldest progress time and then by paper identifier.

The same portfolio bytes and planning timestamp produce the same cycle and lease identities.

## Next lifecycle layers

This contract deliberately stops before theory content is changed. Later contracts add:

1. a stable scope and theorem inventory for every paper;
2. abstract-theory deepening and theorem-package construction;
3. independent theory audits and portfolio-level promotion decisions;
4. a formalization frontier that decomposes a mature theorem package into a claim dependency DAG;
5. a supervisor that executes the leases concurrently and records outcomes.

The existing Formalize transport, certification join, and certified claim manifest remain downstream. A paper reaches those stages only after its theory program has passed the new theory gates.
