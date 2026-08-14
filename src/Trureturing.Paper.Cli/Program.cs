using System.Text.Json;
using StrataLint.Scribe;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            var recipe = ReadJson<PaperRecipe>(options.RecipePath);
            var ports = new FrozenBundleFilePorts(options.BundleDirectory);
            var bytes = new PaperAssemblyService(ports, ports, ports, ports, ports).Assemble(recipe);
            var outputPath = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, bytes);
            Console.WriteLine(outputPath);
            return 0;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or ClaimGateException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    internal static T ReadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), JsonOptions)
        ?? throw new JsonException($"'{path}' is empty or does not match the expected contract.");

    private sealed record CliOptions(string RecipePath, string BundleDirectory, string OutputPath)
    {
        public static CliOptions Parse(string[] args)
        {
            if (args.Length != 7 || !string.Equals(args[0], "assemble", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Usage: trureturing-paper assemble --recipe <recipe.json> --frozen-bundle <directory> --output <paper.tex>");
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 1; index < args.Length; index += 2)
            {
                if (!values.TryAdd(args[index], args[index + 1]))
                {
                    throw new ArgumentException($"Duplicate CLI option '{args[index]}'.");
                }
            }
            if (!values.TryGetValue("--recipe", out var recipe)
                || !values.TryGetValue("--frozen-bundle", out var bundle)
                || !values.TryGetValue("--output", out var output)
                || values.Count != 3)
            {
                throw new ArgumentException("CLI options are incomplete or unknown.");
            }
            return new CliOptions(Path.GetFullPath(recipe), Path.GetFullPath(bundle), output);
        }
    }
}

internal sealed class FrozenBundleFilePorts :
    IBlessedSnapshotPort,
    IFrozenTruthPort,
    IBlueprintPort,
    ICitationPort,
    IEvidencePort
{
    private readonly string _root;

    public FrozenBundleFilePorts(string root) =>
        _root = Directory.Exists(root)
            ? Path.GetFullPath(root)
            : throw new DirectoryNotFoundException($"Frozen bundle directory '{root}' does not exist.");

    public BlessedSnapshotEnvelope Read() => new(
        File.ReadAllBytes(Path.Combine(_root, "source-snapshot.v1.json")),
        File.ReadAllText(Path.Combine(_root, "source-snapshot.v1.sha256")).Trim());

    public IReadOnlyList<FrozenDeclaration> ReadDeclarations()
    {
        var values = Program.ReadJson<FrozenTruthDto[]>(Path.Combine(_root, "frozen-truth.v1.json"));
        return values.Select(static value => new FrozenDeclaration(
            value.DeclarationGid,
            value.Status,
            new Formula.Relation(
                new Formula.Symbol(FormulaIdentifier.Create(value.StatementLeft)),
                FormulaRelationOperator.Equal,
                new Formula.Symbol(FormulaIdentifier.Create(value.StatementRight))),
            value.TruthAnchor,
            value.LeanReportSha256,
            value.DeclaredAxioms,
            value.AllowedAxioms)).ToArray();
    }

    public IReadOnlyList<BlueprintBlock> ReadBlocks() =>
        Program.ReadJson<BlueprintBlock[]>(Path.Combine(_root, "blueprints.v1.json"));

    public IReadOnlyList<Citation> ReadCitations() =>
        ReadOptional<Citation>("citations.v1.json");

    public IReadOnlyList<EvidenceItem> ReadEvidence() =>
        ReadOptional<EvidenceItem>("evidence.v1.json");

    private IReadOnlyList<T> ReadOptional<T>(string name)
    {
        var path = Path.Combine(_root, name);
        return File.Exists(path) ? Program.ReadJson<T[]>(path) : [];
    }

    private sealed record FrozenTruthDto(
        string DeclarationGid,
        string Status,
        string StatementLeft,
        string StatementRight,
        string TruthAnchor,
        string LeanReportSha256,
        string[] DeclaredAxioms,
        string[] AllowedAxioms);
}
