using StrataLint.Scribe;

namespace Trureturing.Paper.Core;

public sealed record PaperRecipe(
    string Schema,
    string PaperId,
    string Title,
    IReadOnlyList<RecipeClaim> Claims);

public sealed record RecipeClaim(string DeclarationGid, string DescribeAnchor);

public sealed record BlessedSnapshotEnvelope(byte[] Json, string ContentSha256);

public sealed record FrozenInputs(
    BlessedSnapshotEnvelope Snapshot,
    IReadOnlyList<FrozenDeclaration> Declarations,
    IReadOnlyList<BlueprintBlock> BlueprintBlocks,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<EvidenceItem> Evidence);

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
    string TruthRootSha256,
    string TruthGraphSha256,
    string LeanReportSha256,
    string BlessedBy);

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
