using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperPortfolioJudgmentAgentTests
{
    [Fact]
    public void StrongestEligiblePapersReceiveBoundedPromotion()
    {
        JudgmentFixture fixture = CreateFixture(
            new Dictionary<string, PaperTheoryAuditMetrics>(StringComparer.Ordinal)
            {
                ["paper-a"] = PaperTheoryTestFactory.Metrics(depth: 10, closure: 10, novelty: 10, significance: 10),
                ["paper-b"] = PaperTheoryTestFactory.Metrics(depth: 9, closure: 9, novelty: 9, significance: 9),
                ["paper-c"] = PaperTheoryTestFactory.Metrics()
            },
            promotionCapacity: 2);
        PaperPortfolioJudgmentDraft draft = Draft(
            fixture,
            ["paper-a", "paper-b", "paper-c"],
            ["promote", "promote", "hold"]);

        PaperPortfolioJudgmentComputation result =
            PaperPortfolioJudgmentAgentService.Compute(
                fixture.Dispatch,
                PaperTheoryTestFactory.Digest("portfolio-dispatch"),
                fixture.Context,
                draft,
                PaperTheoryTestFactory.Digest("portfolio-agent-result"),
                "2026-08-31T15:20:00Z");

        Assert.Equal(
            ["promote-to-frontier", "promote-to-frontier", "hold"],
            result.Decision.DecisionContent.Decisions.Select(item => item.Action));
        Assert.Equal(2, result.Routes.Count(route => route.NextRoute == "frontier-planning"));
        Assert.Equal(
            "frontier-pending",
            result.UpdatedPortfolio.PortfolioContent.CandidateStates.Single(
                state => state.PaperId == "paper-a").Phase);
        Assert.Equal(
            "audit-pending",
            result.UpdatedPortfolio.PortfolioContent.CandidateStates.Single(
                state => state.PaperId == "paper-c").Phase);
        Assert.Equal(
            result.Decision.DecisionId,
            result.Evidence.EvidenceContent.DecisionRef);
    }

    [Fact]
    public void AgentCannotRankLowerCompositeAheadOfHigherComposite()
    {
        JudgmentFixture fixture = CreateFixture(
            new Dictionary<string, PaperTheoryAuditMetrics>(StringComparer.Ordinal)
            {
                ["paper-a"] = PaperTheoryTestFactory.Metrics(depth: 10, closure: 10, novelty: 10, significance: 10),
                ["paper-b"] = PaperTheoryTestFactory.Metrics(),
                ["paper-c"] = PaperTheoryTestFactory.Metrics(depth: 9, closure: 9, novelty: 9, significance: 9)
            },
            promotionCapacity: 2);
        PaperPortfolioJudgmentDraft draft = Draft(
            fixture,
            ["paper-b", "paper-a", "paper-c"],
            ["promote", "promote", "hold"]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            PaperPortfolioJudgmentAgentService.Compute(
                fixture.Dispatch,
                PaperTheoryTestFactory.Digest("dispatch-wrong-order"),
                fixture.Context,
                draft,
                PaperTheoryTestFactory.Digest("result-wrong-order"),
                "2026-08-31T15:20:00Z"));

        Assert.Contains("lower composite score", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactScoreTiesMayUseTheoremLevelAgentOrdering()
    {
        JudgmentFixture fixture = CreateFixture(
            new Dictionary<string, PaperTheoryAuditMetrics>(StringComparer.Ordinal)
            {
                ["paper-a"] = PaperTheoryTestFactory.Metrics(),
                ["paper-b"] = PaperTheoryTestFactory.Metrics(),
                ["paper-c"] = PaperTheoryTestFactory.Metrics()
            },
            promotionCapacity: 1);
        PaperPortfolioJudgmentDraft draft = Draft(
            fixture,
            ["paper-c", "paper-a", "paper-b"],
            ["promote", "hold", "hold"]);

        PaperPortfolioJudgmentComputation result =
            PaperPortfolioJudgmentAgentService.Compute(
                fixture.Dispatch,
                PaperTheoryTestFactory.Digest("tie-dispatch"),
                fixture.Context,
                draft,
                PaperTheoryTestFactory.Digest("tie-result"),
                "2026-08-31T15:20:00Z");

        Assert.Equal("paper-c", result.Decision.DecisionContent.Decisions[0].PaperId);
        Assert.Equal("promote-to-frontier", result.Decision.DecisionContent.Decisions[0].Action);
        Assert.All(
            result.Decision.DecisionContent.Decisions.Skip(1),
            item => Assert.Equal("hold", item.Action));
    }

    [Fact]
    public void FailedAuditPreservesItsTypedBackroute()
    {
        JudgmentFixture fixture = CreateFixture(
            new Dictionary<string, PaperTheoryAuditMetrics>(StringComparer.Ordinal)
            {
                ["paper-a"] = PaperTheoryTestFactory.Metrics(depth: 10, closure: 10, novelty: 10, significance: 10),
                ["paper-b"] = PaperTheoryTestFactory.Metrics(depth: 9, closure: 9, novelty: 9, significance: 9),
                ["paper-c"] = PaperTheoryTestFactory.Metrics(novelty: 6)
            },
            promotionCapacity: 1,
            failingPaperId: "paper-c");
        string[] order = fixture.Context.Papers
            .OrderByDescending(paper => paper.Scorecard.ScorecardContent.CompositeScore)
            .ThenBy(paper => paper.Coordinates.PaperId, StringComparer.Ordinal)
            .Select(paper => paper.Coordinates.PaperId)
            .ToArray();
        string[] actions = order.Select(paperId => paperId == "paper-c"
            ? "continue-deepening"
            : paperId == order.First(id => id != "paper-c")
                ? "promote"
                : "hold").ToArray();
        PaperPortfolioJudgmentDraft draft = Draft(fixture, order, actions);

        PaperPortfolioJudgmentComputation result =
            PaperPortfolioJudgmentAgentService.Compute(
                fixture.Dispatch,
                PaperTheoryTestFactory.Digest("failed-audit-dispatch"),
                fixture.Context,
                draft,
                PaperTheoryTestFactory.Digest("failed-audit-result"),
                "2026-08-31T15:20:00Z");

        PaperPortfolioPaperDecision failed = result.Decision.DecisionContent.Decisions.Single(
            item => item.PaperId == "paper-c");
        Assert.Equal("continue-deepening", failed.Action);
        Assert.Equal(
            "theory-deepening",
            result.Routes.Single(route => route.PaperId == "paper-c").NextRoute);
        Assert.Equal(
            "theory-deepening",
            result.UpdatedPortfolio.PortfolioContent.CandidateStates.Single(
                state => state.PaperId == "paper-c").Phase);
    }

    [Fact]
    public void DraftMustCompareEveryUnorderedPaperPair()
    {
        JudgmentFixture fixture = CreateFixture(
            new Dictionary<string, PaperTheoryAuditMetrics>(StringComparer.Ordinal)
            {
                ["paper-a"] = PaperTheoryTestFactory.Metrics(depth: 10),
                ["paper-b"] = PaperTheoryTestFactory.Metrics(depth: 9),
                ["paper-c"] = PaperTheoryTestFactory.Metrics()
            },
            promotionCapacity: 2);
        PaperPortfolioJudgmentDraft complete = Draft(
            fixture,
            ["paper-a", "paper-b", "paper-c"],
            ["promote", "promote", "hold"]);
        PaperPortfolioJudgmentDraft incomplete = complete with
        {
            PairwiseRelations = complete.PairwiseRelations.Take(2).ToArray()
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            PaperPortfolioJudgmentAgentService.Compute(
                fixture.Dispatch,
                PaperTheoryTestFactory.Digest("missing-pair-dispatch"),
                fixture.Context,
                incomplete,
                PaperTheoryTestFactory.Digest("missing-pair-result"),
                "2026-08-31T15:20:00Z"));

        Assert.Contains("every unordered paper pair", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentCannotPromoteEligibleOverflowPastCapacity()
    {
        JudgmentFixture fixture = CreateFixture(
            new Dictionary<string, PaperTheoryAuditMetrics>(StringComparer.Ordinal)
            {
                ["paper-a"] = PaperTheoryTestFactory.Metrics(depth: 10),
                ["paper-b"] = PaperTheoryTestFactory.Metrics(depth: 9),
                ["paper-c"] = PaperTheoryTestFactory.Metrics()
            },
            promotionCapacity: 1);
        PaperPortfolioJudgmentDraft draft = Draft(
            fixture,
            ["paper-a", "paper-b", "paper-c"],
            ["promote", "promote", "hold"]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            PaperPortfolioJudgmentAgentService.Compute(
                fixture.Dispatch,
                PaperTheoryTestFactory.Digest("over-capacity-dispatch"),
                fixture.Context,
                draft,
                PaperTheoryTestFactory.Digest("over-capacity-result"),
                "2026-08-31T15:20:00Z"));

        Assert.Contains("deterministic hard gate", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PairwiseEvidenceCannotLeakFromAThirdPaper()
    {
        JudgmentFixture fixture = CreateFixture(
            new Dictionary<string, PaperTheoryAuditMetrics>(StringComparer.Ordinal)
            {
                ["paper-a"] = PaperTheoryTestFactory.Metrics(depth: 10),
                ["paper-b"] = PaperTheoryTestFactory.Metrics(depth: 9),
                ["paper-c"] = PaperTheoryTestFactory.Metrics()
            },
            promotionCapacity: 2);
        PaperPortfolioJudgmentDraft complete = Draft(
            fixture,
            ["paper-a", "paper-b", "paper-c"],
            ["promote", "promote", "hold"]);
        PaperPortfolioPairwiseRelationDraft first = complete.PairwiseRelations[0] with
        {
            EvidenceRefs =
            [complete.PairwiseRelations[0].EvidenceRefs[0], fixture.Context.Papers.Single(paper => paper.Coordinates.PaperId == "paper-c").Coordinates.CandidatePaperRef]
        };
        PaperPortfolioJudgmentDraft leaked = complete with
        {
            PairwiseRelations = [first, .. complete.PairwiseRelations.Skip(1)]
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            PaperPortfolioJudgmentAgentService.Compute(
                fixture.Dispatch,
                PaperTheoryTestFactory.Digest("leaked-evidence-dispatch"),
                fixture.Context,
                leaked,
                PaperTheoryTestFactory.Digest("leaked-evidence-result"),
                "2026-08-31T15:20:00Z"));

        Assert.Contains("outside the two compared papers", error.Message, StringComparison.Ordinal);
    }

    private static JudgmentFixture CreateFixture(
        IReadOnlyDictionary<string, PaperTheoryAuditMetrics> metrics,
        int promotionCapacity,
        string? failingPaperId = null)
    {
        string[] paperIds = metrics.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        PaperTheoryFixture theory = PaperTheoryTestFactory.CreatePortfolio(paperIds);
        var papers = new List<PaperPortfolioJudgmentPaperContext>();
        var coordinates = new List<PaperPortfolioJudgmentPaperInput>();
        foreach (string paperId in paperIds)
        {
            string verdict = string.Equals(paperId, failingPaperId, StringComparison.Ordinal)
                ? "deepen"
                : "pass";
            IReadOnlyList<string> blockers = verdict == "pass"
                ? []
                : ["The novelty boundary remains below the A3 threshold and requires another theorem-deepening round."];
            PaperTheoryAudit audit = PaperTheoryTestFactory.CreateAudit(
                theory,
                paperId,
                metrics[paperId],
                verdict,
                blockers);
            PaperCandidateScorecard scorecard =
                PaperPortfolioDecisionService.CreateScorecard(
                    theory.Packages[paperId],
                    audit,
                    "2026-08-31T15:00:00Z");
            PaperTheoryProgram program = theory.Programs.Single(
                value => value.ProgramContent.PaperId == paperId);
            var item = new PaperPortfolioJudgmentPaperInput(
                paperId,
                program.TheoryProgramId,
                theory.Scopes[paperId].ScopeId,
                theory.Inventories[paperId].InventoryId,
                theory.Packages[paperId].TheoremPackageId,
                audit.AuditId,
                scorecard.ScorecardId,
                program.ProgramContent.CandidatePaperRef,
                program.ProgramContent.LiteratureResearchRef);
            coordinates.Add(item);
            papers.Add(new PaperPortfolioJudgmentPaperContext(
                item,
                program,
                theory.Scopes[paperId],
                theory.Inventories[paperId],
                theory.Packages[paperId],
                audit,
                scorecard));
        }
        var inputs = new List<PaperAgentInputArtifact>
        {
            Input(PaperPortfolioSchemas.Portfolio, theory.Portfolio.PortfolioId, "portfolio"),
            Input(PaperPortfolioSchemas.CandidateBatch, theory.Batch.BatchId, "candidate-batch")
        };
        foreach (PaperPortfolioJudgmentPaperContext paper in papers)
        {
            string id = paper.Coordinates.PaperId;
            inputs.Add(Input(PaperPortfolioSchemas.TheoryProgram, paper.Program.TheoryProgramId, $"{id}-program"));
            inputs.Add(Input(PaperTheoryFoundationSchemas.Scope, paper.Scope.ScopeId, $"{id}-scope"));
            inputs.Add(Input(PaperTheoryFoundationSchemas.Inventory, paper.Inventory.InventoryId, $"{id}-inventory"));
            inputs.Add(Input(PaperTheoryDeepeningSchemas.TheoremPackage, paper.TheoremPackage.TheoremPackageId, $"{id}-package"));
            inputs.Add(Input(PaperTheoryAuditSchemas.Audit, paper.Audit.AuditId, $"{id}-audit"));
            inputs.Add(Input(PaperPortfolioDecisionSchemas.Scorecard, paper.Scorecard.ScorecardId, $"{id}-scorecard"));
            inputs.Add(Input(CandidateArtifactSchemas.CandidatePaper, paper.Coordinates.CandidatePaperRef, $"{id}-candidate"));
            inputs.Add(Input(CandidateArtifactSchemas.LiteratureResearch, paper.Coordinates.LiteratureResearchRef, $"{id}-literature"));
        }
        var dispatch = new PaperPortfolioJudgmentAgentDispatch(
            PaperPortfolioJudgmentAgentSchemas.Dispatch,
            theory.Portfolio.PortfolioId,
            theory.Batch.BatchId,
            theory.Portfolio.PortfolioContent.NextCycleNumber,
            new PaperPortfolioDecisionPolicy(promotionCapacity, 2),
            coordinates,
            inputs,
            "2026-08-31T15:10:00Z");
        var context = new PaperPortfolioJudgmentContext(
            theory.Portfolio,
            theory.Batch,
            papers);
        return new JudgmentFixture(theory, dispatch, context);
    }

    private static PaperPortfolioJudgmentDraft Draft(
        JudgmentFixture fixture,
        IReadOnlyList<string> order,
        IReadOnlyList<string> actions)
    {
        var byPaper = fixture.Context.Papers.ToDictionary(
            paper => paper.Coordinates.PaperId,
            StringComparer.Ordinal);
        PaperPortfolioJudgmentPaperDraft[] ordered = order.Select((paperId, index) =>
            new PaperPortfolioJudgmentPaperDraft(
                index + 1,
                paperId,
                byPaper[paperId].Scorecard.ScorecardId,
                actions[index],
                $"{paperId} provides a stronger theorem-level contribution after independent A3 review and supplies a distinct proof architecture for the portfolio.",
                $"{paperId} still carries a material formalization or overlap risk that must remain visible during resource allocation.",
                $"Rank {index + 1} follows the admitted scorecard order and the comparative theorem evidence without changing any A3 metric or promotion gate."))
            .ToArray();
        var relations = new List<PaperPortfolioPairwiseRelationDraft>();
        for (int left = 0; left < order.Count; left++)
        {
            for (int right = left + 1; right < order.Count; right++)
            {
                PaperPortfolioJudgmentPaperContext l = byPaper[order[left]];
                PaperPortfolioJudgmentPaperContext r = byPaper[order[right]];
                relations.Add(new PaperPortfolioPairwiseRelationDraft(
                    l.Coordinates.PaperId,
                    r.Coordinates.PaperId,
                    "distinct",
                    string.Empty,
                    [l.Coordinates.CandidatePaperRef, r.Coordinates.CandidatePaperRef],
                    "The two theorem packages have separately identifiable load-bearing theorem chains and neither package logically subsumes the other under the admitted evidence.",
                    "The two novelty increments address distinct theorem-level hypotheses and conclusions, so neither candidate duplicates the other's publishable contribution."));
            }
        }
        return new PaperPortfolioJudgmentDraft(
            PaperPortfolioJudgmentAgentSchemas.Draft,
            fixture.Dispatch.PortfolioRef,
            fixture.Dispatch.CandidateBatchRef,
            fixture.Dispatch.CycleNumber,
            fixture.Context.Papers.Select(paper => paper.Scorecard.ScorecardId).ToArray(),
            ordered,
            relations,
            "The portfolio ranking preserves the repository-calibrated A3 score order, allocates scarce formalization capacity only to eligible papers, and records pairwise theorem and novelty interactions for every candidate without changing the scientific evidence.",
            "2026-08-31T15:15:00Z");
    }

    private static PaperAgentInputArtifact Input(
        string schema,
        string reference,
        string stem) =>
        new(schema, reference, $"artifacts/test/{stem}.json");

    private sealed record JudgmentFixture(
        PaperTheoryFixture Theory,
        PaperPortfolioJudgmentAgentDispatch Dispatch,
        PaperPortfolioJudgmentContext Context);
}
