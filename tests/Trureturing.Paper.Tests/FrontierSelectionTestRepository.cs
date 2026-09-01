using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

internal sealed record FrontierSelectionTheoryArtifacts(
    PaperTheoryScope Scope,
    PaperTheoryInventory Inventory,
    PaperTheoremPackage Package);

internal sealed record FrontierSelectionEvidence(
    string Schema,
    string ArtifactRef,
    string RepositoryRelativePath,
    string FullPath)
{
    public PaperAgentInputArtifact ToInput() =>
        new(Schema, ArtifactRef, RepositoryRelativePath);
}

internal sealed record FrontierSelectionPortfolioArtifact(
    PaperPortfolioJudgmentStoredArtifact Stored,
    FrontierSelectionEvidence Content);

internal sealed class FrontierSelectionTestRepository : IDisposable
{
    private const string SelectedPaperId = "paper-a";
    private const string PeerPaperId = "paper-b";

    public FrontierSelectionTestRepository()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "trureturing-paper-frontier-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "artifacts", "source"));
        Directory.CreateDirectory(Path.Combine(Root, "artifacts", "research-input"));

        TruthReleaseDigest = Digest("frontier-selection-truth");
        string topologyDigest = Digest("frontier-selection-topology");
        var researchInput = new PaperResearchInput(
            PaperResearchInputSchemas.ResearchInput,
            TruthReleaseDigest,
            topologyDigest,
            new string('a', 40),
            new string('b', 40),
            Digest("topology-receipt"),
            Digest("intuition-receipt"),
            Digest("intuition-release"));
        PaperResearchInputValidation.Validate(researchInput);
        var researchStore = new PaperResearchInputStore(
            Path.Combine(Root, "artifacts", "research-input"));
        ResearchInputRef = researchStore.Put(researchInput);

        PaperCandidateBatch batch = PaperPortfolioService.CreateBatch(
            new PaperCandidateBatchContent(
                TruthReleaseDigest,
                topologyDigest,
                ResearchInputRef,
                new PaperPortfolioPolicy(5, 2, 1, 1),
                [
                    new PaperCandidateSeed(
                        SelectedPaperId,
                        Digest("candidate-paper-a"),
                        Digest("literature-paper-a"),
                        Digest("intuition-paper-a"),
                        100,
                        "2026-08-31T00:00:00Z"),
                    new PaperCandidateSeed(
                        PeerPaperId,
                        Digest("candidate-paper-b"),
                        Digest("literature-paper-b"),
                        Digest("intuition-paper-b"),
                        90,
                        "2026-08-31T00:00:00Z")
                ]));
        PaperTheoryProgram[] programs =
        [
            PaperPortfolioService.CreateTheoryProgram(
                batch,
                SelectedPaperId,
                "2026-08-31T00:10:00Z"),
            PaperPortfolioService.CreateTheoryProgram(
                batch,
                PeerPaperId,
                "2026-08-31T00:10:00Z")
        ];
        var scopes = new Dictionary<string, PaperTheoryScope>(StringComparer.Ordinal);
        var inventories = new Dictionary<string, PaperTheoryInventory>(StringComparer.Ordinal);
        var packages = new Dictionary<string, PaperTheoremPackage>(StringComparer.Ordinal);
        foreach (PaperTheoryProgram program in programs)
        {
            FrontierSelectionTheoryArtifacts theory = BuildTheory(program);
            scopes.Add(program.ProgramContent.PaperId, theory.Scope);
            inventories.Add(program.ProgramContent.PaperId, theory.Inventory);
            packages.Add(program.ProgramContent.PaperId, theory.Package);
        }

