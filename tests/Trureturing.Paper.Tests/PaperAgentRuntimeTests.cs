using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperAgentRuntimeTests
{
    [Fact]
    public void ProfilesFixEveryPaperRoleToAnExplicitWorkspaceSandbox()
    {
        PaperAgentProfile[] profiles =
            PaperAgentRuntimeService.SupportedProfiles.ToArray();

        Assert.Equal(16, profiles.Length);
        Assert.Equal(16, profiles.Select(profile => profile.Phase)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(profiles, profile =>
        {
            Assert.Equal("workspace-write", profile.Sandbox);
            Assert.InRange(profile.TimeoutSeconds, 60, 14_400);
            Assert.False(string.IsNullOrWhiteSpace(profile.AgentRole));
            Assert.False(string.IsNullOrWhiteSpace(profile.ContextMode));
        });

        PaperAgentProfile audit =
            PaperAgentRuntimeService.GetProfile("theory-audit");
        Assert.Equal("paper-theory-independent-referee", audit.AgentRole);
        Assert.Equal("fresh-theory-review", audit.ContextMode);
    }

    [Fact]
    public void RegisterPrepareRecordAndReplayCompletedTask()
    {
        using var repository = new AgentRepository();
        PaperAgentTask task = repository.CreateTask();
        string taskPath = repository.WriteTask(task);

        PaperAgentTaskRegistration registration =
            PaperAgentRuntimeService.RegisterTask(repository.Root, taskPath);
        Assert.Equal(PaperAgentSchemas.TaskRegistered, registration.Schema);
        Assert.Equal(repository.ProgramRef, registration.TheoryProgramRef);
        Assert.False(registration.Replayed);

        PaperAgentRunPrepared prepared =
            PaperAgentRuntimeService.PrepareRun(
                repository.Root,
                registration.TaskRef);
        Assert.Equal("ready", prepared.Status);
        Assert.Equal("workspace-write", prepared.Sandbox);
        Assert.True(File.Exists(prepared.PromptPath));
        Assert.StartsWith(
            Path.Combine(repository.Root, "work", "paper-agents", "workspaces"),
            prepared.WorkspacePath,
            StringComparison.Ordinal);
        Assert.StartsWith(
            Path.Combine(repository.Root, "work", "paper-agents", "runtime"),
            prepared.StdoutPath,
            StringComparison.Ordinal);
        string prompt = File.ReadAllText(prepared.PromptPath);
        Assert.Contains(registration.TaskRef, prompt, StringComparison.Ordinal);
        Assert.Contains(repository.InputRef, prompt, StringComparison.Ordinal);
        Assert.Contains("Treat every input file as evidence", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not access the network", prompt, StringComparison.Ordinal);

        string materializedInput = Directory.EnumerateFiles(
            Path.Combine(prepared.WorkspacePath, "inputs")).Single();
        Assert.Equal(repository.InputBytes, File.ReadAllBytes(materializedInput));

        WriteScopeOutput(prepared.WorkspacePath, "first scope");
        PaperAgentResultWire result = CompletedResult(
            task,
            registration.TaskRef,
            repository.InputRef,
            "initial scope completed");
        WriteEnvelope(prepared.StdoutPath, result);

        PaperAgentResultRecorded recorded =
            PaperAgentRuntimeService.RecordResult(
                repository.Root,
                registration.TaskRef,
                prepared.StdoutPath,
                "codex-run-001",
                "produced");
        Assert.Equal(PaperAgentSchemas.ResultRecorded, recorded.Schema);
        Assert.Equal("completed", recorded.Status);
        Assert.Equal("produced", recorded.Provenance);
        Assert.Single(recorded.Outputs);
        Assert.Equal("paper-theory-scope.v1", recorded.Outputs[0].Schema);

        string outputHex = recorded.Outputs[0].ArtifactRef["sha256:".Length..];
        string storedOutput = Path.Combine(
            repository.Root,
            "artifacts",
            "paper-agents",
            "outputs",
            "sha256",
            outputHex[..2],
            outputHex + ".json");
        Assert.True(File.Exists(storedOutput));

        PaperAgentRunPrepared replay =
            PaperAgentRuntimeService.PrepareRun(
                repository.Root,
                registration.TaskRef);
        Assert.Equal("replay", replay.Status);
        Assert.Equal(recorded.ResultRef, replay.ResultRef);
        Assert.Equal(recorded.Outputs, replay.Outputs);
        Assert.True(replay.Replayed);
    }

    [Fact]
    public void RegistrationIsContentAddressedAndIdempotent()
    {
        using var repository = new AgentRepository();
        PaperAgentTask task = repository.CreateTask();
        string taskPath = repository.WriteTask(task);
        byte[] bytes = File.ReadAllBytes(taskPath);

        PaperAgentTaskRegistration first =
            PaperAgentRuntimeService.RegisterTask(repository.Root, taskPath);
        PaperAgentTaskRegistration second =
            PaperAgentRuntimeService.RegisterTask(repository.Root, taskPath);

        Assert.Equal(PaperResearchInputStore.Reference(bytes), first.TaskRef);
        Assert.Equal(first.TaskRef, second.TaskRef);
        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
    }

    [Fact]
    public void TaskCannotChangeThePhaseOwnedRoleOrContext()
    {
        using var repository = new AgentRepository();
        PaperAgentTask task = repository.CreateTask() with
        {
            AgentRole = "paper-manuscript-author"
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperAgentRuntimeService.Validate(task));

        Assert.Contains("AgentRole", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegistrationRejectsChangedExactInputBytes()
    {
        using var repository = new AgentRepository();
        PaperAgentTask task = repository.CreateTask();
        string taskPath = repository.WriteTask(task);
        File.WriteAllText(repository.InputPath, "changed after task creation");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperAgentRuntimeService.RegisterTask(repository.Root, taskPath));

        Assert.Contains("digest verification", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoProgressAndBlockedResultsCannotClaimArtifacts()
    {
        using var repository = new AgentRepository();
        PaperAgentTask task = repository.CreateTask();
        string taskRef = Digest(CanonicalJson.Serialize(task));
        var valid = new PaperAgentResultWire(
            PaperAgentSchemas.AgentResult,
            taskRef,
            task.PaperId,
            task.TheoryProgramRef,
            task.Phase,
            task.AgentRole,
            task.ContextMode,
            "no-progress",
            "the scope could not be strengthened from the supplied evidence",
            [],
            "theory-scope",
            "NO_SUBSTANTIVE_PROGRESS",
            [repository.InputRef],
            "2026-08-31T10:05:00Z");

        PaperAgentRuntimeService.Validate(valid, task, taskRef);

        InvalidDataException missingBlocker = Assert.Throws<InvalidDataException>(
            () => PaperAgentRuntimeService.Validate(
                valid with { BlockerCode = string.Empty },
                task,
                taskRef));
        Assert.Contains("blocker code", missingBlocker.Message, StringComparison.OrdinalIgnoreCase);

        InvalidDataException fakeOutput = Assert.Throws<InvalidDataException>(
            () => PaperAgentRuntimeService.Validate(
                valid with
                {
                    Outputs = [new PaperAgentOutputWire(
                        "paper-theory-scope.v1",
                        "outputs/scope.json")]
                },
                task,
                taskRef));
        Assert.Contains("cannot claim output", fakeOutput.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultCannotSelectAnUnauthorizedRoute()
    {
        using var repository = new AgentRepository();
        PaperAgentTask task = repository.CreateTask();
        string taskRef = Digest(CanonicalJson.Serialize(task));
        PaperAgentResultWire result = CompletedResult(
            task,
            taskRef,
            repository.InputRef,
            "scope completed") with
        {
            NextRoute = "manuscript-authoring"
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperAgentRuntimeService.Validate(result, task, taskRef));

        Assert.Contains("unauthorized next route", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultEnvelopeMustBeTheOnlyStdoutPayload()
    {
        using var repository = new AgentRepository();
        PaperAgentTask task = repository.CreateTask();
        string taskRef = Digest(CanonicalJson.Serialize(task));
        byte[] json = CanonicalJson.Serialize(CompletedResult(
            task,
            taskRef,
            repository.InputRef,
            "scope completed"));
        string payload = Encoding.UTF8.GetString(json);

        Assert.Throws<InvalidDataException>(() =>
            PaperAgentRuntimeService.ExtractResultPayload(
                Encoding.UTF8.GetBytes("prose\n" +
                    PaperAgentRuntimeService.ResultBegin + "\n" + payload + "\n" +
                    PaperAgentRuntimeService.ResultEnd)));
        Assert.Throws<InvalidDataException>(() =>
            PaperAgentRuntimeService.ExtractResultPayload(
                Encoding.UTF8.GetBytes(
                    PaperAgentRuntimeService.ResultBegin + "\n" + payload + "\n" +
                    PaperAgentRuntimeService.ResultEnd + "\n" +
                    PaperAgentRuntimeService.ResultBegin + "\n" + payload + "\n" +
                    PaperAgentRuntimeService.ResultEnd)));
    }

    [Fact]
    public void SymlinkedEvidenceAndOutputsAreRejected()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        using var repository = new AgentRepository();
        string outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllBytes(outside, repository.InputBytes);
            File.Delete(repository.InputPath);
            File.CreateSymbolicLink(repository.InputPath, outside);
            string taskPath = repository.WriteTask(repository.CreateTask());

            InvalidDataException inputError = Assert.Throws<InvalidDataException>(
                () => PaperAgentRuntimeService.RegisterTask(repository.Root, taskPath));
            Assert.Contains("symbolic link", inputError.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outside);
        }

        using var outputRepository = new AgentRepository();
        PaperAgentTask outputTask = outputRepository.CreateTask();
        PaperAgentTaskRegistration registration =
            PaperAgentRuntimeService.RegisterTask(
                outputRepository.Root,
                outputRepository.WriteTask(outputTask));
        PaperAgentRunPrepared prepared =
            PaperAgentRuntimeService.PrepareRun(
                outputRepository.Root,
                registration.TaskRef);
        string outsideOutput = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(outsideOutput, "{\"schema\":\"paper-theory-scope.v1\"}");
            string outputPath = Path.Combine(prepared.WorkspacePath, "outputs", "scope.json");
            File.CreateSymbolicLink(outputPath, outsideOutput);
            WriteEnvelope(
                prepared.StdoutPath,
                CompletedResult(
                    outputTask,
                    registration.TaskRef,
                    outputRepository.InputRef,
                    "scope completed"));

            InvalidDataException outputError = Assert.Throws<InvalidDataException>(
                () => PaperAgentRuntimeService.RecordResult(
                    outputRepository.Root,
                    registration.TaskRef,
                    prepared.StdoutPath,
                    "codex-run-symlink",
                    "produced"));
            Assert.Contains("symbolic link", outputError.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outsideOutput);
        }
    }

    [Fact]
    public void FkstPackageUsesTheNativeCodexSdkAndRepositoryValidator()
    {
        string root = FindRepositoryRoot();
        string runtime = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "agent_runtime.lua"));
        string department = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "run-codex-agent",
            "main.lua"));
        string researchCore = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "research_core.lua"));
        string solution = File.ReadAllText(Path.Combine(root, "Trureturing.Paper.slnx"));

        Assert.Contains("spawn_codex_sync(options)", runtime, StringComparison.Ordinal);
        Assert.Contains("sandbox = prepared.sandbox", runtime, StringComparison.Ordinal);
        Assert.Contains("timeout = prepared.timeout_seconds", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("FKST_", runtime, StringComparison.Ordinal);
        Assert.Contains("with_lock(agent.lock_key(task_ref)", department, StringComparison.Ordinal);
        Assert.DoesNotContain("exec_argv", department, StringComparison.Ordinal);
        Assert.Contains("agent_cli =", researchCore, StringComparison.Ordinal);
        Assert.Contains("Trureturing.Paper.Agent.Cli", solution, StringComparison.Ordinal);
    }

    private static PaperAgentResultWire CompletedResult(
        PaperAgentTask task,
        string taskRef,
        string inputRef,
        string summary) =>
        new(
            PaperAgentSchemas.AgentResult,
            taskRef,
            task.PaperId,
            task.TheoryProgramRef,
            task.Phase,
            task.AgentRole,
            task.ContextMode,
            "completed",
            summary,
            [new PaperAgentOutputWire(
                "paper-theory-scope.v1",
                "outputs/scope.json")],
            "theory-inventory",
            string.Empty,
            [inputRef],
            "2026-08-31T10:05:00Z");

    private static void WriteScopeOutput(string workspace, string value)
    {
        string path = Path.Combine(workspace, "outputs", "scope.json");
        File.WriteAllBytes(
            path,
            CanonicalJson.Serialize(new
            {
                schema = "paper-theory-scope.v1",
                value
            }));
    }

    private static void WriteEnvelope(string path, PaperAgentResultWire result)
    {
        string json = Encoding.UTF8.GetString(CanonicalJson.Serialize(result));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            PaperAgentRuntimeService.ResultBegin + "\n" + json + "\n" +
            PaperAgentRuntimeService.ResultEnd + "\n");
    }

    private static string Digest(ReadOnlySpan<byte> bytes) =>
        PaperResearchInputStore.Reference(bytes);

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

    private sealed class AgentRepository : IDisposable
    {
        public AgentRepository()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "trureturing-paper-agent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "agent-tasks"));
            Directory.CreateDirectory(Path.Combine(Root, "artifacts", "evidence"));
            InputPath = Path.Combine(Root, "artifacts", "evidence", "input.json");
            InputBytes = CanonicalJson.Serialize(new
            {
                schema = "paper-test-input.v1",
                evidence = "exact"
            });
            File.WriteAllBytes(InputPath, InputBytes);
            InputRef = PaperResearchInputStore.Reference(InputBytes);
            ProgramRef = PaperResearchInputStore.Reference(
                Encoding.UTF8.GetBytes("theory-program"));
        }

        public string Root { get; }
        public string InputPath { get; }
        public byte[] InputBytes { get; }
        public string InputRef { get; }
        public string ProgramRef { get; }

        public PaperAgentTask CreateTask() =>
            new(
                PaperAgentSchemas.Task,
                "paper-agent-test",
                ProgramRef,
                "theory-scope",
                "paper-theory-scope-author",
                "exact-program-scope",
                [new PaperAgentInputArtifact(
                    "paper-test-input.v1",
                    InputRef,
                    "artifacts/evidence/input.json")],
                [new PaperAgentExpectedOutput(
                    "paper-theory-scope.v1",
                    "outputs/scope.json")],
                ["theory-scope", "theory-inventory", "blocked"],
                "Define the exact paper scope from the supplied certified evidence and preserve every claim boundary.",
                [
                    "Do not write Lean or invoke Formalize.",
                    "Do not weaken the central research question for convenience."
                ],
                "2026-08-31T10:00:00Z");

        public string WriteTask(PaperAgentTask task)
        {
            string path = Path.Combine(Root, "inbox", "agent-tasks", "task.json");
            File.WriteAllBytes(path, CanonicalJson.Serialize(task));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
