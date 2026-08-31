using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryDeepeningTests
{
    [Fact]
    public void DeepeningRequestSpecifiesTheoryWorkAndForbidsPrematureFormalization()
    {
        Foundation f = CreateFoundation("paper-01");
        PaperTheoryDeepeningRequest request =
            PaperTheoryDeepeningService.CreateDeepeningRequest(
                f.Program,
                f.Scope,
                f.Inventory,
                null,
                1,
                "2026-08-31T03:00:00Z");

        Assert.Equal("A2-theory-deepening", request.RequestContent.Phase);
        Assert.Contains(
            request.RequestContent.Contract.ScientificTasks,
            task => task.Contains("Strengthen the central theorem", StringComparison.Ordinal));
        Assert.Contains(
            request.RequestContent.Contract.ForbiddenShortcuts,
            rule => rule.Contains("Do not run Lean", StringComparison.Ordinal));
        Assert.Empty(request.RequestContent.PriorTheoremPackageRefs);
    }

    [Fact]
    public void FakeExtensionWithoutStructuralProgressIsRejected()
    {
        Foundation f = CreateFoundation("paper-01");
        PaperTheoryDeepeningRequest request = Request(f);
        PaperTheoryIterationContent content = IterationContent(f, request) with
        {
            NewClaimIds = [],
            ProgressEvidence = new PaperTheoryProgressEvidence(
                0,
                1,
                0,
                1,
                0,
                false,
                false)
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryDeepeningService.CreateIteration(
                f.Program,
                f.Scope,
                f.Inventory,
                request,
                content));

        Assert.Contains("progress evidence counters", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidIterationProducesAuditCandidateTheoremPackage()
    {
        Foundation f = CreateFoundation("paper-01");
        PaperTheoryDeepeningRequest request = Request(f);
        PaperTheoryIteration iteration =
            PaperTheoryDeepeningService.CreateIteration(
                f.Program,
                f.Scope,
                f.Inventory,
                request,
                IterationContent(f, request));

        PaperTheoremPackage package =
            PaperTheoryDeepeningService.CreateTheoremPackage(
                f.Program,
                f.Scope,
                f.Inventory,
                iteration,
                PackageContent(f, iteration, "audit-candidate"));

        Assert.Equal("audit-candidate", package.TheoremPackageContent.Maturity);
        Assert.Equal(5, package.TheoremPackageContent.Claims.Count);
        Assert.Empty(package.TheoremPackageContent.OpenProofObligations);
        Assert.Equal(new[] { "thm:main" }, package.TheoremPackageContent.MainTheoremClaimIds);
        Assert.Equal(new[] { "cor:classification" }, package.TheoremPackageContent.CorollaryClaimIds);
        Assert.Equal(new[] { "thm:sharp" }, package.TheoremPackageContent.SharpnessClaimIds);
    }

    [Fact]
    public void AuditCandidateRequiresCorollaryAndSharpness()
    {
        Foundation f = CreateFoundation("paper-01");
        PaperTheoryDeepeningRequest request = Request(f);
        PaperTheoryIteration iteration =
            PaperTheoryDeepeningService.CreateIteration(
                f.Program,
                f.Scope,
                f.Inventory,
                request,
                IterationContent(f, request));
        PaperTheoremPackageContent content =
            PackageContent(f, iteration, "audit-candidate") with
            {
                CorollaryClaimIds = [],
                SharpnessClaimIds = []
            };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryDeepeningService.CreateTheoremPackage(
                f.Program,
                f.Scope,
                f.Inventory,
                iteration,
                content));

        Assert.Contains(
            "corollary",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuditCandidateAdvancesOnePaperToAuditPending()
    {
        Foundation f = CreateFoundation("paper-01");
        PaperTheoryDeepeningRequest request = Request(f);
        PaperTheoryIteration iteration =
            PaperTheoryDeepeningService.CreateIteration(
                f.Program,
                f.Scope,
                f.Inventory,
                request,
                IterationContent(f, request));
        PaperTheoremPackage package =
            PaperTheoryDeepeningService.CreateTheoremPackage(
                f.Program,
                f.Scope,
                f.Inventory,
                iteration,
                PackageContent(f, iteration, "audit-candidate"));
        PaperCandidateState state = new(
            PaperPortfolioSchemas.CandidateState,
            f.Program.ProgramContent.PaperId,
            f.Program.TheoryProgramId,
            "theory-deepening",
            80,
            2,
            0,
            "2026-08-31T02:00:00Z",
            "inventory ready");

        state = PaperTheoryDeepeningService.AdvanceAfterDeepening(
            state,
            package,
            "2026-08-31T05:00:00Z");

        Assert.Equal("audit-pending", state.Phase);
        Assert.Equal(3, state.CompletedCycles);
        Assert.Contains(package.TheoremPackageId, state.StatusReason, StringComparison.Ordinal);
    }

    [Fact]
    public void NoProgressIncrementsRotationPenalty()
    {
        Foundation f = CreateFoundation("paper-01");
        PaperCandidateState state = new(
            PaperPortfolioSchemas.CandidateState,
            f.Program.ProgramContent.PaperId,
            f.Program.TheoryProgramId,
            "theory-deepening",
            80,
            2,
            1,
            "2026-08-31T02:00:00Z",
            "inventory ready");

        state = PaperTheoryDeepeningService.RecordNoProgress(
            state,
            "only notation changed",
            "2026-08-31T05:00:00Z");

        Assert.Equal(3, state.CompletedCycles);
        Assert.Equal(2, state.ConsecutiveNoProgressCycles);
        Assert.Contains("only notation changed", state.StatusReason, StringComparison.Ordinal);
    }

    [Fact]
    public void MatureOutOfScopeTheoremChainCanBecomeSplitProposal()
    {
        Foundation f = CreateFoundation("paper-01");
        PaperTheoremPackage package = Package(f);
        var content = new PaperCandidateSplitProposalContent(
            f.Program.TheoryProgramId,
            package.TheoremPackageId,
            "paper-01",
            "paper-01-split",
            ["thm:sharp", "cor:classification"],
            "Which obstruction classes characterize every sharp failure of descent?",
            [
                "Classify minimal non-vanishing obstruction classes.",
                "Construct a canonical representative in every class.",
                "Derive the universal sharpness theorem and classification corollary."
            ],
            "The classification is independent of the source paper's positive descent theorem.",
            "The extracted theorem chain supports a separate structural classification paper.",
            "The split must cite the source definitions and avoid repeating the positive theorem.",
            "2026-08-31T06:00:00Z");

        PaperCandidateSplitProposal proposal =
            PaperTheoryPortfolioProposalService.CreateSplitProposal(
                package,
                content);

        Assert.Equal("paper-01-split", proposal.ProposalContent.ProposedPaperId);
        Assert.Equal(2, proposal.ProposalContent.ExtractedClaimIds.Count);
    }

    [Fact]
    public void OverlappingPaperProgramsCanProduceMergeProposal()
    {
        Foundation sourceFoundation = CreateFoundation("paper-01");
        Foundation targetFoundation = CreateFoundation("paper-02");
        PaperTheoremPackage source = Package(sourceFoundation);
        PaperTheoremPackage target = Package(targetFoundation);
        var content = new PaperCandidateMergeProposalContent(
            sourceFoundation.Program.TheoryProgramId,
            targetFoundation.Program.TheoryProgramId,
            source.TheoremPackageId,
            target.TheoremPackageId,
            "paper-01",
            "paper-02",
            "paper-01",
            [
                new PaperClaimOverlapPair(
                    "thm:main",
                    "thm:main",
                    "shared-core",
                    "Both papers prove descent from the same vanishing obstruction under equivalent canonical objects.")
            ],
            "A single obstruction-valued descent functor subsumes both paper-specific formulations.",
            "The shared main theorem and proof spine should be unified before either manuscript is formalized.",
            "2026-08-31T06:00:00Z");

        PaperCandidateMergeProposal proposal =
            PaperTheoryPortfolioProposalService.CreateMergeProposal(
                source,
                target,
                content);

        Assert.Equal("paper-01", proposal.ProposalContent.CanonicalPaperId);
        Assert.Single(proposal.ProposalContent.OverlapPairs);
    }

    [Fact]
    public void BatchPapersReceiveIndependentDeepeningRequests()
    {
        Foundation[] foundations =
        [
            CreateFoundation("paper-01"),
            CreateFoundation("paper-02"),
            CreateFoundation("paper-03")
        ];

        PaperTheoryDeepeningRequest[] requests = foundations
            .Select(Request)
            .ToArray();

        Assert.Equal(
            3,
            requests.Select(request => request.RequestContent.PaperId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            3,
            requests.Select(request => request.RequestId)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static PaperTheoryDeepeningRequest Request(Foundation f) =>
        PaperTheoryDeepeningService.CreateDeepeningRequest(
            f.Program,
            f.Scope,
            f.Inventory,
            null,
            1,
            "2026-08-31T03:00:00Z");

    private static PaperTheoremPackage Package(Foundation f)
    {
        PaperTheoryDeepeningRequest request = Request(f);
        PaperTheoryIteration iteration =
            PaperTheoryDeepeningService.CreateIteration(
                f.Program,
                f.Scope,
                f.Inventory,
                request,
                IterationContent(f, request));
        return PaperTheoryDeepeningService.CreateTheoremPackage(
            f.Program,
            f.Scope,
            f.Inventory,
            iteration,
            PackageContent(f, iteration, "audit-candidate"));
    }

    private static PaperTheoryIterationContent IterationContent(
        Foundation f,
        PaperTheoryDeepeningRequest request) =>
        new(
            f.Program.TheoryProgramId,
            f.Scope.ScopeId,
            f.Inventory.InventoryId,
            request.RequestId,
            request.RequestContent.PriorTheoremPackageRefs,
            f.Program.ProgramContent.PaperId,
            1,
            ["lem:reduction", "thm:sharp", "cor:classification"],
            ["thm:sharp", "cor:classification"],
            ["lem:reduction"],
            [],
            [
                "Construct the canonical local descent datum and obstruction cocycle.",
                "Use the strengthened reduction lemma to glue local descent data exactly when the obstruction vanishes.",
                "Classify a minimal non-vanishing obstruction and derive sharpness plus the classification corollary."
            ],
            "The new theorem package establishes an exact obstruction criterion, a sharp converse, and a classification corollary beyond the one-directional inventory statement.",
            "Known descent and gluing lemmas are cited as tools; the exact obstruction equivalence and minimal sharpness classification are the manuscript's new results.",
            ["A minimal non-zero obstruction gives a sharp failure witness."],
            ["thm:sharp", "cor:classification"],
            [],
            new PaperTheoryProgressEvidence(
                2,
                1,
                2,
                2,
                1,
                true,
                true),
            "2026-08-31T04:00:00Z");

    private static PaperTheoremPackageContent PackageContent(
        Foundation f,
        PaperTheoryIteration iteration,
        string maturity)
    {
        IReadOnlyList<string> open = maturity == "audit-candidate"
            ? []
            : ["Complete the converse sharpness construction."];
        return new(
            f.Program.TheoryProgramId,
            f.Scope.ScopeId,
            f.Inventory.InventoryId,
            iteration.IterationId,
            f.Program.ProgramContent.PaperId,
            1,
            maturity,
            [
                new PaperTheoremPackageClaim(
                    "def:object",
                    "Canonical descent object",
                    "definition",
                    "Every admissible object carries a canonical local descent datum and obstruction class.",
                    [],
                    "informal-complete",
                    [
                        "Construct the local descent datum functorially.",
                        "Show coordinate changes preserve its obstruction class."
                    ],
                    "strengthened",
                    true),
                new PaperTheoremPackageClaim(
                    "lem:reduction",
                    "Exact reduction to obstruction vanishing",
                    "lemma",
                    "Local descent data glue globally exactly when the canonical obstruction class vanishes.",
                    ["def:object"],
                    "informal-complete",
                    [
                        "Prove necessity by functoriality of the obstruction.",
                        "Prove sufficiency by an explicit compatible gluing construction."
                    ],
                    "strengthened",
                    true),
                new PaperTheoremPackageClaim(
                    "thm:main",
                    "Structural descent equivalence",
                    "theorem",
                    "The target observable descends if and only if the canonical obstruction vanishes.",
                    ["def:object", "lem:reduction"],
                    "informal-complete",
                    [
                        "Apply the reduction lemma to the observable's local representatives.",
                        "Identify global descent with vanishing of the canonical obstruction."
                    ],
                    "new",
                    true),
                new PaperTheoremPackageClaim(
                    "thm:sharp",
                    "Sharp obstruction theorem",
                    "theorem",
                    "Every minimal non-zero obstruction class is realized by an admissible object for which descent fails.",
                    ["def:object", "lem:reduction"],
                    "informal-complete",
                    [
                        "Construct an admissible representative for each minimal class.",
                        "Use non-vanishing to rule out every global descent datum."
                    ],
                    "new",
                    true),
                new PaperTheoremPackageClaim(
                    "cor:classification",
                    "Failure classification",
                    "corollary",
                    "Minimal failures of descent are classified by minimal non-zero obstruction classes.",
                    ["thm:main", "thm:sharp"],
                    "informal-complete",
                    [
                        "Map every failure to its obstruction class.",
                        "Use sharpness and minimality to obtain the classification."
                    ],
                    "new",
                    true)
            ],
            ["thm:main"],
            ["cor:classification"],
            ["thm:sharp"],
            open,
            [
                "Classical local-to-global descent lemma with an explicit citation.",
                "Standard cocycle classification theorem used as a known tool."
            ],
            "The package's novel increment is the exact descent-obstruction equivalence together with realization and classification of every minimal sharp failure.",
            "The theorem chain supplies a canonical abstraction, an equivalence theorem, a sharp converse, and a reusable classification corollary at a publication-level scope.",
            "2026-08-31T04:30:00Z");
    }

    private static Foundation CreateFoundation(string paperId)
    {
        PaperCandidateBatch batch = PaperPortfolioService.CreateBatch(
            new PaperCandidateBatchContent(
                Digest($"truth-{paperId}"),
                Digest($"topology-{paperId}"),
                Digest($"research-{paperId}"),
                new PaperPortfolioPolicy(5, 3, 1, 1),
                [
                    new PaperCandidateSeed(
                        paperId,
                        Digest($"candidate-{paperId}"),
                        Digest($"literature-{paperId}"),
                        Digest($"intuition-{paperId}"),
                        80,
                        "2026-08-31T00:00:00Z"),
                    new PaperCandidateSeed(
                        $"{paperId}-peer",
                        Digest($"candidate-{paperId}-peer"),
                        Digest($"literature-{paperId}-peer"),
                        Digest($"intuition-{paperId}-peer"),
                        70,
                        "2026-08-31T00:00:00Z")
                ]));
        PaperTheoryProgram program = PaperPortfolioService.CreateTheoryProgram(
            batch,
            paperId,
            "2026-08-31T00:00:00Z");
        PaperTheoryScopeRequest scopeRequest =
            PaperTheoryFoundationService.CreateScopeRequest(
                program,
                "2026-08-31T00:30:00Z");
        PaperTheoryScope scope = PaperTheoryFoundationService.CreateScope(
            program,
            scopeRequest,
            new PaperTheoryScopeContent(
                program.TheoryProgramId,
                scopeRequest.RequestId,
                paperId,
                "Which structural mechanism characterizes descent of the target observable?",
                "A canonical descent object and its obstruction class.",
                "A structural equivalence theorem, sharpness theorem, and reusable consequence.",
                [
                    "Define the canonical descent object.",
                    "Prove the exact descent theorem.",
                    "Prove sharpness and classify minimal failures."
                ],
                ["Known descent results used with citations."],
                ["Independent applications outside the central theorem chain."],
                "Split only an independently coherent theorem package.",
                ["Realize a minimal non-zero obstruction as a failure witness."],
                "2026-08-31T01:00:00Z"));
        PaperTheoryInventoryRequest inventoryRequest =
            PaperTheoryFoundationService.CreateInventoryRequest(
                program,
                scope,
                "2026-08-31T01:30:00Z");
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
                            "Strengthen to an equivalence."),
                        new PaperTheoryClaimInventoryItem(
                            "thm:main",
                            "Descent theorem",
                            "theorem",
                            "missing",
                            "The observable descends exactly when the obstruction vanishes.",
                            ["def:object", "lem:reduction"],
                            "Main theorem.",
                            "Give a complete proof and converse.")
                    ],
                    ["thm:main"],
                    ["Global compatible gluing interface."],
                    ["Classify all minimal obstruction classes."],
                    ["Prove only the forward direction under finite complexity."],
                    ["Construct a minimal non-zero obstruction witness."],
                    "2026-08-31T02:00:00Z"));
        return new Foundation(program, scope, inventory);
    }

    private sealed record Foundation(
        PaperTheoryProgram Program,
        PaperTheoryScope Scope,
        PaperTheoryInventory Inventory);

    private static string Digest(string seed) =>
        CanonicalJson.Sha256Reference(
            System.Text.Encoding.UTF8.GetBytes(seed));
}
