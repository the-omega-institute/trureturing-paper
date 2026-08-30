using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public sealed record PaperCandidateSplitProposalContent(
    [property: JsonRequired] string SourceTheoryProgramRef,
    [property: JsonRequired] string SourceTheoremPackageRef,
    [property: JsonRequired] string SourcePaperId,
    [property: JsonRequired] string ProposedPaperId,
    [property: JsonRequired] IReadOnlyList<string> ExtractedClaimIds,
    [property: JsonRequired] string IndependentResearchQuestion,
    [property: JsonRequired] IReadOnlyList<string> IndependentProofSpine,
    [property: JsonRequired] string ScopeMismatch,
    [property: JsonRequired] string PublicationRationale,
    [property: JsonRequired] string OverlapRisk,
    [property: JsonRequired] string ProposedAt);

public sealed record PaperCandidateSplitProposal(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ProposalId,
    [property: JsonRequired] PaperCandidateSplitProposalContent ProposalContent);

public sealed record PaperClaimOverlapPair(
    [property: JsonRequired] string SourceClaimId,
    [property: JsonRequired] string TargetClaimId,
    [property: JsonRequired] string Relation,
    [property: JsonRequired] string Evidence);

public sealed record PaperCandidateMergeProposalContent(
    [property: JsonRequired] string SourceTheoryProgramRef,
    [property: JsonRequired] string TargetTheoryProgramRef,
    [property: JsonRequired] string SourceTheoremPackageRef,
    [property: JsonRequired] string TargetTheoremPackageRef,
    [property: JsonRequired] string SourcePaperId,
    [property: JsonRequired] string TargetPaperId,
    [property: JsonRequired] string CanonicalPaperId,
    [property: JsonRequired] IReadOnlyList<PaperClaimOverlapPair> OverlapPairs,
    [property: JsonRequired] string UnifiedAbstraction,
    [property: JsonRequired] string MergeRationale,
    [property: JsonRequired] string ProposedAt);

public sealed record PaperCandidateMergeProposal(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ProposalId,
    [property: JsonRequired] PaperCandidateMergeProposalContent ProposalContent);

public sealed record PaperResearchLedgerEntryContent(
    [property: JsonRequired] string SourceTheoryProgramRef,
    [property: JsonRequired] string SourceTheoremPackageRef,
    [property: JsonRequired] string SourcePaperId,
    [property: JsonRequired] string DiscoveryKind,
    [property: JsonRequired] IReadOnlyList<string> RelatedRefs,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string WhyRecorded,
    [property: JsonRequired] string PromotionStatus,
    [property: JsonRequired] string RecordedAt);

public sealed record PaperResearchLedgerEntry(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string EntryId,
    [property: JsonRequired] PaperResearchLedgerEntryContent EntryContent);

