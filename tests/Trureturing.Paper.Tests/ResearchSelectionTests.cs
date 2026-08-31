using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class ResearchSelectionTests
{
    [Fact]
    public void SelectionProducesCanonicalFormalizeRequest()
    {
        PaperResearchInput researchInput = ValidResearchInput();
        PaperResearchSelectionContent content = ValidContent(researchInput);
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(content);
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(
                selection,
                researchInput);

        Assert.Equal(
            CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(selection.SelectionContent)),
            selection.SelectionId);
        Assert.Equal("formalization-request.v1", request.Schema);
        Assert.Equal(
            PaperResearchSelectionService.TruthSourceRepository,
            request.TruthRelease.SourceRepo);
        Assert.Equal(researchInput.SourceCommit, request.TruthRelease.SourceCommit);
        Assert.Equal(researchInput.SourceTree, request.TruthRelease.SourceTree);
        Assert.Equal(
            researchInput.TruthReleaseDigest,
            request.TruthRelease.ReleaseDigest);
        Assert.Equal(content.PaperId, request.PaperContext.PaperId);
        Assert.Equal(
            content.CandidatePaperRef,
            request.PaperContext.ResearchCandidateId);
        Assert.Equal(
            content.RoleInArgument,
            request.PaperContext.RoleInArgument);
        Assert.Equal(
            content.ExpectedContribution,
            request.PaperContext.WhyLoadBearing);
        Assert.Equal(
            content.Target.LemmaGidIntent,
            request.Target.PreferredGid);
        Assert.Equal(content.Target.LemmaStatement, request.Target.Statement);
        Assert.Equal(content.ClaimBoundary, request.Target.DesiredGenerality);
        Assert.Equal(
            content.Target.KnownDependencies.ToArray(),
            request.Target.KnownDependencies.ToArray());
        Assert.Equal(
            content.Target.AllowedAssumptions.ToArray(),
            request.Target.AllowedAssumptions.ToArray());
        Assert.Equal(
            content.Target.ForbiddenWeakenings.ToArray(),
            request.Target.ForbiddenWeakenings!.ToArray());
        Assert.Equal(
            content.ReuseApi.ToArray(),
            request.ReuseApi.ToArray());
        Assert.Equal(
            content.FailureSemantics.CounterexampleIsUseful,
            request.FailureSemantics.CounterexampleIsUseful);
        Assert.Equal(
            content.FailureSemantics.MissingPrerequisiteIsReportable,
            request.FailureSemantics.MissingPrerequisiteIsReportable);
        Assert.Equal(
            PaperResearchSelectionService.ComputeFormalizationRequestId(request),
            request.RequestId);

        byte[] bytes = PaperResearchSelectionJson.Write(request);
        using JsonDocument document = JsonDocument.Parse(bytes);
        Assert.Equal(
            new[]
            {
                "failure_semantics",
                "paper_context",
                "request_id",
                "reuse_api",
                "schema",
                "target",
                "truth_release"
            },
            document.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());

        FormalizationRequest roundTrip =
            PaperResearchSelectionJson.ReadFormalizationRequest(bytes);
        Assert.True(
            bytes.SequenceEqual(
                PaperResearchSelectionJson.Write(roundTrip)));
    }

    [Fact]
    public void SuppliedResearchInputMustMatchSelectionReference()
    {
        PaperResearchInput researchInput = ValidResearchInput();
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(ValidContent(researchInput));
        PaperResearchInput substituted = researchInput with
        {
            SourceCommit = new string('3', 40)
        };

        Assert.Throws<InvalidDataException>(
            () => PaperResearchSelectionService.BuildFormalizationRequest(
                selection,
                substituted));
    }

    [Fact]
    public void SelectionAndResearchInputMustShareOneExactState()
    {
        PaperResearchInput researchInput = ValidResearchInput() with
        {
            TruthReleaseDigest = Digest('9')
        };
        PaperResearchSelectionContent content = ValidContent(researchInput) with
        {
            TruthReleaseDigest = Digest('a')
        };
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(content);

        Assert.Throws<InvalidDataException>(
            () => PaperResearchSelectionService.BuildFormalizationRequest(
                selection,
                researchInput));
    }

    [Fact]
    public void TamperedSelectionIdentityFailsClosed()
    {
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(
                ValidContent(ValidResearchInput())) with
            {
                SelectionId = Digest('b')
            };

        Assert.Throws<InvalidDataException>(
            () => PaperResearchSelectionService.Validate(selection));
    }

    [Fact]
    public void TamperedRequestIdentityFailsClosed()
    {
        PaperResearchInput researchInput = ValidResearchInput();
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(
                PaperResearchSelectionService.Create(
                    ValidContent(researchInput)),
                researchInput) with
            {
                RequestId = Digest('b')
            };

        Assert.Throws<InvalidDataException>(
            () => PaperResearchSelectionService.Validate(request));
    }

    [Fact]
    public void ReaderRejectsUnknownDuplicateAndLegacyProperties()
    {
        byte[] unknown = Encoding.UTF8.GetBytes(
            "{\"truth_release_digest\":\"" + Digest('a') +
            "\",\"extra\":true}");
        Assert.Throws<JsonException>(
            () => PaperResearchSelectionJson.ReadContent(unknown));

        byte[] duplicate = Encoding.UTF8.GetBytes(
            "{\"truth_release_digest\":\"" + Digest('a') +
            "\",\"truth_release_digest\":\"" + Digest('b') + "\"}");
        Assert.Throws<JsonException>(
            () => PaperResearchSelectionJson.ReadContent(duplicate));

        byte[] legacyRequest = Encoding.UTF8.GetBytes(
            "{\"schema_version\":\"formalization-request.v1\"," +
            "\"request_id\":\"" + Digest('a') + "\"," +
            "\"request_content\":{}}");
        Assert.Throws<JsonException>(
            () => PaperResearchSelectionJson.ReadFormalizationRequest(
                legacyRequest));
    }

    [Fact]
    public void NextReleaseMustFollowSelection()
    {
        PaperResearchSelectionContent content =
            ValidContent(ValidResearchInput()) with
            {
                NextTruthReleaseAt = "2026-08-29T08:59:59Z"
            };

        Assert.Throws<InvalidDataException>(
            () => PaperResearchSelectionService.Create(content));
    }

    [Fact]
    public void CanonicalFormalizeSchemaBytesMatchPinnedDigest()
    {
        string root = FindRepositoryRoot();
        string schemaPath = Path.Combine(
            root,
            "contracts",
            "formalization-request.v1.schema.json");
        string pinPath = Path.Combine(
            root,
            "contracts",
            "formalization-request.v1.schema.sha256");
        string actual = "sha256:" + Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(schemaPath)));
        string pin = File.ReadAllText(pinPath).Trim();

        Assert.Equal(
            "sha256:f8d5f1da9cd2375a45ccda033e579ddbef90f6e1586221f595f709b14bf39cac",
            actual);
        Assert.StartsWith(
            actual + "  formalization-request.v1.schema.json",
            pin,
            StringComparison.Ordinal);
    }

    private static PaperResearchInput ValidResearchInput() => new(
        PaperResearchInputSchemas.ResearchInput,
        Digest('a'),
        Digest('b'),
        new string('1', 40),
        new string('2', 40),
        Digest('c'),
        Digest('d'),
        Digest('e'));

    private static PaperResearchSelectionContent ValidContent(
        PaperResearchInput researchInput) => new(
        researchInput.TruthReleaseDigest,
        researchInput.TopologyDigest,
        CanonicalJson.Sha256Reference(
            CanonicalJson.Serialize(researchInput)),
        Digest('f'),
        Digest('8'),
        Digest('7'),
        "paper:trace-norm-compatibility",
        "Supplies the load-bearing bridge used by the main classification theorem.",
        new PaperResearchTarget(
            "forall x, x = x",
            "D5/S0/Test.reflexive",
            ["D5/S0/Carrier/TraceConjugation.trace_conj"],
            ["propext"],
            ["Do not replace the theorem by a single closed test case."]),
        "Prove the statement for every term in the declared carrier.",
        "A well-typed term for which the proposed equality fails.",
        "The bridge closes the only uncertified step in the paper's main argument.",
        ["Eq.refl", "D5/S0/Carrier/TraceConjugation.trace_conj"],
        new PaperResearchFailureSemantics(
            CounterexampleIsUseful: true,
            MissingPrerequisiteIsReportable: true),
        Digest('6'),
        "AlyciaBHZ",
        "2026-08-29T09:00:00Z",
        "2026-08-31T09:00:00Z");

    private static string Digest(char value) =>
        "sha256:" + new string(value, 64);

    private static string FindRepositoryRoot()
    {
        foreach (DirectoryInfo start in new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory),
        })
        {
            for (DirectoryInfo? current = start;
                 current is not null;
                 current = current.Parent)
            {
                if (File.Exists(Path.Combine(
                        current.FullName,
                        "Trureturing.Paper.slnx")))
                {
                    return current.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the trureturing-paper repository root.");
    }
}
