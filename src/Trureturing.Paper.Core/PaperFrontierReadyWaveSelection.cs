namespace Trureturing.Paper.Core;

public static partial class PaperFrontierNodeSelectionService
{
    public static PaperFrontierReadyWaveSelectionAdmitted AdmitReadyWave(
        string repositoryRoot,
        string frontierRef,
        string readySetRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        PaperFrontierReadySetAuthority authority =
            LoadReadySetAuthority(root, frontierRef, readySetRef);
        PaperFrontierReadySet readySet = authority.ReadySet;
        if (readySet.ReadySetContent.ReadyNodes.Count < 1)
        {
            throw new InvalidDataException(
                "A dependency-ready wave must contain at least one node.");
        }

        RecoverProgressState(root, authority.TriggerContext);
        PaperFormalizationFrontierState current =
            ReadOrInitializeCurrentState(root, authority.TriggerContext.Source);
        RequireEventSubset(authority.SourceState, current);
        ValidateReadyNodesAgainstCurrentState(
            authority.TriggerContext.Source.Frontier,
            readySet,
            current);

        string cursorPath = ReadyWaveCursorPath(
            root,
            frontierRef,
            readySetRef);
        if (File.Exists(cursorPath))
        {
            PaperFrontierReadyWaveSelectionCursor existing =
                ReadReadyWaveCursor(cursorPath);
            PaperFrontierNodeSelectionAdmitted[] replayed =
                ReplayReadyWave(root, authority, existing);
            return ToReadyWaveAdmitted(existing, replayed, replayed: true);
        }

        var admitted = new List<PaperFrontierNodeSelectionAdmitted>();
        foreach (PaperFrontierReadyNode node
            in readySet.ReadySetContent.ReadyNodes)
        {
            PaperFrontierNodeSelectionAdmitted result = Admit(
                root,
                authority.TriggerContext.Source.PlanningCursor.TaskRef,
                node.NodeId);
            ValidateReadyNodeAdmission(
                authority,
                node,
                result);
            admitted.Add(result);
        }

        PaperFrontierReadyWaveNodeAdmission[] stableAdmissions = admitted
            .Select(ToStableAdmission)
            .ToArray();
        var cursor = new PaperFrontierReadyWaveSelectionCursor(
            PaperFrontierReadyWaveSelectionSchemas.AdmissionCursor,
            readySet.ReadySetId,
            readySet.ReadySetContent.FrontierRef,
            readySet.ReadySetContent.TriggerNodeId,
            readySet.ReadySetContent.TriggerManifestRef,
            readySet.ReadySetContent.FrontierStateRef,
            authority.TriggerContext.Source.PlanningCursor.TaskRef,
            authority.TriggerContext.Source.Program.ProgramContent.PaperId,
            authority.TriggerContext.Source.Program.TheoryProgramId,
            authority.TriggerContext.Source.TheoremPackage.TheoremPackageId,
            stableAdmissions,
            readySet.ReadySetContent.CreatedAt);
        Validate(cursor);
        PutImmutable(cursorPath, CanonicalJson.Serialize(cursor));
        return ToReadyWaveAdmitted(
            cursor,
            admitted.ToArray(),
            replayed: false);
    }

