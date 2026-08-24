# Paper truth and Intuition indexes

Paper consumes two different authority classes.

## `PaperTruthIndex`

An upstream adapter verifies a `truth-release.v1` bundle through `Trureturing.Truth`
and writes `paper-truth-release-port.v1`. The Paper core owns the resulting research
index:

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
