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
            return args.FirstOrDefault() switch
            {
                "assemble" => Assemble(args),
                "emit-local-ports" => EmitLocalPorts(args),
                "assemble-example" => AssembleExample(args),
                _ => throw new ArgumentException(Usage)
            };
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

    private static int Assemble(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "assemble",
            "--recipe",
            "--frozen-bundle",
            "--output");
        var recipe = ReadJson<PaperRecipe>(values["--recipe"]);
        var ports = new FrozenBundleFilePorts(values["--frozen-bundle"]);
        byte[] bytes = new PaperAssemblyService(ports, ports, ports, ports, ports, ports)
            .Assemble(recipe);
        WriteFile(values["--output"], bytes);
        return 0;
    }

    private static int EmitLocalPorts(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "emit-local-ports",
            "--frozen-bundle",
            "--output");
        LocalDevRelease release = LocalDevTruthReleaseAdapter.Read(
            values["--frozen-bundle"]);
        WritePorts(values["--output"], release);
        return 0;
    }

    private static int AssembleExample(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "assemble-example",
            "--frozen-bundle",
            "--output-root");
        string outputRoot = Path.GetFullPath(values["--output-root"]);
        LocalDevRelease release = LocalDevTruthReleaseAdapter.Read(
            values["--frozen-bundle"]);
        string exampleRoot = Path.Combine(outputRoot, "Papers", "example");
        WritePorts(exampleRoot, release);

        // Cross the serialized contract boundary exactly as a real upstream adapter would.
        PaperTruthReleasePort truthPort = PaperPortJson.ReadTruthReleasePort(
            File.ReadAllBytes(Path.Combine(exampleRoot, "paper-truth-release-port.v1.json")));
        PaperIntuitionPort intuitionPort = PaperPortJson.ReadIntuitionPort(
            File.ReadAllBytes(Path.Combine(exampleRoot, "paper-intuition-port.v1.json")));
        byte[] latex = ExamplePaperAssembler.Assemble(
            truthPort,
            intuitionPort,
            release.FrozenInputs);
        WriteFile(Path.Combine(exampleRoot, "paper.tex"), latex);
        return 0;
    }

    private static void WritePorts(string outputDirectory, LocalDevRelease release)
    {
        string root = Path.GetFullPath(outputDirectory);
        WriteFile(
            Path.Combine(root, "paper-truth-release-port.v1.json"),
            PaperPortJson.Write(release.TruthPort));
        WriteFile(
            Path.Combine(root, "paper-intuition-port.v1.json"),
            PaperPortJson.Write(release.IntuitionPort));
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        string outputPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, bytes);
        Console.WriteLine(outputPath);
    }

    private static Dictionary<string, string> ParseValues(
        string[] args,
        string verb,
        params string[] expectedOptions)
    {
        if (args.Length != 1 + (expectedOptions.Length * 2)
            || !string.Equals(args[0], verb, StringComparison.Ordinal))
        {
            throw new ArgumentException(Usage);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Duplicate CLI option '{args[index]}'.");
            }
        }

        if (values.Count != expectedOptions.Length
            || expectedOptions.Any(option => !values.ContainsKey(option)))
        {
            throw new ArgumentException("CLI options are incomplete or unknown.\n" + Usage);
        }

        return values;
    }

    private const string Usage = """
Usage:
  trureturing-paper assemble --recipe <recipe.json> --frozen-bundle <directory> --output <paper.tex>
  trureturing-paper emit-local-ports --frozen-bundle <directory> --output <directory>
  trureturing-paper assemble-example --frozen-bundle <directory> --output-root <repository-root>
""";
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
