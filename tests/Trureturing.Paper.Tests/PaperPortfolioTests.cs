using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class PaperPortfolioTests
{
    [Fact]
    public void BatchCreatesDistinctPaperProgramsAndParallelLeases()
    {
        PaperCandidateBatch batch = Batch(candidateCount: 5, maxParallel: 3);
        PaperTheoryProgram[] programs = Programs(batch);
        PaperResearchPortfolio portfolio = PaperPortfolioService.CreatePortfolio(
            batch,
            programs,
            "2026-08-31T00:00:00Z");

        PaperPortfolioCycle cycle = PaperPortfolioService.PlanCycle(
            portfolio,
            programs,
            "2026-08-31T06:00:00Z");

        Assert.Equal("parallel-paper-batch", cycle.CycleContent.ExecutionMode);
        Assert.Equal(5, cycle.CycleContent.RunnablePaperCount);
        Assert.Equal(3, cycle.CycleContent.GrantedParallelism);
        Assert.Equal(3, cycle.CycleContent.Leases.Count);
        Assert.Equal(
            3,
            cycle.CycleContent.Leases
                .Select(lease => lease.PaperId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            Enumerable.Range(1, cycle.CycleContent.Leases.Count),
            cycle.CycleContent.Leases.Select(lease => lease.WorkerSlot));
    }

    [Fact]
    public void SamePortfolioAndClockReplayToSameCycleIdentity()
    {
        PaperCandidateBatch batch = Batch(candidateCount: 5, maxParallel: 4);
        PaperTheoryProgram[] programs = Programs(batch);
        PaperResearchPortfolio portfolio = PaperPortfolioService.CreatePortfolio(
            batch,
            programs,
            "2026-08-31T00:00:00Z");

        PaperPortfolioCycle first = PaperPortfolioService.PlanCycle(
            portfolio,
            programs,
            "2026-08-31T12:00:00Z");
        PaperPortfolioCycle replay = PaperPortfolioService.PlanCycle(
            portfolio,
            programs.Reverse().ToArray(),
            "2026-08-31T12:00:00Z");

        Assert.Equal(first.CycleId, replay.CycleId);
        Assert.Equal(
            first.CycleContent.Leases.Select(lease => lease.LeaseId),
            replay.CycleContent.Leases.Select(lease => lease.LeaseId));
    }

    [Fact]
    public void OneCandidateArtifactCannotOccupyTwoPaperSlots()
    {
        PaperCandidateBatchContent content = BatchContent(
            candidateCount: 2,
            maxParallel: 2);
        PaperCandidateSeed duplicate = content.Candidates[0] with
        {
            PaperId = "paper-duplicate"
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperPortfolioService.CreateBatch(
                content with
                {
                    Candidates = [content.Candidates[0], duplicate]
                }));

        Assert.Contains(
            "candidate-paper artifact",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateBatchMustContainMultiplePapers()
    {
        PaperCandidateBatchContent original = BatchContent(
            candidateCount: 2,
            maxParallel: 2);
        PaperCandidateBatchContent content = original with
        {
            Candidates = [original.Candidates[0]]
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperPortfolioService.CreateBatch(content));

        Assert.Contains(
            "between two papers",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PerPaperLeaseLimitMustRemainOne()
    {
        PaperCandidateBatchContent content = BatchContent(
            candidateCount: 3,
            maxParallel: 3) with
        {
            Policy = new PaperPortfolioPolicy(
                5,
                3,
                2,
                1)
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperPortfolioService.CreateBatch(content));

        Assert.Contains(
            "exactly one",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramCannotChangeExactResearchState()
    {
        PaperCandidateBatch batch = Batch(candidateCount: 3, maxParallel: 3);
        PaperTheoryProgram program = PaperPortfolioService.CreateTheoryProgram(
            batch,
            "paper-01",
            "2026-08-31T00:00:00Z");
        PaperTheoryProgramContent changed = program.ProgramContent with
        {
            TopologyDigest = Digest("different-topology")
        };
        PaperTheoryProgram tampered = new(
            PaperPortfolioSchemas.TheoryProgram,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(changed)),
            changed);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperPortfolioService.CreatePortfolio(
                batch,
                [
                    tampered,
                    PaperPortfolioService.CreateTheoryProgram(
                        batch,
                        "paper-02",
                        "2026-08-31T00:00:00Z"),
                    PaperPortfolioService.CreateTheoryProgram(
                        batch,
                        "paper-03",
                        "2026-08-31T00:00:00Z")
                ],
                "2026-08-31T00:00:00Z"));

        Assert.Contains(
            "exact research input",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OlderPaperReceivesStarvationBoostAcrossBatch()
    {
        PaperCandidateBatch batch = Batch(candidateCount: 3, maxParallel: 2);
        PaperTheoryProgram[] programs = Programs(batch);
        PaperResearchPortfolio portfolio = PaperPortfolioService.CreatePortfolio(
            batch,
            programs,
            "2026-08-31T00:00:00Z");
        PaperCandidateState[] states = portfolio.PortfolioContent.CandidateStates
            .Select(state => state.PaperId switch
            {
                "paper-01" => state with
                {
                    Priority = 80,
                    LastProgressAt = "2026-08-31T11:00:00Z"
                },
                "paper-02" => state with
                {
                    Priority = 70,
                    LastProgressAt = "2026-08-20T00:00:00Z"
                },
                _ => state with
                {
                    Priority = 10,
                    LastProgressAt = "2026-08-31T11:00:00Z"
                }
            })
            .ToArray();
        PaperResearchPortfolioContent content =
            portfolio.PortfolioContent with
            {
                CandidateStates = states,
                UpdatedAt = "2026-08-31T11:00:00Z"
            };
        portfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
            content);

        PaperPortfolioCycle cycle = PaperPortfolioService.PlanCycle(
            portfolio,
            programs,
            "2026-08-31T12:00:00Z");

        Assert.Equal(
            ["paper-02", "paper-01"],
            cycle.CycleContent.Leases
                .Select(lease => lease.PaperId)
                .ToArray());
    }

    [Fact]
    public void TerminalPaperDoesNotConsumeParallelWorkerSlot()
    {
        PaperCandidateBatch batch = Batch(candidateCount: 4, maxParallel: 3);
        PaperTheoryProgram[] programs = Programs(batch);
        PaperResearchPortfolio portfolio = PaperPortfolioService.CreatePortfolio(
            batch,
            programs,
            "2026-08-31T00:00:00Z");
        PaperCandidateState[] states = portfolio.PortfolioContent.CandidateStates
            .Select(state => state.PaperId == "paper-01"
                ? state with
                {
                    Phase = "done",
                    StatusReason = "manuscript released"
                }
                : state)
            .ToArray();
        PaperResearchPortfolioContent content =
            portfolio.PortfolioContent with
            {
                CandidateStates = states
            };
        portfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
            content);

        PaperPortfolioCycle cycle = PaperPortfolioService.PlanCycle(
            portfolio,
            programs,
            "2026-08-31T06:00:00Z");

        Assert.Equal(3, cycle.CycleContent.RunnablePaperCount);
        Assert.Equal(3, cycle.CycleContent.GrantedParallelism);
        Assert.DoesNotContain(
            cycle.CycleContent.Leases,
            lease => lease.PaperId == "paper-01");
    }

    private static PaperCandidateBatch Batch(
        int candidateCount,
        int maxParallel) =>
        PaperPortfolioService.CreateBatch(
            BatchContent(candidateCount, maxParallel));

    private static PaperCandidateBatchContent BatchContent(
        int candidateCount,
        int maxParallel)
    {
        PaperCandidateSeed[] candidates = Enumerable.Range(1, candidateCount)
            .Select(index => new PaperCandidateSeed(
                $"paper-{index:00}",
                Digest($"candidate-{index}"),
                Digest($"literature-{index}"),
                Digest($"intuition-{index}"),
                90 - index,
                "2026-08-31T00:00:00Z"))
            .ToArray();
        return new PaperCandidateBatchContent(
            Digest("truth"),
            Digest("topology"),
            Digest("research-input"),
            new PaperPortfolioPolicy(
                Math.Max(5, candidateCount),
                maxParallel,
                1,
                1),
            candidates);
    }

    private static PaperTheoryProgram[] Programs(
        PaperCandidateBatch batch) =>
        batch.BatchContent.Candidates
            .Select(candidate => PaperPortfolioService.CreateTheoryProgram(
                batch,
                candidate.PaperId,
                "2026-08-31T00:00:00Z"))
            .ToArray();

    private static string Digest(string seed) =>
        CanonicalJson.Sha256Reference(
            System.Text.Encoding.UTF8.GetBytes(seed));
}
