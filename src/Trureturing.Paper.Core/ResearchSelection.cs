using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperResearchSelectionSchemas
{
    public const string Selection = "paper-research-selection.v1";
    public const string FormalizationRequest = "formalization-request.v1";
}

public sealed record PaperResearchTarget(
    [property: JsonRequired] string LemmaStatement,
    [property: JsonRequired] string LemmaGidIntent);

public sealed record PaperResearchSelectionContent(
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyPublicationDigest,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] string IntuitionProposalRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] PaperResearchTarget Target,
    [property: JsonRequired] string ClaimBoundary,
    [property: JsonRequired] string Falsifier,
    [property: JsonRequired] string ExpectedContribution,
    [property: JsonRequired] string VerificationBudgetRef,
    [property: JsonRequired] string SelectedBy,
    [property: JsonRequired] string SelectedAt,
    [property: JsonRequired] string NextTruthReleaseAt);

public sealed record PaperResearchSelection(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string SelectionId,
    [property: JsonRequired] PaperResearchSelectionContent SelectionContent);

public sealed record FormalizationOrigin(
    [property: JsonRequired] string Service,
    [property: JsonRequired] string Identity,
    [property: JsonRequired] string ConfigDigest);

public sealed record FormalizationTarget(
    [property: JsonRequired] string LemmaStatement,
    [property: JsonRequired] string LemmaGidIntent);

public sealed record FormalizationRequestContent(
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyPublicationDigest,
    [property: JsonRequired] FormalizationOrigin OriginatingService,
    [property: JsonRequired] FormalizationTarget Target,
    [property: JsonRequired] string IssuedAt,
    [property: JsonRequired] string NextTruthReleaseAt,
    [property: JsonRequired] string ExpiresAt);

public sealed record FormalizationRequest(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] FormalizationRequestContent RequestContent);

public static class PaperResearchSelectionService
{
    public const string PaperServiceIdentity =
        "the-omega-institute/trureturing-paper";

    public static PaperResearchSelection Create(
        PaperResearchSelectionContent content)
    {
        ValidateContent(content);
        string selectionId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        return new PaperResearchSelection(
            PaperResearchSelectionSchemas.Selection,
            selectionId,
            content);
    }

    public static FormalizationRequest BuildFormalizationRequest(
        PaperResearchSelection selection)
    {
        Validate(selection);
        DateTimeOffset issuedAt = ParseUtc(
            selection.SelectionContent.SelectedAt,
            nameof(selection.SelectionContent.SelectedAt));
        DateTimeOffset nextRelease = ParseUtc(
            selection.SelectionContent.NextTruthReleaseAt,
            nameof(selection.SelectionContent.NextTruthReleaseAt));
        DateTimeOffset expiresAt = issuedAt.AddHours(24) < nextRelease
            ? issuedAt.AddHours(24)
            : nextRelease;

        var content = new FormalizationRequestContent(
            selection.SelectionContent.TruthReleaseDigest,
            selection.SelectionContent.TopologyPublicationDigest,
            new FormalizationOrigin(
                "paper",
                PaperServiceIdentity,
                selection.SelectionId),
            new FormalizationTarget(
                selection.SelectionContent.Target.LemmaStatement,
                selection.SelectionContent.Target.LemmaGidIntent),
            FormatUtc(issuedAt),
            FormatUtc(nextRelease),
            FormatUtc(expiresAt));
        string requestId = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(content));
        return new FormalizationRequest(
            PaperResearchSelectionSchemas.FormalizationRequest,
            requestId,
            content);
    }

    public static void Validate(PaperResearchSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!string.Equals(
                selection.Schema,
                PaperResearchSelectionSchemas.Selection,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Paper research selection has an unsupported schema.");
        }
        ValidateContent(selection.SelectionContent);
        RequireDigest(selection.SelectionId, nameof(selection.SelectionId));
        string expected = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(selection.SelectionContent));
        if (!string.Equals(selection.SelectionId, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "selection_id does not address canonical selection_content bytes.");
        }
    }

    public static void ValidateContent(PaperResearchSelectionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TruthReleaseDigest, nameof(content.TruthReleaseDigest));
        RequireDigest(
            content.TopologyPublicationDigest,
            nameof(content.TopologyPublicationDigest));
        RequireDigest(content.PaperResearchInputRef, nameof(content.PaperResearchInputRef));
        RequireDigest(content.IntuitionProposalRef, nameof(content.IntuitionProposalRef));
        RequireDigest(content.CandidatePaperRef, nameof(content.CandidatePaperRef));
        RequireDigest(content.LiteratureResearchRef, nameof(content.LiteratureResearchRef));
        RequireDigest(content.VerificationBudgetRef, nameof(content.VerificationBudgetRef));
        ArgumentNullException.ThrowIfNull(content.Target);
        RequireText(content.Target.LemmaStatement, nameof(content.Target.LemmaStatement), 16384);
        RequireGid(content.Target.LemmaGidIntent);
        RequireText(content.ClaimBoundary, nameof(content.ClaimBoundary), 8192);
        RequireText(content.Falsifier, nameof(content.Falsifier), 8192);
        RequireText(content.ExpectedContribution, nameof(content.ExpectedContribution), 8192);
        RequireText(content.SelectedBy, nameof(content.SelectedBy), 256);

        DateTimeOffset selectedAt = ParseUtc(
            content.SelectedAt,
            nameof(content.SelectedAt));
        DateTimeOffset nextRelease = ParseUtc(
            content.NextTruthReleaseAt,
            nameof(content.NextTruthReleaseAt));
        if (nextRelease <= selectedAt)
        {
            throw new InvalidDataException(
                "next_truth_release_at must be later than selected_at.");
        }
    }

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GidPattern = new(
        "^D[0-9]+/S[0-9]+/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*(?:\\.[A-Za-z_][A-Za-z0-9_']*)?$",
        RegexOptions.CultureInvariant);

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireGid(string value)
    {
        if (!GidPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                "lemma_gid_intent is not a canonical theorem GID.");
        }
    }

    private static void RequireText(string value, string name, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximum} characters.");
        }
    }

    private static DateTimeOffset ParseUtc(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture);
}

