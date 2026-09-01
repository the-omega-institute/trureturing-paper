namespace Trureturing.Paper.Core;

public interface IBlessedSnapshotPort
{
    BlessedSnapshotEnvelope Read();
}

public interface IFrozenTruthPort
{
    IReadOnlyList<FrozenDeclaration> ReadDeclarations();
}

public interface IBlueprintPort
{
    IReadOnlyList<BlueprintBlock> ReadBlocks();
}

public interface ICitationPort
{
    IReadOnlyList<Citation> ReadCitations();
}

public interface IEvidencePort
{
    IReadOnlyList<EvidenceItem> ReadEvidence();
}

public interface ITruthGraphPort
{
    // The blessed truth-graph (verified against the snapshot's truth_graph_sha256 by the
    // assembler). Supplying it makes the claim gate require each frozen declaration to be a
    // closed node in that graph, so a tampered frozen-truth file cannot impersonate a frozen
    // theorem with a GID that is not actually closed in the blessed truth.
    TruthGraphEnvelope ReadTruthGraph();
}

public interface IDocumentGraphPort
{
    // The document graph and its separately blessed byte digest. The assembler verifies the
    // digest before using describe nodes or truth anchors from this graph.
    DocumentGraphEnvelope ReadDocumentGraph();
}
