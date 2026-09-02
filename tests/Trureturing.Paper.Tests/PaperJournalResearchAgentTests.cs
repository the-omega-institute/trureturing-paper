using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperJournalResearchAgentTests
{
    [Fact]
    public void StageTaskBindsCompleteEditedManuscriptClosure()
    {
        using var fixture = new JournalResearchTestRepository();
        PaperAgentTask task = PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
            File.ReadAllBytes(fixture.JournalStaged.TaskPath));

        Assert.Equal("journal-research", task.Phase);
        Assert.Equal("paper-journal-researcher", task.AgentRole);
        Assert.Equal("source-bundle-only", task.ContextMode);
        Assert.Equal(28, task.ExactInputs.Count);
        Assert.Contains(
            task.ExactInputs,
            input => input.Schema == PaperJournalResearchAgentSchemas.Dispatch
                && input.ArtifactRef == fixture.JournalStaged.DispatchRef);
        Assert.Contains(
            task.ExactInputs,
            input => input.Schema == PaperScientificEditingAgentSchemas.EditedManuscript
                && input.ArtifactRef == fixture.ScientificAdmission.EditedManuscript.EnvelopeRef);
        Assert.Equal(
            new[] { "blocked", "journal-research", "journal-style-editing" },
            task.AllowedNextRoutes.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Single(task.ExpectedOutputs);
        Assert.Equal(
            PaperJournalResearchAgentSchemas.Draft,
            task.ExpectedOutputs[0].Schema);
    }

    [Fact]
    public void SourceBackedPortfolioSelectsTierOneVenueDeterministically()
    {
        using var fixture = new JournalResearchTestRepository();
        PaperJournalResearchDraft draft = fixture.BuildValidJournalDraft();
        _ = fixture.RecordCompletedJournalResearch(draft);

        PaperJournalResearchAgentResultAdmitted admitted =
            PaperManuscriptAuthoringAgentService.AdmitJournalResearchResult(
                fixture.Root,
                fixture.JournalStaged.TaskRef);

        Assert.Equal("journal-style-editing", admitted.NextRoute);
        Assert.Equal("journal-alpha", admitted.SelectedVenueId);
        Assert.Equal("Journal Alpha", admitted.SelectedJournalName);
        Assert.Equal(1, admitted.SelectedPublicationTier);
        Assert.Equal("research-article", admitted.SelectedArticleType);
        Assert.Equal(3, admitted.Scorecards.Count);

        PaperJournalTargetSelection selection = ReadStored<PaperJournalTargetSelection>(
            fixture.Root,
            admitted.TargetSelection);
        Assert.Equal(admitted.SelectedVenueId, selection.SelectionContent.SelectedVenueId);
        Assert.Equal(3, selection.SelectionContent.RankedScorecardRefs.Count);
        Assert.Equal(
            admitted.Scorecards.Select(value => value.ArtifactRef).OrderBy(value => value, StringComparer.Ordinal),
            selection.SelectionContent.RankedScorecardRefs.OrderBy(value => value, StringComparer.Ordinal));

        PaperJournalVenueScorecard[] scorecards = admitted.Scorecards
            .Select(stored => ReadStored<PaperJournalVenueScorecard>(fixture.Root, stored))
            .ToArray();
        Assert.Equal(2, scorecards.Count(value => value.ScorecardContent.Eligible));
        Assert.False(scorecards.Single(value =>
            value.ScorecardContent.VenueId == "journal-gamma").ScorecardContent.Eligible);

        PaperJournalResearchAgentResultAdmitted replay =
            PaperManuscriptAuthoringAgentService.AdmitJournalResearchResult(
                fixture.Root,
                fixture.JournalStaged.TaskRef);
        Assert.True(replay.Replayed);
        Assert.Equal(admitted.Dossier.ArtifactRef, replay.Dossier.ArtifactRef);
        Assert.Equal(
            admitted.TargetSelection.ArtifactRef,
            replay.TargetSelection.ArtifactRef);
    }

    [Fact]
    public void PortfolioCannotFallBelowTierTwoPublicationFloor()
    {
        using var fixture = new JournalResearchTestRepository();
        PaperJournalResearchDraft draft = fixture.BuildValidJournalDraft();
        draft = Retier(draft, "journal-alpha", 3);
        draft = Retier(draft, "journal-beta", 3);
        _ = fixture.RecordCompletedJournalResearch(draft);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitJournalResearchResult(
                fixture.Root,
                fixture.JournalStaged.TaskRef));

        Assert.Contains("eligible Tier 2", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaleOrPreTaskSourceSnapshotIsRejected()
    {
        using var fixture = new JournalResearchTestRepository();
        PaperJournalResearchDraft draft = fixture.BuildValidJournalDraft();
        PaperJournalSourceSnapshotDraft first = draft.Sources[0] with
        {
            RetrievedAt = "2026-07-01T00:00:00Z"
        };
        draft = draft with
        {
            Sources = draft.Sources.Select((source, index) => index == 0 ? first : source).ToArray()
        };
        _ = fixture.RecordCompletedJournalResearch(draft);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitJournalResearchResult(
                fixture.Root,
                fixture.JournalStaged.TaskRef));

        Assert.Contains("retrieval time", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssertionMustAppearInContentAddressedSourceText()
    {
        using var fixture = new JournalResearchTestRepository();
        PaperJournalResearchDraft draft = fixture.BuildValidJournalDraft();
        PaperJournalSourceSnapshotDraft first = draft.Sources[0];
        PaperJournalSourceAssertion changed = first.Assertions[0] with
        {
            EvidenceText = "This quotation does not occur in the retained normalized source text."
        };
        first = first with
        {
            Assertions = first.Assertions.Select((assertion, index) =>
                index == 0 ? changed : assertion).ToArray()
        };
        draft = draft with
        {
            Sources = draft.Sources.Select((source, index) => index == 0 ? first : source).ToArray()
        };
        _ = fixture.RecordCompletedJournalResearch(draft);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitJournalResearchResult(
                fixture.Root,
                fixture.JournalStaged.TaskRef));

        Assert.Contains("absent from normalized_text", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PaperJournalResearchDraft Retier(
        PaperJournalResearchDraft draft,
        string venueId,
        int tier)
    {
        PaperJournalVenueCandidateDraft venue = draft.Venues.Single(value =>
            value.VenueId == venueId);
        PaperJournalSourceSnapshotDraft source = draft.Sources.Single(value =>
            value.SourceId == venueId + "-tier");
        string oldEvidence = source.Assertions.Single().EvidenceText;
        string newEvidence = "Independent publication tier: " + tier + ".";
        string newText = source.NormalizedText.Replace(
            oldEvidence,
            newEvidence,
            StringComparison.Ordinal);
        source = source with
        {
            NormalizedText = newText,
            ContentSha256 = CanonicalJson.Sha256Reference(Encoding.UTF8.GetBytes(newText)),
            Assertions = [source.Assertions.Single() with
            {
                Value = tier.ToString(),
                EvidenceText = newEvidence
            }]
        };
        return draft with
        {
            Venues = draft.Venues.Select(value => value.VenueId == venueId
                ? venue with { ClaimedPublicationTier = tier }
                : value).ToArray(),
            Sources = draft.Sources.Select(value => value.SourceId == source.SourceId
                ? source
                : value).ToArray()
        };
    }

    private static T ReadStored<T>(
        string root,
        PaperManuscriptAuthoringStoredArtifact stored) =>
        PaperResearchInputJson.DeserializeStrict<T>(File.ReadAllBytes(Path.Combine(
            root,
            stored.EnvelopePath.Replace('/', Path.DirectorySeparatorChar))));
}
