using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperFormalizationFrontierTests
{
    [Fact]
    public void PromotedTheoremPackageBecomesDependencyWaves()
    {
        FrontierFixture fixture = CreateFrontierFixture(promotionCapacity: 2);
        PaperFormalizationFrontier frontier = fixture.Frontiers["paper-a"];

        Assert.Equal(5, frontier.FrontierContent.Nodes.Count);
        Assert.Equal(4, frontier.FrontierContent.CriticalPathDepth);
        Assert.Equal(2, frontier.FrontierContent.MaximumWaveWidth);
        Assert.Equal(
            0,
            frontier.FrontierContent.Nodes.Single(node => node.ClaimId == "def:object").ParallelWave);
        Assert.Equal(
            2,
            frontier.FrontierContent.Nodes.Single(node => node.ClaimId == "thm:main").ParallelWave);
        Assert.Equal(
            3,
            frontier.FrontierContent.Nodes.Single(node => node.ClaimId == "cor:classification").ParallelWave);
    }

    [Fact]
    public void FrontierIdentityIsIndependentOfSpecificationInputOrder()
    {
        PaperTheoryFixture theory = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PromotionArtifacts promotion = Promote(theory, 2);
        PaperTheoryProgram program = theory.Programs.Single(
            value => value.ProgramContent.PaperId == "paper-a");
        PaperFormalizationFrontierNodeSpec[] specs = Specs();

        PaperFormalizationFrontier first =
            PaperFormalizationFrontierService.CreateFrontier(
                program,
                theory.Packages["paper-a"],
                promotion.Audits["paper-a"],
                promotion.Scorecards["paper-a"],
                promotion.Decision,
                specs,
                "2026-08-31T05:00:00Z");
        PaperFormalizationFrontier replay =
            PaperFormalizationFrontierService.CreateFrontier(
                program,
                theory.Packages["paper-a"],
                promotion.Audits["paper-a"],
                promotion.Scorecards["paper-a"],
                promotion.Decision,
                specs.Reverse().ToArray(),
                "2026-08-31T05:00:00Z");

        Assert.Equal(first.FrontierId, replay.FrontierId);
    }

    [Fact]
    public void HeldPaperCannotCreateFormalizationFrontier()
    {
        PaperTheoryFixture theory = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PromotionArtifacts promotion = Promote(theory, 1);
        string heldPaper = promotion.Decision.DecisionContent.Decisions.Single(
            item => item.Action == "hold").PaperId;
        PaperTheoryProgram program = theory.Programs.Single(
            value => value.ProgramContent.PaperId == heldPaper);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperFormalizationFrontierService.CreateFrontier(
                program,
                theory.Packages[heldPaper],
                promotion.Audits[heldPaper],
                promotion.Scorecards[heldPaper],
                promotion.Decision,
                Specs(),
                "2026-08-31T05:00:00Z"));

        Assert.Contains("did not promote", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalRequestWaitsForCertifiedDependencies()
    {
        FrontierFixture fixture = CreateFrontierFixture(promotionCapacity: 2);
        PaperFormalizationFrontier frontier = fixture.Frontiers["paper-a"];
        PaperFormalizationFrontierState state =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                frontier,
                "2026-08-31T05:10:00Z");
        PaperFormalizationFrontierNode main = frontier.FrontierContent.Nodes.Single(
            node => node.ClaimId == "thm:main");
        PaperFormalizationFrontierEvent selection = Event(
            frontier,
            state,
            main,
            PaperFormalizationFrontierLifecycleService.GovernedSelectionFamily,
            "paper-research-selection.v1",
            "selection-main",
            "",
            "",
            "governed selection for main theorem",
            "2026-08-31T05:20:00Z");
        state = PaperFormalizationFrontierLifecycleService.ApplyEvent(
            frontier,
            state,
            selection,
            "2026-08-31T05:21:00Z");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => Event(
                frontier,
                state,
                main,
                PaperFormalizationFrontierLifecycleService.CanonicalRequestFamily,
                "formalization-request.v1",
                "request-main",
                "",
                "",
                "canonical request for main theorem",
                "2026-08-31T05:30:00Z"));

        Assert.Contains("dependency node is certified", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontierNodeTraversesExistingFormalizationAndCertificationChain()
    {
        FrontierFixture fixture = CreateFrontierFixture(promotionCapacity: 2);
        PaperFormalizationFrontier frontier = fixture.Frontiers["paper-a"];
        PaperFormalizationFrontierState state =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                frontier,
                "2026-08-31T05:10:00Z");
        PaperFormalizationFrontierNode node = frontier.FrontierContent.Nodes.Single(
            value => value.ClaimId == "def:object");

        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.GovernedSelectionFamily,
            "paper-research-selection.v1",
            "selection",
            "",
            "",
            "governed claim selection",
            "2026-08-31T05:20:00Z");
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.CanonicalRequestFamily,
            "formalization-request.v1",
            "canonical-request",
            "",
            "",
            "canonical Formalize request",
            "2026-08-31T05:30:00Z");
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.FormalizeTransportFamily,
            "paper-formalize-task.v1",
            "transport",
            "",
            "",
            "transported to Formalize",
            "2026-08-31T05:40:00Z");
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.FormalizationOutcomeFamily,
            "paper-formalization-outcome.v1",
            "outcome",
            "candidate-produced",
            "",
            "Lean candidate produced",
            "2026-08-31T05:50:00Z");
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.TruthReleaseCertificationFamily,
            "paper-truth-release-certification.v1",
            "certification",
            "",
            frontier.FrontierContent.TruthReleaseDigest,
            "candidate joined to exact truth release",
            "2026-08-31T06:00:00Z");
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.CertifiedClaimManifestFamily,
            "paper-certified-claim-manifest.v1",
            "manifest",
            "",
            "",
            "claim included in certified manifest",
            "2026-08-31T06:10:00Z");

        PaperFormalizationFrontierNodeState final = state.StateContent.NodeStates.Single(
            value => value.NodeId == node.NodeId);
        Assert.Equal("manifested", final.Status);
        Assert.Equal(
            frontier.FrontierContent.TruthReleaseDigest,
            final.CertifiedTruthReleaseDigest);
        Assert.Equal(6, state.StateContent.Version);
    }

    [Fact]
    public void CounterexampleOutcomeRoutesPaperBackToTheoryRevision()
    {
        FrontierFixture fixture = CreateFrontierFixture(promotionCapacity: 2);
        PaperFormalizationFrontier frontier = fixture.Frontiers["paper-a"];
        PaperFormalizationFrontierState state =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                frontier,
                "2026-08-31T05:10:00Z");
        PaperFormalizationFrontierNode node = frontier.FrontierContent.Nodes.Single(
            value => value.ClaimId == "def:object");
        state = AdvanceToTransport(frontier, state, node);

        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.FormalizationOutcomeFamily,
            "paper-formalization-outcome.v1",
            "counterexample-outcome",
            "counterexample",
            "",
            "formalization found a counterexample to the proposed definition",
            "2026-08-31T05:50:00Z");

        Assert.Equal(
            "theory-revision-required",
            state.StateContent.NodeStates.Single(value => value.NodeId == node.NodeId).Status);
    }

    [Fact]
    public void CertificationRejectsDifferentTruthRelease()
    {
        FrontierFixture fixture = CreateFrontierFixture(promotionCapacity: 2);
        PaperFormalizationFrontier frontier = fixture.Frontiers["paper-a"];
        PaperFormalizationFrontierState state =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                frontier,
                "2026-08-31T05:10:00Z");
        PaperFormalizationFrontierNode node = frontier.FrontierContent.Nodes.Single(
            value => value.ClaimId == "def:object");
        state = AdvanceToTransport(frontier, state, node);
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.FormalizationOutcomeFamily,
            "paper-formalization-outcome.v1",
            "candidate-outcome",
            "candidate-produced",
            "",
            "candidate produced",
            "2026-08-31T05:50:00Z");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => Event(
                frontier,
                state,
                node,
                PaperFormalizationFrontierLifecycleService.TruthReleaseCertificationFamily,
                "paper-truth-release-certification.v1",
                "wrong-release-certification",
                "",
                PaperTheoryTestFactory.Digest("different-truth-release"),
                "attempted cross-release certification",
                "2026-08-31T06:00:00Z"));

        Assert.Contains("exact truth release", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IndependentNodesCanRecordSelectionsInOneParallelBatch()
    {
        FrontierFixture fixture = CreateFrontierFixture(promotionCapacity: 2);
        PaperFormalizationFrontier frontier = fixture.Frontiers["paper-a"];
        PaperFormalizationFrontierState state =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                frontier,
                "2026-08-31T05:10:00Z");
        PaperFormalizationFrontierNode definition = frontier.FrontierContent.Nodes.Single(
            value => value.ClaimId == "def:object");
        PaperFormalizationFrontierNode lemma = frontier.FrontierContent.Nodes.Single(
            value => value.ClaimId == "lem:reduction");
        PaperFormalizationFrontierEvent first = Event(
            frontier,
            state,
            definition,
            PaperFormalizationFrontierLifecycleService.GovernedSelectionFamily,
            "paper-research-selection.v1",
            "selection-definition",
            "",
            "",
            "select definition",
            "2026-08-31T05:20:00Z");
        PaperFormalizationFrontierEvent second = Event(
            frontier,
            state,
            lemma,
            PaperFormalizationFrontierLifecycleService.GovernedSelectionFamily,
            "paper-research-selection.v1",
            "selection-lemma",
            "",
            "",
            "select lemma",
            "2026-08-31T05:20:00Z");

        state = PaperFormalizationFrontierLifecycleService.ApplyIndependentEvents(
            frontier,
            state,
            [first, second],
            "2026-08-31T05:21:00Z");

        Assert.Equal(2, state.StateContent.Version);
        Assert.Equal(
            2,
            state.StateContent.NodeStates.Count(value => value.Status == "selection-recorded"));
    }

    [Fact]
    public void PortfolioCanPromoteSeveralPapersToIndependentFrontiers()
    {
        FrontierFixture fixture = CreateFrontierFixture(promotionCapacity: 2);

        Assert.Equal(2, fixture.Frontiers.Count);
        Assert.Equal(
            2,
            fixture.Frontiers.Values
                .Select(frontier => frontier.FrontierContent.PaperId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            2,
            fixture.Frontiers.Values
                .Select(frontier => frontier.FrontierId)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static PaperFormalizationFrontierState AdvanceToTransport(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        PaperFormalizationFrontierNode node)
    {
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.GovernedSelectionFamily,
            "paper-research-selection.v1",
            "selection",
            "",
            "",
            "governed selection",
            "2026-08-31T05:20:00Z");
        state = Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.CanonicalRequestFamily,
            "formalization-request.v1",
            "request",
            "",
            "",
            "canonical request",
            "2026-08-31T05:30:00Z");
        return Apply(
            frontier,
            state,
            node,
            PaperFormalizationFrontierLifecycleService.FormalizeTransportFamily,
            "paper-formalize-task.v1",
            "transport",
            "",
            "",
            "formalize transport",
            "2026-08-31T05:40:00Z");
    }

    private static PaperFormalizationFrontierState Apply(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        PaperFormalizationFrontierNode node,
        string family,
        string schema,
        string artifactSeed,
        string disposition,
        string release,
        string detail,
        string timestamp)
    {
        PaperFormalizationFrontierEvent frontierEvent = Event(
            frontier,
            state,
            node,
            family,
            schema,
            artifactSeed,
            disposition,
            release,
            detail,
            timestamp);
        return PaperFormalizationFrontierLifecycleService.ApplyEvent(
            frontier,
            state,
            frontierEvent,
            timestamp);
    }

    private static PaperFormalizationFrontierEvent Event(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState state,
        PaperFormalizationFrontierNode node,
        string family,
        string schema,
        string artifactSeed,
        string disposition,
        string release,
        string detail,
        string timestamp) =>
        PaperFormalizationFrontierLifecycleService.CreateEvent(
            frontier,
            state,
            node.NodeId,
            family,
            schema,
            PaperTheoryTestFactory.Digest(artifactSeed),
            disposition,
            release,
            detail,
            timestamp);

    private static FrontierFixture CreateFrontierFixture(int promotionCapacity)
    {
        PaperTheoryFixture theory = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PromotionArtifacts promotion = Promote(theory, promotionCapacity);
        var frontiers = new Dictionary<string, PaperFormalizationFrontier>(
            StringComparer.Ordinal);
        foreach (PaperPortfolioPaperDecision decision in promotion.Decision.DecisionContent.Decisions
            .Where(item => item.Action == "promote-to-frontier"))
        {
            PaperTheoryProgram program = theory.Programs.Single(
                value => value.ProgramContent.PaperId == decision.PaperId);
            frontiers.Add(
                decision.PaperId,
                PaperFormalizationFrontierService.CreateFrontier(
                    program,
                    theory.Packages[decision.PaperId],
                    promotion.Audits[decision.PaperId],
                    promotion.Scorecards[decision.PaperId],
                    promotion.Decision,
                    Specs(),
                    "2026-08-31T05:00:00Z"));
        }
        return new FrontierFixture(theory, promotion, frontiers);
    }

    private static PromotionArtifacts Promote(
        PaperTheoryFixture theory,
        int promotionCapacity)
    {
        var audits = new Dictionary<string, PaperTheoryAudit>(StringComparer.Ordinal);
        var scorecards = new Dictionary<string, PaperCandidateScorecard>(StringComparer.Ordinal);
        foreach (string paperId in theory.Packages.Keys)
        {
            PaperTheoryAudit audit = PaperTheoryTestFactory.CreateAudit(
                theory,
                paperId,
                PaperTheoryTestFactory.Metrics());
            PaperCandidateScorecard scorecard =
                PaperPortfolioDecisionService.CreateScorecard(
                    theory.Packages[paperId],
                    audit,
                    "2026-08-31T03:00:00Z");
            audits.Add(paperId, audit);
            scorecards.Add(paperId, scorecard);
        }
        PaperPortfolioDecision decision =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                theory.Portfolio,
                scorecards.Values.ToArray(),
                new PaperPortfolioDecisionPolicy(promotionCapacity, 2),
                "2026-08-31T04:00:00Z");
        return new PromotionArtifacts(audits, scorecards, decision);
    }

    private static PaperFormalizationFrontierNodeSpec[] Specs() =>
    [
        new(
            "def:object",
            "definition",
            100,
            "Trureturing.Base",
            "Trureturing.Base.DescentObject",
            "Define the canonical descent datum and obstruction class for every admissible object.",
            "Lean accepts the definition and proves invariance under the authorized coordinate changes."),
        new(
            "lem:reduction",
            "prerequisite",
            95,
            "Trureturing.Base",
            "Trureturing.Base.DescentReduction",
            "Prove that compatible local descent data glue exactly when the obstruction class vanishes.",
            "Lean proves both directions using only certified dependencies and named hypotheses."),
        new(
            "thm:main",
            "main-theorem",
            90,
            "Trureturing.Base",
            "Trureturing.Base.StructuralDescent",
            "Prove that the target observable descends if and only if the canonical obstruction vanishes.",
            "Lean proves the equivalence and the theorem imports only certified frontier dependencies."),
        new(
            "thm:sharp",
            "sharpness",
            85,
            "Trureturing.Base",
            "Trureturing.Base.SharpObstruction",
            "Construct an admissible object realizing every minimal non-zero obstruction and prove descent fails.",
            "Lean checks the construction, minimality, non-vanishing, and failure conclusion."),
        new(
            "cor:classification",
            "corollary",
            80,
            "Trureturing.Base",
            "Trureturing.Base.FailureClassification",
            "Classify minimal failures of descent by minimal non-zero obstruction classes.",
            "Lean derives the classification from the certified main and sharpness theorems.")
    ];

    private sealed record PromotionArtifacts(
        IReadOnlyDictionary<string, PaperTheoryAudit> Audits,
        IReadOnlyDictionary<string, PaperCandidateScorecard> Scorecards,
        PaperPortfolioDecision Decision);

    private sealed record FrontierFixture(
        PaperTheoryFixture Theory,
        PromotionArtifacts Promotion,
        IReadOnlyDictionary<string, PaperFormalizationFrontier> Frontiers);
}
