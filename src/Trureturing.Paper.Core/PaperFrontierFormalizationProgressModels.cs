using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperFrontierFormalizationProgressSchemas
{
    public const string TransportCursor =
        "paper-frontier-formalize-transport-cursor.v1";
    public const string OutcomeCursor =
        "paper-frontier-formalization-outcome-cursor.v1";
    public const string CertifiedManifest =
        "paper-frontier-certified-claim-manifest.v1";
    public const string ReadySet =
        "paper-frontier-ready-set.v1";
    public const string CertificationCursor =
        "paper-frontier-certification-cursor.v1";
    public const string TransportRecorded =
        "paper-frontier-formalize-transport-recorded.v1";
    public const string OutcomeRecorded =
        "paper-frontier-formalization-outcome-recorded.v1";
    public const string CertificationRecorded =
        "paper-frontier-certification-recorded.v1";
}

public static class PaperFrontierFormalizationProgressStatuses
{
    public const string Recorded = "recorded";
    public const string NotFrontierBound = "not-frontier-bound";
    public const string Ignored = "ignored";
}

public sealed record PaperFrontierFormalizeTransportCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact TransportEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact FrontierState,
    [property: JsonRequired] string RecordedAt);

public sealed record PaperFrontierFormalizationOutcomeCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DecisionRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string OutcomeClass,
    [property: JsonRequired] string OutcomeDisposition,
    [property: JsonRequired] string Route,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact OutcomeEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact FrontierState,
    [property: JsonRequired] string RecordedAt);

public sealed record PaperFrontierCertifiedClaimManifestContent(
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string FormalizationResultRef,
    [property: JsonRequired] string DecisionRef,
    [property: JsonRequired] string CertificationEvaluationRef,
    [property: JsonRequired] string CertifiedClaimRef,
    [property: JsonRequired] string CertifyingReleaseRef,
    [property: JsonRequired] string CertifyingReleaseDigest,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string LeanDeclaration,
    [property: JsonRequired] string DeclarationKind,
    [property: JsonRequired] string StatementId,
    [property: JsonRequired] IReadOnlyList<string> AxiomClosure,
    [property: JsonRequired] string ManifestedAt);

public sealed record PaperFrontierCertifiedClaimManifest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManifestId,
    [property: JsonRequired]
    PaperFrontierCertifiedClaimManifestContent ManifestContent);

public sealed record PaperFrontierReadyNode(
    [property: JsonRequired] int DispatchOrder,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] int ParallelWave,
    [property: JsonRequired] int Priority,
    [property: JsonRequired] string NextRoute);

public sealed record PaperFrontierReadySetContent(
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string TriggerNodeId,
    [property: JsonRequired] string TriggerManifestRef,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] IReadOnlyList<PaperFrontierReadyNode> ReadyNodes,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperFrontierReadySet(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ReadySetId,
    [property: JsonRequired] PaperFrontierReadySetContent ReadySetContent);

public sealed record PaperFrontierCertificationCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string CertifiedClaimRef,
    [property: JsonRequired] string CertifyingReleaseRef,
    [property: JsonRequired] string CertifyingReleaseDigest,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact CertificationEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact CertifiedManifest,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact ManifestEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact FrontierState,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact ReadySet,
    [property: JsonRequired] string RecordedAt);

public sealed record PaperFrontierFormalizeTransportRecorded(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] string TransportEventRef,
    [property: JsonRequired] bool Replayed);

public sealed record PaperFrontierFormalizationOutcomeRecorded(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DecisionRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string OutcomeClass,
    [property: JsonRequired] string OutcomeDisposition,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] string OutcomeEventRef,
    [property: JsonRequired] bool Replayed);

public sealed record PaperFrontierCertificationRecorded(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string CertifiedClaimRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string CertifiedManifestRef,
    [property: JsonRequired] string ReadySetRef,
    [property: JsonRequired] IReadOnlyList<PaperFrontierReadyNode> ReadyNodes,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] bool Replayed);
