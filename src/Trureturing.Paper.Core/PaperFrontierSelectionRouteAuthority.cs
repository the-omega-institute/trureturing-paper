namespace Trureturing.Paper.Core;

internal static class PaperFrontierSelectionAuthorityKinds
{
    public const string InitialWave = "initial-wave";
    public const string DependencyReadySet = "dependency-ready-set";
}

internal sealed record PaperFrontierSelectionRouteAuthority(
    string Kind,
    string AuthorityRef,
    string AuthorityStateRef,
    string AuthorizedAt,
    PaperFrontierPlanningNodeRoute Route,
    PaperFrontierReadySet? ReadySet,
    PaperFrontierCertificationCursor? CertificationCursor);

internal sealed record PaperFrontierReadySetAuthority(
    PaperFrontierCertificationCursor CertificationCursor,
    PaperFrontierReadySet ReadySet,
    PaperFrontierFormalizationProgressContext TriggerContext,
    PaperFormalizationFrontierState SourceState);

public static partial class PaperFrontierNodeSelectionService
{
    private static PaperFrontierSelectionRouteAuthority
        ResolveSelectionRouteAuthority(
            string root,
            PaperFrontierPlanningAgentAdmissionCursor planningCursor,
            string nodeId)
    {
        PaperFrontierPlanningNodeRoute? initial =
            planningCursor.InitialNodeRoutes.SingleOrDefault(value =>
                string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
        if (initial is not null)
        {
            return new(
                PaperFrontierSelectionAuthorityKinds.InitialWave,
                planningCursor.TaskRef,
                planningCursor.InitialState.ArtifactRef,
                planningCursor.AdmittedAt,
                initial,
                null,
                null);
        }

        var matches = new List<PaperFrontierSelectionRouteAuthority>();
        foreach (PaperFrontierCertificationCursor cursor
            in ReadCertificationCursors(
                root,
                planningCursor.Frontier.ArtifactRef))
        {
            Validate(cursor);
            PaperFrontierReadySet readySet =
                ReadStoredEnvelope<PaperFrontierReadySet>(
                    root,
                    cursor.ReadySet,
                    "Frontier dependency-ready set");
            Validate(readySet);
            if (!string.Equals(
                    cursor.FrontierRef,
                    planningCursor.Frontier.ArtifactRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    readySet.ReadySetContent.FrontierRef,
                    planningCursor.Frontier.ArtifactRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    cursor.ReadySet.ArtifactRef,
                    readySet.ReadySetId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    cursor.FrontierState.ArtifactRef,
                    readySet.ReadySetContent.FrontierStateRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    cursor.CertifiedManifest.ArtifactRef,
                    readySet.ReadySetContent.TriggerManifestRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A dependency-ready set changed its certification lineage.");
            }

            PaperFrontierReadyNode? readyNode =
                readySet.ReadySetContent.ReadyNodes.SingleOrDefault(value =>
                    string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
            if (readyNode is null)
            {
                continue;
            }
            matches.Add(new(
                PaperFrontierSelectionAuthorityKinds.DependencyReadySet,
                readySet.ReadySetId,
                readySet.ReadySetContent.FrontierStateRef,
                readySet.ReadySetContent.CreatedAt,
                new PaperFrontierPlanningNodeRoute(
                    readyNode.DispatchOrder,
                    readyNode.NodeId,
                    readyNode.ClaimId,
                    readyNode.FormalizationKind,
                    readyNode.ParallelWave,
                    readyNode.Priority,
                    readyNode.NextRoute),
                readySet,
                cursor));
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidDataException(
                "No admitted initial route or dependency-ready set released the requested frontier node."),
            _ => throw new InvalidDataException(
                "Multiple dependency-ready sets attempted to release the same frontier node.")
        };
    }

    private static void ValidateSelectionRouteAuthority(
        string root,
        PaperFrontierPlanningAgentAdmissionCursor planningCursor,
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierState initialState,
        PaperFormalizationFrontierNode node,
        PaperFrontierSelectionRouteAuthority authority)
    {
        PaperFrontierPlanningNodeRoute route = authority.Route;
        if (!string.Equals(route.NodeId, node.NodeId, StringComparison.Ordinal)
            || !string.Equals(route.ClaimId, node.ClaimId, StringComparison.Ordinal)
            || !string.Equals(
                route.FormalizationKind,
                node.FormalizationKind,
                StringComparison.Ordinal)
            || route.ParallelWave != node.ParallelWave
            || route.Priority != node.Priority
            || route.DispatchOrder < 1
            || !string.Equals(
                route.NextRoute,
                "governed-selection",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier selection authority changed the admitted node route.");
        }

        if (string.Equals(
                authority.Kind,
                PaperFrontierSelectionAuthorityKinds.InitialWave,
                StringComparison.Ordinal))
        {
            if (route.ParallelWave != 0
                || node.DependencyNodeIds.Count != 0
                || !string.Equals(
                    authority.AuthorityRef,
                    planningCursor.TaskRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    authority.AuthorityStateRef,
                    initialState.StateId,
                    StringComparison.Ordinal)
                || authority.ReadySet is not null
                || authority.CertificationCursor is not null)
            {
                throw new InvalidDataException(
                    "Initial-wave authority is inconsistent with the admitted frontier plan.");
            }
            return;
        }

        if (!string.Equals(
                authority.Kind,
                PaperFrontierSelectionAuthorityKinds.DependencyReadySet,
                StringComparison.Ordinal)
            || route.ParallelWave < 1
            || node.DependencyNodeIds.Count < 1
            || authority.ReadySet is null
            || authority.CertificationCursor is null)
        {
            throw new InvalidDataException(
                "Later-wave selection requires one dependency-ready-set authority.");
        }

        PaperFrontierReadySet readySet = authority.ReadySet;
        PaperFrontierCertificationCursor certification =
            authority.CertificationCursor;
        PaperFormalizationFrontierState releaseState =
            ReadStoredState(root, certification.FrontierState);
        PaperFormalizationFrontierLifecycleService.Validate(
            releaseState,
            frontier);
        PaperFormalizationFrontierNodeState nodeState =
            releaseState.StateContent.NodeStates.Single(value =>
                string.Equals(value.NodeId, node.NodeId, StringComparison.Ordinal));
        var stateByNode = releaseState.StateContent.NodeStates.ToDictionary(
            value => value.NodeId,
            StringComparer.Ordinal);

        if (!string.Equals(
                authority.AuthorityRef,
                readySet.ReadySetId,
                StringComparison.Ordinal)
            || !string.Equals(
                authority.AuthorityStateRef,
                releaseState.StateId,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetContent.FrontierStateRef,
                releaseState.StateId,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetContent.TriggerNodeId,
                certification.NodeId,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetContent.TriggerManifestRef,
                certification.CertifiedManifest.ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(
                nodeState.Status,
                PaperFormalizationFrontierService.InitialNodeStatus,
                StringComparison.Ordinal)
            || node.DependencyNodeIds.Any(dependency =>
                !string.Equals(
                    stateByNode[dependency].Status,
                    "manifested",
                    StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Dependency-ready-set authority does not prove that every node dependency is manifested.");
        }

        DateTimeOffset planningTime = ParseUtc(
            planningCursor.AdmittedAt,
            nameof(planningCursor.AdmittedAt));
        DateTimeOffset readyTime = ParseUtc(
            authority.AuthorizedAt,
            nameof(authority.AuthorizedAt));
        if (readyTime < planningTime)
        {
            throw new InvalidDataException(
                "Dependency-ready selection authority predates frontier planning.");
        }
    }

    private static PaperFrontierReadySetAuthority LoadReadySetAuthority(
        string root,
        string frontierRef,
        string readySetRef)
    {
        RequireDigest(frontierRef, nameof(frontierRef));
        RequireDigest(readySetRef, nameof(readySetRef));
        var matches = new List<PaperFrontierCertificationCursor>();
        foreach (PaperFrontierCertificationCursor cursor
            in ReadCertificationCursors(root, frontierRef))
        {
            Validate(cursor);
            if (string.Equals(
                    cursor.ReadySet.ArtifactRef,
                    readySetRef,
                    StringComparison.Ordinal))
            {
                matches.Add(cursor);
            }
        }
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                matches.Count == 0
                    ? "The requested dependency-ready set is not backed by a frontier certification cursor."
                    : "The requested dependency-ready set has multiple certification authorities.");
        }

        PaperFrontierCertificationCursor certification = matches[0];
        PaperFrontierReadySet readySet =
            ReadStoredEnvelope<PaperFrontierReadySet>(
                root,
                certification.ReadySet,
                "Frontier ready-wave authority");
        Validate(readySet);
        PaperFrontierFormalizationProgressContext context =
            TryLoadProgressContext(
                root,
                certification.FormalizationRequestRef)
            ?? throw new InvalidDataException(
                "Ready-set certification is not bound to a governed frontier request.");
        PaperFormalizationFrontierState sourceState =
            ReadStoredState(root, certification.FrontierState);
        PaperFormalizationFrontierLifecycleService.Validate(
            sourceState,
            context.Source.Frontier);

        if (!string.Equals(
                certification.FrontierRef,
                frontierRef,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetId,
                readySetRef,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetContent.FrontierRef,
                frontierRef,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetContent.FrontierStateRef,
                sourceState.StateId,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetContent.TriggerNodeId,
                certification.NodeId,
                StringComparison.Ordinal)
            || !string.Equals(
                readySet.ReadySetContent.TriggerManifestRef,
                certification.CertifiedManifest.ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(
                context.Source.Frontier.FrontierId,
                frontierRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Ready-set authority changed its frontier, state, trigger, or certification identity.");
        }
        return new(certification, readySet, context, sourceState);
    }
}
