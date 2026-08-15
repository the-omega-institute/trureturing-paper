using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StrataLint.Scribe;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class WalkingSkeletonTests
{
    [Fact]
    public void Assemble_is_byte_reproducible_across_directories_locales_and_timezones()
    {
        var (recipe, inputs) = Fixture.Load();
        var firstDirectory = Directory.CreateTempSubdirectory("paper-a-").FullName;
        var secondDirectory = Directory.CreateTempSubdirectory("paper-b-").FullName;

        var first = AssembleInEnvironment(recipe, inputs, firstDirectory, "en-US", "UTC");
        var second = AssembleInEnvironment(recipe, inputs, secondDirectory, "tr-TR", "Asia/Tokyo");

        Assert.Equal(first, second);
        Assert.Equal(Sha256(first), Sha256(second));
    }

    [Fact]
    public void Claim_gate_rejects_unfrozen_claim_despite_machine_checked_prose()
    {
        var (recipe, inputs) = Fixture.Load();
        var validPaper = Encoding.UTF8.GetString(PaperAssembler.Assemble(recipe, inputs));
        Assert.Contains("SYNTH.THEOREM.IDENTITY", validPaper, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTH.THEOREM.FAKE", validPaper, StringComparison.Ordinal);

        var injectedRecipe = recipe with
        {
            Claims = recipe.Claims.Concat([
                new RecipeClaim("SYNTH.THEOREM.FAKE", "describe:fake-machine-checked")
            ]).ToArray()
        };
        var injectedInputs = inputs with
        {
            BlueprintBlocks = inputs.BlueprintBlocks.Concat([
                new BlueprintBlock(
                    "describe:fake-machine-checked",
                    "SYNTH.THEOREM.FAKE",
                    "truth:fake",
                    "Machine-checked according to narrative text only.")
            ]).ToArray()
        };

        Assert.Throws<ClaimGateException>(() => PaperAssembler.Assemble(injectedRecipe, injectedInputs));
    }

    [Fact]
    public void Real_blessed_snapshot_acceptance()
    {
        var fixture = RealFixture.Load();

        Assert.Equal("90059ebbb6c1d61da93690723af581145b88bad1", fixture.Snapshot.SourceCommit);
        Assert.Equal("AlyicaBHZ", fixture.Snapshot.BlessedBy);
        Assert.Equal(669, fixture.TruthGraph.Edges.Count);
        Assert.Equal(["digestion"], fixture.TruthGraph.DeferredLayers);
        Assert.Equal(fixture.Snapshot.TruthGraphSha256, Sha256(fixture.TruthGraphBytes));
        Assert.Equal(
            $"sha256:{fixture.Snapshot.LeanReportSha256}",
            fixture.TruthGraph.Provenance.LeanReportDigest);
        Assert.Equal(
            fixture.Snapshot.TruthRootSha256,
            fixture.TruthGraph.Provenance.TruthRootSha256);
        Assert.Equal(
            fixture.Snapshot.RepositorySnapshotDigest.Replace(
                "sha256-", "sha256:", StringComparison.Ordinal),
            fixture.TruthGraph.Provenance.SnapshotContentDigest);

        var firstDirectory = Directory.CreateTempSubdirectory("real-paper-a-").FullName;
        var secondDirectory = Directory.CreateTempSubdirectory("real-paper-b-").FullName;
        var first = AssembleInEnvironment(
            fixture.Recipe, fixture.Inputs, firstDirectory, "en-US", "UTC");
        var second = AssembleInEnvironment(
            fixture.Recipe, fixture.Inputs, secondDirectory, "tr-TR", "Asia/Tokyo");

        Assert.Equal(first, second);
        Assert.Equal(Sha256(first), Sha256(second));
        var paper = Encoding.UTF8.GetString(first);
        Assert.Contains(fixture.DeclarationGid, paper, StringComparison.Ordinal);
        Assert.Contains(fixture.LatexStatement, paper, StringComparison.Ordinal);

        var fakeGid = "D5/S0/Carrier/TraceConjugation.forged_not_frozen";
        var injectedRecipe = fixture.Recipe with
        {
            Claims = fixture.Recipe.Claims.Concat([
                new RecipeClaim(fakeGid, "describe:forged-not-frozen")
            ]).ToArray()
        };
        var genuineDeclaration = Assert.Single(fixture.Inputs.Declarations);
        var injectedInputs = fixture.Inputs with
        {
            Declarations = fixture.Inputs.Declarations.Concat([
                genuineDeclaration with { DeclarationGid = fakeGid }
            ]).ToArray(),
            BlueprintBlocks = fixture.Inputs.BlueprintBlocks.Concat([
                new BlueprintBlock(
                    "describe:forged-not-frozen",
                    fakeGid,
                    genuineDeclaration.TruthAnchor,
                    "This prose cannot manufacture a frozen theorem.")
            ]).ToArray()
        };

        Assert.Throws<ClaimGateException>(() =>
            PaperAssembler.Assemble(injectedRecipe, injectedInputs));
    }

    private static byte[] AssembleInEnvironment(
        PaperRecipe recipe,
        FrozenInputs inputs,
        string directory,
        string cultureName,
        string timezone)
    {
        var originalDirectory = Environment.CurrentDirectory;
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalTimezone = Environment.GetEnvironmentVariable("TZ");
        try
        {
            Environment.CurrentDirectory = directory;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            Environment.SetEnvironmentVariable("TZ", timezone);
            TimeZoneInfo.ClearCachedData();
            var bytes = PaperAssembler.Assemble(recipe, inputs);
            File.WriteAllBytes(Path.Combine(directory, "paper.tex"), bytes);
            return bytes;
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            Environment.SetEnvironmentVariable("TZ", originalTimezone);
            TimeZoneInfo.ClearCachedData();
        }
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record RealFixtureResult(
    PaperRecipe Recipe,
    FrozenInputs Inputs,
    SourceSnapshot Snapshot,
    FrozenTruthGraph TruthGraph,
    byte[] TruthGraphBytes,
    string DeclarationGid,
    string LatexStatement);

internal static class RealFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static RealFixtureResult Load()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "fixtures-real");
        var snapshotBytes = File.ReadAllBytes(Path.Combine(
            root, "source-snapshot.v1.blessed.json"));
        var snapshotDigest = File.ReadAllText(Path.Combine(
            root, "source-snapshot.v1.sha256")).Trim();
        var snapshotEnvelope = new BlessedSnapshotEnvelope(snapshotBytes, snapshotDigest);
        var snapshot = SourceSnapshotReader.ReadAndVerify(snapshotEnvelope);

        var truthGraphBytes = File.ReadAllBytes(Path.Combine(root, "truth-graph.v1.json"));
        var truthGraphEnvelope = new TruthGraphEnvelope(truthGraphBytes);
        var truthGraph = TruthGraphReader.ReadAndVerify(truthGraphEnvelope, snapshot);

        var recipe = Deserialize<PaperRecipe>(root, "trace-conjugation.recipe.v1.json");
        var truthEnvelope = Deserialize<RealFrozenTruthEnvelope>(root, "frozen-truth.v1.json");
        Assert.Equal("frozen-truth.v1", truthEnvelope.Schema);
        var truth = Assert.Single(truthEnvelope.Declarations);
        var statement = ToFormula(truth.StatementAst);
        Assert.Equal(truth.LatexStatement, LatexWriter.WriteStatement(statement));
        Assert.Equal("D5.S0.Carrier.trace_conj", truth.LeanDeclarationName);
        Assert.Equal(".lake/build/stratalint/raw-lean-report.json", truth.AxiomSource);

        var blueprintEnvelope = Deserialize<RealBlueprintEnvelope>(root, "blueprints.v1.json");
        Assert.Equal("blueprints.v1", blueprintEnvelope.Schema);
        var blueprint = Assert.Single(blueprintEnvelope.Blocks);
        Assert.Equal(truth.LatexStatement, blueprint.LatexStatement);

        var binding = TruthGraphReader.RequireClosedTheorem(
            truthGraph,
            truth.DeclarationGid,
            blueprint.DescribeAnchor);
        Assert.Equal(truth.DescribeId, binding.DescribeId);
        Assert.Equal(truth.DocumentGid, binding.DocumentGid);
        Assert.Equal(blueprint.DocumentRepoPath, binding.DocumentRepoPath);
        Assert.Equal(blueprint.FormalTruthRepoPath, binding.FormalTruthRepoPath);
        Assert.Equal(truth.DocumentGid, truth.TruthAnchor);
        Assert.Equal(snapshot.LeanReportSha256, truth.LeanReportSha256);

        var declaration = new FrozenDeclaration(
            truth.DeclarationGid,
            truth.Status,
            statement,
            truth.TruthAnchor,
            truth.LeanReportSha256,
            truth.DeclaredAxioms,
            truth.AllowedAxioms);
        var blueprintBlock = new BlueprintBlock(
            blueprint.DescribeAnchor,
            blueprint.DeclarationGid,
            blueprint.TruthAnchor,
            blueprint.Narrative);
        var inputs = new FrozenInputs(
            snapshotEnvelope,
            [declaration],
            [blueprintBlock],
            [],
            [],
            truthGraphEnvelope);
        return new RealFixtureResult(
            recipe,
            inputs,
            snapshot,
            truthGraph,
            truthGraphBytes,
            truth.DeclarationGid,
            truth.LatexStatement);
    }

    private static T Deserialize<T>(string root, string fileName) where T : class =>
        JsonSerializer.Deserialize<T>(
            File.ReadAllBytes(Path.Combine(root, fileName)), JsonOptions)
        ?? throw new InvalidOperationException($"Real fixture '{fileName}' is invalid.");

    private static Formula ToFormula(FormulaFixture value) => value.Kind switch
    {
        "symbol" => new Formula.Symbol(FormulaIdentifier.Create(value.Name!)),
        "function" => new Formula.FunctionCall(
            FormulaIdentifier.Create(value.Name!),
            value.Arguments!.Select(ToFormula).ToImmutableArray()),
        "relation" when string.Equals(value.Operator, "equal", StringComparison.Ordinal) =>
            new Formula.Relation(
                ToFormula(value.Left!),
                FormulaRelationOperator.Equal,
                ToFormula(value.Right!)),
        _ => throw new InvalidOperationException(
            $"Unsupported real fixture formula node '{value.Kind}'.")
    };

    private sealed record RealFrozenTruthEnvelope(
        string Schema,
        RealTruthFixture[] Declarations);

    private sealed record RealTruthFixture(
        string DeclarationGid,
        string LeanDeclarationName,
        string DocumentGid,
        string DescribeId,
        string Status,
        string TruthAnchor,
        string LeanReportSha256,
        string[] DeclaredAxioms,
        string[] AllowedAxioms,
        string AxiomSource,
        string LatexStatement,
        FormulaFixture StatementAst);

    private sealed record FormulaFixture(
        string Kind,
        string? Name,
        string? Operator,
        FormulaFixture[]? Arguments,
        FormulaFixture? Left,
        FormulaFixture? Right);

    private sealed record RealBlueprintEnvelope(
        string Schema,
        RealBlueprintFixture[] Blocks);

    private sealed record RealBlueprintFixture(
        string DescribeAnchor,
        string DeclarationGid,
        string TruthAnchor,
        string DocumentRepoPath,
        string FormalTruthRepoPath,
        string LatexStatement,
        string Narrative);
}

