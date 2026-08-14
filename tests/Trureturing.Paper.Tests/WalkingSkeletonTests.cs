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

    [Fact(Skip = "TBD: real blessed source-snapshot.v1 + truth-graph.v1.json artifact not yet produced on dev")]
    public void Real_blessed_snapshot_acceptance() { }

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
