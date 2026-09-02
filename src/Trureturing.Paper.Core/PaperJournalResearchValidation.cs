using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static partial class PaperManuscriptAuthoringAgentService
{
    private static readonly Regex JournalIssnPattern = new(
        "^[0-9]{4}-[0-9]{3}[0-9Xx]$",
        RegexOptions.CultureInvariant);

    private static readonly Regex JournalCurrencyPattern = new(
        "^(?:[A-Z]{3}|none)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex JournalWordPattern = new(
        "[A-Za-z0-9]+(?:[-'][A-Za-z0-9]+)*",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> JournalTokenStopWords = new(
        [
            "about", "after", "again", "against", "also", "among", "because",
            "before", "being", "between", "both", "could", "from", "have",
            "into", "more", "most", "other", "over", "paper", "research",
            "results", "should", "their", "there", "these", "they", "this",
            "those", "through", "under", "using", "which", "with", "within",
            "would", "journal", "article", "authors", "publication", "submit",
            "submission", "manuscript", "mathematical", "mathematics"
        ],
        StringComparer.Ordinal);

    private static void ValidateJournalResearchPolicy(
        PaperJournalResearchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.MinimumCandidateCount < 2
            || policy.MaximumCandidateCount < policy.MinimumCandidateCount
            || policy.MaximumCandidateCount > 8
            || policy.MinimumEligibleCandidateCount < 2
            || policy.MinimumEligibleCandidateCount > policy.MaximumCandidateCount
            || policy.MaximumPublicationTier != 2
            || policy.MaximumSourceAgeDays is < 1 or > 90)
        {
            throw new InvalidDataException(
                "Journal-research policy is outside the repository-owned bounded ranges.");
        }
        RequireText(
            policy.DesiredArticleType,
            nameof(policy.DesiredArticleType),
            2,
            128);
        if (!IdentifierPattern.IsMatch(policy.DesiredArticleType))
        {
            throw new InvalidDataException(
                "Journal-research desired article type is not canonical.");
        }
    }

    private static void ValidateJournalResearchTaskBinding(
        string root,
        PaperAgentTask task,
        PaperJournalResearchAgentDispatch dispatch,
        string dispatchRef,
        string dispatchPath,
        PaperJournalResearchContext context)
    {
        PaperAgentRuntimeService.Validate(task);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("journal-research");
        if (!string.Equals(task.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                task.TheoryProgramRef,
                dispatch.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(task.Phase, profile.Phase, StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, profile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, profile.ContextMode, StringComparison.Ordinal)
            || !string.Equals(task.RequestedAt, dispatch.RequestedAt, StringComparison.Ordinal)
            || task.ExpectedOutputs.Count != 1
            || !string.Equals(
                task.ExpectedOutputs[0].Schema,
                PaperJournalResearchAgentSchemas.Draft,
                StringComparison.Ordinal)
            || !string.Equals(
                task.ExpectedOutputs[0].WorkspaceRelativePath,
                "outputs/journal-research-draft.json",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal-research task changed its phase, source identity, timestamp, or output contract.");
        }
        string[] expectedRoutes =
            ["blocked", "journal-research", "journal-style-editing"];
        if (!task.AllowedNextRoutes
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(expectedRoutes, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Journal-research task changed its closed route set.");
        }
        PaperAgentInputArtifact[] expectedInputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperJournalResearchAgentSchemas.Dispatch,
                dispatchRef,
                dispatchPath))
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        if (task.ExactInputs.Count != expectedInputs.Length)
        {
            throw new InvalidDataException(
                "Journal-research task changed its exact input count.");
        }
        for (int index = 0; index < expectedInputs.Length; index++)
        {
            PaperAgentInputArtifact expected = expectedInputs[index];
            PaperAgentInputArtifact actual = task.ExactInputs[index];
            if (!string.Equals(actual.Schema, expected.Schema, StringComparison.Ordinal)
                || !string.Equals(actual.ArtifactRef, expected.ArtifactRef, StringComparison.Ordinal)
                || !string.Equals(
                    actual.RepositoryRelativePath,
                    expected.RepositoryRelativePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Journal-research task changed its exact evidence closure.");
            }
            _ = ReadExactInput(root, actual);
        }

        PaperScientificallyEditedManuscriptContent source =
            context.SourceEditedManuscript.ManuscriptContent;
        if (!string.Equals(
                dispatch.SourceScientificEditingTaskRef,
                context.SourceScientificCursor.TaskRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceScientificEditingResultRef,
                context.SourceAgentCursor.ResultRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceScientificEditingCursorRef,
                context.SourceScientificCursorRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceEditedManuscriptRef,
                context.SourceEditedManuscript.ManuscriptId,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceEditedManuscriptEnvelopeRef,
                context.SourceScientificCursor.EditedManuscript.EnvelopeRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceEditDraftRef,
                context.SourceAgentCursor.Outputs[0].ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceEditDeltaRef,
                context.SourceEditDelta.DeltaId,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceMainTexRef,
                context.SourceScientificCursor.MainTex.ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceBibliographyRef,
                context.SourceScientificCursor.Bibliography.ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(dispatch.ClaimManifestRef, source.ClaimManifestRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.ManuscriptPlanRef, source.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.FrontierRef, source.FrontierRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.PaperId, source.PaperId, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryProgramRef, source.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoremPackageRef, source.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryAuditRef, source.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.LiteratureResearchRef, source.LiteratureResearchRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SelectedReleaseRef, source.SelectedReleaseRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SelectedReleaseDigest, source.SelectedReleaseDigest, StringComparison.Ordinal)
            || !dispatch.ExactInputs.SequenceEqual(context.ExactInputs))
        {
            throw new InvalidDataException(
                "Journal-research dispatch changed its admitted scientific manuscript lineage.");
        }
    }

    private static PaperJournalResearchDossier BuildJournalResearchDossier(
        PaperJournalResearchDraft draft,
        PaperJournalResearchAgentDispatch dispatch,
        string dispatchRef,
        PaperJournalResearchContext context,
        string taskRef,
        string resultRef,
        string completedAt)
    {
        ValidateJournalResearchDraft(
            draft,
            dispatch,
            dispatchRef,
            context,
            completedAt);
        PaperJournalSourceSnapshot[] sources = draft.Sources
            .Select(source => new PaperJournalSourceSnapshot(
                source.SourceId,
                source.VenueId,
                source.Roles.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                source.Authority,
                source.Url,
                source.Title,
                source.RetrievedAt,
                source.UpdatedAt,
                source.NormalizedText,
                source.ContentSha256,
                source.Assertions
                    .OrderBy(value => value.Fact, StringComparer.Ordinal)
                    .ThenBy(value => value.Value, StringComparer.Ordinal)
                    .ThenBy(value => value.EvidenceText, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(value => value.VenueId, StringComparer.Ordinal)
            .ThenBy(value => value.SourceId, StringComparer.Ordinal)
            .ToArray();
        PaperJournalVenueEvidence[] venues = draft.Venues
            .Select(venue => new PaperJournalVenueEvidence(
                venue.VenueId,
                venue.JournalName,
                venue.Publisher,
                venue.Issn.ToUpperInvariant(),
                venue.CanonicalUrl,
                venue.TargetArticleType,
                venue.ClaimedPublicationTier,
                venue.ScopeFit,
                venue.ArticleTypeSupported,
                venue.LatexPolicy,
                venue.MaximumAbstractWords,
                venue.MaximumMainTextWords,
                venue.ProofAppendixAllowed,
                venue.SupplementaryMaterialAllowed,
                venue.FeeStatus,
                venue.MandatoryFeeMinorUnits,
                venue.FeeCurrency,
                venue.DataPolicy,
                venue.CodePolicy,
                venue.PreprintPolicy,
                venue.AiPolicy,
                venue.PeerReviewModel,
                venue.AccessModel,
                venue.SourceIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                venue.ComparablePaperSourceIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                venue.Rationale,
                venue.Risks))
            .OrderBy(value => value.VenueId, StringComparer.Ordinal)
            .ToArray();
        DateTimeOffset completed = ParseUtc(completedAt, nameof(completedAt));
        string cutoff = completed
            .AddDays(-dispatch.Policy.MaximumSourceAgeDays)
            .ToString("O", CultureInfo.InvariantCulture);
        var content = new PaperJournalResearchDossierContent(
            taskRef,
            resultRef,
            dispatchRef,
            dispatch.SourceScientificEditingTaskRef,
            dispatch.SourceEditedManuscriptRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.ClaimManifestRef,
            dispatch.ManuscriptPlanRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.LiteratureResearchRef,
            dispatch.SelectedReleaseRef,
            dispatch.SelectedReleaseDigest,
            dispatch.Policy,
            venues,
            sources,
            CountManuscriptWords(context.SourceMainTex),
            cutoff,
            completedAt);
        var dossier = new PaperJournalResearchDossier(
            PaperJournalResearchAgentSchemas.Dossier,
            Reference(CanonicalJson.Serialize(content)),
            content);
        Validate(dossier);
        return dossier;
    }

    private static void ValidateJournalResearchDraft(
        PaperJournalResearchDraft draft,
        PaperJournalResearchAgentDispatch dispatch,
        string dispatchRef,
        PaperJournalResearchContext context,
        string completedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireExact(draft.Schema, PaperJournalResearchAgentSchemas.Draft, nameof(draft.Schema));
        if (!string.Equals(draft.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(
                draft.SourceEditedManuscriptRef,
                dispatch.SourceEditedManuscriptRef,
                StringComparison.Ordinal)
            || !string.Equals(draft.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                draft.TheoryProgramRef,
                dispatch.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(
                draft.DesiredArticleType,
                dispatch.Policy.DesiredArticleType,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal-research draft changed dispatch, manuscript, paper, program, or article type identity.");
        }
        if (draft.Venues is null
            || draft.Venues.Count < dispatch.Policy.MinimumCandidateCount
            || draft.Venues.Count > dispatch.Policy.MaximumCandidateCount
            || draft.Sources is null
            || draft.Sources.Count < draft.Venues.Count)
        {
            throw new InvalidDataException(
                "Journal-research draft venue or source count is outside policy.");
        }
        RequireStringList(
            draft.PortfolioRationale,
            nameof(draft.PortfolioRationale),
            minimum: 2,
            maximum: 16,
            maximumItemLength: 2048);
        RequireStringList(
            draft.RemainingRisks,
            nameof(draft.RemainingRisks),
            minimum: 0,
            maximum: 32,
            maximumItemLength: 2048);
        DateTimeOffset requested = ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
        DateTimeOffset created = ParseUtc(draft.CreatedAt, nameof(draft.CreatedAt));
        DateTimeOffset completed = ParseUtc(completedAt, nameof(completedAt));
        if (created < requested || created > completed)
        {
            throw new InvalidDataException(
                "Journal-research draft created_at must lie between task request and completion.");
        }

        var venuesById = new Dictionary<string, PaperJournalVenueCandidateDraft>(
            StringComparer.Ordinal);
        foreach (PaperJournalVenueCandidateDraft venue in draft.Venues)
        {
            ValidateJournalVenueShape(venue, dispatch.Policy);
            if (!venuesById.TryAdd(venue.VenueId, venue))
            {
                throw new InvalidDataException(
                    "Journal-research venue IDs must be unique.");
            }
        }

        var sourcesById = new Dictionary<string, PaperJournalSourceSnapshotDraft>(
            StringComparer.Ordinal);
        foreach (PaperJournalSourceSnapshotDraft source in draft.Sources)
        {
            ValidateJournalSource(
                source,
                venuesById,
                requested,
                completed,
                dispatch.Policy.MaximumSourceAgeDays);
            if (!sourcesById.TryAdd(source.SourceId, source))
            {
                throw new InvalidDataException(
                    "Journal-research source IDs must be unique.");
            }
        }

        var usedSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperJournalVenueCandidateDraft venue in draft.Venues)
        {
            ValidateJournalVenueEvidence(venue, sourcesById);
            foreach (string sourceId in venue.SourceIds)
            {
                usedSources.Add(sourceId);
            }
        }
        if (!usedSources.SetEquals(sourcesById.Keys))
        {
            throw new InvalidDataException(
                "Every journal source must belong to exactly one admitted venue evidence packet.");
        }

        _ = context.SourceEditedManuscript.ManuscriptContent.Title;
    }

    private static void ValidateJournalVenueShape(
        PaperJournalVenueCandidateDraft venue,
        PaperJournalResearchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(venue);
        RequireIdentifier(venue.VenueId, nameof(venue.VenueId));
        RequireText(venue.JournalName, nameof(venue.JournalName), 2, 512);
        RequireText(venue.Publisher, nameof(venue.Publisher), 2, 512);
        if (!JournalIssnPattern.IsMatch(venue.Issn ?? string.Empty))
        {
            throw new InvalidDataException("Journal ISSN is not canonical.");
        }
        RequireHttpsUrl(venue.CanonicalUrl, nameof(venue.CanonicalUrl));
        RequireText(venue.TargetArticleType, nameof(venue.TargetArticleType), 2, 128);
        if (!string.Equals(
                venue.TargetArticleType,
                policy.DesiredArticleType,
                StringComparison.Ordinal)
            || venue.ClaimedPublicationTier is < 1 or > 4
            || !JournalScopeFits.Contains(venue.ScopeFit)
            || !JournalLatexPolicies.Contains(venue.LatexPolicy)
            || venue.MaximumAbstractWords < 0
            || venue.MaximumAbstractWords > 10000
            || venue.MaximumMainTextWords < 0
            || venue.MaximumMainTextWords > 200000
            || !JournalFeeStatuses.Contains(venue.FeeStatus)
            || venue.MandatoryFeeMinorUnits < 0
            || venue.MandatoryFeeMinorUnits > 100000000
            || !JournalCurrencyPattern.IsMatch(venue.FeeCurrency ?? string.Empty)
            || !JournalDataCodePolicies.Contains(venue.DataPolicy)
            || !JournalDataCodePolicies.Contains(venue.CodePolicy)
            || !JournalPreprintPolicies.Contains(venue.PreprintPolicy)
            || !JournalAiPolicies.Contains(venue.AiPolicy)
            || !JournalPeerReviewModels.Contains(venue.PeerReviewModel)
            || !JournalAccessModels.Contains(venue.AccessModel))
        {
            throw new InvalidDataException(
                "Journal venue facts contain an unsupported or out-of-range value.");
        }
        if (venue.FeeStatus == "none"
            && (venue.MandatoryFeeMinorUnits != 0 || venue.FeeCurrency != "none"))
        {
            throw new InvalidDataException(
                "A no-fee journal must report zero mandatory fee and currency 'none'.");
        }
        if (venue.FeeStatus is "optional" or "mandatory-known"
            && venue.FeeCurrency == "none")
        {
            throw new InvalidDataException(
                "A known optional or mandatory fee requires an ISO currency.");
        }
        if (venue.FeeStatus == "unknown"
            && (venue.MandatoryFeeMinorUnits != 0 || venue.FeeCurrency != "none"))
        {
            throw new InvalidDataException(
                "Unknown fee status cannot carry an invented amount or currency.");
        }
        RequireStringList(
            venue.SourceIds,
            nameof(venue.SourceIds),
            minimum: 3,
            maximum: 32,
            maximumItemLength: 128);
        RequireStringList(
            venue.ComparablePaperSourceIds,
            nameof(venue.ComparablePaperSourceIds),
            minimum: 1,
            maximum: 8,
            maximumItemLength: 128);
        RequireStringList(
            venue.Rationale,
            nameof(venue.Rationale),
            minimum: 2,
            maximum: 16,
            maximumItemLength: 2048);
        RequireStringList(
            venue.Risks,
            nameof(venue.Risks),
            minimum: 0,
            maximum: 16,
            maximumItemLength: 2048);
    }

    private static void ValidateJournalSource(
        PaperJournalSourceSnapshotDraft source,
        IReadOnlyDictionary<string, PaperJournalVenueCandidateDraft> venuesById,
        DateTimeOffset requested,
        DateTimeOffset completed,
        int maximumSourceAgeDays)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireIdentifier(source.SourceId, nameof(source.SourceId));
        RequireIdentifier(source.VenueId, nameof(source.VenueId));
        if (!venuesById.ContainsKey(source.VenueId))
        {
            throw new InvalidDataException(
                "Journal source points to an unknown venue.");
        }
        RequireStringList(
            source.Roles,
            nameof(source.Roles),
            minimum: 1,
            maximum: 4,
            maximumItemLength: 64);
        if (source.Roles.Any(role => !JournalSourceRoles.Contains(role))
            || !JournalSourceAuthorities.Contains(source.Authority))
        {
            throw new InvalidDataException(
                "Journal source role or authority is unsupported.");
        }
        bool hasOfficial = source.Roles.Any(role => role.StartsWith(
            "official-",
            StringComparison.Ordinal));
        if (hasOfficial && source.Authority != "official")
        {
            throw new InvalidDataException(
                "Official journal facts require an official source authority.");
        }
        if (source.Roles.Contains("independent-tier", StringComparer.Ordinal)
            && source.Authority != "independent-index")
        {
            throw new InvalidDataException(
                "Publication tier requires an independent index source.");
        }
        if (source.Roles.Contains("recent-comparable", StringComparer.Ordinal)
            && source.Authority != "journal-article")
        {
            throw new InvalidDataException(
                "Recent comparable-paper evidence requires a journal-article source.");
        }
        RequireHttpsUrl(source.Url, nameof(source.Url));
        RequireText(source.Title, nameof(source.Title), 2, 1024);
        if (source.NormalizedText is null
            || source.NormalizedText.Length < JournalMinimumSourceTextLength
            || source.NormalizedText.Length > JournalMaximumSourceTextLength
            || source.NormalizedText.Contains('\r')
            || !string.Equals(
                source.NormalizedText,
                source.NormalizedText.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal source normalized_text is missing, unbounded, or noncanonical.");
        }
        RequireDigest(source.ContentSha256, nameof(source.ContentSha256));
        string actualContentRef = Reference(Encoding.UTF8.GetBytes(source.NormalizedText));
        if (!string.Equals(
                actualContentRef,
                source.ContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal source content_sha256 does not match normalized_text bytes.");
        }
        DateTimeOffset retrieved = ParseUtc(source.RetrievedAt, nameof(source.RetrievedAt));
        if (retrieved < requested
            || retrieved > completed
            || completed - retrieved > TimeSpan.FromDays(maximumSourceAgeDays))
        {
            throw new InvalidDataException(
                "Journal source retrieval time is outside the task or recency window.");
        }
        if (!string.IsNullOrEmpty(source.UpdatedAt))
        {
            DateTimeOffset updated = ParseUtc(source.UpdatedAt, nameof(source.UpdatedAt));
            if (updated > retrieved)
            {
                throw new InvalidDataException(
                    "Journal source updated_at cannot follow retrieved_at.");
            }
        }
        if (source.Assertions is null
            || source.Assertions.Count < 1
            || source.Assertions.Count > 32)
        {
            throw new InvalidDataException(
                "Journal source must contain between one and thirty-two assertions.");
        }
        var assertions = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperJournalSourceAssertion assertion in source.Assertions)
        {
            ArgumentNullException.ThrowIfNull(assertion);
            if (!JournalAssertionFacts.Contains(assertion.Fact))
            {
                throw new InvalidDataException(
                    $"Unsupported journal source assertion fact '{assertion.Fact}'.");
            }
            RequireText(assertion.Value, nameof(assertion.Value), 1, 2048);
            RequireText(assertion.EvidenceText, nameof(assertion.EvidenceText), 5, 4096);
            if (source.NormalizedText.IndexOf(
                    assertion.EvidenceText,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidDataException(
                    "Journal source assertion evidence_text is absent from normalized_text.");
            }
            string identity = assertion.Fact + "\0" + assertion.Value + "\0" + assertion.EvidenceText;
            if (!assertions.Add(identity))
            {
                throw new InvalidDataException(
                    "Journal source assertions must be unique.");
            }
        }
    }

    private static void ValidateJournalVenueEvidence(
        PaperJournalVenueCandidateDraft venue,
        IReadOnlyDictionary<string, PaperJournalSourceSnapshotDraft> sourcesById)
    {
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var venueSources = new List<PaperJournalSourceSnapshotDraft>();
        foreach (string sourceId in venue.SourceIds)
        {
            RequireIdentifier(sourceId, nameof(venue.SourceIds));
            if (!sourceIds.Add(sourceId)
                || !sourcesById.TryGetValue(sourceId, out PaperJournalSourceSnapshotDraft? source)
                || !string.Equals(source.VenueId, venue.VenueId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Journal venue source IDs are duplicated, missing, or cross-venue.");
            }
            venueSources.Add(source);
        }
        foreach (string comparableId in venue.ComparablePaperSourceIds)
        {
            if (!sourceIds.Contains(comparableId)
                || !sourcesById[comparableId].Roles.Contains(
                    "recent-comparable",
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Journal comparable-paper source is missing or has the wrong role.");
            }
        }
        foreach (string role in RequiredJournalSourceRoles)
        {
            if (!venueSources.Any(source => source.Roles.Contains(role, StringComparer.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Journal venue evidence is missing required role '{role}'.");
            }
        }

        RequireAssertedFact(venueSources, "journal-name", venue.JournalName);
        RequireAssertedFact(venueSources, "publisher", venue.Publisher);
        RequireAssertedFact(venueSources, "issn", venue.Issn.ToUpperInvariant());
        RequireAssertedFact(
            venueSources,
            "publication-tier",
            venue.ClaimedPublicationTier.ToString(CultureInfo.InvariantCulture));
        RequireAssertedFact(venueSources, "scope-fit", venue.ScopeFit);
        RequireAssertedFact(
            venueSources,
            "target-article-type",
            venue.TargetArticleType);
        RequireAssertedFact(
            venueSources,
            "article-type-supported",
            venue.ArticleTypeSupported ? "true" : "false");
        RequireAssertedFact(venueSources, "latex-policy", venue.LatexPolicy);
        RequireAssertedFact(
            venueSources,
            "maximum-abstract-words",
            venue.MaximumAbstractWords.ToString(CultureInfo.InvariantCulture));
        RequireAssertedFact(
            venueSources,
            "maximum-main-text-words",
            venue.MaximumMainTextWords.ToString(CultureInfo.InvariantCulture));
        RequireAssertedFact(
            venueSources,
            "proof-appendix-allowed",
            venue.ProofAppendixAllowed ? "true" : "false");
        RequireAssertedFact(
            venueSources,
            "supplementary-material-allowed",
            venue.SupplementaryMaterialAllowed ? "true" : "false");
        RequireAssertedFact(venueSources, "fee-status", venue.FeeStatus);
        RequireAssertedFact(
            venueSources,
            "mandatory-fee-minor-units",
            venue.MandatoryFeeMinorUnits.ToString(CultureInfo.InvariantCulture));
        RequireAssertedFact(venueSources, "fee-currency", venue.FeeCurrency);
        RequireAssertedFact(venueSources, "data-policy", venue.DataPolicy);
        RequireAssertedFact(venueSources, "code-policy", venue.CodePolicy);
        RequireAssertedFact(venueSources, "preprint-policy", venue.PreprintPolicy);
        RequireAssertedFact(venueSources, "ai-policy", venue.AiPolicy);
        RequireAssertedFact(venueSources, "peer-review-model", venue.PeerReviewModel);
        RequireAssertedFact(venueSources, "access-model", venue.AccessModel);
    }

    private static void RequireAssertedFact(
        IReadOnlyList<PaperJournalSourceSnapshotDraft> sources,
        string fact,
        string expectedValue)
    {
        if (!sources.Any(source => source.Assertions.Any(assertion =>
                string.Equals(assertion.Fact, fact, StringComparison.Ordinal)
                && string.Equals(assertion.Value, expectedValue, StringComparison.Ordinal))))
        {
            throw new InvalidDataException(
                $"Journal venue fact '{fact}' is not backed by an exact source assertion.");
        }
    }

    private static void RequireHttpsUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || value.Length > 4096)
        {
            throw new InvalidDataException(
                $"{name} must be a canonical HTTPS URL without credentials or fragment.");
        }
    }

    private static void ValidateJournalResearchArtifacts(
        PaperJournalResearchDossier dossier,
        IReadOnlyList<PaperJournalVenueScorecard> scorecards,
        PaperJournalTargetSelection selection,
        PaperJournalResearchAgentDispatch dispatch,
        PaperJournalResearchContext context)
    {
        Validate(dossier);
        foreach (PaperJournalVenueScorecard scorecard in scorecards)
        {
            Validate(scorecard);
        }
        Validate(selection);
        PaperJournalResearchDossierContent d = dossier.DossierContent;
        if (!string.Equals(d.DispatchRef, Reference(CanonicalJson.Serialize(dispatch)), StringComparison.Ordinal)
            || !string.Equals(d.SourceScientificEditingTaskRef, dispatch.SourceScientificEditingTaskRef, StringComparison.Ordinal)
            || !string.Equals(d.SourceEditedManuscriptRef, dispatch.SourceEditedManuscriptRef, StringComparison.Ordinal)
            || !string.Equals(d.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(d.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(d.ClaimManifestRef, dispatch.ClaimManifestRef, StringComparison.Ordinal)
            || !string.Equals(d.ManuscriptPlanRef, dispatch.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(d.TheoremPackageRef, dispatch.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(d.TheoryAuditRef, dispatch.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(d.LiteratureResearchRef, dispatch.LiteratureResearchRef, StringComparison.Ordinal)
            || !string.Equals(d.SelectedReleaseRef, dispatch.SelectedReleaseRef, StringComparison.Ordinal)
            || !string.Equals(d.SelectedReleaseDigest, dispatch.SelectedReleaseDigest, StringComparison.Ordinal)
            || d.Policy != dispatch.Policy
            || d.ManuscriptWordCount != CountManuscriptWords(context.SourceMainTex)
            || scorecards.Count != d.Venues.Count)
        {
            throw new InvalidDataException(
                "Journal-research artifacts changed source manuscript or certified evidence lineage.");
        }
        string computedAt = scorecards.Select(value => value.ScorecardContent.ComputedAt)
            .Distinct(StringComparer.Ordinal).SingleOrDefault()
            ?? throw new InvalidDataException(
                "Journal venue scorecards must share one computation timestamp.");
        PaperJournalVenueScorecard[] expectedScorecards = ComputeVenueScorecards(
            dossier,
            context,
            computedAt);
        string[] actualScorecardIds = scorecards
            .OrderBy(value => value.ScorecardContent.VenueId, StringComparer.Ordinal)
            .Select(value => value.ScorecardId)
            .ToArray();
        string[] expectedScorecardIds = expectedScorecards
            .Select(value => value.ScorecardId)
            .ToArray();
        if (!actualScorecardIds.SequenceEqual(expectedScorecardIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Journal venue scorecards do not match repository recomputation.");
        }
        PaperJournalTargetSelection expectedSelection = SelectJournalTarget(
            dossier,
            expectedScorecards,
            selection.SelectionContent.SelectedAt);
        if (!string.Equals(
                selection.SelectionId,
                expectedSelection.SelectionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal target selection does not match deterministic repository ranking.");
        }
    }

    private static int CountManuscriptWords(ReadOnlySpan<byte> mainTexBytes)
    {
        string text = Encoding.UTF8.GetString(mainTexBytes);
        text = Regex.Replace(
            text,
            "(?m)%.*$",
            " ",
            RegexOptions.CultureInvariant);
        text = Regex.Replace(
            text,
            "\\\\[A-Za-z@]+(?:\\[[^\\]]*\\])?",
            " ",
            RegexOptions.CultureInvariant);
        text = text.Replace('{', ' ')
            .Replace('}', ' ')
            .Replace('$', ' ')
            .Replace('\\', ' ');
        int count = JournalWordPattern.Matches(text).Count;
        if (count < 1)
        {
            throw new InvalidDataException(
                "Scientifically edited manuscript contains no countable words.");
        }
        return count;
    }

    private static int CountAbstractWords(PaperJournalResearchContext context) =>
        JournalWordPattern.Matches(context.SourceEditDraft.AbstractLatex).Count;

    private static HashSet<string> JournalTokens(string value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in JournalWordPattern.Matches(value.ToLowerInvariant()))
        {
            string token = match.Value;
            if (token.Length >= 4 && !JournalTokenStopWords.Contains(token))
            {
                result.Add(token);
            }
        }
        return result;
    }
}
