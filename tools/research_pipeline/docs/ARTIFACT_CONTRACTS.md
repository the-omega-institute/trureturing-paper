# Artifact contracts

## Candidate theorem

`Papers/research/candidate-theorem.md` must state the exact hypotheses and
conclusion, nearest prior results, attempted stronger variants, counterexamples,
expected proof mechanism, and proposed Lean declaration location.

## Claim manifest

`Papers/research/claim-manifest.tsv` maps every theorem-level LaTeX claim to a
Lean declaration and file. Formal rows require `proof_status=verified` and a
64-hex SHA-256 evidence value. Informal rows require
`exposition_status=explicitly_informal`. The release gate requires at least one
formal row.

## Novelty ledger

`Papers/research/novelty-ledger.tsv` contains theorem-level comparators. A row is
admissible only when every delta field is concrete, the check date is current,
and `novelty_status=supported`.

## Journal ledger

`Papers/research/journal-candidates.tsv` records an internal policy tier, scope
fit, article type, current requirements URL, date checked, format profile,
disqualifiers, eligibility, and rationale. `policy_tier` is a declared project
policy. It is not represented as a universal journal ranking.

## Lean receipts

The writeback and verification workers write JSON receipts under
`.fkst-pipeline/artifacts/`. A production contract should bind repository,
commit, toolchain, source file, declaration name, statement digest, build result,
axiom closure, worker identity, and command-log digest. The current C gate accepts
canonical compact receipts containing `"status":"pass"`; producer adapters must
fail nonzero for every other status.

## Review and revision receipts

Each SSHX summary is durable research evidence under `Papers/research/reviews/`.
Each revision worker writes a receipt under `Papers/research/revisions/` binding
the input review hash, manuscript hash before and after revision, changed claims,
and unresolved findings.
