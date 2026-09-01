using System.Security.Cryptography;
using System.Text.Json;

namespace Trureturing.Paper.Core;

public static class DocumentGraphReader
{
    public static FrozenDocumentGraph ReadAndVerify(DocumentGraphEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Json);

        RequireDigest(envelope.ContentSha256, "expected digest");
        string actualDigest = Convert.ToHexString(SHA256.HashData(envelope.Json)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actualDigest),
                Convert.FromHexString(envelope.ContentSha256)))
        {
            throw new ClaimGateException(
                "document-graph.v1 content is not bound to its blessed digest.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(envelope.Json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (JsonException exception)
        {
            throw new ClaimGateException($"document-graph.v1 is invalid JSON: {exception.Message}");
        }

        using (document)
        {
            JsonElement root = RequireObject(document.RootElement, "root");
            RequireProperties(root, "root", "documents", "joins", "schema", "source_commit");
            RequireEqual(ReadString(root, "schema"), "document-graph.v1", "schema");
            string sourceCommit = ReadString(root, "source_commit");
            RequireHex(sourceCommit, 40, "source_commit");

            JsonElement documents = RequireObject(ReadProperty(root, "documents"), "documents");
            RequireProperties(
                documents,
                "documents",
                "describe_nodes",
                "document_edges",
                "document_nodes");
            IReadOnlyList<DocumentGraphDescribeNode> describeNodes =
                ReadDescribeNodes(ReadProperty(documents, "describe_nodes"));
            IReadOnlyList<DocumentGraphNode> documentNodes =
                ReadDocumentNodes(ReadProperty(documents, "document_nodes"));
            JsonElement edges = RequireObject(
                ReadProperty(documents, "document_edges"),
                "documents.document_edges");
            RequireProperties(
                edges,
                "documents.document_edges",
                "dependency",
                "narrative_reference");
            IReadOnlyList<DocumentGraphDependencyEdge> dependencyEdges =
                ReadDependencyEdges(ReadProperty(edges, "dependency"));
            IReadOnlyList<DocumentGraphNarrativeReferenceEdge> narrativeEdges =
                ReadNarrativeEdges(ReadProperty(edges, "narrative_reference"));

            JsonElement joins = RequireObject(ReadProperty(root, "joins"), "joins");
            RequireProperties(joins, "joins", "truth_anchors");
            IReadOnlyList<DocumentGraphAnchor> anchors =
                ReadAnchors(ReadProperty(joins, "truth_anchors"));

            RequireNonemptyUniqueMappings(describeNodes, anchors);
            return new FrozenDocumentGraph(
                sourceCommit,
                describeNodes,
                documentNodes,
                dependencyEdges,
                narrativeEdges,
                anchors);
        }
    }

    private static IReadOnlyList<DocumentGraphDescribeNode> ReadDescribeNodes(JsonElement element)
    {
        var values = new List<DocumentGraphDescribeNode>();
        foreach (JsonElement item in RequireArray(element, "documents.describe_nodes").EnumerateArray())
        {
            JsonElement value = RequireObject(item, "documents.describe_nodes item");
            RequireProperties(
                value,
                "documents.describe_nodes item",
                "describe_id",
                "document_gid",
                "formula_provenance",
                "kind",
                "lean_declaration_gid",
                "repo_path");
            values.Add(new DocumentGraphDescribeNode(
                ReadString(value, "describe_id"),
                ReadString(value, "document_gid"),
                ReadString(value, "formula_provenance"),
                ReadString(value, "kind"),
                ReadNullableString(value, "lean_declaration_gid"),
                ReadString(value, "repo_path")));
        }
        return values;
    }

    private static IReadOnlyList<DocumentGraphNode> ReadDocumentNodes(JsonElement element)
    {
        var values = new List<DocumentGraphNode>();
        foreach (JsonElement item in RequireArray(element, "documents.document_nodes").EnumerateArray())
        {
            JsonElement value = RequireObject(item, "documents.document_nodes item");
            RequireProperties(
                value,
                "documents.document_nodes item",
                "gid",
                "receipt",
                "repo_path");
            values.Add(new DocumentGraphNode(
                ReadString(value, "gid"),
                ReadString(value, "receipt"),
                ReadString(value, "repo_path")));
        }
        return values;
    }

    private static IReadOnlyList<DocumentGraphDependencyEdge> ReadDependencyEdges(JsonElement element)
    {
        var values = new List<DocumentGraphDependencyEdge>();
        foreach (JsonElement item in RequireArray(
                     element,
                     "documents.document_edges.dependency").EnumerateArray())
        {
            JsonElement value = RequireObject(item, "dependency edge");
            RequireProperties(value, "dependency edge", "dependency", "dependent");
            values.Add(new DocumentGraphDependencyEdge(
                ReadString(value, "dependency"),
                ReadString(value, "dependent")));
        }
        return values;
    }

    private static IReadOnlyList<DocumentGraphNarrativeReferenceEdge> ReadNarrativeEdges(
        JsonElement element)
    {
        var values = new List<DocumentGraphNarrativeReferenceEdge>();
        foreach (JsonElement item in RequireArray(
                     element,
                     "documents.document_edges.narrative_reference").EnumerateArray())
        {
            JsonElement value = RequireObject(item, "narrative reference edge");
            RequireProperties(value, "narrative reference edge", "source", "target");
            values.Add(new DocumentGraphNarrativeReferenceEdge(
                ReadString(value, "source"),
                ReadString(value, "target")));
        }
        return values;
    }

    private static IReadOnlyList<DocumentGraphAnchor> ReadAnchors(JsonElement element)
    {
        var values = new List<DocumentGraphAnchor>();
        foreach (JsonElement item in RequireArray(element, "joins.truth_anchors").EnumerateArray())
        {
            JsonElement value = RequireObject(item, "joins.truth_anchors item");
            RequireProperties(
                value,
                "joins.truth_anchors item",
                "describe_id",
                "document_gid",
                "document_repo_path",
                "formal_truth_repo_path",
                "lean_declaration_gid");
            values.Add(new DocumentGraphAnchor(
                ReadString(value, "describe_id"),
                ReadString(value, "document_gid"),
                ReadString(value, "document_repo_path"),
                ReadString(value, "formal_truth_repo_path"),
                ReadString(value, "lean_declaration_gid")));
        }
        return values;
    }

    private static void RequireNonemptyUniqueMappings(
        IReadOnlyList<DocumentGraphDescribeNode> describeNodes,
        IReadOnlyList<DocumentGraphAnchor> anchors)
    {
        if (describeNodes.Count == 0)
        {
            throw new ClaimGateException("document-graph.v1 describe_nodes is empty.");
        }
        if (anchors.Count == 0)
        {
            throw new ClaimGateException("document-graph.v1 truth_anchors is empty.");
        }

        var describeKeys = new HashSet<(string, string, string?)>();
        foreach (DocumentGraphDescribeNode value in describeNodes)
        {
            if (!describeKeys.Add((value.DescribeId, value.DocumentGid, value.LeanDeclarationGid)))
            {
                throw new ClaimGateException(
                    "document-graph.v1 has a duplicate describe-node identity.");
            }
        }

        var anchorKeys = new HashSet<(string, string, string)>();
        foreach (DocumentGraphAnchor value in anchors)
        {
            if (!anchorKeys.Add((value.DescribeId, value.DocumentGid, value.LeanDeclarationGid)))
            {
                throw new ClaimGateException(
                    "document-graph.v1 has a duplicate truth-anchor identity.");
            }
        }
    }

    private static void RequireProperties(JsonElement value, string name, params string[] expected)
    {
        string[] actual = value.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(property => property, StringComparer.Ordinal)
            .ToArray();
        string[] required = expected.OrderBy(property => property, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(required, StringComparer.Ordinal))
        {
            throw new ClaimGateException(
                $"document-graph.v1 {name} does not have the required strict property set.");
        }
    }

    private static JsonElement ReadProperty(JsonElement value, string property) =>
        value.TryGetProperty(property, out JsonElement result)
            ? result
            : throw new ClaimGateException($"document-graph.v1 is missing '{property}'.");

    private static JsonElement RequireObject(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object
            ? value
            : throw new ClaimGateException($"document-graph.v1 {name} is not an object.");

    private static JsonElement RequireArray(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Array
            ? value
            : throw new ClaimGateException($"document-graph.v1 {name} is not an array.");

    private static string ReadString(JsonElement value, string property)
    {
        JsonElement result = ReadProperty(value, property);
        return result.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(result.GetString())
            ? result.GetString()!
            : throw new ClaimGateException(
                $"document-graph.v1 '{property}' is not a nonempty string.");
    }

    private static string? ReadNullableString(JsonElement value, string property)
    {
        JsonElement result = ReadProperty(value, property);
        return result.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String when !string.IsNullOrWhiteSpace(result.GetString()) => result.GetString(),
            _ => throw new ClaimGateException(
                $"document-graph.v1 '{property}' is neither a nonempty string nor null.")
        };
    }

    private static void RequireEqual(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ClaimGateException($"document-graph.v1 {name} is not '{expected}'.");
        }
    }

    private static void RequireDigest(string value, string name) => RequireHex(value, 64, name);

    private static void RequireHex(string value, int length, string name)
    {
        if (value.Length != length || value.Any(character =>
                character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ClaimGateException(
                $"document-graph.v1 {name} is not canonical {length}-character hex.");
        }
    }
}
