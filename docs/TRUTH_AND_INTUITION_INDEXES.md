# Paper truth and Intuition indexes

Paper consumes two different authority classes.

## `PaperTruthIndex`

A separate, planned upstream adapter will verify a `truth-release.v1` bundle through
`Trureturing.Truth` and write `paper-truth-release-port.v1`. This repository does not
provide that adapter. The Paper core owns the resulting research index:

- declaration and statement identity;
- exact frozen prerequisite closure;
- axiom closure queries;
- source commit/tree and release digest;
- Blueprint/mdBook anchors.

Paper does not parse the upstream truth graph or replay the Frozen Ledger.

## `PaperIntuitionIndex`

`paper-intuition-port.v1` is advisory. It contains candidate bridges, evidence,
falsifiers and predicted reachability/pruning. The port is bound to the exact truth
release used by `PaperTruthIndex`.

An Intuition proposal cannot be retrieved as a certified declaration. Paper may use
it to identify a load-bearing gap or plan a research programme, while the claim gate
continues to require certified truth for factual mathematical assertions.

The candidate pipeline consumes only bridges whose Intuition status is `proved`. This is
a selection rule, not an authority promotion: the bridge's central claim is still written
as `conjectured` in `candidate-paper.v1`. Only an input resolved from `PaperTruthIndex` can
produce a `certified` key claim. Missing or advisory-only inputs fail closed.
