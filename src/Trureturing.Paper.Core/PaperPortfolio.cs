using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperPortfolioSchemas
{
    public const string CandidateBatch = "paper-candidate-batch.v1";
    public const string TheoryProgram = "paper-theory-program.v1";
    public const string CandidateState = "paper-candidate-state.v1";
    public const string Portfolio = "paper-research-portfolio.v1";
    public const string PortfolioCycle = "paper-portfolio-cycle.v1";
}

public sealed record PaperPortfolioPolicy(
    [property: JsonRequired] int BatchCapacity,
    [property: JsonRequired] int MaxParallelPapers,
    [property: JsonRequired] int PerPaperLeaseLimit,
    [property: JsonRequired] int RefillLowWatermark);

public sealed record PaperCandidateSeed(
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string IntuitionProposalRef,
    [property: JsonRequired] int InitialPriority,
    [property: JsonRequired] string RegisteredAt);

public sealed record PaperCandidateBatchContent(
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyDigest,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] PaperPortfolioPolicy Policy,
    [property: JsonRequired] IReadOnlyList<PaperCandidateSeed> Candidates);

public sealed record PaperCandidateBatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string BatchId,
    [property: JsonRequired] PaperCandidateBatchContent BatchContent);

public sealed record PaperTheoryProgramContent(
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyDigest,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string IntuitionProposalRef,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryProgram(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TheoryProgramId,
    [property: JsonRequired] PaperTheoryProgramContent ProgramContent);

public sealed record PaperCandidateState(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] int Priority,
    [property: JsonRequired] int CompletedCycles,
    [property: JsonRequired] int ConsecutiveNoProgressCycles,
    [property: JsonRequired] string LastProgressAt,
    [property: JsonRequired] string StatusReason);

public sealed record PaperResearchPortfolioContent(
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyDigest,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] PaperPortfolioPolicy Policy,
    [property: JsonRequired] int NextCycleNumber,
    [property: JsonRequired] IReadOnlyList<PaperCandidateState> CandidateStates,
    [property: JsonRequired] string UpdatedAt);

public sealed record PaperResearchPortfolio(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PortfolioId,
    [property: JsonRequired] PaperResearchPortfolioContent PortfolioContent);

public sealed record PaperResearchLease(
    [property: JsonRequired] string LeaseId,
    [property: JsonRequired] int WorkerSlot,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] int SchedulingScore,
    [property: JsonRequired] string LeasedAt);

public sealed record PaperPortfolioCycleContent(
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] string ExecutionMode,
    [property: JsonRequired] int RunnablePaperCount,
    [property: JsonRequired] int GrantedParallelism,
    [property: JsonRequired] IReadOnlyList<PaperResearchLease> Leases,
    [property: JsonRequired] string PlannedAt);

public sealed record PaperPortfolioCycle(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CycleId,
    [property: JsonRequired] PaperPortfolioCycleContent CycleContent);

internal sealed record PaperResearchLeaseIdentity(
    string PortfolioRef,
    int CycleNumber,
    string PaperId,
    string TheoryProgramRef,
    string Phase,
    int SchedulingScore,
    string LeasedAt);

public static class PaperPortfolioService
{
    public const string ParallelExecutionMode = "parallel-paper-batch";

