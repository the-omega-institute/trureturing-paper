using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class SourceSnapshotReader
{
    public const string CanonicalSchemaSha256 =
        "c87562cd6ab18dc0fbc9b587e65900859e6e94cf3564348601860b9aef3b7562";

    private static readonly HashSet<string> CanonicalProperties = new(StringComparer.Ordinal)
    {
        "schema", "source_repo", "source_commit", "source_tree",
        "repository_snapshot_digest", "producer_version", "truth_root_sha256",
        "truth_graph_sha256", "lean_report_sha256", "blueprint_tree_digest",
        "theory_tree_digest", "library_tree_digest", "evidence_digest",
        "derived_at", "blessed_by"
    };

    public static SourceSnapshot ReadAndVerify(BlessedSnapshotEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Json);
        RequirePattern(envelope.ContentSha256, "^[0-9a-f]{64}$", "snapshot content digest");
        var actualDigest = Convert.ToHexString(SHA256.HashData(envelope.Json)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(envelope.ContentSha256),
                Convert.FromHexString(actualDigest)))
        {
            throw new ClaimGateException("Blessed source-snapshot.v1 content digest mismatch.");
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
            throw new ClaimGateException($"Blessed source-snapshot.v1 is invalid JSON: {exception.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ClaimGateException("Blessed source-snapshot.v1 must be an object.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!CanonicalProperties.Contains(property.Name) || !seen.Add(property.Name))
                {
                    throw new ClaimGateException(
                        $"Blessed source-snapshot.v1 has unknown or duplicate property '{property.Name}'.");
                }
            }
            if (!seen.SetEquals(CanonicalProperties))
            {
                throw new ClaimGateException("Blessed source-snapshot.v1 is missing canonical properties.");
            }

            var root = document.RootElement;
            RequireEqual(ReadString(root, "schema"), "source-snapshot.v1", "schema");
            RequirePattern(ReadString(root, "source_repo"),
                "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", "source_repo");
            var commit = RequirePattern(ReadString(root, "source_commit"), "^[0-9a-fA-F]{40}$", "source_commit");
            RequirePattern(ReadString(root, "source_tree"), "^[0-9a-fA-F]{40}$", "source_tree");
            var repositorySnapshotDigest = RequirePattern(
                ReadString(root, "repository_snapshot_digest"),
                "^sha256-[0-9a-f]{64}$",
                "repository_snapshot_digest");
            RequireNonempty(ReadString(root, "producer_version"), "producer_version");
            var truthRoot = RequireRawDigest(root, "truth_root_sha256");
            var truthGraph = RequireRawDigest(root, "truth_graph_sha256");
            var leanReport = RequireRawDigest(root, "lean_report_sha256");
            RequirePrefixedDigest(root, "blueprint_tree_digest");
            RequirePrefixedDigest(root, "theory_tree_digest");
            RequirePrefixedDigest(root, "library_tree_digest");
            RequirePrefixedDigest(root, "evidence_digest");
            var derivedAt = ReadString(root, "derived_at");
            if (!DateTimeOffset.TryParseExact(derivedAt, "O", null,
                    System.Globalization.DateTimeStyles.None, out _)
                && !DateTimeOffset.TryParse(derivedAt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out _))
            {
                throw new ClaimGateException("source-snapshot.v1 derived_at is not an RFC 3339 timestamp.");
            }
            var blessedBy = RequireNonempty(ReadString(root, "blessed_by"), "blessed_by");
            return new SourceSnapshot(
                commit,
                repositorySnapshotDigest,
                truthRoot,
                truthGraph,
                leanReport,
                blessedBy);
        }
    }

    private static string RequireRawDigest(JsonElement root, string property) =>
        RequirePattern(ReadString(root, property), "^[0-9a-f]{64}$", property);

    private static void RequirePrefixedDigest(JsonElement root, string property) =>
        RequirePattern(ReadString(root, property), "^sha256-[0-9a-f]{64}$", property);

    private static string ReadString(JsonElement root, string property)
    {
        var value = root.GetProperty(property);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new ClaimGateException($"source-snapshot.v1 property '{property}' must be a string.");
    }

    private static string RequirePattern(string value, string pattern, string name) =>
        Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant)
            ? value
            : throw new ClaimGateException($"source-snapshot.v1 {name} is not canonical.");

    private static string RequireNonempty(string value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ClaimGateException($"source-snapshot.v1 {name} is empty.");

    private static void RequireEqual(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ClaimGateException($"source-snapshot.v1 {name} is not '{expected}'.");
        }
    }
}