public static class PaperResearchSelectionJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false
    };

    private static readonly string[] ContentProperties =
    [
        "truth_release_digest",
        "topology_publication_digest",
        "paper_research_input_ref",
        "intuition_proposal_ref",
        "candidate_paper_ref",
        "literature_research_ref",
        "target",
        "claim_boundary",
        "falsifier",
        "expected_contribution",
        "verification_budget_ref",
        "selected_by",
        "selected_at",
        "next_truth_release_at"
    ];

    public static PaperResearchSelectionContent ReadContent(
        ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        RequireExactProperties(document.RootElement, ContentProperties);
        RequireExactProperties(
            document.RootElement.GetProperty("target"),
            ["lemma_statement", "lemma_gid_intent"]);
        PaperResearchSelectionContent content =
            JsonSerializer.Deserialize<PaperResearchSelectionContent>(bytes, Options)
            ?? throw new JsonException("Selection content is empty.");
        PaperResearchSelectionService.ValidateContent(content);
        return content;
    }

    public static PaperResearchSelection ReadSelection(ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        RequireExactProperties(
            document.RootElement,
            ["schema", "selection_id", "selection_content"]);
        JsonElement contentElement =
            document.RootElement.GetProperty("selection_content");
        RequireExactProperties(contentElement, ContentProperties);
        RequireExactProperties(
            contentElement.GetProperty("target"),
            ["lemma_statement", "lemma_gid_intent"]);
        PaperResearchSelection selection =
            JsonSerializer.Deserialize<PaperResearchSelection>(bytes, Options)
            ?? throw new JsonException("Selection is empty.");
        PaperResearchSelectionService.Validate(selection);
        return selection;
    }

    public static byte[] Write(PaperResearchSelection selection)
    {
        PaperResearchSelectionService.Validate(selection);
        return CanonicalJson.Serialize(selection);
    }

    public static byte[] Write(FormalizationRequest request) =>
        CanonicalJson.Serialize(request);

    private static void RequireExactProperties(
        JsonElement element,
        IReadOnlyCollection<string> expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Expected a JSON object.");
        }
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!actual.Add(property.Name))
            {
                throw new JsonException(
                    $"Duplicate JSON property '{property.Name}'.");
            }
        }
        if (!actual.SetEquals(expected))
        {
            string unknown = string.Join(", ", actual.Except(expected));
            string missing = string.Join(", ", expected.Except(actual));
            throw new JsonException(
                $"Closed JSON object mismatch. Unknown=[{unknown}] Missing=[{missing}].");
        }
    }
}
