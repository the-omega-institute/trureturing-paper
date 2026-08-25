using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class TruthConsumptionBoundaryTests
{
    [Fact]
    public void PaperOwnsIndexesAndNotTheSharedTruthWire()
    {
        string root = FindRoot();
        string[] files =
        {
            Path.Combine(root, "src", "Trureturing.Paper.Core", "TruthReleasePorts.cs"),
            Path.Combine(root, "src", "Trureturing.Paper.Core", "PaperTruthIndex.cs")
        };

        string text = string.Join(
            "\n",
            files.Select(File.ReadAllText));

        Assert.Contains("paper-truth-release-port.v1", text, StringComparison.Ordinal);
        Assert.Contains("PaperIntuitionIndex", text, StringComparison.Ordinal);
        Assert.DoesNotContain("stratalint.truth-graph", text, StringComparison.Ordinal);
        Assert.DoesNotContain("stratalint.truth-export", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FrozenLedger", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Trureturing.Truth", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryContainsNoVisualizationLayer()
    {
        string root = FindRoot();
        Assert.False(Directory.Exists(Path.Combine(root, "site")));
        Assert.False(File.Exists(Path.Combine(root, ".github", "workflows", "pages.yml")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.Core",
            "ExamplePaperPublisher.cs")));

        string source = string.Join(
            "\n",
            Directory.EnumerateFiles(
                    Path.Combine(root, "src"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("<!doctype html", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WriteHtml", source, StringComparison.Ordinal);
        Assert.DoesNotContain("example-cycle", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryHasNoAbsoluteLocalPackageFeed()
    {
        string root = FindRoot();
        Assert.False(File.Exists(Path.Combine(root, "nuget.config")));

        foreach (string path in Directory.EnumerateFiles(
            root,
            "*",
            SearchOption.AllDirectories))
        {
            if (Path.GetFileName(path) == "TruthConsumptionBoundaryTests.cs" ||
                path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal) ||
                path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                continue;
            }

            Assert.DoesNotContain("/Users/", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\\Users\\", text, StringComparison.Ordinal);
        }
    }

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Trureturing.Paper.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
