using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class TruthAndIntuitionIndexTests
{
    [Fact]
    public void TruthIndexBuildsExactPrerequisiteClosure()
    {
        PaperTruthIndex index = PaperTruthIndex.Build(TruthPort());

        IReadOnlyList<PaperTruthEntry> closure =
            index.PrerequisiteClosure("C.theorem");

        Assert.Equal(
            new[] { "A.theorem", "B.theorem" },
            closure.Select(entry => entry.DeclarationId).ToArray());
    }

    [Fact]
    public void AxiomPolicyIsDeclarationSpecific()
    {
        PaperTruthIndex index = PaperTruthIndex.Build(TruthPort());

        Assert.True(index.UsesOnlyAxioms(
            "A.theorem",
            new HashSet<string>(StringComparer.Ordinal)));
        Assert.False(index.UsesOnlyAxioms(
            "B.theorem",
            new HashSet<string>(StringComparer.Ordinal)));
        Assert.True(index.UsesOnlyAxioms(
            "B.theorem",
            new HashSet<string>(new[] { "Classical.choice" }, StringComparer.Ordinal)));
    }

    [Fact]
    public void IntuitionCandidatesRemainASeparateAdvisoryIndex()
    {
        PaperTruthIndex truth = PaperTruthIndex.Build(TruthPort());
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(
            new PaperIntuitionPort(
                PaperPortSchemas.IntuitionPort,
                truth.ReleaseDigest,
                new[]
                {
                    new PaperIntuitionCandidatePort(
                        "proposal-1",
                        "bridge",
                        "proposed",
                        new[] { "A.theorem" },
                        new[] { "C.theorem" },
                        new[] { "evidence:1" },
                        "find a counterexample",
                        2.0,
                        0.0)
                }),
            truth);

        Assert.Single(intuition.UnsettledCandidates());
        Assert.Equal(3, truth.Declarations.Count);
        Assert.Throws<ClaimGateException>(
            () => truth.GetDeclaration("proposal-1"));
    }

    [Fact]
    public void PortRejectsMissingFrozenPrerequisite()
    {
        PaperTruthReleasePort port = TruthPort() with
        {
            Declarations = new[]
            {
                TruthPort().Declarations[0] with
                {
                    PrerequisiteFrozenNodeIds = new[] { Hash('f') }
                }
            }
        };

        byte[] bytes = PaperPortJson.Write(port);
        ClaimGateException error = Assert.Throws<ClaimGateException>(
            () => PaperPortJson.ReadTruthReleasePort(bytes));

        Assert.Contains("absent", error.Message, StringComparison.Ordinal);
    }

    private static PaperTruthReleasePort TruthPort()
    {
        return new PaperTruthReleasePort(
            PaperPortSchemas.TruthReleasePort,
            Sha('1'),
            new string('a', 40),
            new string('b', 40),
            new[]
            {
                new PaperDeclarationPort(
                    "A.theorem",
                    Sha('2'),
                    Hash('a'),
                    "A.lean",
                    "theorem",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "Blueprint/A.html"),
                new PaperDeclarationPort(
                    "B.theorem",
                    Sha('3'),
                    Hash('b'),
                    "B.lean",
                    "theorem",
                    new[] { Hash('a') },
                    new[] { "Classical.choice" },
                    "Blueprint/B.html"),
                new PaperDeclarationPort(
                    "C.theorem",
                    Sha('4'),
                    Hash('c'),
                    "C.lean",
                    "theorem",
                    new[] { Hash('b') },
                    Array.Empty<string>(),
                    null)
            });
    }

    private static string Sha(char value) => "sha256:" + new string(value, 64);
    private static string Hash(char value) => "sha256:" + new string(value, 64);
}