internal static class Fixture
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static (PaperRecipe Recipe, FrozenInputs Inputs) Load()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "fixtures");
        var recipe = JsonSerializer.Deserialize<PaperRecipe>(
            File.ReadAllBytes(Path.Combine(root, "synthetic-minimal.recipe.v1.json")), JsonOptions)!;
        var truths = JsonSerializer.Deserialize<TruthFixture[]>(
            File.ReadAllBytes(Path.Combine(root, "frozen-truth.v1.json")), JsonOptions)!;
        var truth = Assert.Single(truths);
        var blueprints = JsonSerializer.Deserialize<BlueprintBlock[]>(
            File.ReadAllBytes(Path.Combine(root, "blueprints.v1.json")), JsonOptions)!;
        var statement = new Formula.Relation(
            new Formula.Symbol(FormulaIdentifier.Create(truth.StatementLeft)),
            FormulaRelationOperator.Equal,
            new Formula.Symbol(FormulaIdentifier.Create(truth.StatementRight)));
        var declaration = new FrozenDeclaration(
            truth.DeclarationGid,
            truth.Status,
            statement,
            truth.TruthAnchor,
            truth.LeanReportSha256,
            truth.DeclaredAxioms,
            truth.AllowedAxioms);
        var snapshotBytes = File.ReadAllBytes(Path.Combine(root, "source-snapshot.v1.json"));
        var digest = File.ReadAllText(Path.Combine(root, "source-snapshot.v1.sha256")).Trim();
        return (recipe, new FrozenInputs(
            new BlessedSnapshotEnvelope(snapshotBytes, digest),
            [declaration],
            blueprints,
            [],
            []));
    }

    private sealed record TruthFixture(
        string DeclarationGid,
        string Status,
        string StatementLeft,
        string StatementRight,
        string TruthAnchor,
        string LeanReportSha256,
        string[] DeclaredAxioms,
        string[] AllowedAxioms);
}
