using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class PaperTheoryAuditPortfolioTests
{
    [Fact]
    public void AuditRequestRequiresFreshReviewAndForbidsPriorVerdicts()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperTheoryAuditRequest request =
            PaperTheoryTestFactory.CreateAuditRequest(fixture, "paper-a");

        Assert.Equal(
            PaperTheoryAuditService.FreshContextMode,
            request.RequestContent.ContextMode);
        Assert.Equal(2, request.RequestContent.MinimumIndependentOpinions);
        Assert.Contains(
            request.RequestContent.Contract.ForbiddenShortcuts,
            rule => rule.Contains("previous audit verdicts", StringComparison.Ordinal));
        Assert.Contains(
            fixture.Packages["paper-a"].TheoremPackageId,
            request.RequestContent.Contract.ExactInputRefs);
    }

    [Fact]
    public void TwoIndependentFreshOpinionsCanPassTheoryAudit()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperTheoryAudit audit = PaperTheoryTestFactory.CreateAudit(
            fixture,
            "paper-a",
            PaperTheoryTestFactory.Metrics());

        Assert.True(audit.AuditContent.Passed);
        Assert.Equal("pass", audit.AuditContent.Verdict);
        Assert.Equal(2, audit.AuditContent.Opinions.Count);
        Assert.Empty(audit.AuditContent.BlockerLedger);
        Assert.Equal(8, audit.AuditContent.AggregateMetrics.Novelty);
    }

    [Fact]
    public void ReviewerCannotReuseTheoryAuthorRun()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        const string authorSeed = "shared-author-run";
        PaperTheoryAuditRequest request =
            PaperTheoryTestFactory.CreateAuditRequest(
                fixture,
                "paper-a",
                authorSeed);
        PaperTheoryAuditOpinion first = PaperTheoryTestFactory.Opinion(
            request,
            authorSeed,
            "session-1",
            "mathematical-referee",
            PaperTheoryTestFactory.Metrics());
        PaperTheoryAuditOpinion second = PaperTheoryTestFactory.Opinion(
            request,
            "reviewer-2",
            "session-2",
            "novelty-referee",
            PaperTheoryTestFactory.Metrics());
        PaperTheoryProgram program = fixture.Programs.Single(
            value => value.ProgramContent.PaperId == "paper-a");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryAuditService.CreateAudit(
                program,
                fixture.Scopes["paper-a"],
                fixture.Inventories["paper-a"],
                fixture.Packages["paper-a"],
                request,
                [first, second],
                "2026-08-31T02:20:00Z"));

        Assert.Contains("theory-author", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IndependentOpinionsCannotShareReviewSession()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperTheoryAuditRequest request =
            PaperTheoryTestFactory.CreateAuditRequest(fixture, "paper-a");
        PaperTheoryAuditOpinion first = PaperTheoryTestFactory.Opinion(
            request,
            "reviewer-1",
            "shared-session",
            "mathematical-referee",
            PaperTheoryTestFactory.Metrics());
        PaperTheoryAuditOpinion second = PaperTheoryTestFactory.Opinion(
            request,
            "reviewer-2",
            "shared-session",
            "novelty-referee",
            PaperTheoryTestFactory.Metrics());
        PaperTheoryProgram program = fixture.Programs.Single(
            value => value.ProgramContent.PaperId == "paper-a");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperTheoryAuditService.CreateAudit(
                program,
                fixture.Scopes["paper-a"],
                fixture.Inventories["paper-a"],
                fixture.Packages["paper-a"],
                request,
                [first, second],
                "2026-08-31T02:20:00Z"));

        Assert.Contains("distinct review sessions", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoordinateWiseMinimumPreventsOptimisticAuditAveraging()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperTheoryAuditRequest request =
            PaperTheoryTestFactory.CreateAuditRequest(fixture, "paper-a");
        PaperTheoryAuditOpinion optimistic = PaperTheoryTestFactory.Opinion(
            request,
            "reviewer-1",
            "session-1",
            "mathematical-referee",
            PaperTheoryTestFactory.Metrics(novelty: 10));
        PaperTheoryAuditOpinion skeptical = PaperTheoryTestFactory.Opinion(
            request,
            "reviewer-2",
            "session-2",
            "novelty-referee",
            PaperTheoryTestFactory.Metrics(novelty: 6),
            "deepen",
            ["Novel increment remains too close to the cited obstruction theorem."]);
        PaperTheoryProgram program = fixture.Programs.Single(
            value => value.ProgramContent.PaperId == "paper-a");

        PaperTheoryAudit audit = PaperTheoryAuditService.CreateAudit(
            program,
            fixture.Scopes["paper-a"],
            fixture.Inventories["paper-a"],
            fixture.Packages["paper-a"],
            request,
            [optimistic, skeptical],
            "2026-08-31T02:20:00Z");

        Assert.False(audit.AuditContent.Passed);
        Assert.Equal("deepen", audit.AuditContent.Verdict);
        Assert.Equal(6, audit.AuditContent.AggregateMetrics.Novelty);
        Assert.Single(audit.AuditContent.BlockerLedger);
    }

    [Fact]
    public void ScorecardUsesCalibratedWeightedComposite()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperTheoryAuditMetrics metrics = PaperTheoryTestFactory.Metrics(
            abstraction: 8,
            depth: 9,
            closure: 10,
            proof: 8,
            novelty: 7,
            significance: 9,
            formalization: 8,
            journal: 7,
            overlap: 10);
        PaperTheoryAudit audit = PaperTheoryTestFactory.CreateAudit(
            fixture,
            "paper-a",
            metrics);

        PaperCandidateScorecard scorecard =
            PaperPortfolioDecisionService.CreateScorecard(
                fixture.Packages["paper-a"],
                audit,
                "2026-08-31T03:00:00Z");

        Assert.True(scorecard.ScorecardContent.PromotionEligible);
        Assert.Equal("promote", scorecard.ScorecardContent.RecommendedAction);
        Assert.Equal(
            PaperPortfolioDecisionService.Composite(metrics),
            scorecard.ScorecardContent.CompositeScore);
    }

    [Fact]
    public void PortfolioPromotesTopPapersAndHoldsPassedOverflow()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b",
            "paper-c");
        PaperCandidateScorecard[] scorecards =
        [
            Scorecard(fixture, "paper-a", PaperTheoryTestFactory.Metrics(
                depth: 10, closure: 10, novelty: 10, significance: 10)),
            Scorecard(fixture, "paper-b", PaperTheoryTestFactory.Metrics(
                depth: 9, closure: 9, novelty: 9, significance: 9)),
            Scorecard(fixture, "paper-c", PaperTheoryTestFactory.Metrics())
        ];

        PaperPortfolioDecision decision =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                fixture.Portfolio,
                scorecards,
                new PaperPortfolioDecisionPolicy(2, 2),
                "2026-08-31T04:00:00Z");

        Assert.Equal(
            ["paper-a", "paper-b", "paper-c"],
            decision.DecisionContent.Decisions.Select(item => item.PaperId).ToArray());
        Assert.Equal(
            ["promote-to-frontier", "promote-to-frontier", "hold"],
            decision.DecisionContent.Decisions.Select(item => item.Action).ToArray());
    }

    [Fact]
    public void PortfolioDecisionIsDeterministicAcrossScorecardInputOrder()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b",
            "paper-c");
        PaperCandidateScorecard[] scorecards =
        [
            Scorecard(fixture, "paper-a", PaperTheoryTestFactory.Metrics(depth: 10)),
            Scorecard(fixture, "paper-b", PaperTheoryTestFactory.Metrics(depth: 9)),
            Scorecard(fixture, "paper-c", PaperTheoryTestFactory.Metrics())
        ];

        PaperPortfolioDecision first =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                fixture.Portfolio,
                scorecards,
                new PaperPortfolioDecisionPolicy(1, 2),
                "2026-08-31T04:00:00Z");
        PaperPortfolioDecision replay =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                fixture.Portfolio,
                scorecards.Reverse().ToArray(),
                new PaperPortfolioDecisionPolicy(1, 2),
                "2026-08-31T04:00:00Z");

        Assert.Equal(first.DecisionId, replay.DecisionId);
        Assert.Equal(
            first.DecisionContent.Decisions.Select(item => item.Action),
            replay.DecisionContent.Decisions.Select(item => item.Action));
    }

    [Fact]
    public void FailedPaperKeepsItsAuditRouteDuringPortfolioCompetition()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperCandidateScorecard passed = Scorecard(
            fixture,
            "paper-a",
            PaperTheoryTestFactory.Metrics(depth: 10));
        PaperTheoryAudit failedAudit = PaperTheoryTestFactory.CreateAudit(
            fixture,
            "paper-b",
            PaperTheoryTestFactory.Metrics(novelty: 6),
            "split",
            ["The sharpness chain is independent of the main theorem scope."]);
        PaperCandidateScorecard failed =
            PaperPortfolioDecisionService.CreateScorecard(
                fixture.Packages["paper-b"],
                failedAudit,
                "2026-08-31T03:00:00Z");

        PaperPortfolioDecision decision =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                fixture.Portfolio,
                [passed, failed],
                new PaperPortfolioDecisionPolicy(1, 2),
                "2026-08-31T04:00:00Z");

        PaperPortfolioPaperDecision paperB = decision.DecisionContent.Decisions.Single(
            item => item.PaperId == "paper-b");
        Assert.Equal("split", paperB.Action);
    }

    [Fact]
    public void PromotionAdvancesOnlyMatchingPaperToFrontierPending()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperCandidateScorecard[] scorecards =
        [
            Scorecard(fixture, "paper-a", PaperTheoryTestFactory.Metrics(depth: 10)),
            Scorecard(fixture, "paper-b", PaperTheoryTestFactory.Metrics())
        ];
        PaperPortfolioDecision portfolioDecision =
            PaperPortfolioDecisionService.CreatePortfolioDecision(
                fixture.Portfolio,
                scorecards,
                new PaperPortfolioDecisionPolicy(1, 2),
                "2026-08-31T04:00:00Z");
        PaperPortfolioPaperDecision paperA = portfolioDecision.DecisionContent.Decisions.Single(
            item => item.PaperId == "paper-a");
        PaperCandidateState state = fixture.Portfolio.PortfolioContent.CandidateStates.Single(
            item => item.PaperId == "paper-a");

        state = PaperPortfolioDecisionService.ApplyDecision(
            state,
            paperA,
            "2026-08-31T04:10:00Z");

        Assert.Equal("frontier-pending", state.Phase);
        Assert.Equal(0, state.ConsecutiveNoProgressCycles);
        Assert.Equal("2026-08-31T04:10:00Z", state.LastProgressAt);
    }

    private static PaperCandidateScorecard Scorecard(
        PaperTheoryFixture fixture,
        string paperId,
        PaperTheoryAuditMetrics metrics)
    {
        PaperTheoryAudit audit = PaperTheoryTestFactory.CreateAudit(
            fixture,
            paperId,
            metrics);
        return PaperPortfolioDecisionService.CreateScorecard(
            fixture.Packages[paperId],
            audit,
            "2026-08-31T03:00:00Z");
    }
}
