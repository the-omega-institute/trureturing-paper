using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperPortSchemas
{
    public const string TruthReleasePort = "paper-truth-release-port.v1";
    public const string IntuitionPort = "paper-intuition-port.v1";
}

public sealed record PaperDeclarationPort(
    string DeclarationId,
    string StatementId,
    string FrozenNodeId,
    string RepoPath,
    string Kind,
    IReadOnlyList<string> PrerequisiteFrozenNodeIds,
    IReadOnlyList<string> AxiomClosure,
    string? MdbookPath);

public sealed record PaperTruthReleasePort(
    string Schema,
    string ReleaseDigest,
    string SourceCommit,
    string SourceTree,
    IReadOnlyList<PaperDeclarationPort> Declarations);

public sealed record PaperIntuitionCandidatePort(
    string ProposalId,
    string RelationType,
    string Status,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> EvidenceRefs,
    string Falsifier,
    double? PredictedReachabilityGain,
    double? PredictedPruningGain);

public sealed record PaperIntuitionPort(
    string Schema,
    string SourceTruthReleaseDigest,
    IReadOnlyList<PaperIntuitionCandidatePort> Candidates);

public static class PaperPortJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        WriteIndented = true
    };

    private static readonly HashSet<string> DeclarationKinds =
        new(StringComparer.Ordinal)
        {
            "theorem",
            "lemma",
            "definition",
            "example",
            "axiom",
            "opaque",
            "abbrev"
        };

    private static readonly HashSet<string> CandidateStatuses =
        new(StringComparer.Ordinal)
        {
            "proposed",
            "evidence-backed",
            "under-verification",
            "proved",
            "refuted",
            "wall",
            "duplicate",
            "trivial",
            "open",
            "infrastructure-failure"
        };

    public static PaperTruthReleasePort ReadTruthReleasePort(ReadOnlySpan<byte> bytes)
    {
        PaperTruthReleasePort port = Deserialize<PaperTruthReleasePort>(bytes);
        Validate(port);
        return port;
    }

    public static PaperIntuitionPort ReadIntuitionPort(ReadOnlySpan<byte> bytes)
    {
        PaperIntuitionPort port = Deserialize<PaperIntuitionPort>(bytes);
        Validate(port);
        return port;
    }

    public static byte[] Write<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, Options)
            .Concat(new byte[] { (byte)'\n' })
            .ToArray();

    private static T Deserialize<T>(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, Options)
                ?? throw new ClaimGateException($"{typeof(T).Name} is null.");
        }
        catch (JsonException exception)
        {
            throw new ClaimGateException(
                $"{typeof(T).Name} is invalid JSON: {exception.Message}");
        }
    }

    private static void Validate(PaperTruthReleasePort port)
    {
        Require(port.Schema == PaperPortSchemas.TruthReleasePort,
            $"schema must be {PaperPortSchemas.TruthReleasePort}.");
        RequireSha256(port.ReleaseDigest, "release_digest");
        RequireGitPair(port.SourceCommit, port.SourceTree);

        RequireUnique(
            port.Declarations.Select(declaration => declaration.DeclarationId),
            "declaration_id");
        RequireUnique(
            port.Declarations.Select(declaration => declaration.StatementId),
            "statement_id");

        var frozenIds = port.Declarations
            .Select(declaration => declaration.FrozenNodeId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (PaperDeclarationPort declaration in port.Declarations)
        {
            RequireNonEmpty(declaration.DeclarationId, "declaration_id");
            RequireSha256(declaration.StatementId, "statement_id");
            RequireSha256(declaration.FrozenNodeId, "frozen_node_id");
            RequireNonEmpty(declaration.RepoPath, "repo_path");
            Require(declaration.RepoPath.EndsWith(".lean", StringComparison.Ordinal),
                $"repo_path {declaration.RepoPath} must end in .lean.");
            Require(DeclarationKinds.Contains(declaration.Kind),
                $"declaration {declaration.DeclarationId} has unknown kind {declaration.Kind}.");
            RequireUnique(
                declaration.PrerequisiteFrozenNodeIds,
                $"prerequisite in {declaration.DeclarationId}");
            RequireUnique(
                declaration.AxiomClosure,
                $"axiom in {declaration.DeclarationId}");

            foreach (string prerequisite in declaration.PrerequisiteFrozenNodeIds)
            {
                RequireSha256(prerequisite, "prerequisite_frozen_node_id");
                Require(frozenIds.Contains(prerequisite),
                    $"prerequisite {prerequisite} is absent from the port.");
                Require(prerequisite != declaration.FrozenNodeId,
                    $"declaration {declaration.DeclarationId} has a self prerequisite.");
            }

            if (declaration.MdbookPath is not null)
            {
                RequireNonEmpty(declaration.MdbookPath, "mdbook_path");
            }
        }

        RequireAcyclic(port.Declarations);
    }

    private static void Validate(PaperIntuitionPort port)
    {
        Require(port.Schema == PaperPortSchemas.IntuitionPort,
            $"schema must be {PaperPortSchemas.IntuitionPort}.");
        RequireSha256(port.SourceTruthReleaseDigest, "source_truth_release_digest");
        RequireUnique(port.Candidates.Select(candidate => candidate.ProposalId), "proposal_id");

        foreach (PaperIntuitionCandidatePort candidate in port.Candidates)
        {
            RequireNonEmpty(candidate.ProposalId, "proposal_id");
            RequireNonEmpty(candidate.RelationType, "relation_type");
            Require(CandidateStatuses.Contains(candidate.Status),
                $"candidate {candidate.ProposalId} has unknown status {candidate.Status}.");
            Require(candidate.Inputs.Count > 0,
                $"candidate {candidate.ProposalId} has no inputs.");
            Require(candidate.Outputs.Count > 0,
                $"candidate {candidate.ProposalId} has no outputs.");
            RequireNonEmpty(candidate.Falsifier, "falsifier");
        }
    }

    private static void RequireAcyclic(
        IReadOnlyList<PaperDeclarationPort> declarations)
    {
        var prerequisites = declarations
            .GroupBy(declaration => declaration.FrozenNodeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(item => item.PrerequisiteFrozenNodeIds)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in prerequisites.Keys)
        {
            Visit(id, prerequisites, colors);
        }
    }

    private static void Visit(
        string id,
        IReadOnlyDictionary<string, string[]> prerequisites,
        IDictionary<string, int> colors)
    {
        if (colors.TryGetValue(id, out int color))
        {
            Require(color != 1, "frozen proof graph contains a cycle.");
            return;
        }

        colors[id] = 1;
        foreach (string prerequisite in prerequisites[id])
        {
            Visit(prerequisite, prerequisites, colors);
        }

        colors[id] = 2;
    }

    private static void RequireGitPair(string commit, string tree)
    {
        Require(IsLowerHex(commit) && commit.Length is 40 or 64,
            "source_commit must be a lowercase 40- or 64-hex Git object id.");
        Require(IsLowerHex(tree) && tree.Length == commit.Length,
            "source_tree must use the same Git object-id width as source_commit.");
    }

    private static void RequireSha256(string value, string field)
    {
        Require(value.StartsWith("sha256:", StringComparison.Ordinal),
            $"{field} must use sha256:<64hex>.");
        string hex = value["sha256:".Length..];
        Require(hex.Length == 64 && IsLowerHex(hex),
            $"{field} must use sha256:<64hex>.");
    }

    private static bool IsLowerHex(string value) =>
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireUnique(IEnumerable<string> values, string field)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireNonEmpty(value, field);
            Require(seen.Add(value), $"duplicate {field}: {value}.");
        }
    }

    private static void RequireNonEmpty(string value, string field) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{field} must be non-empty.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ClaimGateException(message);
        }
    }
}
