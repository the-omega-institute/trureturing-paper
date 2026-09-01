namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierReadyWaveSelectionWiringTests
{
    [Fact]
    public void FkstConsumesReadySetAndPublishesCanonicalRequests()
    {
        string root = FindRepositoryRoot();
        string department = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "select-frontier-ready-wave",
            "main.lua"));
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.FrontierSelection.Cli",
            "Program.cs"));

        Assert.Contains(
            "paper_frontier_ready_set_ready",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_ready_wave_selection_ready",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_frontier_node_selection_ready",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "formalization_request_ready",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "admit-frontier-ready-wave",
            department,
            StringComparison.Ordinal);
        Assert.Contains(
            "with_lock",
            department,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "spawn_codex",
            department,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "dotnet run",
            department,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "admit-frontier-ready-wave",
            cli,
            StringComparison.Ordinal);
        Assert.Contains(
            "AdmitReadyWave",
            cli,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ContractsAllowGovernedSelectionsBeyondWaveZero()
    {
        string root = FindRepositoryRoot();
        foreach (string file in new[]
        {
            "paper-frontier-node-selection-authorization.v1.schema.json",
            "paper-frontier-node-selection-cursor.v1.schema.json",
            "paper-frontier-node-selection-admitted.v1.schema.json",
            "paper-frontier-node-selection-ready.v1.schema.json"
        })
        {
            string source = File.ReadAllText(Path.Combine(
                root,
                "contracts",
                file));
            Assert.Contains(
                "\"parallel_wave\": { \"type\": \"integer\", \"minimum\": 0 }",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "\"parallel_wave\": { \"const\": 0 }",
                source,
                StringComparison.Ordinal);
        }

        foreach (string file in new[]
        {
            "paper-frontier-ready-wave-selection-cursor.v1.schema.json",
            "paper-frontier-ready-wave-selection-admitted.v1.schema.json",
            "paper-frontier-ready-wave-selection-ready.v1.schema.json"
        })
        {
            Assert.True(
                File.Exists(Path.Combine(root, "contracts", file)),
                $"Missing ready-wave contract {file}.");
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
