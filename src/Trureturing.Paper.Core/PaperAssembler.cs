namespace Trureturing.Paper.Core;

public static class PaperAssembler
{
    public static byte[] Assemble(PaperRecipe recipe, FrozenInputs frozenInputs)
    {
        ArgumentNullException.ThrowIfNull(frozenInputs);
        var snapshot = SourceSnapshotReader.ReadAndVerify(frozenInputs.Snapshot);
        var document = ClaimGate.Resolve(recipe, frozenInputs, snapshot);
        return LatexDocumentWriter.Write(document);
    }
}

public sealed class PaperAssemblyService
{
    private readonly IBlessedSnapshotPort _snapshot;
    private readonly IFrozenTruthPort _truth;
    private readonly IBlueprintPort _blueprint;
    private readonly ICitationPort _citations;
    private readonly IEvidencePort _evidence;

    public PaperAssemblyService(
        IBlessedSnapshotPort snapshot,
        IFrozenTruthPort truth,
        IBlueprintPort blueprint,
        ICitationPort citations,
        IEvidencePort evidence)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _truth = truth ?? throw new ArgumentNullException(nameof(truth));
        _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
        _citations = citations ?? throw new ArgumentNullException(nameof(citations));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
    }

    public byte[] Assemble(PaperRecipe recipe) => PaperAssembler.Assemble(recipe, new FrozenInputs(
        _snapshot.Read(),
        _truth.ReadDeclarations(),
        _blueprint.ReadBlocks(),
        _citations.ReadCitations(),
        _evidence.ReadEvidence()));
}
