# A2 evidence closure

An abstract-theory worker must read the evidence that gave rise to the paper candidate. A theory-program reference alone records coordinates and does not materialize the referenced candidate, literature, Intuition, or exact research state inside the isolated Codex workspace.

Every round-one A2 dispatch therefore contains the complete immutable closure:

```text
paper-theory-program.v1
paper candidate artifact
paper literature-research artifact
paper Intuition-proposal artifact
paper-research-input.v1
paper-theory-scope.v1
paper-theory-inventory.v1
paper-theory-deepening-request.v1
```

A later round adds the immediately prior `paper-theorem-package.v1`. The repository contract consequently accepts exactly seven domain evidence references in round one and exactly eight in later rounds. The immutable A2 dispatch itself is appended only when the generic agent task is staged.

`PaperTheoryDeepeningService.CreateDeepeningRequest` owns this reference set. The native A2 bridge re-creates that request and requires the dispatch inputs to equal its exact references plus the request itself. Each referenced file is then hash-verified and copied into the task workspace.

This closes four failure modes:

```text
reasoning from a paper-program summary without reading the candidate
claiming novelty without reading the literature evidence
changing the abstraction while omitting the originating Intuition
mixing a scope or theorem package with another truth/topology research state
```

Live-source acquisition remains a separate governed stage. A2 consumes the latest admitted literature artifact supplied to its exact theory program and cannot use unrecorded network searches.
