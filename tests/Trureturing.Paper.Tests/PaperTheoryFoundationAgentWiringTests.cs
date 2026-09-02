namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryFoundationAgentWiringTests
{
    [Fact]
    public void ReadyEventsUseTheirOwnSchemaInsteadOfImpersonatingDomainArtifacts()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "admit-theory-foundation-agent",
            "main.lua"));

        Assert.Contains(
            "schema = \"paper-theory-foundation-ready.v1\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains("domain_schema = admitted.domain_schema", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\n    schema = admitted.domain_schema",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoProgressAndBlockedResultsHaveStatusSpecificRoutes()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "route-theory-foundation-agent-failure",
            "main.lua"));

        Assert.Contains("expected_routes", source, StringComparison.Ordinal);
        Assert.Contains(
            "[\"theory-scope:no-progress\"] = \"theory-scope\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[\"theory-inventory:blocked\"] = \"blocked\"",
            source,
            StringComparison.Ordinal);
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
                if (File.Exists(Path.Combine(directory.FullName, "Trureturing.Paper.slnx")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the Paper repository root.");
    }
}
