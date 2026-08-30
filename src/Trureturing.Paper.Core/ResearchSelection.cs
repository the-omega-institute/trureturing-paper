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
    [property: JsonRequired] string LemmaGidIntent,
    [property: JsonRequired] IReadOnlyList<string> KnownDependencies,
    [property: JsonRequired] IReadOnlyList<string> AllowedAssumptions,
    [property: JsonRequired] IReadOnlyList<string> ForbiddenWeakenings);

public sealed record PaperResearchFailureSemantics(
    [property: JsonRequired] bool CounterexampleIsUseful,
    [property: JsonRequired] bool MissingPrerequisiteIsReportable);

public sealed record PaperResearchSelectionContent(
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyDigest,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] string IntuitionProposalRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string RoleInArgument,
    [property: JsonRequired] PaperResearchTarget Target,
    [property: JsonRequired] string ClaimBoundary,
    [property: JsonRequired] string Falsifier,
    [property: JsonRequired] string ExpectedContribution,
    [property: JsonRequired] IReadOnlyList<string> ReuseApi,
    [property: JsonRequired] PaperResearchFailureSemantics FailureSemantics,
    [property: JsonRequired] string VerificationBudgetRef,
    [property: JsonRequired] string SelectedBy,
    [property: JsonRequired] string SelectedAt,
    [property: JsonRequired] string NextTruthReleaseAt);

public sealed record PaperResearchSelection(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string SelectionId,
    [property: JsonRequired] PaperResearchSelectionContent SelectionContent);

public sealed record FormalizationTruthRelease(
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string ReleaseDigest);

public sealed record FormalizationPaperContext(
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchCandidateId,
    [property: JsonRequired] string RoleInArgument,
    [property: JsonRequired] string WhyLoadBearing);

public sealed record FormalizationTarget(
    string? PreferredGid,
    [property: JsonRequired] string Statement,
    string? DesiredGenerality,
    [property: JsonRequired] IReadOnlyList<string> KnownDependencies,
    [property: JsonRequired] IReadOnlyList<string> AllowedAssumptions,
    IReadOnlyList<string>? ForbiddenWeakenings);

public sealed record FormalizationFailureSemantics(
    [property: JsonRequired] bool CounterexampleIsUseful,
    [property: JsonRequired] bool MissingPrerequisiteIsReportable);

public sealed record FormalizationRequest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] FormalizationTruthRelease TruthRelease,
    [property: JsonRequired] FormalizationPaperContext PaperContext,
    [property: JsonRequired] FormalizationTarget Target,
    [property: JsonRequired] IReadOnlyList<string> ReuseApi,
    [property: JsonRequired] FormalizationFailureSemantics FailureSemantics);

internal sealed record FormalizationRequestIdentity(
    string Schema,
    FormalizationTruthRelease TruthRelease,
    FormalizationPaperContext PaperContext,
    FormalizationTarget Target,
    IReadOnlyList<string> ReuseApi,
    FormalizationFailureSemantics FailureSemantics);

