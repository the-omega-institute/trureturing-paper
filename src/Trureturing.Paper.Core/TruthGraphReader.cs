using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trureturing.Paper.Core;

public static class TruthGraphReader
{
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

        Trureturing.Truth.TruthGraphExportModel shared;
        try
        {
            shared = Trureturing.Truth.TruthGraphJsonReader.Read(envelope.Json);
        }
        catch (Exception exception) when (exception is ArgumentException
            or DecoderFallbackException
            or FormatException
            or InvalidOperationException
            or JsonException
            or NotSupportedException)
        {
            throw new ClaimGateException($"truth-graph.v1 is invalid: {exception.Message}");
        }

        var nodes = shared.Truth.Nodes.Select(node => new TruthGraphNode(
            node.Depth,
            node.Gid,
            node.ModuleName,
            node.RepoPath,
            node.State)).ToArray();
        var edges = shared.Truth.Edges.Select(edge => new TruthGraphEdge(
            edge.Dependency,
            edge.Dependent)).ToArray();
        var describeNodes = shared.Documents.DescribeNodes.Select(node => new TruthGraphDescribeNode(
            node.DescribeId,
            node.DocumentGid,
            node.FormulaProvenance,
            node.Kind,
            node.LeanDeclarationGid,
            node.RepoPath)).ToArray();
        var anchors = shared.Joins.TruthAnchors.Select(anchor => new TruthGraphAnchor(
            anchor.DescribeId
                ?? throw new ClaimGateException(
                    "truth-graph.v1 truth anchor describe_id is null."),
            anchor.DocumentGid,
            anchor.DocumentRepoPath,
            anchor.FormalTruthRepoPath,
            anchor.LeanDeclarationGid)).ToArray();
        var provenance = new TruthGraphProvenance(
            shared.Provenance.LeanReportDigest,
            shared.Provenance.SnapshotContentDigest,
            shared.Provenance.TruthRootSha256,
            shared.Provenance.DependencyGranularity);

        VerifyProvenance(provenance, snapshot);
        return new FrozenTruthGraph(
            nodes,
            edges,
            describeNodes,
            anchors,
            provenance,
            shared.DeferredLayers);
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

}
