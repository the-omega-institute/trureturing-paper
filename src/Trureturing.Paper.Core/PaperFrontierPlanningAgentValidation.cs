using System.Globalization;

namespace Trureturing.Paper.Core;

public static partial class PaperFrontierPlanningAgentService
{
    private static readonly HashSet<string> DraftFormalizationKinds = new(
        [
            "definition",
            "prerequisite",
            "structural",
            "main-theorem",
            "sharpness",
            "corollary",
            "counterexample",
            "proof-interface"
        ],
        StringComparer.Ordinal);

    public static void Validate(PaperFrontierPlanningAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperFrontierPlanningAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        RequireDigest(dispatch.PortfolioTaskRef, nameof(dispatch.PortfolioTaskRef));
        RequireDigest(dispatch.PortfolioResultRef, nameof(dispatch.PortfolioResultRef));
        RequireDigest(dispatch.PortfolioCursorRef, nameof(dispatch.PortfolioCursorRef));
        RequireDigest(dispatch.PortfolioDispatchRef, nameof(dispatch.PortfolioDispatchRef));
        RequireDigest(dispatch.PortfolioRef, nameof(dispatch.PortfolioRef));
        RequireDigest(dispatch.CandidateBatchRef, nameof(dispatch.CandidateBatchRef));
        if (dispatch.CycleNumber < 1)
        {
            throw new InvalidDataException(
                "Frontier-planning cycle_number must be positive.");
        }
        RequireDigest(dispatch.JudgmentEvidenceRef, nameof(dispatch.JudgmentEvidenceRef));
        RequireDigest(dispatch.PortfolioDecisionRef, nameof(dispatch.PortfolioDecisionRef));
        RequireDigest(dispatch.UpdatedPortfolioRef, nameof(dispatch.UpdatedPortfolioRef));
        RequirePaperId(dispatch.PaperId);
        RequireDigest(dispatch.TheoryProgramRef, nameof(dispatch.TheoryProgramRef));
        RequireDigest(dispatch.ScopeRef, nameof(dispatch.ScopeRef));
        RequireDigest(dispatch.InventoryRef, nameof(dispatch.InventoryRef));
        RequireDigest(dispatch.TheoremPackageRef, nameof(dispatch.TheoremPackageRef));
        RequireDigest(dispatch.TheoryAuditRef, nameof(dispatch.TheoryAuditRef));
        RequireDigest(dispatch.ScorecardRef, nameof(dispatch.ScorecardRef));
        RequireDigest(dispatch.CandidatePaperRef, nameof(dispatch.CandidatePaperRef));
        RequireDigest(dispatch.LiteratureResearchRef, nameof(dispatch.LiteratureResearchRef));
        if (dispatch.ExactInputs is null
            || dispatch.ExactInputs.Count != ExactInputCount)
        {
            throw new InvalidDataException(
                $"Frontier planning requires exactly {ExactInputCount} exact inputs.");
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
                    "Frontier-planning exact input refs and paths must be unique.");
            }
        }
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    public static void Validate(PaperFormalizationFrontierDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireExact(
            draft.Schema,
            PaperFrontierPlanningAgentSchemas.Draft,
            nameof(draft.Schema));
        RequireDigest(draft.DispatchRef, nameof(draft.DispatchRef));
        RequirePaperId(draft.PaperId);
        RequireDigest(draft.TheoryProgramRef, nameof(draft.TheoryProgramRef));
        RequireDigest(draft.TheoremPackageRef, nameof(draft.TheoremPackageRef));
        RequireDigest(draft.TheoryAuditRef, nameof(draft.TheoryAuditRef));
        RequireDigest(draft.ScorecardRef, nameof(draft.ScorecardRef));
        RequireDigest(draft.PortfolioDecisionRef, nameof(draft.PortfolioDecisionRef));
        if (draft.NodeSpecs is null || draft.NodeSpecs.Count is < 3 or > 128)
        {
            throw new InvalidDataException(
                "Frontier draft must contain between three and 128 node specifications.");
        }
        var claimIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperFormalizationFrontierNodeSpec spec in draft.NodeSpecs)
        {
            ArgumentNullException.ThrowIfNull(spec);
            if (!ClaimIdPattern.IsMatch(spec.ClaimId ?? string.Empty)
                || !claimIds.Add(spec.ClaimId))
            {
                throw new InvalidDataException(
                    "Frontier draft claim identifiers must be canonical and unique.");
            }
            if (!DraftFormalizationKinds.Contains(spec.FormalizationKind)
                || spec.Priority is < 0 or > 100)
            {
                throw new InvalidDataException(
                    "Frontier draft formalization kind or priority is invalid.");
            }
            RequireText(spec.TargetLeanPackage, nameof(spec.TargetLeanPackage), 512, 1);
            RequireText(spec.TargetLeanModule, nameof(spec.TargetLeanModule), 1024, 1);
            RequireText(spec.FormalStatement, nameof(spec.FormalStatement), 32768, 20);
            RequireText(spec.AcceptanceCriterion, nameof(spec.AcceptanceCriterion), 32768, 20);
        }
        RequireText(draft.PlanningRationale, nameof(draft.PlanningRationale), 65536, 80);
        RequireTextList(draft.RiskLedger, nameof(draft.RiskLedger), 16384, 0, 32);
        ParseUtc(draft.CreatedAt, nameof(draft.CreatedAt));
    }

    public static void Validate(PaperFrontierPlanningAgentAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperFrontierPlanningAgentSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        RequireDigest(cursor.TaskRef, nameof(cursor.TaskRef));
        RequireDigest(cursor.ResultRef, nameof(cursor.ResultRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        RequireDigest(cursor.PortfolioTaskRef, nameof(cursor.PortfolioTaskRef));
        RequireDigest(cursor.PortfolioResultRef, nameof(cursor.PortfolioResultRef));
        RequireDigest(cursor.PortfolioRef, nameof(cursor.PortfolioRef));
        if (cursor.CycleNumber < 1)
        {
            throw new InvalidDataException("Frontier-planning cursor cycle is invalid.");
        }
        RequireDigest(cursor.JudgmentEvidenceRef, nameof(cursor.JudgmentEvidenceRef));
        RequireDigest(cursor.UpdatedPortfolioRef, nameof(cursor.UpdatedPortfolioRef));
        RequirePaperId(cursor.PaperId);
        RequireDigest(cursor.TheoryProgramRef, nameof(cursor.TheoryProgramRef));
        RequireDigest(cursor.TheoremPackageRef, nameof(cursor.TheoremPackageRef));
        RequireDigest(cursor.TheoryAuditRef, nameof(cursor.TheoryAuditRef));
        RequireDigest(cursor.ScorecardRef, nameof(cursor.ScorecardRef));
        RequireDigest(cursor.PortfolioDecisionRef, nameof(cursor.PortfolioDecisionRef));
        ValidateStoredArtifact(cursor.Frontier, PaperFormalizationFrontierSchemas.Frontier);
        ValidateStoredArtifact(cursor.InitialState, PaperFormalizationFrontierSchemas.FrontierState);
        if (cursor.InitialNodeRoutes is null || cursor.InitialNodeRoutes.Count < 1)
        {
            throw new InvalidDataException(
                "Frontier-planning cursor must route at least one initial node.");
        }
        ValidateRouteShape(cursor.InitialNodeRoutes);
        RequireRunId(cursor.RunId);
        if (!ProvenanceValues.Contains(cursor.Provenance))
        {
            throw new InvalidDataException(
                "Frontier-planning cursor provenance is invalid.");
        }
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static PaperFrontierPlanningContext LoadContext(
        string root,
        PaperFrontierPlanningAgentDispatch dispatch)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        string[] expectedRefs =
        [
            dispatch.PortfolioCursorRef,
            dispatch.PortfolioDispatchRef,
            dispatch.TheoryProgramRef,
            dispatch.ScopeRef,
            dispatch.InventoryRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.ScorecardRef,
            dispatch.CandidatePaperRef,
            dispatch.LiteratureResearchRef,
            dispatch.JudgmentEvidenceRef,
            dispatch.PortfolioDecisionRef,
            dispatch.UpdatedPortfolioRef
        ];
        RequireSameSet(
            dispatch.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
            expectedRefs,
            "frontier-planning exact input closure");

        PaperPortfolioJudgmentAgentAdmissionCursor portfolioCursor =
            ReadContent<PaperPortfolioJudgmentAgentAdmissionCursor>(
                root,
                FindInput(
                    dispatch.ExactInputs,
                    PaperPortfolioJudgmentAgentSchemas.AdmissionCursor,
                    dispatch.PortfolioCursorRef));
        PaperPortfolioJudgmentAgentService.Validate(portfolioCursor);
        PaperPortfolioJudgmentAgentDispatch portfolioDispatch =
            ReadContent<PaperPortfolioJudgmentAgentDispatch>(
                root,
                FindInput(
                    dispatch.ExactInputs,
                    PaperPortfolioJudgmentAgentSchemas.Dispatch,
                    dispatch.PortfolioDispatchRef));
        PaperPortfolioJudgmentAgentService.Validate(portfolioDispatch);
        PaperPortfolioJudgmentPaperInput coordinates = portfolioDispatch.Papers
            .SingleOrDefault(value => string.Equals(
                value.PaperId,
                dispatch.PaperId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier-planning source dispatch does not contain this paper.");

        PaperTheoryProgramContent programContent = ReadContent<PaperTheoryProgramContent>(
            root,
            FindInput(
                dispatch.ExactInputs,
                PaperPortfolioSchemas.TheoryProgram,
                dispatch.TheoryProgramRef));
        var program = new PaperTheoryProgram(
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            programContent);
        PaperPortfolioService.Validate(program);

        PaperTheoryScopeContent scopeContent = ReadContent<PaperTheoryScopeContent>(
            root,
            FindInput(
                dispatch.ExactInputs,
                PaperTheoryFoundationSchemas.Scope,
                dispatch.ScopeRef));
        var scope = new PaperTheoryScope(
            PaperTheoryFoundationSchemas.Scope,
            dispatch.ScopeRef,
            scopeContent);
        PaperTheoryFoundationService.Validate(scope, program);

        PaperTheoryInventoryContent inventoryContent = ReadContent<PaperTheoryInventoryContent>(
            root,
            FindInput(
                dispatch.ExactInputs,
                PaperTheoryFoundationSchemas.Inventory,
                dispatch.InventoryRef));
        var inventory = new PaperTheoryInventory(
            PaperTheoryFoundationSchemas.Inventory,
            dispatch.InventoryRef,
            inventoryContent);
        PaperTheoryFoundationService.Validate(inventory);

        PaperTheoremPackageContent packageContent = ReadContent<PaperTheoremPackageContent>(
            root,
            FindInput(
                dispatch.ExactInputs,
                PaperTheoryDeepeningSchemas.TheoremPackage,
                dispatch.TheoremPackageRef));
        var package = new PaperTheoremPackage(
            PaperTheoryDeepeningSchemas.TheoremPackage,
            dispatch.TheoremPackageRef,
            packageContent);
        PaperTheoryDeepeningService.Validate(package);

        PaperTheoryAuditContent auditContent = ReadContent<PaperTheoryAuditContent>(
            root,
            FindInput(
                dispatch.ExactInputs,
                PaperTheoryAuditSchemas.Audit,
                dispatch.TheoryAuditRef));
        var audit = new PaperTheoryAudit(
            PaperTheoryAuditSchemas.Audit,
            dispatch.TheoryAuditRef,
            auditContent);
        PaperTheoryAuditService.Validate(audit);

        PaperCandidateScorecardContent scorecardContent =
            ReadContent<PaperCandidateScorecardContent>(
                root,
                FindInput(
                    dispatch.ExactInputs,
                    PaperPortfolioDecisionSchemas.Scorecard,
                    dispatch.ScorecardRef));
        var scorecard = new PaperCandidateScorecard(
            PaperPortfolioDecisionSchemas.Scorecard,
            dispatch.ScorecardRef,
            scorecardContent);
        PaperPortfolioDecisionService.Validate(scorecard);

        PaperPortfolioJudgmentEvidenceContent evidenceContent =
            ReadContent<PaperPortfolioJudgmentEvidenceContent>(
                root,
                FindInput(
                    dispatch.ExactInputs,
                    PaperPortfolioJudgmentAgentSchemas.Evidence,
                    dispatch.JudgmentEvidenceRef));
        var evidence = new PaperPortfolioJudgmentEvidence(
            PaperPortfolioJudgmentAgentSchemas.Evidence,
            dispatch.JudgmentEvidenceRef,
            evidenceContent);
        PaperPortfolioJudgmentAgentService.Validate(evidence);

        PaperPortfolioDecisionContent decisionContent =
            ReadContent<PaperPortfolioDecisionContent>(
                root,
                FindInput(
                    dispatch.ExactInputs,
                    PaperPortfolioDecisionSchemas.Decision,
                    dispatch.PortfolioDecisionRef));
        var decision = new PaperPortfolioDecision(
            PaperPortfolioDecisionSchemas.Decision,
            dispatch.PortfolioDecisionRef,
            decisionContent);
        PaperPortfolioDecisionService.Validate(decision);

        PaperResearchPortfolioContent updatedPortfolioContent =
            ReadContent<PaperResearchPortfolioContent>(
                root,
                FindInput(
                    dispatch.ExactInputs,
                    PaperPortfolioSchemas.Portfolio,
                    dispatch.UpdatedPortfolioRef));
        var updatedPortfolio = new PaperResearchPortfolio(
            PaperPortfolioSchemas.Portfolio,
            dispatch.UpdatedPortfolioRef,
            updatedPortfolioContent);
        PaperPortfolioService.Validate(updatedPortfolio);

        _ = FindInput(
            dispatch.ExactInputs,
            CandidateArtifactSchemas.CandidatePaper,
            dispatch.CandidatePaperRef);
        _ = FindInput(
            dispatch.ExactInputs,
            CandidateArtifactSchemas.LiteratureResearch,
            dispatch.LiteratureResearchRef);

        PaperPortfolioJudgmentPaperRoute route = portfolioCursor.Routes
            .SingleOrDefault(value => string.Equals(
                value.PaperId,
                dispatch.PaperId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier-planning source cursor does not route this paper.");
        PaperPortfolioPaperDecision paperDecision = decision.DecisionContent.Decisions
            .SingleOrDefault(value => string.Equals(
                value.PaperId,
                dispatch.PaperId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier-planning decision does not contain this paper.");
        PaperCandidateState updatedState = updatedPortfolio.PortfolioContent.CandidateStates
            .SingleOrDefault(value => string.Equals(
                value.PaperId,
                dispatch.PaperId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Updated portfolio does not contain the frontier paper.");

        if (!string.Equals(portfolioCursor.TaskRef, dispatch.PortfolioTaskRef, StringComparison.Ordinal)
            || !string.Equals(portfolioCursor.ResultRef, dispatch.PortfolioResultRef, StringComparison.Ordinal)
            || !string.Equals(portfolioCursor.DispatchRef, dispatch.PortfolioDispatchRef, StringComparison.Ordinal)
            || !string.Equals(portfolioCursor.PortfolioRef, dispatch.PortfolioRef, StringComparison.Ordinal)
            || !string.Equals(portfolioCursor.CandidateBatchRef, dispatch.CandidateBatchRef, StringComparison.Ordinal)
            || portfolioCursor.CycleNumber != dispatch.CycleNumber
            || !string.Equals(portfolioCursor.Evidence.ArtifactRef, dispatch.JudgmentEvidenceRef, StringComparison.Ordinal)
            || !string.Equals(portfolioCursor.Decision.ArtifactRef, dispatch.PortfolioDecisionRef, StringComparison.Ordinal)
            || !string.Equals(portfolioCursor.UpdatedPortfolio.ArtifactRef, dispatch.UpdatedPortfolioRef, StringComparison.Ordinal)
            || !string.Equals(portfolioDispatch.PortfolioRef, dispatch.PortfolioRef, StringComparison.Ordinal)
            || !string.Equals(portfolioDispatch.CandidateBatchRef, dispatch.CandidateBatchRef, StringComparison.Ordinal)
            || portfolioDispatch.CycleNumber != dispatch.CycleNumber)
        {
            throw new InvalidDataException(
                "Frontier planning changed its source portfolio judgment coordinates.");
        }
        if (!string.Equals(route.Action, "promote-to-frontier", StringComparison.Ordinal)
            || !string.Equals(route.NextRoute, "frontier-planning", StringComparison.Ordinal)
            || !string.Equals(route.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(route.ScorecardRef, dispatch.ScorecardRef, StringComparison.Ordinal)
            || !string.Equals(paperDecision.Action, "promote-to-frontier", StringComparison.Ordinal)
            || !string.Equals(paperDecision.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(paperDecision.ScorecardRef, dispatch.ScorecardRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier planning is not bound to an exact promotion decision.");
        }
        if (!string.Equals(updatedState.Phase, "frontier-pending", StringComparison.Ordinal)
            || !string.Equals(updatedState.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(updatedPortfolio.PortfolioContent.CandidateBatchRef, dispatch.CandidateBatchRef, StringComparison.Ordinal)
            || updatedPortfolio.PortfolioContent.NextCycleNumber != dispatch.CycleNumber + 1)
        {
            throw new InvalidDataException(
                "Updated portfolio has not advanced the paper to frontier-pending.");
        }
        if (!string.Equals(evidence.EvidenceContent.DispatchRef, dispatch.PortfolioDispatchRef, StringComparison.Ordinal)
            || !string.Equals(evidence.EvidenceContent.AgentResultRef, dispatch.PortfolioResultRef, StringComparison.Ordinal)
            || !string.Equals(evidence.EvidenceContent.PortfolioRef, dispatch.PortfolioRef, StringComparison.Ordinal)
            || !string.Equals(evidence.EvidenceContent.CandidateBatchRef, dispatch.CandidateBatchRef, StringComparison.Ordinal)
            || evidence.EvidenceContent.CycleNumber != dispatch.CycleNumber
            || !string.Equals(evidence.EvidenceContent.DecisionRef, dispatch.PortfolioDecisionRef, StringComparison.Ordinal)
            || !evidence.EvidenceContent.RankedPaperIds.Contains(dispatch.PaperId, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio judgment evidence does not support this frontier route.");
        }
        if (!string.Equals(coordinates.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(coordinates.ScopeRef, dispatch.ScopeRef, StringComparison.Ordinal)
            || !string.Equals(coordinates.InventoryRef, dispatch.InventoryRef, StringComparison.Ordinal)
            || !string.Equals(coordinates.TheoremPackageRef, dispatch.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(coordinates.TheoryAuditRef, dispatch.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(coordinates.ScorecardRef, dispatch.ScorecardRef, StringComparison.Ordinal)
            || !string.Equals(coordinates.CandidatePaperRef, dispatch.CandidatePaperRef, StringComparison.Ordinal)
            || !string.Equals(coordinates.LiteratureResearchRef, dispatch.LiteratureResearchRef, StringComparison.Ordinal)
            || !string.Equals(program.ProgramContent.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(program.ProgramContent.CandidateBatchRef, dispatch.CandidateBatchRef, StringComparison.Ordinal)
            || !string.Equals(program.ProgramContent.CandidatePaperRef, dispatch.CandidatePaperRef, StringComparison.Ordinal)
            || !string.Equals(program.ProgramContent.LiteratureResearchRef, dispatch.LiteratureResearchRef, StringComparison.Ordinal)
            || !string.Equals(scope.ScopeContent.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(inventory.InventoryContent.ScopeRef, dispatch.ScopeRef, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.InventoryRef, dispatch.InventoryRef, StringComparison.Ordinal)
            || !string.Equals(audit.AuditContent.TheoremPackageRef, dispatch.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(scorecard.ScorecardContent.TheoryAuditRef, dispatch.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(scorecard.ScorecardContent.TheoremPackageRef, dispatch.TheoremPackageRef, StringComparison.Ordinal)
            || !scorecard.ScorecardContent.PromotionEligible)
        {
            throw new InvalidDataException(
                "Frontier-planning evidence does not describe one promoted theorem package.");
        }

        return new(
            portfolioCursor,
            portfolioDispatch,
            coordinates,
            program,
            scope,
            inventory,
            package,
            audit,
            scorecard,
            evidence,
            decision,
            updatedPortfolio);
    }

    private static void ValidateTaskBinding(
        PaperAgentTask actual,
        PaperFrontierPlanningAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperFrontierPlanningContext context)
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
                "Frontier-planning task changed its promotion-bound planning contract.");
        }
    }

    private static void ValidateDraft(
        PaperFrontierPlanningAgentDispatch dispatch,
        string dispatchRef,
        PaperFrontierPlanningContext context,
        PaperFormalizationFrontierDraft draft)
    {
        Validate(draft);
        if (!string.Equals(draft.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(draft.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(draft.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(draft.TheoremPackageRef, dispatch.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(draft.TheoryAuditRef, dispatch.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(draft.ScorecardRef, dispatch.ScorecardRef, StringComparison.Ordinal)
            || !string.Equals(draft.PortfolioDecisionRef, dispatch.PortfolioDecisionRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier draft changed its dispatch, paper, theorem package, audit, scorecard, or decision.");
        }
        string[] expectedClaims = context.TheoremPackage.TheoremPackageContent.Claims
            .Select(claim => claim.ClaimId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualClaims = draft.NodeSpecs
            .Select(spec => spec.ClaimId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!expectedClaims.SequenceEqual(actualClaims, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier draft changed the admitted theorem-package claim set.");
        }
        if (ParseUtc(draft.CreatedAt, nameof(draft.CreatedAt))
            < ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt)))
        {
            throw new InvalidDataException(
                "Frontier draft cannot predate its promotion-bound dispatch.");
        }
    }

    private static void ValidateRoutes(
        IReadOnlyList<PaperFrontierPlanningNodeRoute> routes,
        PaperFormalizationFrontier frontier)
    {
        PaperFormalizationFrontierNode[] expected = frontier.FrontierContent.Nodes
            .Where(node => node.ParallelWave == 0)
            .ToArray();
        if (routes.Count != expected.Length || routes.Count < 1)
        {
            throw new InvalidDataException(
                "Initial frontier routes must cover exactly every wave-zero node.");
        }
        for (int index = 0; index < routes.Count; index++)
        {
            PaperFrontierPlanningNodeRoute route = routes[index];
            PaperFormalizationFrontierNode node = expected[index];
            if (route.DispatchOrder != index + 1
                || !string.Equals(route.NodeId, node.NodeId, StringComparison.Ordinal)
                || !string.Equals(route.ClaimId, node.ClaimId, StringComparison.Ordinal)
                || !string.Equals(route.FormalizationKind, node.FormalizationKind, StringComparison.Ordinal)
                || route.ParallelWave != 0
                || route.Priority != node.Priority
                || !string.Equals(route.NextRoute, "governed-selection", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Initial frontier route changed a wave-zero node or its deterministic order.");
            }
        }
    }

    private static void ValidateRouteShape(
        IReadOnlyList<PaperFrontierPlanningNodeRoute> routes)
    {
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        var claims = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < routes.Count; index++)
        {
            PaperFrontierPlanningNodeRoute route = routes[index];
            ArgumentNullException.ThrowIfNull(route);
            if (route.DispatchOrder != index + 1
                || route.ParallelWave != 0
                || route.Priority is < 0 or > 100
                || !string.Equals(route.NextRoute, "governed-selection", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Initial frontier route order, wave, priority, or next route is invalid.");
            }
            RequireDigest(route.NodeId, nameof(route.NodeId));
            if (!ClaimIdPattern.IsMatch(route.ClaimId ?? string.Empty)
                || !DraftFormalizationKinds.Contains(route.FormalizationKind)
                || !nodes.Add(route.NodeId)
                || !claims.Add(route.ClaimId))
            {
                throw new InvalidDataException(
                    "Initial frontier node and claim routes must be canonical and unique.");
            }
        }
    }

    private static void ValidateAdmissionReplay(
        string root,
        PaperFrontierPlanningAgentAdmissionCursor cursor,
        string taskRef,
        PaperAgentTaskCursor agentCursor,
        PaperFrontierPlanningAgentDispatch dispatch,
        string dispatchRef)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, taskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PortfolioTaskRef, dispatch.PortfolioTaskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PortfolioResultRef, dispatch.PortfolioResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PortfolioRef, dispatch.PortfolioRef, StringComparison.Ordinal)
            || cursor.CycleNumber != dispatch.CycleNumber
            || !string.Equals(cursor.JudgmentEvidenceRef, dispatch.JudgmentEvidenceRef, StringComparison.Ordinal)
            || !string.Equals(cursor.UpdatedPortfolioRef, dispatch.UpdatedPortfolioRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoremPackageRef, dispatch.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryAuditRef, dispatch.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ScorecardRef, dispatch.ScorecardRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PortfolioDecisionRef, dispatch.PortfolioDecisionRef, StringComparison.Ordinal)
            || !string.Equals(cursor.RunId, agentCursor.RunId, StringComparison.Ordinal)
            || !string.Equals(cursor.Provenance, agentCursor.Provenance, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier-planning admission cursor changed task, promotion, or run identity.");
        }
        PaperFormalizationFrontier frontier =
            ReadStoredEnvelope<PaperFormalizationFrontier>(root, cursor.Frontier);
        PaperFormalizationFrontierService.Validate(frontier);
        PaperFormalizationFrontierState state =
            ReadStoredEnvelope<PaperFormalizationFrontierState>(root, cursor.InitialState);
        PaperFormalizationFrontierLifecycleService.Validate(state, frontier);
        if (!string.Equals(frontier.FrontierId, cursor.Frontier.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(state.StateId, cursor.InitialState.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(frontier.FrontierContent.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(frontier.FrontierContent.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(frontier.FrontierContent.TheoremPackageRef, dispatch.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(frontier.FrontierContent.PortfolioDecisionRef, dispatch.PortfolioDecisionRef, StringComparison.Ordinal)
            || !string.Equals(state.StateContent.FrontierRef, frontier.FrontierId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Stored frontier-planning artifacts differ from their admission cursor.");
        }
        ValidateRoutes(cursor.InitialNodeRoutes, frontier);
    }

    private static PaperFrontierPlanningAgentResultAdmitted ToAdmitted(
        PaperFrontierPlanningAgentAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperFrontierPlanningAgentSchemas.ResultAdmitted,
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.PortfolioTaskRef,
            cursor.PortfolioResultRef,
            cursor.PortfolioRef,
            cursor.CycleNumber,
            cursor.JudgmentEvidenceRef,
            cursor.UpdatedPortfolioRef,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.TheoremPackageRef,
            cursor.TheoryAuditRef,
            cursor.ScorecardRef,
            cursor.PortfolioDecisionRef,
            cursor.Frontier,
            cursor.InitialState,
            cursor.InitialNodeRoutes,
            cursor.RunId,
            cursor.Provenance,
            cursor.AdmittedAt,
            replayed);

    private static PaperFrontierPlanningAgentAdmissionCursor ReadAdmissionCursor(
        string path)
    {
        PaperFrontierPlanningAgentAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierPlanningAgentAdmissionCursor>(
                ReadBoundedFile(
                    path,
                    MaximumControlBytes,
                    "Frontier-planning admission cursor"));
        Validate(cursor);
        return cursor;
    }
}
