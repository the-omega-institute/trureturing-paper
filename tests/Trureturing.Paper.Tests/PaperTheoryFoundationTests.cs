using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryFoundationTests
{
    [Fact]
    public void ScopeRequestFixesCodexTaskAndForbiddenShortcuts()
    {
        PaperTheoryProgram program = Program();
        PaperTheoryScopeRequest request =
            PaperTheoryFoundationService.CreateScopeRequest(
                program,
                "2026-08-31T01:00:00Z");

        Assert.Equal("A0-scope", request.RequestContent.Phase);
        Assert.Contains(
            request.RequestContent.Contract.ScientificTasks,
            task => task.Contains("canonical abstraction", StringComparison.Ordinal));
        Assert.Contains(
            request.RequestContent.Contract.ForbiddenShortcuts,
            rule => rule.Contains("Do not run Lean", StringComparison.Ordinal));
        Assert.Contains(
            PaperTheoryFoundationSchemas.Scope,
            request.RequestContent.Contract.RequiredOutputSchemas);
        Assert.Contains(
            program.ProgramContent.PaperResearchInputRef,
            request.RequestContent.Contract.ExactInputRefs);
    }

    [Fact]
    public void InventoryRepresentsATheoremSeriesWithDependencies()
    {
        PaperTheoryProgram program = Program();
        PaperTheoryScope scope = Scope(program);
        PaperTheoryInventoryRequest request =
            PaperTheoryFoundationService.CreateInventoryRequest(
                program,
                scope,
                "2026-08-31T02:00:00Z");

        PaperTheoryInventory inventory =
            PaperTheoryFoundationService.CreateInventory(
                program,
                scope,
                request,
                InventoryContent(program, scope, request));

        Assert.Equal(3, inventory.InventoryContent.Items.Count);
        Assert.Equal(
            new[] { "def:object", "lem:reduction" },
            inventory.InventoryContent.Items
                .Single(item => item.ClaimId == "thm:main")
                .Dependencies);
        Assert.Equal(
            new[] { "thm:main" },
            inventory.InventoryContent.MainTheoremClaimIds);
    }

    [Fact]
    public void InventoryRejectsSingleIsolatedTheorem()
    {
        PaperTheoryProgram program = Program();
        PaperTheoryScope scope = Scope(program);
        PaperTheoryInventoryRequest request =
            PaperTheoryFoundationService.CreateInventoryRequest(
                program,
                scope,
                "2026-08-31T02:00:00Z");
        PaperTheoryInventoryContent content =
            InventoryContent(program, scope, request) with
            {
                Items =
                [
                    new PaperTheoryClaimInventoryItem(
                        "thm:alone",
                        "Isolated theorem",
                        "theorem",
                        "proposed",
                        "For every admissible object, the desired property holds.",
                        [],
                        "Only claimed result.",
                        "Deepen into a theorem package.")
                ],
                MainTheoremClaimIds = ["thm:alone"]
            };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryFoundationService.CreateInventory(
                program,
                scope,
                request,
                content));

        Assert.Contains(
            "at least three claim items",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryRejectsCyclicTheoremDependencies()
    {
        PaperTheoryProgram program = Program();
        PaperTheoryScope scope = Scope(program);
        PaperTheoryInventoryRequest request =
            PaperTheoryFoundationService.CreateInventoryRequest(
                program,
                scope,
                "2026-08-31T02:00:00Z");
        PaperTheoryInventoryContent original =
            InventoryContent(program, scope, request);
        PaperTheoryClaimInventoryItem[] items = original.Items
            .Select(item => item.ClaimId == "def:object"
                ? item with { Dependencies = ["thm:main"] }
                : item)
            .ToArray();

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryFoundationService.CreateInventory(
                program,
                scope,
                request,
                original with { Items = items }));

        Assert.Contains("acyclic", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleLeasedPapersReceiveIndependentFoundationRequests()
    {
        PaperCandidateBatch batch = Batch(4);
        PaperTheoryProgram[] programs = batch.BatchContent.Candidates
            .Select(candidate => PaperPortfolioService.CreateTheoryProgram(
                batch,
                candidate.PaperId,
                "2026-08-31T00:00:00Z"))
            .ToArray();

        PaperTheoryScopeRequest[] requests = programs
            .Select(program => PaperTheoryFoundationService.CreateScopeRequest(
                program,
                "2026-08-31T01:00:00Z"))
            .ToArray();

        Assert.Equal(4, requests.Length);
        Assert.Equal(
            4,
            requests.Select(request => request.RequestContent.PaperId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            4,
            requests.Select(request => request.RequestId)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void CandidateStateAdvancesScopeThenInventory()
    {
        PaperTheoryProgram program = Program();
        PaperCandidateState state = new(
            PaperPortfolioSchemas.CandidateState,
            program.ProgramContent.PaperId,
            program.TheoryProgramId,
            "scope-pending",
            80,
            0,
            0,
            "2026-08-31T00:00:00Z",
            "registered");
        PaperTheoryScope scope = Scope(program);

        state = PaperTheoryFoundationService.AdvanceAfterScope(
            state,
            scope,
            "2026-08-31T02:00:00Z");
        PaperTheoryInventoryRequest request =
            PaperTheoryFoundationService.CreateInventoryRequest(
                program,
                scope,
                "2026-08-31T03:00:00Z");
        PaperTheoryInventory inventory =
            PaperTheoryFoundationService.CreateInventory(
                program,
                scope,
                request,
                InventoryContent(program, scope, request));
        state = PaperTheoryFoundationService.AdvanceAfterInventory(
            state,
            inventory,
            "2026-08-31T04:00:00Z");

        Assert.Equal("theory-deepening", state.Phase);
        Assert.Equal(2, state.CompletedCycles);
        Assert.Equal(0, state.ConsecutiveNoProgressCycles);
        Assert.Contains(inventory.InventoryId, state.StatusReason, StringComparison.Ordinal);
    }

    private static PaperTheoryProgram Program()
    {
        PaperCandidateBatch batch = Batch(2);
        return PaperPortfolioService.CreateTheoryProgram(
            batch,
            "paper-01",
            "2026-08-31T00:00:00Z");
    }

    private static PaperCandidateBatch Batch(int count)
    {
        PaperCandidateSeed[] candidates = Enumerable.Range(1, count)
            .Select(index => new PaperCandidateSeed(
                $"paper-{index:00}",
                Digest($"candidate-{index}"),
                Digest($"literature-{index}"),
                Digest($"intuition-{index}"),
                90 - index,
                "2026-08-31T00:00:00Z"))
            .ToArray();
        return PaperPortfolioService.CreateBatch(
            new PaperCandidateBatchContent(
                Digest("truth"),
                Digest("topology"),
                Digest("research-input"),
                new PaperPortfolioPolicy(
                    Math.Max(5, count),
                    Math.Min(Math.Max(2, count), 4),
                    1,
                    1),
                candidates));
    }

    private static PaperTheoryScope Scope(PaperTheoryProgram program)
    {
        PaperTheoryScopeRequest request =
            PaperTheoryFoundationService.CreateScopeRequest(
                program,
                "2026-08-31T01:00:00Z");
        var content = new PaperTheoryScopeContent(
            program.TheoryProgramId,
            request.RequestId,
            program.ProgramContent.PaperId,
            "Which structural mechanism forces the target observable to descend?",
            "A canonical descent object and its obstruction class.",
            "A theorem chain with a structural result, sharpness witness, and reusable consequence.",
            [
                "Define the canonical descent object.",
                "Prove the structural descent theorem.",
                "Prove a sharp obstruction or counterexample theorem."
            ],
            ["Known background results used with citations."],
            ["Independent applications that do not close the main theorem chain."],
            "Split only a theorem package that has an independent question and proof spine.",
            ["Construct a witness showing failure when the descent hypothesis is removed."],
            "2026-08-31T01:30:00Z");
        return PaperTheoryFoundationService.CreateScope(
            program,
            request,
            content);
    }

    private static PaperTheoryInventoryContent InventoryContent(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventoryRequest request) =>
        new(
            program.TheoryProgramId,
            scope.ScopeId,
            request.RequestId,
            program.ProgramContent.PaperId,
            [
                new PaperTheoryClaimInventoryItem(
                    "def:object",
                    "Canonical descent object",
                    "definition",
                    "proposed",
                    "An admissible object carries a canonical descent datum.",
                    [],
                    "Fixes the abstraction used by every theorem.",
                    "Stabilize the definition and examples."),
                new PaperTheoryClaimInventoryItem(
                    "lem:reduction",
                    "Reduction to the obstruction class",
                    "lemma",
                    "weak",
                    "Vanishing of the obstruction reduces descent to the canonical local problem.",
                    ["def:object"],
                    "Provides the reduction step for the main theorem.",
                    "Strengthen the hypotheses and close the proof."),
                new PaperTheoryClaimInventoryItem(
                    "thm:main",
                    "Structural descent theorem",
                    "theorem",
                    "missing",
                    "The target observable descends exactly when the canonical obstruction vanishes.",
                    ["def:object", "lem:reduction"],
                    "Central theorem of the paper.",
                    "Develop a complete proof and sharp converse.")
            ],
            ["thm:main"],
            ["Global gluing interface between the reduction lemma and the main theorem."],
            ["Classify all minimal non-vanishing obstruction classes."],
            ["Prove the forward implication under a finite-complexity hypothesis."],
            ["Construct a minimal object with non-zero obstruction."],
            "2026-08-31T02:30:00Z");

    private static string Digest(string seed) =>
        CanonicalJson.Sha256Reference(
            System.Text.Encoding.UTF8.GetBytes(seed));
}
