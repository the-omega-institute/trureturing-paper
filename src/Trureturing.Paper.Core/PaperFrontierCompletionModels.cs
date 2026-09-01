using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperFrontierCompletionSchemas
{
    public const string Receipt = "paper-frontier-completion.v1";
    public const string Pending = "paper-frontier-completion-pending.v1";
    public const string Cursor = "paper-frontier-completion-cursor.v1";
    public const string Evaluated = "paper-frontier-completion-evaluated.v1";
    public const string Ready = "paper-frontier-completion-ready.v1";
    public const string CandidatesListed =
        "paper-frontier-completion-candidates-listed.v1";
}

public static class PaperFrontierCompletionStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
}

public static class PaperFrontierCompletionReasons
{
    public const string LoadBearingClaimsIncomplete =
        "load-bearing-claims-incomplete";
    public const string CoherentTruthReleaseAbsent =
        "coherent-truth-release-absent";
    public const string Complete = "frontier-complete";
}

public sealed record PaperFrontierCompletionClaim(
    [property: JsonRequired] int Order,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string TheoremPackageKind,
    [property: JsonRequired] bool LoadBearing,
    [property: JsonRequired] string FrontierManifestRef,
    [property: JsonRequired] string CertifiedClaimRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string CertifyingReleaseRef,
    [property: JsonRequired] string CertifyingReleaseDigest,
    [property: JsonRequired] string ManuscriptDisposition,
    [property: JsonRequired] string ManuscriptClaimKind,
    [property: JsonRequired] string LatexLabel);

public sealed record PaperFrontierCompletionReceipt(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string FrontierPlanningTaskRef,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] IReadOnlyList<string> RequiredNodeIds,
    [property: JsonRequired] IReadOnlyList<PaperFrontierCompletionClaim> Claims,
    [property: JsonRequired] string ManuscriptTruthReleaseRef,
    [property: JsonRequired] string ManuscriptTruthReleaseDigest,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] int FormalClaimCount,
    [property: JsonRequired] int InformalItemCount,
    [property: JsonRequired] string CompletedAt);

public sealed record PaperFrontierCompletionPending(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] IReadOnlyList<string> MissingNodeIds,
    [property: JsonRequired] IReadOnlyList<string> BlockingReleaseRefs,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string CheckedAt);

public sealed record PaperFrontierCompletionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string ManuscriptTruthReleaseRef,
    [property: JsonRequired] string ManuscriptTruthReleaseDigest,
    [property: JsonRequired] string CompletedAt);

public sealed record PaperFrontierCompletionEvaluated(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string PendingRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string ManuscriptTruthReleaseRef,
    [property: JsonRequired] string ManuscriptTruthReleaseDigest,
    [property: JsonRequired] int FormalClaimCount,
    [property: JsonRequired] int InformalItemCount,
    [property: JsonRequired] IReadOnlyList<string> MissingNodeIds,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] bool Replayed);


public sealed record PaperFrontierCompletionCandidatesListed(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] IReadOnlyList<string> FrontierRefs);
