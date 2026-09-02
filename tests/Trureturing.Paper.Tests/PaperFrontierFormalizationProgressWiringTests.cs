namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierFormalizationProgressWiringTests
{
    [Fact]
    public void FkstWritesTransportOutcomeAndCertificationBackToFrontier()
    {
        string root = FindRepositoryRoot();
        string package = Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper");
        string dispatch = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "dispatch-formalization",
            "main.lua"));
        string classify = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "classify-formalization-outcome",
            "main.lua"));
        string certify = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "evaluate-certification-release",
            "main.lua"));
        string core = File.ReadAllText(Path.Combine(
            package,
            "research_core.lua"));
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.FrontierLifecycle.Cli",
            "Program.cs"));

        Assert.Contains("record-transport", dispatch, StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_formalize_transport_ready",
            dispatch,
            StringComparison.Ordinal);
        Assert.Contains("record-outcome", classify, StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_formalization_outcome_ready",
            classify,
            StringComparison.Ordinal);
        Assert.Contains(
            "record-certification",
            certify,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_certified_claim_manifest_ready",
            certify,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_ready_set_ready",
            certify,
            StringComparison.Ordinal);
        Assert.Contains(
            "Trureturing.Paper.FrontierLifecycle.Cli.dll",
            core,
            StringComparison.Ordinal);
        Assert.Contains("record-transport", cli, StringComparison.Ordinal);
        Assert.Contains("record-outcome", cli, StringComparison.Ordinal);
        Assert.Contains("record-certification", cli, StringComparison.Ordinal);

        foreach (string source in new[] { dispatch, classify, certify })
        {
            Assert.DoesNotContain(
                "dotnet run",
                source,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FrontierFormalizationProgressContractsAreCommitted()
    {
        string root = FindRepositoryRoot();
        string[] contracts =
        [
            "paper-frontier-formalize-transport-cursor.v1.schema.json",
            "paper-frontier-formalization-outcome-cursor.v1.schema.json",
            "paper-frontier-certified-claim-manifest.v1.schema.json",
            "paper-frontier-ready-set.v1.schema.json",
            "paper-frontier-certification-cursor.v1.schema.json",
            "paper-frontier-formalize-transport-ready.v1.schema.json",
            "paper-frontier-formalization-outcome-ready.v1.schema.json",
            "paper-frontier-certification-ready.v1.schema.json",
            "paper-frontier-ready-set-ready.v1.schema.json"
        ];

        Assert.All(
            contracts,
            contract => Assert.True(
                File.Exists(Path.Combine(root, "contracts", contract)),
                $"Missing frontier formalization progress contract {contract}."));
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(
                        directory.FullName,
                        "Trureturing.Paper.slnx")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Paper repository root.");
    }
}
