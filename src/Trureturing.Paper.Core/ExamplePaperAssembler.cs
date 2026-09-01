namespace Trureturing.Paper.Core;

public static class ExamplePaperAssembler
{
    public const string CertifiedDeclarationId =
        "D5/S0/Carrier/TraceConjugation.trace_conj";

    private const string DescribeAnchor =
        "describe:trace-invariance-under-conjugation";

    public static byte[] Assemble(
        PaperTruthReleasePort truthPort,
        PaperIntuitionPort intuitionPort,
        FrozenInputs frozenInputs)
    {
        ArgumentNullException.ThrowIfNull(truthPort);
        ArgumentNullException.ThrowIfNull(intuitionPort);
        ArgumentNullException.ThrowIfNull(frozenInputs);

        PaperTruthIndex truth = PaperTruthIndex.Build(truthPort);
        _ = PaperIntuitionIndex.Build(intuitionPort, truth);
        PaperTruthEntry certified = truth.GetDeclaration(CertifiedDeclarationId);
        SourceSnapshot snapshot = SourceSnapshotReader.ReadAndVerify(frozenInputs.Snapshot);
        if (!string.Equals(truth.SourceCommit, snapshot.SourceCommit, StringComparison.Ordinal)
            || !string.Equals(truth.SourceTree, snapshot.SourceTree, StringComparison.Ordinal))
        {
            throw new ClaimGateException(
                "Certified port is bound to a different blessed source snapshot.");
        }

        var recipe = new PaperRecipe(
            "recipe.v1",
            "trace-conjugation-example",
            "Trace Invariance Under Conjugation",
            [new RecipeClaim(certified.DeclarationId, DescribeAnchor)]);

        // The selected port entry must still pass the frozen-ledger, blessed-report,
        // axiom-whitelist and closed-truth-graph claim gate.
        PaperDocument document = PaperAssembler.AssembleDocument(recipe, frozenInputs);
        TheoremBlock theorem = document.Theorems.Single();
        FrozenDeclaration frozen = frozenInputs.Declarations.Single(declaration =>
            string.Equals(
                declaration.DeclarationGid,
                certified.DeclarationId,
                StringComparison.Ordinal));
        if (!certified.AxiomClosure.SequenceEqual(
                frozen.DeclaredAxioms.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new ClaimGateException(
                "Certified port axiom closure does not match the gated frozen declaration.");
        }

        FrozenTruthGraph graph = TruthGraphReader.ReadAndVerify(
            frozenInputs.TruthGraph ?? throw new ClaimGateException(
                "Example assembly requires a frozen truth graph."),
            snapshot);
        FrozenDocumentGraph documentGraph = DocumentGraphReader.ReadAndVerify(
            frozenInputs.DocumentGraph ?? throw new ClaimGateException(
                "Example assembly requires a frozen document graph."));
        ClosedTruthBinding binding = TruthGraphReader.RequireClosedTheorem(
            graph,
            documentGraph,
            certified.DeclarationId,
            theorem.DescribeAnchor);
        if (!string.Equals(
                certified.StatementId,
                PaperPortIdentity.StatementId(theorem.Statement),
                StringComparison.Ordinal)
            || !string.Equals(
                certified.RepoPath,
                binding.FormalTruthRepoPath,
                StringComparison.Ordinal)
            || !string.Equals(
                certified.MdbookPath,
                binding.DocumentRepoPath,
                StringComparison.Ordinal))
        {
            throw new ClaimGateException(
                "Certified port citation does not match the gated truth-graph binding.");
        }

        return LatexDocumentWriter.Write(document);
    }
}
