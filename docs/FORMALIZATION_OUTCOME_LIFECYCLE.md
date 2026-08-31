# Paper Formalization Outcome Lifecycle

## Scope

This lifecycle belongs to the Paper organ. It begins after a Formalize result has
already been recorded and request-bound by `paper-formalization-result.v1`. It
does not run Lean, adopt a patch, write the Base repository, open a Base pull
request, or declare mathematical truth.

The boundary is:

```text
paper-formalization-result.v1
        |
        v
Paper outcome classification
        |
        +-- accepted + verified ------> certification wait
        +-- counterexample -----------> Intuition research
        +-- missing prerequisite -----> governed sublemma research
        +-- already in library -------> novelty reassessment
        +-- proof attempt exhausted --> proof-strategy revision
        +-- request/infrastructure ---> blocked diagnostic
```

## Evidence reconstruction

Classification does not trust the event payload as the scientific record. Given
only `result_ref`, the C# service reopens the content-addressed chain:

```text
result_ref
  -> paper-formalization-result.v1
  -> dispatch_ref
  -> paper-formalization-dispatch.v1
  -> request_blob_ref
  -> formalization-request.v1
  -> selection_blob_ref
  -> paper-research-selection.v1
```

It verifies every digest and then rechecks request, selection, candidate, GID,
source commit, source tree, and truth-release bindings. A route cannot be produced
from a result that has drifted away from its governed request.

## Closed routing vocabulary

Only the first typed token of an abstained verdict is interpreted. Free prose
later in the verdict cannot change the route.

| Formalize outcome | Paper route | Paper claim status |
| --- | --- | --- |
| accepted result with verified exact context | `await-certification` | `pending-certification` |
| `COUNTEREXAMPLE` or `STATEMENT_INCONSISTENT` | `intuition-research` | `ineligible` |
| `GENERALITY_TOO_STRONG` | `intuition-research` | `ineligible` |
| `MISSING_PREREQUISITE` | `sublemma-research` | `ineligible` |
| `ALREADY_IMPLIED_BY_LIBRARY` | `novelty-reassessment` | `ineligible` |
| failed candidate-integrity gate | `proof-strategy-revision` | `ineligible` |
| exhausted bounded proof search | `proof-strategy-revision` | `ineligible` |
| request rejection, unavailable capability, or unknown token | `blocked` | `ineligible` |

The selection's `failure_semantics` remains authoritative. A counterexample is
routed to Intuition only when `counterexample_is_useful` is true. A missing
prerequisite is expanded only when
`missing_prerequisite_is_reportable` is true. Otherwise the outcome is recorded
and blocked for explicit review.

## Certification wait

An accepted Formalize result proves only that a request-bound candidate was
returned. Paper creates `paper-certification-wait.v1`, which binds:

- the result, dispatch, request, and selection;
- the exact Base truth release against which the candidate was produced;
- the expected GID and theorem statement;
- desired generality, dependencies, allowed assumptions, and forbidden
  weakenings;
- the candidate paper, literature, Intuition, and verification-budget evidence.

The wait has exactly one epistemic status:

```text
pending-certification
```

It cannot enter the manuscript claim gate as a certified theorem. A later
Paper-owned truth-release join will decide whether a newer certified release
contains the required declaration with the required statement identity and
acceptable axiom closure.

## FKST route

```text
paper_formalization_result_recorded
        |
        v
classify-formalization-outcome
        |
        +-> paper_candidate_pending_certification
        +-> paper_intuition_research_requested
        +-> paper_sublemma_research_requested
        +-> paper_novelty_reassessment_requested
        +-> paper_formalization_strategy_revision_requested
        +-> paper_formalization_blocked
```

The Department receives only a content reference. The Paper repository root is
deployment-owned through `TRURETURING_PAPER_REPOSITORY_ROOT`; an incoming event
cannot choose a local store or executable. Every route event carries the
content-addressed decision reference and the upstream research references needed
by the next Paper stage.

## Idempotence and supersession

The decision and optional certification wait are canonical JSON artifacts in the
Paper content-addressed store. A per-result decision cursor is written
atomically. At-least-once replay returns the same decision. The same result
cannot be rebound to a different route or wait.

This lifecycle deliberately has no automatic transition from
`pending-certification` to a manuscript claim. That transition belongs to the
next Paper stage, which observes a future truth release.
