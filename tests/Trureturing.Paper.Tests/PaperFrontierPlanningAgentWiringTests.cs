namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierPlanningAgentWiringTests
{
    [Fact]
    public void FkstRoutesPromotionsThroughNativePlannerAndWaveZeroAdmission()
    {
        string root = FindRepositoryRoot();
        string dispatch = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "dispatch-frontier-planning-agent",
            "main.lua"));
        string admit = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "admit-frontier-planning-agent",
            "main.lua"));
        string failure = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "route-frontier-planning-agent-failure",
            "main.lua"));
        string runner = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "run-codex-agent",
            "main.lua"));
        string runtime = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "agent_runtime.lua"));
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.Agent.Cli",
            "Program.cs"));

        Assert.Contains("paper_formalization_frontier_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("stage-frontier-planning-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("promote-to-frontier", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper-formalization-frontier-planner", dispatch, StringComparison.Ordinal);
        Assert.Contains("promotion-bound-planning", dispatch, StringComparison.Ordinal);

        Assert.Contains("admit-frontier-planning-result", admit, StringComparison.Ordinal);
        Assert.Contains("paper_formalization_frontier_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper_frontier_node_selection_requested", admit, StringComparison.Ordinal);
        Assert.Contains("parallel_wave ~= 0", admit, StringComparison.Ordinal);
        Assert.Contains("governed-selection", admit, StringComparison.Ordinal);

        Assert.Contains("paper_frontier_planning_no_progress", failure, StringComparison.Ordinal);
        Assert.Contains("paper_frontier_planning_blocked", failure, StringComparison.Ordinal);
        Assert.Contains("paper_frontier_planning_retry_requested", failure, StringComparison.Ordinal);

        Assert.Contains("spawn_codex_sync", runtime, StringComparison.Ordinal);
        Assert.Contains("agent.execute", runner, StringComparison.Ordinal);
        Assert.Contains("stage-frontier-planning-task", cli, StringComparison.Ordinal);
        Assert.Contains("admit-frontier-planning-result", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run", dispatch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet run", admit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FrontierPlanningContractsAreCommitted()
    {
        string root = FindRepositoryRoot();
        string[] contracts =
        [
            "paper-frontier-planning-agent-dispatch.v1.schema.json",
            "paper-formalization-frontier-draft.v1.schema.json",
            "paper-frontier-planning-agent-task-staged.v1.schema.json",
            "paper-frontier-planning-agent-cursor.v1.schema.json",
            "paper-frontier-planning-agent-result-admitted.v1.schema.json",
            "paper-formalization-frontier-ready.v1.schema.json",
            "paper-frontier-node-selection-requested.v1.schema.json",
            "paper-frontier-planning-agent-failure.v1.schema.json"
        ];

        Assert.All(
            contracts,
            contract => Assert.True(
                File.Exists(Path.Combine(root, "contracts", contract)),
                $"Missing frontier-planning contract {contract}."));
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
