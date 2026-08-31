using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperTheoryAuditAgentSchemas
{
    public const string Dispatch = "paper-theory-audit-agent-dispatch.v1";
    public const string ReviewPlan = "paper-theory-audit-review-plan.v1";
    public const string PlanCursor = "paper-theory-audit-plan-cursor.v1";
    public const string OpinionDraft = "paper-theory-audit-opinion-draft.v1";
    public const string Opinion = "paper-theory-audit-opinion.v1";
    public const string TasksStaged = "paper-theory-audit-agent-tasks-staged.v1";
    public const string OpinionCursor = "paper-theory-audit-opinion-cursor.v1";
    public const string AggregateCursor = "paper-theory-audit-aggregate-cursor.v1";
    public const string ResultAdmitted = "paper-theory-audit-agent-result-admitted.v1";
    public const string Ready = "paper-theory-audit-ready.v1";
    public const string Failure = "paper-theory-audit-agent-failure.v1";
}

public sealed record PaperTheoryAuditReviewerSpec(
    [property: JsonRequired] int Slot,
    [property: JsonRequired] string ReviewerRole,
    [property: JsonRequired] string Focus,
    [property: JsonRequired] int Attempt);

public sealed record PaperTheoryAuditAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] IReadOnlyList<PaperTheoryAuditReviewerSpec> Reviewers,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperTheoryAuditOpinionDraft(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string ReviewerRole,
    [property: JsonRequired] PaperTheoryAuditMetrics Metrics,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] IReadOnlyList<string> Blockers,
    [property: JsonRequired] IReadOnlyList<string> RequiredRevisions,
    [property: JsonRequired] string NoveltyEvidence,
    [property: JsonRequired] IReadOnlyList<string> ProofAudit,
    [property: JsonRequired] IReadOnlyList<string> OverlapFindings,
    [property: JsonRequired] string ReviewedAt);

public sealed record PaperTheoryAuditOpinionArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string OpinionId,
    [property: JsonRequired] PaperTheoryAuditOpinion OpinionContent);

public sealed record PaperTheoryAuditPlannedReviewer(
    [property: JsonRequired] int Slot,
    [property: JsonRequired] string ReviewerRole,
    [property: JsonRequired] string Focus,
    [property: JsonRequired] int Attempt,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath);

public sealed record PaperTheoryAuditReviewPlanContent(
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuthorRunRef,
    [property: JsonRequired] IReadOnlyList<string> ContextInputRefs,
    [property: JsonRequired] IReadOnlyList<PaperTheoryAuditPlannedReviewer> Reviewers,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryAuditReviewPlan(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PlanId,
    [property: JsonRequired] PaperTheoryAuditReviewPlanContent PlanContent);

public sealed record PaperTheoryAuditPlanCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] string PlanRef,
    [property: JsonRequired] string PlanContentPath,
    [property: JsonRequired] string PlanEnvelopeRef,
    [property: JsonRequired] string PlanEnvelopePath,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryAuditStoredArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string ContentPath,
    [property: JsonRequired] string EnvelopeRef,
    [property: JsonRequired] string EnvelopePath);

public sealed record PaperTheoryAuditAgentTasksStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] PaperTheoryAuditStoredArtifact ReviewPlan,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] IReadOnlyList<PaperTheoryAuditPlannedReviewer> Reviewers,
    [property: JsonRequired] bool Replayed);

public sealed record PaperTheoryAuditOpinionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] int ReviewerSlot,
    [property: JsonRequired] string ReviewerRole,
    [property: JsonRequired] string ReviewerRunRef,
    [property: JsonRequired] string ReviewSessionRef,
    [property: JsonRequired] PaperTheoryAuditStoredArtifact Opinion,
    [property: JsonRequired] string AgentRunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperTheoryAuditAggregateCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PlanRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] IReadOnlyList<string> OpinionRefs,
    [property: JsonRequired] IReadOnlyList<string> ReviewerRunRefs,
    [property: JsonRequired] IReadOnlyList<string> ReviewSessionRefs,
    [property: JsonRequired] PaperTheoryAuditStoredArtifact Audit,
    [property: JsonRequired] PaperTheoryAuditStoredArtifact Scorecard,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] bool Passed,
    [property: JsonRequired] bool PromotionEligible,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string AggregatedAt);

public sealed record PaperTheoryAuditAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] int ReviewerSlot,
    [property: JsonRequired] string ReviewerRole,
    [property: JsonRequired] PaperTheoryAuditStoredArtifact Opinion,
    [property: JsonRequired] string AggregateStatus,
    [property: JsonRequired] IReadOnlyList<string> MissingTaskRefs,
    [property: JsonRequired] PaperTheoryAuditStoredArtifact? Audit,
    [property: JsonRequired] PaperTheoryAuditStoredArtifact? Scorecard,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] bool Passed,
    [property: JsonRequired] bool PromotionEligible,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);
