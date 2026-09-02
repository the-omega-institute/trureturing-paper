using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperFrontierNodeSelectionSchemas
{
    public const string Authorization =
        "paper-frontier-node-selection-authorization.v1";
    public const string VerificationBudget =
        "paper-frontier-verification-budget.v1";
    public const string CurrentStateCursor =
        "paper-frontier-current-state-cursor.v1";
    public const string Binding =
        "paper-frontier-formalization-binding.v1";
    public const string BindingLookup =
        "paper-frontier-formalization-binding-lookup.v1";
    public const string AdmissionCursor =
        "paper-frontier-node-selection-cursor.v1";
    public const string ResultAdmitted =
        "paper-frontier-node-selection-admitted.v1";
    public const string Ready =
        "paper-frontier-node-selection-ready.v1";
}

public sealed record PaperFrontierNodeSelectionAuthorizationContent(
    [property: JsonRequired] string FrontierPlanningTaskRef,
    [property: JsonRequired] string FrontierPlanningResultRef,
    [property: JsonRequired] string FrontierPlanningDispatchRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string InitialStateRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] int DispatchOrder,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] int ParallelWave,
    [property: JsonRequired] int Priority,
    [property: JsonRequired] string AuthorizedAt);

public sealed record PaperFrontierNodeSelectionAuthorization(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string AuthorizationId,
    [property: JsonRequired]
    PaperFrontierNodeSelectionAuthorizationContent AuthorizationContent);

public sealed record PaperFrontierVerificationBudgetContent(
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] int MaximumFormalizationRounds,
    [property: JsonRequired] bool RequireExactTruthRelease,
    [property: JsonRequired] bool RequireCertifiedDependencies,
    [property: JsonRequired] bool CounterexampleIsUseful,
    [property: JsonRequired] bool MissingPrerequisiteIsReportable,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperFrontierVerificationBudget(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string BudgetId,
    [property: JsonRequired]
    PaperFrontierVerificationBudgetContent BudgetContent);

public sealed record PaperFrontierNodeSelectionStoredArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string BlobRef,
    [property: JsonRequired] string RepositoryRelativePath);

public sealed record PaperFrontierCurrentStateCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact State,
    [property: JsonRequired] int Version,
    [property: JsonRequired] string UpdatedAt);

public sealed record PaperFrontierFormalizationBindingContent(
    [property: JsonRequired] string FrontierPlanningTaskRef,
    [property: JsonRequired] string FrontierPlanningResultRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string AuthorizationRef,
    [property: JsonRequired] string VerificationBudgetRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string SelectionEventRef,
    [property: JsonRequired] string RequestEventRef,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperFrontierFormalizationBinding(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string BindingId,
    [property: JsonRequired]
    PaperFrontierFormalizationBindingContent BindingContent);

public sealed record PaperFrontierFormalizationBindingLookup(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string BindingRef,
    [property: JsonRequired] string BindingBlobRef,
    [property: JsonRequired] string BindingPath);

public sealed record PaperFrontierNodeSelectionAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FrontierPlanningTaskRef,
    [property: JsonRequired] string FrontierPlanningResultRef,
    [property: JsonRequired] string FrontierPlanningDispatchRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string InitialStateRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] int DispatchOrder,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] int ParallelWave,
    [property: JsonRequired] int Priority,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact Authorization,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact VerificationBudget,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string SelectionBlobRef,
    [property: JsonRequired] string SelectionPath,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string FormalizationRequestBlobRef,
    [property: JsonRequired] string FormalizationRequestPath,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact SelectionEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact RequestEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact FrontierState,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact Binding,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperFrontierNodeSelectionAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FrontierPlanningTaskRef,
    [property: JsonRequired] string FrontierPlanningResultRef,
    [property: JsonRequired] string FrontierPlanningDispatchRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string InitialStateRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] int DispatchOrder,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] int ParallelWave,
    [property: JsonRequired] int Priority,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact Authorization,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact VerificationBudget,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string SelectionBlobRef,
    [property: JsonRequired] string SelectionPath,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string FormalizationRequestBlobRef,
    [property: JsonRequired] string FormalizationRequestPath,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact SelectionEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact RequestEvent,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact FrontierState,
    [property: JsonRequired]
    PaperFrontierNodeSelectionStoredArtifact Binding,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);
