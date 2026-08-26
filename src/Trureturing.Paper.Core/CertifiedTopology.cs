using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Trureturing.Paper.Core;

public sealed record CertifiedTopologyBinding(
    string TruthReleaseDigest,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record ExactNonNegativeRational(
    BigInteger Numerator,
    BigInteger Denominator) : IComparable<ExactNonNegativeRational>
{
    public int CompareTo(ExactNonNegativeRational? other) => other is null
        ? 1
        : (Numerator * other.Denominator).CompareTo(
            other.Numerator * Denominator);
}

public sealed record CertifiedTopologyNodeMetrics(
    string NodeId,
    BigInteger InDegree,
    BigInteger OutDegree,
    BigInteger MinDepth,
    BigInteger MaxDepth,
    BigInteger AncestorCount,
    BigInteger DescendantCount,
    BigInteger DescendantCost,
    ExactNonNegativeRational NormalizedReach,
    ExactNonNegativeRational DependencyBetweenness);

public sealed record TopologyCycleCertificate(
    string Status,
    IReadOnlyList<IReadOnlyList<string>> Cycles);

public sealed record TopologyDanglingReference(
    string SourceNodeId,
    string MissingDependencyId);

public sealed record TopologyDanglingReferenceCertificate(
    string Status,
    IReadOnlyList<TopologyDanglingReference> DanglingReferences);

public sealed class CertifiedTopologyReadModel
{
    private readonly IReadOnlyDictionary<string, CertifiedTopologyNodeMetrics> _byId;

    internal CertifiedTopologyReadModel(
        CertifiedTopologyBinding binding,
        IReadOnlyList<CertifiedTopologyNodeMetrics> nodes,
        TopologyCycleCertificate cycleCertificate,
        TopologyDanglingReferenceCertificate danglingReferenceCertificate)
    {
        Binding = binding;
        Nodes = nodes;
        CycleCertificate = cycleCertificate;
        DanglingReferenceCertificate = danglingReferenceCertificate;
        _byId = nodes.ToImmutableDictionary(node => node.NodeId, StringComparer.Ordinal);
    }

    public CertifiedTopologyBinding Binding { get; }
    public IReadOnlyList<CertifiedTopologyNodeMetrics> Nodes { get; }
    public TopologyCycleCertificate CycleCertificate { get; }
    public TopologyDanglingReferenceCertificate DanglingReferenceCertificate { get; }

    public bool TryGetNode(
        string nodeId,
        out CertifiedTopologyNodeMetrics? node) =>
        _byId.TryGetValue(nodeId, out node);

    public CertifiedTopologyNodeMetrics GetNode(string nodeId) =>
        _byId.TryGetValue(nodeId, out CertifiedTopologyNodeMetrics? node)
            ? node
            : throw new InvalidDataException(
                $"Certified topology does not contain node '{nodeId}'.");
}

public sealed record CertifiedTopologyLoadResult(
    bool Available,
    CertifiedTopologyReadModel? Topology,
    string Status);

public static class CertifiedTopologyReader
{
    private const string Schema = "certified-topology.v1";

    public static CertifiedTopologyLoadResult LoadFile(
        string path,
        CertifiedTopologyBinding expectedBinding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new CertifiedTopologyLoadResult(
                false,
                null,
                "certified-topology publication unavailable");
        }

        try
        {
            return new CertifiedTopologyLoadResult(
                true,
                Read(File.ReadAllBytes(path), expectedBinding),
                "certified-topology.v1 consumed");
        }
        catch (FileNotFoundException)
        {
            return new CertifiedTopologyLoadResult(
                false,
                null,
                "certified-topology publication unavailable");
        }
        catch (DirectoryNotFoundException)
        {
            return new CertifiedTopologyLoadResult(
                false,
                null,
                "certified-topology publication unavailable");
        }
    }

    public static CertifiedTopologyReadModel Read(
        ReadOnlySpan<byte> bytes,
        CertifiedTopologyBinding expectedBinding)
    {
        ArgumentNullException.ThrowIfNull(expectedBinding);
        ValidateBindingShape(expectedBinding, "expected binding");
        Preflight(bytes);

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            JsonElement root = RequireObject(
                document.RootElement,
                "$",
                "schema_version",
                "truth_release_digest",
                "algorithm_profile_digest",
                "producer_commit",
                "nodes",
                "cycle_certificate",
                "dangling_reference_certificate");

            RequireEqual(ReadString(root, "schema_version", "$"), Schema, "schema_version");
            var actualBinding = new CertifiedTopologyBinding(
                ReadString(root, "truth_release_digest", "$"),
                ReadString(root, "algorithm_profile_digest", "$"),
                ReadString(root, "producer_commit", "$"));
            ValidateBindingShape(actualBinding, "certified topology");
            RequireEqual(
                actualBinding.TruthReleaseDigest,
                expectedBinding.TruthReleaseDigest,
                "truth_release_digest");
            RequireEqual(
                actualBinding.AlgorithmProfileDigest,
                expectedBinding.AlgorithmProfileDigest,
                "algorithm_profile_digest");
            RequireEqual(
                actualBinding.ProducerCommit,
                expectedBinding.ProducerCommit,
                "producer_commit");

            IReadOnlyList<CertifiedTopologyNodeMetrics> nodes = ReadNodes(
                root.GetProperty("nodes"));
            TopologyCycleCertificate cycle = ReadCycleCertificate(
                root.GetProperty("cycle_certificate"));
            TopologyDanglingReferenceCertificate dangling =
                ReadDanglingCertificate(
                    root.GetProperty("dangling_reference_certificate"));
            return new CertifiedTopologyReadModel(
                actualBinding,
                nodes,
                cycle,
                dangling);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Certified topology is malformed JSON.",
                exception);
        }
    }

    private static IReadOnlyList<CertifiedTopologyNodeMetrics> ReadNodes(
        JsonElement value)
    {
        RequireKind(value, JsonValueKind.Array, "$.nodes");
        var nodes = ImmutableArray.CreateBuilder<CertifiedTopologyNodeMetrics>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"$.nodes[{index}]";
            JsonElement node = RequireObject(
                item,
                path,
                "node_id",
                "in_degree",
                "out_degree",
                "min_depth",
                "max_depth",
                "ancestor_count",
                "descendant_count",
                "descendant_cost",
                "normalized_reach",
                "dependency_betweenness");
            string id = ReadNonEmptyString(node, "node_id", path);
            Require(ids.Add(id), $"Duplicate node_id '{id}'.");
            nodes.Add(new CertifiedTopologyNodeMetrics(
                id,
                ReadNonNegativeInteger(node, "in_degree", path),
                ReadNonNegativeInteger(node, "out_degree", path),
                ReadNonNegativeInteger(node, "min_depth", path),
                ReadNonNegativeInteger(node, "max_depth", path),
                ReadNonNegativeInteger(node, "ancestor_count", path),
                ReadNonNegativeInteger(node, "descendant_count", path),
                ReadNonNegativeInteger(node, "descendant_cost", path),
                ReadRational(node.GetProperty("normalized_reach"),
                    $"{path}.normalized_reach"),
                ReadRational(node.GetProperty("dependency_betweenness"),
                    $"{path}.dependency_betweenness")));
            index++;
        }

        return nodes.ToImmutable();
    }

    private static ExactNonNegativeRational ReadRational(
        JsonElement value,
        string path)
    {
        JsonElement rational = RequireObject(value, path, "numerator", "denominator");
        BigInteger numerator = ReadNonNegativeInteger(rational, "numerator", path);
        BigInteger denominator = ReadInteger(rational, "denominator", path);
        Require(denominator > 0, $"{path}.denominator must be positive.");
        Require(BigInteger.GreatestCommonDivisor(numerator, denominator) == BigInteger.One,
            $"{path} must be reduced.");
        return new ExactNonNegativeRational(numerator, denominator);
    }

    private static TopologyCycleCertificate ReadCycleCertificate(JsonElement value)
    {
        const string path = "$.cycle_certificate";
        JsonElement certificate = RequireObject(value, path, "status", "cycles");
        string status = ReadString(certificate, "status", path);
        Require(status is "acyclic" or "cycles-detected",
            $"{path}.status is unknown.");
        JsonElement cyclesValue = certificate.GetProperty("cycles");
        RequireKind(cyclesValue, JsonValueKind.Array, $"{path}.cycles");
        var cycles = ImmutableArray.CreateBuilder<IReadOnlyList<string>>();
        int index = 0;
        foreach (JsonElement cycleValue in cyclesValue.EnumerateArray())
        {
            string cyclePath = $"{path}.cycles[{index}]";
            RequireKind(cycleValue, JsonValueKind.Array, cyclePath);
            var cycle = cycleValue.EnumerateArray()
                .Select((item, itemIndex) => ReadNonEmptyString(
                    item,
                    $"{cyclePath}[{itemIndex}]"))
                .ToImmutableArray();
            Require(cycle.Length > 0, $"{cyclePath} must not be empty.");
            cycles.Add(cycle);
            index++;
        }

        bool found = cycles.Count > 0;
        Require((status == "cycles-detected") == found,
            "cycle_certificate status and cycles disagree.");
        return new TopologyCycleCertificate(status, cycles.ToImmutable());
    }

    private static TopologyDanglingReferenceCertificate ReadDanglingCertificate(
        JsonElement value)
    {
        const string path = "$.dangling_reference_certificate";
        JsonElement certificate = RequireObject(
            value,
            path,
            "status",
            "dangling_references");
        string status = ReadString(certificate, "status", path);
        Require(status is "complete" or "dangling-references-detected",
            $"{path}.status is unknown.");
        JsonElement referencesValue = certificate.GetProperty("dangling_references");
        RequireKind(referencesValue, JsonValueKind.Array, $"{path}.dangling_references");
        var references = ImmutableArray.CreateBuilder<TopologyDanglingReference>();
        int index = 0;
        foreach (JsonElement item in referencesValue.EnumerateArray())
        {
            string itemPath = $"{path}.dangling_references[{index}]";
            JsonElement reference = RequireObject(
                item,
                itemPath,
                "source_node_id",
                "missing_dependency_id");
            references.Add(new TopologyDanglingReference(
                ReadNonEmptyString(reference, "source_node_id", itemPath),
                ReadNonEmptyString(reference, "missing_dependency_id", itemPath)));
            index++;
        }

        bool found = references.Count > 0;
        Require((status == "dangling-references-detected") == found,
            "dangling_reference_certificate status and references disagree.");
        return new TopologyDanglingReferenceCertificate(
            status,
            references.ToImmutable());
    }

    private static JsonElement RequireObject(
        JsonElement value,
        string path,
        params string[] properties)
    {
        RequireKind(value, JsonValueKind.Object, path);
        var expected = properties.ToHashSet(StringComparer.Ordinal);
        var actual = value.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        string? missing = expected.Except(actual, StringComparer.Ordinal).FirstOrDefault();
        Require(missing is null, $"{path} is missing required property '{missing}'.");
        string? unknown = actual.Except(expected, StringComparer.Ordinal).FirstOrDefault();
        Require(unknown is null, $"{path} contains unknown property '{unknown}'.");
        return value;
    }

    private static string ReadString(JsonElement parent, string name, string path) =>
        ReadString(parent.GetProperty(name), $"{path}.{name}");

    private static string ReadString(JsonElement value, string path)
    {
        RequireKind(value, JsonValueKind.String, path);
        return value.GetString()!;
    }

    private static string ReadNonEmptyString(
        JsonElement parent,
        string name,
        string path) =>
        ReadNonEmptyString(parent.GetProperty(name), $"{path}.{name}");

    private static string ReadNonEmptyString(JsonElement value, string path)
    {
        string result = ReadString(value, path);
        Require(result.Length > 0, $"{path} must not be empty.");
        return result;
    }

    private static BigInteger ReadNonNegativeInteger(
        JsonElement parent,
        string name,
        string path)
    {
        BigInteger result = ReadInteger(parent, name, path);
        Require(result >= 0, $"{path}.{name} must be non-negative.");
        return result;
    }

    private static BigInteger ReadInteger(
        JsonElement parent,
        string name,
        string path)
    {
        JsonElement value = parent.GetProperty(name);
        RequireKind(value, JsonValueKind.Number, $"{path}.{name}");
        string raw = value.GetRawText();
        Require(BigInteger.TryParse(
                raw,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out BigInteger result),
            $"{path}.{name} must be an integer.");
        return result;
    }

    private static void ValidateBindingShape(
        CertifiedTopologyBinding binding,
        string source)
    {
        RequireSha256(binding.TruthReleaseDigest, $"{source} truth_release_digest");
        RequireSha256(binding.AlgorithmProfileDigest,
            $"{source} algorithm_profile_digest");
        Require(binding.ProducerCommit.Length == 40 && IsLowerHex(binding.ProducerCommit),
            $"{source} producer_commit must be 40 lowercase hexadecimal characters.");
    }

    private static void RequireSha256(string value, string field) =>
        Require(value.Length == 71 &&
            value.StartsWith("sha256:", StringComparison.Ordinal) &&
            IsLowerHex(value["sha256:".Length..]),
            $"{field} must use sha256:<64hex>.");

    private static bool IsLowerHex(string value) => value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireEqual(string actual, string expected, string field) =>
        Require(StringComparer.Ordinal.Equals(actual, expected),
            $"{field} does not match the expected topology binding.");

    private static void RequireKind(
        JsonElement value,
        JsonValueKind expected,
        string path) =>
        Require(value.ValueKind == expected,
            $"{path} must be {expected.ToString().ToLowerInvariant()}.");

    private static void Preflight(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (!reader.Read())
            {
                throw new InvalidDataException("Certified topology is empty.");
            }

            ReadValue(ref reader);
            if (reader.Read())
            {
                throw new InvalidDataException(
                    "Certified topology contains trailing content.");
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Certified topology is malformed JSON.",
                exception);
        }
    }

    private static void ReadValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            string lexeme = Encoding.UTF8.GetString(reader.ValueSpan);
            Require(!lexeme.Contains('.', StringComparison.Ordinal) &&
                !lexeme.Contains('e', StringComparison.OrdinalIgnoreCase),
                "Floating-point numeric lexemes are forbidden in certified topology metrics.");
            return;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected an object property name.");
                }

                string name = reader.GetString()!;
                Require(names.Add(name), $"Duplicate property '{name}'.");
                if (!reader.Read())
                {
                    throw new JsonException($"Property '{name}' has no value.");
                }

                ReadValue(ref reader);
            }

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException("Unterminated object.");
            }

            return;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                ReadValue(ref reader);
            }

            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("Unterminated array.");
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