    public static void Validate(
        PaperFrontierReadyWaveSelectionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperFrontierReadyWaveSelectionSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        RequireDigest(cursor.ReadySetRef, nameof(cursor.ReadySetRef));
        RequireDigest(cursor.FrontierRef, nameof(cursor.FrontierRef));
        RequireDigest(cursor.TriggerNodeId, nameof(cursor.TriggerNodeId));
        RequireDigest(
            cursor.TriggerManifestRef,
            nameof(cursor.TriggerManifestRef));
        RequireDigest(cursor.ReleaseStateRef, nameof(cursor.ReleaseStateRef));
        RequireDigest(
            cursor.FrontierPlanningTaskRef,
            nameof(cursor.FrontierPlanningTaskRef));
        RequirePaperId(cursor.PaperId);
        RequireDigest(cursor.TheoryProgramRef, nameof(cursor.TheoryProgramRef));
        RequireDigest(cursor.TheoremPackageRef, nameof(cursor.TheoremPackageRef));
        if (cursor.NodeAdmissions is null
            || cursor.NodeAdmissions.Count < 1)
        {
            throw new InvalidDataException(
                "Ready-wave cursor must contain at least one node admission.");
        }
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        var claims = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < cursor.NodeAdmissions.Count; index++)
        {
            PaperFrontierReadyWaveNodeAdmission admission =
                cursor.NodeAdmissions[index]
                ?? throw new InvalidDataException(
                    "Ready-wave node admissions cannot contain null.");
            if (admission.DispatchOrder != index + 1
                || admission.ParallelWave < 1
                || admission.Priority is < 0 or > 100)
            {
                throw new InvalidDataException(
                    "Ready-wave node order, wave, or priority is invalid.");
            }
            RequireDigest(admission.NodeId, nameof(admission.NodeId));
            RequireClaimId(admission.ClaimId);
            RequireFormalizationKind(admission.FormalizationKind);
            foreach (string digest in new[]
            {
                admission.AuthorizationRef,
                admission.VerificationBudgetRef,
                admission.SelectionRef,
                admission.FormalizationRequestRef,
                admission.BindingRef,
                admission.FrontierStateRef
            })
            {
                RequireDigest(digest, nameof(admission));
            }
            RequireGid(admission.Gid);
            if (!nodes.Add(admission.NodeId)
                || !claims.Add(admission.ClaimId))
            {
                throw new InvalidDataException(
                    "Ready-wave node and claim admissions must be unique.");
            }
        }
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static PaperFrontierNodeSelectionAdmitted[] ReplayReadyWave(
        string root,
        PaperFrontierReadySetAuthority authority,
        PaperFrontierReadyWaveSelectionCursor cursor)
    {
        Validate(cursor);
        PaperFrontierReadySet readySet = authority.ReadySet;
        if (!string.Equals(
                cursor.ReadySetRef,
                readySet.ReadySetId,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.FrontierRef,
                readySet.ReadySetContent.FrontierRef,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.TriggerNodeId,
                readySet.ReadySetContent.TriggerNodeId,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.TriggerManifestRef,
                readySet.ReadySetContent.TriggerManifestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.ReleaseStateRef,
                readySet.ReadySetContent.FrontierStateRef,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.FrontierPlanningTaskRef,
                authority.TriggerContext.Source.PlanningCursor.TaskRef,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.PaperId,
                authority.TriggerContext.Source.Program.ProgramContent.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.TheoryProgramRef,
                authority.TriggerContext.Source.Program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.TheoremPackageRef,
                authority.TriggerContext.Source.TheoremPackage.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.AdmittedAt,
                readySet.ReadySetContent.CreatedAt,
                StringComparison.Ordinal)
            || cursor.NodeAdmissions.Count
                != readySet.ReadySetContent.ReadyNodes.Count)
        {
            throw new InvalidDataException(
                "Ready-wave replay changed its release authority or paper lineage.");
        }

        var replayed = new List<PaperFrontierNodeSelectionAdmitted>();
        for (int index = 0;
             index < readySet.ReadySetContent.ReadyNodes.Count;
             index++)
        {
            PaperFrontierReadyNode node =
                readySet.ReadySetContent.ReadyNodes[index];
            PaperFrontierNodeSelectionAdmitted admitted = Admit(
                root,
                cursor.FrontierPlanningTaskRef,
                node.NodeId);
            ValidateReadyNodeAdmission(authority, node, admitted);
            PaperFrontierReadyWaveNodeAdmission stable =
                ToStableAdmission(admitted);
            if (!CanonicalJson.Serialize(stable).AsSpan().SequenceEqual(
                    CanonicalJson.Serialize(cursor.NodeAdmissions[index])))
            {
                throw new InvalidDataException(
                    "Ready-wave replay changed a node selection or Formalize request.");
            }
            replayed.Add(admitted);
        }
        return replayed.ToArray();
    }

