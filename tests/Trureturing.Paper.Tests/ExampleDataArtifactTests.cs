using System.Text;
using Trureturing.Paper.Cli;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class ExampleDataArtifactTests
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
            ExamplePaperAssembler.CertifiedDeclarationId);
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
    public void FullExampleAssemblyProducesReproducibleClaimGatedData()
    {
        string root = FindRoot();
        LocalDevRelease release = LocalDevTruthReleaseAdapter.Read(
            Path.Combine(root, "Papers", "frozen-bundle"));

        byte[] latex = ExamplePaperAssembler.Assemble(
            release.TruthPort,
            release.IntuitionPort,
            release.FrozenInputs);

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(root, "Papers", "example", "paper.tex")),
            latex);
        Assert.Contains(
            ExamplePaperAssembler.CertifiedDeclarationId,
            Encoding.UTF8.GetString(latex),
            StringComparison.Ordinal);
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
    public void ExampleAssemblerRejectsPortProvenanceOrCitationDrift()
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

        Assert.Throws<ClaimGateException>(() => ExamplePaperAssembler.Assemble(
            wrongTree,
            release.IntuitionPort,
            release.FrozenInputs));
        Assert.Throws<ClaimGateException>(() => ExamplePaperAssembler.Assemble(
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
