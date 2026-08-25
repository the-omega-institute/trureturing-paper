using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Cli;

public sealed record LocalDevRelease(
    PaperTruthReleasePort TruthPort,
    PaperIntuitionPort IntuitionPort,
    FrozenInputs FrozenInputs);

// Local example adapter only. It demonstrates the Paper-owned consumption contract but does
// not replace, impersonate or partially implement the upstream truth-release verifier.
public static class LocalDevTruthReleaseAdapter
{
    public static LocalDevRelease Read(string bundleDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        string root = Path.GetFullPath(bundleDirectory);
        var ports = new FrozenBundleFilePorts(root);
        var frozenInputs = new FrozenInputs(
            ports.Read(),
            ports.ReadDeclarations(),
            ports.ReadBlocks(),
            ports.ReadCitations(),
            ports.ReadEvidence(),
            ports.ReadTruthGraph());

        SourceSnapshot snapshot = SourceSnapshotReader.ReadAndVerify(frozenInputs.Snapshot);
        FrozenTruthGraph graph = TruthGraphReader.ReadAndVerify(
            frozenInputs.TruthGraph!,
            snapshot);
        string sourceTree = ReadSourceTree(frozenInputs.Snapshot.Json);
        string releaseDigest = HashReleaseInputs(root);

        var verificationRecipe = new PaperRecipe(
            "recipe.v1",
            "local-dev-release-verification",
            "Local development release verification",
            frozenInputs.Declarations.Select(declaration => new RecipeClaim(
                declaration.DeclarationGid,
                RequireBlueprint(
                    frozenInputs.BlueprintBlocks,
                    declaration.DeclarationGid).DescribeAnchor)).ToArray());
        _ = PaperAssembler.AssembleDocument(verificationRecipe, frozenInputs);

        PaperDeclarationPort[] declarations = frozenInputs.Declarations
            .OrderBy(declaration => declaration.DeclarationGid, StringComparer.Ordinal)
            .Select(declaration => ToPortDeclaration(
                declaration,
                frozenInputs.BlueprintBlocks,
                graph,
                snapshot))
            .ToArray();

        var truthPort = new PaperTruthReleasePort(
            PaperPortSchemas.TruthReleasePort,
            releaseDigest,
            snapshot.SourceCommit,
            sourceTree,
            declarations);
        _ = PaperTruthIndex.Build(truthPort);

        var intuitionPort = new PaperIntuitionPort(
            PaperPortSchemas.IntuitionPort,
            releaseDigest,
            [
                new PaperIntuitionCandidatePort(
                    "advisory/trace-norm-interaction",
                    "Trace and norm compatibility under conjugation",
                    "evidence-backed",
                    [ExamplePaperAssembler.CertifiedDeclarationId],
                    ["research/trace-norm-compatibility"],
                    ["describe:trace-invariance-under-conjugation"],
                    "A closed counterexample where conjugation preserves trace but breaks the proposed norm relation.",
                    0.18,
                    0.06),
                new PaperIntuitionCandidatePort(
                    "advisory/higher-carrier-trace",
                    "Lift trace invariance to higher carrier layers",
                    "proposed",
                    [ExamplePaperAssembler.CertifiedDeclarationId],
                    ["research/higher-carrier-trace-invariance"],
                    [],
                    "A higher-layer carrier satisfying the current hypotheses whose trace changes under conjugation.",
                    0.31,
                    0.12)
            ]);
        _ = PaperIntuitionIndex.Build(intuitionPort, PaperTruthIndex.Build(truthPort));

        return new LocalDevRelease(truthPort, intuitionPort, frozenInputs);
    }

    private static PaperDeclarationPort ToPortDeclaration(
        FrozenDeclaration declaration,
        IReadOnlyList<BlueprintBlock> blueprints,
        FrozenTruthGraph graph,
        SourceSnapshot snapshot)
    {
        if (!string.Equals(declaration.Status, "frozen", StringComparison.Ordinal))
        {
            throw new ClaimGateException(
                $"Local adapter input {declaration.DeclarationGid} is not frozen.");
        }

        BlueprintBlock blueprint = RequireBlueprint(
            blueprints,
            declaration.DeclarationGid);
        ClosedTruthBinding binding = TruthGraphReader.RequireClosedTheorem(
            graph,
            declaration.DeclarationGid,
            blueprint.DescribeAnchor);
        if (!string.Equals(
                binding.DocumentGid,
                declaration.TruthAnchor,
                StringComparison.Ordinal))
        {
            throw new ClaimGateException(
                $"Local adapter input {declaration.DeclarationGid} is not provenance closed.");
        }

        string statementDigest = PaperPortIdentity.StatementId(declaration.Statement);
        string frozenNodeDigest = HashText(string.Join(
            "\n",
            "local-dev-frozen-node.v1",
            snapshot.SourceCommit,
            binding.DocumentGid,
            declaration.DeclarationGid,
            statementDigest));

        return new PaperDeclarationPort(
            declaration.DeclarationGid,
            statementDigest,
            frozenNodeDigest,
            binding.FormalTruthRepoPath,
            "theorem",
            [],
            declaration.DeclaredAxioms
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            binding.DocumentRepoPath);
    }

    private static BlueprintBlock RequireBlueprint(
        IReadOnlyList<BlueprintBlock> blueprints,
        string declarationId)
    {
        BlueprintBlock[] matches = blueprints.Where(block => string.Equals(
            block.DeclarationGid,
            declarationId,
            StringComparison.Ordinal)).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new ClaimGateException(
                $"Local adapter input {declarationId} has no unique blueprint binding.");
    }

    private static string ReadSourceTree(byte[] snapshotBytes)
    {
        using JsonDocument document = JsonDocument.Parse(snapshotBytes);
        return document.RootElement.GetProperty("source_tree").GetString()
            ?? throw new ClaimGateException("source-snapshot.v1 source_tree is null.");
    }

    private static string HashReleaseInputs(string root)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "local-dev-truth-release.v1", []);
        foreach (string name in new[]
        {
            "source-snapshot.v1.json",
            "frozen-truth.v1.json",
            "truth-graph.v1.json",
            "blueprints.v1.json"
        })
        {
            Append(hash, name, File.ReadAllBytes(Path.Combine(root, name)));
        }

        return "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string name, byte[] content)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(name));
        hash.AppendData([0]);
        hash.AppendData(content);
        hash.AppendData([0]);
    }

    private static string HashText(string value) =>
        "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
