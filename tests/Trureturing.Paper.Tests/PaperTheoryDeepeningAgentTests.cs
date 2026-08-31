using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryDeepeningAgentTests
{
    [Fact]
    public void A2DispatchRunsThroughGenericAgentAndComputedDeltaAdmission()
    {
        using var repository = new DeepeningRepository("paper-01");
        DeepeningRun run = repository.RunAuditCandidate();

        Assert.Equal("theory-deepening", run.Staged.Phase);
        Assert.Equal("paper-theory-developer", run.Staged.AgentRole);
        Assert.Equal("contextual-theory-execution", run.Staged.ContextMode);
        Assert.Equal("completed", run.AgentResult.Status);
        Assert.Equal(
            PaperTheoryDeepeningAgentSchemas.Draft,
            Assert.Single(run.AgentResult.Outputs).Schema);

        PaperTheoryDeepeningAgentResultAdmitted admitted =
            PaperTheoryDeepeningAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef);

        Assert.Equal("audit-candidate", admitted.Maturity);
        Assert.Equal("theory-audit", admitted.NextRoute);
        Assert.False(admitted.Replayed);
        Assert.Single(admitted.SplitProposals);
        Assert.Equal(new[] { "paper-02" }, admitted.MergeCandidatePaperIds);
        Assert.Equal(4, admitted.ResearchLedgerEntries.Count);

        PaperTheoryIteration iteration = repository.ReadEnvelope<PaperTheoryIteration>(
            admitted.Iteration.EnvelopePath);
        PaperTheoremPackage package = repository.ReadEnvelope<PaperTheoremPackage>(
            admitted.TheoremPackage.EnvelopePath);
        PaperTheoryDeepeningDelta delta = repository.ReadEnvelope<PaperTheoryDeepeningDelta>(
            admitted.Delta.EnvelopePath);
        PaperTheoryDeepeningService.Validate(iteration);
        PaperTheoryDeepeningService.Validate(package);
        PaperTheoryDeepeningAgentService.Validate(delta);

        Assert.Equal(5, package.TheoremPackageContent.Claims.Count);
        Assert.Equal(new[] { "thm:main" }, package.TheoremPackageContent.MainTheoremClaimIds);
        Assert.Equal(new[] { "thm:sharp" }, package.TheoremPackageContent.SharpnessClaimIds);
        Assert.Equal(new[] { "cor:classification" }, package.TheoremPackageContent.CorollaryClaimIds);
        Assert.Equal(
            new[] { "cor:classification", "thm:sharp" },
            delta.DeltaContent.NewClaimIds);
        Assert.Equal(
            new[] { "lem:reduction", "thm:main" },
            delta.DeltaContent.StrengthenedClaimIds);
        Assert.Equal(4, delta.DeltaContent.DependencyEdgesAdded);
        Assert.Equal(5, delta.DeltaContent.ProofObligationsClosed);
        Assert.Equal(1, delta.DeltaContent.CounterexamplesResolved);
        Assert.True(delta.DeltaContent.AbstractionChanged);
        Assert.True(delta.DeltaContent.NoveltyBoundaryChanged);
        Assert.True(delta.DeltaContent.Passed);

        PaperCandidateState state = new(
            PaperPortfolioSchemas.CandidateState,
            repository.Program.ProgramContent.PaperId,
            repository.Program.TheoryProgramId,
            "theory-deepening",
            90,
            2,
            0,
            "2026-08-31T11:00:00Z",
            "inventory admitted");
        PaperCandidateState advanced = PaperTheoryDeepeningService.AdvanceAfterDeepening(
            state,
            package,
            "2026-08-31T15:00:00Z");
        Assert.Equal("audit-pending", advanced.Phase);

        PaperTheoryDeepeningAgentResultAdmitted replay =
            PaperTheoryDeepeningAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef);
        Assert.True(replay.Replayed);
        Assert.Equal(admitted.TheoremPackage.ArtifactRef, replay.TheoremPackage.ArtifactRef);
        Assert.Equal(admitted.Delta.ArtifactRef, replay.Delta.ArtifactRef);
        Assert.Equal(admitted.SplitProposals, replay.SplitProposals);
    }

    [Fact]
    public void RepositoryRejectsInflatedSelfReportedProgress()
    {
        using var repository = new DeepeningRepository("paper-01");
        PaperTheoryDeepeningDraft draft = repository.CreateAuditCandidateDraft();
        PaperTheoryProgressEvidence evidence = draft.Iteration.ProgressEvidence with
        {
            DependencyEdgesAdded = 99
        };
        DeepeningRun run = repository.RunDraft(
            draft with
            {
                Iteration = draft.Iteration with { ProgressEvidence = evidence }
            });

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryDeepeningAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef));

        Assert.Contains(
            "repository-computed theorem-package delta",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryRejectsRenamedClaimAsFakeNovelty()
    {
        using var repository = new DeepeningRepository("paper-01");
        PaperTheoryDeepeningDraft draft = repository.CreateAuditCandidateDraft();
        PaperTheoremPackageClaim main = draft.TheoremPackage.Claims.Single(
            claim => claim.ClaimId == "thm:main");
        PaperTheoremPackageClaim renamed = main with
        {
            ClaimId = "thm:main-renamed"
        };
        PaperTheoremPackageClaim[] claims = draft.TheoremPackage.Claims
            .Where(claim => claim.ClaimId != "thm:main")
            .Append(renamed)
            .Select(claim => claim.ClaimId == "cor:classification"
                ? claim with
                {
                    Dependencies = ["thm:main-renamed", "thm:sharp"]
                }
                : claim)
            .ToArray();
        PaperTheoryIterationDraft iteration = draft.Iteration with
        {
            ChangedClaimIds =
            [
                "def:object",
                "lem:reduction",
                "thm:main",
                "thm:main-renamed",
                "thm:sharp",
                "cor:classification"
            ],
            NewClaimIds = ["thm:main-renamed", "thm:sharp", "cor:classification"],
            StrengthenedClaimIds = ["lem:reduction"],
            RetiredClaimIds = ["thm:main"],
            ProgressEvidence = new PaperTheoryProgressEvidence(
                3,
                1,
                5,
                5,
                1,
                true,
                true)
        };
        DeepeningRun run = repository.RunDraft(
            draft with
            {
                Iteration = iteration,
                TheoremPackage = draft.TheoremPackage with
                {
                    Claims = claims,
                    MainTheoremClaimIds = ["thm:main-renamed"]
                }
            });

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryDeepeningAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef));

        Assert.Contains(
            "repository-computed theorem-package delta",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A2DispatchCannotDropExactInventoryEvidence()
    {
        using var repository = new DeepeningRepository("paper-01");
        PaperTheoryDeepeningAgentDispatch dispatch = repository.CreateDispatch();
        PaperTheoryDeepeningAgentDispatch incomplete = dispatch with
        {
            ExactInputs = dispatch.ExactInputs
                .Where(input => input.Schema != PaperTheoryFoundationSchemas.Inventory)
                .ToArray()
        };
        string path = repository.WriteDispatch(incomplete, "missing-inventory.json");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryDeepeningAgentService.StageTask(
                repository.Root,
                path));

        Assert.Contains("between four and sixty-four", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DifferentPapersStageIndependentA2Tasks()
    {
        using var first = new DeepeningRepository("paper-01");
        using var second = new DeepeningRepository("paper-02");

        PaperTheoryDeepeningAgentTaskStaged firstTask = first.Stage();
        PaperTheoryDeepeningAgentTaskStaged secondTask = second.Stage();

        Assert.NotEqual(firstTask.TaskRef, secondTask.TaskRef);
        Assert.NotEqual(firstTask.DispatchRef, secondTask.DispatchRef);
        Assert.NotEqual(firstTask.TheoryProgramRef, secondTask.TheoryProgramRef);
        Assert.Equal("paper-01", firstTask.PaperId);
        Assert.Equal("paper-02", secondTask.PaperId);
    }

    private sealed record Evidence(
        string Schema,
        string ArtifactRef,
        string RepositoryRelativePath)
    {
        public PaperAgentInputArtifact ToInput() =>
            new(Schema, ArtifactRef, RepositoryRelativePath);
    }

    private sealed record DeepeningRun(
        PaperTheoryDeepeningAgentTaskStaged Staged,
        PaperAgentResultRecorded AgentResult);

    private sealed class DeepeningRepository : IDisposable
    {
        private readonly Evidence _program;
        private readonly Evidence _scope;
        private readonly Evidence _inventory;
        private readonly Evidence _request;

        public DeepeningRepository(string paperId)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "trureturing-paper-deepening-agent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "theory-deepening"));
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "agent-tasks"));
            Directory.CreateDirectory(Path.Combine(Root, "artifacts", "evidence"));

            PaperCandidateBatch batch = PaperPortfolioService.CreateBatch(
                new PaperCandidateBatchContent(
                    Digest($"truth-{paperId}"),
                    Digest($"topology-{paperId}"),
                    Digest($"research-{paperId}"),
                    new PaperPortfolioPolicy(5, 2, 1, 1),
                    [
                        new PaperCandidateSeed(
                            paperId,
                            Digest($"candidate-{paperId}"),
                            Digest($"literature-{paperId}"),
                            Digest($"intuition-{paperId}"),
                            90,
                            "2026-08-31T09:00:00Z"),
                        new PaperCandidateSeed(
                            paperId + "-peer",
                            Digest($"candidate-{paperId}-peer"),
                            Digest($"literature-{paperId}-peer"),
                            Digest($"intuition-{paperId}-peer"),
                            80,
                            "2026-08-31T09:00:00Z")
                    ]));
            Program = PaperPortfolioService.CreateTheoryProgram(
                batch,
                paperId,
                "2026-08-31T09:10:00Z");
            _program = PutContent(
                PaperPortfolioSchemas.TheoryProgram,
                "program.json",
                Program.ProgramContent,
                Program.TheoryProgramId);

            PaperTheoryScopeRequest scopeRequest =
                PaperTheoryFoundationService.CreateScopeRequest(
                    Program,
                    "2026-08-31T09:20:00Z");
            Scope = PaperTheoryFoundationService.CreateScope(
                Program,
                scopeRequest,
                new PaperTheoryScopeContent(
                    Program.TheoryProgramId,
                    scopeRequest.RequestId,
                    paperId,
                    "Which canonical obstruction exactly controls descent of the target observable?",
                    "A functorial local descent datum together with its universal obstruction class.",
                    "A theorem paper with an exact equivalence, sharpness theorem, and classification corollary.",
                    [
                        "Define the canonical descent object.",
                        "Prove the central obstruction equivalence.",
                        "Construct sharp failures and classify minimal obstructions."
                    ],
                    ["Classical local-to-global descent tools used with explicit citations."],
                    ["Applications that do not contribute to the central theorem chain."],
                    "Split only a theorem chain with an independent question and proof spine.",
                    ["Construct a minimal non-zero obstruction that forces descent failure."],
                    "2026-08-31T09:30:00Z"));
            _scope = PutContent(
                PaperTheoryFoundationSchemas.Scope,
                "scope.json",
                Scope.ScopeContent,
                Scope.ScopeId);

            PaperTheoryInventoryRequest inventoryRequest =
                PaperTheoryFoundationService.CreateInventoryRequest(
                    Program,
                    Scope,
                    "2026-08-31T09:40:00Z");
            Inventory = PaperTheoryFoundationService.CreateInventory(
                Program,
                Scope,
                inventoryRequest,
                new PaperTheoryInventoryContent(
                    Program.TheoryProgramId,
                    Scope.ScopeId,
                    inventoryRequest.RequestId,
                    paperId,
                    [
                        new PaperTheoryClaimInventoryItem(
                            "def:object",
                            "Candidate descent object",
                            "definition",
                            "proposed",
                            "Each admissible object has a local descent datum.",
                            [],
                            "Provides the common language for every theorem.",
                            "Stabilize the canonical definition and prove coordinate invariance."),
                        new PaperTheoryClaimInventoryItem(
                            "lem:reduction",
                            "One-way obstruction reduction",
                            "lemma",
                            "weak",
                            "Vanishing obstruction is sufficient for a restricted gluing problem.",
                            ["def:object"],
                            "Supports one direction of the main theorem.",
                            "Strengthen to an exact local-to-global criterion."),
                        new PaperTheoryClaimInventoryItem(
                            "thm:main",
                            "Proposed structural descent theorem",
                            "theorem",
                            "missing",
                            "The target observable should descend when the obstruction vanishes.",
                            ["def:object", "lem:reduction"],
                            "Central theorem of the paper.",
                            "Prove an equivalence and connect it to sharp failure witnesses.")
                    ],
                    ["thm:main"],
                    ["A global gluing interface connecting local data to the observable."],
                    ["Prove a full if-and-only-if obstruction criterion."],
                    ["Retain only the forward implication under a finite-complexity hypothesis."],
                    ["Realize every minimal non-zero obstruction by an explicit failure object."],
                    "2026-08-31T09:50:00Z"));
            _inventory = PutContent(
                PaperTheoryFoundationSchemas.Inventory,
                "inventory.json",
                Inventory.InventoryContent,
                Inventory.InventoryId);

            Request = PaperTheoryDeepeningService.CreateDeepeningRequest(
                Program,
                Scope,
                Inventory,
                null,
                1,
                "2026-08-31T10:00:00Z");
            _request = PutContent(
                PaperTheoryDeepeningSchemas.DeepeningRequest,
                "deepening-request.json",
                Request.RequestContent,
                Request.RequestId);
        }

        public string Root { get; }
        public PaperTheoryProgram Program { get; }
        public PaperTheoryScope Scope { get; }
        public PaperTheoryInventory Inventory { get; }
        public PaperTheoryDeepeningRequest Request { get; }

        public PaperTheoryDeepeningAgentDispatch CreateDispatch() =>
            new(
                PaperTheoryDeepeningAgentSchemas.Dispatch,
                Program.ProgramContent.PaperId,
                Program.TheoryProgramId,
                Request.RequestId,
                [_program.ToInput(), _scope.ToInput(), _inventory.ToInput(), _request.ToInput()],
                Request.RequestContent.RequestedAt);

        public string WriteDispatch(
            PaperTheoryDeepeningAgentDispatch dispatch,
            string fileName)
        {
            string path = Path.Combine(Root, "inbox", "theory-deepening", fileName);
            File.WriteAllBytes(path, CanonicalJson.Serialize(dispatch));
            return path;
        }

        public PaperTheoryDeepeningAgentTaskStaged Stage()
        {
            string path = WriteDispatch(CreateDispatch(), "deepening.json");
            return PaperTheoryDeepeningAgentService.StageTask(Root, path);
        }

        public DeepeningRun RunAuditCandidate() => RunDraft(CreateAuditCandidateDraft());

        public DeepeningRun RunDraft(PaperTheoryDeepeningDraft draft)
        {
            PaperTheoryDeepeningAgentTaskStaged staged = Stage();
            PaperAgentTaskRegistration registration =
                PaperAgentRuntimeService.RegisterTask(Root, staged.TaskPath);
            Assert.Equal(staged.TaskRef, registration.TaskRef);
            PaperAgentTask task =
                PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
                    File.ReadAllBytes(staged.TaskPath));
            PaperAgentRunPrepared prepared =
                PaperAgentRuntimeService.PrepareRun(Root, staged.TaskRef);
            File.WriteAllBytes(
                Path.Combine(prepared.WorkspacePath, "outputs", "theory-deepening-draft.json"),
                CanonicalJson.Serialize(draft));
            var result = new PaperAgentResultWire(
                PaperAgentSchemas.AgentResult,
                staged.TaskRef,
                task.PaperId,
                task.TheoryProgramRef,
                task.Phase,
                task.AgentRole,
                task.ContextMode,
                "completed",
                "A2 produced a stronger obstruction theorem package with a closed proof spine.",
                [new PaperAgentOutputWire(
                    PaperTheoryDeepeningAgentSchemas.Draft,
                    "outputs/theory-deepening-draft.json")],
                "theory-audit",
                string.Empty,
                task.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
                "2026-08-31T14:00:00Z");
            WriteEnvelope(prepared.StdoutPath, result);
            PaperAgentResultRecorded recorded = PaperAgentRuntimeService.RecordResult(
                Root,
                staged.TaskRef,
                prepared.StdoutPath,
                "codex-a2-run-001",
                "produced");
            return new(staged, recorded);
        }

        public PaperTheoryDeepeningDraft CreateAuditCandidateDraft() =>
            new(
                PaperTheoryDeepeningAgentSchemas.Draft,
                Program.TheoryProgramId,
                Scope.ScopeId,
                Inventory.InventoryId,
                Request.RequestId,
                [],
                Program.ProgramContent.PaperId,
                1,
                new PaperTheoryIterationDraft(
                    [
                        "def:object",
                        "lem:reduction",
                        "thm:main",
                        "thm:sharp",
                        "cor:classification"
                    ],
                    ["thm:sharp", "cor:classification"],
                    ["lem:reduction", "thm:main"],
                    [],
                    [
                        "Construct the canonical descent datum and prove that its obstruction class is independent of all authorized coordinate choices.",
                        "Strengthen the reduction lemma into an exact gluing equivalence and use it to prove the central descent theorem in both directions.",
                        "Realize every minimal non-zero obstruction, prove sharp failure, and derive the classification corollary from the equivalence and realization theorems."
                    ],
                    "The round replaces a one-directional conjectural criterion with a canonical obstruction-valued abstraction, a proved equivalence, a sharp realization theorem, and a reusable classification corollary.",
                    "Classical cocycle and local-to-global lemmas remain cited tools; the exact obstruction equivalence, realization of every minimal obstruction, and resulting failure classification form the new theorem package.",
                    ["A complete minimal non-zero obstruction witness demonstrates that the equivalence cannot be weakened."],
                    ["thm:sharp", "cor:classification"],
                    ["paper-02"],
                    new PaperTheoryProgressEvidence(
                        2,
                        2,
                        4,
                        5,
                        1,
                        true,
                        true),
                    "2026-08-31T12:00:00Z"),
                new PaperTheoremPackageDraft(
                    "audit-candidate",
                    Claims(),
                    ["thm:main"],
                    ["cor:classification"],
                    ["thm:sharp"],
                    [],
                    [
                        "Classical local-to-global descent lemma, cited with its exact hypotheses.",
                        "Standard cocycle classification theorem, cited only as a known proof tool."
                    ],
                    "The novel result is the exact descent-obstruction equivalence together with realization and classification of every minimal sharp failure, under assumptions narrower and more explicit than the nearest prior descent statements.",
                    "The package supplies a canonical abstraction, a load-bearing equivalence theorem, a sharpness theorem, and a classification corollary that together meet a tier-two-or-higher publication floor.",
                    "2026-08-31T12:30:00Z"),
                [new PaperCandidateSplitProposalDraft(
                    Program.ProgramContent.PaperId + "-sharpness",
                    ["thm:sharp", "cor:classification"],
                    "Which minimal non-zero obstruction classes classify every irreducible failure of the descent mechanism?",
                    [
                        "Construct canonical representatives of minimal non-zero obstruction classes.",
                        "Prove that every representative forces failure and that every minimal failure yields such a class.",
                        "Derive the classification and compare its hypotheses with the positive descent theorem."
                    ],
                    "The sharpness-classification chain answers an independent negative classification question beyond the positive equivalence required by the source scope.",
                    "The extracted claims form a self-contained classification theorem with their own proof spine and nearest-prior-work comparison.",
                    "The split must share only the canonical definition and must not duplicate the positive equivalence theorem.",
                    "2026-08-31T13:00:00Z")],
                [
                    new PaperResearchLedgerEntryDraft(
                        "prior-work-boundary",
                        [],
                        "Nearest prior work supplies general descent and cocycle tools but does not establish the exact obstruction equivalence or realize every minimal sharp failure.",
                        "The ledger preserves the hypothesis and conclusion boundary that supports the claimed novelty of this A2 theorem package.",
                        "promoted",
                        "2026-08-31T13:10:00Z"),
                    new PaperResearchLedgerEntryDraft(
                        "split-candidate",
                        [],
                        "The sharpness and classification claims form an independent negative theory program with a complete proof spine and distinct central question.",
                        "The proposal is recorded so portfolio governance can decide whether to split it without padding the source paper.",
                        "candidate-seed",
                        "2026-08-31T13:11:00Z"),
                    new PaperResearchLedgerEntryDraft(
                        "merge-candidate",
                        [],
                        "Paper paper-02 appears to use an equivalent obstruction-valued descent abstraction and may share the central proof interface.",
                        "A separate cross-paper comparison is required before constructing any canonical merge proposal.",
                        "candidate-seed",
                        "2026-08-31T13:12:00Z"),
                    new PaperResearchLedgerEntryDraft(
                        "counterexample",
                        [],
                        "A minimal non-zero obstruction is realized by a complete sharpness witness that demonstrates exact failure of global descent.",
                        "The witness closes the scope's counterexample obligation and guards against silently weakening the main equivalence.",
                        "promoted",
                        "2026-08-31T13:13:00Z")
                ],
                "2026-08-31T13:30:00Z");

        public T ReadEnvelope<T>(string relativePath) =>
            PaperResearchInputJson.DeserializeStrict<T>(
                File.ReadAllBytes(Path.Combine(
                    Root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))));

        private static PaperTheoremPackageClaim[] Claims() =>
        [
            new(
                "def:object",
                "Canonical descent object",
                "definition",
                "Every admissible object carries a functorial local descent datum and a coordinate-independent obstruction class.",
                [],
                "informal-complete",
                [
                    "Construct the local datum functorially from the admissible object.",
                    "Verify invariance of the obstruction class under every authorized coordinate change."
                ],
                "strengthened",
                true),
            new(
                "lem:reduction",
                "Exact reduction to obstruction vanishing",
                "lemma",
                "Compatible local descent data glue globally if and only if the canonical obstruction class vanishes.",
                ["def:object"],
                "informal-complete",
                [
                    "Necessity follows by functoriality of the obstruction under a global datum.",
                    "Sufficiency follows from an explicit compatible gluing construction."
                ],
                "strengthened",
                true),
            new(
                "thm:main",
                "Structural descent equivalence",
                "theorem",
                "The target observable descends if and only if the canonical obstruction class vanishes.",
                ["def:object", "lem:reduction"],
                "informal-complete",
                [
                    "Represent the observable by compatible local data controlled by the canonical datum.",
                    "Apply the exact reduction lemma in both directions and identify global descent."
                ],
                "new",
                true),
            new(
                "thm:sharp",
                "Sharp obstruction realization theorem",
                "theorem",
                "Every minimal non-zero obstruction class is realized by an admissible object for which global descent fails.",
                ["def:object", "lem:reduction"],
                "informal-complete",
                [
                    "Construct an admissible representative for each minimal non-zero class.",
                    "Use non-vanishing and the exact reduction theorem to exclude every global descent datum."
                ],
                "new",
                true),
            new(
                "cor:classification",
                "Classification of minimal failures",
                "corollary",
                "Minimal failures of descent are classified by minimal non-zero obstruction classes.",
                ["thm:main", "thm:sharp"],
                "informal-complete",
                [
                    "Associate to each minimal failure its non-zero obstruction class.",
                    "Use the realization theorem and minimality to obtain the inverse classification."
                ],
                "new",
                true)
        ];

        private Evidence PutContent<T>(
            string schema,
            string fileName,
            T content,
            string expectedRef)
        {
            byte[] bytes = CanonicalJson.Serialize(content);
            Assert.Equal(expectedRef, PaperResearchInputStore.Reference(bytes));
            string relative = "artifacts/evidence/" + fileName;
            File.WriteAllBytes(
                Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)),
                bytes);
            return new(schema, expectedRef, relative);
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

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static string Digest(string seed) =>
        PaperResearchInputStore.Reference(Encoding.UTF8.GetBytes(seed));
}
