# Three-round SSHX review protocol

The adapter must invoke the project's real SSHX mechanism. The default script
exits with a blocking status when no adapter is configured. Review completion is
therefore never inferred from a missing command or generated placeholder.

Round 1 audits mathematical correctness, statement faithfulness, hidden
assumptions, counterexamples, and Lean dependencies. Round 2 audits novelty,
nearest-prior-result comparisons, significance, and journal tier and scope fit.
Round 3 audits submission readiness, exposition, reproducibility, and agreement
among Lean declarations, LaTeX claims, title, abstract, and cover letter.

Each round uses a distinct reviewer identity and a fresh context. The adapter
writes a full Markdown report and exactly one TSV summary row under
`Papers/research/reviews/`. The summary contains reviewer identity, round, focus,
verdict, blocking issue count, report SHA-256, and manuscript SHA-256. Report
hashes must be distinct. The manuscript hash must change between rounds, proving
that a revision occurred. Round 3 must pass with zero blocking issues.
