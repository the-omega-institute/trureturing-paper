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
