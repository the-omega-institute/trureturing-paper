using System.Text;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public sealed record PaperFrontierPlanningContext(
    PaperPortfolioJudgmentAgentAdmissionCursor PortfolioCursor,
    PaperPortfolioJudgmentAgentDispatch PortfolioDispatch,
    PaperPortfolioJudgmentPaperInput Coordinates,
    PaperTheoryProgram Program,
    PaperTheoryScope Scope,
    PaperTheoryInventory Inventory,
    PaperTheoremPackage TheoremPackage,
    PaperTheoryAudit Audit,
    PaperCandidateScorecard Scorecard,
    PaperPortfolioJudgmentEvidence JudgmentEvidence,
    PaperPortfolioDecision PortfolioDecision,
    PaperResearchPortfolio UpdatedPortfolio);

public sealed record PaperFrontierPlanningComputation(
    PaperFormalizationFrontier Frontier,
    PaperFormalizationFrontierState InitialState,
    IReadOnlyList<PaperFrontierPlanningNodeRoute> InitialNodeRoutes);

public static partial class PaperFrontierPlanningAgentService
{
    private const int MaximumControlBytes = 4 * 1024 * 1024;
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;
    private const int ExactInputCount = 13;

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PaperIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ClaimIdPattern = new(
        "^[A-Za-z][A-Za-z0-9._:-]{0,255}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex SchemaPattern = new(
        "^[a-z][a-z0-9.-]*\\.v[0-9]+$",
        RegexOptions.CultureInvariant);
    private static readonly Regex RelativePathPattern = new(
        "^[A-Za-z0-9._+@=-]+(?:/[A-Za-z0-9._+@=-]+)*$",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ProvenanceValues = new(
        ["produced", "adopted"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedEvidenceRoots = new(
        ["artifacts", "Papers", "work", "contracts", "docs", "src", "tools", "tests"],
        StringComparer.Ordinal);

    public static PaperFrontierPlanningAgentTaskStaged StageTask(
        string repositoryRoot,
        string portfolioTaskRef,
        string paperId)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(portfolioTaskRef, nameof(portfolioTaskRef));
        RequirePaperId(paperId);

        string portfolioCursorPath = PortfolioAdmissionCursorPath(root, portfolioTaskRef);
        byte[] portfolioCursorBytes = ReadBoundedFile(
            portfolioCursorPath,
            MaximumControlBytes,
            "Portfolio judgment admission cursor");
        string portfolioCursorRef = ByteReference(portfolioCursorBytes);
        PaperPortfolioJudgmentAgentAdmissionCursor portfolioCursor =
            PaperResearchInputJson.DeserializeStrict<PaperPortfolioJudgmentAgentAdmissionCursor>(
                portfolioCursorBytes);
        PaperPortfolioJudgmentAgentService.Validate(portfolioCursor);
        if (!string.Equals(portfolioCursor.TaskRef, portfolioTaskRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier planning source cursor changed the portfolio task identity.");
        }

        PaperPortfolioJudgmentPaperRoute route = portfolioCursor.Routes
            .SingleOrDefault(value => string.Equals(
                value.PaperId,
                paperId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Portfolio judgment does not contain the requested frontier paper.");
        if (!string.Equals(route.Action, "promote-to-frontier", StringComparison.Ordinal)
            || !string.Equals(route.NextRoute, "frontier-planning", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a portfolio-promoted paper may stage frontier planning.");
        }

        string portfolioDispatchPath = PortfolioDispatchPath(
            root,
            portfolioCursor.DispatchRef);
        byte[] portfolioDispatchBytes = ReadImmutable(
            portfolioDispatchPath,
            portfolioCursor.DispatchRef,
            "Portfolio judgment dispatch");
        PaperPortfolioJudgmentAgentDispatch portfolioDispatch =
            PaperResearchInputJson.DeserializeStrict<PaperPortfolioJudgmentAgentDispatch>(
                portfolioDispatchBytes);
        PaperPortfolioJudgmentAgentService.Validate(portfolioDispatch);
        PaperPortfolioJudgmentPaperInput coordinates = portfolioDispatch.Papers
            .SingleOrDefault(value => string.Equals(
                value.PaperId,
                paperId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Portfolio dispatch does not contain the promoted paper.");
        if (!string.Equals(route.TheoryProgramRef, coordinates.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(route.ScorecardRef, coordinates.ScorecardRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio route changed the promoted paper program or scorecard.");
        }

        PaperAgentInputArtifact[] exactInputs = BuildExactInputs(
            root,
            portfolioTaskRef,
            portfolioCursorPath,
            portfolioCursorRef,
            portfolioCursor,
            portfolioDispatchPath,
            portfolioDispatch,
            coordinates);
        var dispatch = new PaperFrontierPlanningAgentDispatch(
            PaperFrontierPlanningAgentSchemas.Dispatch,
            portfolioTaskRef,
            portfolioCursor.ResultRef,
            portfolioCursorRef,
            portfolioCursor.DispatchRef,
            portfolioCursor.PortfolioRef,
            portfolioCursor.CandidateBatchRef,
            portfolioCursor.CycleNumber,
            portfolioCursor.Evidence.ArtifactRef,
            portfolioCursor.Decision.ArtifactRef,
            portfolioCursor.UpdatedPortfolio.ArtifactRef,
            paperId,
            coordinates.TheoryProgramRef,
            coordinates.ScopeRef,
            coordinates.InventoryRef,
            coordinates.TheoremPackageRef,
            coordinates.TheoryAuditRef,
            coordinates.ScorecardRef,
            coordinates.CandidatePaperRef,
            coordinates.LiteratureResearchRef,
            exactInputs,
            portfolioCursor.AdmittedAt);
        Validate(dispatch);
        PaperFrontierPlanningContext context = LoadContext(root, dispatch);

        byte[] dispatchBytes = CanonicalJson.Serialize(dispatch);
        string dispatchRef = ByteReference(dispatchBytes);
        string immutableDispatchPath = DomainArtifactPath(
            root,
            "dispatches",
            "raw",
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
            $"frontier-planning-{paperId}-{Hex(taskRef)}.json");
        bool replayed = PutImmutable(taskPath, taskBytes);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile(task.Phase);
        return new PaperFrontierPlanningAgentTaskStaged(
            PaperFrontierPlanningAgentSchemas.TaskStaged,
            dispatchRef,
            taskRef,
            taskPath,
            portfolioTaskRef,
            portfolioCursor.ResultRef,
            portfolioCursor.PortfolioRef,
            portfolioCursor.CycleNumber,
            paperId,
            coordinates.TheoryProgramRef,
            coordinates.TheoremPackageRef,
            coordinates.ScorecardRef,
            portfolioCursor.Decision.ArtifactRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            replayed);
    }

    public static PaperFrontierPlanningAgentResultAdmitted AdmitResult(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        if (!string.Equals(task.Phase, "frontier-planning", StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, "paper-formalization-frontier-planner", StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, "promotion-bound-planning", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an FKST-native frontier-planning task can enter this admission bridge.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperFrontierPlanningAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Frontier-planning task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = ByteReference(dispatchBytes);
        PaperFrontierPlanningAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperFrontierPlanningAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);
        PaperFrontierPlanningContext context = LoadContext(root, dispatch);
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
            || !string.Equals(result.NextRoute, "formalization-frontier", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a completed formalization-frontier result can be admitted.");
        }
        if (agentCursor.Outputs.Count != 1
            || !string.Equals(
                agentCursor.Outputs[0].Schema,
                PaperFrontierPlanningAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier planning must return exactly one frontier draft.");
        }

        string admissionCursorPath = AdmissionCursorPath(root, taskRef);
        if (File.Exists(admissionCursorPath))
        {
            PaperFrontierPlanningAgentAdmissionCursor existing =
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
        PaperFormalizationFrontierDraft draft =
            PaperResearchInputJson.DeserializeStrict<PaperFormalizationFrontierDraft>(
                draftBytes);
        PaperFrontierPlanningComputation computation = Compute(
            dispatch,
            dispatchRef,
            context,
            draft,
            result.CompletedAt);

        PaperFrontierPlanningStoredArtifact storedFrontier = StoreDomain(
            root,
            "frontiers",
            computation.Frontier.Schema,
            computation.Frontier.FrontierId,
            computation.Frontier.FrontierContent,
            computation.Frontier);
        PaperFrontierPlanningStoredArtifact storedState = StoreDomain(
            root,
            "states",
            computation.InitialState.Schema,
            computation.InitialState.StateId,
            computation.InitialState.StateContent,
            computation.InitialState);
        var cursor = new PaperFrontierPlanningAgentAdmissionCursor(
            PaperFrontierPlanningAgentSchemas.AdmissionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.PortfolioTaskRef,
            dispatch.PortfolioResultRef,
            dispatch.PortfolioRef,
            dispatch.CycleNumber,
            dispatch.JudgmentEvidenceRef,
            dispatch.UpdatedPortfolioRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.ScorecardRef,
            dispatch.PortfolioDecisionRef,
            storedFrontier,
            storedState,
            computation.InitialNodeRoutes,
            agentCursor.RunId,
            agentCursor.Provenance,
            result.CompletedAt);
        Validate(cursor);
        Directory.CreateDirectory(Path.GetDirectoryName(admissionCursorPath)!);
        try
        {
            PaperResearchInputStore.WriteAtomic(
                admissionCursorPath,
                CanonicalJson.Serialize(cursor),
                overwrite: false);
        }
        catch (IOException) when (File.Exists(admissionCursorPath))
        {
            PaperFrontierPlanningAgentAdmissionCursor existing =
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

    public static PaperFrontierPlanningComputation Compute(
        PaperFrontierPlanningAgentDispatch dispatch,
        string dispatchRef,
        PaperFrontierPlanningContext context,
        PaperFormalizationFrontierDraft draft,
        string admittedAt)
    {
        Validate(dispatch);
        RequireDigest(dispatchRef, nameof(dispatchRef));
        ParseUtc(admittedAt, nameof(admittedAt));
        ValidateDraft(dispatch, dispatchRef, context, draft);

        PaperFormalizationFrontier frontier =
            PaperFormalizationFrontierService.CreateFrontier(
                context.Program,
                context.TheoremPackage,
                context.Audit,
                context.Scorecard,
                context.PortfolioDecision,
                draft.NodeSpecs,
                admittedAt);
        PaperFormalizationFrontierState initialState =
            PaperFormalizationFrontierLifecycleService.CreateInitialState(
                frontier,
                admittedAt);
        PaperFrontierPlanningNodeRoute[] initialRoutes = frontier.FrontierContent.Nodes
            .Where(node => node.ParallelWave == 0)
            .Select((node, index) => new PaperFrontierPlanningNodeRoute(
                index + 1,
                node.NodeId,
                node.ClaimId,
                node.FormalizationKind,
                node.ParallelWave,
                node.Priority,
                "governed-selection"))
            .ToArray();
        ValidateRoutes(initialRoutes, frontier);
        return new(frontier, initialState, initialRoutes);
    }

    private static PaperAgentTask BuildTask(
        PaperFrontierPlanningAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperFrontierPlanningContext context)
    {
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile("frontier-planning");
        PaperAgentInputArtifact[] inputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperFrontierPlanningAgentSchemas.Dispatch,
                dispatchRef,
                dispatchRelativePath))
            .OrderBy(input => input.Schema, StringComparer.Ordinal)
            .ThenBy(input => input.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        var instruction = new StringBuilder();
        instruction.AppendLine("Plan the complete formalization frontier for exactly one portfolio-promoted theorem package.");
        instruction.AppendLine("Read the admitted A0 scope, A1 inventory, A2 theorem package, A3 audit, calibrated scorecard, candidate paper, literature boundary, portfolio judgment, decision, and updated portfolio state.");
        instruction.AppendLine($"Produce exactly one formalization node specification for each of the {context.TheoremPackage.TheoremPackageContent.Claims.Count} admitted theorem-package claims.");
        instruction.AppendLine("Preserve every claim identifier, informal statement, theorem kind, and dependency. The repository will recompute all node identities, dependency waves, critical-path depth, and initial ready set.");
        instruction.AppendLine("Mark every admitted main theorem as main-theorem, every sharpness claim as sharpness, and every admitted corollary as corollary. Choose definition, prerequisite, structural, counterexample, or proof-interface only where the theorem package supports that role.");
        instruction.AppendLine("For each claim, give a precise proposed formal statement, a concrete target Lean package and module, an integer priority from 0 to 100, and a machine-checkable acceptance criterion.");
        instruction.AppendLine("Use priority to expose the load-bearing proof spine and foundational prerequisites. Do not flatten the theorem DAG into one isolated lemma or route a dependent node before its certified dependencies.");
        instruction.AppendLine("State the overall planning rationale and preserve an explicit risk ledger for missing APIs, hidden assumptions, over-general statements, or likely prerequisite gaps.");
        instruction.AppendLine($"Return schema {PaperFrontierPlanningAgentSchemas.Draft} at outputs/formalization-frontier-draft.json.");
        return new PaperAgentTask(
            PaperAgentSchemas.Task,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            inputs,
            [new PaperAgentExpectedOutput(
                PaperFrontierPlanningAgentSchemas.Draft,
                "outputs/formalization-frontier-draft.json")],
            ["formalization-frontier"],
            instruction.ToString(),
            [
                "Do not add, remove, rename, weaken, strengthen, merge, or split theorem-package claims.",
                "Do not change claim dependencies, A3 scores, portfolio decisions, or exact evidence references.",
                "Do not mark a non-promoted paper as ready for formalization.",
                "Do not choose a main-theorem, sharpness, or corollary kind inconsistent with the admitted package.",
                "Do not run Lean, invoke Formalize, write Base, use Git or GitHub, or generate manuscript prose.",
                "Do not claim that a package, module, API, or dependency exists unless it is supported by the exact evidence; record uncertainty in the risk ledger."
            ],
            dispatch.RequestedAt);
    }

    private static PaperAgentInputArtifact[] BuildExactInputs(
        string root,
        string portfolioTaskRef,
        string portfolioCursorPath,
        string portfolioCursorRef,
        PaperPortfolioJudgmentAgentAdmissionCursor portfolioCursor,
        string portfolioDispatchPath,
        PaperPortfolioJudgmentAgentDispatch portfolioDispatch,
        PaperPortfolioJudgmentPaperInput coordinates)
    {
        var inputs = new List<PaperAgentInputArtifact>
        {
            new(
                PaperPortfolioJudgmentAgentSchemas.AdmissionCursor,
                portfolioCursorRef,
                RelativePath(root, portfolioCursorPath)),
            new(
                PaperPortfolioJudgmentAgentSchemas.Dispatch,
                portfolioCursor.DispatchRef,
                RelativePath(root, portfolioDispatchPath)),
            FindInput(
                portfolioDispatch.ExactInputs,
                PaperPortfolioSchemas.TheoryProgram,
                coordinates.TheoryProgramRef),
            FindInput(
                portfolioDispatch.ExactInputs,
                PaperTheoryFoundationSchemas.Scope,
                coordinates.ScopeRef),
            FindInput(
                portfolioDispatch.ExactInputs,
                PaperTheoryFoundationSchemas.Inventory,
                coordinates.InventoryRef),
            FindInput(
                portfolioDispatch.ExactInputs,
                PaperTheoryDeepeningSchemas.TheoremPackage,
                coordinates.TheoremPackageRef),
            FindInput(
                portfolioDispatch.ExactInputs,
                PaperTheoryAuditSchemas.Audit,
                coordinates.TheoryAuditRef),
            FindInput(
                portfolioDispatch.ExactInputs,
                PaperPortfolioDecisionSchemas.Scorecard,
                coordinates.ScorecardRef),
            FindInput(
                portfolioDispatch.ExactInputs,
                CandidateArtifactSchemas.CandidatePaper,
                coordinates.CandidatePaperRef),
            FindInput(
                portfolioDispatch.ExactInputs,
                CandidateArtifactSchemas.LiteratureResearch,
                coordinates.LiteratureResearchRef),
            StoredInput(portfolioCursor.Evidence),
            StoredInput(portfolioCursor.Decision),
            StoredInput(portfolioCursor.UpdatedPortfolio)
        };
        PaperAgentInputArtifact[] normalized = inputs
            .OrderBy(input => input.Schema, StringComparer.Ordinal)
            .ThenBy(input => input.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length != ExactInputCount)
        {
            throw new InvalidDataException(
                "Frontier-planning source route did not produce the exact evidence closure.");
        }
        return normalized;
    }

    private static PaperAgentInputArtifact StoredInput(
        PaperPortfolioJudgmentStoredArtifact stored) =>
        new(stored.Schema, stored.ArtifactRef, stored.ContentPath);
}
