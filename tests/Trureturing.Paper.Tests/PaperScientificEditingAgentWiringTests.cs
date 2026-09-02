namespace Trureturing.Paper.Tests;

public sealed class PaperScientificEditingAgentWiringTests
{
    [Fact]
    public void FkstDispatchesAndAdmitsScientificEditorThroughSharedRuntime()
    {
        string root = FindRepositoryRoot();
        string dispatch = Read(root,
            ".fkst/local-packages/trureturing-paper/departments/dispatch-scientific-editing-agent/main.lua");
        string admit = Read(root,
            ".fkst/local-packages/trureturing-paper/departments/admit-scientific-editing-agent/main.lua");
        string failure = Read(root,
            ".fkst/local-packages/trureturing-paper/departments/route-scientific-editing-agent-failure/main.lua");
        string cli = Read(root,
            "src/Trureturing.Paper.Agent.Cli/Program.cs");

        Assert.Contains("paper_scientific_manuscript_ready", dispatch, StringComparison.Ordinal);
        Assert.Contains("stage-scientific-editing-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper-scientific-editor", dispatch, StringComparison.Ordinal);
        Assert.Contains("claim-preserving-edit", dispatch, StringComparison.Ordinal);

        Assert.Contains("paper_agent_task_completed", admit, StringComparison.Ordinal);
        Assert.Contains("admit-scientific-editing-result", admit, StringComparison.Ordinal);
        Assert.Contains("paper_scientifically_edited_manuscript_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper-scientific-edit-delta.v1", admit, StringComparison.Ordinal);
        Assert.Contains("paper-scientifically-edited-manuscript.v1", admit, StringComparison.Ordinal);

        Assert.Contains("paper_agent_task_no_progress", failure, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_blocked", failure, StringComparison.Ordinal);
        Assert.Contains("paper_scientific_editing_retry_requested", failure, StringComparison.Ordinal);
        Assert.Contains("paper_scientific_editing_blocked", failure, StringComparison.Ordinal);

        Assert.Contains("stage-scientific-editing-task", cli, StringComparison.Ordinal);
        Assert.Contains("admit-scientific-editing-result", cli, StringComparison.Ordinal);
        Assert.Contains("StageScientificEditingTask", cli, StringComparison.Ordinal);
        Assert.Contains("AdmitScientificEditingResult", cli, StringComparison.Ordinal);

        foreach (string source in new[] { dispatch, admit, failure })
        {
            Assert.DoesNotContain("spawn_codex", source, StringComparison.Ordinal);
            Assert.DoesNotContain("dotnet run", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("git ", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ScientificEditingContractsArePresentAndStrict()
    {
        string root = FindRepositoryRoot();
        string[] contracts =
        [
            "paper-scientific-editing-agent-dispatch.v1.schema.json",
            "paper-scientific-edit-draft.v1.schema.json",
            "paper-scientific-edit-delta.v1.schema.json",
            "paper-scientifically-edited-manuscript.v1.schema.json",
            "paper-scientific-editing-agent-task-staged.v1.schema.json",
            "paper-scientific-editing-agent-cursor.v1.schema.json",
            "paper-scientific-editing-agent-result-admitted.v1.schema.json",
            "paper-scientifically-edited-manuscript-ready.v1.schema.json",
            "paper-scientific-editing-agent-failure.v1.schema.json",
            "paper-scientific-editing-retry-requested.v1.schema.json"
        ];

        foreach (string contract in contracts)
        {
            string path = Path.Combine(root, "contracts", contract);
            Assert.True(File.Exists(path), $"Missing scientific-editing contract {contract}.");
            string source = File.ReadAllText(path);
            Assert.Contains("\"additionalProperties\": false", source, StringComparison.Ordinal);
        }

        string draft = File.ReadAllText(Path.Combine(
            root,
            "contracts",
            "paper-scientific-edit-draft.v1.schema.json"));
        Assert.Contains("\"minItems\": 8", draft, StringComparison.Ordinal);
        Assert.Contains("\"maxItems\": 8", draft, StringComparison.Ordinal);
        Assert.Contains("\"proof-exposition\"", draft, StringComparison.Ordinal);
        Assert.Contains("\"limitations-and-implications\"", draft, StringComparison.Ordinal);

        string delta = File.ReadAllText(Path.Combine(
            root,
            "contracts",
            "paper-scientific-edit-delta.v1.schema.json"));
        Assert.Contains("\"claim_identity_preserved\": { \"const\": true }", delta, StringComparison.Ordinal);
        Assert.Contains("\"evidence_boundary_preserved\": { \"const\": true }", delta, StringComparison.Ordinal);
        Assert.Contains("\"passed\": { \"const\": true }", delta, StringComparison.Ordinal);
    }

    private static string Read(string root, string path) =>
        File.ReadAllText(Path.Combine(
            root,
            path.Replace('/', Path.DirectorySeparatorChar)));

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
