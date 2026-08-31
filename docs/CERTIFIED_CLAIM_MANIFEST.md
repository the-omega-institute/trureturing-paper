# Certified claim manifest and manuscript eligibility

This stage is the first Paper component allowed to say that a theorem-level item is eligible for manuscript assembly.

It consumes `paper-certified-claim.v1` artifacts produced by the certification join. It does not run Lean, write Base, adopt a patch, or create truth. Its job is narrower:

1. separate formal claims from conjectures and informal exposition;
2. resolve every theorem, lemma, and corollary to an exact Paper-certified claim;
3. require all formal claims to coexist in one explicitly selected truth release;
4. emit a closed, content-addressed claim manifest and an eligibility receipt.

## Workflow

```text
inbox/manuscript-plans/*.json
        |
        v
paper_manuscript_plan_seen
        |
        v
register-manuscript-plan
        |
        +--> paper_manuscript_plan_registered
        |
        +--> paper_manuscript_claim_evaluation_requested
                    |
                    v
            evaluate-manuscript-plan
                    |
                    +-- evidence absent
                    |       -> paper_manuscript_claims_pending
                    |
                    +-- binding or release incoherent
                    |       -> paper_manuscript_claims_ineligible
                    |
                    +-- every formal claim certified
                            -> paper_certified_claim_manifest_ready

paper_certified_claim_ready
        |
        v
refresh-manuscript-plans
        |
        v
paper_manuscript_claim_evaluation_requested
```

A manuscript plan may arrive before its claims. A certified claim may arrive before a plan. Plan registration evaluates immediately, and every later `paper_certified_claim_ready` event re-evaluates all registered plans. The event is only a trigger. The evaluator reopens all evidence from the content-addressed store.

## Manuscript plan

`paper-manuscript-plan.v1` has two disjoint arrays.

### Formal claims

Every item of `formal_claims` is one of:

```text
theorem
lemma
corollary
```

and carries:

```text
claim_id
latex_label
claim_kind
certified_claim_ref
statement
role_in_argument
```

The statement must equal the exact requested statement in the referenced `paper-certified-claim.v1`. The label prefix is also typed:

```text
theorem   -> thm:
lemma     -> lem:
corollary -> cor:
```

A certified claim reference may appear only once. Claim IDs and LaTeX labels are globally unique across the plan.

### Informal exposition

`informal_exposition` cannot carry a certified-claim reference. Its kinds are:

```text
conjecture
definition
example
remark
motivation
discussion
limitation
```

A conjecture must use:

```text
epistemic_status = conjectured
```

Every other informal item must use:

```text
epistemic_status = explicitly-informal
```

The exact text is preserved in the manifest with a domain-separated digest:

```text
SHA256(
  "trureturing:paper-exposition-text:v1\0"
  || UTF8(text)
)
```

## One coherent truth release

A collection of individually certified claims is insufficient for a manuscript. Two claims may have been certified against incomparable truth states.

The plan therefore selects one:

```text
manuscript_truth_release_ref
```

For every formal claim `C` and selected release `R`, the gate requires:

```text
R.digest = C.certifying_release_digest
or
C.certifying_release_digest in R.ancestor_release_digests
```

It then checks that `R` itself contains the same declaration with the same:

```text
GID
Lean declaration name
Formalize request reference
requested-statement digest
Base statement ID
exact statement correspondence
theorem declaration kind
axiom closure
```

This creates a simultaneous-release predicate:

```text
manuscript_eligible(plan, R) iff
    every formal item resolves to a valid paper-certified-claim
and every resolved claim belongs to plan.paper_id
and every planned statement equals its certified statement
and all certified declarations are present unchanged in R
and conjectures and informal exposition have explicit epistemic status.
```

## Outputs

### Pending

`paper-manuscript-claims-pending.v1` lists missing content-addressed evidence references. It is nonterminal. A later certified-claim event changes the evidence-presence state and creates a new evaluation key.

### Ineligible

`paper-manuscript-claims-ineligible.v1` records the first closed mismatch with expected and observed values. Reasons include paper identity drift, statement drift, duplicate theorem identity, release lineage mismatch, declaration absence, request mismatch, statement mismatch, declaration-kind mismatch, and axiom mismatch.

Because all referenced evidence is immutable, an ineligible plan is terminal. Repair requires a new manuscript-plan artifact.

### Eligible

A successful evaluation emits:

```text
paper-certified-claim-manifest.v1
paper-manuscript-eligibility.v1
```

The manifest keeps the full formalization, selection, literature, candidate, release, Lean declaration, statement identity, and axiom provenance for each formal claim. Informal items remain in their separate array.

The eligibility receipt states:

```text
formal_claims_certified = true
exact_release_coherent = true
epistemic_boundaries_explicit = true
status = eligible
```

## Idempotence and resolution

Evaluation cursors are keyed by:

```text
(manuscript_plan_ref, evidence_state_ref)
```

where the evidence state contains the presence bit for the selected release and every certified-claim reference. A pending evaluation can therefore later become eligible without rebinding its earlier cursor.

The first terminal result creates:

```text
manuscript_plan_ref -> terminal evaluation
```

A plan cannot later switch between ineligible and eligible. Any scientific or editorial correction must produce a new content-addressed plan.

## Deliberate next boundary

`paper_certified_claim_manifest_ready` is the input to manuscript assembly. The next Paper stage should render a journal-neutral manuscript from the plan and manifest, then ensure every rendered formal environment and LaTeX label is byte-bound to the manifest. Rendering must not create, delete, or rewrite theorem-level claims.