    private static readonly HashSet<string> RunnablePhases = new(
        ["scope-pending", "inventory-pending", "theory-deepening", "audit-pending",
         "frontier-pending", "formalizing", "certification-pending", "manuscript-pending"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> TerminalPhases =
        new(["parked", "archived", "done"], StringComparer.Ordinal);
    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    public static PaperCandidateBatch CreateBatch(PaperCandidateBatchContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        PaperCandidateSeed[] candidates = content.Candidates?.OrderBy(
            candidate => candidate.PaperId, StringComparer.Ordinal).ToArray()
            ?? throw new InvalidDataException("candidates must be an array.");
        PaperCandidateBatchContent normalized = content with { Candidates = candidates };
        ValidateBatchContent(normalized);
        return new(PaperPortfolioSchemas.CandidateBatch, Reference(normalized), normalized);
    }

    public static PaperTheoryProgram CreateTheoryProgram(
        PaperCandidateBatch batch, string paperId, string createdAt)
    {
        Validate(batch);
        RequireText(paperId, nameof(paperId), 512);
        ParseUtc(createdAt, nameof(createdAt));
        PaperCandidateSeed seed = batch.BatchContent.Candidates.SingleOrDefault(
            candidate => string.Equals(candidate.PaperId, paperId, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Candidate batch does not contain paper {paperId}.");
        var content = new PaperTheoryProgramContent(
            batch.BatchId,
            batch.BatchContent.TruthReleaseDigest,
            batch.BatchContent.TopologyDigest,
            batch.BatchContent.PaperResearchInputRef,
            seed.PaperId,
            seed.CandidatePaperRef,
            seed.LiteratureResearchRef,
            seed.IntuitionProposalRef,
            createdAt);
        ValidateProgramContent(content, batch);
        return new(PaperPortfolioSchemas.TheoryProgram, Reference(content), content);
    }

    public static PaperResearchPortfolio CreatePortfolio(
        PaperCandidateBatch batch,
        IReadOnlyList<PaperTheoryProgram> programs,
        string updatedAt)
    {
        Validate(batch);
        ArgumentNullException.ThrowIfNull(programs);
        ParseUtc(updatedAt, nameof(updatedAt));
        var byPaper = new Dictionary<string, PaperTheoryProgram>(StringComparer.Ordinal);
        foreach (PaperTheoryProgram program in programs)
        {
            Validate(program);
            ValidateProgramContent(program.ProgramContent, batch);
            if (!byPaper.TryAdd(program.ProgramContent.PaperId, program))
            {
                throw new InvalidDataException(
                    "A portfolio cannot contain two theory programs for one paper.");
            }
        }
        if (byPaper.Count != batch.BatchContent.Candidates.Count)
        {
            throw new InvalidDataException(
                "A portfolio must contain exactly one theory program for every batch paper.");
        }

        PaperCandidateState[] states = batch.BatchContent.Candidates.Select(seed =>
            new PaperCandidateState(
                PaperPortfolioSchemas.CandidateState,
                seed.PaperId,
                byPaper[seed.PaperId].TheoryProgramId,
                "scope-pending",
                seed.InitialPriority,
                0,
                0,
                updatedAt,
                "registered in candidate batch"))
            .OrderBy(state => state.PaperId, StringComparer.Ordinal)
            .ToArray();
        var content = new PaperResearchPortfolioContent(
            batch.BatchId,
            batch.BatchContent.TruthReleaseDigest,
            batch.BatchContent.TopologyDigest,
            batch.BatchContent.PaperResearchInputRef,
            batch.BatchContent.Policy,
            1,
            states,
            updatedAt);
        ValidatePortfolioContent(content);
        return new(PaperPortfolioSchemas.Portfolio, Reference(content), content);
    }

    public static PaperPortfolioCycle PlanCycle(
        PaperResearchPortfolio portfolio,
        IReadOnlyList<PaperTheoryProgram> programs,
        string plannedAt)
    {
        Validate(portfolio);
        ArgumentNullException.ThrowIfNull(programs);
        DateTimeOffset now = ParseUtc(plannedAt, nameof(plannedAt));
        var byReference = new Dictionary<string, PaperTheoryProgram>(StringComparer.Ordinal);
        foreach (PaperTheoryProgram program in programs)
        {
            Validate(program);
            if (!byReference.TryAdd(program.TheoryProgramId, program))
            {
                throw new InvalidDataException("Theory program references must be unique.");
            }
        }

        var runnable = new List<(PaperCandidateState State, int Score)>();
        foreach (PaperCandidateState state in portfolio.PortfolioContent.CandidateStates)
        {
            Validate(state);
            if (TerminalPhases.Contains(state.Phase))
            {
                continue;
            }
            if (!byReference.TryGetValue(state.TheoryProgramRef, out PaperTheoryProgram? program))
            {
                throw new InvalidDataException($"Theory program {state.TheoryProgramRef} is missing.");
            }
            if (!string.Equals(program.ProgramContent.PaperId, state.PaperId, StringComparison.Ordinal)
                || !string.Equals(
                    program.ProgramContent.CandidateBatchRef,
                    portfolio.PortfolioContent.CandidateBatchRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    program.ProgramContent.PaperResearchInputRef,
                    portfolio.PortfolioContent.PaperResearchInputRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Portfolio state and theory program do not describe the same paper batch.");
            }
            DateTimeOffset lastProgress = ParseUtc(state.LastProgressAt, nameof(state.LastProgressAt));
            if (lastProgress > now)
            {
                throw new InvalidDataException(
                    "planned_at cannot precede a candidate's last_progress_at.");
            }
            int ageBoost = Math.Min(30, (int)Math.Floor((now - lastProgress).TotalHours / 6d));
            int noProgressPenalty = Math.Min(50, state.ConsecutiveNoProgressCycles * 10);
            runnable.Add((state, state.Priority + ageBoost - noProgressPenalty));
        }

        int granted = Math.Min(
            portfolio.PortfolioContent.Policy.MaxParallelPapers, runnable.Count);
        PaperResearchLease[] leases = runnable
            .OrderByDescending(item => item.Score)
            .ThenBy(item => ParseUtc(item.State.LastProgressAt, nameof(item.State.LastProgressAt)))
            .ThenBy(item => item.State.PaperId, StringComparer.Ordinal)
            .Take(granted)
            .Select((item, index) =>
            {
                var identity = new PaperResearchLeaseIdentity(
                    portfolio.PortfolioId,
                    portfolio.PortfolioContent.NextCycleNumber,
                    item.State.PaperId,
                    item.State.TheoryProgramRef,
                    item.State.Phase,
                    item.Score,
                    plannedAt);
                return new PaperResearchLease(
                    Reference(identity),
                    index + 1,
                    item.State.PaperId,
                    item.State.TheoryProgramRef,
                    item.State.Phase,
                    item.Score,
                    plannedAt);
            })
            .ToArray();
        var content = new PaperPortfolioCycleContent(
            portfolio.PortfolioId,
            portfolio.PortfolioContent.CandidateBatchRef,
            portfolio.PortfolioContent.NextCycleNumber,
            ParallelExecutionMode,
            runnable.Count,
            leases.Length,
            leases,
            plannedAt);
        ValidateCycleContent(content, portfolio);
        return new(PaperPortfolioSchemas.PortfolioCycle, Reference(content), content);
    }

    public static void Validate(PaperCandidateBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        RequireSchema(batch.Schema, PaperPortfolioSchemas.CandidateBatch);
        ValidateBatchContent(batch.BatchContent);
        RequireIdentity(batch.BatchId, batch.BatchContent, nameof(batch.BatchId));
    }

    public static void Validate(PaperTheoryProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        RequireSchema(program.Schema, PaperPortfolioSchemas.TheoryProgram);
        PaperTheoryProgramContent content = program.ProgramContent
            ?? throw new InvalidDataException("program_content is required.");
        RequireDigest(content.CandidateBatchRef, "candidate_batch_ref");
        RequireDigest(content.TruthReleaseDigest, "truth_release_digest");
        RequireDigest(content.TopologyDigest, "topology_digest");
        RequireDigest(content.PaperResearchInputRef, "paper_research_input_ref");
        RequireText(content.PaperId, "paper_id", 512);
        RequireDigest(content.CandidatePaperRef, "candidate_paper_ref");
        RequireDigest(content.LiteratureResearchRef, "literature_research_ref");
        RequireDigest(content.IntuitionProposalRef, "intuition_proposal_ref");
        ParseUtc(content.CreatedAt, "created_at");
        RequireIdentity(program.TheoryProgramId, content, nameof(program.TheoryProgramId));
    }

    public static void Validate(PaperCandidateState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        RequireSchema(state.Schema, PaperPortfolioSchemas.CandidateState);
        RequireText(state.PaperId, nameof(state.PaperId), 512);
        RequireDigest(state.TheoryProgramRef, nameof(state.TheoryProgramRef));
        if (!RunnablePhases.Contains(state.Phase) && !TerminalPhases.Contains(state.Phase))
        {
            throw new InvalidDataException($"Unsupported candidate phase {state.Phase}.");
        }
        if (state.Priority is < 0 or > 100
            || state.CompletedCycles < 0
            || state.ConsecutiveNoProgressCycles < 0)
        {
            throw new InvalidDataException("Candidate priority or cycle counters are invalid.");
        }
        ParseUtc(state.LastProgressAt, nameof(state.LastProgressAt));
        RequireText(state.StatusReason, nameof(state.StatusReason), 4096);
    }

    public static void Validate(PaperResearchPortfolio portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        RequireSchema(portfolio.Schema, PaperPortfolioSchemas.Portfolio);
        ValidatePortfolioContent(portfolio.PortfolioContent);
        RequireIdentity(
            portfolio.PortfolioId,
            portfolio.PortfolioContent,
            nameof(portfolio.PortfolioId));
    }

    public static void Validate(PaperPortfolioCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        RequireSchema(cycle.Schema, PaperPortfolioSchemas.PortfolioCycle);
        PaperPortfolioCycleContent content = cycle.CycleContent
            ?? throw new InvalidDataException("cycle_content is required.");
        RequireDigest(content.PortfolioRef, "portfolio_ref");
        RequireDigest(content.CandidateBatchRef, "candidate_batch_ref");
        if (content.CycleNumber < 1
            || !string.Equals(content.ExecutionMode, ParallelExecutionMode, StringComparison.Ordinal)
            || content.RunnablePaperCount < 0
            || content.GrantedParallelism < 0
            || content.GrantedParallelism > content.RunnablePaperCount)
        {
            throw new InvalidDataException("Portfolio cycle coordinates are invalid.");
        }
        ValidateLeases(content);
        ParseUtc(content.PlannedAt, "planned_at");
        RequireIdentity(cycle.CycleId, content, nameof(cycle.CycleId));
    }

    private static void ValidateBatchContent(PaperCandidateBatchContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TruthReleaseDigest, "truth_release_digest");
        RequireDigest(content.TopologyDigest, "topology_digest");
        RequireDigest(content.PaperResearchInputRef, "paper_research_input_ref");
        ValidatePolicy(content.Policy);
        if (content.Candidates is null
            || content.Candidates.Count < 2
            || content.Candidates.Count > content.Policy.BatchCapacity)
        {
            throw new InvalidDataException(
                "A paper candidate batch must contain between two papers and batch_capacity papers.");
        }
        var papers = new HashSet<string>(StringComparer.Ordinal);
        var candidateRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperCandidateSeed candidate in content.Candidates)
        {
            RequireText(candidate.PaperId, "paper_id", 512);
            RequireDigest(candidate.CandidatePaperRef, "candidate_paper_ref");
            RequireDigest(candidate.LiteratureResearchRef, "literature_research_ref");
            RequireDigest(candidate.IntuitionProposalRef, "intuition_proposal_ref");
            if (candidate.InitialPriority is < 0 or > 100)
            {
                throw new InvalidDataException("initial_priority must be between 0 and 100.");
            }
            ParseUtc(candidate.RegisteredAt, "registered_at");
            if (!papers.Add(candidate.PaperId))
            {
                throw new InvalidDataException("Candidate batch paper_id values must be unique.");
            }
            if (!candidateRefs.Add(candidate.CandidatePaperRef))
            {
                throw new InvalidDataException(
                    "One candidate-paper artifact cannot occupy two paper slots in a batch.");
            }
        }
    }

    private static void ValidatePolicy(PaperPortfolioPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.BatchCapacity is < 2 or > 32
            || policy.MaxParallelPapers is < 2
            || policy.MaxParallelPapers > policy.BatchCapacity
            || policy.RefillLowWatermark < 0
            || policy.RefillLowWatermark >= policy.BatchCapacity)
        {
            throw new InvalidDataException("Paper portfolio policy is outside its bounded ranges.");
        }
        if (policy.PerPaperLeaseLimit != 1)
        {
            throw new InvalidDataException("per_paper_lease_limit must be exactly one.");
        }
    }

    private static void ValidateProgramContent(
        PaperTheoryProgramContent content, PaperCandidateBatch batch)
    {
        if (!string.Equals(content.CandidateBatchRef, batch.BatchId, StringComparison.Ordinal)
            || !string.Equals(
                content.TruthReleaseDigest,
                batch.BatchContent.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                content.TopologyDigest,
                batch.BatchContent.TopologyDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                content.PaperResearchInputRef,
                batch.BatchContent.PaperResearchInputRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory program and candidate batch do not share one exact research input.");
        }
        PaperCandidateSeed seed = batch.BatchContent.Candidates.SingleOrDefault(
            candidate => string.Equals(
                candidate.PaperId, content.PaperId, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Theory program paper_id is absent from its candidate batch.");
        if (!string.Equals(seed.CandidatePaperRef, content.CandidatePaperRef, StringComparison.Ordinal)
            || !string.Equals(
                seed.LiteratureResearchRef,
                content.LiteratureResearchRef,
                StringComparison.Ordinal)
            || !string.Equals(
                seed.IntuitionProposalRef,
                content.IntuitionProposalRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Theory program changed candidate evidence.");
        }
    }

    private static void ValidatePortfolioContent(PaperResearchPortfolioContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.CandidateBatchRef, "candidate_batch_ref");
        RequireDigest(content.TruthReleaseDigest, "truth_release_digest");
        RequireDigest(content.TopologyDigest, "topology_digest");
        RequireDigest(content.PaperResearchInputRef, "paper_research_input_ref");
        ValidatePolicy(content.Policy);
        if (content.NextCycleNumber < 1
            || content.CandidateStates is null
            || content.CandidateStates.Count < 2
            || content.CandidateStates.Count > content.Policy.BatchCapacity)
        {
            throw new InvalidDataException("Portfolio candidate state count or cycle is invalid.");
        }
        var papers = new HashSet<string>(StringComparer.Ordinal);
        var programs = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperCandidateState state in content.CandidateStates)
        {
            Validate(state);
            if (!papers.Add(state.PaperId) || !programs.Add(state.TheoryProgramRef))
            {
                throw new InvalidDataException(
                    "Portfolio paper and theory-program identities must be unique.");
            }
        }
        ParseUtc(content.UpdatedAt, "updated_at");
    }

    private static void ValidateCycleContent(
        PaperPortfolioCycleContent content, PaperResearchPortfolio portfolio)
    {
        if (!string.Equals(content.PortfolioRef, portfolio.PortfolioId, StringComparison.Ordinal)
            || !string.Equals(
                content.CandidateBatchRef,
                portfolio.PortfolioContent.CandidateBatchRef,
                StringComparison.Ordinal)
            || content.CycleNumber != portfolio.PortfolioContent.NextCycleNumber
            || !string.Equals(content.ExecutionMode, ParallelExecutionMode, StringComparison.Ordinal)
            || content.GrantedParallelism
                > portfolio.PortfolioContent.Policy.MaxParallelPapers)
        {
            throw new InvalidDataException(
                "Portfolio cycle changed its portfolio, batch, or parallelism coordinate.");
        }
        ValidateLeases(content);
        ParseUtc(content.PlannedAt, "planned_at");
    }

    private static void ValidateLeases(PaperPortfolioCycleContent content)
    {
        if (content.Leases is null || content.Leases.Count != content.GrantedParallelism)
        {
            throw new InvalidDataException(
                "granted_parallelism must equal the number of leases.");
        }
        var papers = new HashSet<string>(StringComparer.Ordinal);
        var programs = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < content.Leases.Count; index++)
        {
            PaperResearchLease lease = content.Leases[index];
            RequireDigest(lease.LeaseId, "lease_id");
            RequireText(lease.PaperId, "paper_id", 512);
            RequireDigest(lease.TheoryProgramRef, "theory_program_ref");
            ParseUtc(lease.LeasedAt, "leased_at");
            if (!RunnablePhases.Contains(lease.Phase)
                || lease.WorkerSlot != index + 1
                || !papers.Add(lease.PaperId)
                || !programs.Add(lease.TheoryProgramRef))
            {
                throw new InvalidDataException(
                    "A cycle may grant at most one valid lease per paper and theory program.");
            }
            var identity = new PaperResearchLeaseIdentity(
                content.PortfolioRef,
                content.CycleNumber,
                lease.PaperId,
                lease.TheoryProgramRef,
                lease.Phase,
                lease.SchedulingScore,
                lease.LeasedAt);
            if (!string.Equals(lease.LeaseId, Reference(identity), StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "lease_id does not address its canonical cycle coordinates.");
            }
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

    private static void RequireSchema(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"schema must be {expected}.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
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
