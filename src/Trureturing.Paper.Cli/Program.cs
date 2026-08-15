using System.Collections.Immutable;
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
            var bytes = new PaperAssemblyService(ports, ports, ports, ports, ports, ports).Assemble(recipe);
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
    IEvidencePort,
    ITruthGraphPort
{
    private readonly string _root;

    public FrozenBundleFilePorts(string root) =>
        _root = Directory.Exists(root)
            ? Path.GetFullPath(root)
            : throw new DirectoryNotFoundException($"Frozen bundle directory '{root}' does not exist.");

    public BlessedSnapshotEnvelope Read() => new(
        File.ReadAllBytes(Path.Combine(_root, "source-snapshot.v1.json")),
        File.ReadAllText(Path.Combine(_root, "source-snapshot.v1.sha256")).Trim());

    // Required in the production path: the assembler verifies this against the snapshot's
    // truth_graph_sha256 and requires every claimed declaration to be a closed node in it, so a
    // tampered frozen-truth.v1.json cannot impersonate a frozen theorem.
    public TruthGraphEnvelope ReadTruthGraph()
    {
        var path = Path.Combine(_root, "truth-graph.v1.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Frozen bundle is missing truth-graph.v1.json; the closed-theorem binding cannot be verified.",
                path);
        }
        return new TruthGraphEnvelope(File.ReadAllBytes(path));
    }

    public IReadOnlyList<FrozenDeclaration> ReadDeclarations()
    {
        var envelope = Program.ReadJson<FrozenTruthEnvelopeDto>(Path.Combine(_root, "frozen-truth.v1.json"));
        return envelope.Declarations.Select(static value => new FrozenDeclaration(
            value.DeclarationGid,
            value.Status,
            // The statement is rendered from the frozen ledger's own structured AST, so the
            // paper reproduces the theorem faithfully (LatexWriter renders FunctionCall nodes as
            // \operatorname{...}). The ledger's latex_statement is that same render; the paper
            // never re-interprets or degrades the statement to opaque left = right symbols.
            ToFormula(value.StatementAst),
            value.TruthAnchor,
            value.LeanReportSha256,
            value.DeclaredAxioms,
            value.AllowedAxioms)).ToArray();
    }

    // statement_ast -> Scribe Formula. Mirrors the reader the walking-skeleton tests use, and is
    // kept in one place so the CLI and tests consume the single frozen-truth envelope format.
    private static Formula ToFormula(FormulaAstDto value) => value.Kind switch
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
        _ => throw new ArgumentException($"Unsupported frozen-truth formula node '{value.Kind}'.")
    };

    public IReadOnlyList<BlueprintBlock> ReadBlocks()
    {
        var envelope = Program.ReadJson<BlueprintEnvelopeDto>(Path.Combine(_root, "blueprints.v1.json"));
        return envelope.Blocks.Select(static block => new BlueprintBlock(
            block.DescribeAnchor,
            block.DeclarationGid,
            block.TruthAnchor,
            block.Narrative)).ToArray();
    }

    public IReadOnlyList<Citation> ReadCitations() =>
        ReadOptional<Citation>("citations.v1.json");

    public IReadOnlyList<EvidenceItem> ReadEvidence() =>
        ReadOptional<EvidenceItem>("evidence.v1.json");

    private IReadOnlyList<T> ReadOptional<T>(string name)
    {
        var path = Path.Combine(_root, name);
        return File.Exists(path) ? Program.ReadJson<T[]>(path) : [];
    }

    private sealed record FrozenTruthEnvelopeDto(
        string Schema,
        FrozenTruthDeclarationDto[] Declarations);

    private sealed record FrozenTruthDeclarationDto(
        string DeclarationGid,
        string Status,
        string TruthAnchor,
        string LeanReportSha256,
        string[] DeclaredAxioms,
        string[] AllowedAxioms,
        FormulaAstDto StatementAst);

    private sealed record FormulaAstDto(
        string Kind,
        string? Name,
        string? Operator,
        FormulaAstDto? Left,
        FormulaAstDto? Right,
        FormulaAstDto[]? Arguments);

    private sealed record BlueprintEnvelopeDto(
        string Schema,
        BlueprintBlockDto[] Blocks);

    private sealed record BlueprintBlockDto(
        string DescribeAnchor,
        string DeclarationGid,
        string TruthAnchor,
        string Narrative);
}
