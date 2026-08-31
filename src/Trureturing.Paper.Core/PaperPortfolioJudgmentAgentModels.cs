using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperPortfolioJudgmentAgentSchemas
{
    public const string Dispatch = "paper-portfolio-judgment-agent-dispatch.v1";
    public const string Draft = "paper-portfolio-judgment-draft.v1";
    public const string Evidence = "paper-portfolio-judgment-evidence.v1";
    public const string TaskStaged = "paper-portfolio-judgment-agent-task-staged.v1";
    public const string AdmissionCursor = "paper-portfolio-judgment-agent-cursor.v1";
    public const string ResultAdmitted = "paper-portfolio-judgment-agent-result-admitted.v1";
    public const string Ready = "paper-portfolio-judgment-ready.v1";
    public const string Failure = "paper-portfolio-judgment-agent-failure.v1";
}

public sealed record PaperPortfolioJudgmentPaperInput(
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef);

public sealed record PaperPortfolioJudgmentAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] PaperPortfolioDecisionPolicy Policy,
    [property: JsonRequired] IReadOnlyList<PaperPortfolioJudgmentPaperInput> Papers,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperPortfolioJudgmentPaperDraft(
    [property: JsonRequired] int Rank,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string RecommendedAction,
    [property: JsonRequired] string ComparativeAdvantage,
    [property: JsonRequired] string PrincipalRisk,
    [property: JsonRequired] string Rationale);

public sealed record PaperPortfolioPairwiseRelationDraft(
    [property: JsonRequired] string LeftPaperId,
    [property: JsonRequired] string RightPaperId,
    [property: JsonRequired] string Relation,
    [property: JsonRequired] string PreferredOwnerPaperId,
    [property: JsonRequired] IReadOnlyList<string> EvidenceRefs,
    [property: JsonRequired] string TheoremInteraction,
    [property: JsonRequired] string NoveltyInteraction);

public sealed record PaperPortfolioJudgmentDraft(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] IReadOnlyList<string> ComparedScorecardRefs,
    [property: JsonRequired] IReadOnlyList<PaperPortfolioJudgmentPaperDraft> OrderedPapers,
    [property: JsonRequired] IReadOnlyList<PaperPortfolioPairwiseRelationDraft> PairwiseRelations,
    [property: JsonRequired] string PortfolioRationale,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperPortfolioJudgmentEvidenceContent(
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string AgentResultRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] IReadOnlyList<string> ComparedScorecardRefs,
    [property: JsonRequired] IReadOnlyList<string> RankedPaperIds,
    [property: JsonRequired] IReadOnlyList<PaperPortfolioPairwiseRelationDraft> PairwiseRelations,
    [property: JsonRequired] string PortfolioRationale,
    [property: JsonRequired] string DecisionRef,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperPortfolioJudgmentEvidence(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string EvidenceId,
    [property: JsonRequired] PaperPortfolioJudgmentEvidenceContent EvidenceContent);

public sealed record PaperPortfolioJudgmentStoredArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string ContentPath,
    [property: JsonRequired] string EnvelopeRef,
    [property: JsonRequired] string EnvelopePath);

public sealed record PaperPortfolioJudgmentAgentTaskStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] int ComparedPaperCount,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] bool Replayed);

public sealed record PaperPortfolioJudgmentPaperRoute(
    [property: JsonRequired] int Rank,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string Action,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string Reason);

public sealed record PaperPortfolioJudgmentAgentAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] PaperPortfolioJudgmentStoredArtifact Evidence,
    [property: JsonRequired] PaperPortfolioJudgmentStoredArtifact Decision,
    [property: JsonRequired] PaperPortfolioJudgmentStoredArtifact UpdatedPortfolio,
    [property: JsonRequired] IReadOnlyList<PaperPortfolioJudgmentPaperRoute> Routes,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperPortfolioJudgmentAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] PaperPortfolioJudgmentStoredArtifact Evidence,
    [property: JsonRequired] PaperPortfolioJudgmentStoredArtifact Decision,
    [property: JsonRequired] PaperPortfolioJudgmentStoredArtifact UpdatedPortfolio,
    [property: JsonRequired] IReadOnlyList<PaperPortfolioJudgmentPaperRoute> Routes,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);

public sealed record PaperPortfolioJudgmentAgentFailure(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string BlockerCode,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string NextRoute);
