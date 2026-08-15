using System.Security.Cryptography;
using System.Text.Json;

namespace Trureturing.Paper.Core;

public static class TruthGraphReader
{
    private static readonly HashSet<string> States = new(StringComparer.Ordinal)
    {
        "closed", "open", "tail", "semantic"
    };

    public static FrozenTruthGraph ReadAndVerify(
        TruthGraphEnvelope envelope,
        SourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Json);
        ArgumentNullException.ThrowIfNull(snapshot);

        var graphDigest = Sha256(envelope.Json);
        RequireDigestEqual(
            graphDigest,
            snapshot.TruthGraphSha256,
            "truth-graph.v1 content is not bound to the blessed snapshot");

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
            throw new ClaimGateException($"truth-graph.v1 is invalid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = RequireObject(document.RootElement, "truth-graph.v1 root");
            RequireEqual(ReadString(root, "schema"), "stratalint.truth-graph.v1", "schema");
            if (ReadInt32(root, "schema_version") != 1)
            {
                throw new ClaimGateException("truth-graph.v1 schema_version is not 1.");
            }

            var truth = RequireObject(ReadProperty(root, "truth"), "truth");
            var nodes = ReadNodes(ReadProperty(truth, "nodes"));
            var edges = ReadEdges(ReadProperty(truth, "edges"));
            VerifyStateCounts(nodes, RequireObject(ReadProperty(truth, "state_counts"), "truth.state_counts"));

            var documents = RequireObject(ReadProperty(root, "documents"), "documents");
            var describeNodes = ReadDescribeNodes(ReadProperty(documents, "describe_nodes"));
            var joins = RequireObject(ReadProperty(root, "joins"), "joins");
            var anchors = ReadAnchors(ReadProperty(joins, "truth_anchors"));

            var provenanceRoot = RequireObject(ReadProperty(root, "provenance"), "provenance");
            var snapshotRoot = RequireObject(
                ReadProperty(provenanceRoot, "snapshot"),
                "provenance.snapshot");
            var provenance = new TruthGraphProvenance(
                ReadString(provenanceRoot, "lean_report_digest"),
                ReadString(snapshotRoot, "content_digest"),
                ReadString(provenanceRoot, "truth_root_sha256"),
                ReadString(provenanceRoot, "dependency_granularity"));

            VerifyProvenance(provenance, snapshot);
            var deferredLayers = ReadStringArray(
                ReadProperty(root, "deferred_layers"),
                "deferred_layers");
            return new FrozenTruthGraph(
                nodes,
                edges,
                describeNodes,
                anchors,
                provenance,
                deferredLayers);
        }
    }

    public static ClosedTruthBinding RequireClosedTheorem(
        FrozenTruthGraph graph,
        string declarationGid,
        string describeAnchor)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (string.IsNullOrWhiteSpace(declarationGid)
            || string.IsNullOrWhiteSpace(describeAnchor)
            || !describeAnchor.StartsWith("describe:", StringComparison.Ordinal))
        {
            throw new ClaimGateException("Truth-graph claim identity is invalid.");
        }

        var describeId = describeAnchor["describe:".Length..];
        var descriptions = graph.DescribeNodes.Where(value =>
            string.Equals(value.DescribeId, describeId, StringComparison.Ordinal)
            && string.Equals(value.LeanDeclarationGid, declarationGid, StringComparison.Ordinal)
            && string.Equals(value.Kind, "theorem", StringComparison.Ordinal)).ToArray();
        if (descriptions.Length != 1)
        {
            throw new ClaimGateException(
                $"Declaration '{declarationGid}' has no unique theorem describe node in frozen truth.");
        }

        var description = descriptions[0];
        var closedNodes = graph.Nodes.Where(value =>
            string.Equals(value.Gid, description.DocumentGid, StringComparison.Ordinal)
            && string.Equals(value.State, "closed", StringComparison.Ordinal)).ToArray();
        if (closedNodes.Length != 1)
        {
            throw new ClaimGateException(
                $"Declaration '{declarationGid}' does not map to a unique closed truth node.");
        }

        var anchors = graph.TruthAnchors.Where(value =>
            string.Equals(value.DescribeId, description.DescribeId, StringComparison.Ordinal)
            && string.Equals(value.DocumentGid, description.DocumentGid, StringComparison.Ordinal)
            && string.Equals(value.DocumentRepoPath, description.RepoPath, StringComparison.Ordinal)
            && string.Equals(value.LeanDeclarationGid, declarationGid, StringComparison.Ordinal)).ToArray();
        if (anchors.Length != 1)
        {
            throw new ClaimGateException(
                $"Declaration '{declarationGid}' has no unique provenance-closed truth anchor.");
        }

        var anchor = anchors[0];
        return new ClosedTruthBinding(
            description.DescribeId,
            description.DocumentGid,
            declarationGid,
            anchor.DocumentRepoPath,
            anchor.FormalTruthRepoPath);
    }

    private static IReadOnlyList<TruthGraphNode> ReadNodes(JsonElement element)
    {
        var nodes = new List<TruthGraphNode>();
        foreach (var value in RequireArray(element, "truth.nodes").EnumerateArray())
        {
            var node = RequireObject(value, "truth.nodes item");
            var state = ReadString(node, "state");
            if (!States.Contains(state))
            {
                throw new ClaimGateException($"truth-graph.v1 has unknown node state '{state}'.");
            }

            var gid = ReadNullableString(node, "gid");
            nodes.Add(new TruthGraphNode(
                ReadInt32(node, "depth"),
                gid,
                ReadNullableString(node, "module_name"),
                ReadString(node, "repo_path"),
                state));
        }
        return nodes;
    }

    private static IReadOnlyList<TruthGraphEdge> ReadEdges(JsonElement element)
    {
        var edges = new List<TruthGraphEdge>();
        foreach (var value in RequireArray(element, "truth.edges").EnumerateArray())
        {
            var edge = RequireObject(value, "truth.edges item");
            edges.Add(new TruthGraphEdge(
                ReadString(edge, "dependency"),
                ReadString(edge, "dependent")));
        }
        return edges;
    }

    private static IReadOnlyList<TruthGraphDescribeNode> ReadDescribeNodes(JsonElement element)
    {
        var nodes = new List<TruthGraphDescribeNode>();
        foreach (var value in RequireArray(element, "documents.describe_nodes").EnumerateArray())
        {
            var node = RequireObject(value, "documents.describe_nodes item");
            nodes.Add(new TruthGraphDescribeNode(
                ReadString(node, "describe_id"),
                ReadString(node, "document_gid"),
                ReadString(node, "formula_provenance"),
                ReadString(node, "kind"),
                ReadNullableString(node, "lean_declaration_gid"),
                ReadString(node, "repo_path")));
        }
        return nodes;
    }

    private static IReadOnlyList<TruthGraphAnchor> ReadAnchors(JsonElement element)
    {
        var anchors = new List<TruthGraphAnchor>();
        foreach (var value in RequireArray(element, "joins.truth_anchors").EnumerateArray())
        {
            var anchor = RequireObject(value, "joins.truth_anchors item");
            anchors.Add(new TruthGraphAnchor(
                ReadString(anchor, "describe_id"),
                ReadString(anchor, "document_gid"),
                ReadString(anchor, "document_repo_path"),
                ReadString(anchor, "formal_truth_repo_path"),
                ReadString(anchor, "lean_declaration_gid")));
        }
        return anchors;
    }

    private static void VerifyStateCounts(
        IReadOnlyList<TruthGraphNode> nodes,
        JsonElement stateCounts)
    {
        foreach (var state in States)
        {
            var declared = ReadInt32(stateCounts, state);
            var actual = nodes.Count(value => string.Equals(value.State, state, StringComparison.Ordinal));
            if (declared != actual)
            {
                throw new ClaimGateException(
                    $"truth-graph.v1 state_counts.{state} does not match truth.nodes.");
            }
        }
    }

    private static void VerifyProvenance(
        TruthGraphProvenance provenance,
        SourceSnapshot snapshot)
    {
        var leanReport = ReadColonDigest(provenance.LeanReportDigest, "provenance.lean_report_digest");
        RequireDigestEqual(
            leanReport,
            snapshot.LeanReportSha256,
            "truth-graph.v1 Lean report provenance does not match the blessed snapshot");

        RequireCanonicalRawDigest(provenance.TruthRootSha256, "provenance.truth_root_sha256");
        RequireDigestEqual(
            provenance.TruthRootSha256,
            snapshot.TruthRootSha256,
            "truth-graph.v1 truth root provenance does not match the blessed snapshot");

        var repositorySnapshot = ReadColonDigest(
            provenance.SnapshotContentDigest,
            "provenance.snapshot.content_digest");
        var expectedRepositorySnapshot = snapshot.RepositorySnapshotDigest["sha256-".Length..];
        RequireDigestEqual(
            repositorySnapshot,
            expectedRepositorySnapshot,
            "truth-graph.v1 repository provenance does not match the blessed snapshot");

        if (string.IsNullOrWhiteSpace(provenance.DependencyGranularity))
        {
            throw new ClaimGateException("truth-graph.v1 dependency_granularity is empty.");
        }
    }

    private static string ReadColonDigest(string value, string name)
    {
        const string prefix = "sha256:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ClaimGateException($"truth-graph.v1 {name} is not a sha256: digest.");
        }
        var digest = value[prefix.Length..];
        RequireCanonicalRawDigest(digest, name);
        return digest;
    }

    private static void RequireCanonicalRawDigest(string value, string name)
    {
        if (value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ClaimGateException($"truth-graph.v1 {name} is not a canonical digest.");
        }
    }

    private static void RequireDigestEqual(string actual, string expected, string message)
    {
        RequireCanonicalRawDigest(actual, "digest");
        RequireCanonicalRawDigest(expected, "expected digest");
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual),
                Convert.FromHexString(expected)))
        {
            throw new ClaimGateException(message + ".");
        }
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static JsonElement ReadProperty(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value)
            ? value
            : throw new ClaimGateException($"truth-graph.v1 is missing '{property}'.");

    private static JsonElement RequireObject(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object
            ? value
            : throw new ClaimGateException($"truth-graph.v1 {name} is not an object.");

    private static JsonElement RequireArray(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Array
            ? value
            : throw new ClaimGateException($"truth-graph.v1 {name} is not an array.");

    private static IReadOnlyList<string> ReadStringArray(JsonElement value, string name)
    {
        var values = new List<string>();
        foreach (var item in RequireArray(value, name).EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new ClaimGateException(
                    $"truth-graph.v1 {name} contains a non-string or empty value.");
            }
            values.Add(item.GetString()!);
        }
        return values;
    }

    private static string ReadString(JsonElement root, string property)
    {
        var value = ReadProperty(root, property);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ClaimGateException($"truth-graph.v1 '{property}' is not a nonempty string.");
    }

    private static string? ReadNullableString(JsonElement root, string property)
    {
        var value = ReadProperty(root, property);
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => throw new ClaimGateException(
                $"truth-graph.v1 '{property}' is neither a string nor null.")
        };
    }

    private static int ReadInt32(JsonElement root, string property)
    {
        var value = ReadProperty(root, property);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : throw new ClaimGateException($"truth-graph.v1 '{property}' is not an integer.");
    }

    private static void RequireEqual(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ClaimGateException($"truth-graph.v1 {name} is not '{expected}'.");
        }
    }
}
