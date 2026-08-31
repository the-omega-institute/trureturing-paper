namespace Trureturing.Paper.Tests;

public sealed class PaperPortfolioJudgmentAgentWiringTests
{
    [Fact]
    public void FkstDepartmentsStageAdmitAndRoutePortfolioJudgment()
    {
        string root = FindRepositoryRoot();
        string dispatch = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "dispatch-portfolio-judgment-agent",
            "main.lua"));
        string admit = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "admit-portfolio-judgment-agent",
            "main.lua"));
        string failure = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "route-portfolio-judgment-agent-failure",
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

        Assert.Contains("paper_portfolio_judgment_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("stage-portfolio-judgment-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_portfolio_judgment_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper_formalization_frontier_requested", admit, StringComparison.Ordinal);
        Assert.Contains("paper_theory_deepening_requested", admit, StringComparison.Ordinal);
        Assert.Contains("paper_candidate_split_requested", admit, StringComparison.Ordinal);
        Assert.Contains("paper_candidate_merge_requested", admit, StringComparison.Ordinal);
        Assert.Contains("paper_candidate_held", admit, StringComparison.Ordinal);
        Assert.Contains("paper_portfolio_judgment_retry_requested", failure, StringComparison.Ordinal);
        Assert.Contains("spawn_codex_sync", runtime, StringComparison.Ordinal);
        Assert.Contains("agent.execute", runner, StringComparison.Ordinal);
        Assert.Contains("stage-portfolio-judgment-task", cli, StringComparison.Ordinal);
        Assert.Contains("admit-portfolio-judgment-result", cli, StringComparison.Ordinal);
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
