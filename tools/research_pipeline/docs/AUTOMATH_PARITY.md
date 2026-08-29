# Relationship to automath `dev-automation-integration`

The design adopts the useful control ideas from the automath autoresearch loop
while separating policy from implementation language.

| Automath mechanism | C-first counterpart | Integration decision |
| --- | --- | --- |
| Coverage-driven target manifest | `claim_ledger` and `novelty_gap` stages | Add exact-release candidate generation as a worker. |
| LLM proposal with constrained output | Intuition and Lean writeback adapters | Keep model APIs outside the C binary. |
| Forbidden `sorry` / `admit` / `axiom` checks | Lean verification receipt plus claim gate | Preserve kernel/build evidence as authority. |
| Compile and repair loop | Stage retry and failure transition to Intuition | Retry mechanics live in C. Mathematical repair lives in workers. |
| Per-target traces and cost controls | JSONL event log, stage logs, deadlines, retries | Add model-cost receipts when the worker contract is fixed. |
| Scratch changes followed by promotion | Explicit formalization worktree and human promotion | Never write the default branch from the control plane. |
| Circuit breaker | `max_transitions`, timeouts, STOP transitions | Add fleet-level failure-rate policy before daemon activation. |
| Paper coverage synchronization | `claim_manifest.tsv` and manuscript-sync gate | Require every formal manuscript claim to resolve to Lean evidence. |

The C binary deliberately does not contain provider-specific API clients,
LaTeX semantics, Lean proof generation, bibliographic search, or journal ranking
heuristics. Those change quickly and remain replaceable workers. The stable
control problem is kept small, deterministic, and testable.
