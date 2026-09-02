using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperJournalResearchAgentSchemas
{
    public const string Dispatch = "paper-journal-research-agent-dispatch.v1";
    public const string Draft = "paper-journal-research-draft.v1";
    public const string Dossier = "paper-journal-research-dossier.v1";
    public const string VenueScorecard = "paper-journal-venue-scorecard.v1";
    public const string TargetSelection = "paper-journal-target-selection.v1";
    public const string TaskStaged = "paper-journal-research-agent-task-staged.v1";
    public const string AdmissionCursor = "paper-journal-research-agent-cursor.v1";
    public const string ResultAdmitted = "paper-journal-research-agent-result-admitted.v1";
    public const string Ready = "paper-journal-target-ready.v1";
    public const string Failure = "paper-journal-research-agent-failure.v1";
}

public sealed record PaperJournalResearchPolicy(
    [property: JsonRequired] int MinimumCandidateCount,
    [property: JsonRequired] int MaximumCandidateCount,
    [property: JsonRequired] int MinimumEligibleCandidateCount,
    [property: JsonRequired] int MaximumPublicationTier,
    [property: JsonRequired] int MaximumSourceAgeDays,
    [property: JsonRequired] string DesiredArticleType);

public sealed record PaperJournalResearchAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string SourceScientificEditingTaskRef,
    [property: JsonRequired] string SourceScientificEditingResultRef,
    [property: JsonRequired] string SourceScientificEditingCursorRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string SourceEditedManuscriptEnvelopeRef,
    [property: JsonRequired] string SourceEditDraftRef,
    [property: JsonRequired] string SourceEditDeltaRef,
    [property: JsonRequired] string SourceMainTexRef,
    [property: JsonRequired] string SourceBibliographyRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string FrontierRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string SelectedReleaseRef,
    [property: JsonRequired] string SelectedReleaseDigest,
    [property: JsonRequired] PaperJournalResearchPolicy Policy,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperJournalSourceAssertion(
    [property: JsonRequired] string Fact,
    [property: JsonRequired] string Value,
    [property: JsonRequired] string EvidenceText);

public sealed record PaperJournalSourceSnapshotDraft(
    [property: JsonRequired] string SourceId,
    [property: JsonRequired] string VenueId,
    [property: JsonRequired] IReadOnlyList<string> Roles,
    [property: JsonRequired] string Authority,
    [property: JsonRequired] string Url,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string RetrievedAt,
    [property: JsonRequired] string UpdatedAt,
    [property: JsonRequired] string NormalizedText,
    [property: JsonRequired] string ContentSha256,
    [property: JsonRequired] IReadOnlyList<PaperJournalSourceAssertion> Assertions);

public sealed record PaperJournalVenueCandidateDraft(
    [property: JsonRequired] string VenueId,
    [property: JsonRequired] string JournalName,
    [property: JsonRequired] string Publisher,
    [property: JsonRequired] string Issn,
    [property: JsonRequired] string CanonicalUrl,
    [property: JsonRequired] string TargetArticleType,
    [property: JsonRequired] int ClaimedPublicationTier,
    [property: JsonRequired] string ScopeFit,
    [property: JsonRequired] bool ArticleTypeSupported,
    [property: JsonRequired] string LatexPolicy,
    [property: JsonRequired] int MaximumAbstractWords,
    [property: JsonRequired] int MaximumMainTextWords,
    [property: JsonRequired] bool ProofAppendixAllowed,
    [property: JsonRequired] bool SupplementaryMaterialAllowed,
    [property: JsonRequired] string FeeStatus,
    [property: JsonRequired] long MandatoryFeeMinorUnits,
    [property: JsonRequired] string FeeCurrency,
    [property: JsonRequired] string DataPolicy,
    [property: JsonRequired] string CodePolicy,
    [property: JsonRequired] string PreprintPolicy,
    [property: JsonRequired] string AiPolicy,
    [property: JsonRequired] string PeerReviewModel,
    [property: JsonRequired] string AccessModel,
    [property: JsonRequired] IReadOnlyList<string> SourceIds,
    [property: JsonRequired] IReadOnlyList<string> ComparablePaperSourceIds,
    [property: JsonRequired] IReadOnlyList<string> Rationale,
    [property: JsonRequired] IReadOnlyList<string> Risks);

