namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierCompletionWiringTests
{
    [Fact]
    public void FkstCompletesFrontiersOnClaimsAndLaterReleases()
    {
        string root = FindRepositoryRoot();
        string department = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "complete-frontier",
            "main.lua"));
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.FrontierSelection.Cli",
            "Program.cs"));

        Assert.Contains(
            "paper_frontier_certified_claim_manifest_ready",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_certification_release_registered",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_completion_ready",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_completion_pending",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_manuscript_plan_registered",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_manuscript_claim_evaluation_requested",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "evaluate-frontier-completion",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "list-frontier-completion-candidates",
            department,
            StringComparison.Ordinal);
        Assert.Contains("with_lock", department, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "spawn_codex",
            department,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "dotnet run",
            department,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "evaluate-frontier-completion",
            cli,
            StringComparison.Ordinal);
        Assert.Contains(
            "list-frontier-completion-candidates",
            cli,
            StringComparison.Ordinal);
        Assert.Contains(
            "EvaluateFrontierCompletion",
            cli,
            StringComparison.Ordinal);
        Assert.Contains(
            "ListFrontierCompletionCandidates",
            cli,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionAndManuscriptContractsArePresent()
    {
        string root = FindRepositoryRoot();
        foreach (string file in new[]
        {
            "paper-frontier-completion.v1.schema.json",
            "paper-frontier-completion-pending.v1.schema.json",
            "paper-frontier-completion-cursor.v1.schema.json",
            "paper-frontier-completion-evaluated.v1.schema.json",
            "paper-frontier-completion-ready.v1.schema.json",
            "paper-frontier-completion-pending-ready.v1.schema.json",
            "paper-frontier-completion-candidates-listed.v1.schema.json"
        })
        {
            Assert.True(
                File.Exists(Path.Combine(root, "contracts", file)),
                $"Missing frontier completion contract {file}.");
        }

        foreach (string file in new[]
        {
            "paper-manuscript-plan.v1.schema.json",
            "paper-certified-claim-manifest.v1.schema.json"
        })
        {
            string source = File.ReadAllText(Path.Combine(
                root,
                "contracts",
                file));
            Assert.Contains(
                "\"proposition\"",
                source,
                StringComparison.Ordinal);
        }
    }

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
