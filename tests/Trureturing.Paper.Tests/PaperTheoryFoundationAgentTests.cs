using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryFoundationAgentTests
{
    [Fact]
    public void ScopeDispatchRunsThroughGenericAgentAndDomainAdmission()
    {
        using var repository = new FoundationRepository();
        ScopeRun run = repository.RunScope();

        Assert.Equal("theory-scope", run.Staged.Phase);
        Assert.Equal("paper-theory-scope-author", run.Staged.AgentRole);
        Assert.Equal("exact-program-scope", run.Staged.ContextMode);
        Assert.Equal("completed", run.AgentResult.Status);
        Assert.Equal(
            PaperTheoryFoundationAgentSchemas.ScopeDraft,
            Assert.Single(run.AgentResult.Outputs).Schema);

        PaperTheoryFoundationAgentResultAdmitted admitted =
            PaperTheoryFoundationAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef);
        Assert.Equal(PaperTheoryFoundationSchemas.Scope, admitted.DomainSchema);
        Assert.Equal("theory-inventory", admitted.NextRoute);
        Assert.False(admitted.Replayed);
        Assert.NotEqual(run.AgentResult.Outputs[0].ArtifactRef, admitted.DomainRef);

        PaperTheoryScope scope = repository.ReadEnvelope<PaperTheoryScope>(
            admitted.EnvelopePath);
        PaperTheoryFoundationService.Validate(scope, repository.Program);
        Assert.Equal(admitted.DomainRef, scope.ScopeId);
        Assert.Equal(run.Request.RequestId, scope.ScopeContent.ScopeRequestRef);
        Assert.Contains(
            "central theorem",
            scope.ScopeContent.InScopeObligations[1],
            StringComparison.Ordinal);

        PaperTheoryFoundationAgentResultAdmitted replay =
            PaperTheoryFoundationAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef);
        Assert.True(replay.Replayed);
        Assert.Equal(admitted.DomainRef, replay.DomainRef);
        Assert.Equal(admitted.EnvelopeRef, replay.EnvelopeRef);
    }

    [Fact]
    public void InventoryDispatchProducesValidatedMultiClaimDag()
    {
        using var repository = new FoundationRepository();
        InventoryRun run = repository.RunInventory();

        PaperTheoryFoundationAgentResultAdmitted admitted =
            PaperTheoryFoundationAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef);
        Assert.Equal(PaperTheoryFoundationSchemas.Inventory, admitted.DomainSchema);
        Assert.Equal("theory-deepening", admitted.NextRoute);

        PaperTheoryInventory inventory = repository.ReadEnvelope<PaperTheoryInventory>(
            admitted.EnvelopePath);
        PaperTheoryFoundationService.Validate(inventory);
        Assert.Equal(admitted.DomainRef, inventory.InventoryId);
        Assert.Equal(run.Scope.ScopeId, inventory.InventoryContent.ScopeRef);
        Assert.Equal(3, inventory.InventoryContent.Items.Count);
        Assert.Equal(
            new[] { "def:object", "lem:reduction" },
            inventory.InventoryContent.Items
                .Single(item => item.ClaimId == "thm:main")
                .Dependencies);

        PaperCandidateState state = new(
            PaperPortfolioSchemas.CandidateState,
            repository.Program.ProgramContent.PaperId,
            repository.Program.TheoryProgramId,
            "inventory-pending",
            90,
            1,
            0,
            "2026-08-31T10:00:00Z",
            "scope admitted");
        PaperCandidateState advanced =
            PaperTheoryFoundationService.AdvanceAfterInventory(
                state,
                inventory,
                "2026-08-31T13:00:00Z");
        Assert.Equal("theory-deepening", advanced.Phase);
        Assert.Contains(inventory.InventoryId, advanced.StatusReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopeDispatchCannotDropExactProgramEvidence()
    {
        using var repository = new FoundationRepository();
        PaperTheoryScopeRequest request = repository.CreateScopeRequest();
        PaperTheoryFoundationAgentDispatch dispatch =
            repository.CreateScopeDispatch(request);
        PaperTheoryFoundationAgentDispatch incomplete = dispatch with
        {
            ExactInputs = dispatch.ExactInputs
                .Where(input => input.Schema != "paper-intuition-proposal.v1")
                .ToArray()
        };
        string path = repository.WriteDispatch(incomplete, "scope-incomplete.json");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryFoundationAgentService.StageTask(
                repository.Root,
                path));

        Assert.Contains("exact-input closure", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericAgentCannotSmuggleAReboundScopeDraft()
    {
        using var repository = new FoundationRepository();
        ScopeRun run = repository.RunScope(draftPaperId: "paper-substitution");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryFoundationAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef));

        Assert.Contains("Scope draft changed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FkstDepartmentsConnectA0A1SuccessAndFailureRoutes()
    {
        string root = FindRepositoryRoot();
        string dispatch = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "dispatch-theory-foundation-agent",
            "main.lua"));
        string admit = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "admit-theory-foundation-agent",
            "main.lua"));
        string failure = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "route-theory-foundation-agent-failure",
            "main.lua"));
        string cli = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Trureturing.Paper.Agent.Cli",
            "Program.cs"));

        Assert.Contains("paper_theory_scope_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_theory_inventory_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("stage-foundation-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("register-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_theory_scope_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper_theory_inventory_ready", admit, StringComparison.Ordinal);
        Assert.Contains("paper_theory_scope_no_progress", failure, StringComparison.Ordinal);
        Assert.Contains("paper_theory_inventory_blocked", failure, StringComparison.Ordinal);
        Assert.Contains("admit-foundation-result", cli, StringComparison.Ordinal);
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

    private sealed record Evidence(
        string Schema,
        string ArtifactRef,
        string RepositoryRelativePath);

    private sealed record ScopeRun(
        PaperTheoryScopeRequest Request,
        PaperTheoryFoundationAgentTaskStaged Staged,
        PaperAgentResultRecorded AgentResult);

    private sealed record InventoryRun(
        PaperTheoryScope Scope,
        PaperTheoryInventoryRequest Request,
        PaperTheoryFoundationAgentTaskStaged Staged,
        PaperAgentResultRecorded AgentResult);

    private sealed class FoundationRepository : IDisposable
    {
        private readonly Evidence _candidate;
        private readonly Evidence _literature;
        private readonly Evidence _intuition;
        private readonly Evidence _researchInput;
        private readonly Evidence _program;

        public FoundationRepository()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "trureturing-paper-foundation-agent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "theory-foundation"));
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "agent-tasks"));
            Directory.CreateDirectory(Path.Combine(Root, "artifacts", "evidence"));

            _candidate = PutEvidence(
                "paper-candidate.v1",
                "candidate.json",
                new { schema = "paper-candidate.v1", claim = "candidate paper" });
            _literature = PutEvidence(
                "paper-literature-research.v1",
                "literature.json",
                new { schema = "paper-literature-research.v1", boundary = "nearest prior work" });
            _intuition = PutEvidence(
                "paper-intuition-proposal.v1",
                "intuition.json",
                new { schema = "paper-intuition-proposal.v1", intuition = "structural descent" });
            _researchInput = PutEvidence(
                PaperResearchInputSchemas.ResearchInput,
                "research-input.json",
                new { schema = PaperResearchInputSchemas.ResearchInput, release = "exact" });

            PaperCandidateBatch batch = PaperPortfolioService.CreateBatch(
                new PaperCandidateBatchContent(
                    Digest("truth"),
                    Digest("topology"),
                    _researchInput.ArtifactRef,
                    new PaperPortfolioPolicy(5, 2, 1, 1),
                    [
                        new PaperCandidateSeed(
                            "paper-01",
                            _candidate.ArtifactRef,
                            _literature.ArtifactRef,
                            _intuition.ArtifactRef,
                            90,
                            "2026-08-31T09:00:00Z"),
                        new PaperCandidateSeed(
                            "paper-02",
                            Digest("candidate-02"),
                            Digest("literature-02"),
                            Digest("intuition-02"),
                            80,
                            "2026-08-31T09:00:00Z")
                    ]));
            Program = PaperPortfolioService.CreateTheoryProgram(
                batch,
                "paper-01",
                "2026-08-31T09:10:00Z");
            _program = PutContent(
                PaperPortfolioSchemas.TheoryProgram,
                "program.json",
                Program.ProgramContent,
                Program.TheoryProgramId);
        }

        public string Root { get; }
        public PaperTheoryProgram Program { get; }

        public PaperTheoryScopeRequest CreateScopeRequest() =>
            PaperTheoryFoundationService.CreateScopeRequest(
                Program,
                "2026-08-31T10:00:00Z");

        public PaperTheoryFoundationAgentDispatch CreateScopeDispatch(
            PaperTheoryScopeRequest request)
        {
            Evidence requestEvidence = PutContent(
                PaperTheoryFoundationSchemas.ScopeRequest,
                "scope-request.json",
                request.RequestContent,
                request.RequestId);
            return new PaperTheoryFoundationAgentDispatch(
                PaperTheoryFoundationAgentSchemas.Dispatch,
                PaperTheoryFoundationAgentService.ScopeKind,
                Program.ProgramContent.PaperId,
                Program.TheoryProgramId,
                request.RequestId,
                [
                    _program.ToInput(),
                    requestEvidence.ToInput(),
                    _candidate.ToInput(),
                    _literature.ToInput(),
                    _intuition.ToInput(),
                    _researchInput.ToInput()
                ],
                request.RequestContent.RequestedAt);
        }

        public ScopeRun RunScope(string? draftPaperId = null)
        {
            PaperTheoryScopeRequest request = CreateScopeRequest();
            PaperTheoryFoundationAgentDispatch dispatch = CreateScopeDispatch(request);
            string dispatchPath = WriteDispatch(dispatch, "scope.json");
            PaperTheoryFoundationAgentTaskStaged staged =
                PaperTheoryFoundationAgentService.StageTask(Root, dispatchPath);
            PaperAgentTaskRegistration registration =
                PaperAgentRuntimeService.RegisterTask(Root, staged.TaskPath);
            Assert.Equal(staged.TaskRef, registration.TaskRef);
            PaperAgentTask task =
                PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
                    File.ReadAllBytes(staged.TaskPath));
            PaperAgentRunPrepared prepared =
                PaperAgentRuntimeService.PrepareRun(Root, staged.TaskRef);

            var draft = new PaperTheoryScopeDraft(
                PaperTheoryFoundationAgentSchemas.ScopeDraft,
                Program.TheoryProgramId,
                request.RequestId,
                draftPaperId ?? Program.ProgramContent.PaperId,
                "Which canonical obstruction exactly controls descent of the target observable?",
                "A functorial descent datum together with its universal obstruction class.",
                "A tier-two-or-higher theorem paper with a central equivalence, sharpness witness, and reusable corollary.",
                [
                    "Define the canonical descent datum from the exact research state.",
                    "Prove the central theorem characterizing descent by obstruction vanishing.",
                    "Prove sharpness by constructing a minimal non-descending witness."
                ],
                ["Cited certified foundations and motivating examples."],
                ["Independent applications whose proofs do not support the central theorem."],
                "Split only when a direction has an independent research question and proof spine.",
                ["Construct a counterexample when the vanishing hypothesis is removed."],
                "2026-08-31T10:30:00Z");
            WriteJson(
                Path.Combine(prepared.WorkspacePath, "outputs", "scope-draft.json"),
                draft);
            PaperAgentResultRecorded recorded = CompleteAgent(
                task,
                prepared,
                "theory-inventory",
                "A0 scope completed with an explicit theorem and counterexample boundary.",
                "codex-a0-test");
            return new ScopeRun(request, staged, recorded);
        }

        public InventoryRun RunInventory()
        {
            PaperTheoryScopeRequest scopeRequest = CreateScopeRequest();
            PaperTheoryScope scope = PaperTheoryFoundationService.CreateScope(
                Program,
                scopeRequest,
                new PaperTheoryScopeContent(
                    Program.TheoryProgramId,
                    scopeRequest.RequestId,
                    Program.ProgramContent.PaperId,
                    "Which canonical obstruction exactly controls descent?",
                    "A functorial descent object and universal obstruction.",
                    "A tier-two-or-higher theorem chain with sharpness.",
                    [
                        "Define the canonical object.",
                        "Prove the central descent theorem.",
                        "Prove the sharp obstruction theorem."
                    ],
                    ["Certified background."],
                    ["Independent applications."],
                    "Split only an independent theorem package.",
                    ["Remove the vanishing hypothesis and construct failure."],
                    "2026-08-31T10:20:00Z"));
            Evidence scopeEvidence = PutContent(
                PaperTheoryFoundationSchemas.Scope,
                "scope-content.json",
                scope.ScopeContent,
                scope.ScopeId);
            PaperTheoryInventoryRequest request =
                PaperTheoryFoundationService.CreateInventoryRequest(
                    Program,
                    scope,
                    "2026-08-31T11:00:00Z");
            Evidence requestEvidence = PutContent(
                PaperTheoryFoundationSchemas.InventoryRequest,
                "inventory-request.json",
                request.RequestContent,
                request.RequestId);
            var dispatch = new PaperTheoryFoundationAgentDispatch(
                PaperTheoryFoundationAgentSchemas.Dispatch,
                PaperTheoryFoundationAgentService.InventoryKind,
                Program.ProgramContent.PaperId,
                Program.TheoryProgramId,
                request.RequestId,
                [
                    _program.ToInput(),
                    requestEvidence.ToInput(),
                    scopeEvidence.ToInput(),
                    _candidate.ToInput(),
                    _literature.ToInput(),
                    _researchInput.ToInput()
                ],
                request.RequestContent.RequestedAt);
            string dispatchPath = WriteDispatch(dispatch, "inventory.json");
            PaperTheoryFoundationAgentTaskStaged staged =
                PaperTheoryFoundationAgentService.StageTask(Root, dispatchPath);
            PaperAgentTaskRegistration registration =
                PaperAgentRuntimeService.RegisterTask(Root, staged.TaskPath);
            Assert.Equal(staged.TaskRef, registration.TaskRef);
            PaperAgentTask task =
                PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
                    File.ReadAllBytes(staged.TaskPath));
            PaperAgentRunPrepared prepared =
                PaperAgentRuntimeService.PrepareRun(Root, staged.TaskRef);

            var draft = new PaperTheoryInventoryDraft(
                PaperTheoryFoundationAgentSchemas.InventoryDraft,
                Program.TheoryProgramId,
                scope.ScopeId,
                request.RequestId,
                Program.ProgramContent.PaperId,
                [
                    new PaperTheoryClaimInventoryItem(
                        "def:object",
                        "Canonical descent object",
                        "definition",
                        "proposed",
                        "Every admissible object carries a canonical descent datum.",
                        [],
                        "Fixes the abstraction used throughout the theorem chain.",
                        "Stabilize the definition and its functoriality."),
                    new PaperTheoryClaimInventoryItem(
                        "lem:reduction",
                        "Reduction to the obstruction class",
                        "lemma",
                        "weak",
                        "Vanishing of the obstruction reduces descent to the canonical local problem.",
                        ["def:object"],
                        "Provides the proof reduction for the central theorem.",
                        "Strengthen and close the global gluing interface."),
                    new PaperTheoryClaimInventoryItem(
                        "thm:main",
                        "Structural descent theorem",
                        "theorem",
                        "missing",
                        "The observable descends exactly when the canonical obstruction vanishes.",
                        ["def:object", "lem:reduction"],
                        "Central theorem of the paper.",
                        "Develop the complete proof and sharp converse.")
                ],
                ["thm:main"],
                ["Global gluing interface from the reduction lemma to the theorem."],
                ["Classify all minimal non-vanishing obstruction classes."],
                ["Prove the forward implication under finite complexity."],
                ["Construct a minimal object with non-zero obstruction."],
                "2026-08-31T11:30:00Z");
            WriteJson(
                Path.Combine(prepared.WorkspacePath, "outputs", "inventory-draft.json"),
                draft);
            PaperAgentResultRecorded recorded = CompleteAgent(
                task,
                prepared,
                "theory-deepening",
                "A1 inventory completed with a three-node acyclic theorem DAG.",
                "codex-a1-test");
            return new InventoryRun(scope, request, staged, recorded);
        }

        public string WriteDispatch(
            PaperTheoryFoundationAgentDispatch dispatch,
            string fileName)
        {
            string path = Path.Combine(Root, "inbox", "theory-foundation", fileName);
            WriteJson(path, dispatch);
            return path;
        }

        public T ReadEnvelope<T>(string relativePath) =>
            PaperResearchInputJson.DeserializeStrict<T>(
                File.ReadAllBytes(Path.Combine(
                    Root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))));

        private PaperAgentResultRecorded CompleteAgent(
            PaperAgentTask task,
            PaperAgentRunPrepared prepared,
            string nextRoute,
            string summary,
            string runId)
        {
            var result = new PaperAgentResultWire(
                PaperAgentSchemas.AgentResult,
                prepared.TaskRef,
                task.PaperId,
                task.TheoryProgramRef,
                task.Phase,
                task.AgentRole,
                task.ContextMode,
                "completed",
                summary,
                task.ExpectedOutputs
                    .Select(output => new PaperAgentOutputWire(
                        output.Schema,
                        output.WorkspaceRelativePath))
                    .ToArray(),
                nextRoute,
                string.Empty,
                task.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
                "2026-08-31T12:00:00Z");
            string json = Encoding.UTF8.GetString(CanonicalJson.Serialize(result));
            Directory.CreateDirectory(Path.GetDirectoryName(prepared.StdoutPath)!);
            File.WriteAllText(
                prepared.StdoutPath,
                PaperAgentRuntimeService.ResultBegin + "\n" + json + "\n" +
                PaperAgentRuntimeService.ResultEnd + "\n");
            return PaperAgentRuntimeService.RecordResult(
                Root,
                prepared.TaskRef,
                prepared.StdoutPath,
                runId,
                "produced");
        }

        private Evidence PutEvidence<T>(string schema, string fileName, T value)
        {
            byte[] bytes = CanonicalJson.Serialize(value);
            string relative = "artifacts/evidence/" + fileName;
            string path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return new Evidence(schema, PaperResearchInputStore.Reference(bytes), relative);
        }

        private Evidence PutContent<T>(
            string schema,
            string fileName,
            T value,
            string expectedRef)
        {
            Evidence evidence = PutEvidence(schema, fileName, value);
            Assert.Equal(expectedRef, evidence.ArtifactRef);
            return evidence;
        }

        private static void WriteJson<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, CanonicalJson.Serialize(value));
        }

        private static string Digest(string seed) =>
            PaperResearchInputStore.Reference(Encoding.UTF8.GetBytes(seed));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}

internal static class PaperTheoryFoundationAgentTestEvidenceExtensions
{
    public static PaperAgentInputArtifact ToInput(
        this object evidence)
    {
        Type type = evidence.GetType();
        string schema = (string)(type.GetProperty("Schema")?.GetValue(evidence)
            ?? throw new InvalidOperationException("Evidence schema is missing."));
        string artifactRef = (string)(type.GetProperty("ArtifactRef")?.GetValue(evidence)
            ?? throw new InvalidOperationException("Evidence ref is missing."));
        string path = (string)(type.GetProperty("RepositoryRelativePath")?.GetValue(evidence)
            ?? throw new InvalidOperationException("Evidence path is missing."));
        return new PaperAgentInputArtifact(schema, artifactRef, path);
    }
}
