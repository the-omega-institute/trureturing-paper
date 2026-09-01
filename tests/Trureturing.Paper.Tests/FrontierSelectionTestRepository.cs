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

internal sealed class FrontierSelectionTestRepository : IDisposable
{
    private const string SelectedPaperId = "paper-a";

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
                        "paper-b",
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
                "paper-b",
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

        PaperTheoryAudit auditA = PaperTheoryTestFactory.CreateAudit(
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
                overlap: 10));
        PaperTheoryAudit auditB = PaperTheoryTestFactory.CreateAudit(
            fixture,
            "paper-b",
            PaperTheoryTestFactory.Metrics());
        PaperCandidateScorecard scorecardA =
            PaperPortfolioDecisionService.CreateScorecard(
                packages[SelectedPaperId],
                auditA,
                "2026-08-31T03:00:00Z");
        PaperCandidateScorecard scorecardB =
            PaperPortfolioDecisionService.CreateScorecard(
                packages["paper-b"],
                auditB,
                "2026-08-31T03:00:00Z");
        PaperPortfolioDecision decision =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                portfolio,
                [scorecardA, scorecardB],
                new PaperPortfolioDecisionPolicy(1, 2),
                "2026-08-31T04:00:00Z");
        Assert.Equal(
            "promote-to-frontier",
            decision.DecisionContent.Decisions.Single(value =>
                value.PaperId == SelectedPaperId).Action);

        PaperTheoryProgram programA = programs.Single(value =>
            value.ProgramContent.PaperId == SelectedPaperId);
        Frontier = PaperFormalizationFrontierService.CreateFrontier(
            programA,
            packages[SelectedPaperId],
            auditA,
            scorecardA,
            decision,
            Specs(),
            "2026-08-31T05:00:00Z");
        PaperFormalizationFrontierState initialState =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                Frontier,
                "2026-08-31T05:10:00Z");

        FrontierSelectionEvidence programEvidence = PutContent(
            PaperPortfolioSchemas.TheoryProgram,
            "program.json",
            programA.TheoryProgramId,
            programA.ProgramContent);
        FrontierSelectionEvidence packageEvidence = PutContent(
            PaperTheoryDeepeningSchemas.TheoremPackage,
            "package.json",
            packages[SelectedPaperId].TheoremPackageId,
            packages[SelectedPaperId].TheoremPackageContent);
        var exactInputs = new List<PaperAgentInputArtifact>
        {
            programEvidence.ToInput(),
            packageEvidence.ToInput()
        };
        for (int index = 0; index < 11; index++)
        {
            FrontierSelectionEvidence dummy = PutBytes(
                $"paper-frontier-selection-source-{index:D2}.v1",
                $"source-{index:D2}.json",
                Encoding.UTF8.GetBytes($"frontier-source-{index:D2}"));
            exactInputs.Add(dummy.ToInput());
            if (index == 0)
            {
                TamperableInputPath = dummy.FullPath;
            }
        }

        PlanningTaskRef = Digest("frontier-planning-task");
        string planningResultRef = Digest("frontier-planning-result");
        string portfolioTaskRef = Digest("portfolio-task");
        string portfolioResultRef = Digest("portfolio-result");
        var planningDispatch = new PaperFrontierPlanningAgentDispatch(
            PaperFrontierPlanningAgentSchemas.Dispatch,
            portfolioTaskRef,
            portfolioResultRef,
            Digest("portfolio-cursor"),
            Digest("portfolio-dispatch"),
            portfolio.PortfolioId,
            batch.BatchId,
            1,
            Digest("judgment-evidence"),
            decision.DecisionId,
            Digest("updated-portfolio"),
            SelectedPaperId,
            programA.TheoryProgramId,
            scopes[SelectedPaperId].ScopeId,
            inventories[SelectedPaperId].InventoryId,
            packages[SelectedPaperId].TheoremPackageId,
            auditA.AuditId,
            scorecardA.ScorecardId,
            programA.ProgramContent.CandidatePaperRef,
            programA.ProgramContent.LiteratureResearchRef,
            exactInputs,
            "2026-08-31T05:20:00Z");
        PaperFrontierPlanningAgentService.Validate(planningDispatch);
        byte[] dispatchBytes = CanonicalJson.Serialize(planningDispatch);
        string dispatchRef = PaperResearchInputStore.Reference(dispatchBytes);
        WritePlanningDispatch(dispatchRef, dispatchBytes);

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
        var cursor = new PaperFrontierPlanningAgentAdmissionCursor(
            PaperFrontierPlanningAgentSchemas.AdmissionCursor,
            PlanningTaskRef,
            planningResultRef,
            dispatchRef,
            portfolioTaskRef,
            portfolioResultRef,
            portfolio.PortfolioId,
            1,
            planningDispatch.JudgmentEvidenceRef,
            planningDispatch.UpdatedPortfolioRef,
            SelectedPaperId,
            programA.TheoryProgramId,
            packages[SelectedPaperId].TheoremPackageId,
            auditA.AuditId,
            scorecardA.ScorecardId,
            decision.DecisionId,
            storedFrontier,
            storedInitialState,
            routes,
            "codex-frontier-planning-test-run",
            "produced",
            "2026-08-31T05:30:00Z");
        PaperFrontierPlanningAgentService.Validate(cursor);
        string cursorPath = Path.Combine(
            Root,
            "work",
            "paper-frontier-planning",
            "cursors",
            Hex(PlanningTaskRef) + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(cursorPath)!);
        File.WriteAllBytes(cursorPath, CanonicalJson.Serialize(cursor));
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

    private PaperFrontierPlanningStoredArtifact PutPlanningEnvelope<TContent, TEnvelope>(
        string name,
        string schema,
        string artifactRef,
        TContent content,
        TEnvelope envelope)
    {
        byte[] contentBytes = CanonicalJson.Serialize(content);
        Assert.Equal(artifactRef, PaperResearchInputStore.Reference(contentBytes));
        string contentRelative = $"artifacts/source/{name}-content.json";
        File.WriteAllBytes(
            Path.Combine(Root, contentRelative.Replace('/', Path.DirectorySeparatorChar)),
            contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(envelope);
        string envelopeRef = PaperResearchInputStore.Reference(envelopeBytes);
        string envelopeRelative = $"artifacts/source/{name}-envelope.json";
        File.WriteAllBytes(
            Path.Combine(Root, envelopeRelative.Replace('/', Path.DirectorySeparatorChar)),
            envelopeBytes);
        return new(
            schema,
            artifactRef,
            contentRelative,
            envelopeRef,
            envelopeRelative);
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
