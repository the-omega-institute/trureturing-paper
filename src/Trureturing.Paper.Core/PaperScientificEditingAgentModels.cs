using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperScientificEditingAgentSchemas
{
    public const string Dispatch =
        "paper-scientific-editing-agent-dispatch.v1";
    public const string Draft =
        "paper-scientific-edit-draft.v1";
    public const string Delta =
        "paper-scientific-edit-delta.v1";
    public const string TaskStaged =
        "paper-scientific-editing-agent-task-staged.v1";
    public const string EditedManuscript =
        "paper-scientifically-edited-manuscript.v1";
    public const string AdmissionCursor =
        "paper-scientific-editing-agent-cursor.v1";
    public const string ResultAdmitted =
        "paper-scientific-editing-agent-result-admitted.v1";
    public const string Ready =
        "paper-scientifically-edited-manuscript-ready.v1";
    public const string Failure =
        "paper-scientific-editing-agent-failure.v1";
}

public sealed record PaperScientificEditingAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string SourceAuthoringTaskRef,
    [property: JsonRequired] string SourceAuthoringResultRef,
    [property: JsonRequired] string SourceAuthoringCursorRef,
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string SourceManuscriptEnvelopeRef,
    [property: JsonRequired] string SourceDraftRef,
    [property: JsonRequired] string SourceMainTexRef,
    [property: JsonRequired] string SourceBibliographyRef,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string EligibilityRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string CompletionRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string SelectedReleaseRef,
    [property: JsonRequired] string SelectedReleaseDigest,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperScientificEditDraft(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string AbstractLatex,
    [property: JsonRequired] IReadOnlyList<string> Keywords,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptDraftSection> Sections,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptDraftReference> References,
    [property: JsonRequired] IReadOnlyList<string> EditDimensions,
    [property: JsonRequired] IReadOnlyList<string> RevisionSummary,
    [property: JsonRequired] IReadOnlyList<string> RemainingRisks,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperScientificEditDeltaContent(
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string SourceDraftRef,
    [property: JsonRequired] string EditedDraftRef,
    [property: JsonRequired] IReadOnlyList<string> ChangedSectionIds,
    [property: JsonRequired] int ChangedProseBlockCount,
    [property: JsonRequired] int ChangedProofBlockCount,
    [property: JsonRequired] bool AbstractChanged,
    [property: JsonRequired] bool KeywordsChanged,
    [property: JsonRequired] bool CitationSetChanged,
    [property: JsonRequired] IReadOnlyList<string> SubstantiveDimensions,
    [property: JsonRequired] bool ClaimIdentityPreserved,
    [property: JsonRequired] bool EvidenceBoundaryPreserved,
    [property: JsonRequired] bool Passed,
    [property: JsonRequired] string ComputedAt);

public sealed record PaperScientificEditDelta(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DeltaId,
    [property: JsonRequired] PaperScientificEditDeltaContent DeltaContent);

public sealed record PaperScientificallyEditedManuscriptContent(
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceAuthoringTaskRef,
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string EditDeltaRef,
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
    [property: JsonRequired] string EditingStatus,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperScientificallyEditedManuscript(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptId,
    [property: JsonRequired]
    PaperScientificallyEditedManuscriptContent ManuscriptContent);

public sealed record PaperScientificEditingAgentTaskStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath,
    [property: JsonRequired] string SourceAuthoringTaskRef,
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] bool Replayed);

public sealed record PaperScientificEditingAgentAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceAuthoringTaskRef,
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired]
    PaperManuscriptAuthoringStoredArtifact EditDelta,
    [property: JsonRequired]
    PaperManuscriptAuthoringStoredArtifact EditedManuscript,
    [property: JsonRequired] PaperManuscriptSourceFile MainTex,
    [property: JsonRequired] PaperManuscriptSourceFile Bibliography,
    [property: JsonRequired] int ChangedProseBlockCount,
    [property: JsonRequired] int ChangedProofBlockCount,
    [property: JsonRequired] IReadOnlyList<string> ChangedSectionIds,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperScientificEditingAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceAuthoringTaskRef,
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired]
    PaperManuscriptAuthoringStoredArtifact EditDelta,
    [property: JsonRequired]
    PaperManuscriptAuthoringStoredArtifact EditedManuscript,
    [property: JsonRequired] PaperManuscriptSourceFile MainTex,
    [property: JsonRequired] PaperManuscriptSourceFile Bibliography,
    [property: JsonRequired] int ChangedProseBlockCount,
    [property: JsonRequired] int ChangedProofBlockCount,
    [property: JsonRequired] IReadOnlyList<string> ChangedSectionIds,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);

public sealed record PaperScientificEditingAgentFailure(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string SourceAuthoringTaskRef,
    [property: JsonRequired] string SourceManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string BlockerCode,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string NextRoute);
