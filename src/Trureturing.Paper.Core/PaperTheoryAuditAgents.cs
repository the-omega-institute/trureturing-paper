using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

internal sealed record PaperTheoryAuditAgentContext(
    PaperTheoryProgram Program,
    PaperTheoryScope Scope,
    PaperTheoryInventory Inventory,
    PaperTheoremPackage TheoremPackage,
    PaperTheoryAuditRequest Request);

internal sealed record PaperTheoryAuditAggregateResult(
    string Status,
    IReadOnlyList<string> MissingTaskRefs,
    PaperTheoryAuditAggregateCursor? Cursor,
    bool Replayed);

public static class PaperTheoryAuditAgentService
{
    public const string WaitingStatus = "waiting";
    public const string ReadyStatus = "ready";

    private const int MaximumControlBytes = 2 * 1024 * 1024;
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PaperIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex SchemaPattern = new(
        "^[a-z][a-z0-9.-]*\\.v[0-9]+$",
        RegexOptions.CultureInvariant);
    private static readonly Regex RelativePathPattern = new(
        "^[A-Za-z0-9._+@=-]+(?:/[A-Za-z0-9._+@=-]+)*$",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ReviewerRoles = new(
        ["mathematical-referee", "novelty-referee", "scope-referee", "formalization-referee"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> Verdicts = new(
        ["pass", "deepen", "split", "merge", "park", "archive"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedEvidenceRoots = new(
        ["artifacts", "Papers", "work", "contracts", "docs", "src", "tools", "tests"],
        StringComparer.Ordinal);

    public static PaperTheoryAuditAgentTasksStaged StageTasks(
        string repositoryRoot,
        string dispatchPath)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        string fullDispatchPath = RequireDispatchPath(root, dispatchPath);
        byte[] dispatchBytes = ReadBoundedFile(
            fullDispatchPath,
            MaximumControlBytes,
            "Theory-audit dispatch");
        string dispatchRef = Reference(dispatchBytes);
        PaperTheoryAuditAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);
        PaperTheoryAuditAgentContext context = LoadContext(root, dispatch);

        string immutableDispatchPath = ArtifactPath(
            root,
            "dispatches",
            dispatchRef);
        _ = PutImmutable(immutableDispatchPath, dispatchBytes);
        string dispatchRelativePath = RelativePath(root, immutableDispatchPath);

        PaperTheoryAuditPlannedReviewer[] plannedReviewers = dispatch.Reviewers
            .OrderBy(reviewer => reviewer.Slot)
            .Select(reviewer =>
            {
                PaperAgentTask task = BuildTask(
                    root,
                    dispatch,
                    dispatchRef,
                    dispatchRelativePath,
                    context,
                    reviewer);
                PaperAgentRuntimeService.Validate(task);
                byte[] taskBytes = CanonicalJson.Serialize(task);
                string taskRef = Reference(taskBytes);
                string taskPath = Path.Combine(
                    root,
                    "inbox",
                    "agent-tasks",
                    $"theory-audit-{Hex(dispatch.AuditRequestRef)}-{reviewer.Slot:D2}-{reviewer.Attempt:D2}.json");
                _ = PutImmutable(taskPath, taskBytes);
                return new PaperTheoryAuditPlannedReviewer(
                    reviewer.Slot,
                    reviewer.ReviewerRole,
                    reviewer.Focus,
                    reviewer.Attempt,
                    taskRef,
                    RelativePath(root, taskPath));
            })
            .ToArray();

        var planContent = new PaperTheoryAuditReviewPlanContent(
            dispatchRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.AuditRequestRef,
            context.TheoremPackage.TheoremPackageId,
            context.Request.RequestContent.TheoryAuthorRunRef,
            dispatch.ExactInputs
                .Select(input => input.ArtifactRef)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            plannedReviewers,
            dispatch.RequestedAt);
        var plan = new PaperTheoryAuditReviewPlan(
            PaperTheoryAuditAgentSchemas.ReviewPlan,
            Reference(planContent),
            planContent);
        Validate(plan);
        PaperTheoryAuditStoredArtifact storedPlan = StoreDomain(
            root,
            "review-plans",
            plan.Schema,
            plan.PlanId,
            plan.PlanContent,
            plan);
        var cursor = new PaperTheoryAuditPlanCursor(
            PaperTheoryAuditAgentSchemas.PlanCursor,
            dispatch.AuditRequestRef,
            plan.PlanId,
            storedPlan.ContentPath,
            storedPlan.EnvelopeRef,
            storedPlan.EnvelopePath,
            dispatch.RequestedAt);
        Validate(cursor);
        string cursorPath = PlanCursorPath(root, dispatch.AuditRequestRef);
        bool replayed = PutImmutable(cursorPath, CanonicalJson.Serialize(cursor));
        if (replayed)
        {
            ValidatePlanReplay(root, cursor, plan, plannedReviewers);
        }
        return new PaperTheoryAuditAgentTasksStaged(
            PaperTheoryAuditAgentSchemas.TasksStaged,
            dispatchRef,
            storedPlan,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.AuditRequestRef,
            context.TheoremPackage.TheoremPackageId,
            plannedReviewers,
            replayed);
    }

    public static PaperTheoryAuditAgentResultAdmitted AdmitOpinion(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        if (!string.Equals(task.Phase, "theory-audit", StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, "paper-theory-independent-referee", StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, PaperTheoryAuditService.FreshContextMode, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a fresh A3 theory-audit task can enter this admission bridge.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperTheoryAuditAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Theory-audit task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = Reference(dispatchBytes);
        PaperTheoryAuditAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);
        PaperTheoryAuditAgentContext context = LoadContext(root, dispatch);
        PaperTheoryAuditReviewPlan plan = ReadPlan(root, dispatch.AuditRequestRef);
        PaperTheoryAuditPlannedReviewer reviewer = plan.PlanContent.Reviewers
            .SingleOrDefault(value => string.Equals(
                value.TaskRef,
                taskRef,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Theory-audit plan does not contain this reviewer task.");
        ValidateTaskBinding(
            root,
            task,
            dispatch,
            dispatchRef,
            dispatchInput.RepositoryRelativePath,
            context,
            reviewer);

        PaperAgentTaskCursor agentCursor = ReadAgentCursor(root, task, taskRef);
        PaperAgentResultWire result = ReadAgentResult(
            root,
            task,
            taskRef,
            agentCursor.ResultRef);
        RequireCursorMatchesResult(agentCursor, result);
        if (!string.Equals(result.Status, "completed", StringComparison.Ordinal)
            || !string.Equals(result.NextRoute, "theory-audit-opinion", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a completed A3 opinion result can be admitted.");
        }
        RequireFreshRunId(agentCursor.RunId);

        string opinionCursorPath = OpinionCursorPath(root, taskRef);
        bool opinionReplayed = File.Exists(opinionCursorPath);
        PaperTheoryAuditOpinionCursor opinionCursor;
        if (opinionReplayed)
        {
            opinionCursor = ReadOpinionCursor(opinionCursorPath);
            ValidateOpinionReplay(
                root,
                opinionCursor,
                taskRef,
                agentCursor,
                dispatchRef,
                plan.PlanId,
                reviewer);
        }
        else
        {
            opinionCursor = AdmitNewOpinion(
                root,
                taskRef,
                task,
                agentCursor,
                dispatch,
                dispatchRef,
                plan,
                reviewer,
                context);
            Directory.CreateDirectory(Path.GetDirectoryName(opinionCursorPath)!);
            try
            {
                PaperResearchInputStore.WriteAtomic(
                    opinionCursorPath,
                    CanonicalJson.Serialize(opinionCursor),
                    overwrite: false);
            }
            catch (IOException) when (File.Exists(opinionCursorPath))
            {
                opinionCursor = ReadOpinionCursor(opinionCursorPath);
                ValidateOpinionReplay(
                    root,
                    opinionCursor,
                    taskRef,
                    agentCursor,
                    dispatchRef,
                    plan.PlanId,
                    reviewer);
                opinionReplayed = true;
            }
        }

        PaperTheoryAuditAggregateResult aggregate = TryAggregate(
            root,
            context,
            plan,
            result.CompletedAt);
        return ToAdmitted(
            opinionCursor,
            aggregate,
            opinionReplayed || aggregate.Replayed);
    }

    public static void Validate(PaperTheoryAuditAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperTheoryAuditAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        RequirePaperId(dispatch.PaperId);
        RequireDigest(dispatch.TheoryProgramRef, nameof(dispatch.TheoryProgramRef));
        RequireDigest(dispatch.AuditRequestRef, nameof(dispatch.AuditRequestRef));
        if (dispatch.ExactInputs is null
            || dispatch.ExactInputs.Count is < 5 or > 64)
        {
            throw new InvalidDataException(
                "Theory-audit dispatch must contain between five and sixty-four exact inputs.");
        }
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperAgentInputArtifact input in dispatch.ExactInputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            RequireSchema(input.Schema, nameof(input.Schema));
            RequireDigest(input.ArtifactRef, nameof(input.ArtifactRef));
            RequireRepositoryRelativePath(
                input.RepositoryRelativePath,
                nameof(input.RepositoryRelativePath));
            if (!refs.Add(input.ArtifactRef) || !paths.Add(input.RepositoryRelativePath))
            {
                throw new InvalidDataException(
                    "Theory-audit exact input refs and paths must be unique.");
            }
        }
        if (!refs.Contains(dispatch.TheoryProgramRef)
            || !refs.Contains(dispatch.AuditRequestRef))
        {
            throw new InvalidDataException(
                "Theory-audit dispatch must include its program and audit request artifacts.");
        }
        ValidateReviewerSpecs(dispatch.Reviewers);
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    public static void Validate(PaperTheoryAuditReviewPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        RequireExact(
            plan.Schema,
            PaperTheoryAuditAgentSchemas.ReviewPlan,
            nameof(plan.Schema));
        PaperTheoryAuditReviewPlanContent content = plan.PlanContent
            ?? throw new InvalidDataException("plan_content is required.");
        RequireDigest(content.DispatchRef, nameof(content.DispatchRef));
        RequirePaperId(content.PaperId);
        RequireDigest(content.TheoryProgramRef, nameof(content.TheoryProgramRef));
        RequireDigest(content.AuditRequestRef, nameof(content.AuditRequestRef));
        RequireDigest(content.TheoremPackageRef, nameof(content.TheoremPackageRef));
        RequireDigest(content.TheoryAuthorRunRef, nameof(content.TheoryAuthorRunRef));
        RequireDigestList(content.ContextInputRefs, nameof(content.ContextInputRefs), 5);
        if (content.Reviewers is null
            || content.Reviewers.Count is < 2 or > 4)
        {
            throw new InvalidDataException(
                "Theory-audit review plan requires between two and four reviewers.");
        }
        var roles = new HashSet<string>(StringComparer.Ordinal);
        var tasks = new HashSet<string>(StringComparer.Ordinal);
        var taskPaths = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < content.Reviewers.Count; index++)
        {
            PaperTheoryAuditPlannedReviewer reviewer = content.Reviewers[index];
            ValidateReviewerSpec(new(
                reviewer.Slot,
                reviewer.ReviewerRole,
                reviewer.Focus,
                reviewer.Attempt));
            if (reviewer.Slot != index + 1
                || !roles.Add(reviewer.ReviewerRole)
                || !tasks.Add(reviewer.TaskRef)
                || !taskPaths.Add(reviewer.TaskPath))
            {
                throw new InvalidDataException(
                    "Review-plan slots, roles, task refs, and task paths must be distinct and contiguous.");
            }
            RequireDigest(reviewer.TaskRef, nameof(reviewer.TaskRef));
            RequireRepositoryRelativePath(reviewer.TaskPath, nameof(reviewer.TaskPath));
        }
        RequireMandatoryRoles(roles);
        ParseUtc(content.CreatedAt, nameof(content.CreatedAt));
        RequireIdentity(plan.PlanId, content, nameof(plan.PlanId));
    }

    public static void Validate(PaperTheoryAuditPlanCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(cursor.Schema, PaperTheoryAuditAgentSchemas.PlanCursor, nameof(cursor.Schema));
        RequireDigest(cursor.AuditRequestRef, nameof(cursor.AuditRequestRef));
        RequireDigest(cursor.PlanRef, nameof(cursor.PlanRef));
        RequireRepositoryRelativePath(cursor.PlanContentPath, nameof(cursor.PlanContentPath));
        RequireDigest(cursor.PlanEnvelopeRef, nameof(cursor.PlanEnvelopeRef));
        RequireRepositoryRelativePath(cursor.PlanEnvelopePath, nameof(cursor.PlanEnvelopePath));
        ParseUtc(cursor.CreatedAt, nameof(cursor.CreatedAt));
    }

    public static void Validate(PaperTheoryAuditOpinionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        RequireExact(artifact.Schema, PaperTheoryAuditAgentSchemas.Opinion, nameof(artifact.Schema));
        RequireIdentity(artifact.OpinionId, artifact.OpinionContent, nameof(artifact.OpinionId));
    }

    public static void Validate(PaperTheoryAuditOpinionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(cursor.Schema, PaperTheoryAuditAgentSchemas.OpinionCursor, nameof(cursor.Schema));
        RequireDigest(cursor.TaskRef, nameof(cursor.TaskRef));
        RequireDigest(cursor.ResultRef, nameof(cursor.ResultRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        RequireDigest(cursor.PlanRef, nameof(cursor.PlanRef));
        RequirePaperId(cursor.PaperId);
        RequireDigest(cursor.TheoryProgramRef, nameof(cursor.TheoryProgramRef));
        RequireDigest(cursor.AuditRequestRef, nameof(cursor.AuditRequestRef));
        if (cursor.ReviewerSlot < 1 || !ReviewerRoles.Contains(cursor.ReviewerRole))
        {
            throw new InvalidDataException("Opinion cursor reviewer slot or role is invalid.");
        }
        RequireDigest(cursor.ReviewerRunRef, nameof(cursor.ReviewerRunRef));
        RequireDigest(cursor.ReviewSessionRef, nameof(cursor.ReviewSessionRef));
        ValidateStoredArtifact(cursor.Opinion, PaperTheoryAuditAgentSchemas.Opinion);
        RequireFreshRunId(cursor.AgentRunId);
        RequireProvenance(cursor.Provenance);
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    public static void Validate(PaperTheoryAuditAggregateCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(cursor.Schema, PaperTheoryAuditAgentSchemas.AggregateCursor, nameof(cursor.Schema));
        RequireDigest(cursor.PlanRef, nameof(cursor.PlanRef));
        RequireDigest(cursor.AuditRequestRef, nameof(cursor.AuditRequestRef));
        RequirePaperId(cursor.PaperId);
        RequireDigest(cursor.TheoryProgramRef, nameof(cursor.TheoryProgramRef));
        RequireDigestList(cursor.OpinionRefs, nameof(cursor.OpinionRefs), 2);
        RequireDigestList(cursor.ReviewerRunRefs, nameof(cursor.ReviewerRunRefs), 2);
        RequireDigestList(cursor.ReviewSessionRefs, nameof(cursor.ReviewSessionRefs), 2);
        ValidateStoredArtifact(cursor.Audit, PaperTheoryAuditSchemas.Audit);
        ValidateStoredArtifact(cursor.Scorecard, PaperPortfolioDecisionSchemas.Scorecard);
        if (!Verdicts.Contains(cursor.Verdict))
        {
            throw new InvalidDataException("Aggregate cursor verdict is invalid.");
        }
        RequireNextRoute(cursor.NextRoute);
        if (cursor.Passed != cursor.PromotionEligible
            || cursor.PromotionEligible != string.Equals(cursor.Verdict, "pass", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Aggregate pass, promotion eligibility, and verdict are inconsistent.");
        }
        ParseUtc(cursor.AggregatedAt, nameof(cursor.AggregatedAt));
    }

    private static PaperTheoryAuditOpinionCursor AdmitNewOpinion(
        string root,
        string taskRef,
        PaperAgentTask task,
        PaperAgentTaskCursor agentCursor,
        PaperTheoryAuditAgentDispatch dispatch,
        string dispatchRef,
        PaperTheoryAuditReviewPlan plan,
        PaperTheoryAuditPlannedReviewer reviewer,
        PaperTheoryAuditAgentContext context)
    {
        if (agentCursor.Outputs.Count != 1)
        {
            throw new InvalidDataException(
                "A completed theory-audit reviewer must produce exactly one opinion draft.");
        }
        PaperAgentStoredOutput output = agentCursor.Outputs[0];
        if (!string.Equals(
                output.Schema,
                PaperTheoryAuditAgentSchemas.OpinionDraft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-audit reviewer produced the wrong draft schema.");
        }
        byte[] draftBytes = ReadAgentOutput(root, output.ArtifactRef);
        PaperTheoryAuditOpinionDraft draft =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditOpinionDraft>(draftBytes);
        ValidateDraft(draft, dispatch, reviewer, context);

        string reviewerRunRef = DomainReference(
            "trureturing:paper-theory-audit-reviewer-run:v1",
            agentCursor.RunId);
        string reviewSessionRef = DomainReference(
            "trureturing:paper-theory-audit-review-session:v1",
            taskRef + "\n" + agentCursor.RunId);
        if (string.Equals(
                reviewerRunRef,
                context.Request.RequestContent.TheoryAuthorRunRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A3 reviewer reused the theory-author run identity.");
        }
        var opinion = new PaperTheoryAuditOpinion(
            reviewerRunRef,
            reviewSessionRef,
            reviewer.ReviewerRole,
            PaperTheoryAuditService.FreshContextMode,
            context.Request.RequestContent.Contract.ExactInputRefs,
            draft.Metrics,
            draft.Verdict,
            draft.Blockers,
            draft.RequiredRevisions,
            draft.NoveltyEvidence,
            draft.ProofAudit,
            draft.OverlapFindings,
            draft.ReviewedAt);
        string opinionId = Reference(opinion);
        var opinionArtifact = new PaperTheoryAuditOpinionArtifact(
            PaperTheoryAuditAgentSchemas.Opinion,
            opinionId,
            opinion);
        PaperTheoryAuditStoredArtifact storedOpinion = StoreDomain(
            root,
            "opinions",
            opinionArtifact.Schema,
            opinionId,
            opinion,
            opinionArtifact);
        var cursor = new PaperTheoryAuditOpinionCursor(
            PaperTheoryAuditAgentSchemas.OpinionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            plan.PlanId,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.AuditRequestRef,
            reviewer.Slot,
            reviewer.ReviewerRole,
            reviewerRunRef,
            reviewSessionRef,
            storedOpinion,
            agentCursor.RunId,
            agentCursor.Provenance,
            draft.ReviewedAt);
        Validate(cursor);
        return cursor;
    }

    private static PaperTheoryAuditAggregateResult TryAggregate(
        string root,
        PaperTheoryAuditAgentContext context,
        PaperTheoryAuditReviewPlan plan,
        string completedAt)
    {
        string aggregatePath = AggregateCursorPath(root, plan.PlanId);
        if (File.Exists(aggregatePath))
        {
            PaperTheoryAuditAggregateCursor replay = ReadAggregateCursor(aggregatePath);
            ValidateAggregateReplay(root, replay, context, plan);
            return new(ReadyStatus, [], replay, true);
        }

        string[] missing = plan.PlanContent.Reviewers
            .Select(reviewer => reviewer.TaskRef)
            .Where(taskRef => !File.Exists(OpinionCursorPath(root, taskRef)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length != 0)
        {
            return new(WaitingStatus, missing, null, false);
        }

        PaperTheoryAuditOpinionCursor[] opinionCursors = plan.PlanContent.Reviewers
            .Select(reviewer => ReadOpinionCursor(OpinionCursorPath(root, reviewer.TaskRef)))
            .OrderBy(cursor => cursor.ReviewerSlot)
            .ToArray();
        ValidateIndependentRuns(opinionCursors, context.Request, plan);
        PaperTheoryAuditOpinion[] opinions = opinionCursors
            .Select(cursor => ReadOpinion(root, cursor.Opinion))
            .ToArray();
        string aggregatedAt = MaxTimestamp(
            opinions.Select(opinion => opinion.ReviewedAt).Append(completedAt));
        PaperTheoryAudit audit = PaperTheoryAuditService.CreateAudit(
            context.Program,
            context.Scope,
            context.Inventory,
            context.TheoremPackage,
            context.Request,
            opinions,
            aggregatedAt);
        PaperCandidateScorecard scorecard = PaperPortfolioDecisionService.CreateScorecard(
            context.TheoremPackage,
            audit,
            aggregatedAt);
        PaperTheoryAuditStoredArtifact storedAudit = StoreDomain(
            root,
            "audits",
            audit.Schema,
            audit.AuditId,
            audit.AuditContent,
            audit);
        PaperTheoryAuditStoredArtifact storedScorecard = StoreDomain(
            root,
            "scorecards",
            scorecard.Schema,
            scorecard.ScorecardId,
            scorecard.ScorecardContent,
            scorecard);
        string nextRoute = NextRoute(audit.AuditContent.Verdict);
        var cursor = new PaperTheoryAuditAggregateCursor(
            PaperTheoryAuditAgentSchemas.AggregateCursor,
            plan.PlanId,
            context.Request.RequestId,
            context.Program.ProgramContent.PaperId,
            context.Program.TheoryProgramId,
            opinionCursors
                .Select(value => value.Opinion.ArtifactRef)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            opinionCursors
                .Select(value => value.ReviewerRunRef)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            opinionCursors
                .Select(value => value.ReviewSessionRef)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            storedAudit,
            storedScorecard,
            audit.AuditContent.Verdict,
            audit.AuditContent.Passed,
            scorecard.ScorecardContent.PromotionEligible,
            nextRoute,
            aggregatedAt);
        Validate(cursor);
        Directory.CreateDirectory(Path.GetDirectoryName(aggregatePath)!);
        try
        {
            PaperResearchInputStore.WriteAtomic(
                aggregatePath,
                CanonicalJson.Serialize(cursor),
                overwrite: false);
        }
        catch (IOException) when (File.Exists(aggregatePath))
        {
            PaperTheoryAuditAggregateCursor replay = ReadAggregateCursor(aggregatePath);
            ValidateAggregateReplay(root, replay, context, plan);
            return new(ReadyStatus, [], replay, true);
        }
        return new(ReadyStatus, [], cursor, false);
    }

    private static PaperTheoryAuditAgentResultAdmitted ToAdmitted(
        PaperTheoryAuditOpinionCursor opinion,
        PaperTheoryAuditAggregateResult aggregate,
        bool replayed)
    {
        PaperTheoryAuditAggregateCursor? ready = aggregate.Cursor;
        return new PaperTheoryAuditAgentResultAdmitted(
            PaperTheoryAuditAgentSchemas.ResultAdmitted,
            opinion.TaskRef,
            opinion.ResultRef,
            opinion.DispatchRef,
            opinion.PlanRef,
            opinion.PaperId,
            opinion.TheoryProgramRef,
            opinion.AuditRequestRef,
            opinion.ReviewerSlot,
            opinion.ReviewerRole,
            opinion.Opinion,
            aggregate.Status,
            aggregate.MissingTaskRefs,
            ready?.Audit,
            ready?.Scorecard,
            ready?.Verdict ?? string.Empty,
            ready?.Passed ?? false,
            ready?.PromotionEligible ?? false,
            ready?.NextRoute ?? "theory-audit",
            opinion.AgentRunId,
            opinion.Provenance,
            opinion.AdmittedAt,
            replayed);
    }

    private static PaperAgentTask BuildTask(
        string root,
        PaperTheoryAuditAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperTheoryAuditAgentContext context,
        PaperTheoryAuditReviewerSpec reviewer)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile("theory-audit");
        return new PaperAgentTask(
            PaperAgentSchemas.Task,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            dispatch.ExactInputs
                .Append(new PaperAgentInputArtifact(
                    PaperTheoryAuditAgentSchemas.Dispatch,
                    dispatchRef,
                    dispatchRelativePath))
                .OrderBy(input => input.Schema, StringComparer.Ordinal)
                .ThenBy(input => input.ArtifactRef, StringComparer.Ordinal)
                .ToArray(),
            [new PaperAgentExpectedOutput(
                PaperTheoryAuditAgentSchemas.OpinionDraft,
                "outputs/theory-audit-opinion.json")],
            ["theory-audit-opinion", "theory-audit", "blocked"],
            BuildInstruction(context.Request, reviewer),
            context.Request.RequestContent.Contract.ForbiddenShortcuts
                .Append("Do not inspect or infer any prior audit opinion, aggregate verdict, scorecard, or portfolio decision.")
                .Append("Do not coordinate with another reviewer task or reuse its session.")
                .Append("Do not compute reviewer_run_ref, review_session_ref, opinion_id, audit_id, or scorecard_id; repository admission owns those identities.")
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            dispatch.RequestedAt);
    }

    private static string BuildInstruction(
        PaperTheoryAuditRequest request,
        PaperTheoryAuditReviewerSpec reviewer)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Execute one clean-room A3 paper-theory audit opinion.");
        builder.AppendLine($"Reviewer slot: {reviewer.Slot}.");
        builder.AppendLine($"Reviewer role: {reviewer.ReviewerRole}.");
        builder.AppendLine($"Specialized focus: {reviewer.Focus}");
        builder.AppendLine($"Use paper_id={request.RequestContent.PaperId}.");
        builder.AppendLine($"Use theory_program_ref={request.RequestContent.TheoryProgramRef}.");
        builder.AppendLine($"Use audit_request_ref={request.RequestId}.");
        builder.AppendLine($"Use theorem_package_ref={request.RequestContent.TheoremPackageRef}.");
        builder.AppendLine("Read every supplied evidence file. Do not search for prior review output or use pipeline acceptance history.");
        builder.AppendLine("Write exactly one paper-theory-audit-opinion-draft.v1 object to outputs/theory-audit-opinion.json.");
        builder.AppendLine("Score abstraction_quality, theorem_depth, logical_closure, proof_plausibility, novelty, significance, formalization_readiness, journal_floor, and overlap_hygiene from zero to ten.");
        builder.AppendLine("Give one verdict from pass, deepen, split, merge, park, or archive. A pass requires no blockers, no required revisions, and every metric at the repository threshold.");
        builder.AppendLine("Novelty evidence must compare theorem-level hypotheses and conclusions against the supplied literature evidence. Proof audit must reconstruct at least two load-bearing proof interfaces.");
        builder.AppendLine("Return blocker and revision arrays even when empty. The repository will derive reviewer and session identities and aggregate opinions coordinate-wise by minimum.");
        builder.AppendLine("Scientific tasks:");
        foreach (string value in request.RequestContent.Contract.ScientificTasks)
        {
            builder.AppendLine($"- {value}");
        }
        builder.AppendLine("Pass conditions:");
        foreach (string value in request.RequestContent.Contract.PassConditions)
        {
            builder.AppendLine($"- {value}");
        }
        builder.AppendLine("Fail conditions:");
        foreach (string value in request.RequestContent.Contract.FailConditions)
        {
            builder.AppendLine($"- {value}");
        }
        return builder.ToString();
    }

    private static PaperTheoryAuditAgentContext LoadContext(
        string root,
        PaperTheoryAuditAgentDispatch dispatch)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperTheoryProgram program = ReadProgram(root, dispatch);
        PaperAgentInputArtifact requestInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryAuditSchemas.AuditRequest,
            dispatch.AuditRequestRef,
            "audit request");
        PaperTheoryAuditRequestContent requestContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditRequestContent>(
                ReadExactInput(root, requestInput));
        var request = new PaperTheoryAuditRequest(
            PaperTheoryAuditSchemas.AuditRequest,
            dispatch.AuditRequestRef,
            requestContent);
        PaperAgentInputArtifact scopeInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryFoundationSchemas.Scope,
            request.RequestContent.ScopeRef,
            "theory scope");
        PaperTheoryScopeContent scopeContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryScopeContent>(
                ReadExactInput(root, scopeInput));
        var scope = new PaperTheoryScope(
            PaperTheoryFoundationSchemas.Scope,
            request.RequestContent.ScopeRef,
            scopeContent);
        PaperAgentInputArtifact inventoryInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryFoundationSchemas.Inventory,
            request.RequestContent.InventoryRef,
            "theory inventory");
        PaperTheoryInventoryContent inventoryContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryInventoryContent>(
                ReadExactInput(root, inventoryInput));
        var inventory = new PaperTheoryInventory(
            PaperTheoryFoundationSchemas.Inventory,
            request.RequestContent.InventoryRef,
            inventoryContent);
        PaperAgentInputArtifact packageInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryDeepeningSchemas.TheoremPackage,
            request.RequestContent.TheoremPackageRef,
            "theorem package");
        PaperTheoremPackageContent packageContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoremPackageContent>(
                ReadExactInput(root, packageInput));
        var package = new PaperTheoremPackage(
            PaperTheoryDeepeningSchemas.TheoremPackage,
            request.RequestContent.TheoremPackageRef,
            packageContent);
        PaperTheoryAuditService.Validate(request, program, scope, inventory, package);
        if (!string.Equals(dispatch.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(dispatch.AuditRequestRef, request.RequestId, StringComparison.Ordinal)
            || !string.Equals(dispatch.RequestedAt, request.RequestContent.RequestedAt, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-audit dispatch changed its paper, program, request, or timestamp.");
        }

        string[] expectedRefs = request.RequestContent.Contract.ExactInputRefs
            .Append(request.RequestId)
            .Append(program.ProgramContent.CandidatePaperRef)
            .Append(program.ProgramContent.LiteratureResearchRef)
            .Append(program.ProgramContent.IntuitionProposalRef)
            .Append(program.ProgramContent.PaperResearchInputRef)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        RequireInputRefsExactly(dispatch.ExactInputs, expectedRefs);
        _ = RequiredInputRef(
            dispatch.ExactInputs,
            program.ProgramContent.CandidatePaperRef,
            "candidate paper evidence");
        _ = RequiredInputRef(
            dispatch.ExactInputs,
            program.ProgramContent.LiteratureResearchRef,
            "literature research evidence");
        _ = RequiredInputRef(
            dispatch.ExactInputs,
            program.ProgramContent.IntuitionProposalRef,
            "Intuition proposal evidence");
        _ = RequiredInputRef(
            dispatch.ExactInputs,
            program.ProgramContent.PaperResearchInputRef,
            "exact Paper research input");
        return new(program, scope, inventory, package, request);
    }

    private static PaperTheoryProgram ReadProgram(
        string root,
        PaperTheoryAuditAgentDispatch dispatch)
    {
        PaperAgentInputArtifact input = RequiredInput(
            dispatch.ExactInputs,
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            "theory program");
        PaperTheoryProgramContent content =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryProgramContent>(
                ReadExactInput(root, input));
        var program = new PaperTheoryProgram(
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            content);
        PaperPortfolioService.Validate(program);
        return program;
    }

    private static void ValidateTaskBinding(
        string root,
        PaperAgentTask actual,
        PaperTheoryAuditAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperTheoryAuditAgentContext context,
        PaperTheoryAuditPlannedReviewer reviewer)
    {
        var spec = new PaperTheoryAuditReviewerSpec(
            reviewer.Slot,
            reviewer.ReviewerRole,
            reviewer.Focus,
            reviewer.Attempt);
        PaperAgentTask expected = BuildTask(
            root,
            dispatch,
            dispatchRef,
            dispatchRelativePath,
            context,
            spec);
        if (!CanonicalJson.Serialize(actual).AsSpan().SequenceEqual(
                CanonicalJson.Serialize(expected)))
        {
            throw new InvalidDataException(
                "Theory-audit task changed its dispatch-owned reviewer contract.");
        }
    }

    private static void ValidateDraft(
        PaperTheoryAuditOpinionDraft draft,
        PaperTheoryAuditAgentDispatch dispatch,
        PaperTheoryAuditPlannedReviewer reviewer,
        PaperTheoryAuditAgentContext context)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireExact(draft.Schema, PaperTheoryAuditAgentSchemas.OpinionDraft, nameof(draft.Schema));
        if (!string.Equals(draft.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(draft.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(draft.AuditRequestRef, dispatch.AuditRequestRef, StringComparison.Ordinal)
            || !string.Equals(draft.TheoremPackageRef, context.TheoremPackage.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(draft.ReviewerRole, reviewer.ReviewerRole, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-audit opinion draft changed its paper, request, theorem package, or reviewer role.");
        }
        _ = PaperTheoryAuditService.MetricsPass(draft.Metrics);
        if (!Verdicts.Contains(draft.Verdict))
        {
            throw new InvalidDataException("Theory-audit opinion verdict is unsupported.");
        }
        RequireTextList(draft.Blockers, nameof(draft.Blockers), 16384, 0);
        RequireTextList(draft.RequiredRevisions, nameof(draft.RequiredRevisions), 16384, 0);
        RequireText(draft.NoveltyEvidence, nameof(draft.NoveltyEvidence), 32768, 80);
        RequireTextList(draft.ProofAudit, nameof(draft.ProofAudit), 16384, 2);
        RequireTextList(draft.OverlapFindings, nameof(draft.OverlapFindings), 16384, 1);
        DateTimeOffset reviewedAt = ParseUtc(draft.ReviewedAt, nameof(draft.ReviewedAt));
        if (reviewedAt < ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt)))
        {
            throw new InvalidDataException(
                "Theory-audit opinion cannot predate its request.");
        }
        if (string.Equals(draft.Verdict, "pass", StringComparison.Ordinal)
            && (draft.Blockers.Count != 0
                || draft.RequiredRevisions.Count != 0
                || !PaperTheoryAuditService.MetricsPass(draft.Metrics)))
        {
            throw new InvalidDataException(
                "A pass opinion cannot carry blockers, revisions, or sub-threshold metrics.");
        }
    }

    private static void ValidateIndependentRuns(
        IReadOnlyList<PaperTheoryAuditOpinionCursor> cursors,
        PaperTheoryAuditRequest request,
        PaperTheoryAuditReviewPlan plan)
    {
        if (cursors.Count < request.RequestContent.MinimumIndependentOpinions)
        {
            throw new InvalidDataException(
                "A3 aggregate has fewer than the required independent reviewer results.");
        }
        var runIds = new HashSet<string>(StringComparer.Ordinal);
        var runRefs = new HashSet<string>(StringComparer.Ordinal);
        var sessionRefs = new HashSet<string>(StringComparer.Ordinal);
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperTheoryAuditOpinionCursor cursor in cursors)
        {
            Validate(cursor);
            if (!string.Equals(cursor.PlanRef, plan.PlanId, StringComparison.Ordinal)
                || !string.Equals(cursor.AuditRequestRef, request.RequestId, StringComparison.Ordinal)
                || !runIds.Add(cursor.AgentRunId)
                || !runRefs.Add(cursor.ReviewerRunRef)
                || !sessionRefs.Add(cursor.ReviewSessionRef)
                || !roles.Add(cursor.ReviewerRole))
            {
                throw new InvalidDataException(
                    "A3 opinions must use distinct fresh Codex runs, sessions, and reviewer roles.");
            }
            if (string.Equals(
                    cursor.ReviewerRunRef,
                    request.RequestContent.TheoryAuthorRunRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A3 reviewer reused the theory-author run identity.");
            }
        }
        RequireMandatoryRoles(roles);
    }

    private static void ValidateReviewerSpecs(
        IReadOnlyList<PaperTheoryAuditReviewerSpec>? reviewers)
    {
        if (reviewers is null || reviewers.Count is < 2 or > 4)
        {
            throw new InvalidDataException(
                "Theory-audit dispatch requires between two and four reviewers.");
        }
        PaperTheoryAuditReviewerSpec[] normalized = reviewers
            .OrderBy(reviewer => reviewer.Slot)
            .ToArray();
        var roles = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < normalized.Length; index++)
        {
            ValidateReviewerSpec(normalized[index]);
            if (normalized[index].Slot != index + 1
                || !roles.Add(normalized[index].ReviewerRole))
            {
                throw new InvalidDataException(
                    "Theory-audit reviewer slots must be contiguous and roles must be unique.");
            }
        }
        RequireMandatoryRoles(roles);
    }

    private static void ValidateReviewerSpec(PaperTheoryAuditReviewerSpec reviewer)
    {
        ArgumentNullException.ThrowIfNull(reviewer);
        if (reviewer.Slot < 1
            || reviewer.Attempt < 1
            || !ReviewerRoles.Contains(reviewer.ReviewerRole))
        {
            throw new InvalidDataException(
                "Theory-audit reviewer slot, role, or attempt is invalid.");
        }
        RequireText(reviewer.Focus, nameof(reviewer.Focus), 8192, 40);
    }

    private static void RequireMandatoryRoles(IReadOnlySet<string> roles)
    {
        if (!roles.Contains("mathematical-referee")
            || !roles.Contains("novelty-referee"))
        {
            throw new InvalidDataException(
                "A3 requires independent mathematical-referee and novelty-referee roles.");
        }
    }

    private static string NextRoute(string verdict) =>
        verdict switch
        {
            "pass" => "portfolio-judgment",
            "deepen" => "theory-deepening",
            "split" => "portfolio-split",
            "merge" => "portfolio-merge",
            "park" => "parked",
            "archive" => "archived",
            _ => throw new InvalidDataException($"Unsupported A3 verdict {verdict}.")
        };

    private static void RequireNextRoute(string route)
    {
        _ = NextRoute(route switch
        {
            "portfolio-judgment" => "pass",
            "theory-deepening" => "deepen",
            "portfolio-split" => "split",
            "portfolio-merge" => "merge",
            "parked" => "park",
            "archived" => "archive",
            _ => throw new InvalidDataException($"Unsupported A3 next route {route}.")
        });
    }

    private static void ValidatePlanReplay(
        string root,
        PaperTheoryAuditPlanCursor cursor,
        PaperTheoryAuditReviewPlan expectedPlan,
        IReadOnlyList<PaperTheoryAuditPlannedReviewer> reviewers)
    {
        Validate(cursor);
        if (!string.Equals(cursor.PlanRef, expectedPlan.PlanId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Audit-request plan cursor changed the review plan identity.");
        }
        PaperTheoryAuditReviewPlan actual = ReadPlan(root, cursor.AuditRequestRef);
        if (!CanonicalJson.Serialize(actual).AsSpan().SequenceEqual(
                CanonicalJson.Serialize(expectedPlan)))
        {
            throw new InvalidDataException(
                "Stored A3 review plan differs from the deterministic replay plan.");
        }
        foreach (PaperTheoryAuditPlannedReviewer reviewer in reviewers)
        {
            string full = Path.GetFullPath(Path.Combine(
                root,
                reviewer.TaskPath.Replace('/', Path.DirectorySeparatorChar)));
            byte[] bytes = ReadImmutable(full, reviewer.TaskRef, "Staged A3 reviewer task");
            PaperAgentTask task =
                PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(bytes);
            PaperAgentRuntimeService.Validate(task);
        }
    }

    private static void ValidateOpinionReplay(
        string root,
        PaperTheoryAuditOpinionCursor cursor,
        string taskRef,
        PaperAgentTaskCursor agentCursor,
        string dispatchRef,
        string planRef,
        PaperTheoryAuditPlannedReviewer reviewer)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, taskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PlanRef, planRef, StringComparison.Ordinal)
            || cursor.ReviewerSlot != reviewer.Slot
            || !string.Equals(cursor.ReviewerRole, reviewer.ReviewerRole, StringComparison.Ordinal)
            || !string.Equals(cursor.AgentRunId, agentCursor.RunId, StringComparison.Ordinal)
            || !string.Equals(cursor.Provenance, agentCursor.Provenance, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A3 opinion cursor changed task, result, plan, reviewer, or run identity.");
        }
        _ = ReadOpinion(root, cursor.Opinion);
    }

    private static void ValidateAggregateReplay(
        string root,
        PaperTheoryAuditAggregateCursor cursor,
        PaperTheoryAuditAgentContext context,
        PaperTheoryAuditReviewPlan plan)
    {
        Validate(cursor);
        if (!string.Equals(cursor.PlanRef, plan.PlanId, StringComparison.Ordinal)
            || !string.Equals(cursor.AuditRequestRef, context.Request.RequestId, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, context.Program.ProgramContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, context.Program.TheoryProgramId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A3 aggregate cursor changed its plan, request, paper, or program.");
        }
        PaperTheoryAudit audit = ReadEnvelope<PaperTheoryAudit>(root, cursor.Audit);
        PaperCandidateScorecard scorecard = ReadEnvelope<PaperCandidateScorecard>(root, cursor.Scorecard);
        PaperTheoryAuditService.Validate(audit);
        PaperPortfolioDecisionService.Validate(scorecard);
        if (!string.Equals(audit.AuditId, cursor.Audit.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(scorecard.ScorecardId, cursor.Scorecard.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(audit.AuditContent.Verdict, cursor.Verdict, StringComparison.Ordinal)
            || audit.AuditContent.Passed != cursor.Passed
            || scorecard.ScorecardContent.PromotionEligible != cursor.PromotionEligible
            || !string.Equals(NextRoute(audit.AuditContent.Verdict), cursor.NextRoute, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Stored A3 aggregate artifacts differ from the aggregate cursor.");
        }
    }

    private static PaperTheoryAuditReviewPlan ReadPlan(string root, string requestRef)
    {
        string cursorPath = PlanCursorPath(root, requestRef);
        PaperTheoryAuditPlanCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditPlanCursor>(
                ReadBoundedFile(cursorPath, MaximumControlBytes, "A3 plan cursor"));
        Validate(cursor);
        byte[] contentBytes = ReadRepositoryArtifact(
            root,
            cursor.PlanContentPath,
            cursor.PlanRef,
            "A3 review plan content");
        PaperTheoryAuditReviewPlanContent content =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditReviewPlanContent>(contentBytes);
        var plan = new PaperTheoryAuditReviewPlan(
            PaperTheoryAuditAgentSchemas.ReviewPlan,
            cursor.PlanRef,
            content);
        Validate(plan);
        byte[] envelopeBytes = ReadRepositoryArtifact(
            root,
            cursor.PlanEnvelopePath,
            cursor.PlanEnvelopeRef,
            "A3 review plan envelope");
        PaperTheoryAuditReviewPlan envelope =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditReviewPlan>(envelopeBytes);
        Validate(envelope);
        if (!string.Equals(envelope.PlanId, plan.PlanId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A3 plan envelope does not match its content artifact.");
        }
        return plan;
    }

    private static PaperTheoryAuditOpinion ReadOpinion(
        string root,
        PaperTheoryAuditStoredArtifact stored)
    {
        ValidateStoredArtifact(stored, PaperTheoryAuditAgentSchemas.Opinion);
        byte[] contentBytes = ReadRepositoryArtifact(
            root,
            stored.ContentPath,
            stored.ArtifactRef,
            "A3 opinion content");
        PaperTheoryAuditOpinion content =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditOpinion>(contentBytes);
        byte[] envelopeBytes = ReadRepositoryArtifact(
            root,
            stored.EnvelopePath,
            stored.EnvelopeRef,
            "A3 opinion envelope");
        PaperTheoryAuditOpinionArtifact envelope =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditOpinionArtifact>(envelopeBytes);
        Validate(envelope);
        if (!string.Equals(envelope.OpinionId, stored.ArtifactRef, StringComparison.Ordinal)
            || !CanonicalJson.Serialize(envelope.OpinionContent).AsSpan().SequenceEqual(contentBytes))
        {
            throw new InvalidDataException(
                "Stored A3 opinion envelope differs from its content artifact.");
        }
        return content;
    }

    private static TEnvelope ReadEnvelope<TEnvelope>(
        string root,
        PaperTheoryAuditStoredArtifact stored)
    {
        byte[] bytes = ReadRepositoryArtifact(
            root,
            stored.EnvelopePath,
            stored.EnvelopeRef,
            $"{stored.Schema} envelope");
        return PaperResearchInputJson.DeserializeStrict<TEnvelope>(bytes);
    }

    private static PaperTheoryAuditOpinionCursor ReadOpinionCursor(string path)
    {
        PaperTheoryAuditOpinionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditOpinionCursor>(
                ReadBoundedFile(path, MaximumControlBytes, "A3 opinion cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperTheoryAuditAggregateCursor ReadAggregateCursor(string path)
    {
        PaperTheoryAuditAggregateCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryAuditAggregateCursor>(
                ReadBoundedFile(path, MaximumControlBytes, "A3 aggregate cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperAgentTask ReadRegisteredTask(string root, string taskRef)
    {
        byte[] bytes = ReadImmutable(
            AgentArtifactPath(root, "tasks", taskRef),
            taskRef,
            "Registered A3 agent task");
        PaperAgentTask task =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(bytes);
        PaperAgentRuntimeService.Validate(task);
        ValidateInputSources(root, task.ExactInputs);
        return task;
    }

    private static PaperAgentTaskCursor ReadAgentCursor(
        string root,
        PaperAgentTask task,
        string taskRef)
    {
        string path = Path.Combine(
            root,
            "work",
            "paper-agents",
            "cursors",
            Hex(taskRef) + ".json");
        PaperAgentTaskCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTaskCursor>(
                ReadBoundedFile(path, MaximumControlBytes, "Paper agent cursor"));
        PaperAgentRuntimeService.Validate(cursor, task, taskRef);
        return cursor;
    }

    private static PaperAgentResultWire ReadAgentResult(
        string root,
        PaperAgentTask task,
        string taskRef,
        string resultRef)
    {
        PaperAgentResultWire result =
            PaperResearchInputJson.DeserializeStrict<PaperAgentResultWire>(
                ReadImmutable(
                    AgentArtifactPath(root, "results", resultRef),
                    resultRef,
                    "A3 agent result"));
        PaperAgentRuntimeService.Validate(result, task, taskRef);
        return result;
    }

    private static byte[] ReadAgentOutput(string root, string outputRef) =>
        ReadImmutable(
            AgentArtifactPath(root, "outputs", outputRef),
            outputRef,
            "A3 opinion output");

    private static void RequireCursorMatchesResult(
        PaperAgentTaskCursor cursor,
        PaperAgentResultWire result)
    {
        if (!string.Equals(cursor.Status, result.Status, StringComparison.Ordinal)
            || !string.Equals(cursor.Summary, result.Summary, StringComparison.Ordinal)
            || !string.Equals(cursor.NextRoute, result.NextRoute, StringComparison.Ordinal)
            || !string.Equals(cursor.BlockerCode, result.BlockerCode, StringComparison.Ordinal)
            || !string.Equals(cursor.CompletedAt, result.CompletedAt, StringComparison.Ordinal)
            || cursor.Outputs.Count != result.Outputs.Count)
        {
            throw new InvalidDataException(
                "Paper agent cursor does not match its immutable A3 result.");
        }
        string[] cursorOutputs = cursor.Outputs
            .Select(output => $"{output.Schema}\n{output.WorkspaceRelativePath}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] resultOutputs = result.Outputs
            .Select(output => $"{output.Schema}\n{output.WorkspaceRelativePath}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!cursorOutputs.SequenceEqual(resultOutputs, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Paper agent cursor changed the A3 result output set.");
        }
    }

    private static PaperTheoryAuditStoredArtifact StoreDomain<TContent, TEnvelope>(
        string root,
        string family,
        string domainSchema,
        string domainRef,
        TContent content,
        TEnvelope envelope)
    {
        byte[] contentBytes = CanonicalJson.Serialize(content);
        if (!string.Equals(Reference(contentBytes), domainRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Canonical {domainSchema} content does not match its domain identifier.");
        }
        string contentPath = ArtifactPath(root, family, domainRef);
        _ = PutImmutable(contentPath, contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(envelope);
        string envelopeRef = Reference(envelopeBytes);
        string envelopePath = ArtifactPath(root, "envelopes", envelopeRef);
        _ = PutImmutable(envelopePath, envelopeBytes);
        return new PaperTheoryAuditStoredArtifact(
            domainSchema,
            domainRef,
            RelativePath(root, contentPath),
            envelopeRef,
            RelativePath(root, envelopePath));
    }

    private static void ValidateStoredArtifact(
        PaperTheoryAuditStoredArtifact artifact,
        string expectedSchema)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        RequireExact(artifact.Schema, expectedSchema, nameof(artifact.Schema));
        RequireDigest(artifact.ArtifactRef, nameof(artifact.ArtifactRef));
        RequireRepositoryRelativePath(artifact.ContentPath, nameof(artifact.ContentPath));
        RequireDigest(artifact.EnvelopeRef, nameof(artifact.EnvelopeRef));
        RequireRepositoryRelativePath(artifact.EnvelopePath, nameof(artifact.EnvelopePath));
    }

    private static PaperAgentInputArtifact RequiredInput(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        string schema,
        string artifactRef,
        string name)
    {
        PaperAgentInputArtifact? input = inputs.SingleOrDefault(value =>
            string.Equals(value.Schema, schema, StringComparison.Ordinal)
            && string.Equals(value.ArtifactRef, artifactRef, StringComparison.Ordinal));
        return input ?? throw new InvalidDataException(
            $"Theory-audit dispatch is missing the exact {name} artifact.");
    }

    private static PaperAgentInputArtifact RequiredInputRef(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        string artifactRef,
        string name)
    {
        PaperAgentInputArtifact? input = inputs.SingleOrDefault(value =>
            string.Equals(value.ArtifactRef, artifactRef, StringComparison.Ordinal));
        return input ?? throw new InvalidDataException(
            $"Theory-audit dispatch is missing the exact {name} artifact.");
    }

    private static void RequireInputRefsExactly(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        IReadOnlyList<string> expectedRefs)
    {
        string[] actual = inputs.Select(input => input.ArtifactRef)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expected = expectedRefs
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-audit dispatch changed the complete clean-room context closure.");
        }
    }

    private static void ValidateInputSources(
        string root,
        IReadOnlyList<PaperAgentInputArtifact> inputs)
    {
        foreach (PaperAgentInputArtifact input in inputs)
        {
            _ = ReadExactInput(root, input);
        }
    }

    private static byte[] ReadExactInput(
        string root,
        PaperAgentInputArtifact input) =>
        ReadRepositoryArtifact(
            root,
            input.RepositoryRelativePath,
            input.ArtifactRef,
            $"Exact input {input.Schema}");

    private static byte[] ReadRepositoryArtifact(
        string root,
        string relativePath,
        string expectedRef,
        string name)
    {
        RequireRepositoryRelativePath(relativePath, name);
        string full = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        RequirePathWithin(root, full, name);
        RejectReparsePointsBetween(root, full, name);
        return ReadImmutable(full, expectedRef, name);
    }

    private static string RequireRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new InvalidDataException("Paper repository root is required.");
        }
        string root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Paper repository root does not exist: {root}");
        }
        return root;
    }

    private static string RequireDispatchPath(string root, string dispatchPath)
    {
        if (string.IsNullOrWhiteSpace(dispatchPath))
        {
            throw new InvalidDataException("Theory-audit dispatch path is required.");
        }
        string full = Path.GetFullPath(dispatchPath);
        string inbox = Path.GetFullPath(Path.Combine(root, "inbox", "theory-audit"));
        RequirePathWithin(inbox, full, "Theory-audit dispatch path");
        if (!File.Exists(full)
            || !string.Equals(Path.GetExtension(full), ".json", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-audit dispatch must be an existing JSON file in its deployment inbox.");
        }
        RejectReparsePointsBetween(inbox, full, "Theory-audit dispatch path");
        return full;
    }

    private static string ArtifactPath(string root, string family, string reference)
    {
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-theory-audit",
            family,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string AgentArtifactPath(
        string root,
        string family,
        string reference)
    {
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-agents",
            family,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string PlanCursorPath(string root, string requestRef) =>
        Path.Combine(
            root,
            "work",
            "paper-theory-audit",
            "plans",
            Hex(requestRef) + ".json");

    private static string OpinionCursorPath(string root, string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-theory-audit",
            "opinions",
            Hex(taskRef) + ".json");

    private static string AggregateCursorPath(string root, string planRef) =>
        Path.Combine(
            root,
            "work",
            "paper-theory-audit",
            "aggregates",
            Hex(planRef) + ".json");

    private static bool PutImmutable(string path, ReadOnlySpan<byte> bytes)
    {
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {path}.");
            }
            return true;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
        return false;
    }

    private static byte[] ReadImmutable(
        string path,
        string expectedRef,
        string name)
    {
        RequireDigest(expectedRef, nameof(expectedRef));
        byte[] bytes = ReadBoundedFile(path, MaximumArtifactBytes, name);
        if (!string.Equals(Reference(bytes), expectedRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} failed content-address verification.");
        }
        return bytes;
    }

    private static byte[] ReadBoundedFile(
        string path,
        int maximumBytes,
        string name)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{name} is missing.", path);
        }
        var info = new FileInfo(path);
        if (info.Length < 1 || info.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"{name} must contain between one and {maximumBytes} bytes.");
        }
        return File.ReadAllBytes(path);
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string DomainReference(string domain, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(domain + "\0" + value);
        return Reference(bytes);
    }

    private static string Reference<T>(T value) =>
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(value));

    private static string Reference(ReadOnlySpan<byte> bytes) =>
        PaperResearchInputStore.Reference(bytes);

    private static string Hex(string reference)
    {
        RequireDigest(reference, nameof(reference));
        return reference["sha256:".Length..];
    }

    private static void RequireIdentity<T>(string reference, T content, string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, Reference(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} does not address canonical content bytes.");
        }
    }

    private static void RequireSchema(string value, string name)
    {
        if (!SchemaPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} is not a versioned schema name.");
        }
    }

    private static void RequirePaperId(string value)
    {
        if (!PaperIdPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException("paper_id is not a canonical identifier.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be sha256:<64 lowercase hex>.");
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

    private static void RequireText(
        string value,
        string name,
        int maximumLength,
        int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }

    private static void RequireFreshRunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 512
            || value.Contains('\n')
            || value.Contains('\r'))
        {
            throw new InvalidDataException(
                "A3 requires a nonempty bounded Codex run_id for clean-room independence.");
        }
    }

    private static void RequireProvenance(string value)
    {
        if (value is not "produced" and not "adopted")
        {
            throw new InvalidDataException("A3 agent provenance is invalid.");
        }
    }

    private static void RequireRepositoryRelativePath(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 4096
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || !RelativePathPattern.IsMatch(value))
        {
            throw new InvalidDataException($"{name} is not a canonical relative path.");
        }
        foreach (string segment in value.Split('/'))
        {
            if (segment is "." or ".." || segment.All(character => character == '.'))
            {
                throw new InvalidDataException($"{name} contains an unsafe path segment.");
            }
        }
        string first = value.Split('/')[0];
        if (!AllowedEvidenceRoots.Contains(first))
        {
            throw new InvalidDataException(
                $"{name} is outside the approved Paper evidence roots.");
        }
    }

    private static void RequirePathWithin(string root, string path, string name)
    {
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(path);
        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} escapes its owned filesystem boundary.");
        }
    }

    private static void RejectReparsePointsBetween(
        string boundaryRoot,
        string path,
        string name)
    {
        string root = Path.GetFullPath(boundaryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = Path.GetFullPath(path);
        RequirePathWithin(root, full, name);
        string relative = Path.GetRelativePath(root, full);
        string current = root;
        if (Directory.Exists(current) || File.Exists(current))
        {
            RejectReparsePoint(current, name);
        }
        foreach (string segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) || File.Exists(current))
            {
                RejectReparsePoint(current, name);
            }
        }
    }

    private static void RejectReparsePoint(string path, string name)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"{name} cannot traverse a symbolic link.");
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
                out DateTimeOffset parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 UTC timestamp.");
        }
        return parsed;
    }

    private static string MaxTimestamp(IEnumerable<string> values)
    {
        DateTimeOffset maximum = values
            .Select(value => ParseUtc(value, "aggregate timestamp"))
            .Max();
        return maximum.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
