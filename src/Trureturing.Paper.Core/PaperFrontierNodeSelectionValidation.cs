using System.Globalization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static partial class PaperFrontierNodeSelectionService
{
    private static readonly Regex GitSha1Pattern = new(
        "^[0-9a-f]{40}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex GidPattern = new(
        "^D[0-9]+/S[0-9]+/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*(?:\\.[A-Za-z_][A-Za-z0-9_']*)?$",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> FormalizationKinds = new(
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

    public static void Validate(
        PaperFrontierNodeSelectionAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        RequireExact(
            authorization.Schema,
            PaperFrontierNodeSelectionSchemas.Authorization,
            nameof(authorization.Schema));
        PaperFrontierNodeSelectionAuthorizationContent content =
            authorization.AuthorizationContent
            ?? throw new InvalidDataException(
                "authorization_content is required.");
        RequireDigest(content.FrontierPlanningTaskRef, nameof(content.FrontierPlanningTaskRef));
        RequireDigest(content.FrontierPlanningResultRef, nameof(content.FrontierPlanningResultRef));
        RequireDigest(content.FrontierPlanningDispatchRef, nameof(content.FrontierPlanningDispatchRef));
        RequireDigest(content.FrontierRef, nameof(content.FrontierRef));
        RequireDigest(content.InitialStateRef, nameof(content.InitialStateRef));
        RequirePaperId(content.PaperId);
        RequireDigest(content.TheoryProgramRef, nameof(content.TheoryProgramRef));
        RequireDigest(content.TheoremPackageRef, nameof(content.TheoremPackageRef));
        RequireDigest(content.PortfolioDecisionRef, nameof(content.PortfolioDecisionRef));
        if (content.DispatchOrder < 1
            || content.ParallelWave != 0
            || content.Priority is < 0 or > 100)
        {
            throw new InvalidDataException(
                "Frontier selection authorization route coordinates are invalid.");
        }
        RequireDigest(content.NodeId, nameof(content.NodeId));
        RequireClaimId(content.ClaimId);
        RequireFormalizationKind(content.FormalizationKind);
        ParseUtc(content.AuthorizedAt, nameof(content.AuthorizedAt));
        RequireIdentity(
            authorization.AuthorizationId,
            content,
            nameof(authorization.AuthorizationId));
    }

    public static void Validate(PaperFrontierVerificationBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        RequireExact(
            budget.Schema,
            PaperFrontierNodeSelectionSchemas.VerificationBudget,
            nameof(budget.Schema));
        PaperFrontierVerificationBudgetContent content = budget.BudgetContent
            ?? throw new InvalidDataException("budget_content is required.");
        RequireDigest(content.FrontierRef, nameof(content.FrontierRef));
        RequireDigest(content.NodeId, nameof(content.NodeId));
        RequireClaimId(content.ClaimId);
        if (content.MaximumFormalizationRounds is < 1 or > 64
            || !content.RequireExactTruthRelease
            || !content.RequireCertifiedDependencies
            || !content.CounterexampleIsUseful
            || !content.MissingPrerequisiteIsReportable)
        {
            throw new InvalidDataException(
                "Frontier verification budget weakened the governed verification policy.");
        }
        ParseUtc(content.CreatedAt, nameof(content.CreatedAt));
        RequireIdentity(budget.BudgetId, content, nameof(budget.BudgetId));
    }

    public static void Validate(PaperFrontierCurrentStateCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperFrontierNodeSelectionSchemas.CurrentStateCursor,
            nameof(cursor.Schema));
        RequireDigest(cursor.FrontierRef, nameof(cursor.FrontierRef));
        RequireStoredArtifact(
            cursor.State,
            PaperFormalizationFrontierSchemas.FrontierState);
        if (cursor.Version < 0)
        {
            throw new InvalidDataException(
                "Current frontier state version cannot be negative.");
        }
        ParseUtc(cursor.UpdatedAt, nameof(cursor.UpdatedAt));
    }

    public static void Validate(PaperFrontierFormalizationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        RequireExact(
            binding.Schema,
            PaperFrontierNodeSelectionSchemas.Binding,
            nameof(binding.Schema));
        PaperFrontierFormalizationBindingContent content = binding.BindingContent
            ?? throw new InvalidDataException("binding_content is required.");
        RequireDigest(content.FrontierPlanningTaskRef, nameof(content.FrontierPlanningTaskRef));
        RequireDigest(content.FrontierPlanningResultRef, nameof(content.FrontierPlanningResultRef));
        RequireDigest(content.FrontierRef, nameof(content.FrontierRef));
        RequireDigest(content.NodeId, nameof(content.NodeId));
        RequireClaimId(content.ClaimId);
        RequireDigest(content.AuthorizationRef, nameof(content.AuthorizationRef));
        RequireDigest(content.VerificationBudgetRef, nameof(content.VerificationBudgetRef));
        RequireDigest(content.SelectionRef, nameof(content.SelectionRef));
        RequireDigest(content.FormalizationRequestRef, nameof(content.FormalizationRequestRef));
        RequireDigest(content.SelectionEventRef, nameof(content.SelectionEventRef));
        RequireDigest(content.RequestEventRef, nameof(content.RequestEventRef));
        RequireDigest(content.TruthReleaseDigest, nameof(content.TruthReleaseDigest));
        RequireGitSha1(content.SourceCommit, nameof(content.SourceCommit));
        RequireGitSha1(content.SourceTree, nameof(content.SourceTree));
        RequireGid(content.Gid);
        ParseUtc(content.CreatedAt, nameof(content.CreatedAt));
        RequireIdentity(binding.BindingId, content, nameof(binding.BindingId));
    }

    public static void Validate(PaperFrontierFormalizationBindingLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        RequireExact(
            lookup.Schema,
            PaperFrontierNodeSelectionSchemas.BindingLookup,
            nameof(lookup.Schema));
        RequireDigest(
            lookup.FormalizationRequestRef,
            nameof(lookup.FormalizationRequestRef));
        RequireDigest(lookup.BindingRef, nameof(lookup.BindingRef));
        RequireDigest(lookup.BindingBlobRef, nameof(lookup.BindingBlobRef));
        RequireRepositoryRelativePath(lookup.BindingPath, nameof(lookup.BindingPath));
    }

    public static void Validate(PaperFrontierNodeSelectionAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperFrontierNodeSelectionSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        RequireDigest(cursor.FrontierPlanningTaskRef, nameof(cursor.FrontierPlanningTaskRef));
        RequireDigest(cursor.FrontierPlanningResultRef, nameof(cursor.FrontierPlanningResultRef));
        RequireDigest(cursor.FrontierPlanningDispatchRef, nameof(cursor.FrontierPlanningDispatchRef));
        RequireDigest(cursor.FrontierRef, nameof(cursor.FrontierRef));
        RequireDigest(cursor.InitialStateRef, nameof(cursor.InitialStateRef));
        RequirePaperId(cursor.PaperId);
        RequireDigest(cursor.TheoryProgramRef, nameof(cursor.TheoryProgramRef));
        RequireDigest(cursor.TheoremPackageRef, nameof(cursor.TheoremPackageRef));
        RequireDigest(cursor.PortfolioDecisionRef, nameof(cursor.PortfolioDecisionRef));
        if (cursor.DispatchOrder < 1
            || cursor.ParallelWave != 0
            || cursor.Priority is < 0 or > 100)
        {
            throw new InvalidDataException(
                "Frontier node selection cursor route coordinates are invalid.");
        }
        RequireDigest(cursor.NodeId, nameof(cursor.NodeId));
        RequireClaimId(cursor.ClaimId);
        RequireFormalizationKind(cursor.FormalizationKind);
        RequireStoredArtifact(
            cursor.Authorization,
            PaperFrontierNodeSelectionSchemas.Authorization);
        RequireStoredArtifact(
            cursor.VerificationBudget,
            PaperFrontierNodeSelectionSchemas.VerificationBudget);
        RequireDigest(cursor.SelectionRef, nameof(cursor.SelectionRef));
        RequireDigest(cursor.SelectionBlobRef, nameof(cursor.SelectionBlobRef));
        RequireAbsolutePath(cursor.SelectionPath, nameof(cursor.SelectionPath));
        RequireDigest(
            cursor.FormalizationRequestRef,
            nameof(cursor.FormalizationRequestRef));
        RequireDigest(
            cursor.FormalizationRequestBlobRef,
            nameof(cursor.FormalizationRequestBlobRef));
        RequireAbsolutePath(
            cursor.FormalizationRequestPath,
            nameof(cursor.FormalizationRequestPath));
        RequireStoredArtifact(
            cursor.SelectionEvent,
            PaperFormalizationFrontierSchemas.FrontierEvent);
        RequireStoredArtifact(
            cursor.RequestEvent,
            PaperFormalizationFrontierSchemas.FrontierEvent);
        RequireStoredArtifact(
            cursor.FrontierState,
            PaperFormalizationFrontierSchemas.FrontierState);
        RequireStoredArtifact(
            cursor.Binding,
            PaperFrontierNodeSelectionSchemas.Binding);
        RequireDigest(cursor.TruthReleaseDigest, nameof(cursor.TruthReleaseDigest));
        RequireGitSha1(cursor.SourceCommit, nameof(cursor.SourceCommit));
        RequireGitSha1(cursor.SourceTree, nameof(cursor.SourceTree));
        RequireGid(cursor.Gid);
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static void ValidateReplay(
        string root,
        PaperFrontierNodeSelectionAdmissionCursor cursor,
        PaperFrontierNodeSelectionSource source)
    {
        Validate(cursor);
        if (!string.Equals(
                cursor.FrontierPlanningTaskRef,
                source.PlanningCursor.TaskRef,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.FrontierPlanningResultRef,
                source.PlanningCursor.ResultRef,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.FrontierPlanningDispatchRef,
                source.PlanningCursor.DispatchRef,
                StringComparison.Ordinal)
            || !string.Equals(cursor.FrontierRef, source.Frontier.FrontierId, StringComparison.Ordinal)
            || !string.Equals(cursor.InitialStateRef, source.InitialState.StateId, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, source.Program.ProgramContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, source.Program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoremPackageRef, source.TheoremPackage.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(cursor.PortfolioDecisionRef, source.PlanningCursor.PortfolioDecisionRef, StringComparison.Ordinal)
            || cursor.DispatchOrder != source.Route.DispatchOrder
            || !string.Equals(cursor.NodeId, source.Node.NodeId, StringComparison.Ordinal)
            || !string.Equals(cursor.ClaimId, source.Node.ClaimId, StringComparison.Ordinal)
            || !string.Equals(cursor.FormalizationKind, source.Node.FormalizationKind, StringComparison.Ordinal)
            || cursor.ParallelWave != source.Node.ParallelWave
            || cursor.Priority != source.Node.Priority
            || !string.Equals(cursor.AdmittedAt, source.PlanningCursor.AdmittedAt, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier node selection replay changed its planning or node identity.");
        }

        PaperFrontierNodeSelectionAuthorization authorization =
            ReadStoredEnvelope<PaperFrontierNodeSelectionAuthorization>(
                root,
                cursor.Authorization,
                "Frontier selection authorization");
        Validate(authorization);
        PaperFrontierNodeSelectionAuthorization expectedAuthorization =
            CreateAuthorization(source, cursor.AdmittedAt);
        RequireCanonicalEquality(
            authorization,
            expectedAuthorization,
            "Frontier selection authorization");

        PaperFrontierVerificationBudget budget =
            ReadStoredEnvelope<PaperFrontierVerificationBudget>(
                root,
                cursor.VerificationBudget,
                "Frontier verification budget");
        Validate(budget);
        PaperFrontierVerificationBudget expectedBudget =
            CreateVerificationBudget(source, cursor.AdmittedAt);
        RequireCanonicalEquality(
            budget,
            expectedBudget,
            "Frontier verification budget");

        PaperResearchSelection selection = ReadSelection(
            root,
            cursor.SelectionPath,
            cursor.SelectionBlobRef);
        if (!string.Equals(selection.SelectionId, cursor.SelectionRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Stored frontier selection changed its semantic identity.");
        }
        PaperResearchSelection expectedSelection = CreateSelection(
            source,
            authorization,
            budget,
            cursor.AdmittedAt);
        if (!PaperResearchSelectionJson.Write(selection).AsSpan().SequenceEqual(
                PaperResearchSelectionJson.Write(expectedSelection)))
        {
            throw new InvalidDataException(
                "Stored frontier selection differs from the deterministic node selection.");
        }

        FormalizationRequest request = ReadFormalizationRequest(
            root,
            cursor.FormalizationRequestPath,
            cursor.FormalizationRequestBlobRef);
        if (!string.Equals(
                request.RequestId,
                cursor.FormalizationRequestRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Stored canonical request changed its semantic identity.");
        }
        FormalizationRequest expectedRequest =
            PaperResearchSelectionService.BuildFormalizationRequest(
                selection,
                source.ResearchInput);
        if (!PaperResearchSelectionJson.Write(request).AsSpan().SequenceEqual(
                PaperResearchSelectionJson.Write(expectedRequest)))
        {
            throw new InvalidDataException(
                "Stored canonical request differs from the deterministic selection handoff.");
        }

        PaperFormalizationFrontierEvent selectionEvent =
            ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                root,
                cursor.SelectionEvent,
                "Governed selection frontier event");
        PaperFormalizationFrontierLifecycleService.Validate(
            selectionEvent,
            source.Frontier);
        PaperFormalizationFrontierEvent requestEvent =
            ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                root,
                cursor.RequestEvent,
                "Canonical request frontier event");
        PaperFormalizationFrontierLifecycleService.Validate(
            requestEvent,
            source.Frontier);
        if (!string.Equals(
                selectionEvent.EventContent.ArtifactFamily,
                PaperFormalizationFrontierLifecycleService.GovernedSelectionFamily,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionEvent.EventContent.ArtifactSchema,
                PaperResearchSelectionSchemas.Selection,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionEvent.EventContent.ArtifactRef,
                selection.SelectionId,
                StringComparison.Ordinal)
            || !string.Equals(
                requestEvent.EventContent.ArtifactFamily,
                PaperFormalizationFrontierLifecycleService.CanonicalRequestFamily,
                StringComparison.Ordinal)
            || !string.Equals(
                requestEvent.EventContent.ArtifactSchema,
                PaperResearchSelectionSchemas.FormalizationRequest,
                StringComparison.Ordinal)
            || !string.Equals(
                requestEvent.EventContent.ArtifactRef,
                request.RequestId,
                StringComparison.Ordinal)
            || !string.Equals(
                requestEvent.EventContent.PredecessorEventRef,
                selectionEvent.EventId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Stored frontier lifecycle events changed the selection or request binding.");
        }

        PaperFormalizationFrontierState state =
            ReadStoredState(root, cursor.FrontierState);
        PaperFormalizationFrontierLifecycleService.Validate(
            state,
            source.Frontier);
        PaperFormalizationFrontierNodeState nodeState = state.StateContent.NodeStates.Single(
            value => string.Equals(value.NodeId, source.Node.NodeId, StringComparison.Ordinal));
        if (!state.StateContent.AppliedEventRefs.Contains(
                selectionEvent.EventId,
                StringComparer.Ordinal)
            || !state.StateContent.AppliedEventRefs.Contains(
                requestEvent.EventId,
                StringComparer.Ordinal)
            || !string.Equals(nodeState.Status, "request-recorded", StringComparison.Ordinal)
            || !string.Equals(nodeState.LatestEventRef, requestEvent.EventId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Stored frontier state did not admit both governed lifecycle events.");
        }

        PaperFrontierFormalizationBinding binding =
            ReadStoredEnvelope<PaperFrontierFormalizationBinding>(
                root,
                cursor.Binding,
                "Frontier formalization binding");
        Validate(binding);
        PaperFrontierFormalizationBinding expectedBinding = CreateBinding(
            source,
            authorization,
            budget,
            selection,
            request,
            selectionEvent,
            requestEvent,
            cursor.AdmittedAt);
        RequireCanonicalEquality(
            binding,
            expectedBinding,
            "Frontier formalization binding");

        if (!string.Equals(cursor.TruthReleaseDigest, source.ResearchInput.TruthReleaseDigest, StringComparison.Ordinal)
            || !string.Equals(cursor.SourceCommit, source.ResearchInput.SourceCommit, StringComparison.Ordinal)
            || !string.Equals(cursor.SourceTree, source.ResearchInput.SourceTree, StringComparison.Ordinal)
            || !string.Equals(cursor.Gid, request.Target.PreferredGid, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection replay changed the exact truth release or target GID.");
        }
    }

    private static T ReadStoredEnvelope<T>(
        string root,
        PaperFrontierNodeSelectionStoredArtifact stored,
        string name)
    {
        byte[] bytes = ReadRepositoryArtifact(
            root,
            stored.RepositoryRelativePath,
            stored.BlobRef,
            name);
        return PaperResearchInputJson.DeserializeStrict<T>(bytes);
    }

    private static PaperResearchSelection ReadSelection(
        string root,
        string path,
        string blobRef)
    {
        byte[] bytes = ReadResearchSelectionArtifact(
            root,
            path,
            blobRef,
            "Paper research selection");
        PaperResearchSelection selection =
            PaperResearchSelectionJson.ReadSelection(bytes);
        PaperResearchSelectionService.Validate(selection);
        return selection;
    }

    private static FormalizationRequest ReadFormalizationRequest(
        string root,
        string path,
        string blobRef)
    {
        byte[] bytes = ReadResearchSelectionArtifact(
            root,
            path,
            blobRef,
            "Formalization request");
        FormalizationRequest request =
            PaperResearchSelectionJson.ReadFormalizationRequest(bytes);
        PaperResearchSelectionService.Validate(request);
        return request;
    }

    private static byte[] ReadResearchSelectionArtifact(
        string root,
        string path,
        string expectedRef,
        string name)
    {
        string full = Path.GetFullPath(path);
        string boundary = Path.GetFullPath(Path.Combine(
            root,
            "artifacts",
            "research-selections"));
        RequirePathWithin(boundary, full, name);
        RejectReparsePointsBetween(boundary, full, name);
        return ReadImmutable(full, expectedRef, name);
    }

    private static void RequireStoredArtifact(
        PaperFrontierNodeSelectionStoredArtifact stored,
        string expectedSchema)
    {
        ArgumentNullException.ThrowIfNull(stored);
        RequireExact(stored.Schema, expectedSchema, nameof(stored.Schema));
        RequireDigest(stored.ArtifactRef, nameof(stored.ArtifactRef));
        RequireDigest(stored.BlobRef, nameof(stored.BlobRef));
        RequireRepositoryRelativePath(
            stored.RepositoryRelativePath,
            nameof(stored.RepositoryRelativePath));
    }

    private static void RequireCanonicalEquality<T>(
        T actual,
        T expected,
        string name)
    {
        if (!CanonicalJson.Serialize(actual).AsSpan().SequenceEqual(
                CanonicalJson.Serialize(expected)))
        {
            throw new InvalidDataException($"{name} changed canonical content.");
        }
    }

    private static void RequireIdentity<T>(
        string reference,
        T content,
        string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, ContentReference(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} does not address canonical content bytes.");
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

    private static void RequireGitSha1(string value, string name)
    {
        if (!GitSha1Pattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be a 40-character lowercase Git object id.");
        }
    }

    private static void RequireGid(string value)
    {
        if (!GidPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException("gid is not a canonical theorem GID.");
        }
    }

    private static void RequirePaperId(string value)
    {
        if (!PaperIdPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException("paper_id is not a canonical identifier.");
        }
    }

    private static void RequireClaimId(string value)
    {
        if (!ClaimIdPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException("claim_id is not a canonical identifier.");
        }
    }

    private static void RequireFormalizationKind(string value)
    {
        if (!FormalizationKinds.Contains(value ?? string.Empty))
        {
            throw new InvalidDataException(
                "formalization_kind is unsupported.");
        }
    }

    private static void RequireSchema(string value, string name)
    {
        if (!SchemaPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} is not a versioned schema name.");
        }
    }

    private static void RequireRepositoryRelativePath(
        string value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 4096
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || !RelativePathPattern.IsMatch(value))
        {
            throw new InvalidDataException(
                $"{name} is not a canonical repository-relative path.");
        }
        foreach (string segment in value.Split('/'))
        {
            if (segment is "." or ".."
                || segment.All(character => character == '.'))
            {
                throw new InvalidDataException(
                    $"{name} contains an unsafe path segment.");
            }
        }
    }

    private static void RequireAbsolutePath(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 4096
            || !Path.IsPathRooted(value)
            || value.Contains('\n')
            || value.Contains('\r'))
        {
            throw new InvalidDataException($"{name} is not a canonical absolute path.");
        }
    }

    private static void RequireExact(
        string actual,
        string expected,
        string name)
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
