# Contracts

`source-snapshot.v1.schema.json` is vendored unchanged from
`trureturing-fkst-deployments` and pinned by
`source-snapshot.v1.schema.sha256`. It is the sole canonical cross-organ entry contract.
The paper-owned truth and Intuition port schemas are typed contracts for their C# reader;
the remaining local v1 schemas describe walking-skeleton inputs and must not be treated as
extensions to `source-snapshot.v1`.

The three candidate artifact schemas are Paper-owned output contracts:

- `candidate-paper.v1` separates certified index facts from conjectured bridge claims;
- `literature-research.v1` records the reproducible library-before-proof search result;
- `candidate-journal.v1` records possible venues without pretending an unmeasured fit is a score.

Generated artifacts use canonical JSON and SHA-256 content-addressed filenames under
`Papers/candidates/`. They are reasoning data only; rendering belongs to Pages.

The shared external `certified-topology.v1` schema is vendored byte-for-byte from
`trureturing-fkst-packages/packages/trureturing-topology` and pinned by
`certified-topology.v1.schema.sha256`; it remains upstream-owned.
`CertifiedTopologyReader` implements that closed schema. Candidate-paper and literature output schemas own
their structural-context projection: unbounded integers and reduced rational components are
preserved as decimal strings, accompanied by all three topology binding coordinates. The
projection is advisory and has no claim-gate authority.

`formalization-request.v1.schema.json` is vendored byte-for-byte from
`trureturing-fkst-packages/contracts/formalization-request.v1.schema.json` at
commit `6008d6a98ca2c4f00a4e88e82c4b64b88262fda1` and pinned by
`formalization-request.v1.schema.sha256`. Paper resolves the selected
`paper_research_input_ref` from its content-addressed store before emitting this request,
then binds the canonical request to the exact truth release commit, tree, and digest.
`paper-research-selection.v1` remains Paper-owned governance evidence and carries the
candidate, literature, boundary, falsifier, proof constraints, and authorization context
that precede the cross-organ request.
