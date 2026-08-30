using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperParallelSupervisorSchemas
{
    public const string CyclePlan = "paper-supervisor-cycle-plan.v1";
    public const string WorkerResult = "paper-supervisor-worker-result.v1";
    public const string CycleOutcome = "paper-supervisor-cycle-outcome.v1";
    public const string RefillRequest = "paper-portfolio-refill-request.v1";
}

public sealed record PaperSupervisorPolicy(
    [property: JsonRequired] int MaximumParallelPapers,
    [property: JsonRequired] int WorkerTimeoutSeconds,
    [property: JsonRequired] int NoProgressEscalationThreshold,
    [property: JsonRequired] int NoProgressParkThreshold);

public sealed record PaperSupervisorPaperContext(
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string WorkingDirectory,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string LatestTheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] string FormalizationFrontierRef,
    [property: JsonRequired] string FormalizationFrontierStateRef,
    [property: JsonRequired] string CertifiedClaimManifestRef);

public sealed record PaperSupervisorWorkItemContent(
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string PortfolioCycleRef,
    [property: JsonRequired] string LeaseRef,
    [property: JsonRequired] int WorkerSlot,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] string WorkingDirectory,
    [property: JsonRequired] PaperCodexPhaseContract Contract,
    [property: JsonRequired] string Prompt,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperSupervisorWorkItem(
    [property: JsonRequired] string WorkItemId,
    [property: JsonRequired] PaperSupervisorWorkItemContent WorkItemContent);

public sealed record PaperSupervisorCyclePlanContent(
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string PortfolioCycleRef,
    [property: JsonRequired] PaperSupervisorPolicy Policy,
    [property: JsonRequired] string ExecutionMode,
    [property: JsonRequired] IReadOnlyList<PaperSupervisorWorkItem> WorkItems,
    [property: JsonRequired] string PlannedAt);

public sealed record PaperSupervisorCyclePlan(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PlanId,
    [property: JsonRequired] PaperSupervisorCyclePlanContent PlanContent);

public sealed record PaperSupervisorArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ArtifactRef);

public sealed record PaperSupervisorWorkerResultContent(
    [property: JsonRequired] string WorkItemRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string NextPhase,
    [property: JsonRequired] IReadOnlyList<PaperSupervisorArtifact> Artifacts,
    [property: JsonRequired] string StateTransitionRef,
    [property: JsonRequired] string Detail,
    [property: JsonRequired] string StartedAt,
    [property: JsonRequired] string CompletedAt);

public sealed record PaperSupervisorWorkerResult(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ResultId,
    [property: JsonRequired] PaperSupervisorWorkerResultContent ResultContent);

public sealed record PaperSupervisorCycleOutcomeContent(
    [property: JsonRequired] string PlanRef,
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string PortfolioCycleRef,
    [property: JsonRequired] IReadOnlyList<PaperSupervisorWorkerResult> Results,
    [property: JsonRequired] int SubstantiveProgressCount,
    [property: JsonRequired] int NoProgressCount,
    [property: JsonRequired] int WaitingCount,
    [property: JsonRequired] int BlockedCount,
    [property: JsonRequired] int FailedCount,
    [property: JsonRequired] string CompletedAt);

public sealed record PaperSupervisorCycleOutcome(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string OutcomeId,
    [property: JsonRequired] PaperSupervisorCycleOutcomeContent OutcomeContent);

public sealed record PaperPortfolioRefillRequestContent(
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyDigest,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] int RequestedCandidateCount,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperPortfolioRefillRequest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] PaperPortfolioRefillRequestContent RequestContent);

public interface IPaperResearchWorker
{
    Task<PaperSupervisorWorkerResult> ExecuteAsync(
        PaperSupervisorWorkItem workItem,
        CancellationToken cancellationToken);
}

