using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public sealed record PaperPortfolioJudgmentPaperContext(
    PaperPortfolioJudgmentPaperInput Coordinates,
    PaperTheoryProgram Program,
    PaperTheoryScope Scope,
    PaperTheoryInventory Inventory,
    PaperTheoremPackage TheoremPackage,
    PaperTheoryAudit Audit,
    PaperCandidateScorecard Scorecard);

public sealed record PaperPortfolioJudgmentContext(
    PaperResearchPortfolio Portfolio,
    PaperCandidateBatch CandidateBatch,
    IReadOnlyList<PaperPortfolioJudgmentPaperContext> Papers);

public sealed record PaperPortfolioJudgmentComputation(
    PaperPortfolioJudgmentEvidence Evidence,
    PaperPortfolioDecision Decision,
    PaperResearchPortfolio UpdatedPortfolio,
    IReadOnlyList<PaperPortfolioJudgmentPaperRoute> Routes);

public static class PaperPortfolioJudgmentAgentService
{
    private const int MaximumControlBytes = 4 * 1024 * 1024;
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;
    private const int MinimumComparedPapers = 2;
    private const int MaximumComparedPapers = 5;

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
    private static readonly HashSet<string> AllowedEvidenceRoots = new(
        ["artifacts", "Papers", "work", "inbox", "contracts", "docs", "src", "tools", "tests"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> DraftActions = new(
        ["promote", "hold", "continue-deepening", "split", "merge", "park", "archive"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> PairRelations = new(
        ["distinct", "complementary", "overlapping", "duplicate"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> ProvenanceValues = new(
        ["produced", "adopted"],
        StringComparer.Ordinal);

    public static PaperPortfolioJudgmentAgentTaskStaged StageTask(
        string repositoryRoot,
        string dispatchPath)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        string fullDispatchPath = RequireDispatchPath(root, dispatchPath);
        byte[] dispatchBytes = ReadBoundedFile(
            fullDispatchPath,
            MaximumControlBytes,
            "Portfolio-judgment dispatch");
        string dispatchRef = ByteReference(dispatchBytes);
        PaperPortfolioJudgmentAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperPortfolioJudgmentAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);
        PaperPortfolioJudgmentContext context = LoadContext(root, dispatch);

        string immutableDispatchPath = ArtifactPath(
            root,
            "dispatches",
            dispatchRef);
        _ = PutImmutable(immutableDispatchPath, dispatchBytes);
        string dispatchRelativePath = RelativePath(root, immutableDispatchPath);
        PaperAgentTask task = BuildTask(
            dispatch,
            dispatchRef,
            dispatchRelativePath,
            context);
        PaperAgentRuntimeService.Validate(task);

        byte[] taskBytes = CanonicalJson.Serialize(task);
        string taskRef = ByteReference(taskBytes);
        string taskPath = Path.Combine(
            root,
            "inbox",
            "agent-tasks",
            $"portfolio-judgment-{dispatch.CycleNumber:D8}-{Hex(taskRef)}.json");
        bool replayed = PutImmutable(taskPath, taskBytes);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile(task.Phase);
        return new PaperPortfolioJudgmentAgentTaskStaged(
            PaperPortfolioJudgmentAgentSchemas.TaskStaged,
            dispatchRef,
            taskRef,
            taskPath,
            dispatch.PortfolioRef,
            dispatch.CandidateBatchRef,
            dispatch.CycleNumber,
            dispatch.Papers.Count,
            task.Phase,
            profile.AgentRole,
            profile.ContextMode,
            replayed);
    }

    public static PaperPortfolioJudgmentAgentResultAdmitted AdmitResult(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        if (!string.Equals(task.Phase, "portfolio-judgment", StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, "paper-portfolio-judge", StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, "cross-paper-comparison", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an FKST-native portfolio-judgment task can enter this admission bridge.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperPortfolioJudgmentAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Portfolio-judgment task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = ByteReference(dispatchBytes);
        PaperPortfolioJudgmentAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperPortfolioJudgmentAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);
        PaperPortfolioJudgmentContext context = LoadContext(root, dispatch);
        ValidateTaskBinding(
            task,
            dispatch,
            dispatchRef,
            dispatchInput.RepositoryRelativePath,
            context);

        PaperAgentTaskCursor agentCursor = ReadAgentCursor(root, task, taskRef);
        PaperAgentResultWire result = ReadAgentResult(
            root,
            task,
            taskRef,
            agentCursor.ResultRef);
        RequireCursorMatchesResult(agentCursor, result);
        if (!string.Equals(result.Status, "completed", StringComparison.Ordinal)
            || !string.Equals(result.NextRoute, "portfolio-decision", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a completed portfolio-decision result can be admitted.");
        }
        if (agentCursor.Outputs.Count != 1
            || !string.Equals(
                agentCursor.Outputs[0].Schema,
                PaperPortfolioJudgmentAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio judgment must return exactly one judgment draft.");
        }

        string admissionCursorPath = AdmissionCursorPath(root, taskRef);
        if (File.Exists(admissionCursorPath))
        {
            PaperPortfolioJudgmentAgentAdmissionCursor existing =
                ReadAdmissionCursor(admissionCursorPath);
            ValidateAdmissionReplay(
                root,
                existing,
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef);
            return ToAdmitted(existing, replayed: true);
        }

        byte[] draftBytes = ReadAgentOutput(
            root,
            agentCursor.Outputs[0].ArtifactRef);
        PaperPortfolioJudgmentDraft draft =
            PaperResearchInputJson.DeserializeStrict<PaperPortfolioJudgmentDraft>(
                draftBytes);
        PaperPortfolioJudgmentComputation computation = Compute(
            dispatch,
            dispatchRef,
            context,
            draft,
            agentCursor.ResultRef,
            result.CompletedAt);

        PaperPortfolioJudgmentStoredArtifact storedEvidence = StoreDomain(
            root,
            "evidence",
            computation.Evidence.Schema,
            computation.Evidence.EvidenceId,
            computation.Evidence.EvidenceContent,
            computation.Evidence);
        PaperPortfolioJudgmentStoredArtifact storedDecision = StoreDomain(
            root,
            "decisions",
            computation.Decision.Schema,
            computation.Decision.DecisionId,
            computation.Decision.DecisionContent,
            computation.Decision);
        PaperPortfolioJudgmentStoredArtifact storedPortfolio = StoreDomain(
            root,
            "portfolios",
            computation.UpdatedPortfolio.Schema,
            computation.UpdatedPortfolio.PortfolioId,
            computation.UpdatedPortfolio.PortfolioContent,
            computation.UpdatedPortfolio);

        var cursor = new PaperPortfolioJudgmentAgentAdmissionCursor(
            PaperPortfolioJudgmentAgentSchemas.AdmissionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.PortfolioRef,
            dispatch.CandidateBatchRef,
            dispatch.CycleNumber,
            storedEvidence,
            storedDecision,
            storedPortfolio,
            computation.Routes,
            agentCursor.RunId,
            agentCursor.Provenance,
            result.CompletedAt);
        Validate(cursor);
        try
        {
            PaperResearchInputStore.WriteAtomic(
                admissionCursorPath,
                CanonicalJson.Serialize(cursor),
                overwrite: false);
        }
        catch (IOException) when (File.Exists(admissionCursorPath))
        {
            PaperPortfolioJudgmentAgentAdmissionCursor existing =
                ReadAdmissionCursor(admissionCursorPath);
            ValidateAdmissionReplay(
                root,
                existing,
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef);
            return ToAdmitted(existing, replayed: true);
        }
        return ToAdmitted(cursor, replayed: false);
    }

    public static PaperPortfolioJudgmentComputation Compute(
        PaperPortfolioJudgmentAgentDispatch dispatch,
        string dispatchRef,
        PaperPortfolioJudgmentContext context,
        PaperPortfolioJudgmentDraft draft,
        string agentResultRef,
        string admittedAt)
    {
        Validate(dispatch);
        RequireDigest(dispatchRef, nameof(dispatchRef));
        RequireDigest(agentResultRef, nameof(agentResultRef));
        ParseUtc(admittedAt, nameof(admittedAt));
        ValidateDraft(dispatch, context, draft);

        var scorecards = context.Papers.ToDictionary(
            paper => paper.Coordinates.PaperId,
            paper => paper.Scorecard,
            StringComparer.Ordinal);
        int promotions = 0;
        PaperPortfolioPaperDecision[] decisions = draft.OrderedPapers
            .OrderBy(item => item.Rank)
            .Select(item =>
            {
                PaperCandidateScorecard scorecard = scorecards[item.PaperId];
                string action;
                if (scorecard.ScorecardContent.PromotionEligible
                    && promotions < dispatch.Policy.PromotionCapacity)
                {
                    promotions++;
                    action = "promote-to-frontier";
                }
                else if (scorecard.ScorecardContent.PromotionEligible)
                {
                    action = "hold";
                }
                else
                {
                    action = ScorecardActionToPortfolioAction(
                        scorecard.ScorecardContent.RecommendedAction);
                }
                return new PaperPortfolioPaperDecision(
                    item.PaperId,
                    scorecard.ScorecardContent.TheoryProgramRef,
                    scorecard.ScorecardId,
                    item.Rank,
                    scorecard.ScorecardContent.CompositeScore,
                    action,
                    $"cross-paper rank {item.Rank}; {item.Rationale}");
            })
            .ToArray();
        var decisionContent = new PaperPortfolioDecisionContent(
            context.Portfolio.PortfolioId,
            context.Portfolio.PortfolioContent.CandidateBatchRef,
            context.Portfolio.PortfolioContent.NextCycleNumber,
            dispatch.Policy,
            context.Papers
                .Select(paper => paper.Scorecard.ScorecardId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            decisions,
            admittedAt);
        var decision = new PaperPortfolioDecision(
            PaperPortfolioDecisionSchemas.Decision,
            ContentReference(decisionContent),
            decisionContent);
        PaperPortfolioDecisionService.Validate(decision);
        ValidateDecisionAgainstContext(decision, dispatch, context);

        var decisionsByPaper = decisions.ToDictionary(
            item => item.PaperId,
            StringComparer.Ordinal);
        PaperCandidateState[] updatedStates = context.Portfolio.PortfolioContent.CandidateStates
            .Select(state => decisionsByPaper.TryGetValue(
                    state.PaperId,
                    out PaperPortfolioPaperDecision? item)
                ? PaperPortfolioDecisionService.ApplyDecision(state, item, admittedAt)
                : state)
            .OrderBy(state => state.PaperId, StringComparer.Ordinal)
            .ToArray();
        PaperResearchPortfolioContent portfolioContent =
            context.Portfolio.PortfolioContent with
            {
                NextCycleNumber = context.Portfolio.PortfolioContent.NextCycleNumber + 1,
                CandidateStates = updatedStates,
                UpdatedAt = admittedAt
            };
        var updatedPortfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            ContentReference(portfolioContent),
            portfolioContent);
        PaperPortfolioService.Validate(updatedPortfolio);

        var evidenceContent = new PaperPortfolioJudgmentEvidenceContent(
            dispatchRef,
            agentResultRef,
            context.Portfolio.PortfolioId,
            context.CandidateBatch.BatchId,
            dispatch.CycleNumber,
            draft.ComparedScorecardRefs
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            draft.OrderedPapers
                .OrderBy(item => item.Rank)
                .Select(item => item.PaperId)
                .ToArray(),
            draft.PairwiseRelations
                .OrderBy(item => CanonicalPair(item.LeftPaperId, item.RightPaperId), StringComparer.Ordinal)
                .ToArray(),
            draft.PortfolioRationale,
            decision.DecisionId,
            admittedAt);
        var evidence = new PaperPortfolioJudgmentEvidence(
            PaperPortfolioJudgmentAgentSchemas.Evidence,
            ContentReference(evidenceContent),
            evidenceContent);
        Validate(evidence);

        PaperPortfolioJudgmentPaperRoute[] routes = decisions
            .Select(item => new PaperPortfolioJudgmentPaperRoute(
                item.Rank,
                item.PaperId,
                item.TheoryProgramRef,
                item.ScorecardRef,
                item.Action,
                ActionToRoute(item.Action),
                item.Reason))
            .ToArray();
        return new(evidence, decision, updatedPortfolio, routes);
    }

    public static void Validate(PaperPortfolioJudgmentAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperPortfolioJudgmentAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        RequireDigest(dispatch.PortfolioRef, nameof(dispatch.PortfolioRef));
        RequireDigest(dispatch.CandidateBatchRef, nameof(dispatch.CandidateBatchRef));
        if (dispatch.CycleNumber < 1)
        {
            throw new InvalidDataException("Portfolio judgment cycle_number must be positive.");
        }
        ValidatePolicy(dispatch.Policy, MaximumComparedPapers);
        if (dispatch.Papers is null
            || dispatch.Papers.Count is < MinimumComparedPapers or > MaximumComparedPapers)
        {
            throw new InvalidDataException(
                "Portfolio judgment must compare between two and five papers.");
        }
        var papers = new HashSet<string>(StringComparer.Ordinal);
        var programs = new HashSet<string>(StringComparer.Ordinal);
        var scorecards = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperPortfolioJudgmentPaperInput paper in dispatch.Papers)
        {
            ValidatePaperInput(paper);
            if (!papers.Add(paper.PaperId)
                || !programs.Add(paper.TheoryProgramRef)
                || !scorecards.Add(paper.ScorecardRef))
            {
                throw new InvalidDataException(
                    "Portfolio judgment requires distinct papers, programs, and scorecards.");
            }
        }
        if (dispatch.Policy.MinimumComparedPapers > dispatch.Papers.Count)
        {
            throw new InvalidDataException(
                "Portfolio judgment has fewer papers than its comparison policy requires.");
        }
        if (dispatch.ExactInputs is null || dispatch.ExactInputs.Count is < 18 or > 64)
        {
            throw new InvalidDataException(
                "Portfolio judgment exact input closure is outside its bounded size.");
        }
        ValidateInputCoordinates(dispatch.ExactInputs);
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    public static void Validate(PaperPortfolioJudgmentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireExact(draft.Schema, PaperPortfolioJudgmentAgentSchemas.Draft, nameof(draft.Schema));
        RequireDigest(draft.PortfolioRef, nameof(draft.PortfolioRef));
        RequireDigest(draft.CandidateBatchRef, nameof(draft.CandidateBatchRef));
        if (draft.CycleNumber < 1)
        {
            throw new InvalidDataException("Portfolio judgment draft cycle_number must be positive.");
        }
        RequireDigestList(draft.ComparedScorecardRefs, nameof(draft.ComparedScorecardRefs), 2, 5);
        if (draft.OrderedPapers is null
            || draft.OrderedPapers.Count is < MinimumComparedPapers or > MaximumComparedPapers)
        {
            throw new InvalidDataException("Portfolio judgment draft paper ordering is invalid.");
        }
        foreach (PaperPortfolioJudgmentPaperDraft item in draft.OrderedPapers)
        {
            ValidatePaperDraft(item);
        }
        if (draft.PairwiseRelations is null)
        {
            throw new InvalidDataException("Portfolio judgment pairwise relations are required.");
        }
        foreach (PaperPortfolioPairwiseRelationDraft relation in draft.PairwiseRelations)
        {
            ValidatePairwiseRelation(relation);
        }
        RequireText(draft.PortfolioRationale, nameof(draft.PortfolioRationale), 65536, 120);
        ParseUtc(draft.CreatedAt, nameof(draft.CreatedAt));
    }

    public static void Validate(PaperPortfolioJudgmentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        RequireExact(evidence.Schema, PaperPortfolioJudgmentAgentSchemas.Evidence, nameof(evidence.Schema));
        PaperPortfolioJudgmentEvidenceContent content = evidence.EvidenceContent
            ?? throw new InvalidDataException("evidence_content is required.");
        RequireDigest(content.DispatchRef, nameof(content.DispatchRef));
        RequireDigest(content.AgentResultRef, nameof(content.AgentResultRef));
        RequireDigest(content.PortfolioRef, nameof(content.PortfolioRef));
        RequireDigest(content.CandidateBatchRef, nameof(content.CandidateBatchRef));
        if (content.CycleNumber < 1)
        {
            throw new InvalidDataException("Portfolio judgment evidence cycle is invalid.");
        }
        RequireDigestList(content.ComparedScorecardRefs, nameof(content.ComparedScorecardRefs), 2, 5);
        RequirePaperIdList(content.RankedPaperIds, nameof(content.RankedPaperIds), 2, 5);
        foreach (PaperPortfolioPairwiseRelationDraft relation in content.PairwiseRelations)
        {
            ValidatePairwiseRelation(relation);
        }
        RequireText(content.PortfolioRationale, nameof(content.PortfolioRationale), 65536, 120);
        RequireDigest(content.DecisionRef, nameof(content.DecisionRef));
        ParseUtc(content.AdmittedAt, nameof(content.AdmittedAt));
        RequireIdentity(evidence.EvidenceId, content, nameof(evidence.EvidenceId));
    }

    public static void Validate(PaperPortfolioJudgmentAgentAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperPortfolioJudgmentAgentSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        RequireDigest(cursor.TaskRef, nameof(cursor.TaskRef));
        RequireDigest(cursor.ResultRef, nameof(cursor.ResultRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        RequireDigest(cursor.PortfolioRef, nameof(cursor.PortfolioRef));
        RequireDigest(cursor.CandidateBatchRef, nameof(cursor.CandidateBatchRef));
        if (cursor.CycleNumber < 1 || cursor.Routes is null || cursor.Routes.Count < 2)
        {
            throw new InvalidDataException("Portfolio judgment cursor cycle or routes are invalid.");
        }
        ValidateStoredArtifact(cursor.Evidence, PaperPortfolioJudgmentAgentSchemas.Evidence);
        ValidateStoredArtifact(cursor.Decision, PaperPortfolioDecisionSchemas.Decision);
        ValidateStoredArtifact(cursor.UpdatedPortfolio, PaperPortfolioSchemas.Portfolio);
        ValidateRoutes(cursor.Routes);
        RequireRunId(cursor.RunId);
        if (!ProvenanceValues.Contains(cursor.Provenance))
        {
            throw new InvalidDataException("Portfolio judgment provenance is invalid.");
        }
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static PaperPortfolioJudgmentContext LoadContext(
        string root,
        PaperPortfolioJudgmentAgentDispatch dispatch)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        string[] expectedRefs =
        [
            dispatch.PortfolioRef,
            dispatch.CandidateBatchRef,
            .. dispatch.Papers.SelectMany(paper => new[]
            {
                paper.TheoryProgramRef,
                paper.ScopeRef,
                paper.InventoryRef,
                paper.TheoremPackageRef,
                paper.TheoryAuditRef,
                paper.ScorecardRef,
                paper.CandidatePaperRef,
                paper.LiteratureResearchRef
            })
        ];
        RequireSameSet(
            dispatch.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
            expectedRefs,
            "portfolio judgment exact input closure");

        PaperResearchPortfolioContent portfolioContent = ReadContent<PaperResearchPortfolioContent>(
            root,
            FindInput(dispatch.ExactInputs, PaperPortfolioSchemas.Portfolio, dispatch.PortfolioRef));
        var portfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            dispatch.PortfolioRef,
            portfolioContent);
        PaperPortfolioService.Validate(portfolio);

        PaperCandidateBatchContent batchContent = ReadContent<PaperCandidateBatchContent>(
            root,
            FindInput(dispatch.ExactInputs, PaperPortfolioSchemas.CandidateBatch, dispatch.CandidateBatchRef));
        var batch = new PaperCandidateBatch(
            PaperPortfolioSchemas.CandidateBatch,
            dispatch.CandidateBatchRef,
            batchContent);
        PaperPortfolioService.Validate(batch);
        if (!string.Equals(
                portfolio.PortfolioContent.CandidateBatchRef,
                batch.BatchId,
                StringComparison.Ordinal)
            || !string.Equals(dispatch.CandidateBatchRef, batch.BatchId, StringComparison.Ordinal)
            || dispatch.CycleNumber != portfolio.PortfolioContent.NextCycleNumber)
        {
            throw new InvalidDataException(
                "Portfolio judgment changed its candidate batch or portfolio cycle.");
        }
        ValidatePolicy(dispatch.Policy, portfolio.PortfolioContent.Policy.MaxParallelPapers);

        var states = portfolio.PortfolioContent.CandidateStates.ToDictionary(
            state => state.PaperId,
            StringComparer.Ordinal);
        var contexts = new List<PaperPortfolioJudgmentPaperContext>();
        foreach (PaperPortfolioJudgmentPaperInput coordinates in dispatch.Papers)
        {
            PaperTheoryProgramContent programContent = ReadContent<PaperTheoryProgramContent>(
                root,
                FindInput(dispatch.ExactInputs, PaperPortfolioSchemas.TheoryProgram, coordinates.TheoryProgramRef));
            var program = new PaperTheoryProgram(
                PaperPortfolioSchemas.TheoryProgram,
                coordinates.TheoryProgramRef,
                programContent);
            PaperPortfolioService.Validate(program);

            PaperTheoryScopeContent scopeContent = ReadContent<PaperTheoryScopeContent>(
                root,
                FindInput(dispatch.ExactInputs, PaperTheoryFoundationSchemas.Scope, coordinates.ScopeRef));
            var scope = new PaperTheoryScope(
                PaperTheoryFoundationSchemas.Scope,
                coordinates.ScopeRef,
                scopeContent);
            PaperTheoryFoundationService.Validate(scope, program);

            PaperTheoryInventoryContent inventoryContent = ReadContent<PaperTheoryInventoryContent>(
                root,
                FindInput(dispatch.ExactInputs, PaperTheoryFoundationSchemas.Inventory, coordinates.InventoryRef));
            var inventory = new PaperTheoryInventory(
                PaperTheoryFoundationSchemas.Inventory,
                coordinates.InventoryRef,
                inventoryContent);
            PaperTheoryFoundationService.Validate(inventory);

            PaperTheoremPackageContent packageContent = ReadContent<PaperTheoremPackageContent>(
                root,
                FindInput(dispatch.ExactInputs, PaperTheoryDeepeningSchemas.TheoremPackage, coordinates.TheoremPackageRef));
            var package = new PaperTheoremPackage(
                PaperTheoryDeepeningSchemas.TheoremPackage,
                coordinates.TheoremPackageRef,
                packageContent);
            PaperTheoryDeepeningService.Validate(package);

            PaperTheoryAuditContent auditContent = ReadContent<PaperTheoryAuditContent>(
                root,
                FindInput(dispatch.ExactInputs, PaperTheoryAuditSchemas.Audit, coordinates.TheoryAuditRef));
            var audit = new PaperTheoryAudit(
                PaperTheoryAuditSchemas.Audit,
                coordinates.TheoryAuditRef,
                auditContent);
            PaperTheoryAuditService.Validate(audit);

            PaperCandidateScorecardContent scorecardContent = ReadContent<PaperCandidateScorecardContent>(
                root,
                FindInput(dispatch.ExactInputs, PaperPortfolioDecisionSchemas.Scorecard, coordinates.ScorecardRef));
            var scorecard = new PaperCandidateScorecard(
                PaperPortfolioDecisionSchemas.Scorecard,
                coordinates.ScorecardRef,
                scorecardContent);
            PaperPortfolioDecisionService.Validate(scorecard);

            _ = FindInput(
                dispatch.ExactInputs,
                CandidateArtifactSchemas.CandidatePaper,
                coordinates.CandidatePaperRef);
            _ = FindInput(
                dispatch.ExactInputs,
                CandidateArtifactSchemas.LiteratureResearch,
                coordinates.LiteratureResearchRef);

            if (!states.TryGetValue(coordinates.PaperId, out PaperCandidateState? state)
                || !string.Equals(state.Phase, "audit-pending", StringComparison.Ordinal)
                || !string.Equals(state.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
                || !string.Equals(program.ProgramContent.PaperId, coordinates.PaperId, StringComparison.Ordinal)
                || !string.Equals(program.ProgramContent.CandidateBatchRef, batch.BatchId, StringComparison.Ordinal)
                || !string.Equals(program.ProgramContent.CandidatePaperRef, coordinates.CandidatePaperRef, StringComparison.Ordinal)
                || !string.Equals(program.ProgramContent.LiteratureResearchRef, coordinates.LiteratureResearchRef, StringComparison.Ordinal)
                || !string.Equals(scope.ScopeContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
                || !string.Equals(inventory.InventoryContent.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
                || !string.Equals(package.TheoremPackageContent.InventoryRef, inventory.InventoryId, StringComparison.Ordinal)
                || !string.Equals(audit.AuditContent.TheoremPackageRef, package.TheoremPackageId, StringComparison.Ordinal)
                || !string.Equals(scorecard.ScorecardContent.TheoryAuditRef, audit.AuditId, StringComparison.Ordinal)
                || !string.Equals(scorecard.ScorecardContent.TheoremPackageRef, package.TheoremPackageId, StringComparison.Ordinal)
                || !string.Equals(scorecard.ScorecardContent.PaperId, coordinates.PaperId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Portfolio judgment paper evidence does not describe one audit-pending paper program.");
            }
            contexts.Add(new(
                coordinates,
                program,
                scope,
                inventory,
                package,
                audit,
                scorecard));
        }
        return new(portfolio, batch, contexts);
    }

    private static PaperAgentTask BuildTask(
        PaperPortfolioJudgmentAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperPortfolioJudgmentContext context)
    {
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile("portfolio-judgment");
        PaperAgentInputArtifact[] inputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperPortfolioJudgmentAgentSchemas.Dispatch,
                dispatchRef,
                dispatchRelativePath))
            .OrderBy(input => input.Schema, StringComparer.Ordinal)
            .ThenBy(input => input.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        string paperId = $"portfolio-cycle-{dispatch.CycleNumber:D8}";
        var instruction = new StringBuilder();
        instruction.AppendLine("Compare the entire admitted paper batch as one research portfolio.");
        instruction.AppendLine("Read every theorem package, independent A3 audit, scorecard, candidate paper, and literature artifact.");
        instruction.AppendLine("Rank papers by publication-level theorem contribution while preserving the repository score order: a lower composite score may never precede a higher score, although exact-score ties may be resolved by theorem-level comparative evidence.");
        instruction.AppendLine($"The portfolio promotion capacity is {dispatch.Policy.PromotionCapacity}. Mark exactly the first eligible papers within that capacity as promote and eligible overflow as hold.");
        instruction.AppendLine("For every unordered paper pair, classify the relationship as distinct, complementary, overlapping, or duplicate. Cite only exact input refs belonging to that pair.");
        instruction.AppendLine("For failed A3 papers, preserve the scorecard route exactly: continue-deepening, split, merge, park, or archive.");
        instruction.AppendLine("Explain comparative advantage, principal risk, theorem interaction, novelty interaction, and the portfolio-level allocation rationale.");
        instruction.AppendLine("Do not create a new theorem, change an audit score, invent literature, or promote a paper that failed A3.");
        instruction.AppendLine($"Return schema {PaperPortfolioJudgmentAgentSchemas.Draft} at outputs/portfolio-judgment-draft.json.");
        return new PaperAgentTask(
            PaperAgentSchemas.Task,
            paperId,
            dispatch.PortfolioRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            inputs,
            [new PaperAgentExpectedOutput(
                PaperPortfolioJudgmentAgentSchemas.Draft,
                "outputs/portfolio-judgment-draft.json")],
            ["portfolio-decision"],
            instruction.ToString(),
            [
                "Do not alter scorecard metrics, A3 verdicts, theorem-package identities, or exact input references.",
                "Do not place a lower composite score ahead of a higher score.",
                "Do not use prior portfolio decisions or unrecorded external information.",
                "Do not exceed promotion capacity or promote an ineligible paper.",
                "Do not omit any pairwise paper comparison.",
                "Do not run Lean, Formalize, Git, GitHub, or manuscript generation."
            ],
            dispatch.RequestedAt);
    }

    private static void ValidateTaskBinding(
        PaperAgentTask actual,
        PaperPortfolioJudgmentAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperPortfolioJudgmentContext context)
    {
        PaperAgentTask expected = BuildTask(
            dispatch,
            dispatchRef,
            dispatchRelativePath,
            context);
        if (!CanonicalJson.Serialize(actual).AsSpan().SequenceEqual(
                CanonicalJson.Serialize(expected)))
        {
            throw new InvalidDataException(
                "Portfolio-judgment task changed its dispatch-owned comparison contract.");
        }
    }

    private static void ValidateDraft(
        PaperPortfolioJudgmentAgentDispatch dispatch,
        PaperPortfolioJudgmentContext context,
        PaperPortfolioJudgmentDraft draft)
    {
        Validate(draft);
        if (!string.Equals(draft.PortfolioRef, dispatch.PortfolioRef, StringComparison.Ordinal)
            || !string.Equals(draft.CandidateBatchRef, dispatch.CandidateBatchRef, StringComparison.Ordinal)
            || draft.CycleNumber != dispatch.CycleNumber)
        {
            throw new InvalidDataException(
                "Portfolio judgment draft changed the portfolio, batch, or cycle.");
        }
        RequireSameSet(
            draft.ComparedScorecardRefs,
            context.Papers.Select(paper => paper.Scorecard.ScorecardId).ToArray(),
            "compared_scorecard_refs");
        if (draft.OrderedPapers.Count != context.Papers.Count)
        {
            throw new InvalidDataException(
                "Portfolio judgment must rank every compared paper exactly once.");
        }
        var contextByPaper = context.Papers.ToDictionary(
            paper => paper.Coordinates.PaperId,
            StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int priorComposite = int.MaxValue;
        int eligibleSeen = 0;
        for (int index = 0; index < draft.OrderedPapers.Count; index++)
        {
            PaperPortfolioJudgmentPaperDraft item = draft.OrderedPapers[index];
            if (item.Rank != index + 1
                || !seen.Add(item.PaperId)
                || !contextByPaper.TryGetValue(
                    item.PaperId,
                    out PaperPortfolioJudgmentPaperContext? paper)
                || !string.Equals(item.ScorecardRef, paper.Scorecard.ScorecardId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Portfolio judgment ordering changed a paper, rank, or scorecard identity.");
            }
            int composite = paper.Scorecard.ScorecardContent.CompositeScore;
            if (composite > priorComposite)
            {
                throw new InvalidDataException(
                    "Portfolio judgment cannot rank a lower composite score before a higher score.");
            }
            priorComposite = composite;
            string expectedRecommendation;
            if (paper.Scorecard.ScorecardContent.PromotionEligible)
            {
                eligibleSeen++;
                expectedRecommendation = eligibleSeen <= dispatch.Policy.PromotionCapacity
                    ? "promote"
                    : "hold";
            }
            else
            {
                expectedRecommendation = paper.Scorecard.ScorecardContent.RecommendedAction;
            }
            if (!string.Equals(
                    item.RecommendedAction,
                    expectedRecommendation,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Portfolio judgment recommendation conflicts with the deterministic hard gate.");
            }
        }

        int expectedPairs = context.Papers.Count * (context.Papers.Count - 1) / 2;
        if (draft.PairwiseRelations.Count != expectedPairs)
        {
            throw new InvalidDataException(
                "Portfolio judgment must compare every unordered paper pair exactly once.");
        }
        var pairKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperPortfolioPairwiseRelationDraft relation in draft.PairwiseRelations)
        {
            if (!contextByPaper.ContainsKey(relation.LeftPaperId)
                || !contextByPaper.ContainsKey(relation.RightPaperId)
                || !pairKeys.Add(CanonicalPair(relation.LeftPaperId, relation.RightPaperId)))
            {
                throw new InvalidDataException(
                    "Portfolio judgment pairwise relation is duplicated or references an unknown paper.");
            }
            PaperPortfolioJudgmentPaperContext left = contextByPaper[relation.LeftPaperId];
            PaperPortfolioJudgmentPaperContext right = contextByPaper[relation.RightPaperId];
            string[] permittedEvidence = PairEvidence(left)
                .Concat(PairEvidence(right))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (relation.EvidenceRefs.Any(reference =>
                    !permittedEvidence.Contains(reference, StringComparer.Ordinal)))
            {
                throw new InvalidDataException(
                    "Pairwise relation cites evidence outside the two compared papers.");
            }
        }
        DateTimeOffset created = ParseUtc(draft.CreatedAt, nameof(draft.CreatedAt));
        if (created < ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt)))
        {
            throw new InvalidDataException(
                "Portfolio judgment draft cannot predate its dispatch.");
        }
    }

    private static void ValidateDecisionAgainstContext(
        PaperPortfolioDecision decision,
        PaperPortfolioJudgmentAgentDispatch dispatch,
        PaperPortfolioJudgmentContext context)
    {
        if (!string.Equals(decision.DecisionContent.PortfolioRef, context.Portfolio.PortfolioId, StringComparison.Ordinal)
            || !string.Equals(decision.DecisionContent.CandidateBatchRef, context.CandidateBatch.BatchId, StringComparison.Ordinal)
            || decision.DecisionContent.CycleNumber != dispatch.CycleNumber
            || decision.DecisionContent.Policy != dispatch.Policy
            || decision.DecisionContent.Decisions.Count != context.Papers.Count)
        {
            throw new InvalidDataException(
                "Portfolio decision changed its portfolio comparison coordinates.");
        }
        var scorecards = context.Papers.ToDictionary(
  paper => paper.Scorecard.ScorecardId,
  paper => paper.Scorecard,
  StringComparer.Ordinal);
        int promotions = 0;
        foreach (PaperPortfolioPaperDecision item in decision.DecisionContent.Decisions)
        {
            if (!scorecards.TryGetValue(item.ScorecardRef, out PaperCandidateScorecard? scorecard)
                || !string.Equals(item.PaperId, scorecard.ScorecardContent.PaperId, StringComparison.Ordinal)
                || !string.Equals(item.TheoryProgramRef, scorecard.ScorecardContent.TheoryProgramRef, StringComparison.Ordinal)
                || item.CompositeScore != scorecard.ScorecardContent.CompositeScore)
            {
                throw new InvalidDataException(
                    "Portfolio decision no longer matches its admitted scorecard.");
            }
            if (string.Equals(item.Action, "promote-to-frontier", StringComparison.Ordinal))
            {
                promotions++;
                if (!scorecard.ScorecardContent.PromotionEligible)
                {
                    throw new InvalidDataException(
                        "Portfolio decision promoted a paper that failed A3.");
                }
            }
        }
        if (promotions > dispatch.Policy.PromotionCapacity)
        {
            throw new InvalidDataException(
                "Portfolio decision exceeds its promotion capacity.");
        }
    }

    private static string[] PairEvidence(PaperPortfolioJudgmentPaperContext paper) =>
    [
        paper.Coordinates.CandidatePaperRef,
        paper.Coordinates.LiteratureResearchRef,
        paper.Program.TheoryProgramId,
        paper.Scope.ScopeId,
        paper.Inventory.InventoryId,
        paper.TheoremPackage.TheoremPackageId,
        paper.Audit.AuditId,
        paper.Scorecard.ScorecardId
    ];

    private static string ScorecardActionToPortfolioAction(string action) =>
        action switch
        {
            "promote" => "promote-to-frontier",
            "continue-deepening" => "continue-deepening",
            "split" => "split",
            "merge" => "merge",
            "park" => "park",
            "archive" => "archive",
            _ => throw new InvalidDataException(
                $"Unsupported scorecard action {action}.")
        };

    private static string ActionToRoute(string action) =>
        action switch
        {
            "promote-to-frontier" => "frontier-planning",
            "hold" => "portfolio-judgment",
            "continue-deepening" => "theory-deepening",
            "split" => "portfolio-split",
            "merge" => "portfolio-merge",
            "park" => "parked",
            "archive" => "archived",
            _ => throw new InvalidDataException(
                $"Unsupported portfolio action {action}.")
        };

    private static void ValidatePaperInput(PaperPortfolioJudgmentPaperInput paper)
    {
        ArgumentNullException.ThrowIfNull(paper);
        RequirePaperId(paper.PaperId);
        RequireDigest(paper.TheoryProgramRef, nameof(paper.TheoryProgramRef));
        RequireDigest(paper.ScopeRef, nameof(paper.ScopeRef));
        RequireDigest(paper.InventoryRef, nameof(paper.InventoryRef));
        RequireDigest(paper.TheoremPackageRef, nameof(paper.TheoremPackageRef));
        RequireDigest(paper.TheoryAuditRef, nameof(paper.TheoryAuditRef));
        RequireDigest(paper.ScorecardRef, nameof(paper.ScorecardRef));
        RequireDigest(paper.CandidatePaperRef, nameof(paper.CandidatePaperRef));
        RequireDigest(paper.LiteratureResearchRef, nameof(paper.LiteratureResearchRef));
    }

    private static void ValidatePaperDraft(PaperPortfolioJudgmentPaperDraft item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Rank < 1)
        {
            throw new InvalidDataException("Portfolio judgment rank must be positive.");
        }
        RequirePaperId(item.PaperId);
        RequireDigest(item.ScorecardRef, nameof(item.ScorecardRef));
        if (!DraftActions.Contains(item.RecommendedAction))
        {
            throw new InvalidDataException(
                "Portfolio judgment recommended_action is unsupported.");
        }
        RequireText(item.ComparativeAdvantage, nameof(item.ComparativeAdvantage), 16384, 40);
        RequireText(item.PrincipalRisk, nameof(item.PrincipalRisk), 16384, 20);
        RequireText(item.Rationale, nameof(item.Rationale), 16384, 40);
    }

    private static void ValidatePairwiseRelation(PaperPortfolioPairwiseRelationDraft relation)
    {
        ArgumentNullException.ThrowIfNull(relation);
        RequirePaperId(relation.LeftPaperId);
        RequirePaperId(relation.RightPaperId);
        if (string.Equals(relation.LeftPaperId, relation.RightPaperId, StringComparison.Ordinal)
            || !PairRelations.Contains(relation.Relation))
        {
            throw new InvalidDataException(
                "Portfolio pairwise relation has invalid papers or relation kind.");
        }
        if (relation.Relation is "overlapping" or "duplicate")
        {
            if (!string.Equals(relation.PreferredOwnerPaperId, relation.LeftPaperId, StringComparison.Ordinal)
                && !string.Equals(relation.PreferredOwnerPaperId, relation.RightPaperId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Overlapping or duplicate papers require one preferred owner.");
            }
        }
        else if (!string.IsNullOrEmpty(relation.PreferredOwnerPaperId))
        {
            throw new InvalidDataException(
                "Distinct or complementary papers cannot declare a preferred owner.");
        }
        RequireDigestList(relation.EvidenceRefs, nameof(relation.EvidenceRefs), 2, 16);
        RequireText(relation.TheoremInteraction, nameof(relation.TheoremInteraction), 32768, 60);
        RequireText(relation.NoveltyInteraction, nameof(relation.NoveltyInteraction), 32768, 60);
    }

    private static void ValidatePolicy(
        PaperPortfolioDecisionPolicy policy,
        int maximumParallelPapers)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.PromotionCapacity < 1
            || policy.PromotionCapacity > maximumParallelPapers
            || policy.MinimumComparedPapers < 2
            || policy.MinimumComparedPapers > MaximumComparedPapers)
        {
            throw new InvalidDataException(
                "Portfolio judgment policy is outside its bounded ranges.");
        }
    }

    private static void ValidateInputCoordinates(
        IReadOnlyList<PaperAgentInputArtifact> inputs)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperAgentInputArtifact input in inputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            RequireSchema(input.Schema, nameof(input.Schema));
            RequireDigest(input.ArtifactRef, nameof(input.ArtifactRef));
            RequireRepositoryRelativePath(input.RepositoryRelativePath, nameof(input.RepositoryRelativePath));
            if (!refs.Add(input.ArtifactRef) || !paths.Add(input.RepositoryRelativePath))
            {
                throw new InvalidDataException(
                    "Portfolio judgment input refs and paths must be unique.");
            }
        }
    }

    private static void ValidateRoutes(
        IReadOnlyList<PaperPortfolioJudgmentPaperRoute> routes)
    {
        var papers = new HashSet<string>(StringComparer.Ordinal);
        var scorecards = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < routes.Count; index++)
        {
            PaperPortfolioJudgmentPaperRoute route = routes[index];
            if (route.Rank != index + 1
                || !papers.Add(route.PaperId)
                || !scorecards.Add(route.ScorecardRef))
            {
                throw new InvalidDataException(
                    "Portfolio judgment routes must be ranked and unique.");
            }
            RequirePaperId(route.PaperId);
            RequireDigest(route.TheoryProgramRef, nameof(route.TheoryProgramRef));
            RequireDigest(route.ScorecardRef, nameof(route.ScorecardRef));
            RequireText(route.Action, nameof(route.Action), 128, 1);
            RequireExact(route.NextRoute, ActionToRoute(route.Action), nameof(route.NextRoute));
            RequireText(route.Reason, nameof(route.Reason), 8192, 1);
        }
    }

    private static PaperPortfolioJudgmentAgentResultAdmitted ToAdmitted(
        PaperPortfolioJudgmentAgentAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperPortfolioJudgmentAgentSchemas.ResultAdmitted,
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.PortfolioRef,
            cursor.CandidateBatchRef,
            cursor.CycleNumber,
            cursor.Evidence,
            cursor.Decision,
            cursor.UpdatedPortfolio,
            cursor.Routes,
            cursor.RunId,
            cursor.Provenance,
            cursor.AdmittedAt,
            replayed);

    private static PaperPortfolioJudgmentAgentAdmissionCursor ReadAdmissionCursor(
        string path)
    {
        PaperPortfolioJudgmentAgentAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperPortfolioJudgmentAgentAdmissionCursor>(
                ReadBoundedFile(path, MaximumControlBytes, "Portfolio judgment admission cursor"));
        Validate(cursor);
        return cursor;
    }

    private static void ValidateAdmissionReplay(
        string root,
        PaperPortfolioJudgmentAgentAdmissionCursor cursor,
        string taskRef,
        PaperAgentTaskCursor agentCursor,
        PaperPortfolioJudgmentAgentDispatch dispatch,
        string dispatchRef)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, taskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PortfolioRef, dispatch.PortfolioRef, StringComparison.Ordinal)
            || !string.Equals(cursor.CandidateBatchRef, dispatch.CandidateBatchRef, StringComparison.Ordinal)
            || cursor.CycleNumber != dispatch.CycleNumber
            || !string.Equals(cursor.RunId, agentCursor.RunId, StringComparison.Ordinal)
            || !string.Equals(cursor.Provenance, agentCursor.Provenance, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio judgment admission cursor changed task, result, dispatch, or run identity.");
        }
        _ = ReadStoredEnvelope<PaperPortfolioJudgmentEvidence>(root, cursor.Evidence);
        _ = ReadStoredEnvelope<PaperPortfolioDecision>(root, cursor.Decision);
        _ = ReadStoredEnvelope<PaperResearchPortfolio>(root, cursor.UpdatedPortfolio);
    }

    private static PaperPortfolioJudgmentStoredArtifact StoreDomain<TContent, TEnvelope>(
        string root,
        string family,
        string schema,
        string artifactRef,
        TContent content,
        TEnvelope envelope)
    {
        byte[] contentBytes = CanonicalJson.Serialize(content);
        if (!string.Equals(ByteReference(contentBytes), artifactRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{schema} content identity does not match its canonical bytes.");
        }
        string contentPath = DomainArtifactPath(root, family, "content", artifactRef);
        _ = PutImmutable(contentPath, contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(envelope);
        string envelopeRef = ByteReference(envelopeBytes);
        string envelopePath = DomainArtifactPath(root, family, "envelopes", envelopeRef);
        _ = PutImmutable(envelopePath, envelopeBytes);
        return new PaperPortfolioJudgmentStoredArtifact(
            schema,
            artifactRef,
            RelativePath(root, contentPath),
            envelopeRef,
            RelativePath(root, envelopePath));
    }

    private static T ReadStoredEnvelope<T>(
        string root,
        PaperPortfolioJudgmentStoredArtifact stored)
    {
        ValidateStoredArtifact(stored, stored.Schema);
        byte[] bytes = ReadRepositoryArtifact(
            root,
            stored.EnvelopePath,
            stored.EnvelopeRef,
            "Portfolio judgment stored envelope");
        return PaperResearchInputJson.DeserializeStrict<T>(bytes);
    }

    private static void ValidateStoredArtifact(
        PaperPortfolioJudgmentStoredArtifact stored,
        string expectedSchema)
    {
        ArgumentNullException.ThrowIfNull(stored);
        RequireExact(stored.Schema, expectedSchema, nameof(stored.Schema));
        RequireDigest(stored.ArtifactRef, nameof(stored.ArtifactRef));
        RequireRepositoryRelativePath(stored.ContentPath, nameof(stored.ContentPath));
        RequireDigest(stored.EnvelopeRef, nameof(stored.EnvelopeRef));
        RequireRepositoryRelativePath(stored.EnvelopePath, nameof(stored.EnvelopePath));
    }

    private static PaperAgentTask ReadRegisteredTask(string root, string taskRef)
    {
        byte[] bytes = ReadImmutable(
            GenericAgentArtifactPath(root, "tasks", taskRef),
            taskRef,
            "Registered portfolio-judgment task");
        PaperAgentTask task = PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(bytes);
        PaperAgentRuntimeService.Validate(task);
        return task;
    }

    private static PaperAgentTaskCursor ReadAgentCursor(
        string root,
        PaperAgentTask task,
        string taskRef)
    {
        PaperAgentTaskCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTaskCursor>(
                ReadBoundedFile(
                    GenericAgentCursorPath(root, taskRef),
                    MaximumControlBytes,
                    "Portfolio-judgment generic agent cursor"));
        PaperAgentRuntimeService.Validate(cursor, task, taskRef);
        return cursor;
    }

    private static PaperAgentResultWire ReadAgentResult(
        string root,
        PaperAgentTask task,
        string taskRef,
        string resultRef)
    {
        byte[] bytes = ReadImmutable(
            GenericAgentArtifactPath(root, "results", resultRef),
            resultRef,
            "Portfolio-judgment generic agent result");
        PaperAgentResultWire result =
            PaperResearchInputJson.DeserializeStrict<PaperAgentResultWire>(bytes);
        PaperAgentRuntimeService.Validate(result, task, taskRef);
        return result;
    }

    private static void RequireCursorMatchesResult(
        PaperAgentTaskCursor cursor,
        PaperAgentResultWire result)
    {
        if (!string.Equals(cursor.Status, result.Status, StringComparison.Ordinal)
            || !string.Equals(cursor.Summary, result.Summary, StringComparison.Ordinal)
            || !string.Equals(cursor.NextRoute, result.NextRoute, StringComparison.Ordinal)
            || !string.Equals(cursor.BlockerCode, result.BlockerCode, StringComparison.Ordinal)
            || !string.Equals(cursor.CompletedAt, result.CompletedAt, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio-judgment generic cursor does not match its immutable result.");
        }
    }

    private static byte[] ReadAgentOutput(string root, string outputRef) =>
        ReadImmutable(
            GenericAgentArtifactPath(root, "outputs", outputRef),
            outputRef,
            "Portfolio-judgment draft output");

    private static T ReadContent<T>(
        string root,
        PaperAgentInputArtifact input) =>
        PaperResearchInputJson.DeserializeStrict<T>(ReadExactInput(root, input));

    private static byte[] ReadExactInput(
        string root,
        PaperAgentInputArtifact input) =>
        ReadRepositoryArtifact(
            root,
            input.RepositoryRelativePath,
            input.ArtifactRef,
            $"Exact input {input.Schema}");

    private static PaperAgentInputArtifact FindInput(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        string schema,
        string reference) =>
        inputs.SingleOrDefault(input =>
                string.Equals(input.Schema, schema, StringComparison.Ordinal)
                && string.Equals(input.ArtifactRef, reference, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Portfolio judgment is missing exact input {schema} at {reference}.");

    private static void ValidateInputSources(
        string root,
        IReadOnlyList<PaperAgentInputArtifact> inputs)
    {
        foreach (PaperAgentInputArtifact input in inputs)
        {
            _ = ReadExactInput(root, input);
        }
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
            throw new InvalidDataException("Portfolio judgment dispatch path is required.");
        }
        string full = Path.GetFullPath(dispatchPath);
        string inbox = Path.GetFullPath(Path.Combine(root, "inbox", "portfolio-judgments"));
        RequirePathWithin(inbox, full, "Portfolio judgment dispatch path");
        if (!File.Exists(full)
            || !string.Equals(Path.GetExtension(full), ".json", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio judgment dispatch must be an existing JSON file in its deployment inbox.");
        }
        RejectReparsePointsBetween(inbox, full, "Portfolio judgment dispatch path");
        return full;
    }

    private static byte[] ReadRepositoryArtifact(
        string root,
        string relativePath,
        string expectedRef,
        string name)
    {
        RequireRepositoryRelativePath(relativePath, name);
        string first = relativePath.Split('/')[0];
        if (!AllowedEvidenceRoots.Contains(first))
        {
            throw new InvalidDataException($"{name} is outside approved evidence roots.");
        }
        string full = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        RequirePathWithin(root, full, name);
        RejectReparsePointsBetween(root, full, name);
        return ReadImmutable(full, expectedRef, name);
    }

    private static string ArtifactPath(string root, string family, string reference) =>
        DomainArtifactPath(root, family, "raw", reference);

    private static string DomainArtifactPath(
        string root,
        string family,
        string kind,
        string reference)
    {
        RequireDigest(reference, nameof(reference));
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-portfolio-judgments",
            family,
            kind,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string GenericAgentArtifactPath(
        string root,
        string family,
        string reference)
    {
        RequireDigest(reference, nameof(reference));
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

    private static string GenericAgentCursorPath(string root, string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-agents",
            "cursors",
            Hex(taskRef) + ".json");

    private static string AdmissionCursorPath(string root, string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-portfolio-judgments",
            "cursors",
            Hex(taskRef) + ".json");

    private static bool PutImmutable(string path, ReadOnlySpan<byte> bytes)
    {
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException($"Content-address collision at {path}.");
            }
            return true;
        }
        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
        return false;
    }

    private static byte[] ReadImmutable(
        string path,
        string expectedRef,
        string name)
    {
        byte[] bytes = ReadBoundedFile(path, MaximumArtifactBytes, name);
        string actual = ByteReference(bytes);
        if (!string.Equals(actual, expectedRef, StringComparison.Ordinal))
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
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

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

    private static string ByteReference(ReadOnlySpan<byte> bytes) =>
        PaperResearchInputStore.Reference(bytes);

    private static string ContentReference<T>(T content) =>
        ByteReference(CanonicalJson.Serialize(content));

    private static void RequireIdentity<T>(string reference, T content, string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, ContentReference(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} does not address canonical content bytes.");
        }
    }

    private static string Hex(string reference) => reference["sha256:".Length..];

    private static string CanonicalPair(string left, string right) =>
        string.CompareOrdinal(left, right) < 0
            ? left + "\0" + right
            : right + "\0" + left;

    private static void RequireSameSet(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        string name)
    {
        if (actual.Count != expected.Count
            || !actual.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{name} changed its exact evidence set.");
        }
    }

    private static void RequireDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum,
        int maximum)
    {
        if (values is null || values.Count < minimum || values.Count > maximum)
        {
            throw new InvalidDataException($"{name} has an invalid count.");
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

    private static void RequirePaperIdList(
        IReadOnlyList<string>? values,
        string name,
        int minimum,
        int maximum)
    {
        if (values is null || values.Count < minimum || values.Count > maximum)
        {
            throw new InvalidDataException($"{name} has an invalid count.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequirePaperId(value);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
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
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireSchema(string value, string name)
    {
        if (!SchemaPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} is not a versioned schema name.");
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
    }

    private static void RequireText(
        string value,
        string name,
        int maximumLength,
        int minimumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static void RequireRunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 512
            || value.Contains('\n')
            || value.Contains('\r'))
        {
            throw new InvalidDataException("Portfolio judgment run_id is invalid.");
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
