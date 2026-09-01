# FKST frontier completion and manuscript planning

This layer closes the mathematical frontier before any manuscript authoring agent is allowed to write the paper.

## Position in the workflow

```text
A3-audited theorem package
  -> portfolio promotion
  -> formalization frontier
  -> successive governed Formalize waves
  -> certified frontier manifests
  -> frontier completion
  -> repository-generated manuscript plan
  -> existing certified-claim-manifest eligibility gate
```

The completion reducer is deterministic. It does not invoke Codex, change theorem statements, choose a weaker subset of claims, or write prose.

## Required frontier claims

Completion computes the required claim set as the union of:

- every theorem-package claim marked `load_bearing`;
- every declared main theorem;
- every sharpness theorem;
- every registered corollary.

Every required claim must have one frontier node and that node must be `manifested`. A successful Formalize process, an accepted proof candidate, or a certified claim that has not entered the frontier manifest is insufficient.

When any required node is incomplete, the reducer creates:

```text
paper-frontier-completion-pending.v1
reason = load-bearing-claims-incomplete
```

The pending receipt records the exact current frontier state and missing node IDs. It does not create a manuscript plan.

## One coherent manuscript truth release

Claims may be certified at different descendant releases. The manuscript needs one release in which all claims coexist.

The reducer scans registered certification releases and accepts a release only when:

```text
for every required claim C:
  selected release is C.certifying_release
  or selected release names C.certifying_release_digest as an ancestor

and

  selected release contains C's exact GID and Formalize request
  Lean declaration is unchanged
  requested-statement digest is unchanged
  statement ID is unchanged
  correspondence remains exact
  declaration kind remains theorem
  axiom closure is unchanged
```

Among coherent releases, there must be one unique release that descends from every other coherent candidate. Incomparable release branches remain pending:

```text
paper-frontier-completion-pending.v1
reason = coherent-truth-release-absent
```

A later merge release can unblock the frontier even after all individual claims have already been manifested. For that reason the FKST completion department consumes both:

```text
paper_frontier_certified_claim_manifest_ready
paper_certification_release_registered
```

Release registration triggers a refresh of all incomplete frontier candidates.

## Completion receipt

A successful reduction creates:

```text
paper-frontier-completion.v1
```

The receipt binds:

- frontier-planning task;
- final frontier state;
- paper and theory program;
- theorem package, A3 audit, scorecard, and portfolio decision;
- complete required node set;
- every frontier manifest, certified claim, Formalize request, GID, and certifying release;
- selected coherent manuscript release;
- generated manuscript plan;
- formal and informal item counts.

The receipt is content addressed. A terminal completion cursor prevents the same frontier from later being rebound to another plan or release.

## Manuscript plan construction

The plan is derived from the audited theorem package and exact frontier evidence.

```text
lemma       -> lemma formal claim, lem: label
proposition -> proposition formal claim, prop: label
theorem     -> theorem formal claim, thm: label
corollary   -> corollary formal claim, cor: label
definition  -> explicitly informal definition, def: label
counterexample -> explicitly informal example, ex: label
proof-interface -> explicitly informal remark, rem: label
```

Formal claim text is copied from the frontier node's exact formal statement. Definitions and other non-theorem items also retain their frontier formal statement as the plan text. The completion layer cannot paraphrase or omit a required claim.

The existing `paper-manuscript-plan.v1` and claim-manifest evaluator remain authoritative. The generated plan is registered through the existing plan registration service and immediately routed to:

```text
paper_manuscript_claim_evaluation_requested
```

The existing gate then independently reopens every certified claim and checks simultaneous declaration presence in the selected release.

## Replay and recovery

Pending receipts are state scoped and may be superseded by later frontier states or releases. Successful completion is terminal:

```text
frontier_ref
  -> completion_ref
  -> manuscript_plan_ref
  -> manuscript_truth_release_ref
```

Replay reloads the completion receipt, manuscript plan, completed frontier state, and selected release. It requires the completed state to be an ancestor of the current state and returns the same identities.

## Portfolio concurrency

Each frontier has its own completion lock. Different papers may complete simultaneously:

```text
Paper A frontier completion
Paper B A2 theory deepening
Paper C A3 review
Paper D Formalize certification
Paper E manuscript eligibility evaluation
```

A paper waiting for a coherent merge release does not consume a theory or Formalize worker.

## Authority boundary

```text
Theorem package
  defines the required scientific claims

Formalization frontier
  defines dependency order and exact formal statements

Formalize and certification
  establish certified declarations

Frontier completion
  proves all required nodes are manifested in one coherent release

Certified-claim-manifest gate
  proves the generated manuscript plan is eligible

Manuscript authoring agent
  may begin only after the eligibility receipt exists
```
