namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryDeepeningAgentWiringTests
{
    [Fact]
    public void A2BusinessEventStagesTheGenericNativeAgentTask()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "dispatch-theory-deepening-agent",
            "main.lua"));

        Assert.Contains("paper_theory_deepening_requested", source, StringComparison.Ordinal);
        Assert.Contains("stage-deepening-task", source, StringComparison.Ordinal);
        Assert.Contains("register-task", source, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", source, StringComparison.Ordinal);
        Assert.Contains("paper-theory-developer", source, StringComparison.Ordinal);
        Assert.Contains("contextual-theory-execution", source, StringComparison.Ordinal);
        Assert.DoesNotContain("spawn_codex_sync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("exec_argv", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A2CompletedResultIsAdmittedBeforeDomainEventsAreRaised()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "admit-theory-deepening-agent",
            "main.lua"));

        Assert.Contains("admit-deepening-result", source, StringComparison.Ordinal);
        Assert.Contains("paper_theory_deepening_ready", source, StringComparison.Ordinal);
        Assert.Contains("paper_candidate_split_proposed", source, StringComparison.Ordinal);
        Assert.Contains("paper_candidate_merge_research_requested", source, StringComparison.Ordinal);
        Assert.Contains("paper_research_ledger_entry_ready", source, StringComparison.Ordinal);
        Assert.Contains("paper-theory-deepening-ready.v1", source, StringComparison.Ordinal);
        Assert.Contains("paper-theory-deepening-delta.v1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("schema = admitted.theorem_package.schema", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A2FailureRoutesAreStatusSpecific()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "route-theory-deepening-agent-failure",
            "main.lua"));

        Assert.Contains("paper_theory_deepening_no_progress", source, StringComparison.Ordinal);
        Assert.Contains("paper_theory_deepening_blocked", source, StringComparison.Ordinal);
        Assert.Contains("[\"no-progress\"] = \"theory-deepening\"", source, StringComparison.Ordinal);
        Assert.Contains("[\"blocked\"] = \"blocked\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentCliExposesA2StagingAndAdmission()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.Agent.Cli",
            "Program.cs"));

        Assert.Contains("stage-deepening-task", source, StringComparison.Ordinal);
        Assert.Contains("admit-deepening-result", source, StringComparison.Ordinal);
        Assert.Contains("PaperTheoryDeepeningAgentService.StageTask", source, StringComparison.Ordinal);
        Assert.Contains("PaperTheoryDeepeningAgentService.AdmitResult", source, StringComparison.Ordinal);
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
