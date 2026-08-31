using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryAuditAgentTests
{
    [Fact]
    public void StageCreatesTwoDistinctFreshReviewerTasks()
    {
        using var repository = new AuditRepository("paper-01");

        PaperTheoryAuditAgentTasksStaged staged = repository.Stage();

        Assert.Equal(PaperTheoryAuditAgentSchemas.TasksStaged, staged.Schema);
        Assert.Equal(2, staged.Reviewers.Count);
        Assert.Equal(
            2,
            staged.Reviewers.Select(reviewer => reviewer.TaskRef)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            new[] { "mathematical-referee", "novelty-referee" },
            staged.Reviewers.Select(reviewer => reviewer.ReviewerRole).ToArray());
        foreach (PaperTheoryAuditPlannedReviewer reviewer in staged.Reviewers)
        {
            PaperAgentTask task = repository.ReadTask(reviewer.TaskPath);
            Assert.Equal("theory-audit", task.Phase);
            Assert.Equal("paper-theory-independent-referee", task.AgentRole);
            Assert.Equal(PaperTheoryAuditService.FreshContextMode, task.ContextMode);
            Assert.Contains(reviewer.ReviewerRole, task.ScientificInstruction, StringComparison.Ordinal);
            Assert.DoesNotContain(
                task.ExactInputs,
                input => input.Schema == PaperTheoryAuditSchemas.Audit);
            Assert.Contains(
                task.ExactInputs,
                input => input.ArtifactRef == repository.LiteratureRef);
        }
    }

    [Fact]
    public void SecondIndependentOpinionCreatesConservativeAuditAndScorecard()
    {
        using var repository = new AuditRepository("paper-01");
        PaperTheoryAuditAgentTasksStaged staged = repository.Stage();
        PaperTheoryAuditPlannedReviewer mathematical = staged.Reviewers[0];
        PaperTheoryAuditPlannedReviewer novelty = staged.Reviewers[1];

        repository.RunReviewer(
            mathematical,
            repository.OpinionDraft(
                mathematical.ReviewerRole,
                new PaperTheoryAuditMetrics(9, 9, 9, 9, 9, 9, 8, 8, 9),
                "pass"),
            "codex-a3-mathematical-run");
        PaperTheoryAuditAgentResultAdmitted first =
            PaperTheoryAuditAgentService.AdmitOpinion(
                repository.Root,
                mathematical.TaskRef);
        Assert.Equal(PaperTheoryAuditAgentService.WaitingStatus, first.AggregateStatus);
        Assert.Single(first.MissingTaskRefs);
        Assert.Null(first.Audit);
        Assert.Null(first.Scorecard);

        repository.RunReviewer(
            novelty,
            repository.OpinionDraft(
                novelty.ReviewerRole,
                new PaperTheoryAuditMetrics(8, 8, 8, 8, 8, 8, 7, 7, 8),
                "pass"),
            "codex-a3-novelty-run");
        PaperTheoryAuditAgentResultAdmitted second =
            PaperTheoryAuditAgentService.AdmitOpinion(
                repository.Root,
                novelty.TaskRef);

        Assert.Equal(PaperTheoryAuditAgentService.ReadyStatus, second.AggregateStatus);
        Assert.NotNull(second.Audit);
        Assert.NotNull(second.Scorecard);
        Assert.True(second.Passed);
        Assert.True(second.PromotionEligible);
        Assert.Equal("pass", second.Verdict);
        Assert.Equal("portfolio-judgment", second.NextRoute);

        PaperTheoryAudit audit = repository.ReadEnvelope<PaperTheoryAudit>(
            second.Audit!.EnvelopePath);
        PaperCandidateScorecard scorecard = repository.ReadEnvelope<PaperCandidateScorecard>(
            second.Scorecard!.EnvelopePath);
        PaperTheoryAuditService.Validate(audit);
        PaperPortfolioDecisionService.Validate(scorecard);
        Assert.Equal(2, audit.AuditContent.Opinions.Count);
        Assert.Equal(8, audit.AuditContent.AggregateMetrics.AbstractionQuality);
        Assert.Equal(8, audit.AuditContent.AggregateMetrics.Novelty);
        Assert.Equal(7, audit.AuditContent.AggregateMetrics.JournalFloor);
        Assert.Equal(audit.AuditId, scorecard.ScorecardContent.TheoryAuditRef);

        PaperTheoryAuditAgentResultAdmitted replay =
            PaperTheoryAuditAgentService.AdmitOpinion(
                repository.Root,
                novelty.TaskRef);
        Assert.True(replay.Replayed);
        Assert.Equal(second.Audit.ArtifactRef, replay.Audit!.ArtifactRef);
        Assert.Equal(second.Scorecard.ArtifactRef, replay.Scorecard!.ArtifactRef);
    }

    [Fact]
    public void SkepticalReviewerControlsCoordinateMinimumAndBlocksPromotion()
    {
        using var repository = new AuditRepository("paper-01");
        PaperTheoryAuditAgentTasksStaged staged = repository.Stage();
        PaperTheoryAuditPlannedReviewer mathematical = staged.Reviewers[0];
        PaperTheoryAuditPlannedReviewer novelty = staged.Reviewers[1];

        repository.RunReviewer(
            mathematical,
            repository.OpinionDraft(
                mathematical.ReviewerRole,
                new PaperTheoryAuditMetrics(10, 10, 10, 10, 10, 10, 10, 10, 10),
                "pass"),
            "codex-a3-optimistic-run");
        _ = PaperTheoryAuditAgentService.AdmitOpinion(
            repository.Root,
            mathematical.TaskRef);

        repository.RunReviewer(
            novelty,
            repository.OpinionDraft(
                novelty.ReviewerRole,
                new PaperTheoryAuditMetrics(8, 8, 8, 8, 6, 7, 7, 7, 8),
                "deepen",
                blockers: ["The exact novelty delta is still too close to the nearest supplied theorem."],
                revisions: ["Strengthen the hypothesis or conclusion delta before formalization."]),
            "codex-a3-skeptical-run");
        PaperTheoryAuditAgentResultAdmitted admitted =
            PaperTheoryAuditAgentService.AdmitOpinion(
                repository.Root,
                novelty.TaskRef);

        Assert.Equal(PaperTheoryAuditAgentService.ReadyStatus, admitted.AggregateStatus);
        Assert.False(admitted.Passed);
        Assert.False(admitted.PromotionEligible);
        Assert.Equal("deepen", admitted.Verdict);
        Assert.Equal("theory-deepening", admitted.NextRoute);
        PaperTheoryAudit audit = repository.ReadEnvelope<PaperTheoryAudit>(
            admitted.Audit!.EnvelopePath);
        Assert.Equal(6, audit.AuditContent.AggregateMetrics.Novelty);
        Assert.Equal(2, audit.AuditContent.BlockerLedger.Count);
    }

    [Fact]
    public void ReviewersCannotShareOneCodexRun()
    {
        using var repository = new AuditRepository("paper-01");
        PaperTheoryAuditAgentTasksStaged staged = repository.Stage();
        foreach (PaperTheoryAuditPlannedReviewer reviewer in staged.Reviewers)
        {
            repository.RunReviewer(
                reviewer,
                repository.OpinionDraft(
                    reviewer.ReviewerRole,
                    new PaperTheoryAuditMetrics(8, 8, 8, 8, 8, 8, 8, 8, 8),
                    "pass"),
                "shared-codex-run");
        }
        _ = PaperTheoryAuditAgentService.AdmitOpinion(
            repository.Root,
            staged.Reviewers[0].TaskRef);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryAuditAgentService.AdmitOpinion(
                repository.Root,
                staged.Reviewers[1].TaskRef));

        Assert.Contains("distinct fresh Codex runs", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditDispatchCannotDropLiteratureEvidence()
    {
        using var repository = new AuditRepository("paper-01");
        PaperTheoryAuditAgentDispatch dispatch = repository.CreateDispatch();
        PaperTheoryAuditAgentDispatch incomplete = dispatch with
        {
            ExactInputs = dispatch.ExactInputs
                .Where(input => input.ArtifactRef != repository.LiteratureRef)
                .ToArray()
        };
        string path = repository.WriteDispatch(incomplete, "missing-literature.json");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryAuditAgentService.StageTasks(repository.Root, path));

        Assert.Contains("clean-room context closure", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditDispatchRejectsPriorAuditHistoryAsHiddenContext()
    {
        using var repository = new AuditRepository("paper-01");
        Evidence priorAudit = repository.PutOpaqueEvidence(
            PaperTheoryAuditSchemas.Audit,
            "prior-audit.json",
            new { schema = PaperTheoryAuditSchemas.Audit, verdict = "pass" });
        PaperTheoryAuditAgentDispatch dispatch = repository.CreateDispatch() with
        {
            ExactInputs = repository.CreateDispatch().ExactInputs
                .Append(priorAudit.ToInput())
                .ToArray()
        };
        string path = repository.WriteDispatch(dispatch, "hidden-prior-audit.json");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryAuditAgentService.StageTasks(repository.Root, path));

        Assert.Contains("clean-room context closure", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MathematicalAndNoveltyRolesAreMandatory()
    {
        using var repository = new AuditRepository("paper-01");
        PaperTheoryAuditAgentDispatch dispatch = repository.CreateDispatch() with
        {
            Reviewers =
            [
                new PaperTheoryAuditReviewerSpec(
                    1,
                    "scope-referee",
                    "Audit the publication scope and whether the theorem package closes its announced obligations.",
                    1),
                new PaperTheoryAuditReviewerSpec(
                    2,
                    "formalization-referee",
                    "Audit the dependency interfaces and whether the package can be decomposed into formal claims.",
                    1)
            ]
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryAuditAgentService.Validate(dispatch));

        Assert.Contains("mathematical-referee", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentPapersCreateIndependentReviewPlansAndTasks()
    {
        using var first = new AuditRepository("paper-01");
        using var second = new AuditRepository("paper-02");

        PaperTheoryAuditAgentTasksStaged firstPlan = first.Stage();
        PaperTheoryAuditAgentTasksStaged secondPlan = second.Stage();

        Assert.NotEqual(firstPlan.ReviewPlan.ArtifactRef, secondPlan.ReviewPlan.ArtifactRef);
        Assert.Empty(
            firstPlan.Reviewers.Select(value => value.TaskRef)
                .Intersect(secondPlan.Reviewers.Select(value => value.TaskRef), StringComparer.Ordinal));
    }

    private sealed record Evidence(
        string Schema,
        string ArtifactRef,
        string RepositoryRelativePath)
    {
        public PaperAgentInputArtifact ToInput() =>
            new(Schema, ArtifactRef, RepositoryRelativePath);
    }

    private sealed class AuditRepository : IDisposable
    {
        private readonly Evidence _candidate;
        private readonly Evidence _literature;
        private readonly Evidence _intuition;
        private readonly Evidence _researchInput;
        private readonly Evidence _program;
        private readonly Evidence _scope;
        private readonly Evidence _inventory;
        private readonly Evidence _package;
        private readonly Evidence _request;

        public AuditRepository(string paperId)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "trureturing-paper-a3-agent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "theory-audit"));
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "agent-tasks"));
            Directory.CreateDirectory(Path.Combine(Root, "artifacts", "evidence"));

            _candidate = PutOpaqueEvidence(
                CandidateArtifactSchemas.CandidatePaper,
                "candidate.json",
                new
                {
                    schema = CandidateArtifactSchemas.CandidatePaper,
                    paper_id = paperId,
                    thesis = "A canonical obstruction exactly controls descent."
                });
            _literature = PutOpaqueEvidence(
                CandidateArtifactSchemas.LiteratureResearch,
                "literature.json",
                new
                {
                    schema = CandidateArtifactSchemas.LiteratureResearch,
                    paper_id = paperId,
                    nearest_prior = "Known tools stop before the exact equivalence and sharp realization."
                });
            _intuition = PutOpaqueEvidence(
                "paper-intuition-proposal.v1",
                "intuition.json",
                new
                {
                    schema = "paper-intuition-proposal.v1",
                    paper_id = paperId,
                    proposal = "Use a functorial obstruction class as the canonical abstraction."
                });
            _researchInput = PutOpaqueEvidence(
                PaperResearchInputSchemas.ResearchInput,
                "research-input.json",
                new
                {
                    schema = PaperResearchInputSchemas.ResearchInput,
                    paper_id = paperId,
                    release = "exact"
                });

            PaperCandidateBatch batch = PaperPortfolioService.CreateBatch(
                new PaperCandidateBatchContent(
                    Digest("truth-" + paperId),
                    Digest("topology-" + paperId),
                    _researchInput.ArtifactRef,
                    new PaperPortfolioPolicy(5, 2, 1, 1),
                    [
                        new PaperCandidateSeed(
                            paperId,
                            _candidate.ArtifactRef,
                            _literature.ArtifactRef,
                            _intuition.ArtifactRef,
                            90,
                            "2026-08-31T09:00:00Z"),
                        new PaperCandidateSeed(
                            paperId + "-peer",
                            Digest("peer-candidate-" + paperId),
                            Digest("peer-literature-" + paperId),
                            Digest("peer-intuition-" + paperId),
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
                    "A theorem paper with an exact equivalence, sharp realization theorem, and classification corollary.",
                    [
                        "Define the canonical descent object.",
                        "Prove the central obstruction equivalence.",
                        "Construct sharp failures and classify minimal obstructions."
                    ],
                    ["Classical descent and cocycle tools used with explicit citations."],
                    ["Applications that do not contribute to the central theorem chain."],
                    "Split only a theorem chain with an independent question and proof spine.",
                    ["Realize a minimal non-zero obstruction that forces descent failure."],
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
                            "Each admissible object has canonical local descent data.",
                            [],
                            "Provides the language for every theorem.",
                            "Stabilize the definition and coordinate invariance."),
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
                            "Prove an equivalence and sharp failure boundary.")
                    ],
                    ["thm:main"],
                    ["A compatible global gluing interface."],
                    ["Prove a full if-and-only-if obstruction criterion."],
                    ["Retain only the forward implication under finite complexity."],
                    ["Realize every minimal non-zero obstruction by an explicit failure object."],
                    "2026-08-31T09:50:00Z"));
            _inventory = PutContent(
                PaperTheoryFoundationSchemas.Inventory,
                "inventory.json",
                Inventory.InventoryContent,
                Inventory.InventoryId);

            PaperTheoryDeepeningRequest deepeningRequest =
                PaperTheoryDeepeningService.CreateDeepeningRequest(
                    Program,
                    Scope,
                    Inventory,
                    null,
                    1,
                    "2026-08-31T10:00:00Z");
            PaperTheoryIteration iteration =
                PaperTheoryDeepeningService.CreateIteration(
                    Program,
                    Scope,
                    Inventory,
                    deepeningRequest,
                    new PaperTheoryIterationContent(
                        Program.TheoryProgramId,
                        Scope.ScopeId,
                        Inventory.InventoryId,
                        deepeningRequest.RequestId,
                        [],
                        paperId,
                        1,
                        ["lem:reduction", "thm:sharp", "cor:classification"],
                        ["thm:sharp", "cor:classification"],
                        ["lem:reduction"],
                        [],
                        [
                            "Construct the canonical local descent datum and obstruction class.",
                            "Strengthen the reduction lemma to an exact gluing criterion and prove the central equivalence.",
                            "Realize minimal non-zero obstructions and derive the failure classification."
                        ],
                        "The package establishes an exact obstruction equivalence, a sharp realization theorem, and a classification corollary beyond the one-directional inventory result.",
                        "Classical descent and cocycle lemmas remain cited tools; the exact equivalence and minimal failure realization are the paper's new results.",
                        ["A minimal non-zero obstruction gives a sharp failure witness."],
                        ["thm:sharp", "cor:classification"],
                        [],
                        new PaperTheoryProgressEvidence(2, 1, 2, 2, 1, true, true),
                        "2026-08-31T10:10:00Z"));
            TheoremPackage = PaperTheoryDeepeningService.CreateTheoremPackage(
                Program,
                Scope,
                Inventory,
                iteration,
                new PaperTheoremPackageContent(
                    Program.TheoryProgramId,
                    Scope.ScopeId,
                    Inventory.InventoryId,
                    iteration.IterationId,
                    paperId,
                    1,
                    "audit-candidate",
                    Claims(),
                    ["thm:main"],
                    ["cor:classification"],
                    ["thm:sharp"],
                    [],
                    [
                        "Classical local-to-global descent lemma with a precise citation.",
                        "Standard cocycle classification theorem used as a known tool."
                    ],
                    "The new contribution is the exact descent-obstruction equivalence together with realization and classification of every minimal sharp failure.",
                    "The theorem chain supplies a canonical abstraction, a main equivalence, a sharp converse, and a reusable classification corollary at publication scale.",
                    "2026-08-31T10:20:00Z"));
            _package = PutContent(
                PaperTheoryDeepeningSchemas.TheoremPackage,
                "theorem-package.json",
                TheoremPackage.TheoremPackageContent,
                TheoremPackage.TheoremPackageId);

            Request = PaperTheoryAuditService.CreateAuditRequest(
                Program,
                Scope,
                Inventory,
                TheoremPackage,
                Digest("theory-author-run-" + paperId),
                "2026-08-31T10:30:00Z");
            _request = PutContent(
                PaperTheoryAuditSchemas.AuditRequest,
                "audit-request.json",
                Request.RequestContent,
                Request.RequestId);
        }

        public string Root { get; }
        public PaperTheoryProgram Program { get; }
        public PaperTheoryScope Scope { get; }
        public PaperTheoryInventory Inventory { get; }
        public PaperTheoremPackage TheoremPackage { get; }
        public PaperTheoryAuditRequest Request { get; }
        public string LiteratureRef => _literature.ArtifactRef;

        public PaperTheoryAuditAgentDispatch CreateDispatch() =>
            new(
                PaperTheoryAuditAgentSchemas.Dispatch,
                Program.ProgramContent.PaperId,
                Program.TheoryProgramId,
                Request.RequestId,
                [
                    _program.ToInput(),
                    _scope.ToInput(),
                    _inventory.ToInput(),
                    _package.ToInput(),
                    _request.ToInput(),
                    _candidate.ToInput(),
                    _literature.ToInput(),
                    _intuition.ToInput(),
                    _researchInput.ToInput()
                ],
                [
                    new PaperTheoryAuditReviewerSpec(
                        1,
                        "mathematical-referee",
                        "Reconstruct the load-bearing proof spine, test every hypothesis, and identify any logical gap or false converse.",
                        1),
                    new PaperTheoryAuditReviewerSpec(
                        2,
                        "novelty-referee",
                        "Compare theorem-level assumptions and conclusions against the supplied literature and sibling evidence, then assess publication significance.",
                        1)
                ],
                Request.RequestContent.RequestedAt);

        public string WriteDispatch(PaperTheoryAuditAgentDispatch dispatch, string fileName)
        {
            string path = Path.Combine(Root, "inbox", "theory-audit", fileName);
            File.WriteAllBytes(path, CanonicalJson.Serialize(dispatch));
            return path;
        }

        public PaperTheoryAuditAgentTasksStaged Stage() =>
            PaperTheoryAuditAgentService.StageTasks(
                Root,
                WriteDispatch(CreateDispatch(), "audit.json"));

        public PaperAgentTask ReadTask(string relativePath) =>
            PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
                File.ReadAllBytes(Path.Combine(
                    Root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))));

        public PaperTheoryAuditOpinionDraft OpinionDraft(
            string reviewerRole,
            PaperTheoryAuditMetrics metrics,
            string verdict,
            IReadOnlyList<string>? blockers = null,
            IReadOnlyList<string>? revisions = null) =>
            new(
                PaperTheoryAuditAgentSchemas.OpinionDraft,
                Program.ProgramContent.PaperId,
                Program.TheoryProgramId,
                Request.RequestId,
                TheoremPackage.TheoremPackageId,
                reviewerRole,
                metrics,
                verdict,
                blockers ?? [],
                revisions ?? [],
                "The exact equivalence and sharp realization are absent from the supplied nearest-prior theorem evidence, while every classical descent tool is explicitly separated and attributed.",
                [
                    "Reconstruct necessity from functoriality of the obstruction and verify every hypothesis used by the main theorem.",
                    "Reconstruct sufficiency through the explicit gluing map and verify that the sharp realization closes the converse boundary."
                ],
                ["The supplied literature contains no equivalent theorem package with the same assumptions, conclusion, sharpness result, and classification corollary."],
                "2026-08-31T11:00:00Z");

        public PaperAgentResultRecorded RunReviewer(
            PaperTheoryAuditPlannedReviewer reviewer,
            PaperTheoryAuditOpinionDraft draft,
            string runId)
        {
            string taskPath = Path.Combine(
                Root,
                reviewer.TaskPath.Replace('/', Path.DirectorySeparatorChar));
            PaperAgentTaskRegistration registration =
                PaperAgentRuntimeService.RegisterTask(Root, taskPath);
            Assert.Equal(reviewer.TaskRef, registration.TaskRef);
            PaperAgentTask task = ReadTask(reviewer.TaskPath);
            PaperAgentRunPrepared prepared =
                PaperAgentRuntimeService.PrepareRun(Root, reviewer.TaskRef);
            File.WriteAllBytes(
                Path.Combine(prepared.WorkspacePath, "outputs", "theory-audit-opinion.json"),
                CanonicalJson.Serialize(draft));
            var result = new PaperAgentResultWire(
                PaperAgentSchemas.AgentResult,
                reviewer.TaskRef,
                task.PaperId,
                task.TheoryProgramRef,
                task.Phase,
                task.AgentRole,
                task.ContextMode,
                "completed",
                "fresh A3 opinion completed",
                [new PaperAgentOutputWire(
                    PaperTheoryAuditAgentSchemas.OpinionDraft,
                    "outputs/theory-audit-opinion.json")],
                "theory-audit-opinion",
                string.Empty,
                task.ExactInputs.Select(input => input.ArtifactRef)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                "2026-08-31T11:00:00Z");
            File.WriteAllText(
                prepared.StdoutPath,
                PaperAgentRuntimeService.ResultBegin
                + "\n"
                + Encoding.UTF8.GetString(CanonicalJson.Serialize(result))
                + "\n"
                + PaperAgentRuntimeService.ResultEnd
                + "\n");
            return PaperAgentRuntimeService.RecordResult(
                Root,
                reviewer.TaskRef,
                prepared.StdoutPath,
                runId,
                "produced");
        }

        public Evidence PutOpaqueEvidence<T>(string schema, string fileName, T value)
        {
            byte[] bytes = CanonicalJson.Serialize(value);
            string reference = PaperResearchInputStore.Reference(bytes);
            string relative = "artifacts/evidence/" + fileName;
            File.WriteAllBytes(
                Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)),
                bytes);
            return new(schema, reference, relative);
        }

        public T ReadEnvelope<T>(string relativePath) =>
            PaperResearchInputJson.DeserializeStrict<T>(
                File.ReadAllBytes(Path.Combine(
                    Root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

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

        private static IReadOnlyList<PaperTheoremPackageClaim> Claims() =>
        [
            Claim(
                "def:object",
                "Canonical descent object",
                "definition",
                "Every admissible object carries canonical local descent data and an obstruction class.",
                []),
            Claim(
                "lem:reduction",
                "Exact reduction lemma",
                "lemma",
                "Local descent data glue globally exactly when the canonical obstruction vanishes.",
                ["def:object"]),
            Claim(
                "thm:main",
                "Structural descent equivalence",
                "theorem",
                "The target observable descends if and only if the canonical obstruction vanishes.",
                ["def:object", "lem:reduction"]),
            Claim(
                "thm:sharp",
                "Sharp realization theorem",
                "theorem",
                "Every minimal non-zero obstruction is realized by an admissible object for which descent fails.",
                ["def:object", "lem:reduction"]),
            Claim(
                "cor:classification",
                "Failure classification",
                "corollary",
                "Minimal failures are classified by minimal non-zero obstruction classes.",
                ["thm:main", "thm:sharp"])
        ];

        private static PaperTheoremPackageClaim Claim(
            string id,
            string title,
            string kind,
            string statement,
            IReadOnlyList<string> dependencies) =>
            new(
                id,
                title,
                kind,
                statement,
                dependencies,
                "informal-complete",
                [
                    "Construct the canonical objects and verify all hypotheses used by this claim.",
                    "Derive the stated conclusion along the dependency spine and check the sharp boundary."
                ],
                kind == "definition" ? "strengthened" : "new",
                true);
    }

    private static string Digest(string value) =>
        PaperResearchInputStore.Reference(Encoding.UTF8.GetBytes(value));
}
