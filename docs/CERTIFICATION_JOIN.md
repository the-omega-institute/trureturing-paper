# Paper certification join

This stage closes the Paper-owned boundary between an accepted Formalize candidate and a claim that may enter a manuscript.

It does not run Lean, apply a candidate patch, write the Base repository, create a Base pull request, or declare truth. Those operations remain outside the Paper organ. Paper only records a pending candidate and observes later certified truth-release evidence.

## State transition

```text
paper_candidate_pending_certification
        |
        v
register-certification-wait
        |
        +-------------------------------+
        |                               |
        | existing release observations| later release observation
        v                               v
paper_certification_evaluation_requested
        |
        v
evaluate-certification-release
        |
        +-- same release ----------------------> paper_candidate_still_pending_certification
        |
        +-- declaration absent ----------------> paper_candidate_still_pending_certification
        |
        +-- lineage/request/statement mismatch -> paper_certification_mismatch
        |
        +-- declaration kind/axiom mismatch ---> paper_certification_mismatch
        |
        +-- exact later certification ---------> paper_certified_claim_ready
```

Waits and release observations may arrive in either order. Registering either side enumerates the already registered peers and raises the same pairwise evaluation request. The pair evaluator is content-addressed and idempotent.

## Trust boundary

`paper-certification-release.v1` is a Paper-owned projection produced by the deployment adapter named `trureturing-paper-truth-release-adapter`. The adapter is responsible for acquiring a real immutable truth release, verifying the bundle against its out-of-band release digest, checking protected-dev provenance, resolving the requested declaration, and establishing the request-statement correspondence.

The core Paper package does not fetch GitHub, invoke Git, inspect a checkout, or trust an event's prose. It accepts only canonical observation bytes from the deployment-owned inbox, stores them by SHA-256, and checks the exact closed contract.

Until the deployment adapter exists and emits a valid observation, certification remains fail closed.

## Exact certification predicate

For a pending wait `W`, release observation `R`, and declaration observation `D`:

```text
certified(W, R, D) iff
    R.source_repo = W.source_repo
and R.release_digest != W.base_truth_release_digest
and W.base_truth_release_digest in R.ancestor_release_digests
and D.gid = W.gid
and D.formalization_request_ref = W.formalization_request_ref
and D.requested_statement_digest =
      SHA256("trureturing:paper-request-statement:v1\0" || UTF8(W.expected_statement))
and D.statement_correspondence = "exact"
and D.kind = "theorem"
and D.axiom_closure subseteq {
      "Classical.choice",
      "Quot.sound",
      "propext"
    }.
```

The release's Base `statement_id` is preserved in `paper-certified-claim.v1`. The Paper request statement uses a separate domain-separated digest. The adapter's explicit `statement_correspondence` evidence binds the two identity domains.

## Release-scoped mismatch

A mismatch against one release does not close the wait. A later release may still contain the exact declaration. Mismatch artifacts therefore keep:

```text
claim_status = pending-certification
```

and record the expected and observed values for one release only.

## Resolution immutability

Pair evaluations are keyed by:

```text
(certification_wait_ref, release_ref)
```

The first successful certified claim also creates a wait-level resolution cursor:

```text
certification_wait_ref -> certified_claim_ref
```

A later release cannot silently replace the claim that resolved the wait. Rebinding fails closed and requires a new governed Paper selection.

## Artifacts

The stage adds:

- `paper-certification-release.v1`
- `paper-certification-evaluation.v1`
- `paper-certification-mismatch.v1`
- `paper-certified-claim.v1`

All artifacts are canonical JSON in the existing Paper content-addressed store.

## Deliberate next boundary

`paper_certified_claim_ready` is still data, not manuscript text. The next Paper stage must build a certified claim manifest and require every formal theorem, lemma, and corollary in the manuscript to resolve to one `paper-certified-claim.v1` artifact. Conjectures and informal exposition remain explicitly separate.
