# FKST-native Paper agent runtime

## Boundary

The Paper workflow does not delegate its scientific roles to shell-command environment variables. A Paper role is a reliable FKST event handled by a repository-owned Department. The Department invokes Codex through the fixed FKST Lua SDK and records the result through repository validators.

```text
paper-agent-task.v1 in deployment inbox
    -> paper_agent_task_seen
    -> register-agent-task
    -> paper_agent_task_requested
    -> run-codex-agent
    -> spawn_codex_sync
    -> Paper Agent CLI validation
    -> content-addressed result and outputs
    -> completed | no-progress | blocked queue
```

The engine owns process admission, Codex process ownership, timeout enforcement, child reaping, reliable-delivery retry, and dead-letter handling. The Paper package owns the role, exact inputs, prompt, sandbox, timeout policy, expected outputs, and scientific route. C# owns deterministic validation, filesystem boundaries, hashes, and immutable result resolution.

## Supported roles

The closed profile registry contains:

```text
candidate-discovery
literature-query-planning
literature-synthesis
theory-scope
theory-inventory
theory-deepening
theory-audit
portfolio-judgment
frontier-planning
journal-research
manuscript-authoring
scientific-editing
journal-style-editing
language-editing
proofreading
cover-letter-authoring
```

Each phase maps to one exact `agent_role`, `context_mode`, explicit `workspace-write` sandbox, and bounded timeout. A task cannot select a different role, context, sandbox, or timeout through event data.

Read-oriented roles still receive `workspace-write` because the model must write the declared JSON output files. The writable surface is an isolated task directory containing copies of exact input artifacts and an empty `outputs/` tree. The prompt forbids network, Git, GitHub, Base writeback, and Formalize access. External literature and journal facts must therefore arrive through separately governed source adapters as content-addressed input artifacts.

## Exact inputs

A task lists every input as:

```text
schema
artifact_ref
repository_relative_path
```

Registration and preparation recompute each SHA-256 reference. Inputs are copied into the isolated workspace and listed in the generated prompt. The final result must acknowledge exactly the same input-ref set.

Input paths are restricted to approved Paper evidence roots. Absolute paths, traversal segments, reserved output paths, missing files, digest drift, and symbolic-link traversal fail closed.

## Expected outputs

A task lists one to sixteen expected JSON files under `outputs/`. A completed result must return every expected `(schema, path)` pair exactly once. Added, removed, duplicated, renamed, or retyped outputs fail closed.

The recorder checks that each file exists, is nonempty, is below the owned workspace, does not traverse a symbolic link, and declares the expected top-level schema. The exact bytes are copied into the content-addressed Paper agent store.

A `no-progress` or `blocked` result must return no outputs and must carry a canonical uppercase blocker code. This prevents a worker from attaching unvalidated artifacts to a failed scientific step.

## Result envelope

Codex stdout must contain exactly one envelope and no surrounding prose:

```text
PAPER_AGENT_RESULT_BEGIN
{ paper-agent-result.v1 JSON }
PAPER_AGENT_RESULT_END
```

The result is rejected unless task, paper, theory program, phase, role, context, inputs, outputs, route, and timestamps all match the registered task.

## Replay and provenance

The first validated terminal result creates an immutable task cursor:

```text
task_ref -> result_ref + stored output refs + run provenance
```

Reliable redelivery returns that recorded result and does not start another Codex process. A task cannot later bind to a different result.

The FKST Codex SDK supplies `provenance = produced | adopted` and an optional `run_id`. Both are carried into the Paper cursor and result event. An adopted successful engine run is therefore distinguishable from work produced by the current Department invocation.

## Concurrency

The runtime is compatible with both forms of Paper parallelism:

```text
portfolio parallelism:
    several paper_agent_task_requested deliveries run in parallel

theorem-frontier parallelism:
    independent claims from one paper can have separate task refs
```

`with_lock` serializes duplicate invocations for the same task ref. It does not serialize different papers or independent theorem claims. FKST engine backpressure remains the authority for global and per-Department process concurrency.

## Build and regression integration

`Trureturing.Paper.Agent.Cli` is part of `Trureturing.Paper.slnx`, so the ordinary warnings-as-errors build compiles the same validator binary that the FKST Departments invoke. `research_core.paths()` resolves that repository-local binary through the `agent_cli` field. A missing build artifact fails closed before a Codex result can be recorded.

The ordinary Paper test assembly covers task registration, exact evidence materialization, output admission, route enforcement, immutable replay, result-envelope parsing, symbolic-link rejection, and the direct FKST Codex SDK wiring. These tests execute alongside the stacked portfolio, theory, audit, and frontier regression suite.

## Connected business phases

A0 scope and A1 inventory use the generic runtime through domain-specific adapters:

```text
paper_theory_scope_requested | paper_theory_inventory_requested
    -> immutable foundation dispatch
    -> reconstructed phase-owned paper-agent-task.v1
    -> native Codex execution
    -> scope or inventory draft
    -> existing domain validator
    -> canonical content ID and full envelope
    -> paper_theory_scope_ready | paper_theory_inventory_ready
```

A2 abstract-theory deepening uses the same runtime:

```text
paper_theory_deepening_requested
    -> immutable A2 dispatch
    -> reconstructed paper-theory-developer task
    -> native Codex execution
    -> one theory-deepening draft bundle
    -> existing iteration and theorem-package validators
    -> repository-computed theorem delta
    -> typed theorem package, split, merge-research, and ledger events
```

The A2 adapter compares the returned package with the exact A1 inventory or immediately prior theorem package. Agent-provided progress counters must match repository-computed new, strengthened, retired, dependency, proof-closure, sharpness, abstraction, and novelty changes. The model cannot promote presentation edits or fabricated counters as mathematical progress.

See `docs/FKST_A0_A1_NATIVE_AGENTS.md` and `docs/FKST_A2_NATIVE_AGENT.md` for the complete lanes.

## Remaining integrations

Fresh multi-agent A3 audit, source acquisition, journal research, manuscript authoring, editing, proofreading, and SSHX review still need business-event adapters. They should use the same task runtime and preserve their existing domain validators and progress gates.

This runtime does not invoke Formalize, write Base, submit a manuscript, or treat an agent draft as certified truth.
