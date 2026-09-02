using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperFrontierPlanningAgentSchemas
{
    public const string Dispatch = "paper-frontier-planning-agent-dispatch.v1";
    public const string Draft = "paper-formalization-frontier-draft.v1";
    public const string TaskStaged = "paper-frontier-planning-agent-task-staged.v1";
    public const string AdmissionCursor = "paper-frontier-planning-agent-cursor.v1";
    public const string ResultAdmitted = "paper-frontier-planning-agent-result-admitted.v1";
    public const string Ready = "paper-formalization-frontier-ready.v1";
    public const string NodeSelectionRequested = "paper-frontier-node-selection-requested.v1";
    public const string Failure = "paper-frontier-planning-agent-failure.v1";
}

public sealed record PaperFrontierPlanningAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PortfolioTaskRef,
    [property: JsonRequired] string PortfolioResultRef,
    [property: JsonRequired] string PortfolioCursorRef,
    [property: JsonRequired] string PortfolioDispatchRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] string JudgmentEvidenceRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] string UpdatedPortfolioRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperFormalizationFrontierDraft(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] IReadOnlyList<PaperFormalizationFrontierNodeSpec> NodeSpecs,
    [property: JsonRequired] string PlanningRationale,
    [property: JsonRequired] IReadOnlyList<string> RiskLedger,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperFrontierPlanningStoredArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string ContentPath,
    [property: JsonRequired] string EnvelopeRef,
    [property: JsonRequired] string EnvelopePath);

public sealed record PaperFrontierPlanningAgentTaskStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath,
    [property: JsonRequired] string PortfolioTaskRef,
    [property: JsonRequired] string PortfolioResultRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] bool Replayed);

public sealed record PaperFrontierPlanningNodeRoute(
    [property: JsonRequired] int DispatchOrder,
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] int ParallelWave,
    [property: JsonRequired] int Priority,
    [property: JsonRequired] string NextRoute);

public sealed record PaperFrontierPlanningAgentAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PortfolioTaskRef,
    [property: JsonRequired] string PortfolioResultRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] string JudgmentEvidenceRef,
    [property: JsonRequired] string UpdatedPortfolioRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] PaperFrontierPlanningStoredArtifact Frontier,
    [property: JsonRequired] PaperFrontierPlanningStoredArtifact InitialState,
    [property: JsonRequired] IReadOnlyList<PaperFrontierPlanningNodeRoute> InitialNodeRoutes,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperFrontierPlanningAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PortfolioTaskRef,
    [property: JsonRequired] string PortfolioResultRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] string JudgmentEvidenceRef,
    [property: JsonRequired] string UpdatedPortfolioRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] PaperFrontierPlanningStoredArtifact Frontier,
    [property: JsonRequired] PaperFrontierPlanningStoredArtifact InitialState,
    [property: JsonRequired] IReadOnlyList<PaperFrontierPlanningNodeRoute> InitialNodeRoutes,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);

public sealed record PaperFrontierPlanningAgentFailure(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string PortfolioTaskRef,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string BlockerCode,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string NextRoute);
