using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperFrontierReadyWaveSelectionSchemas
{
    public const string AdmissionCursor =
        "paper-frontier-ready-wave-selection-cursor.v1";
    public const string Admitted =
        "paper-frontier-ready-wave-selection-admitted.v1";
    public const string Ready =
        "paper-frontier-ready-wave-selection-ready.v1";
}

public sealed record PaperFrontierReadyWaveNodeAdmission(
    [property: JsonRequired] int DispatchOrder,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] int ParallelWave,
    [property: JsonRequired] int Priority,
    [property: JsonRequired] string AuthorizationRef,
    [property: JsonRequired] string VerificationBudgetRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string BindingRef,
    [property: JsonRequired] string FrontierStateRef,
    [property: JsonRequired] string Gid);

public sealed record PaperFrontierReadyWaveSelectionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ReadySetRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string TriggerNodeId,
    [property: JsonRequired] string TriggerManifestRef,
    [property: JsonRequired] string ReleaseStateRef,
    [property: JsonRequired] string FrontierPlanningTaskRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired]
    IReadOnlyList<PaperFrontierReadyWaveNodeAdmission> NodeAdmissions,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperFrontierReadyWaveSelectionAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ReadySetRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string TriggerNodeId,
    [property: JsonRequired] string TriggerManifestRef,
    [property: JsonRequired] string ReleaseStateRef,
    [property: JsonRequired] string FrontierPlanningTaskRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired]
    IReadOnlyList<PaperFrontierNodeSelectionAdmitted> NodeAdmissions,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);
