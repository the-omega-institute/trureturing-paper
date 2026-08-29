using System.Text.Json;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class ResearchSelectionTests
{
    private const string A =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string B =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string C =
        "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string D =
        "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string E =
        "sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const string F =
        "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

    [Fact]
    public void SelectionProducesCanonicalPaperOriginRequest()
    {
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(ValidContent());
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(selection);

        Assert.Equal(
            CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(selection.SelectionContent)),
            selection.SelectionId);
        Assert.Equal("formalization-request.v1", request.SchemaVersion);
        Assert.Equal("paper", request.RequestContent.OriginatingService.Service);
        Assert.Equal(
            PaperResearchSelectionService.PaperServiceIdentity,
            request.RequestContent.OriginatingService.Identity);
        Assert.Equal(
            selection.SelectionId,
            request.RequestContent.OriginatingService.ConfigDigest);
        Assert.Equal(
            "2026-08-30T09:00:00Z",
            request.RequestContent.ExpiresAt);
        Assert.Equal(
            CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(request.RequestContent)),
            request.RequestId);
    }

    [Fact]
    public void EarlierNextReleaseBoundsRequestExpiry()
    {
        PaperResearchSelectionContent content = ValidContent() with
        {
            NextTruthReleaseAt = "2026-08-29T12:00:00Z"
        };
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(
                PaperResearchSelectionService.Create(content));

        Assert.Equal(
            "2026-08-29T12:00:00Z",
            request.RequestContent.ExpiresAt);
    }

    [Fact]
    public void TamperedSelectionIdentityFailsClosed()
    {
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(ValidContent()) with
            {
                SelectionId = B
            };

        Assert.Throws<InvalidDataException>(
            () => PaperResearchSelectionService.Validate(selection));
    }

    [Fact]
    public void ReaderRejectsUnknownAndDuplicateProperties()
    {
        byte[] unknown = System.Text.Encoding.UTF8.GetBytes(
            "{\"truth_release_digest\":\"" + A +
            "\",\"topology_publication_digest\":\"" + B +
            "\",\"paper_research_input_ref\":\"" + C +
            "\",\"intuition_proposal_ref\":\"" + D +
            "\",\"candidate_paper_ref\":\"" + E +
            "\",\"literature_research_ref\":\"" + F +
            "\",\"target\":{\"lemma_statement\":\"x = x\",\"lemma_gid_intent\":\"D5/S0/Test.reflexive\"}," +
            "\"claim_boundary\":\"Only reflexivity.\",\"falsifier\":\"A non-reflexive equality witness.\"," +
            "\"expected_contribution\":\"A scoped bridge.\",\"verification_budget_ref\":\"" + A +
            "\",\"selected_by\":\"owner\",\"selected_at\":\"2026-08-29T09:00:00Z\"," +
            "\"next_truth_release_at\":\"2026-08-31T09:00:00Z\",\"extra\":true}");
        Assert.Throws<JsonException>(
            () => PaperResearchSelectionJson.ReadContent(unknown));

        byte[] duplicate = System.Text.Encoding.UTF8.GetBytes(
            "{\"truth_release_digest\":\"" + A +
            "\",\"truth_release_digest\":\"" + B + "\"}");
        Assert.Throws<JsonException>(
            () => PaperResearchSelectionJson.ReadContent(duplicate));
    }

    [Fact]
    public void NextReleaseMustFollowSelection()
    {
        PaperResearchSelectionContent content = ValidContent() with
        {
            NextTruthReleaseAt = "2026-08-29T08:59:59Z"
        };

        Assert.Throws<InvalidDataException>(
            () => PaperResearchSelectionService.Create(content));
    }

    private static PaperResearchSelectionContent ValidContent() => new(
        A,
        B,
        C,
        D,
        E,
        F,
        new PaperResearchTarget(
            "forall x, x = x",
            "D5/S0/Test.reflexive"),
        "The claim is limited to reflexivity in the declared type.",
        "A well-typed witness for which reflexivity fails.",
        "A reusable certified bridge for the selected paper argument.",
        A,
        "AlyciaBHZ",
        "2026-08-29T09:00:00Z",
        "2026-08-31T09:00:00Z");
}
