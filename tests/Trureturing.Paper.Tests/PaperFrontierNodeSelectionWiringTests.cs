namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierNodeSelectionWiringTests
{
    [Fact]
    public void FkstConvertsExactFrontierRoutesIntoFormalizeRequests()
    {
        string root = FindRepositoryRoot();
        string selection = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "select-frontier-node",
            "main.lua"));
        string dispatch = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "dispatch-formalization",
            "main.lua"));
        string researchCore = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "research_core.lua"));
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.FrontierSelection.Cli",
            "Program.cs"));

        Assert.Contains("paper_frontier_node_selection_requested", selection, StringComparison.Ordinal);
        Assert.Contains("paper_frontier_node_selection_ready", selection, StringComparison.Ordinal);
        Assert.Contains("formalization_request_ready", selection, StringComparison.Ordinal);
        Assert.Contains("admit-frontier-node-selection", selection, StringComparison.Ordinal);
        Assert.Contains("paper-frontier-state:v1:", selection, StringComparison.Ordinal);
        Assert.Contains("with_lock", selection, StringComparison.Ordinal);
        Assert.Contains("paper-frontier-governance", selection, StringComparison.Ordinal);
        Assert.Contains("frontier_binding_ref", selection, StringComparison.Ordinal);
        Assert.Contains("formalization_request_ready", dispatch, StringComparison.Ordinal);
        Assert.Contains("frontier_selection_cli", researchCore, StringComparison.Ordinal);
        Assert.Contains("admit-frontier-node-selection", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("spawn_codex_sync", selection, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run", selection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FrontierSelectionContractsAreCommitted()
    {
        string root = FindRepositoryRoot();
        string[] contracts =
        [
            "paper-frontier-node-selection-authorization.v1.schema.json",
            "paper-frontier-verification-budget.v1.schema.json",
            "paper-frontier-current-state-cursor.v1.schema.json",
            "paper-frontier-formalization-binding.v1.schema.json",
            "paper-frontier-formalization-binding-lookup.v1.schema.json",
            "paper-frontier-node-selection-cursor.v1.schema.json",
            "paper-frontier-node-selection-admitted.v1.schema.json",
            "paper-frontier-node-selection-ready.v1.schema.json"
        ];

        Assert.All(
            contracts,
            contract => Assert.True(
                File.Exists(Path.Combine(root, "contracts", contract)),
                $"Missing frontier-selection contract {contract}."));
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
        throw new DirectoryNotFoundException(
            "Could not locate the Paper repository root.");
    }
}
