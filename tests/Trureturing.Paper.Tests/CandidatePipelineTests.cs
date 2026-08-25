using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class CandidatePipelineTests
{
    [Fact]
    public void ProvedBridgeProducesCandidateWhileUnprovedBridgeIsExcluded()
    {
        (PaperTruthIndex truth, PaperIntuitionIndex intuition) = ReadExampleIndexes();

        CandidateProposalArtifacts proposal = Assert.Single(
            CandidatePipeline.Propose(truth, intuition));

        Assert.Equal(
            "Trace and norm compatibility under conjugation",
            proposal.CandidatePaper.Title);
        Assert.Single(proposal.CandidatePaper.GroundedOn.CertifiedNodeIds);
        Assert.Single(proposal.CandidatePaper.GroundedOn.ProvedBridgeRefs);
        Assert.Equal("known", proposal.LiteratureResearch.NoveltyAssessment);
        Assert.All(
            proposal.LiteratureResearch.RelatedWork,
            work => Assert.Equal("verified", work.VerificationStatus));
        Assert.NotEmpty(proposal.CandidateVenues);
    }

    [Fact]
    public void IntuitionBridgeCannotBeEmittedAsACertifiedClaimEvenWhenProved()
    {
        string root = FindRoot();
        PaperTruthReleasePort truthPort = PaperPortJson.ReadTruthReleasePort(
            File.ReadAllBytes(Path.Combine(
                root,
                "Papers/example/paper-truth-release-port.v1.json")));
        PaperTruthIndex truth = PaperTruthIndex.Build(truthPort);
        PaperIntuitionPort intuitionPort = PaperPortJson.ReadIntuitionPort(
            File.ReadAllBytes(Path.Combine(
                root,
                "Papers/example/paper-intuition-port.v1.json")));
        PaperIntuitionCandidatePort bridge = intuitionPort.Candidates[0] with
        {
            // A hostile identity collision must not confer Truth authority.
            ProposalId = truth.Declarations.Single().DeclarationId,
            Status = "proved"
        };
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(
            intuitionPort with { Candidates = [bridge] },
            truth);

        CandidatePaperArtifact paper = Assert.Single(
            CandidatePipeline.Propose(truth, intuition)).CandidatePaper;
        CandidateKeyClaim centralClaim = Assert.Single(
            paper.KeyClaims,
            claim => claim.Claim == bridge.RelationType);

        Assert.Equal("conjectured", centralClaim.Kind);
        Assert.StartsWith("paper-intuition-port.v1@", centralClaim.SourceRef);
        Assert.All(
            paper.KeyClaims.Where(claim => claim.Kind == "certified"),
            claim => Assert.StartsWith(
                "paper-truth-release-port.v1@",
                claim.SourceRef));
        Assert.Throws<ClaimGateException>(
            () => truth.GetDeclaration("advisory/trace-norm-interaction"));
    }

    [Fact]
    public void ProvedBridgeWithUncertifiedInputFailsClosed()
    {
        string root = FindRoot();
        PaperTruthReleasePort truthPort = PaperPortJson.ReadTruthReleasePort(
            File.ReadAllBytes(Path.Combine(
                root,
                "Papers/example/paper-truth-release-port.v1.json")));
        PaperTruthIndex truth = PaperTruthIndex.Build(truthPort);
        var port = new PaperIntuitionPort(
            PaperPortSchemas.IntuitionPort,
            truth.ReleaseDigest,
            [
                new PaperIntuitionCandidatePort(
                    "proved/untrusted-input",
                    "An uncertified bridge",
                    "proved",
                    ["not/in/truth-index"],
                    ["research/output"],
                    [],
                    "A counterexample",
                    null,
                    null)
            ]);
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(port, truth);

        Assert.Throws<ClaimGateException>(
            () => CandidatePipeline.Propose(truth, intuition));
    }

    [Fact]
    public void CheckedCandidateArtifactsValidateAgainstSchemasAndAddresses()
    {
        string root = FindRoot();
        string candidateRoot = Path.Combine(root, "Papers", "candidates");
        string[] paths = Directory.GetFiles(candidateRoot, "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(3, paths.Length);

        foreach (string path in paths)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using JsonDocument artifact = JsonDocument.Parse(bytes);
            string schemaId = artifact.RootElement.GetProperty("schema").GetString()!;
            string schemaPath = Path.Combine(
                root,
                "contracts",
                $"{schemaId}.schema.json");
            using JsonDocument schema = JsonDocument.Parse(
                File.ReadAllBytes(schemaPath));

            AssertValid(artifact.RootElement, schema.RootElement, "$");
            string digest = Convert.ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant();
            Assert.EndsWith($".{digest}.json", path, StringComparison.Ordinal);
        }

        string paperPath = Assert.Single(Directory.GetFiles(
            candidateRoot,
            "candidate-paper.v1.*.json"));
        string journalPath = Assert.Single(Directory.GetFiles(
            candidateRoot,
            "candidate-journal.v1.*.json"));
        using JsonDocument journal = JsonDocument.Parse(
            File.ReadAllBytes(journalPath));
        string expectedPaperReference = "sha256:" + Path.GetFileName(paperPath)
            .Split('.')[2];
        Assert.Equal(
            expectedPaperReference,
            journal.RootElement.GetProperty("candidate_paper_ref").GetString());
    }

    [Fact]
    public void CanonicalSerializationIsByteReproducibleAndSortsObjectKeys()
    {
        (PaperTruthIndex truth, PaperIntuitionIndex intuition) = ReadExampleIndexes();
        CandidatePaperArtifact paper = Assert.Single(
            CandidatePipeline.Propose(truth, intuition)).CandidatePaper;

        byte[] first = CanonicalJson.Serialize(paper);
        byte[] second = CanonicalJson.Serialize(paper);

        Assert.Equal(first, second);
        Assert.StartsWith("{\"abstract\":", System.Text.Encoding.UTF8.GetString(first));
        Assert.DoesNotContain((byte)'\n', first);
    }

    private static (PaperTruthIndex Truth, PaperIntuitionIndex Intuition)
        ReadExampleIndexes()
    {
        string root = FindRoot();
        PaperTruthIndex truth = PaperTruthIndex.Build(
            PaperPortJson.ReadTruthReleasePort(File.ReadAllBytes(Path.Combine(
                root,
                "Papers/example/paper-truth-release-port.v1.json"))));
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(
            PaperPortJson.ReadIntuitionPort(File.ReadAllBytes(Path.Combine(
                root,
                "Papers/example/paper-intuition-port.v1.json"))),
            truth);
        return (truth, intuition);
    }

    private static void AssertValid(
        JsonElement value,
        JsonElement schema,
        string path)
    {
        if (schema.TryGetProperty("type", out JsonElement type))
        {
            Assert.True(IsType(value, type.GetString()!),
                $"{path} has JSON kind {value.ValueKind}, expected {type.GetString()}.");
        }

        if (schema.TryGetProperty("const", out JsonElement constant))
        {
            Assert.True(JsonElement.DeepEquals(value, constant),
                $"{path} does not equal its schema const.");
        }

        if (schema.TryGetProperty("enum", out JsonElement choices))
        {
            Assert.Contains(
                choices.EnumerateArray(),
                choice => JsonElement.DeepEquals(value, choice));
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            JsonElement properties = schema.GetProperty("properties");
            if (schema.TryGetProperty("required", out JsonElement required))
            {
                foreach (JsonElement name in required.EnumerateArray())
                {
                    Assert.True(value.TryGetProperty(name.GetString()!, out _),
                        $"{path} is missing required property {name.GetString()}.");
                }
            }

            if (schema.TryGetProperty(
                    "additionalProperties",
                    out JsonElement additionalProperties)
                && additionalProperties.ValueKind == JsonValueKind.False)
            {
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    Assert.True(properties.TryGetProperty(property.Name, out _),
                        $"{path} contains unknown property {property.Name}.");
                }
            }

            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (properties.TryGetProperty(
                        property.Name,
                        out JsonElement propertySchema))
                {
                    AssertValid(
                        property.Value,
                        propertySchema,
                        $"{path}.{property.Name}");
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            JsonElement[] items = value.EnumerateArray().ToArray();
            if (schema.TryGetProperty("minItems", out JsonElement minItems))
            {
                Assert.True(items.Length >= minItems.GetInt32(),
                    $"{path} has too few items.");
            }

            if (schema.TryGetProperty("uniqueItems", out JsonElement uniqueItems)
                && uniqueItems.GetBoolean())
            {
                Assert.Equal(
                    items.Length,
                    items.Select(item => item.GetRawText())
                        .Distinct(StringComparer.Ordinal)
                        .Count());
            }

            if (schema.TryGetProperty("items", out JsonElement itemSchema))
            {
                for (var index = 0; index < items.Length; index++)
                {
                    AssertValid(items[index], itemSchema, $"{path}[{index}]");
                }
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            if (schema.TryGetProperty("minLength", out JsonElement minLength))
            {
                Assert.True(text.Length >= minLength.GetInt32(),
                    $"{path} is shorter than minLength.");
            }

            if (schema.TryGetProperty("pattern", out JsonElement pattern))
            {
                Assert.Matches(new Regex(pattern.GetString()!), text);
            }

            if (schema.TryGetProperty("format", out JsonElement format)
                && format.GetString() == "uri")
            {
                Assert.True(Uri.TryCreate(text, UriKind.Absolute, out _),
                    $"{path} is not an absolute URI.");
            }
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            decimal number = value.GetDecimal();
            if (schema.TryGetProperty("minimum", out JsonElement minimum))
            {
                Assert.True(number >= minimum.GetDecimal(),
                    $"{path} is below minimum.");
            }
            if (schema.TryGetProperty("maximum", out JsonElement maximum))
            {
                Assert.True(number <= maximum.GetDecimal(),
                    $"{path} is above maximum.");
            }
        }
    }

    private static bool IsType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => throw new InvalidOperationException($"Unsupported schema type {type}.")
    };

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "Trureturing.Paper.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
