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
        return ClaimGate.Resolve(recipe, frozenInputs, snapshot, truthGraph);
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

    public PaperAssemblyService(
        IBlessedSnapshotPort snapshot,
        IFrozenTruthPort truth,
        IBlueprintPort blueprint,
        ICitationPort citations,
        IEvidencePort evidence,
        ITruthGraphPort truthGraph)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _truth = truth ?? throw new ArgumentNullException(nameof(truth));
        _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
        _citations = citations ?? throw new ArgumentNullException(nameof(citations));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        _truthGraph = truthGraph ?? throw new ArgumentNullException(nameof(truthGraph));
    }

    // The truth-graph is always supplied here, so the claim gate's closed-theorem binding is
    // never silently skipped in this composed path (contrast the null default on FrozenInputs).
    public byte[] Assemble(PaperRecipe recipe) => PaperAssembler.Assemble(recipe, new FrozenInputs(
        _snapshot.Read(),
        _truth.ReadDeclarations(),
        _blueprint.ReadBlocks(),
        _citations.ReadCitations(),
        _evidence.ReadEvidence(),
        _truthGraph.ReadTruthGraph()));
}
