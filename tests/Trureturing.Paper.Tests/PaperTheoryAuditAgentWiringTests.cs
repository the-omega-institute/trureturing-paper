namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryAuditAgentWiringTests
{
    [Fact]
    public void FkstDepartmentsFanOutFreshReviewersAndAggregateResults()
    {
        string root = FindRepositoryRoot();
        string dispatch = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "dispatch-theory-audit-agents",
            "main.lua"));
        string admit = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "admit-theory-audit-agent",
            "main.lua"));
        string failure = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "route-theory-audit-agent-failure",
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

        Assert.Contains("paper_theory_audit_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("stage-audit-tasks", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("reviewer_slot", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_theory_audit_opinion_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper_theory_audit_waiting", admit, StringComparison.Ordinal);
        Assert.Contains("paper_theory_audit_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper_candidate_scorecard_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper_theory_audit_reviewer_replacement_requested", failure, StringComparison.Ordinal);
        Assert.Contains("spawn_codex_sync", runtime, StringComparison.Ordinal);
        Assert.Contains("agent.execute", runner, StringComparison.Ordinal);
        Assert.Contains("stage-audit-tasks", cli, StringComparison.Ordinal);
        Assert.Contains("admit-audit-opinion", cli, StringComparison.Ordinal);
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