    private static void ValidateReadyNodesAgainstCurrentState(
        PaperFormalizationFrontier frontier,
        PaperFrontierReadySet readySet,
        PaperFormalizationFrontierState current)
    {
        PaperFormalizationFrontierLifecycleService.Validate(current, frontier);
        var stateByNode = current.StateContent.NodeStates.ToDictionary(
            value => value.NodeId,
            StringComparer.Ordinal);
        foreach (PaperFrontierReadyNode ready in readySet.ReadySetContent.ReadyNodes)
        {
            PaperFormalizationFrontierNode node =
                PaperFormalizationFrontierService.RequireNode(
                    frontier,
                    ready.NodeId);
            PaperFormalizationFrontierNodeState state =
                stateByNode[ready.NodeId];
            bool alreadyAdmitted =
                string.Equals(state.Status, "selection-recorded", StringComparison.Ordinal)
                || string.Equals(state.Status, "request-recorded", StringComparison.Ordinal)
                || string.Equals(state.Status, "transport-recorded", StringComparison.Ordinal)
                || string.Equals(state.Status, "certification-pending", StringComparison.Ordinal)
                || string.Equals(state.Status, "certified", StringComparison.Ordinal)
                || string.Equals(state.Status, "manifested", StringComparison.Ordinal);
            if (!alreadyAdmitted
                && !string.Equals(
                    state.Status,
                    PaperFormalizationFrontierService.InitialNodeStatus,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A dependency-ready node has entered an incompatible scientific backroute state.");
            }
            if (!string.Equals(ready.ClaimId, node.ClaimId, StringComparison.Ordinal)
                || !string.Equals(
                    ready.FormalizationKind,
                    node.FormalizationKind,
                    StringComparison.Ordinal)
                || ready.ParallelWave != node.ParallelWave
                || ready.Priority != node.Priority
                || node.DependencyNodeIds.Count < 1
                || node.DependencyNodeIds.Any(dependency =>
                    !string.Equals(
                        stateByNode[dependency].Status,
                        "manifested",
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "A dependency-ready node no longer matches the admitted frontier or manifested dependencies.");
            }
        }
    }

    private static void ValidateReadyNodeAdmission(
        PaperFrontierReadySetAuthority authority,
        PaperFrontierReadyNode ready,
        PaperFrontierNodeSelectionAdmitted admitted)
    {
        if (!string.Equals(
                admitted.FrontierPlanningTaskRef,
                authority.TriggerContext.Source.PlanningCursor.TaskRef,
                StringComparison.Ordinal)
            || !string.Equals(
                admitted.FrontierRef,
                authority.ReadySet.ReadySetContent.FrontierRef,
                StringComparison.Ordinal)
            || !string.Equals(
                admitted.PaperId,
                authority.TriggerContext.Source.Program.ProgramContent.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                admitted.TheoryProgramRef,
                authority.TriggerContext.Source.Program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                admitted.TheoremPackageRef,
                authority.TriggerContext.Source.TheoremPackage.TheoremPackageId,
                StringComparison.Ordinal)
            || admitted.DispatchOrder != ready.DispatchOrder
            || !string.Equals(admitted.NodeId, ready.NodeId, StringComparison.Ordinal)
            || !string.Equals(admitted.ClaimId, ready.ClaimId, StringComparison.Ordinal)
            || !string.Equals(
                admitted.FormalizationKind,
                ready.FormalizationKind,
                StringComparison.Ordinal)
            || admitted.ParallelWave != ready.ParallelWave
            || admitted.Priority != ready.Priority
            || !string.Equals(
                admitted.AdmittedAt,
                authority.ReadySet.ReadySetContent.CreatedAt,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A ready-wave node admission changed its release route or paper identity.");
        }
    }

    private static PaperFrontierReadyWaveNodeAdmission ToStableAdmission(
        PaperFrontierNodeSelectionAdmitted admitted) =>
        new(
            admitted.DispatchOrder,
            admitted.NodeId,
            admitted.ClaimId,
            admitted.FormalizationKind,
            admitted.ParallelWave,
            admitted.Priority,
            admitted.Authorization.ArtifactRef,
            admitted.VerificationBudget.ArtifactRef,
            admitted.SelectionRef,
            admitted.FormalizationRequestRef,
            admitted.Binding.ArtifactRef,
            admitted.FrontierState.ArtifactRef,
            admitted.Gid);

    private static PaperFrontierReadyWaveSelectionAdmitted ToReadyWaveAdmitted(
        PaperFrontierReadyWaveSelectionCursor cursor,
        IReadOnlyList<PaperFrontierNodeSelectionAdmitted> nodes,
        bool replayed) =>
        new(
            PaperFrontierReadyWaveSelectionSchemas.Admitted,
            cursor.ReadySetRef,
            cursor.FrontierRef,
            cursor.TriggerNodeId,
            cursor.TriggerManifestRef,
            cursor.ReleaseStateRef,
            cursor.FrontierPlanningTaskRef,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.TheoremPackageRef,
            nodes,
            cursor.AdmittedAt,
            replayed);

    private static PaperFrontierReadyWaveSelectionCursor ReadReadyWaveCursor(
        string path)
    {
        PaperFrontierReadyWaveSelectionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperFrontierReadyWaveSelectionCursor>(
                    ReadBoundedFile(
                        path,
                        MaximumControlBytes,
                        "Frontier ready-wave selection cursor"));
        Validate(cursor);
        return cursor;
    }

    private static string ReadyWaveCursorPath(
        string root,
        string frontierRef,
        string readySetRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontier-ready-wave-selections",
            Hex(frontierRef),
            Hex(readySetRef) + ".json");
}
