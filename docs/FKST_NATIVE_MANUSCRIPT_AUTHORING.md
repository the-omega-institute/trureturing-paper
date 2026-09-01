# FKST-native certified manuscript authoring

This stage converts an eligible certified claim manifest into a journal-neutral scientific LaTeX manuscript. FKST owns the Codex process. The Paper package owns the scientific role and prompt. Repository validators own theorem identity, document structure, citation admission, source rendering, and replay.

## Entry condition

The stage consumes only:

```text
paper_certified_claim_manifest_ready
```

The event is a trigger. Before staging a Codex task, the repository reopens and validates the complete chain:

```text
eligible manuscript evaluation
  -> paper-certified-claim-manifest.v1
  -> paper-manuscript-eligibility.v1
  -> paper-manuscript-plan.v1
  -> paper-frontier-completion.v1
  -> final formalization frontier state
  -> audited theorem package
  -> A3 audit
  -> A1 inventory
  -> A0 scope
  -> paper candidate
  -> literature evidence
  -> one selected coherent truth release
```

The dispatch contains fourteen exact content-addressed inputs. The generic `paper-agent-task.v1` adds the immutable dispatch itself as the fifteenth input. Missing, replaced, path-traversing, symlinked, or hash-drifted evidence fails closed.

## FKST process ownership

The dispatch department calls the repository-local Agent CLI to stage and register a task. It emits:

```text
paper_agent_task_requested
```

The existing generic FKST department then runs:

```text
spawn_codex_sync
```

under the fixed profile:

```text
phase         manuscript-authoring
agent role    paper-manuscript-author
context       certified-claims-only
sandbox       workspace-write
timeout       bounded by the Paper profile
```

Phase-specific departments do not start Codex, Lean, Git, or `dotnet run`. They validate and route business artifacts around the shared FKST runtime.

## Structured draft boundary

Codex writes one:

```text
paper-scientific-manuscript-draft.v1
```

The draft is a structured exposition plan, not a complete TeX document. It contains exactly eight ordered sections:

```text
Introduction
Prior work and contribution boundary
Setting and definitions
Main results
Proof architecture
Formalization and certified provenance
Boundaries, sharpness, and counterexamples
Discussion
```

Each section must contain substantive prose. The draft uses four block kinds:

```text
prose
formal-claim
proof
informal-item
```

A `formal-claim` block names a manuscript claim ID and has empty LaTeX. An `informal-item` block names an explicit plan item and also has empty LaTeX. The agent cannot provide their statements, environments, labels, or epistemic status.

Every certified formal claim must appear exactly once, in manifest order, in `Main results`. Every formal claim must also receive exactly one proof-narrative block in `Proof architecture`. Every informal plan item must appear exactly once in the repository-owned section determined by its type.

## Repository-owned theorem rendering

After the generic Agent result is admitted, the repository constructs the final `main.tex`. It inserts each theorem-level item directly from `paper-certified-claim-manifest.v1`:

```text
claim ID
claim kind
LaTeX label
exact statement
certified claim ref
GID
statement ID
requested-statement digest
```

The rendered source surrounds every formal environment with immutable provenance markers:

```text
TRURETURING-FORMAL-CLAIM-BEGIN
TRURETURING-FORMAL-CLAIM-END
```

Definitions, examples, conjectures, remarks, and limitations receive separate informal markers carrying their text digest and epistemic status:

```text
TRURETURING-INFORMAL-ITEM-BEGIN
TRURETURING-INFORMAL-ITEM-END
```

The agent is forbidden to emit theorem, lemma, proposition, corollary, definition, example, remark, or proof environments. It cannot add, omit, merge, split, weaken, strengthen, paraphrase, or reclassify a formal claim.

## Proof narrative

The author supplies a proof narrative for every formal claim. Repository rendering wraps that narrative in a proof environment bound to the corresponding certified LaTeX label.

This stage checks coverage and identity. It does not claim that an expository proof narrative has the same status as the certified Lean declaration. The declaration remains the formal evidence. The narrative is routed next to independent scientific editing.

## Citation admission

The author may cite only with canonical:

```tex
\cite{key}
```

Every citation key must have exactly one draft reference entry. Each entry points by one-based index into the exact `related_work` array of the admitted literature artifact. The repository, rather than Codex, renders the BibTeX fields from that evidence.

If the historical literature artifact is opaque or lacks structured related-work records, the draft cannot introduce bibliography entries. A later live-literature stage can replace that artifact only through its own governed evidence chain.

## Forbidden TeX surface

Agent prose and proof fragments reject document-level or mutation-capable controls, including:

```text
documentclass and package commands
section and label commands
file input and output
macro definitions
catcode and csname construction
theorem and proof environments
bibliography commands
comments and character-code escapes
repository provenance markers
```

The repository owns the preamble, section structure, labels, environments, bibliography inclusion, and document boundary.

## Output artifacts

Successful admission creates:

```text
paper-scientific-manuscript.v1
main.tex, content-addressed source
references.bib, content-addressed source
paper-manuscript-authoring-agent-cursor.v1
paper_scientific_manuscript_ready
```

`paper-scientific-manuscript.v1` binds the sources to the task, result, dispatch, frontier completion, eligibility evaluation, claim manifest, manuscript plan, theorem package, A3 audit, literature evidence, and selected release.

The source coordinate stores media type, content digest, repository-relative path, and byte count. The domain manuscript has separate semantic-content and envelope identities.

## Replay

The generic FKST agent cursor prevents repeated Codex execution for the same task. The manuscript admission cursor additionally fixes:

```text
task and result
exact scientific evidence
rendered main.tex
rendered bibliography
claim binding ledger
next route
Codex run identity and provenance
```

A repeated delivery reopens the stored sources and manuscript envelope, recomputes every content address, rechecks every formal and informal marker, and returns the same identities. The same task cannot later bind to another manuscript.

## Failure routes

```text
completed
  -> scientific-editing

no-progress
  -> paper_manuscript_authoring_no_progress
  -> paper_manuscript_authoring_retry_requested

blocked
  -> paper_manuscript_authoring_blocked
```

A no-progress or blocked result cannot attach draft outputs. A domain-invalid completed draft fails admission and does not create a scientific manuscript artifact.

## Multi-paper behavior

Each eligible paper produces an independent task, workspace, result cursor, and manuscript cursor. FKST can run several manuscript authors while other papers remain in A2, A3, Formalize, certification, or frontier completion.

```text
Paper A  manuscript authoring
Paper B  scientific editing
Paper C  A3 audit
Paper D  certification wait
Paper E  frontier wave formalization
```

No manuscript author may read or modify another paper's workspace.

## Next boundary

The next stage consumes `paper_scientific_manuscript_ready` and performs fresh scientific editing. It may reorganize prose and strengthen explanations. It must preserve all formal markers, claim labels, exact statements, certified claim refs, GIDs, statement IDs, bibliography evidence, and selected release.
