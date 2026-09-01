using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

internal sealed record PaperFrontierNodeSelectionSource(
    PaperFrontierPlanningAgentAdmissionCursor PlanningCursor,
    PaperFrontierPlanningAgentDispatch PlanningDispatch,
    PaperFormalizationFrontier Frontier,
    PaperFormalizationFrontierState InitialState,
    PaperTheoryProgram Program,
    PaperTheoremPackage TheoremPackage,
    PaperFrontierPlanningNodeRoute Route,
    PaperFormalizationFrontierNode Node,
    PaperResearchInput ResearchInput);

public static partial class PaperFrontierNodeSelectionService
{
    private const int MaximumControlBytes = 4 * 1024 * 1024;
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;
    private const int MaximumFormalizationRounds = 8;

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
    private static readonly Regex DomainPrefixPattern = new(
        "^D[0-9]+$",
        RegexOptions.CultureInvariant);
    private static readonly Regex SectionPrefixPattern = new(
        "^S[0-9]+$",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedRepositoryRoots = new(
        ["artifacts", "Papers", "work", "contracts", "docs", "src", "tools", "tests"],
        StringComparer.Ordinal);

    public static PaperFrontierNodeSelectionAdmitted Admit(
        string repositoryRoot,
        string frontierPlanningTaskRef,
        string nodeId)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(frontierPlanningTaskRef, nameof(frontierPlanningTaskRef));
        RequireDigest(nodeId, nameof(nodeId));
        PaperFrontierNodeSelectionSource source = LoadSource(
            root,
            frontierPlanningTaskRef,
            nodeId);

        using FileStream frontierLock = AcquireFrontierLock(
            root,
            source.Frontier.FrontierId);
        RecoverCurrentStateCursor(root, source);
        string cursorPath = AdmissionCursorPath(
            root,
            source.Frontier.FrontierId,
            nodeId);
        if (File.Exists(cursorPath))
        {
            PaperFrontierNodeSelectionAdmissionCursor existing =
                ReadAdmissionCursor(cursorPath);
            ValidateReplay(root, existing, source);
            WriteBindingLookup(
                root,
                existing.FormalizationRequestRef,
                existing.Binding);
            RepairCurrentStatePointer(root, source, existing);
            return ToAdmitted(existing, replayed: true);
        }

        PaperFormalizationFrontierState currentState =
            ReadOrInitializeCurrentState(root, source);
        PaperFormalizationFrontierNodeState currentNodeState =
            currentState.StateContent.NodeStates.Single(value =>
                string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
        if (!string.Equals(
                currentNodeState.Status,
                PaperFormalizationFrontierService.InitialNodeStatus,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier node has already left selection-pending without a matching selection cursor.");
        }

        string admittedAt = source.PlanningCursor.AdmittedAt;
        PaperFrontierNodeSelectionAuthorization authorization =
            CreateAuthorization(source, admittedAt);
        PaperFrontierVerificationBudget budget =
            CreateVerificationBudget(source, admittedAt);
        PaperResearchSelection selection = CreateSelection(
            source,
            authorization,
            budget,
            admittedAt);
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(
                selection,
                source.ResearchInput);

        PaperFormalizationFrontierEvent selectionEvent =
            PaperFormalizationFrontierLifecycleService.CreateEvent(
                source.Frontier,
                currentState,
                nodeId,
                PaperFormalizationFrontierLifecycleService.GovernedSelectionFamily,
                PaperResearchSelectionSchemas.Selection,
                selection.SelectionId,
                string.Empty,
                string.Empty,
                $"Repository-governed frontier authorization {authorization.AuthorizationId} admitted exact selection {selection.SelectionId}.",
                admittedAt);
        PaperFormalizationFrontierState selectedState =
            PaperFormalizationFrontierLifecycleService.ApplyEvent(
                source.Frontier,
                currentState,
                selectionEvent,
                admittedAt);
        PaperFormalizationFrontierEvent requestEvent =
            PaperFormalizationFrontierLifecycleService.CreateEvent(
                source.Frontier,
                selectedState,
                nodeId,
                PaperFormalizationFrontierLifecycleService.CanonicalRequestFamily,
                PaperResearchSelectionSchemas.FormalizationRequest,
                request.RequestId,
                string.Empty,
                string.Empty,
                $"Canonical Formalize request {request.RequestId} was derived from selection {selection.SelectionId} and the exact paper research input.",
                admittedAt);
        PaperFormalizationFrontierState requestedState =
            PaperFormalizationFrontierLifecycleService.ApplyEvent(
                source.Frontier,
                selectedState,
                requestEvent,
                admittedAt);

        PaperFrontierNodeSelectionStoredArtifact storedAuthorization =
            StoreEnvelope(
                root,
                "authorizations",
                authorization.Schema,
                authorization.AuthorizationId,
                authorization);
        PaperFrontierNodeSelectionStoredArtifact storedBudget =
            StoreEnvelope(
                root,
                "verification-budgets",
                budget.Schema,
                budget.BudgetId,
                budget);
        (string selectionBlobRef, string selectionPath) =
            StoreResearchSelection(root, authorization.AuthorizationId, selection);
        (string requestBlobRef, string requestPath) =
            StoreFormalizationRequest(root, authorization.AuthorizationId, request);
        PaperFrontierNodeSelectionStoredArtifact storedSelectionEvent =
            StoreEnvelope(
                root,
                "events",
                selectionEvent.Schema,
                selectionEvent.EventId,
                selectionEvent);
        PaperFrontierNodeSelectionStoredArtifact storedRequestEvent =
            StoreEnvelope(
                root,
                "events",
                requestEvent.Schema,
                requestEvent.EventId,
                requestEvent);
        PaperFrontierNodeSelectionStoredArtifact storedState =
            StoreEnvelope(
                root,
                "states",
                requestedState.Schema,
                requestedState.StateId,
                requestedState);

        PaperFrontierFormalizationBinding binding = CreateBinding(
            source,
            authorization,
            budget,
            selection,
            request,
            selectionEvent,
            requestEvent,
            admittedAt);
        PaperFrontierNodeSelectionStoredArtifact storedBinding =
            StoreEnvelope(
                root,
                "bindings",
                binding.Schema,
                binding.BindingId,
                binding);

        var cursor = new PaperFrontierNodeSelectionAdmissionCursor(
            PaperFrontierNodeSelectionSchemas.AdmissionCursor,
            source.PlanningCursor.TaskRef,
            source.PlanningCursor.ResultRef,
            source.PlanningCursor.DispatchRef,
            source.Frontier.FrontierId,
            source.InitialState.StateId,
            source.Program.ProgramContent.PaperId,
            source.Program.TheoryProgramId,
            source.TheoremPackage.TheoremPackageId,
            source.PlanningCursor.PortfolioDecisionRef,
            source.Route.DispatchOrder,
            source.Node.NodeId,
            source.Node.ClaimId,
            source.Node.FormalizationKind,
            source.Node.ParallelWave,
            source.Node.Priority,
            storedAuthorization,
            storedBudget,
            selection.SelectionId,
            selectionBlobRef,
            selectionPath,
            request.RequestId,
            requestBlobRef,
            requestPath,
            storedSelectionEvent,
            storedRequestEvent,
            storedState,
            storedBinding,
            source.ResearchInput.TruthReleaseDigest,
            source.ResearchInput.SourceCommit,
            source.ResearchInput.SourceTree,
            request.Target.PreferredGid!,
            admittedAt);
        Validate(cursor);

        PutImmutable(cursorPath, CanonicalJson.Serialize(cursor));
        WriteBindingLookup(root, request.RequestId, storedBinding);
        WriteCurrentStateCursor(
            root,
            source.Frontier,
            storedState,
            requestedState);
        return ToAdmitted(cursor, replayed: false);
    }

    private static PaperFrontierNodeSelectionAuthorization CreateAuthorization(
        PaperFrontierNodeSelectionSource source,
        string authorizedAt)
    {
        var content = new PaperFrontierNodeSelectionAuthorizationContent(
            source.PlanningCursor.TaskRef,
            source.PlanningCursor.ResultRef,
            source.PlanningCursor.DispatchRef,
            source.Frontier.FrontierId,
            source.InitialState.StateId,
            source.Program.ProgramContent.PaperId,
            source.Program.TheoryProgramId,
            source.TheoremPackage.TheoremPackageId,
            source.PlanningCursor.PortfolioDecisionRef,
            source.Route.DispatchOrder,
            source.Node.NodeId,
            source.Node.ClaimId,
            source.Node.FormalizationKind,
            source.Node.ParallelWave,
            source.Node.Priority,
            authorizedAt);
        var authorization = new PaperFrontierNodeSelectionAuthorization(
            PaperFrontierNodeSelectionSchemas.Authorization,
            ContentReference(content),
            content);
        Validate(authorization);
        return authorization;
    }

    private static PaperFrontierVerificationBudget CreateVerificationBudget(
        PaperFrontierNodeSelectionSource source,
        string createdAt)
    {
        var content = new PaperFrontierVerificationBudgetContent(
            source.Frontier.FrontierId,
            source.Node.NodeId,
            source.Node.ClaimId,
            MaximumFormalizationRounds,
            RequireExactTruthRelease: true,
            RequireCertifiedDependencies: true,
            CounterexampleIsUseful: true,
            MissingPrerequisiteIsReportable: true,
            createdAt);
        var budget = new PaperFrontierVerificationBudget(
            PaperFrontierNodeSelectionSchemas.VerificationBudget,
            ContentReference(content),
            content);
        Validate(budget);
        return budget;
    }

    private static PaperResearchSelection CreateSelection(
        PaperFrontierNodeSelectionSource source,
        PaperFrontierNodeSelectionAuthorization authorization,
        PaperFrontierVerificationBudget budget,
        string selectedAt)
    {
        RequireSelectionCompatibility(source);
        string gid = BuildGid(source.Node);
        string[] dependencyGids = source.Node.DependencyNodeIds
            .Select(dependency => BuildGid(
                PaperFormalizationFrontierService.RequireNode(
                    source.Frontier,
                    dependency)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] knownResults = source.TheoremPackage.TheoremPackageContent
            .KnownResultsToCite
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] reuseApi = dependencyGids
            .Concat(knownResults)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string statementDigest = TextReference(source.Node.FormalStatement);
        string dependencyDigest = TextReference(
            string.Join("\n", source.Node.DependencyNodeIds
                .OrderBy(value => value, StringComparer.Ordinal)));
        string nextReleaseAt = ParseUtc(selectedAt, nameof(selectedAt))
            .AddDays(7)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var content = new PaperResearchSelectionContent(
            source.ResearchInput.TruthReleaseDigest,
            source.ResearchInput.TopologyDigest,
            source.Program.ProgramContent.PaperResearchInputRef,
            source.Program.ProgramContent.IntuitionProposalRef,
            source.Program.ProgramContent.CandidatePaperRef,
            source.Program.ProgramContent.LiteratureResearchRef,
            source.Program.ProgramContent.PaperId,
            $"Frontier {source.Node.FormalizationKind} claim {source.Node.ClaimId}: {source.Node.Title}",
            new PaperResearchTarget(
                source.Node.FormalStatement,
                gid,
                dependencyGids,
                knownResults,
                [
                    $"Do not replace frontier node {source.Node.NodeId} or theorem-package claim {source.Node.ClaimId}.",
                    $"Do not weaken, strengthen, or paraphrase the exact formal statement addressed by {statementDigest}.",
                    $"Do not change the certified dependency set addressed by {dependencyDigest}."
                ]),
            $"Selection is limited to frontier {source.Frontier.FrontierId}, node {source.Node.NodeId}, claim {source.Node.ClaimId}, and formal-statement digest {statementDigest}.",
            "A well-typed counterexample to the exact statement, or evidence that a required prerequisite is absent from the exact truth release.",
            source.Node.AcceptanceCriterion,
            reuseApi,
            new PaperResearchFailureSemantics(
                budget.BudgetContent.CounterexampleIsUseful,
                budget.BudgetContent.MissingPrerequisiteIsReportable),
            budget.BudgetId,
            authorization.AuthorizationId,
            selectedAt,
            nextReleaseAt);
        return PaperResearchSelectionService.Create(content);
    }

    private static PaperFrontierFormalizationBinding CreateBinding(
        PaperFrontierNodeSelectionSource source,
        PaperFrontierNodeSelectionAuthorization authorization,
        PaperFrontierVerificationBudget budget,
        PaperResearchSelection selection,
        FormalizationRequest request,
        PaperFormalizationFrontierEvent selectionEvent,
        PaperFormalizationFrontierEvent requestEvent,
        string createdAt)
    {
        var content = new PaperFrontierFormalizationBindingContent(
            source.PlanningCursor.TaskRef,
            source.PlanningCursor.ResultRef,
            source.Frontier.FrontierId,
            source.Node.NodeId,
            source.Node.ClaimId,
            authorization.AuthorizationId,
            budget.BudgetId,
            selection.SelectionId,
            request.RequestId,
            selectionEvent.EventId,
            requestEvent.EventId,
            source.ResearchInput.TruthReleaseDigest,
            source.ResearchInput.SourceCommit,
            source.ResearchInput.SourceTree,
            request.Target.PreferredGid!,
            createdAt);
        var binding = new PaperFrontierFormalizationBinding(
            PaperFrontierNodeSelectionSchemas.Binding,
            ContentReference(content),
            content);
        Validate(binding);
        return binding;
    }

    private static string BuildGid(PaperFormalizationFrontierNode node)
    {
        string[] raw = node.TargetLeanModule
            .Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries);
        string[] moduleParts = raw.Select(SanitizePathSegment).ToArray();
        var path = new List<string>();
        if (moduleParts.Length >= 3
            && DomainPrefixPattern.IsMatch(moduleParts[0])
            && SectionPrefixPattern.IsMatch(moduleParts[1]))
        {
            path.Add(moduleParts[0]);
            path.Add(moduleParts[1]);
            path.AddRange(moduleParts.Skip(2));
        }
        else
        {
            path.Add("D0");
            path.Add("S0");
            path.Add("Paper");
            path.AddRange(moduleParts);
        }
        if (path.Count < 3)
        {
            path.Add("Frontier");
        }
        return string.Join('/', path) + "." + SanitizeDeclaration(node.ClaimId);
    }

    private static string SanitizePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-'
                ? character
                : '_');
        }
        return builder.Length == 0 ? "Frontier" : builder.ToString();
    }

    private static string SanitizeDeclaration(string value)
    {
        var builder = new StringBuilder(value.Length + 6);
        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '\''
                ? character
                : '_');
        }
        if (builder.Length == 0
            || !(char.IsLetter(builder[0]) || builder[0] == '_'))
        {
            builder.Insert(0, "claim_");
        }
        return builder.ToString();
    }

    private static void RequireSelectionCompatibility(
        PaperFrontierNodeSelectionSource source)
    {
        if (source.Node.FormalStatement.Length > 16384)
        {
            throw new InvalidDataException(
                "Frontier formal statement exceeds the canonical Formalize selection boundary and cannot be truncated.");
        }
        if (source.Node.AcceptanceCriterion.Length > 8192)
        {
            throw new InvalidDataException(
                "Frontier acceptance criterion exceeds the canonical selection contribution boundary and cannot be truncated.");
        }
        foreach (string knownResult in source.TheoremPackage.TheoremPackageContent.KnownResultsToCite)
        {
            if (knownResult.Length > 4096)
            {
                throw new InvalidDataException(
                    "A known-result citation exceeds the canonical selection assumption boundary and cannot be truncated.");
            }
        }
    }

    private static PaperFrontierNodeSelectionAdmitted ToAdmitted(
        PaperFrontierNodeSelectionAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperFrontierNodeSelectionSchemas.ResultAdmitted,
            cursor.FrontierPlanningTaskRef,
            cursor.FrontierPlanningResultRef,
            cursor.FrontierPlanningDispatchRef,
            cursor.FrontierRef,
            cursor.InitialStateRef,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.TheoremPackageRef,
            cursor.PortfolioDecisionRef,
            cursor.DispatchOrder,
            cursor.NodeId,
            cursor.ClaimId,
            cursor.FormalizationKind,
            cursor.ParallelWave,
            cursor.Priority,
            cursor.Authorization,
            cursor.VerificationBudget,
            cursor.SelectionRef,
            cursor.SelectionBlobRef,
            cursor.SelectionPath,
            cursor.FormalizationRequestRef,
            cursor.FormalizationRequestBlobRef,
            cursor.FormalizationRequestPath,
            cursor.SelectionEvent,
            cursor.RequestEvent,
            cursor.FrontierState,
            cursor.Binding,
            cursor.TruthReleaseDigest,
            cursor.SourceCommit,
            cursor.SourceTree,
            cursor.Gid,
            cursor.AdmittedAt,
            replayed);
}