public static class PaperResearchSelectionService
{
    public const string TruthSourceRepository =
        "the-omega-institute/trureturing";

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
        PaperResearchSelection selection,
        PaperResearchInput researchInput)
    {
        Validate(selection);
        ArgumentNullException.ThrowIfNull(researchInput);
        PaperResearchInputValidation.Validate(researchInput);

        string actualResearchInputRef = CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(researchInput));
        if (!string.Equals(
                selection.SelectionContent.PaperResearchInputRef,
                actualResearchInputRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "paper_research_input_ref does not address the supplied exact-release research input.");
        }

        if (!string.Equals(
                selection.SelectionContent.TruthReleaseDigest,
                researchInput.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                selection.SelectionContent.TopologyDigest,
                researchInput.TopologyDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The selection and Paper research input do not describe one exact research state.");
        }

        RequireGitSha1(
            researchInput.SourceCommit,
            nameof(researchInput.SourceCommit));
        RequireGitSha1(
            researchInput.SourceTree,
            nameof(researchInput.SourceTree));

        var request = new FormalizationRequest(
            PaperResearchSelectionSchemas.FormalizationRequest,
            string.Empty,
            new FormalizationTruthRelease(
                TruthSourceRepository,
                researchInput.SourceCommit,
                researchInput.SourceTree,
                researchInput.TruthReleaseDigest),
            new FormalizationPaperContext(
                selection.SelectionContent.PaperId,
                selection.SelectionContent.CandidatePaperRef,
                selection.SelectionContent.RoleInArgument,
                selection.SelectionContent.ExpectedContribution),
            new FormalizationTarget(
                selection.SelectionContent.Target.LemmaGidIntent,
                selection.SelectionContent.Target.LemmaStatement,
                selection.SelectionContent.ClaimBoundary,
                selection.SelectionContent.Target.KnownDependencies.ToArray(),
                selection.SelectionContent.Target.AllowedAssumptions.ToArray(),
                selection.SelectionContent.Target.ForbiddenWeakenings.ToArray()),
            selection.SelectionContent.ReuseApi.ToArray(),
            new FormalizationFailureSemantics(
                selection.SelectionContent.FailureSemantics.CounterexampleIsUseful,
                selection.SelectionContent.FailureSemantics
                    .MissingPrerequisiteIsReportable));

        request = request with
        {
            RequestId = ComputeFormalizationRequestId(request)
        };
        Validate(request);
        return request;
    }

    public static string ComputeFormalizationRequestId(
        FormalizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var identity = new FormalizationRequestIdentity(
            request.Schema,
            request.TruthRelease,
            request.PaperContext,
            request.Target,
            request.ReuseApi,
            request.FailureSemantics);
        return CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(identity));
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

    public static void Validate(FormalizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(
                request.Schema,
                PaperResearchSelectionSchemas.FormalizationRequest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalization request has an unsupported schema.");
        }
        RequireDigest(request.RequestId, nameof(request.RequestId));

        ArgumentNullException.ThrowIfNull(request.TruthRelease);
        if (!string.Equals(
                request.TruthRelease.SourceRepo,
                TruthSourceRepository,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalization request targets an unexpected truth repository.");
        }
        RequireGitSha1(
            request.TruthRelease.SourceCommit,
            nameof(request.TruthRelease.SourceCommit));
        RequireGitSha1(
            request.TruthRelease.SourceTree,
            nameof(request.TruthRelease.SourceTree));
        RequireDigest(
            request.TruthRelease.ReleaseDigest,
            nameof(request.TruthRelease.ReleaseDigest));

        ArgumentNullException.ThrowIfNull(request.PaperContext);
        RequireText(
            request.PaperContext.PaperId,
            nameof(request.PaperContext.PaperId),
            512);
        RequireText(
            request.PaperContext.ResearchCandidateId,
            nameof(request.PaperContext.ResearchCandidateId),
            512);
        RequireText(
            request.PaperContext.RoleInArgument,
            nameof(request.PaperContext.RoleInArgument),
            8192);
        RequireText(
            request.PaperContext.WhyLoadBearing,
            nameof(request.PaperContext.WhyLoadBearing),
            8192);

        ArgumentNullException.ThrowIfNull(request.Target);
        if (request.Target.PreferredGid is not null)
        {
            RequireGid(request.Target.PreferredGid);
        }
        RequireText(
            request.Target.Statement,
            nameof(request.Target.Statement),
            16384);
        if (request.Target.DesiredGenerality is not null)
        {
            RequireText(
                request.Target.DesiredGenerality,
                nameof(request.Target.DesiredGenerality),
                8192);
        }
        RequireTextList(
            request.Target.KnownDependencies,
            nameof(request.Target.KnownDependencies),
            4096);
        RequireTextList(
            request.Target.AllowedAssumptions,
            nameof(request.Target.AllowedAssumptions),
            4096);
        if (request.Target.ForbiddenWeakenings is not null)
        {
            RequireTextList(
                request.Target.ForbiddenWeakenings,
                nameof(request.Target.ForbiddenWeakenings),
                8192);
        }
        RequireTextList(
            request.ReuseApi,
            nameof(request.ReuseApi),
            4096);
        ArgumentNullException.ThrowIfNull(request.FailureSemantics);

        string expected = ComputeFormalizationRequestId(request);
        if (!string.Equals(request.RequestId, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "request_id does not address the canonical request with request_id excluded.");
        }
    }

    public static void ValidateContent(PaperResearchSelectionContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TruthReleaseDigest, nameof(content.TruthReleaseDigest));
        RequireDigest(content.TopologyDigest, nameof(content.TopologyDigest));
        RequireDigest(content.PaperResearchInputRef, nameof(content.PaperResearchInputRef));
        RequireDigest(content.IntuitionProposalRef, nameof(content.IntuitionProposalRef));
        RequireDigest(content.CandidatePaperRef, nameof(content.CandidatePaperRef));
        RequireDigest(content.LiteratureResearchRef, nameof(content.LiteratureResearchRef));
        RequireText(content.PaperId, nameof(content.PaperId), 512);
        RequireText(content.RoleInArgument, nameof(content.RoleInArgument), 8192);
        RequireDigest(content.VerificationBudgetRef, nameof(content.VerificationBudgetRef));

        ArgumentNullException.ThrowIfNull(content.Target);
        RequireText(
            content.Target.LemmaStatement,
            nameof(content.Target.LemmaStatement),
            16384);
        RequireGid(content.Target.LemmaGidIntent);
        RequireTextList(
            content.Target.KnownDependencies,
            nameof(content.Target.KnownDependencies),
            4096);
        RequireTextList(
            content.Target.AllowedAssumptions,
            nameof(content.Target.AllowedAssumptions),
            4096);
        RequireTextList(
            content.Target.ForbiddenWeakenings,
            nameof(content.Target.ForbiddenWeakenings),
            8192);

        RequireText(content.ClaimBoundary, nameof(content.ClaimBoundary), 8192);
        RequireText(content.Falsifier, nameof(content.Falsifier), 8192);
        RequireText(
            content.ExpectedContribution,
            nameof(content.ExpectedContribution),
            8192);
        RequireTextList(content.ReuseApi, nameof(content.ReuseApi), 4096);
        ArgumentNullException.ThrowIfNull(content.FailureSemantics);
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

    private static readonly Regex GitSha1Pattern = new(
        "^[0-9a-f]{40}$",
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

    private static void RequireGitSha1(string value, string name)
    {
        if (!GitSha1Pattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be a 40-character lowercase Git object id.");
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

    private static void RequireTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximumItemLength)
    {
        if (values is null)
        {
            throw new InvalidDataException($"{name} must be an array.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, maximumItemLength);
            if (!seen.Add(value))
            {
                throw new InvalidDataException(
                    $"{name} must not contain duplicate values.");
            }
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
}

public static class PaperResearchSelectionJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly string[] ContentProperties =
    [
        "truth_release_digest",
        "topology_digest",
        "paper_research_input_ref",
        "intuition_proposal_ref",
        "candidate_paper_ref",
        "literature_research_ref",
        "paper_id",
        "role_in_argument",
        "target",
        "claim_boundary",
        "falsifier",
        "expected_contribution",
        "reuse_api",
        "failure_semantics",
        "verification_budget_ref",
        "selected_by",
        "selected_at",
        "next_truth_release_at"
    ];

    private static readonly string[] SelectionTargetProperties =
    [
        "lemma_statement",
        "lemma_gid_intent",
        "known_dependencies",
        "allowed_assumptions",
        "forbidden_weakenings"
    ];

    private static readonly string[] FailureSemanticsProperties =
    [
        "counterexample_is_useful",
        "missing_prerequisite_is_reportable"
    ];

    private static readonly string[] RequestProperties =
    [
        "schema",
        "request_id",
        "truth_release",
        "paper_context",
        "target",
        "reuse_api",
        "failure_semantics"
    ];

    public static PaperResearchSelectionContent ReadContent(
        ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = Parse(bytes);
        RequireExactProperties(document.RootElement, ContentProperties);
        RequireExactProperties(
            document.RootElement.GetProperty("target"),
            SelectionTargetProperties);
        RequireExactProperties(
            document.RootElement.GetProperty("failure_semantics"),
            FailureSemanticsProperties);
        PaperResearchSelectionContent content =
            JsonSerializer.Deserialize<PaperResearchSelectionContent>(bytes, Options)
            ?? throw new JsonException("Selection content is empty.");
        PaperResearchSelectionService.ValidateContent(content);
        return content;
    }

    public static PaperResearchSelection ReadSelection(ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = Parse(bytes);
        RequireExactProperties(
            document.RootElement,
            ["schema", "selection_id", "selection_content"]);
        JsonElement contentElement =
            document.RootElement.GetProperty("selection_content");
        RequireExactProperties(contentElement, ContentProperties);
        RequireExactProperties(
            contentElement.GetProperty("target"),
            SelectionTargetProperties);
        RequireExactProperties(
            contentElement.GetProperty("failure_semantics"),
            FailureSemanticsProperties);
        PaperResearchSelection selection =
            JsonSerializer.Deserialize<PaperResearchSelection>(bytes, Options)
            ?? throw new JsonException("Selection is empty.");
        PaperResearchSelectionService.Validate(selection);
        return selection;
    }

    public static FormalizationRequest ReadFormalizationRequest(
        ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = Parse(bytes);
        RequireExactProperties(document.RootElement, RequestProperties);
        RequireExactProperties(
            document.RootElement.GetProperty("truth_release"),
            ["source_repo", "source_commit", "source_tree", "release_digest"]);
        RequireExactProperties(
            document.RootElement.GetProperty("paper_context"),
            ["paper_id", "research_candidate_id", "role_in_argument", "why_load_bearing"]);
        RequireExactProperties(
            document.RootElement.GetProperty("target"),
            [
                "preferred_gid",
                "statement",
                "desired_generality",
                "known_dependencies",
                "allowed_assumptions",
                "forbidden_weakenings"
            ]);
        RequireExactProperties(
            document.RootElement.GetProperty("failure_semantics"),
            FailureSemanticsProperties);
        FormalizationRequest request =
            JsonSerializer.Deserialize<FormalizationRequest>(bytes, Options)
            ?? throw new JsonException("Formalization request is empty.");
        PaperResearchSelectionService.Validate(request);
        return request;
    }

    public static byte[] Write(PaperResearchSelection selection)
    {
        PaperResearchSelectionService.Validate(selection);
        return CanonicalJson.Serialize(selection);
    }

    public static byte[] Write(FormalizationRequest request)
    {
        PaperResearchSelectionService.Validate(request);
        return CanonicalJson.Serialize(request);
    }

    private static JsonDocument Parse(ReadOnlySpan<byte> bytes) =>
        JsonDocument.Parse(
            bytes.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });

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
