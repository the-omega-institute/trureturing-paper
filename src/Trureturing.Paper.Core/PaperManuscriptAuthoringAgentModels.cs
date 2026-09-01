using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperManuscriptAuthoringAgentSchemas
{
    public const string Dispatch =
        "paper-manuscript-authoring-agent-dispatch.v1";
    public const string Draft =
        "paper-scientific-manuscript-draft.v1";
    public const string TaskStaged =
        "paper-manuscript-authoring-agent-task-staged.v1";
    public const string ScientificManuscript =
        "paper-scientific-manuscript.v1";
    public const string AdmissionCursor =
        "paper-manuscript-authoring-agent-cursor.v1";
    public const string ResultAdmitted =
        "paper-manuscript-authoring-agent-result-admitted.v1";
    public const string Ready =
        "paper-scientific-manuscript-ready.v1";
    public const string Failure =
        "paper-manuscript-authoring-agent-failure.v1";
}

public static class PaperManuscriptDraftBlockKinds
{
    public const string Prose = "prose";
    public const string FormalClaim = "formal-claim";
    public const string Proof = "proof";
    public const string InformalItem = "informal-item";
}

public sealed record PaperManuscriptAuthoringAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string EligibilityRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string SelectedReleaseRef,
    [property: JsonRequired] string SelectedReleaseDigest,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperManuscriptDraftBlock(
    [property: JsonRequired] int Order,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string TargetId,
    [property: JsonRequired] string Latex);

public sealed record PaperManuscriptDraftSection(
    [property: JsonRequired] int Order,
    [property: JsonRequired] string SectionId,
    [property: JsonRequired] string Title,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptDraftBlock> Blocks);

public sealed record PaperManuscriptDraftReference(
    [property: JsonRequired] string CitationKey,
    [property: JsonRequired] int RelatedWorkIndex,
    [property: JsonRequired] string SourceRef,
    [property: JsonRequired] string Usage);

public sealed record PaperScientificManuscriptDraft(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string AbstractLatex,
    [property: JsonRequired] IReadOnlyList<string> Keywords,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptDraftSection> Sections,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptDraftReference> References,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperManuscriptSourceFile(
    [property: JsonRequired] string Role,
    [property: JsonRequired] string MediaType,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string RepositoryRelativePath,
    [property: JsonRequired] long SizeBytes);

public sealed record PaperManuscriptClaimBinding(
    [property: JsonRequired] int Order,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string LatexLabel,
    [property: JsonRequired] string ClaimKind,
    [property: JsonRequired] string Environment,
    [property: JsonRequired] string CertifiedClaimRef,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string StatementId,
    [property: JsonRequired] string RequestedStatementDigest,
    [property: JsonRequired] string BeginMarker,
    [property: JsonRequired] string EndMarker);

public sealed record PaperScientificManuscriptContent(
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string EligibilityRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string SelectedReleaseRef,
    [property: JsonRequired] string SelectedReleaseDigest,
    [property: JsonRequired] string Title,
    [property: JsonRequired] PaperManuscriptSourceFile MainTex,
    [property: JsonRequired] PaperManuscriptSourceFile Bibliography,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptClaimBinding> ClaimBindings,
    [property: JsonRequired] IReadOnlyList<string> SectionIds,
    [property: JsonRequired] IReadOnlyList<string> CitationKeys,
    [property: JsonRequired] int FormalClaimCount,
    [property: JsonRequired] int InformalItemCount,
    [property: JsonRequired] string AuthoringStatus,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperScientificManuscript(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptId,
    [property: JsonRequired] PaperScientificManuscriptContent ManuscriptContent);

public sealed record PaperManuscriptAuthoringStoredArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string ContentPath,
    [property: JsonRequired] string EnvelopeRef,
    [property: JsonRequired] string EnvelopePath);

public sealed record PaperManuscriptAuthoringAgentTaskStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string EligibilityRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] bool Replayed);

public sealed record PaperManuscriptAuthoringAgentAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string EligibilityRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] PaperManuscriptAuthoringStoredArtifact Manuscript,
    [property: JsonRequired] PaperManuscriptSourceFile MainTex,
    [property: JsonRequired] PaperManuscriptSourceFile Bibliography,
    [property: JsonRequired] int FormalClaimCount,
    [property: JsonRequired] int InformalItemCount,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperManuscriptAuthoringAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string EligibilityRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] PaperManuscriptAuthoringStoredArtifact Manuscript,
    [property: JsonRequired] PaperManuscriptSourceFile MainTex,
    [property: JsonRequired] PaperManuscriptSourceFile Bibliography,
    [property: JsonRequired] int FormalClaimCount,
    [property: JsonRequired] int InformalItemCount,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);

public sealed record PaperManuscriptAuthoringAgentFailure(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string BlockerCode,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string NextRoute);
