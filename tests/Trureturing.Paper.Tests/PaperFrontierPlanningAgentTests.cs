using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierPlanningAgentTests
{
    [Fact]
    public void PromotedPaperRunsThroughNativePlannerAndReleasesWaveZero()
    {
        using var repository = new FrontierPlanningRepository();
        FrontierPlanningRun run = repository.RunPromotedPaper();

        Assert.Equal("frontier-planning", run.Staged.Phase);
        Assert.Equal("paper-formalization-frontier-planner", run.Staged.AgentRole);
        Assert.Equal("promotion-bound-planning", run.Staged.ContextMode);
        Assert.Equal("completed", run.AgentResult.Status);
        Assert.Equal(
            PaperFrontierPlanningAgentSchemas.Draft,
            Assert.Single(run.AgentResult.Outputs).Schema);

        PaperFrontierPlanningAgentResultAdmitted admitted =
            PaperFrontierPlanningAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef);

        Assert.False(admitted.Replayed);
        Assert.Equal(repository.PromotedPaperId, admitted.PaperId);
        Assert.Equal(repository.PortfolioTaskRef, admitted.PortfolioTaskRef);
        Assert.Equal(repository.SourceResultRef, admitted.PortfolioResultRef);
        Assert.Equal(repository.SourceDecision.DecisionId, admitted.PortfolioDecisionRef);

        PaperFormalizationFrontier frontier = repository.ReadEnvelope<PaperFormalizationFrontier>(
            admitted.Frontier.EnvelopePath);
        PaperFormalizationFrontierState state = repository.ReadEnvelope<PaperFormalizationFrontierState>(
            admitted.InitialState.EnvelopePath);
        PaperFormalizationFrontierService.Validate(frontier);
        PaperFormalizationFrontierLifecycleService.Validate(state, frontier);

        Assert.Equal(5, frontier.FrontierContent.Nodes.Count);
        Assert.Equal(4, frontier.FrontierContent.CriticalPathDepth);
        Assert.Equal(2, frontier.FrontierContent.MaximumWaveWidth);
        Assert.Equal(0, state.StateContent.Version);
        Assert.All(
            state.StateContent.NodeStates,
            node => Assert.Equal("selection-pending", node.Status));

        PaperFrontierPlanningNodeRoute initial = Assert.Single(admitted.InitialNodeRoutes);
        Assert.Equal("def:object", initial.ClaimId);
        Assert.Equal("definition", initial.FormalizationKind);
        Assert.Equal(0, initial.ParallelWave);
        Assert.Equal("governed-selection", initial.NextRoute);
        Assert.Equal(
            frontier.FrontierContent.Nodes.Single(node => node.ClaimId == "def:object").NodeId,
            initial.NodeId);

        PaperFrontierPlanningAgentResultAdmitted replay =
            PaperFrontierPlanningAgentService.AdmitResult(
                repository.Root,
                run.Staged.TaskRef);
        Assert.True(replay.Replayed);
        Assert.Equal(admitted.Frontier.ArtifactRef, replay.Frontier.ArtifactRef);
        Assert.Equal(admitted.InitialState.ArtifactRef, replay.InitialState.ArtifactRef);
        Assert.Equal(admitted.InitialNodeRoutes, replay.InitialNodeRoutes);
    }

    [Fact]
    public void HeldPaperCannotStageFrontierPlanning()
    {
        using var repository = new FrontierPlanningRepository();

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperFrontierPlanningAgentService.StageTask(
                repository.Root,
                repository.PortfolioTaskRef,
                repository.HeldPaperId));

        Assert.Contains("portfolio-promoted", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftCannotDropAnAdmittedTheoremClaim()
    {
        using var repository = new FrontierPlanningRepository();
        PaperFrontierPlanningAgentTaskStaged staged = repository.StagePromotedPaper();
        PaperFrontierPlanningAgentDispatch dispatch = repository.ReadDispatch(staged.DispatchRef);
        PaperFrontierPlanningContext context = repository.ContextForPromotedPaper();
        PaperFormalizationFrontierDraft complete = repository.CreateDraft(staged.DispatchRef);
        PaperFormalizationFrontierDraft incomplete = complete with
        {
            NodeSpecs = complete.NodeSpecs
                .Where(spec => spec.ClaimId != "thm:sharp")
                .ToArray()
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperFrontierPlanningAgentService.Compute(
                dispatch,
                staged.DispatchRef,
                context,
                incomplete,
                "2026-08-31T05:00:00Z"));

        Assert.Contains("claim set", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MainTheoremKindCannotBeDowngradedByPlanner()
    {
        using var repository = new FrontierPlanningRepository();
        PaperFrontierPlanningAgentTaskStaged staged = repository.StagePromotedPaper();
        PaperFrontierPlanningAgentDispatch dispatch = repository.ReadDispatch(staged.DispatchRef);
        PaperFrontierPlanningContext context = repository.ContextForPromotedPaper();
        PaperFormalizationFrontierDraft complete = repository.CreateDraft(staged.DispatchRef);
        PaperFormalizationFrontierDraft downgraded = complete with
        {
            NodeSpecs = complete.NodeSpecs.Select(spec => spec.ClaimId == "thm:main"
                ? spec with { FormalizationKind = "structural" }
                : spec).ToArray()
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperFrontierPlanningAgentService.Compute(
                dispatch,
                staged.DispatchRef,
                context,
                downgraded,
                "2026-08-31T05:00:00Z"));

        Assert.Contains("main theorem", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactEvidenceDigestDriftFailsBeforeTaskAdmission()
    {
        using var repository = new FrontierPlanningRepository();
        File.WriteAllText(
            repository.PromotedCandidatePath,
            "candidate-paper-a-tampered",
            Encoding.UTF8);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => repository.StagePromotedPaper());

        Assert.Contains("content-address", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Evidence(
        string Schema,
        string ArtifactRef,
        string RepositoryRelativePath)
    {
        public PaperAgentInputArtifact ToInput() =>
            new(Schema, ArtifactRef, RepositoryRelativePath);
    }

    private sealed record FrontierPlanningRun(
        PaperFrontierPlanningAgentTaskStaged Staged,
        PaperAgentResultRecorded AgentResult);

    private sealed class FrontierPlanningRepository : IDisposable
    {
        private readonly PaperTheoryFixture _theory;
        private readonly IReadOnlyDictionary<string, PaperTheoryAudit> _audits;
        private readonly IReadOnlyDictionary<string, PaperCandidateScorecard> _scorecards;
        private readonly IReadOnlyDictionary<string, PaperPortfolioJudgmentPaperInput> _coordinates;
        private readonly IReadOnlyDictionary<string, Evidence> _programs;
        private readonly IReadOnlyDictionary<string, Evidence> _scopes;
        private readonly IReadOnlyDictionary<string, Evidence> _inventories;
        private readonly IReadOnlyDictionary<string, Evidence> _packages;
        private readonly IReadOnlyDictionary<string, Evidence> _auditEvidence;
        private readonly IReadOnlyDictionary<string, Evidence> _scorecardEvidence;
        private readonly IReadOnlyDictionary<string, Evidence> _candidateEvidence;
        private readonly IReadOnlyDictionary<string, Evidence> _literatureEvidence;
        private readonly PaperPortfolioJudgmentAgentDispatch _sourceDispatch;
        private readonly string _sourceDispatchRef;
        private readonly PaperPortfolioJudgmentAgentAdmissionCursor _sourceCursor;

        public FrontierPlanningRepository()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "trureturing-paper-frontier-planning-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(Root, "inbox", "agent-tasks"));
            Directory.CreateDirectory(Path.Combine(Root, "artifacts", "source"));

            _theory = PaperTheoryTestFactory.CreatePortfolio("paper-a", "paper-b");
            PromotedPaperId = "paper-a";
            HeldPaperId = "paper-b";
            PortfolioTaskRef = Digest("portfolio-task");
            SourceResultRef = Digest("portfolio-result");

            var audits = new Dictionary<string, PaperTheoryAudit>(StringComparer.Ordinal);
            var scorecards = new Dictionary<string, PaperCandidateScorecard>(StringComparer.Ordinal);
            var coordinates = new Dictionary<string, PaperPortfolioJudgmentPaperInput>(StringComparer.Ordinal);
            var programs = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var scopes = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var inventories = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var packages = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var auditEvidence = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var scorecardEvidence = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var candidateEvidence = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var literatureEvidence = new Dictionary<string, Evidence>(StringComparer.Ordinal);
            var contexts = new List<PaperPortfolioJudgmentPaperContext>();

            foreach (string paperId in new[] { PromotedPaperId, HeldPaperId })
            {
                PaperTheoryAuditMetrics metrics = paperId == PromotedPaperId
                    ? PaperTheoryTestFactory.Metrics(
                        abstraction: 10,
                        depth: 10,
                        closure: 10,
                        proof: 10,
                        novelty: 10,
                        significance: 10,
                        formalization: 10,
                        journal: 10,
                        overlap: 10)
                    : PaperTheoryTestFactory.Metrics();
                PaperTheoryAudit audit = PaperTheoryTestFactory.CreateAudit(
                    _theory,
                    paperId,
                    metrics);
                PaperCandidateScorecard scorecard =
                    PaperPortfolioDecisionService.CreateScorecard(
                        _theory.Packages[paperId],
                        audit,
                        "2026-08-31T03:00:00Z");
                audits.Add(paperId, audit);
                scorecards.Add(paperId, scorecard);

                PaperTheoryProgram program = _theory.Programs.Single(
                    value => value.ProgramContent.PaperId == paperId);
                var item = new PaperPortfolioJudgmentPaperInput(
                    paperId,
                    program.TheoryProgramId,
                    _theory.Scopes[paperId].ScopeId,
                    _theory.Inventories[paperId].InventoryId,
                    _theory.Packages[paperId].TheoremPackageId,
                    audit.AuditId,
                    scorecard.ScorecardId,
                    program.ProgramContent.CandidatePaperRef,
                    program.ProgramContent.LiteratureResearchRef);
                coordinates.Add(paperId, item);
                contexts.Add(new(
                    item,
                    program,
                    _theory.Scopes[paperId],
                    _theory.Inventories[paperId],
                    _theory.Packages[paperId],
                    audit,
                    scorecard));

                programs.Add(paperId, PutContent(
                    PaperPortfolioSchemas.TheoryProgram,
                    $"{paperId}-program.json",
                    program.TheoryProgramId,
                    program.ProgramContent));
                scopes.Add(paperId, PutContent(
                    PaperTheoryFoundationSchemas.Scope,
                    $"{paperId}-scope.json",
                    _theory.Scopes[paperId].ScopeId,
                    _theory.Scopes[paperId].ScopeContent));
                inventories.Add(paperId, PutContent(
                    PaperTheoryFoundationSchemas.Inventory,
                    $"{paperId}-inventory.json",
                    _theory.Inventories[paperId].InventoryId,
                    _theory.Inventories[paperId].InventoryContent));
                packages.Add(paperId, PutContent(
                    PaperTheoryDeepeningSchemas.TheoremPackage,
                    $"{paperId}-package.json",
                    _theory.Packages[paperId].TheoremPackageId,
                    _theory.Packages[paperId].TheoremPackageContent));
                auditEvidence.Add(paperId, PutContent(
                    PaperTheoryAuditSchemas.Audit,
                    $"{paperId}-audit.json",
                    audit.AuditId,
                    audit.AuditContent));
                scorecardEvidence.Add(paperId, PutContent(
                    PaperPortfolioDecisionSchemas.Scorecard,
                    $"{paperId}-scorecard.json",
                    scorecard.ScorecardId,
                    scorecard.ScorecardContent));
                candidateEvidence.Add(paperId, PutSeed(
                    CandidateArtifactSchemas.CandidatePaper,
                    $"{paperId}-candidate.json",
                    $"candidate-{paperId}",
                    program.ProgramContent.CandidatePaperRef));
                literatureEvidence.Add(paperId, PutSeed(
                    CandidateArtifactSchemas.LiteratureResearch,
                    $"{paperId}-literature.json",
                    $"literature-{paperId}",
                    program.ProgramContent.LiteratureResearchRef));
            }
            _audits = audits;
            _scorecards = scorecards;
            _coordinates = coordinates;
            _programs = programs;
            _scopes = scopes;
            _inventories = inventories;
            _packages = packages;
            _auditEvidence = auditEvidence;
            _scorecardEvidence = scorecardEvidence;
            _candidateEvidence = candidateEvidence;
            _literatureEvidence = literatureEvidence;
            PromotedCandidatePath = Path.Combine(
                Root,
                _candidateEvidence[PromotedPaperId].RepositoryRelativePath
                    .Replace('/', Path.DirectorySeparatorChar));

            Evidence portfolio = PutContent(
                PaperPortfolioSchemas.Portfolio,
                "portfolio.json",
                _theory.Portfolio.PortfolioId,
                _theory.Portfolio.PortfolioContent);
            Evidence batch = PutContent(
                PaperPortfolioSchemas.CandidateBatch,
                "candidate-batch.json",
                _theory.Batch.BatchId,
                _theory.Batch.BatchContent);
            var inputs = new List<PaperAgentInputArtifact>
            {
                portfolio.ToInput(),
                batch.ToInput()
            };
            foreach (string paperId in new[] { PromotedPaperId, HeldPaperId })
            {
                inputs.Add(_programs[paperId].ToInput());
                inputs.Add(_scopes[paperId].ToInput());
                inputs.Add(_inventories[paperId].ToInput());
                inputs.Add(_packages[paperId].ToInput());
                inputs.Add(_auditEvidence[paperId].ToInput());
                inputs.Add(_scorecardEvidence[paperId].ToInput());
                inputs.Add(_candidateEvidence[paperId].ToInput());
                inputs.Add(_literatureEvidence[paperId].ToInput());
            }
            _sourceDispatch = new PaperPortfolioJudgmentAgentDispatch(
                PaperPortfolioJudgmentAgentSchemas.Dispatch,
                _theory.Portfolio.PortfolioId,
                _theory.Batch.BatchId,
                _theory.Portfolio.PortfolioContent.NextCycleNumber,
                new PaperPortfolioDecisionPolicy(1, 2),
                [coordinates[PromotedPaperId], coordinates[HeldPaperId]],
                inputs,
                "2026-08-31T03:10:00Z");
            byte[] sourceDispatchBytes = CanonicalJson.Serialize(_sourceDispatch);
            _sourceDispatchRef = PaperResearchInputStore.Reference(sourceDispatchBytes);
            WriteCanonicalPortfolioDispatch(_sourceDispatchRef, sourceDispatchBytes);

            var sourceContext = new PaperPortfolioJudgmentContext(
                _theory.Portfolio,
                _theory.Batch,
                contexts);
            PaperPortfolioJudgmentDraft sourceDraft = new(
                PaperPortfolioJudgmentAgentSchemas.Draft,
                _sourceDispatch.PortfolioRef,
                _sourceDispatch.CandidateBatchRef,
                _sourceDispatch.CycleNumber,
                scorecards.Values.Select(value => value.ScorecardId).ToArray(),
                [
                    new PaperPortfolioJudgmentPaperDraft(
                        1,
                        PromotedPaperId,
                        scorecards[PromotedPaperId].ScorecardId,
                        "promote",
                        "Paper A has the strongest admitted theorem depth, closure, novelty, and significance across the exact comparison batch.",
                        "Its main risk is the need to expose the obstruction and gluing interfaces as reusable Lean definitions before the central equivalence.",
                        "Rank one follows the strictly higher calibrated score and the portfolio capacity admits this exact paper to frontier planning."),
                    new PaperPortfolioJudgmentPaperDraft(
                        2,
                        HeldPaperId,
                        scorecards[HeldPaperId].ScorecardId,
                        "hold",
                        "Paper B remains publication-eligible and supplies an independently coherent theorem package under the same audit discipline.",
                        "Its theorem-level contribution is weaker under the calibrated comparison and formalization capacity is exhausted for this cycle.",
                        "Rank two preserves the admitted score order and holds the eligible overflow paper for a later portfolio cycle.")
                ],
                [
                    new PaperPortfolioPairwiseRelationDraft(
                        PromotedPaperId,
                        HeldPaperId,
                        "distinct",
                        string.Empty,
                        [
                            coordinates[PromotedPaperId].CandidatePaperRef,
                            coordinates[HeldPaperId].CandidatePaperRef
                        ],
                        "The two theorem packages have independently identifiable proof spines and neither central theorem logically subsumes the other under the exact evidence.",
                        "Their novelty increments address distinct paper identities and can be maintained separately without duplicating one publishable theorem contribution.")
                ],
                "The comparison preserves calibrated score order, allocates the single frontier slot to the strongest eligible theorem package, and records the exact pairwise theorem boundary without changing any A3 evidence.",
                "2026-08-31T03:20:00Z");
            PaperPortfolioJudgmentComputation sourceComputation =
                PaperPortfolioJudgmentAgentService.Compute(
                    _sourceDispatch,
                    _sourceDispatchRef,
                    sourceContext,
                    sourceDraft,
                    SourceResultRef,
                    "2026-08-31T04:00:00Z");
            SourceDecision = sourceComputation.Decision;

            PaperPortfolioJudgmentStoredArtifact evidence = PutPortfolioArtifact(
                "judgment-evidence",
                sourceComputation.Evidence.Schema,
                sourceComputation.Evidence.EvidenceId,
                sourceComputation.Evidence.EvidenceContent,
                sourceComputation.Evidence);
            PaperPortfolioJudgmentStoredArtifact decision = PutPortfolioArtifact(
                "decision",
                sourceComputation.Decision.Schema,
                sourceComputation.Decision.DecisionId,
                sourceComputation.Decision.DecisionContent,
                sourceComputation.Decision);
            PaperPortfolioJudgmentStoredArtifact updatedPortfolio = PutPortfolioArtifact(
                "updated-portfolio",
                sourceComputation.UpdatedPortfolio.Schema,
                sourceComputation.UpdatedPortfolio.PortfolioId,
                sourceComputation.UpdatedPortfolio.PortfolioContent,
                sourceComputation.UpdatedPortfolio);
            _sourceCursor = new PaperPortfolioJudgmentAgentAdmissionCursor(
                PaperPortfolioJudgmentAgentSchemas.AdmissionCursor,
                PortfolioTaskRef,
                SourceResultRef,
                _sourceDispatchRef,
                _sourceDispatch.PortfolioRef,
                _sourceDispatch.CandidateBatchRef,
                _sourceDispatch.CycleNumber,
                evidence,
                decision,
                updatedPortfolio,
                sourceComputation.Routes,
                "codex-portfolio-run-001",
                "produced",
                "2026-08-31T04:00:00Z");
            PaperPortfolioJudgmentAgentService.Validate(_sourceCursor);
            string cursorPath = Path.Combine(
                Root,
                "work",
                "paper-portfolio-judgments",
                "cursors",
                Hex(PortfolioTaskRef) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(cursorPath)!);
            File.WriteAllBytes(cursorPath, CanonicalJson.Serialize(_sourceCursor));
        }

        public string Root { get; }
        public string PortfolioTaskRef { get; }
        public string SourceResultRef { get; }
        public string PromotedPaperId { get; }
        public string HeldPaperId { get; }
        public string PromotedCandidatePath { get; }
        public PaperPortfolioDecision SourceDecision { get; }

        public PaperFrontierPlanningAgentTaskStaged StagePromotedPaper() =>
            PaperFrontierPlanningAgentService.StageTask(
                Root,
                PortfolioTaskRef,
                PromotedPaperId);

        public FrontierPlanningRun RunPromotedPaper()
        {
            PaperFrontierPlanningAgentTaskStaged staged = StagePromotedPaper();
            PaperAgentTaskRegistration registration =
                PaperAgentRuntimeService.RegisterTask(Root, staged.TaskPath);
            Assert.Equal(staged.TaskRef, registration.TaskRef);
            PaperAgentTask task =
                PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
                    File.ReadAllBytes(staged.TaskPath));
            PaperAgentRunPrepared prepared =
                PaperAgentRuntimeService.PrepareRun(Root, staged.TaskRef);
            File.WriteAllBytes(
                Path.Combine(
                    prepared.WorkspacePath,
                    "outputs",
                    "formalization-frontier-draft.json"),
                CanonicalJson.Serialize(CreateDraft(staged.DispatchRef)));
            var result = new PaperAgentResultWire(
                PaperAgentSchemas.AgentResult,
                staged.TaskRef,
                task.PaperId,
                task.TheoryProgramRef,
                task.Phase,
                task.AgentRole,
                task.ContextMode,
                "completed",
                "The promoted theorem package was decomposed into an exact dependency-aware formalization frontier.",
                [
                    new PaperAgentOutputWire(
                        PaperFrontierPlanningAgentSchemas.Draft,
                        "outputs/formalization-frontier-draft.json")
                ],
                "formalization-frontier",
                string.Empty,
                task.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
                "2026-08-31T05:00:00Z");
            WriteEnvelope(prepared.StdoutPath, result);
            PaperAgentResultRecorded recorded = PaperAgentRuntimeService.RecordResult(
                Root,
                staged.TaskRef,
                prepared.StdoutPath,
                "codex-frontier-planner-run-001",
                "produced");
            return new(staged, recorded);
        }

        public PaperFrontierPlanningAgentDispatch ReadDispatch(string dispatchRef)
        {
            string path = Path.Combine(
                Root,
                "artifacts",
                "paper-frontier-planning",
                "dispatches",
                "raw",
                "sha256",
                Hex(dispatchRef)[..2],
                Hex(dispatchRef) + ".json");
            return PaperResearchInputJson.DeserializeStrict<PaperFrontierPlanningAgentDispatch>(
                File.ReadAllBytes(path));
        }

        public PaperFrontierPlanningContext ContextForPromotedPaper()
        {
            string paperId = PromotedPaperId;
            PaperTheoryProgram program = _theory.Programs.Single(
                value => value.ProgramContent.PaperId == paperId);
            PaperPortfolioJudgmentPaperRoute route = _sourceCursor.Routes.Single(
                value => value.PaperId == paperId);
            Assert.Equal("promote-to-frontier", route.Action);
            PaperPortfolioJudgmentEvidence evidence = ReadPortfolioEnvelope<PaperPortfolioJudgmentEvidence>(
                _sourceCursor.Evidence.EnvelopePath);
            PaperResearchPortfolio updatedPortfolio = ReadPortfolioEnvelope<PaperResearchPortfolio>(
                _sourceCursor.UpdatedPortfolio.EnvelopePath);
            return new PaperFrontierPlanningContext(
                _sourceCursor,
                _sourceDispatch,
                _coordinates[paperId],
                program,
                _theory.Scopes[paperId],
                _theory.Inventories[paperId],
                _theory.Packages[paperId],
                _audits[paperId],
                _scorecards[paperId],
                evidence,
                SourceDecision,
                updatedPortfolio);
        }

        public PaperFormalizationFrontierDraft CreateDraft(string dispatchRef) =>
            new(
                PaperFrontierPlanningAgentSchemas.Draft,
                dispatchRef,
                PromotedPaperId,
                _coordinates[PromotedPaperId].TheoryProgramRef,
                _coordinates[PromotedPaperId].TheoremPackageRef,
                _coordinates[PromotedPaperId].TheoryAuditRef,
                _coordinates[PromotedPaperId].ScorecardRef,
                SourceDecision.DecisionId,
                Specs(),
                "The plan preserves the complete admitted theorem DAG, formalizes the canonical object first, then the exact reduction interface, and only afterwards releases the main equivalence, sharp realization, and classification corollary according to their repository-computed dependency waves.",
                [
                    "The exact Base package and reusable obstruction APIs must be verified before any implementation claims an existing module.",
                    "The main equivalence may expose hidden typeclass or gluing prerequisites that must become explicit frontier dependencies rather than unauthorized assumptions."
                ],
                "2026-08-31T04:30:00Z");

        public T ReadEnvelope<T>(string relativePath) =>
            PaperResearchInputJson.DeserializeStrict<T>(
                File.ReadAllBytes(Path.Combine(
                    Root,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))));

        private T ReadPortfolioEnvelope<T>(string relativePath) =>
            ReadEnvelope<T>(relativePath);

        private Evidence PutContent<T>(
            string schema,
            string fileName,
            string expectedRef,
            T content)
        {
            byte[] bytes = CanonicalJson.Serialize(content);
            Assert.Equal(expectedRef, PaperResearchInputStore.Reference(bytes));
            return PutBytes(schema, fileName, expectedRef, bytes);
        }

        private Evidence PutSeed(
            string schema,
            string fileName,
            string seed,
            string expectedRef)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(seed);
            Assert.Equal(expectedRef, PaperResearchInputStore.Reference(bytes));
            return PutBytes(schema, fileName, expectedRef, bytes);
        }

        private Evidence PutBytes(
            string schema,
            string fileName,
            string expectedRef,
            byte[] bytes)
        {
            string relative = "artifacts/source/" + fileName;
            string path = Path.Combine(
                Root,
                relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return new(schema, expectedRef, relative);
        }

        private PaperPortfolioJudgmentStoredArtifact PutPortfolioArtifact<TContent, TEnvelope>(
            string name,
            string schema,
            string artifactRef,
            TContent content,
            TEnvelope envelope)
        {
            byte[] contentBytes = CanonicalJson.Serialize(content);
            Assert.Equal(artifactRef, PaperResearchInputStore.Reference(contentBytes));
            string contentRelative = $"artifacts/source/{name}-content.json";
            string contentPath = Path.Combine(
                Root,
                contentRelative.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(contentPath, contentBytes);

            byte[] envelopeBytes = CanonicalJson.Serialize(envelope);
            string envelopeRef = PaperResearchInputStore.Reference(envelopeBytes);
            string envelopeRelative = $"artifacts/source/{name}-envelope.json";
            string envelopePath = Path.Combine(
                Root,
                envelopeRelative.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(envelopePath, envelopeBytes);
            return new(
                schema,
                artifactRef,
                contentRelative,
                envelopeRef,
                envelopeRelative);
        }

        private void WriteCanonicalPortfolioDispatch(
            string dispatchRef,
            byte[] bytes)
        {
            string hex = Hex(dispatchRef);
            string path = Path.Combine(
                Root,
                "artifacts",
                "paper-portfolio-judgments",
                "dispatches",
                "raw",
                "sha256",
                hex[..2],
                hex + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        private static void WriteEnvelope(
            string stdoutPath,
            PaperAgentResultWire result)
        {
            string text = PaperAgentRuntimeService.ResultBegin
                + "\n"
                + Encoding.UTF8.GetString(CanonicalJson.Serialize(result))
                + "\n"
                + PaperAgentRuntimeService.ResultEnd
                + "\n";
            File.WriteAllText(stdoutPath, text, Encoding.UTF8);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static PaperFormalizationFrontierNodeSpec[] Specs() =>
    [
        new(
            "def:object",
            "definition",
            100,
            "Trureturing.Base",
            "Trureturing.Base.DescentObject",
            "Define the canonical descent datum and obstruction class for every admissible object with coordinate-invariance stated explicitly.",
            "Lean accepts the definition and proves invariance under every authorized coordinate change using only named dependencies."),
        new(
            "lem:reduction",
            "prerequisite",
            95,
            "Trureturing.Base",
            "Trureturing.Base.DescentReduction",
            "Prove that compatible local descent data glue globally if and only if the canonical obstruction class vanishes.",
            "Lean proves both directions with all gluing assumptions exposed and imports only the certified canonical descent definition."),
        new(
            "thm:main",
            "main-theorem",
            90,
            "Trureturing.Base",
            "Trureturing.Base.StructuralDescent",
            "Prove that the target observable descends if and only if the canonical obstruction class vanishes.",
            "Lean proves the equivalence from the certified descent object and exact reduction lemma without adding unauthorized assumptions."),
        new(
            "thm:sharp",
            "sharpness",
            85,
            "Trureturing.Base",
            "Trureturing.Base.SharpObstruction",
            "Construct an admissible object realizing every minimal non-zero obstruction class and prove global descent fails.",
            "Lean verifies admissibility, minimality, non-vanishing, realization, and the failure conclusion from certified dependencies."),
        new(
            "cor:classification",
            "corollary",
            80,
            "Trureturing.Base",
            "Trureturing.Base.FailureClassification",
            "Classify minimal failures of descent by the corresponding minimal non-zero obstruction classes.",
            "Lean derives both directions of the classification from the certified main equivalence and sharp realization theorem.")
    ];

    private static string Digest(string seed) =>
        PaperTheoryTestFactory.Digest(seed);

    private static string Hex(string reference) =>
        reference["sha256:".Length..];
}
