namespace Trureturing.Paper.Core;

public static partial class PaperFrontierNodeSelectionService
{
    private static PaperFrontierNodeSelectionSource LoadSource(
        string root,
        string frontierPlanningTaskRef,
        string nodeId)
    {
        string planningCursorPath = PlanningAdmissionCursorPath(
            root,
            frontierPlanningTaskRef);
        PaperFrontierPlanningAgentAdmissionCursor planningCursor =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierPlanningAgentAdmissionCursor>(
                ReadBoundedFile(
                    planningCursorPath,
                    MaximumControlBytes,
                    "Frontier-planning admission cursor"));
        PaperFrontierPlanningAgentService.Validate(planningCursor);
        if (!string.Equals(
                planningCursor.TaskRef,
                frontierPlanningTaskRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection source cursor changed the planning task identity.");
        }

        PaperFrontierPlanningNodeRoute route = planningCursor.InitialNodeRoutes
            .SingleOrDefault(value => string.Equals(
                value.NodeId,
                nodeId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier-planning admission did not release the requested node.");
        if (route.ParallelWave != 0
            || !string.Equals(
                route.NextRoute,
                "governed-selection",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an admitted wave-zero governed-selection route may be selected.");
        }

        string planningDispatchPath = PlanningDispatchPath(
            root,
            planningCursor.DispatchRef);
        byte[] planningDispatchBytes = ReadImmutable(
            planningDispatchPath,
            planningCursor.DispatchRef,
            "Frontier-planning dispatch");
        PaperFrontierPlanningAgentDispatch planningDispatch =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierPlanningAgentDispatch>(
                planningDispatchBytes);
        PaperFrontierPlanningAgentService.Validate(planningDispatch);
        foreach (PaperAgentInputArtifact input in planningDispatch.ExactInputs)
        {
            _ = ReadRepositoryArtifact(
                root,
                input.RepositoryRelativePath,
                input.ArtifactRef,
                $"Exact frontier-planning input {input.Schema}");
        }

        PaperFormalizationFrontier frontier =
            ReadPlanningStoredEnvelope<PaperFormalizationFrontier>(
                root,
                planningCursor.Frontier,
                PaperFormalizationFrontierSchemas.Frontier,
                "Formalization frontier");
        PaperFormalizationFrontierService.Validate(frontier);
        PaperFormalizationFrontierState initialState =
            ReadPlanningStoredEnvelope<PaperFormalizationFrontierState>(
                root,
                planningCursor.InitialState,
                PaperFormalizationFrontierSchemas.FrontierState,
                "Initial formalization frontier state");
        PaperFormalizationFrontierLifecycleService.Validate(
            initialState,
            frontier);

        PaperAgentInputArtifact programInput = FindExactInput(
            planningDispatch.ExactInputs,
            PaperPortfolioSchemas.TheoryProgram,
            planningDispatch.TheoryProgramRef);
        PaperTheoryProgramContent programContent =
            ReadExactContent<PaperTheoryProgramContent>(root, programInput);
        var program = new PaperTheoryProgram(
            PaperPortfolioSchemas.TheoryProgram,
            planningDispatch.TheoryProgramRef,
            programContent);
        PaperPortfolioService.Validate(program);

        PaperAgentInputArtifact packageInput = FindExactInput(
            planningDispatch.ExactInputs,
            PaperTheoryDeepeningSchemas.TheoremPackage,
            planningDispatch.TheoremPackageRef);
        PaperTheoremPackageContent packageContent =
            ReadExactContent<PaperTheoremPackageContent>(root, packageInput);
        var theoremPackage = new PaperTheoremPackage(
            PaperTheoryDeepeningSchemas.TheoremPackage,
            planningDispatch.TheoremPackageRef,
            packageContent);
        PaperTheoryDeepeningService.Validate(theoremPackage);

        PaperFormalizationFrontierNode node =
            PaperFormalizationFrontierService.RequireNode(frontier, nodeId);
        PaperFormalizationFrontierNodeState initialNodeState =
            initialState.StateContent.NodeStates.Single(value =>
                string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
        PaperTheoremPackageClaim claim = theoremPackage.TheoremPackageContent.Claims
            .SingleOrDefault(value => string.Equals(
                value.ClaimId,
                node.ClaimId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier node claim is absent from the admitted theorem package.");

        var researchStore = new PaperResearchInputStore(
            Path.Combine(root, "artifacts", "research-input"));
        PaperResearchInput researchInput = researchStore.Get<PaperResearchInput>(
            program.ProgramContent.PaperResearchInputRef);
        PaperResearchInputValidation.Validate(researchInput);

        if (!string.Equals(
                planningCursor.PortfolioTaskRef,
                planningDispatch.PortfolioTaskRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PortfolioResultRef,
                planningDispatch.PortfolioResultRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PortfolioRef,
                planningDispatch.PortfolioRef,
                StringComparison.Ordinal)
            || planningCursor.CycleNumber != planningDispatch.CycleNumber
            || !string.Equals(
                planningCursor.JudgmentEvidenceRef,
                planningDispatch.JudgmentEvidenceRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.UpdatedPortfolioRef,
                planningDispatch.UpdatedPortfolioRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PaperId,
                planningDispatch.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.TheoryProgramRef,
                planningDispatch.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.TheoremPackageRef,
                planningDispatch.TheoremPackageRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.TheoryAuditRef,
                planningDispatch.TheoryAuditRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.ScorecardRef,
                planningDispatch.ScorecardRef,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.PortfolioDecisionRef,
                planningDispatch.PortfolioDecisionRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection source cursor and planning dispatch disagree.");
        }
        if (!string.Equals(
                planningCursor.Frontier.ArtifactRef,
                frontier.FrontierId,
                StringComparison.Ordinal)
            || !string.Equals(
                planningCursor.InitialState.ArtifactRef,
                initialState.StateId,
                StringComparison.Ordinal)
            || !string.Equals(
                initialState.StateContent.FrontierRef,
                frontier.FrontierId,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.TheoryProgramRef,
                program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.TheoremPackageRef,
                theoremPackage.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.TheoryAuditRef,
                planningCursor.TheoryAuditRef,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.ScorecardRef,
                planningCursor.ScorecardRef,
                StringComparison.Ordinal)
            || !string.Equals(
                frontier.FrontierContent.PortfolioDecisionRef,
                planningCursor.PortfolioDecisionRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection source artifacts do not form one planning admission.");
        }
        if (!string.Equals(
                program.ProgramContent.PaperId,
                planningCursor.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                program.ProgramContent.CandidatePaperRef,
                planningDispatch.CandidatePaperRef,
                StringComparison.Ordinal)
            || !string.Equals(
                program.ProgramContent.LiteratureResearchRef,
                planningDispatch.LiteratureResearchRef,
                StringComparison.Ordinal)
            || !string.Equals(
                theoremPackage.TheoremPackageContent.TheoryProgramRef,
                program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                theoremPackage.TheoremPackageContent.PaperId,
                program.ProgramContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection program and theorem package identities disagree.");
        }
        if (!string.Equals(route.ClaimId, node.ClaimId, StringComparison.Ordinal)
            || !string.Equals(
                route.FormalizationKind,
                node.FormalizationKind,
                StringComparison.Ordinal)
            || route.Priority != node.Priority
            || route.DispatchOrder < 1
            || node.ParallelWave != 0
            || node.DependencyNodeIds.Count != 0
            || !string.Equals(
                initialNodeState.Status,
                PaperFormalizationFrontierService.InitialNodeStatus,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.Statement,
                node.InformalStatement,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection route changed the admitted wave-zero node.");
        }
        if (!string.Equals(
                researchInput.TruthReleaseDigest,
                frontier.FrontierContent.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                researchInput.TopologyDigest,
                frontier.FrontierContent.TopologyDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                program.ProgramContent.PaperResearchInputRef,
                frontier.FrontierContent.PaperResearchInputRef,
                StringComparison.Ordinal)
            || !string.Equals(
                researchInput.TruthReleaseDigest,
                program.ProgramContent.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                researchInput.TopologyDigest,
                program.ProgramContent.TopologyDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier node is not bound to the exact Paper research input release.");
        }

        return new(
            planningCursor,
            planningDispatch,
            frontier,
            initialState,
            program,
            theoremPackage,
            route,
            node,
            researchInput);
    }

    private static T ReadPlanningStoredEnvelope<T>(
        string root,
        PaperFrontierPlanningStoredArtifact stored,
        string expectedSchema,
        string name)
    {
        if (!string.Equals(stored.Schema, expectedSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} has the wrong stored schema.");
        }
        byte[] bytes = ReadRepositoryArtifact(
            root,
            stored.EnvelopePath,
            stored.EnvelopeRef,
            name);
        return PaperResearchInputJson.DeserializeStrict<T>(bytes);
    }

    private static T ReadExactContent<T>(
        string root,
        PaperAgentInputArtifact input) =>
        PaperResearchInputJson.DeserializeStrict<T>(
            ReadRepositoryArtifact(
                root,
                input.RepositoryRelativePath,
                input.ArtifactRef,
                $"Exact frontier-planning input {input.Schema}"));

    private static PaperAgentInputArtifact FindExactInput(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        string schema,
        string reference) =>
        inputs.SingleOrDefault(input =>
                string.Equals(input.Schema, schema, StringComparison.Ordinal)
                && string.Equals(input.ArtifactRef, reference, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Frontier selection is missing exact input {schema} at {reference}.");
}
