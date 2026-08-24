using System.Text;
using Trureturing.Paper.Cli;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class ExampleProducePublishTests
{
    [Fact]
    public void LocalAdapterProducesTypedCertifiedAndAdvisoryPorts()
    {
        string root = FindRoot();
        LocalDevRelease release = LocalDevTruthReleaseAdapter.Read(
            Path.Combine(root, "Papers", "frozen-bundle"));

        PaperTruthReleasePort truthPort = PaperPortJson.ReadTruthReleasePort(
            PaperPortJson.Write(release.TruthPort));
        PaperIntuitionPort intuitionPort = PaperPortJson.ReadIntuitionPort(
            PaperPortJson.Write(release.IntuitionPort));
        PaperTruthIndex truth = PaperTruthIndex.Build(truthPort);
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(intuitionPort, truth);

        PaperTruthEntry theorem = truth.GetDeclaration(
            ExamplePaperPublisher.CertifiedDeclarationId);
        Assert.Equal(new[] { "propext" }, theorem.AxiomClosure);
        Assert.EndsWith("TraceConjugation.lean", theorem.RepoPath, StringComparison.Ordinal);
        Assert.Equal(2, intuition.Candidates.Count);
        foreach (PaperIntuitionEntry candidate in intuition.Candidates)
        {
            Assert.Throws<ClaimGateException>(() =>
                truth.GetDeclaration(candidate.ProposalId));
        }
    }

    [Fact]
    public void FullExampleCycleAssemblesClaimGatedPaperAndReadingSite()
    {
        string root = FindRoot();
        LocalDevRelease release = LocalDevTruthReleaseAdapter.Read(
            Path.Combine(root, "Papers", "frozen-bundle"));
        ExamplePaperArtifacts artifacts = ExamplePaperPublisher.Produce(
            release.TruthPort,
            release.IntuitionPort,
            release.FrozenInputs);

        string latex = Encoding.UTF8.GetString(artifacts.Latex);
        string html = Encoding.UTF8.GetString(artifacts.Html);
        Assert.Contains("D5/S0/Carrier/TraceConjugation.trace_conj", latex, StringComparison.Ordinal);
        Assert.Contains("Trace Invariance Under Conjugation", html, StringComparison.Ordinal);
        Assert.Contains("Certified result", html, StringComparison.Ordinal);
        Assert.Contains("Axiom closure</dt><dd>propext", html, StringComparison.Ordinal);
        Assert.Contains("Research directions", html, StringComparison.Ordinal);
        Assert.Contains("Advisory, not certified.", html, StringComparison.Ordinal);
        Assert.Contains(release.TruthPort.ReleaseDigest, html, StringComparison.Ordinal);
        Assert.Contains("certified declarations</dt><dd>1", html, StringComparison.Ordinal);
        Assert.Contains("advisory candidates</dt><dd>2", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AdvisoryCandidateCannotEnterTheClaimGateAsFact()
    {
        string root = FindRoot();
        LocalDevRelease release = LocalDevTruthReleaseAdapter.Read(
            Path.Combine(root, "Papers", "frozen-bundle"));
        string proposalId = release.IntuitionPort.Candidates[0].ProposalId;
        var recipe = new PaperRecipe(
            "recipe.v1",
            "invalid-advisory-paper",
            "Invalid advisory paper",
            [new RecipeClaim(proposalId, "describe:trace-invariance-under-conjugation")]);

        ClaimGateException error = Assert.Throws<ClaimGateException>(() =>
            PaperAssembler.AssembleDocument(recipe, release.FrozenInputs));

        Assert.Contains("absent", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublisherRejectsPortProvenanceOrCitationDrift()
    {
        string root = FindRoot();
        LocalDevRelease release = LocalDevTruthReleaseAdapter.Read(
            Path.Combine(root, "Papers", "frozen-bundle"));
        PaperTruthReleasePort wrongTree = release.TruthPort with
        {
            SourceTree = new string('0', 40)
        };
        PaperTruthReleasePort wrongPath = release.TruthPort with
        {
            Declarations = release.TruthPort.Declarations.Select(declaration =>
                declaration with { RepoPath = "D5/S0/Carrier/Wrong.lean" }).ToArray()
        };

        Assert.Throws<ClaimGateException>(() => ExamplePaperPublisher.Produce(
            wrongTree,
            release.IntuitionPort,
            release.FrozenInputs));
        Assert.Throws<ClaimGateException>(() => ExamplePaperPublisher.Produce(
            wrongPath,
            release.IntuitionPort,
            release.FrozenInputs));
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
