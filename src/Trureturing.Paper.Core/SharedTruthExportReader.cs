using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public sealed record SharedTruthDeclaration(
    string DeclarationNameKey,
    string Kind,
    string StatementId);

public sealed record SharedTruthNode(
    string RepoPath,
    string FrozenNodeId,
    ImmutableArray<string> NodeAxiomClosure,
    ImmutableArray<SharedTruthDeclaration> Declarations);

public sealed record SharedTruthExport(
    string SourceCommit,
    string SourceTree,
    ImmutableArray<SharedTruthNode> Nodes);

public static class SharedTruthExportReader
{
    private const string Schema = "stratalint.truth-export";
    private const int SchemaVersion = 1;
    private const string Dialect = "stratalint.truth-export.v1";
    private const string Producer = "TruthExportCommand";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex GitObjectId = new(
        "^(?:[0-9a-f]{40}|[0-9a-f]{64})$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex ContentId = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Kind = new(
        "^[a-z][a-z0-9_-]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly string[] TopLevelProperties =
    [
        "dialect",
        "nodes",
        "producer",
        "schema",
        "schema_version",
        "source_commit",
        "source_tree",
    ];

    private static readonly string[] NodeProperties =
    [
        "declarations",
        "frozen_node_id",
        "node_axiom_closure",
        "repo_path",
    ];

    private static readonly string[] DeclarationProperties =
    [
        "declaration_name_key",
        "kind",
        "statement_id",
    ];

    public static SharedTruthExport Read(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
        {
            throw Invalid("Shared truth export is empty.");
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(utf8);
        }
        catch (DecoderFallbackException exception)
        {
            throw Invalid("Shared truth export is not strict UTF-8.", exception);
        }

        if (text.StartsWith('\uFEFF')
            || text.Contains('\r', StringComparison.Ordinal)
            || !text.EndsWith('\n')
            || text.EndsWith("\n\n", StringComparison.Ordinal))
        {
            throw Invalid("Shared truth export does not use the canonical UTF-8/LF envelope.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                utf8,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                });
        }
        catch (JsonException exception)
        {
            throw Invalid("Shared truth export is invalid JSON.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            RequireExactProperties(root, TopLevelProperties, "root");
            RequireEqual(ReadString(root, "schema", "root"), Schema, "schema");
            RequireEqual(ReadInt32(root, "schema_version", "root"), SchemaVersion, "schema_version");
            RequireEqual(ReadString(root, "dialect", "root"), Dialect, "dialect");
            RequireEqual(ReadString(root, "producer", "root"), Producer, "producer");

            var sourceCommit = RequireMatch(
                ReadString(root, "source_commit", "root"),
                GitObjectId,
                "source_commit");
            var sourceTree = RequireMatch(
                ReadString(root, "source_tree", "root"),
                GitObjectId,
                "source_tree");
            if (sourceCommit.Length != sourceTree.Length)
            {
                throw Invalid("source_commit and source_tree use different Git hash algorithms.");
            }

            var nodesElement = ReadArray(root, "nodes", "root");
            var nodes = ImmutableArray.CreateBuilder<SharedTruthNode>();
            var repoPaths = new HashSet<string>(StringComparer.Ordinal);
            var frozenNodeIds = new HashSet<string>(StringComparer.Ordinal);
            string? previousNodeKey = null;
            foreach (var nodeElement in nodesElement.EnumerateArray())
            {
                var node = ReadNode(nodeElement);
                var nodeKey = node.RepoPath + "\0" + node.FrozenNodeId;
                RequireStrictlyAfter(previousNodeKey, nodeKey, "nodes");
                previousNodeKey = nodeKey;
                if (!repoPaths.Add(node.RepoPath))
                {
                    throw Invalid($"Duplicate active node repo_path '{node.RepoPath}'.");
                }
                if (!frozenNodeIds.Add(node.FrozenNodeId))
                {
                    throw Invalid($"Duplicate active frozen_node_id '{node.FrozenNodeId}'.");
                }
                nodes.Add(node);
            }

            return new SharedTruthExport(
                sourceCommit,
                sourceTree,
                nodes.ToImmutable());
        }
    }

    private static SharedTruthNode ReadNode(JsonElement element)
    {
        RequireExactProperties(element, NodeProperties, "node");
        var repoPath = RequireRepoPath(ReadString(element, "repo_path", "node"));
        var frozenNodeId = RequireMatch(
            ReadString(element, "frozen_node_id", "node"),
            ContentId,
            "frozen_node_id");

        var axioms = ImmutableArray.CreateBuilder<string>();
        string? previousAxiom = null;
        foreach (var axiomElement in ReadArray(
                     element,
                     "node_axiom_closure",
                     "node").EnumerateArray())
        {
            if (axiomElement.ValueKind != JsonValueKind.String)
            {
                throw Invalid("node_axiom_closure entries must be strings.");
            }
            var axiom = RequireNonempty(
                axiomElement.GetString(),
                "node_axiom_closure entry");
            RequireStrictlyAfter(previousAxiom, axiom, "node_axiom_closure");
            previousAxiom = axiom;
            axioms.Add(axiom);
        }

        var declarations = ImmutableArray.CreateBuilder<SharedTruthDeclaration>();
        var declarationNames = new HashSet<string>(StringComparer.Ordinal);
        var statementIds = new HashSet<string>(StringComparer.Ordinal);
        string? previousDeclarationKey = null;
        foreach (var declarationElement in ReadArray(
                     element,
                     "declarations",
                     "node").EnumerateArray())
        {
            var declaration = ReadDeclaration(declarationElement);
            var declarationKey =
                declaration.DeclarationNameKey + "\0" + declaration.StatementId;
            RequireStrictlyAfter(
                previousDeclarationKey,
                declarationKey,
                "declarations");
            previousDeclarationKey = declarationKey;
            if (!declarationNames.Add(declaration.DeclarationNameKey))
            {
                throw Invalid(
                    $"Duplicate declaration_name_key '{declaration.DeclarationNameKey}'.");
            }
            if (!statementIds.Add(declaration.StatementId))
            {
                throw Invalid($"Duplicate statement_id '{declaration.StatementId}' in one node.");
            }
            declarations.Add(declaration);
        }
        if (declarations.Count == 0)
        {
            throw Invalid("Shared truth node has no declarations.");
        }

        return new SharedTruthNode(
            repoPath,
            frozenNodeId,
            axioms.ToImmutable(),
            declarations.ToImmutable());
    }

    private static SharedTruthDeclaration ReadDeclaration(JsonElement element)
    {
        RequireExactProperties(element, DeclarationProperties, "declaration");
        var declarationNameKey = RequireNonempty(
            ReadString(element, "declaration_name_key", "declaration"),
            "declaration_name_key");
        var kind = RequireMatch(
            ReadString(element, "kind", "declaration"),
            Kind,
            "kind");
        var statementId = RequireMatch(
            ReadString(element, "statement_id", "declaration"),
            ContentId,
            "statement_id");
        return new SharedTruthDeclaration(
            declarationNameKey,
            kind,
            statementId);
    }

    private static void RequireExactProperties(
        JsonElement element,
        IReadOnlyCollection<string> expected,
        string context)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Invalid($"Shared truth {context} must be an object.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name, StringComparer.Ordinal)
                || !seen.Add(property.Name))
            {
                throw Invalid(
                    $"Shared truth {context} has unknown or duplicate property '{property.Name}'.");
            }
        }
        if (seen.Count != expected.Count)
        {
            throw Invalid($"Shared truth {context} is missing canonical properties.");
        }
    }

    private static JsonElement ReadArray(
        JsonElement parent,
        string property,
        string context)
    {
        var value = parent.GetProperty(property);
        return value.ValueKind == JsonValueKind.Array
            ? value
            : throw Invalid($"Shared truth {context}.{property} must be an array.");
    }

    private static string ReadString(
        JsonElement parent,
        string property,
        string context)
    {
        var value = parent.GetProperty(property);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw Invalid($"Shared truth {context}.{property} must be a string.");
    }

    private static int ReadInt32(
        JsonElement parent,
        string property,
        string context)
    {
        var value = parent.GetProperty(property);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : throw Invalid($"Shared truth {context}.{property} must be an integer.");
    }

    private static string RequireRepoPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('\0', StringComparison.Ordinal)
            || !value.EndsWith(".lean", StringComparison.Ordinal))
        {
            throw Invalid("repo_path is not a canonical relative Lean path.");
        }
        var segments = value.Split('/');
        if (segments.Any(static segment =>
                string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw Invalid("repo_path contains an empty or traversal segment.");
        }
        return value;
    }

    private static string RequireNonempty(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            && !value.Any(char.IsControl)
                ? value
                : throw Invalid($"{name} is empty or contains control characters.");

    private static string RequireMatch(string value, Regex pattern, string name) =>
        pattern.IsMatch(value)
            ? value
            : throw Invalid($"Shared truth {name} is not canonical.");

    private static void RequireStrictlyAfter(
        string? previous,
        string current,
        string name)
    {
        if (previous is not null && string.CompareOrdinal(previous, current) >= 0)
        {
            throw Invalid($"Shared truth {name} must be strictly sorted and unique.");
        }
    }

    private static void RequireEqual(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw Invalid($"Shared truth {name} is not '{expected}'.");
        }
    }

    private static void RequireEqual(int actual, int expected, string name)
    {
        if (actual != expected)
        {
            throw Invalid($"Shared truth {name} is not {expected}.");
        }
    }

    private static ClaimGateException Invalid(string message) => new(message);

    private static ClaimGateException Invalid(string message, Exception innerException) =>
        new($"{message} {innerException.Message}");
}
