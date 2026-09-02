using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static partial class PaperManuscriptAuthoringAgentService
{
    private const int JournalMinimumSourceTextLength = 80;
    private const int JournalMaximumSourceTextLength = 131072;

    private static readonly HashSet<string> JournalSourceRoles = new(
        [
            "official-scope",
            "official-author-guidelines",
            "official-article-types",
            "official-formatting",
            "official-length",
            "official-fees",
            "official-policies",
            "independent-tier",
            "recent-comparable"
        ],
        StringComparer.Ordinal);

    private static readonly string[] RequiredJournalSourceRoles =
    [
        "official-scope",
        "official-author-guidelines",
        "official-article-types",
        "official-formatting",
        "official-length",
        "official-fees",
        "official-policies",
        "independent-tier",
        "recent-comparable"
    ];

    private static readonly HashSet<string> JournalSourceAuthorities = new(
        ["official", "independent-index", "journal-article"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalAssertionFacts = new(
        [
            "journal-name",
            "publisher",
            "issn",
            "publication-tier",
            "scope-fit",
            "target-article-type",
            "article-type-supported",
            "latex-policy",
            "maximum-abstract-words",
            "maximum-main-text-words",
            "proof-appendix-allowed",
            "supplementary-material-allowed",
            "fee-status",
            "mandatory-fee-minor-units",
            "fee-currency",
            "data-policy",
            "code-policy",
            "preprint-policy",
            "ai-policy",
            "peer-review-model",
            "access-model",
            "comparable-title",
            "comparable-publication-date",
            "comparable-article-type",
            "comparable-doi"
        ],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalScopeFits = new(
        ["exact", "strong", "partial", "none"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalLatexPolicies = new(
        ["latex-required", "latex-accepted", "source-upload-accepted", "word-only", "unknown"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalFeeStatuses = new(
        ["none", "optional", "mandatory-known", "unknown"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalDataCodePolicies = new(
        ["required", "optional", "not-applicable", "unknown"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalPreprintPolicies = new(
        ["allowed", "restricted", "prohibited", "unknown"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalAiPolicies = new(
        ["allowed", "disclosure-required", "prohibited", "unknown"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalPeerReviewModels = new(
        ["single-anonymized", "double-anonymized", "open", "editorial", "unknown"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> JournalAccessModels = new(
        ["subscription", "hybrid", "gold-open-access", "diamond-open-access", "unknown"],
        StringComparer.Ordinal);

    public static PaperJournalResearchAgentTaskStaged StageJournalResearchTask(
        string repositoryRoot,
        string sourceScientificEditingTaskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(
            sourceScientificEditingTaskRef,
            nameof(sourceScientificEditingTaskRef));
        PaperJournalResearchContext context = LoadJournalResearchContext(
            root,
            sourceScientificEditingTaskRef);
        var policy = new PaperJournalResearchPolicy(
            MinimumCandidateCount: 2,
            MaximumCandidateCount: 8,
            MinimumEligibleCandidateCount: 2,
            MaximumPublicationTier: 2,
            MaximumSourceAgeDays: 30,
            DesiredArticleType: "research-article");
        var dispatch = new PaperJournalResearchAgentDispatch(
            PaperJournalResearchAgentSchemas.Dispatch,
            sourceScientificEditingTaskRef,
            context.SourceAgentCursor.ResultRef,
            context.SourceScientificCursorRef,
            context.SourceEditedManuscript.ManuscriptId,
            context.SourceScientificCursor.EditedManuscript.EnvelopeRef,
            context.SourceAgentCursor.Outputs[0].ArtifactRef,
            context.SourceEditDelta.DeltaId,
            context.SourceScientificCursor.MainTex.ArtifactRef,
            context.SourceScientificCursor.Bibliography.ArtifactRef,
            context.SourceScientificCursor.ClaimManifestRef,
            context.SourceScientificCursor.ManuscriptPlanRef,
            context.SourceEditedManuscript.ManuscriptContent.FrontierRef,
            context.SourceScientificCursor.PaperId,
            context.SourceScientificCursor.TheoryProgramRef,
            context.SourceEditedManuscript.ManuscriptContent.TheoremPackageRef,
            context.SourceEditedManuscript.ManuscriptContent.TheoryAuditRef,
            context.SourceEditedManuscript.ManuscriptContent.LiteratureResearchRef,
            context.SourceEditedManuscript.ManuscriptContent.SelectedReleaseRef,
            context.SourceEditedManuscript.ManuscriptContent.SelectedReleaseDigest,
            policy,
            context.ExactInputs,
            context.SourceScientificCursor.AdmittedAt);
        Validate(dispatch);

        byte[] dispatchBytes = CanonicalJson.Serialize(dispatch);
        string dispatchRef = Reference(dispatchBytes);
        string dispatchPath = DomainArtifactPath(
            root,
            "journal-research-dispatches",
            "raw",
            dispatchRef,
            ".json");
        _ = PutImmutable(dispatchPath, dispatchBytes);
        string dispatchRelativePath = RelativePath(root, dispatchPath);
        PaperAgentTask task = BuildJournalResearchTask(
            dispatch,
            dispatchRef,
            dispatchRelativePath,
            context);
        PaperAgentRuntimeService.Validate(task);

        byte[] taskBytes = CanonicalJson.Serialize(task);
        string taskRef = Reference(taskBytes);
        string taskPath = Path.Combine(
            root,
            "inbox",
            "agent-tasks",
            $"journal-research-{dispatch.PaperId}-{Hex(taskRef)}.json");
        bool replayed = PutImmutable(taskPath, taskBytes);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile(task.Phase);
        return new PaperJournalResearchAgentTaskStaged(
            PaperJournalResearchAgentSchemas.TaskStaged,
            dispatchRef,
            taskRef,
            taskPath,
            sourceScientificEditingTaskRef,
            dispatch.SourceEditedManuscriptRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            replayed);
    }

    public static PaperJournalResearchAgentResultAdmitted AdmitJournalResearchResult(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("journal-research");
        if (!string.Equals(task.Phase, profile.Phase, StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, profile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, profile.ContextMode, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an FKST-native journal-research task can enter this admission bridge.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperJournalResearchAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Journal-research task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = Reference(dispatchBytes);
        PaperJournalResearchAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<
                PaperJournalResearchAgentDispatch>(dispatchBytes);
        Validate(dispatch);
        PaperJournalResearchContext context = LoadJournalResearchContext(
            root,
            dispatch.SourceScientificEditingTaskRef);
        ValidateJournalResearchTaskBinding(
            root,
            task,
            dispatch,
            dispatchRef,
            dispatchInput.RepositoryRelativePath,
            context);

        PaperAgentTaskCursor agentCursor = ReadAgentCursor(root, task, taskRef);
        PaperAgentResultWire result = ReadAgentResult(
            root,
            task,
            taskRef,
            agentCursor.ResultRef);
        RequireCursorMatchesResult(agentCursor, result);
        if (!string.Equals(result.Status, "completed", StringComparison.Ordinal)
            || !string.Equals(
                result.NextRoute,
                "journal-style-editing",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only completed journal research routed to journal-style editing can be admitted.");
        }

        string cursorPath = JournalResearchCursorPath(root, taskRef);
        if (File.Exists(cursorPath))
        {
            return ReplayJournalResearchAdmission(
                root,
                ReadJournalResearchCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef,
                context);
        }
        if (agentCursor.Outputs.Count != 1
            || !string.Equals(
                agentCursor.Outputs[0].Schema,
                PaperJournalResearchAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Completed journal research must return exactly one source-backed research draft.");
        }

        PaperAgentStoredOutput output = agentCursor.Outputs[0];
        byte[] draftBytes = ReadAgentOutput(root, output.ArtifactRef);
        PaperJournalResearchDraft draft =
            PaperResearchInputJson.DeserializeStrict<PaperJournalResearchDraft>(
                draftBytes);
        PaperJournalResearchDossier dossier = BuildJournalResearchDossier(
            draft,
            dispatch,
            dispatchRef,
            context,
            taskRef,
            agentCursor.ResultRef,
            result.CompletedAt);
        PaperJournalVenueScorecard[] scorecards = ComputeVenueScorecards(
            dossier,
            context,
            result.CompletedAt);
        PaperJournalVenueScorecard[] eligible = scorecards
            .Where(scorecard => scorecard.ScorecardContent.Eligible)
            .ToArray();
        if (eligible.Length < dispatch.Policy.MinimumEligibleCandidateCount)
        {
            throw new InvalidDataException(
                $"Journal research produced only {eligible.Length} eligible Tier {dispatch.Policy.MaximumPublicationTier} or stronger venues; {dispatch.Policy.MinimumEligibleCandidateCount} are required.");
        }
        PaperJournalTargetSelection selection = SelectJournalTarget(
            dossier,
            scorecards,
            result.CompletedAt);
        ValidateJournalResearchArtifacts(
            dossier,
            scorecards,
            selection,
            dispatch,
            context);

        PaperManuscriptAuthoringStoredArtifact storedDossier = StoreDomain(
            root,
            "journal-research-dossiers",
            PaperJournalResearchAgentSchemas.Dossier,
            dossier.DossierId,
            dossier.DossierContent,
            dossier);
        PaperManuscriptAuthoringStoredArtifact[] storedScorecards = scorecards
            .Select(scorecard => StoreDomain(
                root,
                "journal-venue-scorecards",
                PaperJournalResearchAgentSchemas.VenueScorecard,
                scorecard.ScorecardId,
                scorecard.ScorecardContent,
                scorecard))
            .OrderBy(stored => stored.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        PaperManuscriptAuthoringStoredArtifact storedSelection = StoreDomain(
            root,
            "journal-target-selections",
            PaperJournalResearchAgentSchemas.TargetSelection,
            selection.SelectionId,
            selection.SelectionContent,
            selection);
        var cursor = new PaperJournalResearchAgentAdmissionCursor(
            PaperJournalResearchAgentSchemas.AdmissionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.SourceScientificEditingTaskRef,
            dispatch.SourceEditedManuscriptRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            storedDossier,
            storedScorecards,
            storedSelection,
            selection.SelectionContent.SelectedVenueId,
            selection.SelectionContent.SelectedJournalName,
            selection.SelectionContent.SelectedPublicationTier,
            selection.SelectionContent.SelectedArticleType,
            "journal-style-editing",
            agentCursor.RunId,
            agentCursor.Provenance,
            result.CompletedAt);
        Validate(cursor);
        try
        {
            PaperResearchInputStore.WriteAtomic(
                cursorPath,
                CanonicalJson.Serialize(cursor),
                overwrite: false);
        }
        catch (IOException) when (File.Exists(cursorPath))
        {
            return ReplayJournalResearchAdmission(
                root,
                ReadJournalResearchCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef,
                context);
        }
        return JournalResearchRecorded(cursor, replayed: false);
    }

    public static void Validate(PaperJournalResearchAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperJournalResearchAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        foreach (string digest in new[]
        {
            dispatch.SourceScientificEditingTaskRef,
            dispatch.SourceScientificEditingResultRef,
            dispatch.SourceScientificEditingCursorRef,
            dispatch.SourceEditedManuscriptRef,
            dispatch.SourceEditedManuscriptEnvelopeRef,
            dispatch.SourceEditDraftRef,
            dispatch.SourceEditDeltaRef,
            dispatch.SourceMainTexRef,
            dispatch.SourceBibliographyRef,
            dispatch.ClaimManifestRef,
            dispatch.ManuscriptPlanRef,
            dispatch.FrontierRef,
            dispatch.TheoryProgramRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.LiteratureResearchRef,
            dispatch.SelectedReleaseRef,
            dispatch.SelectedReleaseDigest
        })
        {
            RequireDigest(digest, nameof(dispatch));
        }
        RequirePaperId(dispatch.PaperId);
        ValidateJournalResearchPolicy(dispatch.Policy);
        if (dispatch.ExactInputs is null
            || dispatch.ExactInputs.Count < JournalResearchMinimumExactInputCount
            || dispatch.ExactInputs.Count > JournalResearchMaximumExactInputCount)
        {
            throw new InvalidDataException(
                $"Journal-research dispatch must contain between {JournalResearchMinimumExactInputCount} and {JournalResearchMaximumExactInputCount} inputs.");
        }
        RequireExactInputs(dispatch.ExactInputs);
        string[] requiredRefs =
        [
            dispatch.SourceScientificEditingTaskRef,
            dispatch.SourceScientificEditingResultRef,
            dispatch.SourceScientificEditingCursorRef,
            dispatch.SourceEditedManuscriptEnvelopeRef,
            dispatch.SourceEditDraftRef,
            dispatch.SourceMainTexRef,
            dispatch.SourceBibliographyRef,
            dispatch.ClaimManifestRef,
            dispatch.ManuscriptPlanRef,
            dispatch.TheoryProgramRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.LiteratureResearchRef,
            dispatch.SelectedReleaseRef
        ];
        foreach (string reference in requiredRefs)
        {
            if (!dispatch.ExactInputs.Any(value => string.Equals(
                    value.ArtifactRef,
                    reference,
                    StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Journal-research dispatch omitted a required exact evidence reference.");
            }
        }
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    public static void Validate(PaperJournalResearchDossier dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        RequireExact(
            dossier.Schema,
            PaperJournalResearchAgentSchemas.Dossier,
            nameof(dossier.Schema));
        PaperJournalResearchDossierContent content = dossier.DossierContent
            ?? throw new InvalidDataException(
                "Journal-research dossier content is required.");
        RequireIdentity(dossier.DossierId, content, nameof(dossier.DossierId));
        foreach (string digest in new[]
        {
            content.TaskRef,
            content.ResultRef,
            content.DispatchRef,
            content.SourceScientificEditingTaskRef,
            content.SourceEditedManuscriptRef,
            content.TheoryProgramRef,
            content.ClaimManifestRef,
            content.ManuscriptPlanRef,
            content.TheoremPackageRef,
            content.TheoryAuditRef,
            content.LiteratureResearchRef,
            content.SelectedReleaseRef,
            content.SelectedReleaseDigest
        })
        {
            RequireDigest(digest, nameof(content));
        }
        RequirePaperId(content.PaperId);
        ValidateJournalResearchPolicy(content.Policy);
        if (content.Venues is null
            || content.Venues.Count < content.Policy.MinimumCandidateCount
            || content.Venues.Count > content.Policy.MaximumCandidateCount
            || content.Sources is null
            || content.Sources.Count < content.Venues.Count)
        {
            throw new InvalidDataException(
                "Journal-research dossier venue or source count is outside policy.");
        }
        if (content.ManuscriptWordCount < 1)
        {
            throw new InvalidDataException(
                "Journal-research dossier requires a positive manuscript word count.");
        }
        ParseUtc(content.EvidenceCutoff, nameof(content.EvidenceCutoff));
        ParseUtc(content.CreatedAt, nameof(content.CreatedAt));
    }

    public static void Validate(PaperJournalVenueScorecard scorecard)
    {
        ArgumentNullException.ThrowIfNull(scorecard);
        RequireExact(
            scorecard.Schema,
            PaperJournalResearchAgentSchemas.VenueScorecard,
            nameof(scorecard.Schema));
        PaperJournalVenueScorecardContent content = scorecard.ScorecardContent
            ?? throw new InvalidDataException("Journal venue scorecard content is required.");
        RequireIdentity(scorecard.ScorecardId, content, nameof(scorecard.ScorecardId));
        RequireDigest(content.DossierRef, nameof(content.DossierRef));
        RequireIdentifier(content.VenueId, nameof(content.VenueId));
        RequireText(content.JournalName, nameof(content.JournalName), 2, 512);
        RequireText(content.TargetArticleType, nameof(content.TargetArticleType), 2, 128);
        if (content.PublicationTier is < 1 or > 4)
        {
            throw new InvalidDataException("Journal publication tier must be between one and four.");
        }
        foreach (int score in new[]
        {
            content.ScopeFitScore,
            content.TheoremPackageFitScore,
            content.ArticleTypeFitScore,
            content.ComparablePaperScore,
            content.FormatFeasibilityScore,
            content.LengthFeasibilityScore,
            content.PolicyCompatibilityScore,
            content.FeeFeasibilityScore,
            content.EvidenceCompletenessScore,
            content.EvidenceRecencyScore,
            content.OverallScore
        })
        {
            if (score is < 0 or > 100)
            {
                throw new InvalidDataException(
                    "Journal venue scorecard values must lie between zero and one hundred.");
            }
        }
        RequireStringList(
            content.Blockers,
            nameof(content.Blockers),
            minimum: 0,
            maximum: 32,
            maximumItemLength: 512);
        if (content.Eligible && content.Blockers.Count != 0)
        {
            throw new InvalidDataException(
                "An eligible journal scorecard cannot contain blockers.");
        }
        ParseUtc(content.ComputedAt, nameof(content.ComputedAt));
    }

    public static void Validate(PaperJournalTargetSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        RequireExact(
            selection.Schema,
            PaperJournalResearchAgentSchemas.TargetSelection,
            nameof(selection.Schema));
        PaperJournalTargetSelectionContent content = selection.SelectionContent
            ?? throw new InvalidDataException(
                "Journal target selection content is required.");
        RequireIdentity(selection.SelectionId, content, nameof(selection.SelectionId));
        RequireDigest(content.DossierRef, nameof(content.DossierRef));
        RequireDigest(content.SourceEditedManuscriptRef, nameof(content.SourceEditedManuscriptRef));
        RequirePaperId(content.PaperId);
        RequireDigest(content.TheoryProgramRef, nameof(content.TheoryProgramRef));
        RequireIdentifier(content.SelectedVenueId, nameof(content.SelectedVenueId));
        RequireText(content.SelectedJournalName, nameof(content.SelectedJournalName), 2, 512);
        RequireText(content.SelectedArticleType, nameof(content.SelectedArticleType), 2, 128);
        RequireDigest(content.SelectedScorecardRef, nameof(content.SelectedScorecardRef));
        RequireStringList(
            content.RankedScorecardRefs,
            nameof(content.RankedScorecardRefs),
            minimum: 2,
            maximum: 8,
            maximumItemLength: 71);
        foreach (string reference in content.RankedScorecardRefs)
        {
            RequireDigest(reference, nameof(content.RankedScorecardRefs));
        }
        RequireStringList(
            content.SelectedSourceIds,
            nameof(content.SelectedSourceIds),
            minimum: 3,
            maximum: 32,
            maximumItemLength: 128);
        if (content.MaximumPublicationTier != 2
            || content.SelectedPublicationTier is < 1 or > 2
            || !string.Equals(
                content.NextRoute,
                "journal-style-editing",
                StringComparison.Ordinal)
            || !content.RankedScorecardRefs.Contains(
                content.SelectedScorecardRef,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Journal target selection tier, route, or scorecard membership is invalid.");
        }
        ParseUtc(content.SelectedAt, nameof(content.SelectedAt));
    }

    public static void Validate(PaperJournalResearchAgentAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperJournalResearchAgentSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        foreach (string digest in new[]
        {
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.SourceScientificEditingTaskRef,
            cursor.SourceEditedManuscriptRef,
            cursor.TheoryProgramRef
        })
        {
            RequireDigest(digest, nameof(cursor));
        }
        RequirePaperId(cursor.PaperId);
        ValidateStoredArtifact(cursor.Dossier, PaperJournalResearchAgentSchemas.Dossier);
        if (cursor.Scorecards is null || cursor.Scorecards.Count < 2 || cursor.Scorecards.Count > 8)
        {
            throw new InvalidDataException(
                "Journal-research cursor must contain between two and eight scorecards.");
        }
        foreach (PaperManuscriptAuthoringStoredArtifact scorecard in cursor.Scorecards)
        {
            ValidateStoredArtifact(
                scorecard,
                PaperJournalResearchAgentSchemas.VenueScorecard);
        }
        if (cursor.Scorecards.Select(value => value.ArtifactRef)
            .Distinct(StringComparer.Ordinal).Count() != cursor.Scorecards.Count)
        {
            throw new InvalidDataException(
                "Journal-research cursor scorecard references must be unique.");
        }
        ValidateStoredArtifact(
            cursor.TargetSelection,
            PaperJournalResearchAgentSchemas.TargetSelection);
        RequireIdentifier(cursor.SelectedVenueId, nameof(cursor.SelectedVenueId));
        RequireText(cursor.SelectedJournalName, nameof(cursor.SelectedJournalName), 2, 512);
        RequireText(cursor.SelectedArticleType, nameof(cursor.SelectedArticleType), 2, 128);
        if (cursor.SelectedPublicationTier is < 1 or > 2
            || !string.Equals(
                cursor.NextRoute,
                "journal-style-editing",
                StringComparison.Ordinal)
            || !ProvenanceValues.Contains(cursor.Provenance))
        {
            throw new InvalidDataException(
                "Journal-research cursor tier, route, or provenance is invalid.");
        }
        RequireRunId(cursor.RunId);
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static PaperAgentTask BuildJournalResearchTask(
        PaperJournalResearchAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperJournalResearchContext context)
    {
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("journal-research");
        PaperAgentInputArtifact[] inputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperJournalResearchAgentSchemas.Dispatch,
                dispatchRef,
                dispatchRelativePath))
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        return new PaperAgentTask(
            PaperAgentSchemas.Task,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            inputs,
            [new PaperAgentExpectedOutput(
                PaperJournalResearchAgentSchemas.Draft,
                "outputs/journal-research-draft.json")],
            ["journal-style-editing", "journal-research", "blocked"],
            BuildJournalResearchInstruction(dispatch, context),
            JournalResearchForbiddenShortcuts(),
            dispatch.RequestedAt);
    }

    private static string BuildJournalResearchInstruction(
        PaperJournalResearchAgentDispatch dispatch,
        PaperJournalResearchContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Research a current portfolio of journals for the supplied scientifically edited mathematical manuscript.");
        builder.AppendLine("Write exactly one paper-journal-research-draft.v1 JSON object to outputs/journal-research-draft.json.");
        builder.AppendLine($"Use dispatch_ref={Reference(CanonicalJson.Serialize(dispatch))}, source_edited_manuscript_ref={dispatch.SourceEditedManuscriptRef}, paper_id={dispatch.PaperId}, and theory_program_ref={dispatch.TheoryProgramRef}.");
        builder.AppendLine($"Research between {dispatch.Policy.MinimumCandidateCount} and {dispatch.Policy.MaximumCandidateCount} journals and identify at least {dispatch.Policy.MinimumEligibleCandidateCount} candidates independently evidenced as Tier {dispatch.Policy.MaximumPublicationTier} or stronger.");
        builder.AppendLine($"The desired article type is {dispatch.Policy.DesiredArticleType}. Research the manuscript as it exists now: title '{context.SourceEditedManuscript.ManuscriptContent.Title}', certified theorem package, proof architecture, source length, formalization contribution, and evidence-backed literature boundary.");
        builder.AppendLine("Use current HTTPS sources. For every venue, cover official scope, official author guidelines, official article types, official formatting, official length, official fees, official policies, an independent tier source, and at least one recent comparable article.");
        builder.AppendLine("Each source snapshot must include normalized source text, its SHA-256 reference, retrieval time, source roles, authority, exact supported assertions, and an evidence_text substring copied into normalized_text. Do not cite search-result snippets when an official page is available.");
        builder.AppendLine("For each venue, report its canonical identity, target article type, claimed tier, scope fit, LaTeX policy, word limits, appendix and supplement status, fees, data/code/preprint/AI policies, peer-review model, access model, evidence source IDs, rationale, and risks.");
        builder.AppendLine("Do not assign a final score or winner. Repository code validates source support, recomputes every score, applies the Tier 2 floor, and selects the target deterministically.");
        builder.AppendLine("A completed result must route to journal-style-editing. Use journal-research only for a no-progress retry and blocked only for a genuine source-access or evidence blocker.");
        return builder.ToString();
    }

    private static string[] JournalResearchForbiddenShortcuts() =>
    [
        "Do not alter the manuscript, theorem package, claim manifest, selected truth release, or scientific-editing artifacts.",
        "Do not invent a journal, ISSN, publisher, tier, policy, fee, article type, word limit, source URL, retrieval date, comparable paper, or evidence quotation.",
        "Do not treat a journal home page, search-result snippet, or aggregator as sufficient evidence for every policy role.",
        "Do not use a publisher-owned prestige statement as the independent tier source.",
        "Do not omit inconvenient fees, policy restrictions, article-type limits, or formatting requirements.",
        "Do not select the winner, provide repository scores, or weaken the Tier 2 minimum.",
        "Do not run Lean, Formalize, Git, GitHub, manuscript editing, peer review, language editing, or cover-letter authoring.",
        "Do not compute dossier, scorecard, selection, cursor, or artifact identities. Repository validation owns them."
    ];
}
