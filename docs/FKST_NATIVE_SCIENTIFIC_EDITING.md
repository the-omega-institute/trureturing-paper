# FKST-native claim-preserving scientific editing

Scientific editing begins only after the manuscript-authoring bridge has admitted a journal-neutral `paper-scientific-manuscript.v1` and routed it to `scientific-editing`.

The phase improves scientific communication while treating the certified theorem ledger as immutable evidence.

## Workflow

```text
paper_scientific_manuscript_ready
  -> dispatch-scientific-editing-agent
  -> paper-agent-task.v1
  -> shared FKST run-codex-agent / spawn_codex_sync
  -> paper-scientific-edit-draft.v1
  -> repository-computed edit delta
  -> repository-owned LaTeX regeneration
  -> protected-claim byte comparison
  -> paper-scientifically-edited-manuscript.v1
  -> paper_scientifically_edited_manuscript_ready
  -> journal-research
```

The phase-specific Departments do not invoke Codex directly. The shared FKST Agent runtime owns subprocess execution, workspace isolation, timeout, replay, and generic result admission.

## Exact source closure

The editor reopens the admitted manuscript-authoring task, result, domain cursor, structured source draft, scientific manuscript envelope, `main.tex`, and bibliography. It also retains the fourteen certified inputs used by the authoring phase:

- claim evaluation;
- certified claim manifest;
- manuscript eligibility;
- manuscript plan;
- frontier completion receipt;
- selected truth release;
- theory program;
- A0 scope;
- A1 inventory;
- A2 theorem package;
- A3 audit;
- candidate-paper evidence;
- literature-research evidence;
- formalization frontier.

The resulting scientific-editing dispatch contains nineteen content-addressed inputs. Its immutable dispatch becomes the twentieth generic Agent input.

Every path, schema, digest, paper identity, theory-program identity, source-manuscript identity, and selected-release coordinate is checked again before task staging and result admission.

## Editing authority

The editor may revise:

```text
abstract and keywords
scientific motivation
contribution framing
prior-work boundary
logical sequencing
proof exposition
formalization explanation
sharpness and limitation treatment
discussion and implications
```

The editor may not change:

```text
eight section identities or titles
section order
block count or block kind
formal-claim targets
proof targets
informal-item targets
formal statements
claim kinds
LaTeX labels
certified claim refs
GIDs
statement IDs
requested-statement digests
selected truth release
axiom closure
```

The editor returns a structured `paper-scientific-edit-draft.v1`. It never writes a complete LaTeX document, theorem environment, proof environment, label, macro, file input, bibliography command, or repository provenance marker.

## Repository-computed progress

The repository compares the edit with the exact structured authoring draft and computes:

```text
changed section IDs
changed prose-block count
changed proof-block count
abstract change
keyword change
citation-set change
substantive scientific dimensions
```

Admission requires at least:

```text
2 changed prose blocks
1 changed proof block
3 changed sections
3 substantive dimensions
```

The actual dimensions must include:

```text
contribution-framing
proof-exposition
limitations-and-implications
```

The Agent-declared `edit_dimensions` must equal the repository-computed set. A no-op edit, wording-only self-report, or fabricated progress count cannot pass.

## Protected formal content

After the structured draft is admitted, the repository invokes the existing manuscript renderer. The renderer inserts every theorem, lemma, proposition, corollary, definition, example, and remark from the certified manifest and plan.

For each formal claim, the repository compares the complete source segment bounded by:

```text
TRURETURING-FORMAL-CLAIM-BEGIN
TRURETURING-FORMAL-CLAIM-END
```

For each governed informal item, it compares the segment bounded by:

```text
TRURETURING-INFORMAL-ITEM-BEGIN
TRURETURING-INFORMAL-ITEM-END
```

Every protected segment must be byte-identical to the authoring-stage source. The claim-binding ledger must also be exactly equal. This freezes theorem text, environment, label, certification coordinates, and epistemic status while allowing surrounding exposition and proof narration to improve.

## Citation boundary

The edited draft remains subject to the authoring-stage citation validator. Citation keys and bibliography records must resolve to the admitted literature artifact. The repository regenerates `references.bib` from evidence metadata. Unsupported authors, titles, venues, dates, URLs, or references are rejected.

## Artifacts

A successful pass creates:

```text
paper-scientific-edit-delta.v1
paper-scientifically-edited-manuscript.v1
content-addressed scientifically edited main.tex
content-addressed scientifically edited references.bib
paper-scientific-editing-agent-cursor.v1
```

The edited manuscript binds the new sources to the source authoring task and manuscript, the edit delta, the complete certification lineage, the theorem package, the A3 audit, the literature evidence, and the selected truth release.

## Replay and failure semantics

The generic Agent cursor prevents a repeated Codex execution for the same task. The scientific-editing cursor additionally freezes the edited manuscript, delta, rendered sources, changed sections, and run provenance. Replay reopens and revalidates every source and protected segment.

Failure routes are closed:

```text
completed   -> journal-research
no-progress -> scientific-editing retry
blocked     -> typed scientific-editing block
```

A no-progress or blocked result cannot carry output artifacts or produce an edited manuscript.

## Portfolio concurrency

Each manuscript receives a separate FKST task, workspace, and cursor. Multiple scientific editors may run for different papers while other portfolio members remain in theory deepening, audit, frontier formalization, certification, manuscript authoring, or journal research.