public static class PaperTheoryPortfolioProposalService
{
    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex ClaimIdPattern =
        new("^[A-Za-z][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> TheoremLikeKinds =
        new(["lemma", "proposition", "theorem", "corollary"], StringComparer.Ordinal);
    private static readonly HashSet<string> MergeRelations = new(
        ["equivalent", "generalizes", "specializes", "shared-core", "incompatible-framing"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> DiscoveryKinds = new(
        ["split-candidate", "merge-candidate", "stronger-route",
         "counterexample", "prior-work-boundary"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> PromotionStatuses =
        new(["candidate-seed", "promoted", "parked", "consumed"], StringComparer.Ordinal);

    public static PaperCandidateSplitProposal CreateSplitProposal(
        PaperTheoremPackage package,
        PaperCandidateSplitProposalContent content)
    {
        PaperTheoryDeepeningService.Validate(package);
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.SourceTheoryProgramRef, "source_theory_program_ref");
        RequireDigest(content.SourceTheoremPackageRef, "source_theorem_package_ref");
        RequireText(content.SourcePaperId, "source_paper_id", 512);
        RequireText(content.ProposedPaperId, "proposed_paper_id", 512);
        if (string.Equals(content.SourcePaperId, content.ProposedPaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A split proposal must create a distinct paper identity.");
        }
        if (!string.Equals(
                content.SourceTheoryProgramRef,
                package.TheoremPackageContent.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(
                content.SourceTheoremPackageRef,
                package.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                content.SourcePaperId,
                package.TheoremPackageContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Split proposal changed its source paper or theorem package.");
        }

        RequireClaimIds(content.ExtractedClaimIds, "extracted_claim_ids", 2);
        var byId = package.TheoremPackageContent.Claims.ToDictionary(
            claim => claim.ClaimId,
            StringComparer.Ordinal);
        int theoremLike = 0;
        foreach (string id in content.ExtractedClaimIds)
        {
            if (!byId.TryGetValue(id, out PaperTheoremPackageClaim claim))
            {
                throw new InvalidDataException(
                    $"Split claim {id} is absent from the source package.");
            }
            if (TheoremLikeKinds.Contains(claim.Kind))
            {
                theoremLike++;
            }
        }
        if (theoremLike < 1)
        {
            throw new InvalidDataException(
                "A split proposal must extract at least one theorem-like claim.");
        }
        RequireText(content.IndependentResearchQuestion, "independent_research_question", 16384, 40);
        RequireTextList(content.IndependentProofSpine, "independent_proof_spine", 16384, 3);
        RequireText(content.ScopeMismatch, "scope_mismatch", 16384, 40);
        RequireText(content.PublicationRationale, "publication_rationale", 16384, 40);
        RequireText(content.OverlapRisk, "overlap_risk", 16384);
        ParseUtc(content.ProposedAt, "proposed_at");
        return new(
            PaperTheoryDeepeningSchemas.SplitProposal,
            Reference(content),
            content);
    }

    public static PaperCandidateMergeProposal CreateMergeProposal(
        PaperTheoremPackage source,
        PaperTheoremPackage target,
        PaperCandidateMergeProposalContent content)
    {
        PaperTheoryDeepeningService.Validate(source);
        PaperTheoryDeepeningService.Validate(target);
        ArgumentNullException.ThrowIfNull(content);
        if (string.Equals(
                source.TheoremPackageContent.PaperId,
                target.TheoremPackageContent.PaperId,
                StringComparison.Ordinal)
            || string.Equals(
                source.TheoremPackageContent.TheoryProgramRef,
                target.TheoremPackageContent.TheoryProgramRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A merge proposal requires two distinct paper programs.");
        }
        if (!string.Equals(content.SourceTheoryProgramRef, source.TheoremPackageContent.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(content.TargetTheoryProgramRef, target.TheoremPackageContent.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(content.SourceTheoremPackageRef, source.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(content.TargetTheoremPackageRef, target.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(content.SourcePaperId, source.TheoremPackageContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(content.TargetPaperId, target.TheoremPackageContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Merge proposal changed a source or target package coordinate.");
        }
        if (!string.Equals(content.CanonicalPaperId, content.SourcePaperId, StringComparison.Ordinal)
            && !string.Equals(content.CanonicalPaperId, content.TargetPaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "canonical_paper_id must be one of the merged papers.");
        }
        if (content.OverlapPairs is null || content.OverlapPairs.Count < 1)
        {
            throw new InvalidDataException(
                "A merge proposal needs at least one claim-overlap pair.");
        }
        var sourceIds = source.TheoremPackageContent.Claims
            .Select(claim => claim.ClaimId)
            .ToHashSet(StringComparer.Ordinal);
        var targetIds = target.TheoremPackageContent.Claims
            .Select(claim => claim.ClaimId)
            .ToHashSet(StringComparer.Ordinal);
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperClaimOverlapPair pair in content.OverlapPairs)
        {
            RequireClaimId(pair.SourceClaimId, "source_claim_id");
            RequireClaimId(pair.TargetClaimId, "target_claim_id");
            if (!sourceIds.Contains(pair.SourceClaimId)
                || !targetIds.Contains(pair.TargetClaimId)
                || !MergeRelations.Contains(pair.Relation))
            {
                throw new InvalidDataException(
                    "Merge overlap pair is unresolved or has an unsupported relation.");
            }
            RequireText(pair.Evidence, "evidence", 16384, 40);
            if (!pairs.Add($"{pair.SourceClaimId}\n{pair.TargetClaimId}"))
            {
                throw new InvalidDataException("Merge overlap pairs must be unique.");
            }
        }
        RequireText(content.UnifiedAbstraction, "unified_abstraction", 16384, 80);
        RequireText(content.MergeRationale, "merge_rationale", 16384, 80);
        ParseUtc(content.ProposedAt, "proposed_at");
        return new(
            PaperTheoryDeepeningSchemas.MergeProposal,
            Reference(content),
            content);
    }

    public static PaperResearchLedgerEntry CreateLedgerEntry(
        PaperTheoremPackage package,
        PaperResearchLedgerEntryContent content)
    {
        PaperTheoryDeepeningService.Validate(package);
        ArgumentNullException.ThrowIfNull(content);
        if (!string.Equals(
                content.SourceTheoryProgramRef,
                package.TheoremPackageContent.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(
                content.SourceTheoremPackageRef,
                package.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                content.SourcePaperId,
                package.TheoremPackageContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Research ledger entry changed its source package.");
        }
        if (!DiscoveryKinds.Contains(content.DiscoveryKind)
            || !PromotionStatuses.Contains(content.PromotionStatus))
        {
            throw new InvalidDataException(
                "Research ledger discovery kind or promotion status is unsupported.");
        }
        RequireDigestList(content.RelatedRefs, "related_refs", 0);
        RequireText(content.Summary, "summary", 16384, 40);
        RequireText(content.WhyRecorded, "why_recorded", 16384, 40);
        ParseUtc(content.RecordedAt, "recorded_at");
        return new(
            PaperTheoryDeepeningSchemas.ResearchLedgerEntry,
            Reference(content),
            content);
    }

    public static void Validate(PaperCandidateSplitProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        RequireExact(proposal.Schema, PaperTheoryDeepeningSchemas.SplitProposal, "schema");
        RequireDigest(proposal.ProposalId, "proposal_id");
        if (!string.Equals(
                proposal.ProposalId,
                Reference(proposal.ProposalContent),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "proposal_id does not address canonical split-proposal content.");
        }
    }

    public static void Validate(PaperCandidateMergeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        RequireExact(proposal.Schema, PaperTheoryDeepeningSchemas.MergeProposal, "schema");
        RequireDigest(proposal.ProposalId, "proposal_id");
        if (!string.Equals(
                proposal.ProposalId,
                Reference(proposal.ProposalContent),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "proposal_id does not address canonical merge-proposal content.");
        }
    }

    public static void Validate(PaperResearchLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        RequireExact(entry.Schema, PaperTheoryDeepeningSchemas.ResearchLedgerEntry, "schema");
        RequireDigest(entry.EntryId, "entry_id");
        if (!string.Equals(
                entry.EntryId,
                Reference(entry.EntryContent),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "entry_id does not address canonical ledger content.");
        }
    }

    private static void RequireClaimIds(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireClaimId(value, name);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireClaimId(string value, string name)
    {
        if (!ClaimIdPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} contains a noncanonical claim id.");
        }
    }

    private static void RequireDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireDigest(value, name);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximumLength,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, maximumLength);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireText(
        string value,
        string name,
        int maximumLength,
        int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static string Reference<T>(T content) =>
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content));

    private static DateTimeOffset ParseUtc(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }
}
