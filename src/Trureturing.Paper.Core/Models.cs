using StrataLint.Scribe;

namespace Trureturing.Paper.Core;

public sealed record PaperRecipe(
    string Schema,
    string PaperId,
    string Title,
    IReadOnlyList<RecipeClaim> Claims);

public sealed record RecipeClaim(string DeclarationGid, string DescribeAnchor);

public sealed record BlessedSnapshotEnvelope(byte[] Json, string ContentSha256);

public sealed record TruthGraphEnvelope(byte[] Json);

public sealed record DocumentGraphEnvelope(byte[] Json, string ContentSha256);

public sealed record FrozenInputs(
    BlessedSnapshotEnvelope Snapshot,
    IReadOnlyList<FrozenDeclaration> Declarations,
    IReadOnlyList<BlueprintBlock> BlueprintBlocks,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<EvidenceItem> Evidence,
    TruthGraphEnvelope? TruthGraph = null,
    DocumentGraphEnvelope? DocumentGraph = null);

public sealed record FrozenDeclaration(
    string DeclarationGid,
    string Status,
    Formula Statement,
    string TruthAnchor,
    string LeanReportSha256,
    IReadOnlyList<string> DeclaredAxioms,
    IReadOnlyList<string> AllowedAxioms);

public sealed record BlueprintBlock(
    string DescribeAnchor,
    string DeclarationGid,
    string TruthAnchor,
    string Narrative);

public sealed record Citation(string Key, string BibTex);

public sealed record EvidenceItem(string Key, string Digest);

public sealed record SourceSnapshot(
    string SourceCommit,
    string RepositorySnapshotDigest,
    string TruthRootSha256,
    string TruthGraphSha256,
    string LeanReportSha256,
    string BlessedBy)
{
    public string? SourceTree { get; init; }
}

public sealed record FrozenTruthGraph(
    IReadOnlyList<TruthGraphNode> Nodes,
    IReadOnlyList<TruthGraphEdge> Edges,
    TruthGraphProvenance Provenance,
    IReadOnlyList<string> DeferredLayers);

public sealed record FrozenDocumentGraph(
    string SourceCommit,
    IReadOnlyList<DocumentGraphDescribeNode> DescribeNodes,
    IReadOnlyList<DocumentGraphNode> DocumentNodes,
    IReadOnlyList<DocumentGraphDependencyEdge> DependencyEdges,
    IReadOnlyList<DocumentGraphNarrativeReferenceEdge> NarrativeReferenceEdges,
    IReadOnlyList<DocumentGraphAnchor> TruthAnchors);

public sealed record TruthGraphNode(
    int Depth,
    string? Gid,
    string? ModuleName,
    string RepoPath,
    string State);

public sealed record TruthGraphEdge(string Dependency, string Dependent);

public sealed record DocumentGraphDescribeNode(
    string DescribeId,
    string DocumentGid,
    string FormulaProvenance,
    string Kind,
    string? LeanDeclarationGid,
    string RepoPath);

public sealed record DocumentGraphNode(string Gid, string Receipt, string RepoPath);

public sealed record DocumentGraphDependencyEdge(string Dependency, string Dependent);

public sealed record DocumentGraphNarrativeReferenceEdge(string Source, string Target);

public sealed record DocumentGraphAnchor(
    string DescribeId,
    string DocumentGid,
    string DocumentRepoPath,
    string FormalTruthRepoPath,
    string LeanDeclarationGid);

public sealed record TruthGraphProvenance(
    string LeanReportDigest,
    string SnapshotContentDigest,
    string TruthRootSha256,
    string DependencyGranularity);

public sealed record ClosedTruthBinding(
    string DescribeId,
    string DocumentGid,
    string LeanDeclarationGid,
    string DocumentRepoPath,
    string FormalTruthRepoPath);

public sealed record PaperDocument(string PaperId, string Title, IReadOnlyList<TheoremBlock> Theorems);

public sealed record TheoremBlock(
    string DeclarationGid,
    string DescribeAnchor,
    string Narrative,
    Formula Statement);

public sealed class ClaimGateException : InvalidOperationException
{
    public ClaimGateException(string message) : base(message) { }
}
