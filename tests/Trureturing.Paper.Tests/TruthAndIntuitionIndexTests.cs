using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class TruthAndIntuitionIndexTests
{
    [Fact]
    public void TruthIndexBuildsExactPrerequisiteClosure()
    {
        PaperTruthIndex index = BuildTruthIndex();

        IReadOnlyList<PaperTruthEntry> closure =
            index.PrerequisiteClosure("C.theorem");

        Assert.Equal(
            new[] { "A.theorem", "B.theorem" },
            closure.Select(entry => entry.DeclarationId).ToArray());
    }

    [Fact]
    public void AxiomPolicyIsDeclarationSpecific()
    {
        PaperTruthIndex index = BuildTruthIndex();

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
        PaperTruthIndex truth = BuildTruthIndex();
        PaperIntuitionPort port = ReadIntuitionPort(IntuitionPort(truth.ReleaseDigest));
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(
            port,
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

        ClaimGateException error = Assert.Throws<ClaimGateException>(
            () => PaperTruthIndex.Build(port));

        Assert.Contains("absent", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TruthIndexRejectsWrongSchemaDigestAndMalformedIdentity()
    {
        PaperTruthReleasePort valid = TruthPort();

        Assert.Throws<ClaimGateException>(() => PaperTruthIndex.Build(
            valid with { Schema = "paper-truth-release-port.v2" }));
        Assert.Throws<ClaimGateException>(() => PaperTruthIndex.Build(
            valid with { ReleaseDigest = "not-a-digest" }));
        Assert.Throws<ClaimGateException>(() => PaperTruthIndex.Build(
            valid with
            {
                Declarations = valid.Declarations.Select((declaration, index) =>
                    index == 0
                        ? declaration with { StatementId = "not-an-identity" }
                        : declaration).ToArray()
            }));
    }

    [Fact]
    public void TruthIndexRejectsCyclicProofGraph()
    {
        PaperTruthReleasePort valid = TruthPort();
        PaperTruthReleasePort cyclic = valid with
        {
            Declarations = valid.Declarations.Select((declaration, index) =>
                index == 0
                    ? declaration with
                    {
                        PrerequisiteFrozenNodeIds = new[] { Hash('c') }
                    }
                    : declaration).ToArray()
        };

        ClaimGateException error = Assert.Throws<ClaimGateException>(
            () => PaperTruthIndex.Build(cyclic));

        Assert.Contains("cycle", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IntuitionIndexRejectsInvalidRawPortBeforeReleaseBinding()
    {
        PaperTruthIndex truth = BuildTruthIndex();
        PaperIntuitionPort valid = IntuitionPort(truth.ReleaseDigest);

        Assert.Throws<ClaimGateException>(() => PaperIntuitionIndex.Build(
            valid with { Schema = "paper-intuition-port.v2" },
            truth));
        Assert.Throws<ClaimGateException>(() => PaperIntuitionIndex.Build(
            valid with { SourceTruthReleaseDigest = "not-a-digest" },
            truth));
        Assert.Throws<ClaimGateException>(() => PaperIntuitionIndex.Build(
            valid with
            {
                Candidates = new[]
                {
                    valid.Candidates[0] with { Status = "certified" }
                }
            },
            truth));
    }

    private static PaperTruthIndex BuildTruthIndex() =>
        PaperTruthIndex.Build(ReadTruthPort(TruthPort()));

    private static PaperTruthReleasePort ReadTruthPort(PaperTruthReleasePort port) =>
        PaperPortJson.ReadTruthReleasePort(PaperPortJson.Write(port));

    private static PaperIntuitionPort ReadIntuitionPort(PaperIntuitionPort port) =>
        PaperPortJson.ReadIntuitionPort(PaperPortJson.Write(port));

    private static PaperIntuitionPort IntuitionPort(string releaseDigest) =>
        new(
            PaperPortSchemas.IntuitionPort,
            releaseDigest,
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
            });

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