public sealed record PaperJournalResearchDraft(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string DesiredArticleType,
    [property: JsonRequired] IReadOnlyList<PaperJournalVenueCandidateDraft> Venues,
    [property: JsonRequired] IReadOnlyList<PaperJournalSourceSnapshotDraft> Sources,
    [property: JsonRequired] IReadOnlyList<string> PortfolioRationale,
    [property: JsonRequired] IReadOnlyList<string> RemainingRisks,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperJournalSourceSnapshot(
    [property: JsonRequired] string SourceId,
    [property: JsonRequired] string VenueId,
    [property: JsonRequired] IReadOnlyList<string> Roles,
    [property: JsonRequired] string Authority,
    [property: JsonRequired] string Url,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string RetrievedAt,
    [property: JsonRequired] string UpdatedAt,
    [property: JsonRequired] string NormalizedText,
    [property: JsonRequired] string ContentSha256,
    [property: JsonRequired] IReadOnlyList<PaperJournalSourceAssertion> Assertions);

public sealed record PaperJournalVenueEvidence(
    [property: JsonRequired] string VenueId,
    [property: JsonRequired] string JournalName,
    [property: JsonRequired] string Publisher,
    [property: JsonRequired] string Issn,
    [property: JsonRequired] string CanonicalUrl,
    [property: JsonRequired] string TargetArticleType,
    [property: JsonRequired] int ClaimedPublicationTier,
    [property: JsonRequired] string ScopeFit,
    [property: JsonRequired] bool ArticleTypeSupported,
    [property: JsonRequired] string LatexPolicy,
    [property: JsonRequired] int MaximumAbstractWords,
    [property: JsonRequired] int MaximumMainTextWords,
    [property: JsonRequired] bool ProofAppendixAllowed,
    [property: JsonRequired] bool SupplementaryMaterialAllowed,
    [property: JsonRequired] string FeeStatus,
    [property: JsonRequired] long MandatoryFeeMinorUnits,
    [property: JsonRequired] string FeeCurrency,
    [property: JsonRequired] string DataPolicy,
    [property: JsonRequired] string CodePolicy,
    [property: JsonRequired] string PreprintPolicy,
    [property: JsonRequired] string AiPolicy,
    [property: JsonRequired] string PeerReviewModel,
    [property: JsonRequired] string AccessModel,
    [property: JsonRequired] IReadOnlyList<string> SourceIds,
    [property: JsonRequired] IReadOnlyList<string> ComparablePaperSourceIds,
    [property: JsonRequired] IReadOnlyList<string> Rationale,
    [property: JsonRequired] IReadOnlyList<string> Risks);

public sealed record PaperJournalResearchDossierContent(
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceScientificEditingTaskRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string SelectedReleaseRef,
    [property: JsonRequired] string SelectedReleaseDigest,
    [property: JsonRequired] PaperJournalResearchPolicy Policy,
    [property: JsonRequired] IReadOnlyList<PaperJournalVenueEvidence> Venues,
    [property: JsonRequired] IReadOnlyList<PaperJournalSourceSnapshot> Sources,
    [property: JsonRequired] int ManuscriptWordCount,
    [property: JsonRequired] string EvidenceCutoff,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperJournalResearchDossier(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DossierId,
    [property: JsonRequired] PaperJournalResearchDossierContent DossierContent);

public sealed record PaperJournalVenueScorecardContent(
    [property: JsonRequired] string DossierRef,
    [property: JsonRequired] string VenueId,
    [property: JsonRequired] string JournalName,
    [property: JsonRequired] string TargetArticleType,
    [property: JsonRequired] int PublicationTier,
    [property: JsonRequired] int ScopeFitScore,
    [property: JsonRequired] int TheoremPackageFitScore,
    [property: JsonRequired] int ArticleTypeFitScore,
    [property: JsonRequired] int ComparablePaperScore,
    [property: JsonRequired] int FormatFeasibilityScore,
    [property: JsonRequired] int LengthFeasibilityScore,
    [property: JsonRequired] int PolicyCompatibilityScore,
    [property: JsonRequired] int FeeFeasibilityScore,
    [property: JsonRequired] int EvidenceCompletenessScore,
    [property: JsonRequired] int EvidenceRecencyScore,
    [property: JsonRequired] int OverallScore,
    [property: JsonRequired] bool Eligible,
    [property: JsonRequired] IReadOnlyList<string> Blockers,
    [property: JsonRequired] string ComputedAt);

public sealed record PaperJournalVenueScorecard(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ScorecardId,
    [property: JsonRequired] PaperJournalVenueScorecardContent ScorecardContent);

public sealed record PaperJournalTargetSelectionContent(
    [property: JsonRequired] string DossierRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] int MaximumPublicationTier,
    [property: JsonRequired] string SelectedVenueId,
    [property: JsonRequired] string SelectedJournalName,
    [property: JsonRequired] string SelectedArticleType,
    [property: JsonRequired] int SelectedPublicationTier,
    [property: JsonRequired] string SelectedScorecardRef,
    [property: JsonRequired] IReadOnlyList<string> RankedScorecardRefs,
    [property: JsonRequired] IReadOnlyList<string> SelectedSourceIds,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string SelectedAt);

public sealed record PaperJournalTargetSelection(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string SelectionId,
    [property: JsonRequired] PaperJournalTargetSelectionContent SelectionContent);

public sealed record PaperJournalResearchAgentTaskStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath,
    [property: JsonRequired] string SourceScientificEditingTaskRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] bool Replayed);

public sealed record PaperJournalResearchAgentAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceScientificEditingTaskRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] PaperManuscriptAuthoringStoredArtifact Dossier,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptAuthoringStoredArtifact> Scorecards,
    [property: JsonRequired] PaperManuscriptAuthoringStoredArtifact TargetSelection,
    [property: JsonRequired] string SelectedVenueId,
    [property: JsonRequired] string SelectedJournalName,
    [property: JsonRequired] int SelectedPublicationTier,
    [property: JsonRequired] string SelectedArticleType,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperJournalResearchAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string SourceScientificEditingTaskRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] PaperManuscriptAuthoringStoredArtifact Dossier,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptAuthoringStoredArtifact> Scorecards,
    [property: JsonRequired] PaperManuscriptAuthoringStoredArtifact TargetSelection,
    [property: JsonRequired] string SelectedVenueId,
    [property: JsonRequired] string SelectedJournalName,
    [property: JsonRequired] int SelectedPublicationTier,
    [property: JsonRequired] string SelectedArticleType,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);

public sealed record PaperJournalResearchAgentFailure(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string SourceScientificEditingTaskRef,
    [property: JsonRequired] string SourceEditedManuscriptRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string BlockerCode,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string NextRoute);
