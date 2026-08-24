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
        return Validate(port);
    }

    public static PaperIntuitionPort ReadIntuitionPort(ReadOnlySpan<byte> bytes)
    {
        PaperIntuitionPort port = Deserialize<PaperIntuitionPort>(bytes);
        return Validate(port);
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

    internal static PaperTruthReleasePort Validate(PaperTruthReleasePort port)
    {
        if (port is null)
        {
            throw new ClaimGateException("truth release port must not be null.");
        }

        IReadOnlyList<PaperDeclarationPort> declarations =
            port.Declarations ?? throw new ClaimGateException(
                "declarations must not be null.");
        foreach (PaperDeclarationPort? declaration in declarations)
        {
            if (declaration is null)
            {
                throw new ClaimGateException("declaration must not be null.");
            }
        }

        Require(port.Schema == PaperPortSchemas.TruthReleasePort,
            $"schema must be {PaperPortSchemas.TruthReleasePort}.");
        RequireSha256(port.ReleaseDigest, "release_digest");
        RequireGitPair(port.SourceCommit, port.SourceTree);

        RequireUnique(
            declarations.Select(declaration => declaration.DeclarationId),
            "declaration_id");
        RequireUnique(
            declarations.Select(declaration => declaration.StatementId),
            "statement_id");

        var frozenIds = declarations
            .Select(declaration => declaration.FrozenNodeId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (PaperDeclarationPort declaration in declarations)
        {
            IReadOnlyList<string> prerequisites =
                declaration.PrerequisiteFrozenNodeIds ?? throw new ClaimGateException(
                    $"declaration {declaration.DeclarationId} prerequisites must not be null.");
            IReadOnlyList<string> axioms =
                declaration.AxiomClosure ?? throw new ClaimGateException(
                    $"declaration {declaration.DeclarationId} axioms must not be null.");
            RequireNonEmpty(declaration.DeclarationId, "declaration_id");
            RequireSha256(declaration.StatementId, "statement_id");
            RequireSha256(declaration.FrozenNodeId, "frozen_node_id");
            RequireNonEmpty(declaration.RepoPath, "repo_path");
            Require(declaration.RepoPath.EndsWith(".lean", StringComparison.Ordinal),
                $"repo_path {declaration.RepoPath} must end in .lean.");
            Require(DeclarationKinds.Contains(declaration.Kind),
                $"declaration {declaration.DeclarationId} has unknown kind {declaration.Kind}.");
            RequireUnique(
                prerequisites,
                $"prerequisite in {declaration.DeclarationId}");
            RequireUnique(
                axioms,
                $"axiom in {declaration.DeclarationId}");

            foreach (string prerequisite in prerequisites)
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

        RequireAcyclic(declarations);
        return port;
    }

    internal static PaperIntuitionPort Validate(PaperIntuitionPort port)
    {
        if (port is null)
        {
            throw new ClaimGateException("intuition port must not be null.");
        }

        IReadOnlyList<PaperIntuitionCandidatePort> candidates =
            port.Candidates ?? throw new ClaimGateException(
                "candidates must not be null.");
        foreach (PaperIntuitionCandidatePort? candidate in candidates)
        {
            if (candidate is null)
            {
                throw new ClaimGateException("intuition candidate must not be null.");
            }
        }

        Require(port.Schema == PaperPortSchemas.IntuitionPort,
            $"schema must be {PaperPortSchemas.IntuitionPort}.");
        RequireSha256(port.SourceTruthReleaseDigest, "source_truth_release_digest");
        RequireUnique(candidates.Select(candidate => candidate.ProposalId), "proposal_id");

        foreach (PaperIntuitionCandidatePort candidate in candidates)
        {
            IReadOnlyList<string> inputs = candidate.Inputs ?? throw new ClaimGateException(
                $"candidate {candidate.ProposalId} inputs must not be null.");
            IReadOnlyList<string> outputs = candidate.Outputs ?? throw new ClaimGateException(
                $"candidate {candidate.ProposalId} outputs must not be null.");
            _ = candidate.EvidenceRefs ?? throw new ClaimGateException(
                $"candidate {candidate.ProposalId} evidence must not be null.");
            RequireNonEmpty(candidate.ProposalId, "proposal_id");
            RequireNonEmpty(candidate.RelationType, "relation_type");
            Require(CandidateStatuses.Contains(candidate.Status),
                $"candidate {candidate.ProposalId} has unknown status {candidate.Status}.");
            Require(inputs.Count > 0,
                $"candidate {candidate.ProposalId} has no inputs.");
            Require(outputs.Count > 0,
                $"candidate {candidate.ProposalId} has no outputs.");
            RequireNonEmpty(candidate.Falsifier, "falsifier");
        }

        return port;
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

    private static void RequireGitPair(string? commit, string? tree)
    {
        if (commit is null || tree is null || !IsLowerHex(commit) ||
            commit.Length is not (40 or 64))
        {
            throw new ClaimGateException(
                "source_commit must be a lowercase 40- or 64-hex Git object id.");
        }

        if (!IsLowerHex(tree) || tree.Length != commit.Length)
        {
            throw new ClaimGateException(
                "source_tree must use the same Git object-id width as source_commit.");
        }
    }

    private static void RequireSha256(string? value, string field)
    {
        if (value is null || !value.StartsWith("sha256:", StringComparison.Ordinal))
        {
            throw new ClaimGateException($"{field} must use sha256:<64hex>.");
        }

        string hex = value["sha256:".Length..];
        Require(hex.Length == 64 && IsLowerHex(hex),
            $"{field} must use sha256:<64hex>.");
    }

    private static bool IsLowerHex(string? value) =>
        value is not null && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireUnique(IEnumerable<string>? values, string field)
    {
        if (values is null)
        {
            throw new ClaimGateException($"{field} must not be null.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireNonEmpty(value, field);
            Require(seen.Add(value), $"duplicate {field}: {value}.");
        }
    }

    private static void RequireNonEmpty(string? value, string field) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{field} must be non-empty.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ClaimGateException(message);
        }
    }
}
