using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperTheoryDeepeningAgentSchemas
{
    public const string Dispatch = "paper-theory-deepening-agent-dispatch.v1";
    public const string Draft = "paper-theory-deepening-draft.v1";
    public const string TaskStaged = "paper-theory-deepening-agent-task-staged.v1";
    public const string Delta = "paper-theory-deepening-delta.v1";
    public const string AdmissionCursor = "paper-theory-deepening-agent-cursor.v1";
    public const string ResultAdmitted = "paper-theory-deepening-agent-result-admitted.v1";
    public const string Ready = "paper-theory-deepening-ready.v1";
    public const string Failure = "paper-theory-deepening-agent-failure.v1";
}

public sealed record PaperTheoryDeepeningAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperTheoryIterationDraft(
    [property: JsonRequired] IReadOnlyList<string> ChangedClaimIds,
    [property: JsonRequired] IReadOnlyList<string> NewClaimIds,
    [property: JsonRequired] IReadOnlyList<string> StrengthenedClaimIds,
    [property: JsonRequired] IReadOnlyList<string> RetiredClaimIds,
    [property: JsonRequired] IReadOnlyList<string> ProofSpine,
    [property: JsonRequired] string NovelIncrement,
    [property: JsonRequired] string PriorWorkBoundary,
    [property: JsonRequired] IReadOnlyList<string> CounterexampleFindings,
    [property: JsonRequired] IReadOnlyList<string> SplitCandidateClaimIds,
    [property: JsonRequired] IReadOnlyList<string> MergeCandidatePaperIds,
    [property: JsonRequired] PaperTheoryProgressEvidence ProgressEvidence,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoremPackageDraft(
    [property: JsonRequired] string Maturity,
    [property: JsonRequired] IReadOnlyList<PaperTheoremPackageClaim> Claims,
    [property: JsonRequired] IReadOnlyList<string> MainTheoremClaimIds,
    [property: JsonRequired] IReadOnlyList<string> CorollaryClaimIds,
    [property: JsonRequired] IReadOnlyList<string> SharpnessClaimIds,
    [property: JsonRequired] IReadOnlyList<string> OpenProofObligations,
    [property: JsonRequired] IReadOnlyList<string> KnownResultsToCite,
    [property: JsonRequired] string NoveltySummary,
    [property: JsonRequired] string PublicationSignificance,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperCandidateSplitProposalDraft(
    [property: JsonRequired] string ProposedPaperId,
    [property: JsonRequired] IReadOnlyList<string> ExtractedClaimIds,
    [property: JsonRequired] string IndependentResearchQuestion,
    [property: JsonRequired] IReadOnlyList<string> IndependentProofSpine,
    [property: JsonRequired] string ScopeMismatch,
    [property: JsonRequired] string PublicationRationale,
    [property: JsonRequired] string OverlapRisk,
    [property: JsonRequired] string ProposedAt);

public sealed record PaperResearchLedgerEntryDraft(
    [property: JsonRequired] string DiscoveryKind,
    [property: JsonRequired] IReadOnlyList<string> RelatedRefs,
    [property: JsonRequired] string Summary,
    [property: JsonRequired] string WhyRecorded,
    [property: JsonRequired] string PromotionStatus,
    [property: JsonRequired] string RecordedAt);

public sealed record PaperTheoryDeepeningDraft(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string DeepeningRequestRef,
    [property: JsonRequired] IReadOnlyList<string> PriorTheoremPackageRefs,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] int Round,
    [property: JsonRequired] PaperTheoryIterationDraft Iteration,
    [property: JsonRequired] PaperTheoremPackageDraft TheoremPackage,
    [property: JsonRequired] IReadOnlyList<PaperCandidateSplitProposalDraft> SplitProposals,
    [property: JsonRequired] IReadOnlyList<PaperResearchLedgerEntryDraft> ResearchLedgerEntries,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryDeepeningDeltaContent(
    [property: JsonRequired] string DeepeningRequestRef,
    [property: JsonRequired] string BaselineSchema,
    [property: JsonRequired] string BaselineRef,
    [property: JsonRequired] string IterationRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] IReadOnlyList<string> NewClaimIds,
    [property: JsonRequired] IReadOnlyList<string> StrengthenedClaimIds,
    [property: JsonRequired] IReadOnlyList<string> RetiredClaimIds,
    [property: JsonRequired] int DependencyEdgesAdded,
    [property: JsonRequired] int ProofObligationsClosed,
    [property: JsonRequired] int CounterexamplesResolved,
    [property: JsonRequired] bool AbstractionChanged,
    [property: JsonRequired] bool NoveltyBoundaryChanged,
    [property: JsonRequired] IReadOnlyList<string> SubstantiveDimensions,
    [property: JsonRequired] bool Passed,
    [property: JsonRequired] string ComputedAt);

public sealed record PaperTheoryDeepeningDelta(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DeltaId,
    [property: JsonRequired] PaperTheoryDeepeningDeltaContent DeltaContent);

public sealed record PaperTheoryDeepeningStoredArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ArtifactRef,
    [property: JsonRequired] string ContentPath,
    [property: JsonRequired] string EnvelopeRef,
    [property: JsonRequired] string EnvelopePath);

public sealed record PaperTheoryDeepeningAgentTaskStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] int Round,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] bool Replayed);

public sealed record PaperTheoryDeepeningAgentAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] int Round,
    [property: JsonRequired] PaperTheoryDeepeningStoredArtifact Iteration,
    [property: JsonRequired] PaperTheoryDeepeningStoredArtifact TheoremPackage,
    [property: JsonRequired] PaperTheoryDeepeningStoredArtifact Delta,
    [property: JsonRequired] IReadOnlyList<PaperTheoryDeepeningStoredArtifact> SplitProposals,
    [property: JsonRequired] IReadOnlyList<PaperTheoryDeepeningStoredArtifact> ResearchLedgerEntries,
    [property: JsonRequired] IReadOnlyList<string> MergeCandidatePaperIds,
    [property: JsonRequired] string Maturity,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperTheoryDeepeningAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] int Round,
    [property: JsonRequired] PaperTheoryDeepeningStoredArtifact Iteration,
    [property: JsonRequired] PaperTheoryDeepeningStoredArtifact TheoremPackage,
    [property: JsonRequired] PaperTheoryDeepeningStoredArtifact Delta,
    [property: JsonRequired] IReadOnlyList<PaperTheoryDeepeningStoredArtifact> SplitProposals,
    [property: JsonRequired] IReadOnlyList<PaperTheoryDeepeningStoredArtifact> ResearchLedgerEntries,
    [property: JsonRequired] IReadOnlyList<string> MergeCandidatePaperIds,
    [property: JsonRequired] string Maturity,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);