public static class PaperParallelSupervisorService
{
    public const string ParallelExecutionMode = "parallel-paper-workers";

    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> WorkerStatuses = new(
        ["substantive-progress", "no-progress", "waiting", "blocked", "failed"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> CandidatePhases = new(
        ["scope-pending", "inventory-pending", "theory-deepening", "audit-pending",
         "frontier-pending", "formalizing", "certification-pending",
         "manuscript-pending", "parked", "archived", "done"],
        StringComparer.Ordinal);

    public static PaperSupervisorCyclePlan CreatePlan(
        PaperResearchPortfolio portfolio,
        IReadOnlyList<PaperTheoryProgram> programs,
        IReadOnlyList<PaperSupervisorPaperContext> contexts,
        PaperSupervisorPolicy policy,
        string plannedAt)
    {
        PaperPortfolioService.Validate(portfolio);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(contexts);
        ValidatePolicy(policy, portfolio.PortfolioContent.Policy.MaxParallelPapers);
        ParseUtc(plannedAt, nameof(plannedAt));

        PaperPortfolioCycle portfolioCycle = PaperPortfolioService.PlanCycle(
            portfolio,
            programs,
            plannedAt);
        if (portfolioCycle.CycleContent.GrantedParallelism > policy.MaximumParallelPapers)
        {
            throw new InvalidDataException(
                "Supervisor policy grants fewer paper workers than the portfolio cycle.");
        }
        var contextByPaper = new Dictionary<string, PaperSupervisorPaperContext>(
            StringComparer.Ordinal);
        foreach (PaperSupervisorPaperContext context in contexts)
        {
            ValidateContext(context);
            if (!contextByPaper.TryAdd(context.PaperId, context))
            {
                throw new InvalidDataException(
                    "Supervisor paper contexts must have unique paper IDs.");
            }
        }

        PaperSupervisorWorkItem[] workItems = portfolioCycle.CycleContent.Leases
            .Select(lease =>
            {
                if (!contextByPaper.TryGetValue(
                        lease.PaperId,
                        out PaperSupervisorPaperContext? context))
                {
                    throw new InvalidDataException(
                        $"Missing supervisor context for leased paper {lease.PaperId}.");
                }
                if (!string.Equals(
                        context.TheoryProgramRef,
                        lease.TheoryProgramRef,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Supervisor context changed the leased theory program.");
                }
                PaperCodexPhaseContract contract = BuildPhaseContract(lease.Phase, context);
                var content = new PaperSupervisorWorkItemContent(
                    portfolio.PortfolioId,
                    portfolioCycle.CycleId,
                    lease.LeaseId,
                    lease.WorkerSlot,
                    lease.PaperId,
                    lease.TheoryProgramRef,
                    lease.Phase,
                    AgentRole(lease.Phase),
                    ContextMode(lease.Phase),
                    context.WorkingDirectory,
                    contract,
                    BuildPrompt(lease, contract),
                    plannedAt);
                ValidateWorkItemContent(content);
                return new PaperSupervisorWorkItem(Reference(content), content);
            })
            .OrderBy(item => item.WorkItemContent.WorkerSlot)
            .ToArray();
        var planContent = new PaperSupervisorCyclePlanContent(
            portfolio.PortfolioId,
            portfolioCycle.CycleId,
            policy,
            ParallelExecutionMode,
            workItems,
            plannedAt);
        ValidatePlanContent(planContent, portfolioCycle);
        return new(
            PaperParallelSupervisorSchemas.CyclePlan,
            Reference(planContent),
            planContent);
    }

    public static async Task<PaperSupervisorCycleOutcome> ExecuteCycleAsync(
        PaperSupervisorCyclePlan plan,
        IPaperResearchWorker worker,
        CancellationToken cancellationToken = default)
    {
        Validate(plan);
        ArgumentNullException.ThrowIfNull(worker);
        Task<PaperSupervisorWorkerResult>[] tasks = plan.PlanContent.WorkItems
            .Select(workItem => ExecuteOneAsync(plan, workItem, worker, cancellationToken))
            .ToArray();
        PaperSupervisorWorkerResult[] completed = await Task.WhenAll(tasks)
            .ConfigureAwait(false);
        PaperSupervisorWorkerResult[] normalized = completed
            .OrderBy(result => result.ResultContent.PaperId, StringComparer.Ordinal)
            .ToArray();
        DateTimeOffset completedAt = normalized
            .Select(result => ParseUtc(result.ResultContent.CompletedAt, "completed_at"))
            .Max();
        var content = new PaperSupervisorCycleOutcomeContent(
            plan.PlanId,
            plan.PlanContent.PortfolioRef,
            plan.PlanContent.PortfolioCycleRef,
            normalized,
            normalized.Count(result => result.ResultContent.Status == "substantive-progress"),
            normalized.Count(result => result.ResultContent.Status == "no-progress"),
            normalized.Count(result => result.ResultContent.Status == "waiting"),
            normalized.Count(result => result.ResultContent.Status == "blocked"),
            normalized.Count(result => result.ResultContent.Status == "failed"),
            completedAt.ToString("O", CultureInfo.InvariantCulture));
        ValidateOutcomeContent(content, plan);
        return new(
            PaperParallelSupervisorSchemas.CycleOutcome,
            Reference(content),
            content);
    }

    public static PaperResearchPortfolio ApplyOutcome(
        PaperResearchPortfolio portfolio,
        PaperSupervisorCyclePlan plan,
        PaperSupervisorCycleOutcome outcome,
        string updatedAt)
    {
        PaperPortfolioService.Validate(portfolio);
        Validate(plan);
        Validate(outcome, plan);
        ParseUtc(updatedAt, nameof(updatedAt));
        if (!string.Equals(
                plan.PlanContent.PortfolioRef,
                portfolio.PortfolioId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Supervisor plan does not address the supplied portfolio.");
        }
        var resultByPaper = outcome.OutcomeContent.Results.ToDictionary(
            result => result.ResultContent.PaperId,
            StringComparer.Ordinal);
        PaperCandidateState[] states = portfolio.PortfolioContent.CandidateStates
            .Select(state => resultByPaper.TryGetValue(
                    state.PaperId,
                    out PaperSupervisorWorkerResult? result)
                ? ApplyResult(state, result, plan.PlanContent.Policy)
                : state)
            .OrderBy(state => state.PaperId, StringComparer.Ordinal)
            .ToArray();
        PaperResearchPortfolioContent content = portfolio.PortfolioContent with
        {
            NextCycleNumber = portfolio.PortfolioContent.NextCycleNumber + 1,
            CandidateStates = states,
            UpdatedAt = updatedAt
        };
        return new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            Reference(content),
            content);
    }

    public static PaperPortfolioRefillRequest CreateRefillRequest(
        PaperResearchPortfolio portfolio,
        string requestedAt)
    {
        PaperPortfolioService.Validate(portfolio);
        ParseUtc(requestedAt, nameof(requestedAt));
        int active = portfolio.PortfolioContent.CandidateStates.Count(state =>
            state.Phase is not "parked" and not "archived" and not "done");
        int lowWatermark = portfolio.PortfolioContent.Policy.RefillLowWatermark;
        if (active > lowWatermark)
        {
            throw new InvalidDataException(
                "Portfolio remains above its refill low-watermark.");
        }
        int requested = portfolio.PortfolioContent.Policy.BatchCapacity - active;
        if (requested < 1)
        {
            throw new InvalidDataException(
                "Refill request has no available candidate capacity.");
        }
        var content = new PaperPortfolioRefillRequestContent(
            portfolio.PortfolioId,
            portfolio.PortfolioContent.CandidateBatchRef,
            portfolio.PortfolioContent.TruthReleaseDigest,
            portfolio.PortfolioContent.TopologyDigest,
            portfolio.PortfolioContent.PaperResearchInputRef,
            requested,
            $"active paper count {active} reached refill low-watermark {lowWatermark}",
            requestedAt);
        ValidateRefillContent(content);
        return new(
            PaperParallelSupervisorSchemas.RefillRequest,
            Reference(content),
            content);
    }

    public static void Validate(PaperSupervisorCyclePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RequireExact(plan.Schema, PaperParallelSupervisorSchemas.CyclePlan, "schema");
        ValidatePlanContent(plan.PlanContent, null);
        RequireIdentity(plan.PlanId, plan.PlanContent, nameof(plan.PlanId));
    }

    public static void Validate(PaperSupervisorWorkerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        RequireExact(result.Schema, PaperParallelSupervisorSchemas.WorkerResult, "schema");
        ValidateWorkerResultContent(result.ResultContent, null);
        RequireIdentity(result.ResultId, result.ResultContent, nameof(result.ResultId));
    }

    public static void Validate(PaperSupervisorCycleOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        RequireExact(outcome.Schema, PaperParallelSupervisorSchemas.CycleOutcome, "schema");
        ValidateOutcomeContent(outcome.OutcomeContent, null);
        RequireIdentity(outcome.OutcomeId, outcome.OutcomeContent, nameof(outcome.OutcomeId));
    }

    public static void Validate(
        PaperSupervisorCycleOutcome outcome,
        PaperSupervisorCyclePlan plan)
    {
        Validate(outcome);
        ValidateOutcomeContent(outcome.OutcomeContent, plan);
    }

    public static void Validate(PaperPortfolioRefillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireExact(request.Schema, PaperParallelSupervisorSchemas.RefillRequest, "schema");
        ValidateRefillContent(request.RequestContent);
        RequireIdentity(request.RequestId, request.RequestContent, nameof(request.RequestId));
    }

    public static PaperSupervisorWorkerResult CreateWorkerResult(
        PaperSupervisorWorkItem workItem,
        string status,
        string nextPhase,
        IReadOnlyList<PaperSupervisorArtifact> artifacts,
        string stateTransitionRef,
        string detail,
        string startedAt,
        string completedAt)
    {
        Validate(workItem);
        var content = new PaperSupervisorWorkerResultContent(
            workItem.WorkItemId,
            workItem.WorkItemContent.PaperId,
            workItem.WorkItemContent.TheoryProgramRef,
            status,
            nextPhase,
            artifacts,
            stateTransitionRef,
            detail,
            startedAt,
            completedAt);
        ValidateWorkerResultContent(content, workItem);
        return new(
            PaperParallelSupervisorSchemas.WorkerResult,
            Reference(content),
            content);
    }

    public static void Validate(PaperSupervisorWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ValidateWorkItemContent(workItem.WorkItemContent);
        RequireIdentity(
            workItem.WorkItemId,
            workItem.WorkItemContent,
            nameof(workItem.WorkItemId));
    }

    private static async Task<PaperSupervisorWorkerResult> ExecuteOneAsync(
        PaperSupervisorCyclePlan plan,
        PaperSupervisorWorkItem workItem,
        IPaperResearchWorker worker,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(plan.PlanContent.Policy.WorkerTimeoutSeconds));
        PaperSupervisorWorkerResult result = await worker.ExecuteAsync(
            workItem,
            timeout.Token).ConfigureAwait(false);
        ValidateWorkerResultContent(result.ResultContent, workItem);
        RequireIdentity(result.ResultId, result.ResultContent, nameof(result.ResultId));
        return result;
    }

    private static PaperCandidateState ApplyResult(
        PaperCandidateState state,
        PaperSupervisorWorkerResult result,
        PaperSupervisorPolicy policy)
    {
        PaperSupervisorWorkerResultContent r = result.ResultContent;
        if (!string.Equals(state.PaperId, r.PaperId, StringComparison.Ordinal)
            || !string.Equals(state.TheoryProgramRef, r.TheoryProgramRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Worker result does not address the portfolio candidate state.");
        }
        if (string.Equals(r.Status, "waiting", StringComparison.Ordinal))
        {
            return state with
            {
                StatusReason = $"waiting: {r.Detail}"
            };
        }
        if (string.Equals(r.Status, "substantive-progress", StringComparison.Ordinal))
        {
            return state with
            {
                Phase = r.NextPhase,
                CompletedCycles = state.CompletedCycles + 1,
                ConsecutiveNoProgressCycles = 0,
                LastProgressAt = r.CompletedAt,
                StatusReason = $"substantive progress via {r.StateTransitionRef}: {r.Detail}"
            };
        }

        int noProgress = state.ConsecutiveNoProgressCycles + 1;
        string phase = noProgress >= policy.NoProgressParkThreshold
            ? "parked"
            : state.Phase;
        string escalation = noProgress >= policy.NoProgressEscalationThreshold
            ? "; independent escalation required"
            : string.Empty;
        return state with
        {
            Phase = phase,
            CompletedCycles = state.CompletedCycles + 1,
            ConsecutiveNoProgressCycles = noProgress,
            StatusReason = $"{r.Status}: {r.Detail}{escalation}"
        };
    }

    private static PaperCodexPhaseContract BuildPhaseContract(
        string phase,
        PaperSupervisorPaperContext context)
    {
        string[] exactInputs = RequiredInputs(phase, context);
        string[] outputs = RequiredOutputs(phase);
        return new PaperCodexPhaseContract(
            exactInputs,
            outputs,
            ScientificTasks(phase),
            ForbiddenShortcuts(phase),
            outputs,
            PassConditions(phase),
            FailConditions(phase));
    }

    private static string[] RequiredInputs(
        string phase,
        PaperSupervisorPaperContext context)
    {
        var refs = new List<string> { context.TheoryProgramRef };
        void AddRequired(string value, string name)
        {
            RequireDigest(value, name);
            refs.Add(value);
        }
        switch (phase)
        {
            case "scope-pending":
                break;
            case "inventory-pending":
                AddRequired(context.ScopeRef, "scope_ref");
                break;
            case "theory-deepening":
                AddRequired(context.ScopeRef, "scope_ref");
                AddRequired(context.InventoryRef, "inventory_ref");
                if (!string.IsNullOrEmpty(context.LatestTheoremPackageRef))
                {
                    AddRequired(context.LatestTheoremPackageRef, "latest_theorem_package_ref");
                }
                break;
            case "audit-pending":
                AddRequired(context.ScopeRef, "scope_ref");
                AddRequired(context.InventoryRef, "inventory_ref");
                AddRequired(context.LatestTheoremPackageRef, "latest_theorem_package_ref");
                break;
            case "frontier-pending":
                AddRequired(context.LatestTheoremPackageRef, "latest_theorem_package_ref");
                AddRequired(context.TheoryAuditRef, "theory_audit_ref");
                AddRequired(context.ScorecardRef, "scorecard_ref");
                AddRequired(context.PortfolioDecisionRef, "portfolio_decision_ref");
                break;
            case "formalizing":
            case "certification-pending":
                AddRequired(context.FormalizationFrontierRef, "formalization_frontier_ref");
                AddRequired(context.FormalizationFrontierStateRef, "formalization_frontier_state_ref");
                break;
            case "manuscript-pending":
                AddRequired(context.FormalizationFrontierRef, "formalization_frontier_ref");
                AddRequired(context.FormalizationFrontierStateRef, "formalization_frontier_state_ref");
                AddRequired(context.CertifiedClaimManifestRef, "certified_claim_manifest_ref");
                break;
            default:
                throw new InvalidDataException(
                    $"Supervisor cannot build work for candidate phase {phase}.");
        }
        return refs.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] RequiredOutputs(string phase) =>
        phase switch
        {
            "scope-pending" =>
            [PaperTheoryFoundationSchemas.ScopeRequest, PaperTheoryFoundationSchemas.Scope],
            "inventory-pending" =>
            [PaperTheoryFoundationSchemas.InventoryRequest, PaperTheoryFoundationSchemas.Inventory],
            "theory-deepening" =>
            [
                PaperTheoryDeepeningSchemas.DeepeningRequest,
                PaperTheoryDeepeningSchemas.TheoryIteration,
                PaperTheoryDeepeningSchemas.TheoremPackage,
                PaperTheoryDeepeningSchemas.SplitProposal,
                PaperTheoryDeepeningSchemas.MergeProposal,
                PaperTheoryDeepeningSchemas.ResearchLedgerEntry
            ],
            "audit-pending" =>
            [
                PaperTheoryAuditSchemas.AuditRequest,
                PaperTheoryAuditSchemas.Audit,
                PaperPortfolioDecisionSchemas.Scorecard,
                PaperPortfolioDecisionSchemas.Decision
            ],
            "frontier-pending" =>
            [PaperFormalizationFrontierSchemas.Frontier, PaperFormalizationFrontierSchemas.FrontierState],
            "formalizing" =>
            [PaperFormalizationFrontierSchemas.FrontierEvent, PaperFormalizationFrontierSchemas.FrontierState],
            "certification-pending" =>
            [PaperFormalizationFrontierSchemas.FrontierEvent, PaperFormalizationFrontierSchemas.FrontierState],
            "manuscript-pending" =>
            ["paper-manuscript-package.v1"],
            _ => throw new InvalidDataException($"Unsupported supervisor phase {phase}.")
        };

    private static string AgentRole(string phase) =>
        phase switch
        {
            "scope-pending" => "paper-theory-scope-author",
            "inventory-pending" => "paper-theory-inventory-auditor",
            "theory-deepening" => "paper-theory-developer",
            "audit-pending" => "paper-theory-independent-referee",
            "frontier-pending" => "paper-formalization-frontier-planner",
            "formalizing" => "paper-formalization-coordinator",
            "certification-pending" => "paper-certification-coordinator",
            "manuscript-pending" => "paper-manuscript-assembler",
            _ => throw new InvalidDataException($"Unsupported supervisor phase {phase}.")
        };

    private static string ContextMode(string phase) =>
        phase switch
        {
            "scope-pending" => "exact-program-scope",
            "inventory-pending" => "scope-bound-review",
            "theory-deepening" => "contextual-theory-execution",
            "audit-pending" => PaperTheoryAuditService.FreshContextMode,
            "frontier-pending" => "promotion-bound-planning",
            "formalizing" => "dependency-bound-formalization",
            "certification-pending" => "exact-release-certification",
            "manuscript-pending" => "certified-claims-only",
            _ => throw new InvalidDataException($"Unsupported supervisor phase {phase}.")
        };

    private static string[] ScientificTasks(string phase) =>
        phase switch
        {
            "scope-pending" =>
            ["Create the exact A0 scope request and scope artifact for this paper program."],
            "inventory-pending" =>
            ["Create the A1 multi-theorem inventory and dependency DAG without changing the theory."],
            "theory-deepening" =>
            ["Run one bounded A2 abstract-theory iteration and return substantive progress evidence plus the updated theorem package."],
            "audit-pending" =>
            ["Obtain fresh independent theory opinions, create the conservative audit and scorecard, and participate in the batch portfolio decision."],
            "frontier-pending" =>
            ["Translate the promoted theorem package into a complete dependency-aware formalization frontier and initial state."],
            "formalizing" =>
            ["Advance every currently legal frontier node through the governed Formalize lifecycle while respecting certified dependencies."],
            "certification-pending" =>
            ["Join produced candidates to the exact truth release and update frontier certification events."],
            "manuscript-pending" =>
            ["Assemble the paper only from certified or manifested frontier claims and their exact references."],
            _ => throw new InvalidDataException($"Unsupported supervisor phase {phase}.")
        };

    private static string[] ForbiddenShortcuts(string phase)
    {
        var common = new List<string>
        {
            "Do not change the leased paper ID, theory program, or exact input references.",
            "Do not edit another paper program from this worker slot.",
            "Do not claim progress without the required content-addressed output artifacts.",
            "Do not bypass phase gates or synthesize downstream certification evidence."
        };
        if (phase is "scope-pending" or "inventory-pending" or "theory-deepening" or "audit-pending")
        {
            common.Add("Do not run Lean, dispatch Formalize, or assemble manuscript prose in this theory phase.");
        }
        if (phase == "audit-pending")
        {
            common.Add("Do not reuse the theory-author run, a prior verdict, or another review session.");
        }
        if (phase == "formalizing")
        {
            common.Add("Do not issue a canonical request before every dependency node is certified or manifested.");
        }
        if (phase == "manuscript-pending")
        {
            common.Add("Do not include uncertified claims, stale releases, or informal theorem variants in the manuscript package.");
        }
        return common.ToArray();
    }

    private static string[] PassConditions(string phase) =>
        phase switch
        {
            "scope-pending" => ["A valid A0 scope advances the paper to inventory-pending."],
            "inventory-pending" => ["A valid multi-theorem A1 inventory advances the paper to theory-deepening."],
            "theory-deepening" => ["The iteration passes anti-fake progress gates and returns a developing or audit-candidate theorem package."],
            "audit-pending" => ["Fresh independent opinions produce a valid audit, scorecard, and batch-level portfolio route."],
            "frontier-pending" => ["Every theorem-package claim has one valid frontier node and the exact paper was promoted."],
            "formalizing" => ["At least one legal node lifecycle event is applied without violating dependency order."],
            "certification-pending" => ["At least one produced candidate joins the exact truth release."],
            "manuscript-pending" => ["The manuscript package references only certified or manifested claims from the exact frontier."],
            _ => throw new InvalidDataException($"Unsupported supervisor phase {phase}.")
        };

    private static string[] FailConditions(string phase) =>
        phase switch
        {
            "scope-pending" => ["The scope weakens the research question or omits theorem and counterexample obligations."],
            "inventory-pending" => ["The output is a single isolated theorem or has unresolved or cyclic dependencies."],
            "theory-deepening" => ["Only wording, notation, ordering, or an isolated easy lemma changes."],
            "audit-pending" => ["Opinions are not independent and fresh, or any hard metric remains below threshold."],
            "frontier-pending" => ["The paper was not portfolio-promoted or any theorem-package claim is omitted."],
            "formalizing" => ["A dependency is bypassed or outcome evidence is invented."],
            "certification-pending" => ["Certification refers to a different truth release or a non-produced candidate."],
            "manuscript-pending" => ["Any uncertified, cross-release, or unresolved claim enters the manuscript."],
            _ => throw new InvalidDataException($"Unsupported supervisor phase {phase}.")
        };

    private static string BuildPrompt(
        PaperResearchLease lease,
        PaperCodexPhaseContract contract)
    {
        string inputs = string.Join("\n", contract.ExactInputRefs.Select(value => $"- {value}"));
        string tasks = string.Join("\n", contract.ScientificTasks.Select(value => $"- {value}"));
        string forbidden = string.Join("\n", contract.ForbiddenShortcuts.Select(value => $"- {value}"));
        string outputs = string.Join("\n", contract.RequiredOutputSchemas.Select(value => $"- {value}"));
        string passes = string.Join("\n", contract.PassConditions.Select(value => $"- {value}"));
        string fails = string.Join("\n", contract.FailConditions.Select(value => $"- {value}"));
        return $"""
            Paper portfolio worker lease
            paper_id: {lease.PaperId}
            theory_program_ref: {lease.TheoryProgramRef}
            phase: {lease.Phase}
            worker_slot: {lease.WorkerSlot}

            Exact inputs:
            {inputs}

            Scientific task:
            {tasks}

            Forbidden shortcuts:
            {forbidden}

            Required output schemas:
            {outputs}

            Pass conditions:
            {passes}

            Fail conditions:
            {fails}

            Work only on this paper and return machine-readable artifact references.
            """;
    }

    private static void ValidatePlanContent(
        PaperSupervisorCyclePlanContent content,
        PaperPortfolioCycle? portfolioCycle)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.PortfolioRef, "portfolio_ref");
        RequireDigest(content.PortfolioCycleRef, "portfolio_cycle_ref");
        ValidatePolicy(content.Policy, 32);
        RequireExact(content.ExecutionMode, ParallelExecutionMode, "execution_mode");
        if (content.WorkItems is null
            || content.WorkItems.Count < 1
            || content.WorkItems.Count > content.Policy.MaximumParallelPapers)
        {
            throw new InvalidDataException(
                "Supervisor plan work-item count is outside its parallel capacity.");
        }
        var papers = new HashSet<string>(StringComparer.Ordinal);
        var programs = new HashSet<string>(StringComparer.Ordinal);
        var leases = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < content.WorkItems.Count; index++)
        {
            PaperSupervisorWorkItem workItem = content.WorkItems[index];
            Validate(workItem);
            PaperSupervisorWorkItemContent w = workItem.WorkItemContent;
            if (w.WorkerSlot != index + 1
                || !papers.Add(w.PaperId)
                || !programs.Add(w.TheoryProgramRef)
                || !leases.Add(w.LeaseRef)
                || !string.Equals(w.PortfolioRef, content.PortfolioRef, StringComparison.Ordinal)
                || !string.Equals(w.PortfolioCycleRef, content.PortfolioCycleRef, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Supervisor plan must contain one distinct paper, program, and lease per worker slot.");
            }
        }
        if (portfolioCycle is not null)
        {
            PaperPortfolioService.Validate(portfolioCycle);
            if (!string.Equals(content.PortfolioCycleRef, portfolioCycle.CycleId, StringComparison.Ordinal)
                || content.WorkItems.Count != portfolioCycle.CycleContent.Leases.Count)
            {
                throw new InvalidDataException(
                    "Supervisor plan does not cover the exact portfolio cycle leases.");
            }
        }
        ParseUtc(content.PlannedAt, "planned_at");
    }

    private static void ValidateWorkItemContent(PaperSupervisorWorkItemContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.PortfolioRef, "portfolio_ref");
        RequireDigest(content.PortfolioCycleRef, "portfolio_cycle_ref");
        RequireDigest(content.LeaseRef, "lease_ref");
        if (content.WorkerSlot < 1)
        {
            throw new InvalidDataException("worker_slot must be positive.");
        }
        RequireText(content.PaperId, "paper_id", 512);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        if (!CandidatePhases.Contains(content.Phase)
            || content.Phase is "parked" or "archived" or "done")
        {
            throw new InvalidDataException(
                "Supervisor work item phase must be runnable.");
        }
        RequireText(content.AgentRole, "agent_role", 512);
        RequireText(content.ContextMode, "context_mode", 512);
        RequireText(content.WorkingDirectory, "working_directory", 4096);
        ValidateContract(content.Contract);
        RequireText(content.Prompt, "prompt", 131072);
        ParseUtc(content.RequestedAt, "requested_at");
        if (!string.Equals(content.AgentRole, AgentRole(content.Phase), StringComparison.Ordinal)
            || !string.Equals(content.ContextMode, ContextMode(content.Phase), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Supervisor work item role or context mode is inconsistent with its phase.");
        }
    }

    private static void ValidateWorkerResultContent(
        PaperSupervisorWorkerResultContent content,
        PaperSupervisorWorkItem? workItem)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.WorkItemRef, "work_item_ref");
        RequireText(content.PaperId, "paper_id", 512);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        if (!WorkerStatuses.Contains(content.Status)
            || !CandidatePhases.Contains(content.NextPhase))
        {
            throw new InvalidDataException(
                "Worker result status or next phase is unsupported.");
        }
        if (content.Artifacts is null)
        {
            throw new InvalidDataException("Worker result artifacts are required.");
        }
        var artifactRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperSupervisorArtifact artifact in content.Artifacts)
        {
            RequireText(artifact.Schema, "artifact_schema", 512);
            RequireDigest(artifact.ArtifactRef, "artifact_ref");
            if (!artifactRefs.Add(artifact.ArtifactRef))
            {
                throw new InvalidDataException(
                    "Worker result artifact references must be unique.");
            }
        }
        RequireText(content.Detail, "detail", 16384);
        DateTimeOffset started = ParseUtc(content.StartedAt, "started_at");
        DateTimeOffset completed = ParseUtc(content.CompletedAt, "completed_at");
        if (completed < started)
        {
            throw new InvalidDataException(
                "Worker result completed_at cannot precede started_at.");
        }
        if (string.Equals(content.Status, "substantive-progress", StringComparison.Ordinal))
        {
            if (content.Artifacts.Count < 1)
            {
                throw new InvalidDataException(
                    "Substantive progress requires at least one output artifact.");
            }
            RequireDigest(content.StateTransitionRef, "state_transition_ref");
        }
        else
        {
            RequireOptionalDigest(content.StateTransitionRef, "state_transition_ref");
            if (content.Artifacts.Count != 0)
            {
                throw new InvalidDataException(
                    "Non-progress worker results cannot claim output artifacts.");
            }
        }
        if (workItem is not null)
        {
            Validate(workItem);
            if (!string.Equals(content.WorkItemRef, workItem.WorkItemId, StringComparison.Ordinal)
                || !string.Equals(content.PaperId, workItem.WorkItemContent.PaperId, StringComparison.Ordinal)
                || !string.Equals(content.TheoryProgramRef, workItem.WorkItemContent.TheoryProgramRef, StringComparison.Ordinal)
                || !TransitionAllowed(
                    workItem.WorkItemContent.Phase,
                    content.NextPhase,
                    content.Status))
            {
                throw new InvalidDataException(
                    "Worker result changed its work item, paper, program, or phase transition.");
            }
            var allowedSchemas = workItem.WorkItemContent.Contract.RequiredOutputSchemas
                .ToHashSet(StringComparer.Ordinal);
            if (content.Artifacts.Any(artifact => !allowedSchemas.Contains(artifact.Schema)))
            {
                throw new InvalidDataException(
                    "Worker result contains an artifact schema outside its phase contract.");
            }
        }
    }

    private static bool TransitionAllowed(
        string current,
        string next,
        string status)
    {
        if (!string.Equals(status, "substantive-progress", StringComparison.Ordinal))
        {
            return string.Equals(current, next, StringComparison.Ordinal);
        }
        return current switch
        {
            "scope-pending" => next == "inventory-pending",
            "inventory-pending" => next == "theory-deepening",
            "theory-deepening" => next is "theory-deepening" or "audit-pending",
            "audit-pending" => next is "audit-pending" or "frontier-pending"
                or "theory-deepening" or "parked" or "archived",
            "frontier-pending" => next == "formalizing",
            "formalizing" => next is "formalizing" or "certification-pending"
                or "theory-deepening" or "audit-pending" or "manuscript-pending",
            "certification-pending" => next is "certification-pending"
                or "formalizing" or "manuscript-pending",
            "manuscript-pending" => next == "done",
            _ => false
        };
    }

    private static void ValidateOutcomeContent(
        PaperSupervisorCycleOutcomeContent content,
        PaperSupervisorCyclePlan? plan)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.PlanRef, "plan_ref");
        RequireDigest(content.PortfolioRef, "portfolio_ref");
        RequireDigest(content.PortfolioCycleRef, "portfolio_cycle_ref");
        if (content.Results is null || content.Results.Count < 1)
        {
            throw new InvalidDataException("Supervisor cycle outcome requires results.");
        }
        var papers = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperSupervisorWorkerResult result in content.Results)
        {
            Validate(result);
            if (!papers.Add(result.ResultContent.PaperId))
            {
                throw new InvalidDataException(
                    "Supervisor cycle outcome may contain at most one result per paper.");
            }
        }
        int substantive = content.Results.Count(result => result.ResultContent.Status == "substantive-progress");
        int noProgress = content.Results.Count(result => result.ResultContent.Status == "no-progress");
        int waiting = content.Results.Count(result => result.ResultContent.Status == "waiting");
        int blocked = content.Results.Count(result => result.ResultContent.Status == "blocked");
        int failed = content.Results.Count(result => result.ResultContent.Status == "failed");
        if (content.SubstantiveProgressCount != substantive
            || content.NoProgressCount != noProgress
            || content.WaitingCount != waiting
            || content.BlockedCount != blocked
            || content.FailedCount != failed)
        {
            throw new InvalidDataException(
                "Supervisor cycle outcome counters do not match worker results.");
        }
        if (plan is not null)
        {
            if (!string.Equals(content.PlanRef, plan.PlanId, StringComparison.Ordinal)
                || !string.Equals(content.PortfolioRef, plan.PlanContent.PortfolioRef, StringComparison.Ordinal)
                || !string.Equals(content.PortfolioCycleRef, plan.PlanContent.PortfolioCycleRef, StringComparison.Ordinal)
                || content.Results.Count != plan.PlanContent.WorkItems.Count)
            {
                throw new InvalidDataException(
                    "Supervisor cycle outcome does not cover the supplied plan.");
            }
            var byWorkItem = plan.PlanContent.WorkItems.ToDictionary(
                workItem => workItem.WorkItemId,
                StringComparer.Ordinal);
            foreach (PaperSupervisorWorkerResult result in content.Results)
            {
                if (!byWorkItem.TryGetValue(
                        result.ResultContent.WorkItemRef,
                        out PaperSupervisorWorkItem? workItem))
                {
                    throw new InvalidDataException(
                        "Supervisor result does not match any planned work item.");
                }
                ValidateWorkerResultContent(result.ResultContent, workItem);
            }
        }
        ParseUtc(content.CompletedAt, "completed_at");
    }

    private static void ValidateRefillContent(PaperPortfolioRefillRequestContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.PortfolioRef, "portfolio_ref");
        RequireDigest(content.CandidateBatchRef, "candidate_batch_ref");
        RequireDigest(content.TruthReleaseDigest, "truth_release_digest");
        RequireDigest(content.TopologyDigest, "topology_digest");
        RequireDigest(content.PaperResearchInputRef, "paper_research_input_ref");
        if (content.RequestedCandidateCount is < 1 or > 32)
        {
            throw new InvalidDataException(
                "requested_candidate_count must be between one and thirty-two.");
        }
        RequireText(content.Reason, "reason", 8192);
        ParseUtc(content.RequestedAt, "requested_at");
    }

    private static void ValidateContext(PaperSupervisorPaperContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RequireText(context.PaperId, "paper_id", 512);
        RequireDigest(context.TheoryProgramRef, "theory_program_ref");
        RequireText(context.WorkingDirectory, "working_directory", 4096);
        RequireOptionalDigest(context.ScopeRef, "scope_ref");
        RequireOptionalDigest(context.InventoryRef, "inventory_ref");
        RequireOptionalDigest(context.LatestTheoremPackageRef, "latest_theorem_package_ref");
        RequireOptionalDigest(context.TheoryAuditRef, "theory_audit_ref");
        RequireOptionalDigest(context.ScorecardRef, "scorecard_ref");
        RequireOptionalDigest(context.PortfolioDecisionRef, "portfolio_decision_ref");
        RequireOptionalDigest(context.FormalizationFrontierRef, "formalization_frontier_ref");
        RequireOptionalDigest(context.FormalizationFrontierStateRef, "formalization_frontier_state_ref");
        RequireOptionalDigest(context.CertifiedClaimManifestRef, "certified_claim_manifest_ref");
    }

    private static void ValidatePolicy(
        PaperSupervisorPolicy policy,
        int maximumPortfolioParallelism)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.MaximumParallelPapers < 2
            || policy.MaximumParallelPapers > maximumPortfolioParallelism
            || policy.WorkerTimeoutSeconds is < 30 or > 86400
            || policy.NoProgressEscalationThreshold < 1
            || policy.NoProgressParkThreshold <= policy.NoProgressEscalationThreshold)
        {
            throw new InvalidDataException(
                "Supervisor policy is outside its bounded ranges.");
        }
    }

    private static void ValidateContract(PaperCodexPhaseContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        RequireDigestList(contract.ExactInputRefs, "exact_input_refs", 1);
        RequireTextList(contract.PermittedArtifactFamilies, "permitted_artifact_families", 512, 1);
        RequireTextList(contract.ScientificTasks, "scientific_tasks", 8192, 1);
        RequireTextList(contract.ForbiddenShortcuts, "forbidden_shortcuts", 8192, 1);
        RequireTextList(contract.RequiredOutputSchemas, "required_output_schemas", 512, 1);
        RequireTextList(contract.PassConditions, "pass_conditions", 8192, 1);
        RequireTextList(contract.FailConditions, "fail_conditions", 8192, 1);
    }

    private static void RequireOptionalDigest(string value, string name)
    {
        if (!string.IsNullOrEmpty(value))
        {
            RequireDigest(value, name);
        }
    }

    private static void RequireDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireDigest(value, name);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximumLength,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, maximumLength);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireText(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximumLength} characters.");
        }
    }

    private static string Reference<T>(T content) =>
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content));

    private static void RequireIdentity<T>(string reference, T content, string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, Reference(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} does not address canonical content bytes.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static DateTimeOffset ParseUtc(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }
}
