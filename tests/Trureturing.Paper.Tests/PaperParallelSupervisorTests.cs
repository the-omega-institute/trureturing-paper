using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperParallelSupervisorTests
{
    [Fact]
    public void PlanAssignsOneDistinctPaperPerWorkerSlot()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b",
            "paper-c");

        PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
            fixture.Portfolio,
            fixture.Programs,
            Contexts(fixture),
            Policy(maximumParallelPapers: 3),
            "2026-08-31T06:00:00Z");

        Assert.Equal("parallel-paper-workers", plan.PlanContent.ExecutionMode);
        Assert.Equal(3, plan.PlanContent.WorkItems.Count);
        Assert.Equal(
            3,
            plan.PlanContent.WorkItems
                .Select(item => item.WorkItemContent.PaperId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            3,
            plan.PlanContent.WorkItems
                .Select(item => item.WorkItemContent.TheoryProgramRef)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            Enumerable.Range(1, 3),
            plan.PlanContent.WorkItems.Select(item => item.WorkItemContent.WorkerSlot));
    }

    [Fact]
    public void AuditPendingWorkItemUsesFreshReviewContract()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");

        PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
            fixture.Portfolio,
            fixture.Programs,
            Contexts(fixture),
            Policy(maximumParallelPapers: 2),
            "2026-08-31T06:00:00Z");
        PaperSupervisorWorkItem workItem = plan.PlanContent.WorkItems[0];

        Assert.Equal("audit-pending", workItem.WorkItemContent.Phase);
        Assert.Equal(
            PaperTheoryAuditService.FreshContextMode,
            workItem.WorkItemContent.ContextMode);
        Assert.Equal(
            "paper-theory-independent-referee",
            workItem.WorkItemContent.AgentRole);
        Assert.Contains(
            workItem.WorkItemContent.Contract.ForbiddenShortcuts,
            rule => rule.Contains("theory-author run", StringComparison.Ordinal));
        Assert.Contains(
            PaperTheoryAuditSchemas.Audit,
            workItem.WorkItemContent.Contract.RequiredOutputSchemas);
        Assert.Contains(
            fixture.Packages[workItem.WorkItemContent.PaperId].TheoremPackageId,
            workItem.WorkItemContent.Contract.ExactInputRefs);
    }

    [Fact]
    public async Task ExecuteCycleStartsSeveralPaperWorkersConcurrently()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b",
            "paper-c");
        PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
            fixture.Portfolio,
            fixture.Programs,
            Contexts(fixture),
            Policy(maximumParallelPapers: 3),
            "2026-08-31T06:00:00Z");
        var worker = new BarrierWorker(expectedWorkers: 3);

        PaperSupervisorCycleOutcome outcome =
            await PaperParallelSupervisorService.ExecuteCycleAsync(plan, worker);

        Assert.Equal(3, worker.MaximumObservedConcurrency);
        Assert.Equal(3, outcome.OutcomeContent.SubstantiveProgressCount);
        Assert.Equal(0, outcome.OutcomeContent.FailedCount);
        Assert.Equal(
            new[] { "paper-a", "paper-b", "paper-c" },
            outcome.OutcomeContent.Results
                .Select(result => result.ResultContent.PaperId)
                .ToArray());
    }

    [Fact]
    public async Task CompletionOrderDoesNotChangeCycleOutcomeIdentity()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b",
            "paper-c");
        PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
            fixture.Portfolio,
            fixture.Programs,
            Contexts(fixture),
            Policy(maximumParallelPapers: 3),
            "2026-08-31T06:00:00Z");

        PaperSupervisorCycleOutcome first =
            await PaperParallelSupervisorService.ExecuteCycleAsync(
                plan,
                new OrderedDelayWorker(reverse: false));
        PaperSupervisorCycleOutcome second =
            await PaperParallelSupervisorService.ExecuteCycleAsync(
                plan,
                new OrderedDelayWorker(reverse: true));

        Assert.Equal(first.OutcomeId, second.OutcomeId);
        Assert.Equal(
            first.OutcomeContent.Results.Select(result => result.ResultId),
            second.OutcomeContent.Results.Select(result => result.ResultId));
    }

    [Fact]
    public async Task SubstantiveProgressUpdatesEveryLeasedPaperIndependently()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b",
            "paper-c");
        PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
            fixture.Portfolio,
            fixture.Programs,
            Contexts(fixture),
            Policy(maximumParallelPapers: 2),
            "2026-08-31T06:00:00Z");
        PaperSupervisorCycleOutcome outcome =
            await PaperParallelSupervisorService.ExecuteCycleAsync(
                plan,
                new ImmediateProgressWorker());

        PaperResearchPortfolio updated = PaperParallelSupervisorService.ApplyOutcome(
            fixture.Portfolio,
            plan,
            outcome,
            "2026-08-31T06:20:00Z");

        string[] leased = plan.PlanContent.WorkItems
            .Select(item => item.WorkItemContent.PaperId)
            .ToArray();
        foreach (PaperCandidateState state in updated.PortfolioContent.CandidateStates)
        {
            if (leased.Contains(state.PaperId, StringComparer.Ordinal))
            {
                Assert.Equal("frontier-pending", state.Phase);
                Assert.Equal(4, state.CompletedCycles);
                Assert.Equal(0, state.ConsecutiveNoProgressCycles);
                Assert.Contains("substantive progress", state.StatusReason, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal("audit-pending", state.Phase);
                Assert.Equal(3, state.CompletedCycles);
            }
        }
        Assert.Equal(2, updated.PortfolioContent.NextCycleNumber);
    }

    [Fact]
    public async Task WaitingPaperDoesNotAccumulateNoProgressPenalty()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
            fixture.Portfolio,
            fixture.Programs,
            Contexts(fixture),
            Policy(maximumParallelPapers: 2),
            "2026-08-31T06:00:00Z");
        PaperSupervisorCycleOutcome outcome =
            await PaperParallelSupervisorService.ExecuteCycleAsync(
                plan,
                new StatusWorker("waiting"));

        PaperResearchPortfolio updated = PaperParallelSupervisorService.ApplyOutcome(
            fixture.Portfolio,
            plan,
            outcome,
            "2026-08-31T06:20:00Z");

        Assert.All(updated.PortfolioContent.CandidateStates, state =>
        {
            Assert.Equal(3, state.CompletedCycles);
            Assert.Equal(0, state.ConsecutiveNoProgressCycles);
            Assert.Equal("audit-pending", state.Phase);
            Assert.Contains("waiting", state.StatusReason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RepeatedNoProgressEscalatesThenParksPaper()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperResearchPortfolio portfolio = fixture.Portfolio;
        PaperSupervisorPolicy policy = new(
            MaximumParallelPapers: 2,
            WorkerTimeoutSeconds: 60,
            NoProgressEscalationThreshold: 2,
            NoProgressParkThreshold: 3);

        for (int round = 0; round < 3; round++)
        {
            PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
                portfolio,
                fixture.Programs,
                Contexts(fixture),
                policy,
                $"2026-08-31T0{6 + round}:00:00Z");
            PaperSupervisorCycleOutcome outcome =
                await PaperParallelSupervisorService.ExecuteCycleAsync(
                    plan,
                    new StatusWorker("no-progress"));
            portfolio = PaperParallelSupervisorService.ApplyOutcome(
                portfolio,
                plan,
                outcome,
                $"2026-08-31T0{6 + round}:20:00Z");
        }

        Assert.All(portfolio.PortfolioContent.CandidateStates, state =>
        {
            Assert.Equal("parked", state.Phase);
            Assert.Equal(3, state.ConsecutiveNoProgressCycles);
            Assert.Contains("independent escalation required", state.StatusReason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void WorkerCannotReturnArtifactOutsidePhaseContract()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b");
        PaperSupervisorCyclePlan plan = PaperParallelSupervisorService.CreatePlan(
            fixture.Portfolio,
            fixture.Programs,
            Contexts(fixture),
            Policy(maximumParallelPapers: 2),
            "2026-08-31T06:00:00Z");
        PaperSupervisorWorkItem workItem = plan.PlanContent.WorkItems[0];

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperParallelSupervisorService.CreateWorkerResult(
                workItem,
                "substantive-progress",
                "frontier-pending",
                [new PaperSupervisorArtifact(
                    "unauthorized-manuscript.v1",
                    PaperTheoryTestFactory.Digest("unauthorized"))],
                PaperTheoryTestFactory.Digest("transition"),
                "claimed progress outside the phase contract",
                "2026-08-31T06:01:00Z",
                "2026-08-31T06:02:00Z"));

        Assert.Contains("outside its phase contract", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefillStartsOnlyAtOrBelowPortfolioLowWatermark()
    {
        PaperTheoryFixture fixture = PaperTheoryTestFactory.CreatePortfolio(
            "paper-a",
            "paper-b",
            "paper-c");
        PaperCandidateState[] states = fixture.Portfolio.PortfolioContent.CandidateStates
            .Select(state => state.PaperId switch
            {
                "paper-a" => state,
                "paper-b" => state with { Phase = "done", StatusReason = "released" },
                _ => state with { Phase = "archived", StatusReason = "superseded" }
            })
            .ToArray();
        PaperResearchPortfolioContent content = fixture.Portfolio.PortfolioContent with
        {
            CandidateStates = states,
            UpdatedAt = "2026-08-31T06:00:00Z"
        };
        PaperResearchPortfolio lowPortfolio = new(
            PaperPortfolioSchemas.Portfolio,
            CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content)),
            content);

        PaperPortfolioRefillRequest request =
            PaperParallelSupervisorService.CreateRefillRequest(
                lowPortfolio,
                "2026-08-31T06:10:00Z");

        Assert.Equal(4, request.RequestContent.RequestedCandidateCount);
        Assert.Equal(lowPortfolio.PortfolioId, request.RequestContent.PortfolioRef);
        Assert.Equal(
            lowPortfolio.PortfolioContent.PaperResearchInputRef,
            request.RequestContent.PaperResearchInputRef);
    }

    private static PaperSupervisorPolicy Policy(int maximumParallelPapers) =>
        new(
            maximumParallelPapers,
            WorkerTimeoutSeconds: 60,
            NoProgressEscalationThreshold: 2,
            NoProgressParkThreshold: 4);

    private static PaperSupervisorPaperContext[] Contexts(
        PaperTheoryFixture fixture) =>
        fixture.Programs.Select(program =>
        {
            string paperId = program.ProgramContent.PaperId;
            return new PaperSupervisorPaperContext(
                paperId,
                program.TheoryProgramId,
                $"papers/{paperId}",
                fixture.Scopes[paperId].ScopeId,
                fixture.Inventories[paperId].InventoryId,
                fixture.Packages[paperId].TheoremPackageId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }).ToArray();

    private sealed class ImmediateProgressWorker : IPaperResearchWorker
    {
        public Task<PaperSupervisorWorkerResult> ExecuteAsync(
            PaperSupervisorWorkItem workItem,
            CancellationToken cancellationToken) =>
            Task.FromResult(Progress(workItem));
    }

    private sealed class StatusWorker(string status) : IPaperResearchWorker
    {
        public Task<PaperSupervisorWorkerResult> ExecuteAsync(
            PaperSupervisorWorkItem workItem,
            CancellationToken cancellationToken) =>
            Task.FromResult(PaperParallelSupervisorService.CreateWorkerResult(
                workItem,
                status,
                workItem.WorkItemContent.Phase,
                [],
                string.Empty,
                $"simulated {status}",
                "2026-08-31T06:01:00Z",
                "2026-08-31T06:02:00Z"));
    }

    private sealed class BarrierWorker(int expectedWorkers) : IPaperResearchWorker
    {
        private readonly TaskCompletionSource<bool> _allStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _started;
        private int _maximumObservedConcurrency;

        public int MaximumObservedConcurrency => Volatile.Read(
            ref _maximumObservedConcurrency);

        public async Task<PaperSupervisorWorkerResult> ExecuteAsync(
            PaperSupervisorWorkItem workItem,
            CancellationToken cancellationToken)
        {
            int active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            if (Interlocked.Increment(ref _started) == expectedWorkers)
            {
                _allStarted.TrySetResult(true);
            }
            await _allStarted.Task.WaitAsync(cancellationToken);
            await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref _active);
            return Progress(workItem);
        }

        private void UpdateMaximum(int value)
        {
            int observed;
            do
            {
                observed = Volatile.Read(ref _maximumObservedConcurrency);
                if (value <= observed)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(
                ref _maximumObservedConcurrency,
                value,
                observed) != observed);
        }
    }

    private sealed class OrderedDelayWorker(bool reverse) : IPaperResearchWorker
    {
        public async Task<PaperSupervisorWorkerResult> ExecuteAsync(
            PaperSupervisorWorkItem workItem,
            CancellationToken cancellationToken)
        {
            int slot = workItem.WorkItemContent.WorkerSlot;
            int delay = reverse ? slot * 15 : (4 - slot) * 15;
            await Task.Delay(delay, cancellationToken);
            return Progress(workItem);
        }
    }

    private static PaperSupervisorWorkerResult Progress(
        PaperSupervisorWorkItem workItem) =>
        PaperParallelSupervisorService.CreateWorkerResult(
            workItem,
            "substantive-progress",
            "frontier-pending",
            [new PaperSupervisorArtifact(
                PaperTheoryAuditSchemas.Audit,
                PaperTheoryTestFactory.Digest(
                    $"audit-{workItem.WorkItemContent.PaperId}"))],
            PaperTheoryTestFactory.Digest(
                $"transition-{workItem.WorkItemContent.PaperId}"),
            "fresh independent theory audit completed",
            "2026-08-31T06:01:00Z",
            "2026-08-31T06:02:00Z");
}
