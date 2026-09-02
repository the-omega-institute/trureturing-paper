# FKST-native journal research and governed target selection

This stage consumes a claim-preserving scientific manuscript and selects one current Tier 2 or stronger journal before any venue-specific rewriting occurs.

## Execution chain

```text
paper_scientifically_edited_manuscript_ready
  -> dispatch-journal-research-agent
  -> paper-agent-task.v1
  -> shared FKST run-codex-agent
  -> paper-journal-research-draft.v1
  -> repository evidence admission
  -> repository-computed venue scorecards
  -> deterministic Tier 2+ target selection
  -> paper_journal_target_ready
```

The FKST package owns the Codex subprocess and reliable delivery. The journal-research Agent gathers dated source snapshots and source-local assertions. Repository code owns the evidence gate, publication-floor policy, score weights, eligibility, ranking, and selected venue.

## Exact manuscript closure

The journal task reopens the scientific-editing task, result, admission cursor, structured edit draft, edit delta, edited manuscript, edited LaTeX source, edited bibliography, and all exact inputs used by the scientific editor. Identical unchanged source bytes are deduplicated by content reference, so the final dispatch contains twenty-seven or twenty-eight unique evidence items. The dispatch itself becomes one additional Agent input.

Every input is checked again for repository-relative path safety, byte identity, schema, paper identity, theory-program identity, certified-claim lineage, and selected truth-release identity.

## Source roles

Each venue evidence packet must cover all of the following roles:

```text
official-scope
official-author-guidelines
official-article-types
official-formatting
official-length
official-fees
official-policies
independent-tier
recent-comparable
```

One official page may cover several official roles. Publication tier must come from an independent index. Comparable-paper evidence must come from a journal article. A venue therefore requires at least three distinct sources and complete role coverage.

Each retained source records:

```text
source identity
venue identity
roles
authority
HTTPS URL
retrieval timestamp
optional page-update timestamp
normalized source text
SHA-256 of the normalized bytes
exact assertions with evidence text
```

The evidence text for every assertion must occur inside the retained normalized source text. The source retrieval must lie between task creation and result completion and remain inside the repository-owned recency window.

## Venue facts

A candidate records the current evidence for:

```text
journal name, publisher, ISSN, and canonical URL
publication tier
scope fit
target article type
LaTeX and source-upload policy
abstract and main-text limits
proof appendix and supplement policy
mandatory and optional fees
data and code policy
preprint policy
AI-use policy
peer-review model
access model
recent comparable papers
```

Unknown critical policies are preserved as unknown and block eligibility. They are never interpreted as permission.

## Repository-owned scoring

The repository computes ten scores from zero to one hundred:

```text
scope fit                         weight 18
theorem-package fit               weight 15
article-type fit                  weight 10
recent comparable-paper support  weight 10
format feasibility               weight 10
length feasibility               weight 10
policy compatibility             weight 10
fee feasibility                   weight 5
evidence completeness             weight 7
evidence recency                  weight 5
```

The overall score is the weighted total. A venue is ineligible when any hard blocker applies, including:

```text
publication tier weaker than Tier 2
insufficient scope or theorem-package fit
unsupported target article type
missing comparable-paper evidence
incompatible source format
incompatible manuscript or abstract length
unknown or incompatible policy
unresolved fee status
missing source-role coverage
stale evidence
explicit Agent-declared blocking risk
```

The Agent cannot provide the scorecard or select the winner.

## Deterministic target selection

Eligible candidates are ranked by:

```text
eligibility
publication tier, strongest first
overall score
evidence completeness
policy compatibility
journal name
canonical venue ID
```

At least two eligible Tier 2 or stronger candidates are required. If the portfolio does not meet that floor, admission fails and the paper remains in journal research. The workflow never silently lowers the journal floor.

## Replay

The generic Agent cursor freezes the task-to-result binding. The journal admission cursor freezes:

```text
dossier
all scorecards
target selection
selected venue
selected publication tier
selected article type
run identity and provenance
```

Replay reopens the scientific manuscript closure, recomputes all scorecards, recomputes the deterministic winner, and returns the same content-addressed artifacts.

## Concurrency

Each paper has an independent journal task, workspace, source dossier, scorecards, and target-selection cursor. Several portfolio members may perform journal research concurrently while other papers remain in theory development, formalization, certification, authoring, or scientific editing.
