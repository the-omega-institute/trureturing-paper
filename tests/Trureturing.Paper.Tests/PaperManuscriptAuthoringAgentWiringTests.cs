namespace Trureturing.Paper.Tests;

public sealed class PaperManuscriptAuthoringAgentWiringTests
{
    [Fact]
    public void FkstDispatchesAndAdmitsNativeManuscriptAgents()
    {
        string root = FindRepositoryRoot();
        string dispatch = Read(root,
            ".fkst/local-packages/trureturing-paper/departments/dispatch-manuscript-authoring-agent/main.lua");
        string admit = Read(root,
            ".fkst/local-packages/trureturing-paper/departments/admit-manuscript-authoring-agent/main.lua");
        string failure = Read(root,
            ".fkst/local-packages/trureturing-paper/departments/route-manuscript-authoring-agent-failure/main.lua");
        string agentCli = Read(root,
            "src/Trureturing.Paper.Agent.Cli/Program.cs");
        string runtime = Read(root,
            ".fkst/local-packages/trureturing-paper/departments/run-codex-agent/main.lua");

        Assert.Contains("paper_certified_claim_manifest_ready", dispatch, StringComparison.Ordinal);
        Assert.Contains("stage-manuscript-authoring-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("register-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("spawn_codex", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run", dispatch, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("paper_agent_task_completed", admit, StringComparison.Ordinal);
        Assert.Contains("admit-manuscript-authoring-result", admit, StringComparison.Ordinal);
        Assert.Contains("paper_scientific_manuscript_ready", admit, StringComparison.Ordinal);
        Assert.DoesNotContain("spawn_codex", admit, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run", admit, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("paper_agent_task_no_progress", failure, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_blocked", failure, StringComparison.Ordinal);
        Assert.Contains("paper_manuscript_authoring_retry_requested", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("spawn_codex", failure, StringComparison.Ordinal);

        Assert.Contains("stage-manuscript-authoring-task", agentCli, StringComparison.Ordinal);
        Assert.Contains("admit-manuscript-authoring-result", agentCli, StringComparison.Ordinal);
        Assert.Contains("spawn_codex_sync", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void ManuscriptAuthoringContractsAreStrictAndRepositoryOwned()
    {
        string root = FindRepositoryRoot();
        foreach (string file in new[]
        {
            "paper-manuscript-authoring-agent-dispatch.v1.schema.json",
            "paper-scientific-manuscript-draft.v1.schema.json",
            "paper-manuscript-authoring-agent-task-staged.v1.schema.json",
            "paper-scientific-manuscript.v1.schema.json",
            "paper-manuscript-authoring-agent-cursor.v1.schema.json",
            "paper-manuscript-authoring-agent-result-admitted.v1.schema.json",
            "paper-scientific-manuscript-ready.v1.schema.json",
            "paper-manuscript-authoring-agent-failure.v1.schema.json",
            "paper-manuscript-authoring-retry-requested.v1.schema.json"
        })
        {
            string path = Path.Combine(root, "contracts", file);
            Assert.True(File.Exists(path), $"Missing manuscript authoring contract {file}.");
            string source = File.ReadAllText(path);
            Assert.Contains("\"additionalProperties\": false", source, StringComparison.Ordinal);
        }

        string draft = Read(root,
            "contracts/paper-scientific-manuscript-draft.v1.schema.json");
        string manuscript = Read(root,
            "contracts/paper-scientific-manuscript.v1.schema.json");
        Assert.Contains("\"minItems\": 8", draft, StringComparison.Ordinal);
        Assert.Contains("\"formal-claim\"", draft, StringComparison.Ordinal);
        Assert.Contains("\"proof\"", draft, StringComparison.Ordinal);
        Assert.Contains("paper-scientific-manuscript.v1", manuscript, StringComparison.Ordinal);
        Assert.Contains("requested_statement_digest", manuscript, StringComparison.Ordinal);
        Assert.Contains("statement_id", manuscript, StringComparison.Ordinal);
        Assert.Contains("certified_claim_ref", manuscript, StringComparison.Ordinal);
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(
            root,
            relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        foreach (DirectoryInfo start in new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory)
        })
        {
            for (DirectoryInfo? current = start;
                 current is not null;
                 current = current.Parent)
            {
                if (File.Exists(Path.Combine(
                        current.FullName,
                        "Trureturing.Paper.slnx")))
                {
                    return current.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the trureturing-paper repository root.");
    }
}