        PaperResearchPortfolio portfolio = PaperPortfolioService.CreatePortfolio(
            batch,
            programs,
            "2026-08-31T02:00:00Z");
        PaperResearchPortfolioContent portfolioContent =
            portfolio.PortfolioContent with
            {
                CandidateStates = portfolio.PortfolioContent.CandidateStates
                    .Select(state => state with
                    {
                        Phase = "audit-pending",
                        CompletedCycles = 3,
                        LastProgressAt = "2026-08-31T01:50:00Z",
                        StatusReason = "audit candidate theorem package ready"
                    })
                    .ToArray(),
                UpdatedAt = "2026-08-31T02:00:00Z"
            };
        portfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(portfolioContent)),
            portfolioContent);
        var fixture = new PaperTheoryFixture(
            batch,
            programs,
            scopes,
            inventories,
            packages,
            portfolio);

        var audits = new Dictionary<string, PaperTheoryAudit>(StringComparer.Ordinal)
        {
            [SelectedPaperId] = PaperTheoryTestFactory.CreateAudit(
                fixture,
                SelectedPaperId,
                PaperTheoryTestFactory.Metrics(
                    abstraction: 10,
                    depth: 10,
                    closure: 10,
                    proof: 10,
                    novelty: 10,
                    significance: 10,
                    formalization: 10,
                    journal: 10,
                    overlap: 10)),
            [PeerPaperId] = PaperTheoryTestFactory.CreateAudit(
                fixture,
                PeerPaperId,
                PaperTheoryTestFactory.Metrics())
        };
        var scorecards = audits.ToDictionary(
            item => item.Key,
            item => PaperPortfolioDecisionService.CreateScorecard(
                packages[item.Key],
                item.Value,
                "2026-08-31T03:00:00Z"),
            StringComparer.Ordinal);
        var policy = new PaperPortfolioDecisionPolicy(1, 2);
        PaperPortfolioDecision decision =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                portfolio,
                scorecards.Values.ToArray(),
                policy,
                "2026-08-31T04:00:00Z");
        Assert.Equal(
            "promote-to-frontier",
            decision.DecisionContent.Decisions.Single(value =>
                value.PaperId == SelectedPaperId).Action);

        string portfolioAdmittedAt = "2026-08-31T04:10:00Z";
        var decisionsByPaper = decision.DecisionContent.Decisions.ToDictionary(
            item => item.PaperId,
            StringComparer.Ordinal);
        PaperCandidateState[] updatedStates = portfolio.PortfolioContent.CandidateStates
            .Select(state => PaperPortfolioDecisionService.ApplyDecision(
                state,
                decisionsByPaper[state.PaperId],
                portfolioAdmittedAt))
            .OrderBy(state => state.PaperId, StringComparer.Ordinal)
            .ToArray();
        PaperResearchPortfolioContent updatedPortfolioContent =
            portfolio.PortfolioContent with
            {
                NextCycleNumber = portfolio.PortfolioContent.NextCycleNumber + 1,
                CandidateStates = updatedStates,
                UpdatedAt = portfolioAdmittedAt
            };
        var updatedPortfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(updatedPortfolioContent)),
            updatedPortfolioContent);
        PaperPortfolioService.Validate(updatedPortfolio);

        FrontierSelectionEvidence batchEvidence = PutContent(
            PaperPortfolioSchemas.CandidateBatch,
            "candidate-batch.json",
            batch.BatchId,
            batch.BatchContent);
        FrontierSelectionEvidence portfolioEvidence = PutContent(
            PaperPortfolioSchemas.Portfolio,
            "portfolio.json",
            portfolio.PortfolioId,
            portfolio.PortfolioContent);
        var programEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        var scopeEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        var inventoryEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        var packageEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        var auditEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        var scorecardEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        var candidateEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        var literatureEvidence = new Dictionary<string, FrontierSelectionEvidence>(StringComparer.Ordinal);
        foreach (PaperTheoryProgram program in programs)
        {
            string paperId = program.ProgramContent.PaperId;
            programEvidence.Add(
                paperId,
                PutContent(
                    PaperPortfolioSchemas.TheoryProgram,
                    $"{paperId}-program.json",
                    program.TheoryProgramId,
                    program.ProgramContent));
            scopeEvidence.Add(
                paperId,
                PutContent(
                    PaperTheoryFoundationSchemas.Scope,
                    $"{paperId}-scope.json",
                    scopes[paperId].ScopeId,
                    scopes[paperId].ScopeContent));
            inventoryEvidence.Add(
                paperId,
                PutContent(
                    PaperTheoryFoundationSchemas.Inventory,
                    $"{paperId}-inventory.json",
                    inventories[paperId].InventoryId,
                    inventories[paperId].InventoryContent));
            packageEvidence.Add(
                paperId,
                PutContent(
                    PaperTheoryDeepeningSchemas.TheoremPackage,
                    $"{paperId}-package.json",
                    packages[paperId].TheoremPackageId,
                    packages[paperId].TheoremPackageContent));
            auditEvidence.Add(
                paperId,
                PutContent(
                    PaperTheoryAuditSchemas.Audit,
                    $"{paperId}-audit.json",
                    audits[paperId].AuditId,
                    audits[paperId].AuditContent));
            scorecardEvidence.Add(
                paperId,
                PutContent(
                    PaperPortfolioDecisionSchemas.Scorecard,
                    $"{paperId}-scorecard.json",
                    scorecards[paperId].ScorecardId,
                    scorecards[paperId].ScorecardContent));
            candidateEvidence.Add(
                paperId,
                PutExpectedBytes(
                    CandidateArtifactSchemas.CandidatePaper,
                    $"{paperId}-candidate.json",
                    program.ProgramContent.CandidatePaperRef,
                    Encoding.UTF8.GetBytes($"candidate-{paperId}")));
            literatureEvidence.Add(
                paperId,
                PutExpectedBytes(
                    CandidateArtifactSchemas.LiteratureResearch,
                    $"{paperId}-literature.json",
                    program.ProgramContent.LiteratureResearchRef,
                    Encoding.UTF8.GetBytes($"literature-{paperId}")));
        }
        TamperableInputPath = candidateEvidence[SelectedPaperId].FullPath;

        PaperPortfolioJudgmentPaperInput[] coordinates = programs
            .Select(program =>
            {
                string paperId = program.ProgramContent.PaperId;
                return new PaperPortfolioJudgmentPaperInput(
                    paperId,
                    program.TheoryProgramId,
                    scopes[paperId].ScopeId,
                    inventories[paperId].InventoryId,
                    packages[paperId].TheoremPackageId,
                    audits[paperId].AuditId,
                    scorecards[paperId].ScorecardId,
                    program.ProgramContent.CandidatePaperRef,
                    program.ProgramContent.LiteratureResearchRef);
            })
            .OrderBy(value => value.PaperId, StringComparer.Ordinal)
            .ToArray();
        var portfolioInputs = new List<PaperAgentInputArtifact>
        {
            portfolioEvidence.ToInput(),
            batchEvidence.ToInput()
        };
        foreach (string paperId in programs
            .Select(value => value.ProgramContent.PaperId)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            portfolioInputs.Add(programEvidence[paperId].ToInput());
            portfolioInputs.Add(scopeEvidence[paperId].ToInput());
            portfolioInputs.Add(inventoryEvidence[paperId].ToInput());
            portfolioInputs.Add(packageEvidence[paperId].ToInput());
            portfolioInputs.Add(auditEvidence[paperId].ToInput());
            portfolioInputs.Add(scorecardEvidence[paperId].ToInput());
            portfolioInputs.Add(candidateEvidence[paperId].ToInput());
            portfolioInputs.Add(literatureEvidence[paperId].ToInput());
        }
        Assert.Equal(18, portfolioInputs.Count);
        var portfolioDispatch = new PaperPortfolioJudgmentAgentDispatch(
            PaperPortfolioJudgmentAgentSchemas.Dispatch,
            portfolio.PortfolioId,
            batch.BatchId,
            portfolio.PortfolioContent.NextCycleNumber,
            policy,
            coordinates,
            portfolioInputs,
            "2026-08-31T03:30:00Z");
        PaperPortfolioJudgmentAgentService.Validate(portfolioDispatch);
        FrontierSelectionEvidence portfolioDispatchEvidence = PutBytes(
            PaperPortfolioJudgmentAgentSchemas.Dispatch,
            "portfolio-dispatch.json",
            CanonicalJson.Serialize(portfolioDispatch));

        string portfolioTaskRef = Digest("portfolio-task");
        string portfolioResultRef = Digest("portfolio-result");
        var pairwise = new PaperPortfolioPairwiseRelationDraft(
            SelectedPaperId,
            PeerPaperId,
            "distinct",
            string.Empty,
            [
                programs.Single(value => value.ProgramContent.PaperId == SelectedPaperId)
                    .ProgramContent.CandidatePaperRef,
                programs.Single(value => value.ProgramContent.PaperId == PeerPaperId)
                    .ProgramContent.CandidatePaperRef
            ],
            "The theorem packages have independently identifiable load-bearing proof chains and neither admitted package logically subsumes the other.",
            "The two candidate papers claim distinct theorem-level novelty increments under the admitted literature and audit evidence boundaries.");
        var judgmentEvidenceContent = new PaperPortfolioJudgmentEvidenceContent(
            portfolioDispatchEvidence.ArtifactRef,
            portfolioResultRef,
            portfolio.PortfolioId,
            batch.BatchId,
            portfolio.PortfolioContent.NextCycleNumber,
            scorecards.Values
                .Select(value => value.ScorecardId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            decision.DecisionContent.Decisions
                .OrderBy(value => value.Rank)
                .Select(value => value.PaperId)
                .ToArray(),
            [pairwise],
            "The admitted portfolio judgment preserves the calibrated score order, promotes only the strongest eligible theorem package, records the complete pairwise theorem boundary, and keeps overflow work outside the formalization frontier.",
            decision.DecisionId,
            portfolioAdmittedAt);
        var judgmentEvidence = new PaperPortfolioJudgmentEvidence(
            PaperPortfolioJudgmentAgentSchemas.Evidence,
            CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(judgmentEvidenceContent)),
            judgmentEvidenceContent);
        PaperPortfolioJudgmentAgentService.Validate(judgmentEvidence);

        FrontierSelectionPortfolioArtifact storedJudgment = PutPortfolioArtifact(
            "judgment-evidence",
            judgmentEvidence.Schema,
            judgmentEvidence.EvidenceId,
            judgmentEvidence.EvidenceContent,
            judgmentEvidence);
        FrontierSelectionPortfolioArtifact storedDecision = PutPortfolioArtifact(
            "portfolio-decision",
            decision.Schema,
            decision.DecisionId,
            decision.DecisionContent,
            decision);
        FrontierSelectionPortfolioArtifact storedUpdatedPortfolio = PutPortfolioArtifact(
            "updated-portfolio",
            updatedPortfolio.Schema,
            updatedPortfolio.PortfolioId,
            updatedPortfolio.PortfolioContent,
            updatedPortfolio);

        PaperPortfolioJudgmentPaperRoute[] portfolioRoutes = decision.DecisionContent.Decisions
            .OrderBy(value => value.Rank)
            .Select(value => new PaperPortfolioJudgmentPaperRoute(
                value.Rank,
                value.PaperId,
                value.TheoryProgramRef,
                value.ScorecardRef,
                value.Action,
                PortfolioRoute(value.Action),
                value.Reason))
            .ToArray();
        var portfolioCursor = new PaperPortfolioJudgmentAgentAdmissionCursor(
            PaperPortfolioJudgmentAgentSchemas.AdmissionCursor,
            portfolioTaskRef,
            portfolioResultRef,
            portfolioDispatchEvidence.ArtifactRef,
            portfolio.PortfolioId,
            batch.BatchId,
            portfolio.PortfolioContent.NextCycleNumber,
            storedJudgment.Stored,
            storedDecision.Stored,
            storedUpdatedPortfolio.Stored,
            portfolioRoutes,
            "codex-portfolio-test-run",
            "produced",
            portfolioAdmittedAt);
        PaperPortfolioJudgmentAgentService.Validate(portfolioCursor);
        FrontierSelectionEvidence portfolioCursorEvidence = PutBytes(
            PaperPortfolioJudgmentAgentSchemas.AdmissionCursor,
            "portfolio-cursor.json",
            CanonicalJson.Serialize(portfolioCursor));

        PaperTheoryProgram selectedProgram = programs.Single(value =>
            value.ProgramContent.PaperId == SelectedPaperId);
        var planningInputs = new PaperAgentInputArtifact[]
        {
            portfolioCursorEvidence.ToInput(),
            portfolioDispatchEvidence.ToInput(),
            programEvidence[SelectedPaperId].ToInput(),
            scopeEvidence[SelectedPaperId].ToInput(),
            inventoryEvidence[SelectedPaperId].ToInput(),
            packageEvidence[SelectedPaperId].ToInput(),
            auditEvidence[SelectedPaperId].ToInput(),
            scorecardEvidence[SelectedPaperId].ToInput(),
            candidateEvidence[SelectedPaperId].ToInput(),
            literatureEvidence[SelectedPaperId].ToInput(),
            storedJudgment.Content.ToInput(),
            storedDecision.Content.ToInput(),
            storedUpdatedPortfolio.Content.ToInput()
        };
        Assert.Equal(13, planningInputs.Length);
        var planningDispatch = new PaperFrontierPlanningAgentDispatch(
            PaperFrontierPlanningAgentSchemas.Dispatch,
            portfolioTaskRef,
            portfolioResultRef,
            portfolioCursorEvidence.ArtifactRef,
            portfolioDispatchEvidence.ArtifactRef,
            portfolio.PortfolioId,
            batch.BatchId,
            portfolio.PortfolioContent.NextCycleNumber,
            judgmentEvidence.EvidenceId,
            decision.DecisionId,
            updatedPortfolio.PortfolioId,
            SelectedPaperId,
            selectedProgram.TheoryProgramId,
            scopes[SelectedPaperId].ScopeId,
            inventories[SelectedPaperId].InventoryId,
            packages[SelectedPaperId].TheoremPackageId,
            audits[SelectedPaperId].AuditId,
            scorecards[SelectedPaperId].ScorecardId,
            selectedProgram.ProgramContent.CandidatePaperRef,
            selectedProgram.ProgramContent.LiteratureResearchRef,
            planningInputs,
            portfolioAdmittedAt);
        PaperFrontierPlanningContext reopened =
            PaperFrontierPlanningAgentService.ReopenContext(
                Root,
                planningDispatch);
        Assert.Equal(SelectedPaperId, reopened.Program.ProgramContent.PaperId);

        Frontier = PaperFormalizationFrontierService.CreateFrontier(
            selectedProgram,
            packages[SelectedPaperId],
            audits[SelectedPaperId],
            scorecards[SelectedPaperId],
            decision,
            Specs(),
            "2026-08-31T05:00:00Z");
        PaperFormalizationFrontierState initialState =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                Frontier,
                "2026-08-31T05:10:00Z");
        byte[] planningDispatchBytes = CanonicalJson.Serialize(planningDispatch);
        string planningDispatchRef = PaperResearchInputStore.Reference(
            planningDispatchBytes);
        WritePlanningDispatch(planningDispatchRef, planningDispatchBytes);

        PaperFrontierPlanningStoredArtifact storedFrontier = PutPlanningEnvelope(
            "frontier",
            Frontier.Schema,
            Frontier.FrontierId,
            Frontier.FrontierContent,
            Frontier);
        PaperFrontierPlanningStoredArtifact storedInitialState = PutPlanningEnvelope(
            "initial-state",
            initialState.Schema,
            initialState.StateId,
            initialState.StateContent,
            initialState);
        PaperFrontierPlanningNodeRoute[] routes = Frontier.FrontierContent.Nodes
            .Where(node => node.ParallelWave == 0)
            .Select((node, index) => new PaperFrontierPlanningNodeRoute(
                index + 1,
                node.NodeId,
                node.ClaimId,
                node.FormalizationKind,
                node.ParallelWave,
                node.Priority,
                "governed-selection"))
            .ToArray();
        Assert.Equal(2, routes.Length);

        PlanningTaskRef = Digest("frontier-planning-task");
        var planningCursor = new PaperFrontierPlanningAgentAdmissionCursor(
            PaperFrontierPlanningAgentSchemas.AdmissionCursor,
            PlanningTaskRef,
            Digest("frontier-planning-result"),
            planningDispatchRef,
            portfolioTaskRef,
            portfolioResultRef,
            portfolio.PortfolioId,
            portfolio.PortfolioContent.NextCycleNumber,
            judgmentEvidence.EvidenceId,
            updatedPortfolio.PortfolioId,
            SelectedPaperId,
            selectedProgram.TheoryProgramId,
            packages[SelectedPaperId].TheoremPackageId,
            audits[SelectedPaperId].AuditId,
            scorecards[SelectedPaperId].ScorecardId,
            decision.DecisionId,
            storedFrontier,
            storedInitialState,
            routes,
            "codex-frontier-planning-test-run",
            "produced",
            "2026-08-31T05:30:00Z");
        PaperFrontierPlanningAgentService.Validate(planningCursor);
        string planningCursorPath = Path.Combine(
            Root,
            "work",
            "paper-frontier-planning",
            "cursors",
            Hex(PlanningTaskRef) + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(planningCursorPath)!);
        File.WriteAllBytes(
            planningCursorPath,
            CanonicalJson.Serialize(planningCursor));
    }

    public string Root { get; }
    public string PlanningTaskRef { get; }
    public string ResearchInputRef { get; }
    public string TruthReleaseDigest { get; }
    public string TamperableInputPath { get; } = string.Empty;
    public PaperFormalizationFrontier Frontier { get; }

    public PaperFormalizationFrontierNode Node(string claimId) =>
        Frontier.FrontierContent.Nodes.Single(value =>
            value.ClaimId == claimId);

    public PaperFrontierCurrentStateCursor ReadCurrentStateCursor()
    {
        string path = Path.Combine(
            Root,
            "work",
            "paper-frontiers",
            "current-state",
            Hex(Frontier.FrontierId) + ".json");
        return PaperResearchInputJson.DeserializeStrict<PaperFrontierCurrentStateCursor>(
            File.ReadAllBytes(path));
    }

    public PaperFormalizationFrontierState ReadState(
        PaperFrontierNodeSelectionStoredArtifact stored) =>
        PaperResearchInputJson.DeserializeStrict<PaperFormalizationFrontierState>(
            File.ReadAllBytes(Path.Combine(
                Root,
                stored.RepositoryRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar))));

    public bool BindingLookupExists(string requestRef) =>
        File.Exists(Path.Combine(
            Root,
            "work",
            "paper-frontier-formalization-bindings",
            "by-request",
            Hex(requestRef) + ".json"));

    private FrontierSelectionTheoryArtifacts BuildTheory(PaperTheoryProgram program)
    {
        string paperId = program.ProgramContent.PaperId;
        PaperTheoryScopeRequest scopeRequest =
            PaperTheoryFoundationService.CreateScopeRequest(
                program,
                "2026-08-31T00:20:00Z");
        PaperTheoryScope scope = PaperTheoryFoundationService.CreateScope(
            program,
            scopeRequest,
            new PaperTheoryScopeContent(
                program.TheoryProgramId,
                scopeRequest.RequestId,
                paperId,
                "Which obstruction exactly characterizes descent of the target observable?",
                "A canonical descent datum and its obstruction class.",
                "An equivalence theorem, an independent sharp realization theorem, and a classification corollary.",
                [
                    "Define the canonical descent datum.",
                    "Prove descent is equivalent to obstruction vanishing.",
                    "Realize and classify minimal sharp failures."
                ],
                ["Known gluing and cocycle tools with citations."],
                ["Applications outside the central theorem chain."],
                "Split only independently coherent theorem packages.",
                ["Construct a minimal non-zero obstruction witness."],
                "2026-08-31T00:30:00Z"));
        PaperTheoryInventoryRequest inventoryRequest =
            PaperTheoryFoundationService.CreateInventoryRequest(
                program,
                scope,
                "2026-08-31T00:40:00Z");
        PaperTheoryInventory inventory =
            PaperTheoryFoundationService.CreateInventory(
                program,
                scope,
                inventoryRequest,
                new PaperTheoryInventoryContent(
                    program.TheoryProgramId,
                    scope.ScopeId,
                    inventoryRequest.RequestId,
                    paperId,
                    [
                        new PaperTheoryClaimInventoryItem(
                            "def:object",
                            "Canonical descent object",
                            "definition",
                            "proposed",
                            "Every admissible object carries canonical local descent data.",
                            [],
                            "Foundation of the theory.",
                            "Stabilize the definition."),
                        new PaperTheoryClaimInventoryItem(
                            "lem:reduction",
                            "Reduction lemma",
                            "lemma",
                            "weak",
                            "Obstruction vanishing reduces descent to compatible local data.",
                            ["def:object"],
                            "Supports the main theorem.",
                            "Strengthen to an exact gluing statement."),
                        new PaperTheoryClaimInventoryItem(
                            "thm:main",
                            "Structural descent theorem",
                            "theorem",
                            "missing",
                            "The observable descends exactly when the obstruction vanishes.",
                            ["def:object", "lem:reduction"],
                            "Central theorem.",
                            "Give a complete proof and sharp converse.")
                    ],
                    ["thm:main"],
                    ["Global compatible gluing interface."],
                    ["Classify all minimal obstruction classes."],
                    ["Prove only the forward direction under finite complexity."],
                    ["Construct a minimal non-zero obstruction witness."],
                    "2026-08-31T00:50:00Z"));
        PaperTheoryDeepeningRequest request =
            PaperTheoryDeepeningService.CreateDeepeningRequest(
                program,
                scope,
                inventory,
                null,
                1,
                "2026-08-31T01:00:00Z");
        PaperTheoryIteration iteration =
            PaperTheoryDeepeningService.CreateIteration(
                program,
                scope,
                inventory,
                request,
                new PaperTheoryIterationContent(
                    program.TheoryProgramId,
                    scope.ScopeId,
                    inventory.InventoryId,
                    request.RequestId,
                    [],
                    paperId,
                    1,
                    ["lem:reduction", "thm:sharp", "cor:classification"],
                    ["thm:sharp", "cor:classification"],
                    ["lem:reduction"],
                    [],
                    [
                        "Construct the canonical descent datum.",
                        "Prove the exact obstruction equivalence.",
                        "Realize independent sharp failures and classify them."
                    ],
                    "The package establishes an exact obstruction equivalence and an independently rooted sharpness construction.",
                    "Classical descent and cocycle lemmas remain cited tools.",
                    ["A minimal non-zero obstruction produces a sharp failure witness."],
                    ["thm:sharp", "cor:classification"],
                    [],
                    new PaperTheoryProgressEvidence(2, 1, 3, 3, 1, true, true),
                    "2026-08-31T01:10:00Z"));
        PaperTheoremPackage package =
            PaperTheoryDeepeningService.CreateTheoremPackage(
                program,
                scope,
                inventory,
                iteration,
                new PaperTheoremPackageContent(
                    program.TheoryProgramId,
                    scope.ScopeId,
                    inventory.InventoryId,
                    iteration.IterationId,
                    paperId,
                    1,
                    "audit-candidate",
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
                            "Independent sharp realization theorem",
                            "theorem",
                            "Every minimal non-zero obstruction is realized by an admissible object for which descent fails.",
                            []),
                        Claim(
                            "cor:classification",
                            "Failure classification",
                            "corollary",
                            "Minimal failures are classified by minimal non-zero obstruction classes.",
                            ["thm:main", "thm:sharp"])
                    ],
                    ["thm:main"],
                    ["cor:classification"],
                    ["thm:sharp"],
                    [],
                    [
                        "Classical local-to-global descent lemma with a precise citation.",
                        "Standard cocycle classification theorem used as a known tool."
                    ],
                    "The new contribution is the exact descent-obstruction equivalence together with independent sharp realization and classification.",
                    "The package supplies a canonical abstraction, a main equivalence, a sharpness theorem, and a reusable classification corollary.",
                    "2026-08-31T01:20:00Z"));
        return new(scope, inventory, package);
    }

    private FrontierSelectionEvidence PutContent<T>(
        string schema,
        string fileName,
        string expectedRef,
        T content)
    {
        byte[] bytes = CanonicalJson.Serialize(content);
        Assert.Equal(expectedRef, PaperResearchInputStore.Reference(bytes));
        return PutBytes(schema, fileName, bytes);
    }

    private FrontierSelectionEvidence PutExpectedBytes(
        string schema,
        string fileName,
        string expectedRef,
        byte[] bytes)
    {
        FrontierSelectionEvidence evidence = PutBytes(schema, fileName, bytes);
        Assert.Equal(expectedRef, evidence.ArtifactRef);
        return evidence;
    }

    private FrontierSelectionEvidence PutBytes(
        string schema,
        string fileName,
        byte[] bytes)
    {
        string reference = PaperResearchInputStore.Reference(bytes);
        string relative = "artifacts/source/" + fileName;
        string full = Path.Combine(
            Root,
            relative.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllBytes(full, bytes);
        return new(schema, reference, relative, full);
    }

    private FrontierSelectionPortfolioArtifact PutPortfolioArtifact<TContent, TEnvelope>(
        string name,
        string schema,
        string artifactRef,
        TContent content,
        TEnvelope envelope)
    {
        FrontierSelectionEvidence storedContent = PutContent(
            schema,
            $"{name}-content.json",
            artifactRef,
            content);
        FrontierSelectionEvidence storedEnvelope = PutBytes(
            schema,
            $"{name}-envelope.json",
            CanonicalJson.Serialize(envelope));
        var stored = new PaperPortfolioJudgmentStoredArtifact(
            schema,
            artifactRef,
            storedContent.RepositoryRelativePath,
            storedEnvelope.ArtifactRef,
            storedEnvelope.RepositoryRelativePath);
        return new(stored, storedContent);
    }

    private PaperFrontierPlanningStoredArtifact PutPlanningEnvelope<TContent, TEnvelope>(
        string name,
        string schema,
        string artifactRef,
        TContent content,
        TEnvelope envelope)
    {
        FrontierSelectionEvidence storedContent = PutContent(
            schema,
            $"{name}-content.json",
            artifactRef,
            content);
        FrontierSelectionEvidence storedEnvelope = PutBytes(
            schema,
            $"{name}-envelope.json",
            CanonicalJson.Serialize(envelope));
        return new(
            schema,
            artifactRef,
            storedContent.RepositoryRelativePath,
            storedEnvelope.ArtifactRef,
            storedEnvelope.RepositoryRelativePath);
    }

    private void WritePlanningDispatch(string dispatchRef, byte[] bytes)
    {
        string hex = Hex(dispatchRef);
        string path = Path.Combine(
            Root,
            "artifacts",
            "paper-frontier-planning",
            "dispatches",
            "raw",
            "sha256",
            hex[..2],
            hex + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static string PortfolioRoute(string action) =>
        action switch
        {
            "promote-to-frontier" => "frontier-planning",
            "hold" => "portfolio-judgment",
            "continue-deepening" => "theory-deepening",
            "split" => "portfolio-split",
            "merge" => "portfolio-merge",
            "park" => "parked",
            "archive" => "archived",
            _ => throw new InvalidDataException(
                $"Unsupported portfolio action {action}.")
        };

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

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
                $"Establish the construction required by {title}.",
                $"Verify every dependency interface used by {title}."
            ],
            "new",
            true);

    private static PaperFormalizationFrontierNodeSpec[] Specs() =>
    [
        new(
            "def:object",
            "definition",
            100,
            "Trureturing.Base",
            "Trureturing.Base.DescentObject",
            "Define the canonical descent datum and obstruction class for every admissible object with coordinate invariance stated explicitly.",
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
