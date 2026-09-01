namespace Trureturing.Paper.Core;

public static class PaperAssembler
{
    public static byte[] Assemble(PaperRecipe recipe, FrozenInputs frozenInputs)
        => LatexDocumentWriter.Write(AssembleDocument(recipe, frozenInputs));

    public static PaperDocument AssembleDocument(
        PaperRecipe recipe,
        FrozenInputs frozenInputs)
    {
        ArgumentNullException.ThrowIfNull(frozenInputs);
        var snapshot = SourceSnapshotReader.ReadAndVerify(frozenInputs.Snapshot);
        var truthGraph = frozenInputs.TruthGraph is null
            ? null
            : TruthGraphReader.ReadAndVerify(frozenInputs.TruthGraph, snapshot);
        var documentGraph = frozenInputs.DocumentGraph is null
            ? null
            : DocumentGraphReader.ReadAndVerify(frozenInputs.DocumentGraph);
        return ClaimGate.Resolve(recipe, frozenInputs, snapshot, truthGraph, documentGraph);
    }
}

public sealed class PaperAssemblyService
{
    private readonly IBlessedSnapshotPort _snapshot;
    private readonly IFrozenTruthPort _truth;
    private readonly IBlueprintPort _blueprint;
    private readonly ICitationPort _citations;
    private readonly IEvidencePort _evidence;
    private readonly ITruthGraphPort _truthGraph;
    private readonly IDocumentGraphPort _documentGraph;

    public PaperAssemblyService(
        IBlessedSnapshotPort snapshot,
        IFrozenTruthPort truth,
        IBlueprintPort blueprint,
        ICitationPort citations,
        IEvidencePort evidence,
        ITruthGraphPort truthGraph,
        IDocumentGraphPort documentGraph)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _truth = truth ?? throw new ArgumentNullException(nameof(truth));
        _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
        _citations = citations ?? throw new ArgumentNullException(nameof(citations));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _truthGraph = truthGraph ?? throw new ArgumentNullException(nameof(truthGraph));
        _documentGraph = documentGraph ?? throw new ArgumentNullException(nameof(documentGraph));
    }

    // Both graphs are always supplied here: proof state comes only from truth-graph.v1, while
    // describe nodes and anchors come only from the independently verified document graph.
    public byte[] Assemble(PaperRecipe recipe) => PaperAssembler.Assemble(recipe, new FrozenInputs(
        _snapshot.Read(),
        _truth.ReadDeclarations(),
        _blueprint.ReadBlocks(),
        _citations.ReadCitations(),
        _evidence.ReadEvidence(),
        _truthGraph.ReadTruthGraph(),
        _documentGraph.ReadDocumentGraph()));
}
