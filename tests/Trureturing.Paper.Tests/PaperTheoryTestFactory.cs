using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

internal sealed record PaperTheoryFixture(
    PaperCandidateBatch Batch,
    IReadOnlyList<PaperTheoryProgram> Programs,
    IReadOnlyDictionary<string, PaperTheoryScope> Scopes,
    IReadOnlyDictionary<string, PaperTheoryInventory> Inventories,
    IReadOnlyDictionary<string, PaperTheoremPackage> Packages,
    PaperResearchPortfolio Portfolio);

internal static class PaperTheoryTestFactory
{
    public static PaperTheoryFixture CreatePortfolio(params string[] paperIds)
    {
        if (paperIds.Length < 2)
        {
            throw new ArgumentException("At least two paper IDs are required.", nameof(paperIds));
        }
        PaperCandidateBatch batch = PaperPortfolioService.CreateBatch(
            new PaperCandidateBatchContent(
                Digest("fixture-truth"),
                Digest("fixture-topology"),
                Digest("fixture-research-input"),
                new PaperPortfolioPolicy(
                    Math.Max(5, paperIds.Length),
                    Math.Min(4, paperIds.Length),
                    1,
                    1),
                paperIds.Select((paperId, index) => new PaperCandidateSeed(
                    paperId,
                    Digest($"candidate-{paperId}"),
                    Digest($"literature-{paperId}"),
                    Digest($"intuition-{paperId}"),
                    90 - index,
                    "2026-08-31T00:00:00Z")).ToArray()));
        PaperTheoryProgram[] programs = paperIds
            .Select(paperId => PaperPortfolioService.CreateTheoryProgram(
                batch,
                paperId,
                "2026-08-31T00:10:00Z"))
            .ToArray();
        var scopes = new Dictionary<string, PaperTheoryScope>(StringComparer.Ordinal);
        var inventories = new Dictionary<string, PaperTheoryInventory>(StringComparer.Ordinal);
        var packages = new Dictionary<string, PaperTheoremPackage>(StringComparer.Ordinal);
        foreach (PaperTheoryProgram program in programs)
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
                    "An equivalence theorem, a sharp realization theorem, and a classification corollary.",
                    [
                        "Define the canonical descent datum.",
                        "Prove descent is equivalent to obstruction vanishing.",
                        "Realize and classify minimal sharp failures."
                    ],
                    ["Known gluing and cocycle tools with citations."],
                    ["Applications that do not close the central theorem chain."],
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
            PaperTheoryDeepeningRequest deepeningRequest =
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
                    deepeningRequest,
                    new PaperTheoryIterationContent(
                        program.TheoryProgramId,
                        scope.ScopeId,
                        inventory.InventoryId,
                        deepeningRequest.RequestId,
                        [],
                        paperId,
                        1,
                        ["lem:reduction", "thm:sharp", "cor:classification"],
                        ["thm:sharp", "cor:classification"],
                        ["lem:reduction"],
                        [],
                        [
                            "Construct canonical local descent data and the obstruction class.",
                            "Strengthen reduction into an exact gluing criterion and prove the main equivalence.",
                            "Realize every minimal non-zero class and derive the failure classification."
                        ],
                        "The package establishes an exact obstruction equivalence, realizes minimal failures, and derives a classification corollary beyond the one-directional inventory theorem.",
                        "Classical descent and cocycle lemmas remain cited tools; the exact equivalence and minimal failure realization are the paper's new results.",
                        ["A minimal non-zero obstruction produces a sharp failure witness."],
                        ["thm:sharp", "cor:classification"],
                        [],
                        new PaperTheoryProgressEvidence(2, 1, 2, 2, 1, true, true),
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
                                [],
                                "strengthened"),
                            Claim(
                                "lem:reduction",
                                "Exact reduction lemma",
                                "lemma",
                                "Local descent data glue globally exactly when the canonical obstruction vanishes.",
                                ["def:object"],
                                "strengthened"),
                            Claim(
                                "thm:main",
                                "Structural descent equivalence",
                                "theorem",
                                "The target observable descends if and only if the canonical obstruction vanishes.",
                                ["def:object", "lem:reduction"],
                                "new"),
                            Claim(
                                "thm:sharp",
                                "Sharp realization theorem",
                                "theorem",
                                "Every minimal non-zero obstruction is realized by an admissible object for which descent fails.",
                                ["def:object", "lem:reduction"],
                                "new"),
                            Claim(
                                "cor:classification",
                                "Failure classification",
                                "corollary",
                                "Minimal failures are classified by minimal non-zero obstruction classes.",
                                ["thm:main", "thm:sharp"],
                                "new")
                        ],
                        ["thm:main"],
                        ["cor:classification"],
                        ["thm:sharp"],
                        [],
                        [
                            "Classical local-to-global descent lemma with a precise citation.",
                            "Standard cocycle classification theorem used as a known tool."
                        ],
                        "The new contribution is the exact descent-obstruction equivalence together with realization and classification of every minimal sharp failure.",
                        "The package provides a canonical abstraction, a main equivalence, a sharp converse, and a reusable classification corollary at publication scale.",
                        "2026-08-31T01:20:00Z"));
            scopes.Add(paperId, scope);
            inventories.Add(paperId, inventory);
            packages.Add(paperId, package);
        }
        PaperResearchPortfolio portfolio = PaperPortfolioService.CreatePortfolio(
            batch,
            programs,
            "2026-08-31T01:30:00Z");
        PaperCandidateState[] auditStates = portfolio.PortfolioContent.CandidateStates
            .Select(state => state with
            {
                Phase = "audit-pending",
                CompletedCycles = 3,
                LastProgressAt = "2026-08-31T01:20:00Z",
                StatusReason = "audit candidate theorem package ready"
            })
            .ToArray();
        PaperResearchPortfolioContent portfolioContent = portfolio.PortfolioContent with
        {
            CandidateStates = auditStates,
            UpdatedAt = "2026-08-31T01:30:00Z"
        };
        portfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(portfolioContent)),
            portfolioContent);
        return new PaperTheoryFixture(
            batch,
            programs,
            scopes,
            inventories,
            packages,
            portfolio);
    }

    public static PaperTheoryAuditRequest CreateAuditRequest(
        PaperTheoryFixture fixture,
        string paperId,
        string? authorRunSeed = null)
    {
        PaperTheoryProgram program = fixture.Programs.Single(
            value => value.ProgramContent.PaperId == paperId);
        return PaperTheoryAuditService.CreateAuditRequest(
            program,
            fixture.Scopes[paperId],
            fixture.Inventories[paperId],
            fixture.Packages[paperId],
            Digest(authorRunSeed ?? $"author-{paperId}"),
            "2026-08-31T02:00:00Z");
    }

    public static PaperTheoryAuditOpinion Opinion(
        PaperTheoryAuditRequest request,
        string runSeed,
        string sessionSeed,
        string role,
        PaperTheoryAuditMetrics metrics,
        string verdict = "pass",
        IReadOnlyList<string>? blockers = null,
        IReadOnlyList<string>? revisions = null) =>
        new(
            Digest(runSeed),
            Digest(sessionSeed),
            role,
            PaperTheoryAuditService.FreshContextMode,
            request.RequestContent.Contract.ExactInputRefs,
            metrics,
            verdict,
            blockers ?? [],
            revisions ?? [],
            "The main equivalence and sharp realization are absent from the cited tools, while the dependency on classical descent is explicitly separated and attributed.",
            [
                "Reconstruct the necessity direction from functoriality of the obstruction and verify every hypothesis used by the main theorem.",
                "Reconstruct sufficiency through the explicit gluing map and verify that the sharp realization closes the converse boundary."
            ],
            ["No equivalent theorem package is present among the authorized sibling evidence; the cited tools stop before the exact equivalence."],
            "2026-08-31T02:10:00Z");

    public static PaperTheoryAudit CreateAudit(
        PaperTheoryFixture fixture,
        string paperId,
        PaperTheoryAuditMetrics metrics,
        string verdict = "pass",
        IReadOnlyList<string>? blockers = null)
    {
        PaperTheoryProgram program = fixture.Programs.Single(
            value => value.ProgramContent.PaperId == paperId);
        PaperTheoryAuditRequest request = CreateAuditRequest(fixture, paperId);
        PaperTheoryAuditOpinion[] opinions =
        [
            Opinion(
                request,
                $"math-run-{paperId}",
                $"math-session-{paperId}",
                "mathematical-referee",
                metrics,
                verdict,
                blockers),
            Opinion(
                request,
                $"novelty-run-{paperId}",
                $"novelty-session-{paperId}",
                "novelty-referee",
                metrics,
                verdict,
                blockers)
        ];
        return PaperTheoryAuditService.CreateAudit(
            program,
            fixture.Scopes[paperId],
            fixture.Inventories[paperId],
            fixture.Packages[paperId],
            request,
            opinions,
            "2026-08-31T02:20:00Z");
    }

    public static PaperTheoryAuditMetrics Metrics(
        int abstraction = 8,
        int depth = 8,
        int closure = 8,
        int proof = 8,
        int novelty = 8,
        int significance = 8,
        int formalization = 8,
        int journal = 8,
        int overlap = 8) =>
        new(
            abstraction,
            depth,
            closure,
            proof,
            novelty,
            significance,
            formalization,
            journal,
            overlap);

    public static string Digest(string seed) =>
        CanonicalJson.Sha256Reference(
            System.Text.Encoding.UTF8.GetBytes(seed));

    private static PaperTheoremPackageClaim Claim(
        string id,
        string title,
        string kind,
        string statement,
        IReadOnlyList<string> dependencies,
        string novelty) =>
        new(
            id,
            title,
            kind,
            statement,
            dependencies,
            "informal-complete",
            [
                $"Establish the defining construction for {title}.",
                $"Verify the universal property and dependency interfaces used by {title}."
            ],
            novelty,
            true);
}
