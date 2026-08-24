namespace Trureturing.Paper.Core;

public sealed record PaperTruthEntry(
    string DeclarationId,
    string StatementId,
    string FrozenNodeId,
    string RepoPath,
    string Kind,
    IReadOnlyList<string> PrerequisiteFrozenNodeIds,
    IReadOnlyList<string> AxiomClosure,
    string? MdbookPath);

public sealed class PaperTruthIndex
{
    private readonly IReadOnlyDictionary<string, PaperTruthEntry> _byDeclaration;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PaperTruthEntry>> _byFrozenNode;

    private PaperTruthIndex(
        string releaseDigest,
        string sourceCommit,
        string sourceTree,
        IReadOnlyDictionary<string, PaperTruthEntry> byDeclaration,
        IReadOnlyDictionary<string, IReadOnlyList<PaperTruthEntry>> byFrozenNode)
    {
        ReleaseDigest = releaseDigest;
        SourceCommit = sourceCommit;
        SourceTree = sourceTree;
        _byDeclaration = byDeclaration;
        _byFrozenNode = byFrozenNode;
    }

    public string ReleaseDigest { get; }
    public string SourceCommit { get; }
    public string SourceTree { get; }
    public IReadOnlyCollection<PaperTruthEntry> Declarations =>
        _byDeclaration.Values.OrderBy(
            entry => entry.DeclarationId,
            StringComparer.Ordinal).ToArray();

    public static PaperTruthIndex Build(PaperTruthReleasePort port)
    {
        var entries = port.Declarations.Select(declaration =>
            new PaperTruthEntry(
                declaration.DeclarationId,
                declaration.StatementId,
                declaration.FrozenNodeId,
                declaration.RepoPath,
                declaration.Kind,
                declaration.PrerequisiteFrozenNodeIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                declaration.AxiomClosure
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                declaration.MdbookPath))
            .OrderBy(entry => entry.DeclarationId, StringComparer.Ordinal)
            .ToArray();

        var byDeclaration = entries.ToDictionary(
            entry => entry.DeclarationId,
            StringComparer.Ordinal);
        var byFrozen = entries
            .GroupBy(entry => entry.FrozenNodeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PaperTruthEntry>)group
                    .OrderBy(entry => entry.DeclarationId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        return new PaperTruthIndex(
            port.ReleaseDigest,
            port.SourceCommit,
            port.SourceTree,
            byDeclaration,
            byFrozen);
    }

    public PaperTruthEntry GetDeclaration(string declarationId) =>
        _byDeclaration.TryGetValue(declarationId, out PaperTruthEntry? entry)
            ? entry
            : throw new ClaimGateException(
                $"Declaration {declarationId} is absent from release {ReleaseDigest}.");

    public IReadOnlyList<PaperTruthEntry> PrerequisiteClosure(
        string declarationId)
    {
        PaperTruthEntry root = GetDeclaration(declarationId);
        var result = new SortedDictionary<string, PaperTruthEntry>(
            StringComparer.Ordinal);
        var stack = new Stack<string>(
            root.PrerequisiteFrozenNodeIds.Reverse());

        while (stack.TryPop(out string? frozenNodeId))
        {
            if (!_byFrozenNode.TryGetValue(
                    frozenNodeId,
                    out IReadOnlyList<PaperTruthEntry>? declarations))
            {
                throw new ClaimGateException(
                    $"Frozen prerequisite {frozenNodeId} is absent.");
            }

            foreach (PaperTruthEntry declaration in declarations)
            {
                if (result.TryAdd(declaration.DeclarationId, declaration))
                {
                    foreach (string prerequisite in
                        declaration.PrerequisiteFrozenNodeIds.Reverse())
                    {
                        stack.Push(prerequisite);
                    }
                }
            }
        }

        return result.Values.ToArray();
    }

    public bool UsesOnlyAxioms(
        string declarationId,
        IReadOnlySet<string> allowedAxioms)
    {
        PaperTruthEntry declaration = GetDeclaration(declarationId);
        return declaration.AxiomClosure.All(allowedAxioms.Contains);
    }

    public IReadOnlyList<PaperTruthEntry> WithMdbookAnchor() =>
        _byDeclaration.Values
            .Where(entry => entry.MdbookPath is not null)
            .OrderBy(entry => entry.DeclarationId, StringComparer.Ordinal)
            .ToArray();
}

public sealed record PaperIntuitionEntry(
    string ProposalId,
    string RelationType,
    string Status,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> EvidenceRefs,
    string Falsifier,
    double? PredictedReachabilityGain,
    double? PredictedPruningGain);

public sealed class PaperIntuitionIndex
{
    private readonly IReadOnlyDictionary<string, PaperIntuitionEntry> _candidates;

    private PaperIntuitionIndex(
        string sourceTruthReleaseDigest,
        IReadOnlyDictionary<string, PaperIntuitionEntry> candidates)
    {
        SourceTruthReleaseDigest = sourceTruthReleaseDigest;
        _candidates = candidates;
    }

    public string SourceTruthReleaseDigest { get; }

    public static PaperIntuitionIndex Build(
        PaperIntuitionPort port,
        PaperTruthIndex truth)
    {
        if (port.SourceTruthReleaseDigest != truth.ReleaseDigest)
        {
            throw new ClaimGateException(
                "Intuition index is bound to a different truth release.");
        }

        var candidates = port.Candidates
            .Select(candidate => new PaperIntuitionEntry(
                candidate.ProposalId,
                candidate.RelationType,
                candidate.Status,
                candidate.Inputs.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                candidate.Outputs.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                candidate.EvidenceRefs.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                candidate.Falsifier,
                candidate.PredictedReachabilityGain,
                candidate.PredictedPruningGain))
            .OrderBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .ToDictionary(
                candidate => candidate.ProposalId,
                StringComparer.Ordinal);

        return new PaperIntuitionIndex(port.SourceTruthReleaseDigest, candidates);
    }

    public IReadOnlyList<PaperIntuitionEntry> Candidates =>
        _candidates.Values
            .OrderBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<PaperIntuitionEntry> UnsettledCandidates() =>
        _candidates.Values
            .Where(candidate => candidate.Status is
                "proposed" or
                "evidence-backed" or
                "under-verification" or
                "open")
            .OrderBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .ToArray();

    public PaperIntuitionEntry GetCandidate(string proposalId) =>
        _candidates.TryGetValue(proposalId, out PaperIntuitionEntry? candidate)
            ? candidate
            : throw new ClaimGateException(
                $"Intuition proposal {proposalId} is absent.");
}
