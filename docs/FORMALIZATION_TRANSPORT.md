# Governed Paper to Formalize transport

This document describes the request and result boundary between the Paper organ
and the `trureturing-formalize` FKST package.

## Event path

```text
paper-selection authorization
        |
        v
formalization_request_ready
        |
        v
Paper dispatch-formalization
        |
        | validates selection and request again
        | stores canonical selection and request bytes
        | writes one content-addressed dispatch record
        v
trureturing-formalize.solve_request
        |
        v
Formalize solve
        |
        v
trureturing-formalize.solve_result
        |
        v
Paper record-formalization-result
        |
        | resolves the previously recorded dispatch
        | rejects request, selection, candidate, or release drift
        | stores one content-addressed terminal result
        v
paper_formalization_result_recorded
```

The outgoing queue is qualified. It depends on the published
`solve_request` seam in the `trureturing-formalize` package. No other Formalize
queue is opened to Paper.

## Dispatch authority

`formalization_request_ready` contains paths and redundant coordinates. Paper
does not forward them directly. The dispatch CLI reopens both canonical files,
recomputes their identities, verifies that the request faithfully represents the
governed selection, and stores these immutable bindings:

- the semantic request ID;
- the governed selection ID;
- SHA-256 references for the complete canonical request and selection bytes;
- the source repository, commit, tree, and truth-release digest;
- the Paper and research-candidate identities;
- the preferred theorem GID.

A per-request cursor prevents one request ID from being rebound to another
dispatch. At-least-once replay of identical input returns the existing dispatch.

## Result intake

Formalize returns a correlated scalar event. Paper resolves the dispatch cursor
before accepting that event. A result must retain the original request and
selection references. Every non-empty exact-release or Paper coordinate must
match the dispatch.

An `accepted` result requires the complete context and receives
`binding_status = "verified"`. An `abstained` result may be recorded with
`binding_status = "rejected-before-context"` when Formalize rejected substituted
or malformed request bytes before it could recover the complete context. Such a
record is failure evidence and does not promote any mathematical claim.

A per-request result cursor makes duplicate delivery idempotent and rejects a
different terminal result for the same request.

## Runtime binding

The result consumer cannot infer the Paper repository from a cross-package event.
The Paper deployment therefore owns:

```text
TRURETURING_PAPER_REPOSITORY_ROOT
```

The event cannot choose this path. Formalize likewise owns its checkout and CLI
paths through its deployment environment.

## Remaining boundary

This transport records the current Formalize outcome. Candidate production still
depends on the Base truth-export and `codex-formalize` candidate-mode seams.
A future adoption lane must validate any candidate bundle, open a protected Base
change, wait for protected merge, and verify the declaration in a later truth
release before the manuscript may treat it as certified truth.
