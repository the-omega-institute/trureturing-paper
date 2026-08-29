using System.Text.RegularExpressions;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class FkstOrganBoundaryTests
{
    private static readonly string[] ForbiddenCrossOrganTokens =
    [
        "fkst-ops",
        "trureturing-fkst-packages",
        "Golden/Frozen",
        "skills/codex",
        "docs/develop/spec",
        "github.com",
        "http://",
        "https://",
        "\"git\"",
        "\"gh\"",
        "\"curl\"",
        "\"python3\"",
    ];

    [Fact]
    public void Host_package_is_repository_local_and_framework_agnostic()
    {
        string repositoryRoot = FindRepositoryRoot();
        string packageRoot = Path.Combine(
            repositoryRoot,
            ".fkst",
            "local-packages",
            "trureturing-paper");
        string[] luaFiles = Directory.GetFiles(
            packageRoot,
            "*.lua",
            SearchOption.AllDirectories);

        Assert.NotEmpty(luaFiles);
        string source = string.Join(
            "\n",
            luaFiles.Order(StringComparer.Ordinal)
                .Select(path => StripFullLineComments(File.ReadAllText(path))));

        foreach (string token in ForbiddenCrossOrganTokens)
        {
            Assert.False(
                source.Contains(token, StringComparison.OrdinalIgnoreCase),
                $"The repository-local FKST package must not reference " +
                $"cross-organ/framework detail '{token}'.");
        }

        string actPath = Path.Combine(
            packageRoot,
            "departments",
            "act",
            "main.lua");
        string researchCorePath = Path.Combine(packageRoot, "research_core.lua");
        string act = StripFullLineComments(File.ReadAllText(actPath));
        string researchCore = StripFullLineComments(
            File.ReadAllText(researchCorePath));

        Assert.Single(ExecCalls(act));
        Assert.Equal(2, ExecCalls(researchCore).Count);
        Assert.Contains(
            "\"dotnet\", \"run\", \"--project\", pth.cli_project",
            act,
            StringComparison.Ordinal);
        Assert.Contains(
            "local argv = { \"dotnet\", executable }",
            researchCore,
            StringComparison.Ordinal);
        Assert.Contains(
            "Trureturing.Paper.ResearchInput.Cli.dll",
            researchCore,
            StringComparison.Ordinal);
        Assert.Contains(
            "Trureturing.Paper.ResearchSelection.Cli.dll",
            researchCore,
            StringComparison.Ordinal);
        Assert.Contains(
            "argv = { \"mkdir\", \"-p\", path }",
            researchCore,
            StringComparison.Ordinal);

        foreach (string path in luaFiles.Where(path =>
                     !string.Equals(path, actPath, StringComparison.Ordinal)
                     && !string.Equals(
                         path,
                         researchCorePath,
                         StringComparison.Ordinal)))
        {
            Assert.Empty(ExecCalls(StripFullLineComments(File.ReadAllText(path))));
        }
    }

    [Fact]
    public void Core_logic_has_no_host_authority_calls()
    {
        string core = StripFullLineComments(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "core.lua")));

        foreach (string token in new[]
        {
            "file.read",
            "file.write",
            "file.exists",
            "exec_argv",
            "raise(",
            "with_lock(",
            "log.",
        })
        {
            Assert.DoesNotContain(token, core, StringComparison.Ordinal);
        }
    }

    private static MatchCollection ExecCalls(string source) =>
        Regex.Matches(source, @"\bexec_argv\s*\(");

    private static string FindRepositoryRoot()
    {
        foreach (DirectoryInfo start in new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory),
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

    private static string StripFullLineComments(string source) =>
        string.Join(
            "\n",
            source.Split('\n').Where(line =>
                !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
}
