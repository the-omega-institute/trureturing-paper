namespace Trureturing.Paper.Tests;

public sealed class PaperJournalResearchAgentWiringTests
{
    [Fact]
    public void FkstDepartmentsAndAgentCliAreWired()
    {
        string root = RepositoryRoot();
        string dispatch = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "dispatch-journal-research-agent",
            "main.lua"));
        string admit = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "admit-journal-research-agent",
            "main.lua"));
        string failure = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "route-journal-research-agent-failure",
            "main.lua"));
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.Agent.Cli",
            "Program.cs"));

        Assert.Contains("paper_scientifically_edited_manuscript_ready", dispatch);
        Assert.Contains("stage-journal-research-task", dispatch);
        Assert.Contains("paper_agent_task_requested", dispatch);
        Assert.Contains("admit-journal-research-result", admit);
        Assert.Contains("paper_journal_target_ready", admit);
        Assert.Contains("selected_publication_tier > 2", admit);
        Assert.Contains("paper_journal_research_retry_requested", failure);
        Assert.Contains("stage-journal-research-task", cli);
        Assert.Contains("admit-journal-research-result", cli);
        string[] contracts =
        [
            "paper-journal-research-agent-dispatch.v1.schema.json",
            "paper-journal-research-draft.v1.schema.json",
            "paper-journal-research-dossier.v1.schema.json",
            "paper-journal-venue-scorecard.v1.schema.json",
            "paper-journal-target-selection.v1.schema.json",
            "paper-journal-research-agent-task-staged.v1.schema.json",
            "paper-journal-research-agent-cursor.v1.schema.json",
            "paper-journal-research-agent-result-admitted.v1.schema.json",
            "paper-journal-target-ready.v1.schema.json",
            "paper-journal-research-agent-failure.v1.schema.json",
            "paper-journal-research-retry-requested.v1.schema.json"
        ];
        foreach (string contract in contracts)
        {
            string text = File.ReadAllText(Path.Combine(root, "contracts", contract));
            Assert.Contains("\"additionalProperties\": false", text);
            Assert.Contains("https://json-schema.org/draft/2020-12/schema", text);
        }
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "fkst.workspace.toml")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
