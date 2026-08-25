namespace Trureturing.Paper.Core;

public static class CandidatePipeline
{
    private const string TraceNormRelation =
        "Trace and norm compatibility under conjugation";

    public static IReadOnlyList<CandidateProposalArtifacts> Propose(
        PaperTruthIndex truth,
        PaperIntuitionIndex intuition)
    {
        ArgumentNullException.ThrowIfNull(truth);
        ArgumentNullException.ThrowIfNull(intuition);
        if (!string.Equals(
                intuition.SourceTruthReleaseDigest,
                truth.ReleaseDigest,
                StringComparison.Ordinal))
        {
            throw new ClaimGateException(
                "Candidate generation requires matching truth and Intuition releases.");
        }

        return intuition.Candidates
            .Where(candidate => candidate.Status == "proved")
            .OrderBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .Select(candidate => ProposeOne(truth, candidate))
            .ToArray();
    }

    private static CandidateProposalArtifacts ProposeOne(
        PaperTruthIndex truth,
        PaperIntuitionEntry bridge)
    {
        PaperTruthEntry[] certified = bridge.Inputs
            .Distinct(StringComparer.Ordinal)
            .Select(truth.GetDeclaration)
            .OrderBy(entry => entry.DeclarationId, StringComparer.Ordinal)
            .ToArray();
        if (certified.Length == 0)
        {
            throw new ClaimGateException(
                $"Proved bridge {bridge.ProposalId} has no certified inputs.");
        }

        string bridgeRef =
            $"paper-intuition-port.v1@{truth.ReleaseDigest}#{bridge.ProposalId}";
        CandidateKeyClaim[] certifiedClaims = certified.Select(entry =>
            new CandidateKeyClaim(
                $"The certified release records {entry.DeclarationId} as a " +
                $"{entry.Kind} with statement identity {entry.StatementId}.",
                "certified",
                $"paper-truth-release-port.v1@{truth.ReleaseDigest}#{entry.DeclarationId}"))
            .ToArray();
        CandidateKeyClaim conjecturedClaim = new(
            bridge.RelationType,
            "conjectured",
            bridgeRef);

        var paper = new CandidatePaperArtifact(
            CandidateArtifactSchemas.CandidatePaper,
            bridge.RelationType,
            $"The certified results may support a precise theorem about " +
            $"{bridge.RelationType.ToLowerInvariant()}; the bridge remains a research " +
            "claim until it enters the certified truth release.",
            new CandidateGrounding(
                certified.Select(entry => entry.FrozenNodeId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                [bridgeRef]),
            [
                "Certified foundations and provenance",
                "Statement of the candidate bridge",
                "Prior literature and novelty boundary",
                "Proof strategy and falsification conditions",
                "Consequences and certification plan"
            ],
            certifiedClaims.Append(conjecturedClaim).ToArray(),
            $"This research candidate starts from {certified.Length} certified " +
            $"result{(certified.Length == 1 ? string.Empty : "s")} and the proved " +
            $"Intuition bridge {bridge.ProposalId}. It proposes a focused study of " +
            $"{bridge.RelationType.ToLowerInvariant()}. Certified metadata and the " +
            "candidate mathematical claim are kept separate: the latter is conjectured " +
            "until independently checked and admitted to a certified release.");

        return new CandidateProposalArtifacts(
            paper,
            LiteratureFor(bridge.RelationType),
            CandidateVenues());
    }

    private static LiteratureResearchArtifact LiteratureFor(string claim)
    {
        if (!string.Equals(claim, TraceNormRelation, StringComparison.Ordinal))
        {
            return new LiteratureResearchArtifact(
                CandidateArtifactSchemas.LiteratureResearch,
                claim,
                [
                    $"Crossref bibliographic query: {claim}",
                    $"arXiv all-fields query: {claim}"
                ],
                [],
                "partial",
                "No source in the checked research catalog verifies this exact claim. " +
                "The literature result is therefore unverified and no novelty claim is made.");
        }

        return new LiteratureResearchArtifact(
            CandidateArtifactSchemas.LiteratureResearch,
            claim,
            [
                "Crossref title query: The invariant theory of n x n matrices",
                "Crossref title query: Algebraic invariants for a set of matrices",
                "Crossref bibliographic query: reduced trace reduced norm central simple algebra conjugation",
                "arXiv all-fields query: trace invariants AND conjugation"
            ],
            [
                new RelatedWork(
                    "The invariant theory of n x n matrices",
                    ["Claudio Procesi"],
                    "Advances in Mathematics",
                    1976,
                    "https://doi.org/10.1016/0001-8708(76)90027-x",
                    "prior-art",
                    "verified"),
                new RelatedWork(
                    "Algebraic invariants for a set of matrices",
                    ["K. S. Sibirskii"],
                    "Siberian Mathematical Journal",
                    1968,
                    "https://doi.org/10.1007/bf02196663",
                    "prior-art",
                    "verified"),
                new RelatedWork(
                    "The Invariant Theory of Matrices",
                    ["Corrado De Concini", "Claudio Procesi"],
                    "American Mathematical Society University Lecture Series",
                    2017,
                    "https://doi.org/10.1090/ulect/069",
                    "related",
                    "verified"),
                new RelatedWork(
                    "Conjugation of elements in central simple algebras",
                    ["Oliver Villa"],
                    "Communications in Algebra",
                    2017,
                    "https://doi.org/10.1080/00927872.2016.1233188",
                    "related",
                    "verified")
            ],
            "known",
            "Classical matrix invariant theory already treats trace-based conjugation " +
            "invariants, and the central-simple-algebra literature treats conjugacy in a " +
            "setting with reduced trace and norm. The broad claim is known. A publishable " +
            "candidate must state a narrower system-specific theorem or a new synthesis; it " +
            "must not present trace/norm conjugation compatibility itself as novel.");
    }

    private static IReadOnlyList<CandidateVenue> CandidateVenues() =>
    [
        new CandidateVenue(
            "Journal of Algebra",
            "A fit if the candidate becomes a substantive new theorem in ring, field, or " +
            "central simple algebra rather than a formalization report.",
            ["associative algebra", "ring theory", "field theory", "invariant theory"],
            "open"),
        new CandidateVenue(
            "Linear Algebra and its Applications",
            "A fit if the central result is formulated for matrices or linear operators and " +
            "adds a nontrivial trace, determinant, norm, or conjugation theorem.",
            ["matrix theory", "linear algebra", "operator theory", "matrix invariants"],
            "open"),
        new CandidateVenue(
            "Communications in Algebra",
            "A fit for a focused algebraic advance connecting conjugation with trace or norm " +
            "in a clearly specified algebraic structure.",
            ["general algebra", "noncommutative algebra", "ring theory", "representation theory"],
            "open")
    ];
}
