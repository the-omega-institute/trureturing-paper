using System.Numerics;
using System.Text;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class CertifiedTopologyConsumerTests
{
    [Fact]
    public void VendoredSchemaBytesMatchTheUpstreamContract()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "certified-topology.v1.schema.json"));
        string digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(
            "f6a6eabcf79b7db44eb2ec8b296345c44fa23e5057a26dc4a506529398ff2c42",
            digest);
    }

    [Fact]
    public void ExactCertifiedMetricsEnterCandidateAndLiteratureContext()
    {
        (PaperTruthIndex truth, PaperIntuitionIndex intuition) = ReadExampleIndexes();
        CertifiedTopologyReadModel topology = CertifiedTopologyReader.Read(
            FixtureBytes(),
            Binding(truth.ReleaseDigest));

        CertifiedTopologyNodeMetrics node = Assert.Single(topology.Nodes);
        Assert.Equal(new BigInteger(377), node.DescendantCost);
        Assert.Equal(
            new ExactNonNegativeRational(7, 9),
            node.DependencyBetweenness);

        CandidateProposalArtifacts proposal = Assert.Single(
            CandidatePipeline.Propose(truth, intuition, topology));
        CandidateStructuralContext context = proposal.CandidatePaper.StructuralContext;
        Assert.Equal("certified", context.Availability);
        Assert.Equal(truth.ReleaseDigest, context.TruthReleaseDigest);
        CandidateTopologyNodeContext keyNode = Assert.Single(context.KeyNodes);
        Assert.Equal("7", keyNode.MinDepth);
        Assert.Equal("9", keyNode.MaxDepth);
        Assert.Equal("377", keyNode.DescendantCost);
        Assert.Equal(
            new CandidateExactRational("7", "9"),
            keyNode.DependencyBetweenness);
        Assert.Equal(context, proposal.LiteratureResearch.StructuralContext);
        Assert.Contains(
            proposal.CandidatePaper.KeyClaims,
            claim => claim.Kind == "conjectured");
    }

    [Fact]
    public void ConsumerRejectsMalformedFloatUnreducedAndMismatchedInput()
    {
        string valid = Encoding.UTF8.GetString(FixtureBytes());
        string releaseDigest = ReadExampleIndexes().Truth.ReleaseDigest;

        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            Encoding.UTF8.GetBytes("{"),
            Binding(releaseDigest)));
        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            Encoding.UTF8.GetBytes("{}"),
            Binding(releaseDigest)));
        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            Encoding.UTF8.GetBytes(valid.Replace(
                "\"out_degree\": 4",
                "\"out_degree\": 4.0",
                StringComparison.Ordinal)),
            Binding(releaseDigest)));
        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            Encoding.UTF8.GetBytes(valid.Replace(
                "\"numerator\": 13, \"denominator\": 21",
                "\"numerator\": 14, \"denominator\": 21",
                StringComparison.Ordinal)),
            Binding(releaseDigest)));
        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            FixtureBytes(),
            Binding("sha256:" + new string('f', 64))));
        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            FixtureBytes(),
            Binding(releaseDigest) with
            {
                AlgorithmProfileDigest = "sha256:" + new string('b', 64)
            }));
        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            FixtureBytes(),
            Binding(releaseDigest) with { ProducerCommit = new string('d', 40) }));
        Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.Read(
            Encoding.UTF8.GetBytes(valid.Replace(
                "\"schema_version\": \"certified-topology.v1\"",
                "\"schema_version\": \"certified-topology.v1\", \"unknown\": true",
                StringComparison.Ordinal)),
            Binding(releaseDigest)));
    }

    [Fact]
    public void MissingPublicationDegradesButExistingInvalidFileFailsClosed()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "trureturing-paper-topology-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string missing = Path.Combine(root, "not-published.json");
            CertifiedTopologyLoadResult result = CertifiedTopologyReader.LoadFile(
                missing,
                Binding(ReadExampleIndexes().Truth.ReleaseDigest));
            Assert.False(result.Available);
            Assert.Null(result.Topology);

            string malformed = Path.Combine(root, "malformed.json");
            File.WriteAllText(malformed, "{}");
            Assert.Throws<InvalidDataException>(() => CertifiedTopologyReader.LoadFile(
                malformed,
                Binding(ReadExampleIndexes().Truth.ReleaseDigest)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CandidatePipelineWithoutPublicationMarksContextUnavailable()
    {
        (PaperTruthIndex truth, PaperIntuitionIndex intuition) = ReadExampleIndexes();

        CandidateProposalArtifacts proposal = Assert.Single(
            CandidatePipeline.Propose(truth, intuition));

        Assert.Equal("unavailable", proposal.CandidatePaper.StructuralContext.Availability);
        Assert.Empty(proposal.CandidatePaper.StructuralContext.KeyNodes);
        Assert.Equal(
            proposal.CandidatePaper.StructuralContext,
            proposal.LiteratureResearch.StructuralContext);
    }

    private static CertifiedTopologyBinding Binding(string releaseDigest) => new(
        releaseDigest,
        "sha256:" + new string('a', 64),
        new string('c', 40));

    private static byte[] FixtureBytes() => File.ReadAllBytes(Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "certified-topology.v1.json"));

    private static (PaperTruthIndex Truth, PaperIntuitionIndex Intuition)
        ReadExampleIndexes()
    {
        string root = FindRoot();
        PaperTruthIndex truth = PaperTruthIndex.Build(
            PaperPortJson.ReadTruthReleasePort(File.ReadAllBytes(Path.Combine(
                root,
                "Papers/example/paper-truth-release-port.v1.json"))));
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(
            PaperPortJson.ReadIntuitionPort(File.ReadAllBytes(Path.Combine(
                root,
                "Papers/example/paper-intuition-port.v1.json"))),
            truth);
        return (truth, intuition);
    }

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Trureturing.Paper.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
