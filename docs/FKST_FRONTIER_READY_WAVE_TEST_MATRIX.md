# Frontier ready-wave admission test matrix

This matrix records the behavioral evidence required before a dependency-ready frontier wave may be released to Formalize.

| Boundary | Required behavior | Failure that must be rejected |
| --- | --- | --- |
| Ready-set source | The ready set is stored by exactly one frontier certification cursor. | A free-standing or multiply bound ready-set reference. |
| Frontier identity | Ready set, certification cursor, trigger manifest, source state, and planning admission name one frontier. | Cross-frontier ready-set rebinding. |
| Node identity | Every ready route matches the immutable frontier node, theorem-package claim, kind, wave, and priority. | Invented, renamed, omitted, or reordered claims. |
| Dependency evidence | Every dependency is `manifested` in the ready set's source state and remains manifested in current state. | Certification-pending, merely certified, failed, or missing dependencies. |
| Selection construction | The existing governed selection constructor creates authorization, budget, selection, request, events, and binding. | A later-wave-specific request constructor with divergent semantics. |
| Dependency API | Dependency GIDs populate both known dependencies and reuse API. | Literature prose or uncatalogued names presented as Base APIs. |
| State transition | Each node advances from `selection-pending` to `request-recorded` through two existing lifecycle events. | Direct advancement to transport, certification, or manifested state. |
| Batch ordering | Admissions preserve ready-set dispatch order. | Agent- or event-selected ordering. |
| Partial replay | Existing node cursors are replayed and remaining nodes are admitted before the batch cursor is written. | Duplicated selections or a second Formalize request for one node. |
| Batch replay | The same ready set returns the same node selections, requests, bindings, and final state. | Ready-set rebinding or state regression. |
| Successive waves | Manifesting wave zero releases wave one, and manifesting wave one releases wave two when dependencies permit. | One-shot scheduling that cannot progress the theorem DAG. |
| Existing transport | A later-wave canonical request enters the same Formalize dispatch and outcome lifecycle as wave zero. | A parallel proof-search or certification path. |
| FKST boundary | One batch receipt and the existing per-node ready/request events are emitted under the frontier lock. | Codex execution, Git mutation, `dotnet run`, or event-authored claims. |
| Contract boundary | Initial node-selection requests remain wave-zero-only, while admitted node artifacts accept non-negative waves. | Relaxing the initial frontier planning event to invent later routes. |

The integration tests construct a two-paper portfolio, promote one audited theorem package, plan a five-claim frontier, certify the root definition in a descendant release, admit the reduction lemma, certify that lemma, and admit the main theorem. The same fixtures verify replay and continued transport through the existing Formalize result classifier.
