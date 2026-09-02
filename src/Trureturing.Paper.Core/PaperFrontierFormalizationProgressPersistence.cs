namespace Trureturing.Paper.Core;

internal sealed record PaperFrontierProgressStateCandidate(
    PaperFrontierNodeSelectionStoredArtifact Stored,
    PaperFormalizationFrontierState State);

public static partial class PaperFrontierNodeSelectionService
{
    private static PaperFrontierFormalizeTransportCursor RequireTransportCursor(
        string root,
        PaperFrontierFormalizationProgressContext context)
    {
        string path = ProgressCursorPath(
            root,
            "transports",
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId);
        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                "Frontier outcome cannot be admitted before Formalize transport.");
        }
        PaperFrontierFormalizeTransportCursor cursor =
            ReadTransportCursor(path);
        ValidateTransportReplay(
            root,
            context,
            cursor,
            cursor.DispatchRef);
        return cursor;
    }

    private static PaperFrontierFormalizationOutcomeCursor RequireOutcomeCursor(
        string root,
        PaperFrontierFormalizationProgressContext context)
    {
        string path = ProgressCursorPath(
            root,
            "outcomes",
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId);
        if (!File.Exists(path))
        {
            throw new InvalidDataException(
                "Frontier certification cannot precede the formalization outcome.");
        }
        PaperFrontierFormalizationOutcomeCursor cursor =
            ReadOutcomeCursor(path);
        PaperFormalizationDecision decision =
            ResearchStore(root).Get<PaperFormalizationDecision>(
                cursor.DecisionRef);
        PaperFormalizationOutcomeService.Validate(decision);
        ValidateOutcomeReplay(
            root,
            context,
            cursor,
            cursor.DecisionRef,
            decision,
            cursor.OutcomeDisposition);
        return cursor;
    }

    private static void RecoverProgressState(
        string root,
        PaperFrontierFormalizationProgressContext context)
    {
        var candidates = new List<PaperFrontierProgressStateCandidate>();
        _ = ReadOrInitializeCurrentState(root, context.Source);
        PaperFrontierCurrentStateCursor currentCursor =
            ReadCurrentStateCursor(CurrentStateCursorPath(
                root,
                context.Source.Frontier.FrontierId));
        PaperFormalizationFrontierState current =
            ReadStoredState(root, currentCursor.State);
        PaperFormalizationFrontierLifecycleService.Validate(
            current,
            context.Source.Frontier);
        candidates.Add(new(currentCursor.State, current));

        string frontierRef = context.Source.Frontier.FrontierId;
        foreach (PaperFrontierFormalizeTransportCursor cursor
            in ReadTransportCursors(root, frontierRef))
        {
            Validate(cursor);
            RequireProgressLineage(
                context.Source.Frontier,
                cursor.FrontierRef,
                cursor.NodeId,
                cursor.ClaimId);
            PaperFormalizationFrontierEvent progressEvent =
                ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                    root,
                    cursor.TransportEvent,
                    "Recovered Formalize transport event");
            PaperFormalizationFrontierState state =
                ReadStoredState(root, cursor.FrontierState);
            ValidateProgressEventState(
                context.Source.Frontier,
                progressEvent,
                state,
                cursor.NodeId,
                PaperFormalizationFrontierLifecycleService.FormalizeTransportFamily,
                cursor.DispatchRef,
                "transport-recorded");
            candidates.Add(new(cursor.FrontierState, state));
        }

        foreach (PaperFrontierFormalizationOutcomeCursor cursor
            in ReadOutcomeCursors(root, frontierRef))
        {
            Validate(cursor);
            RequireProgressLineage(
                context.Source.Frontier,
                cursor.FrontierRef,
                cursor.NodeId,
                cursor.ClaimId);
            PaperFormalizationFrontierEvent progressEvent =
                ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                    root,
                    cursor.OutcomeEvent,
                    "Recovered formalization outcome event");
            PaperFormalizationFrontierState state =
                ReadStoredState(root, cursor.FrontierState);
            string expectedStatus = OutcomeStatus(cursor.OutcomeDisposition);
            ValidateProgressEventState(
                context.Source.Frontier,
                progressEvent,
                state,
                cursor.NodeId,
                PaperFormalizationFrontierLifecycleService.FormalizationOutcomeFamily,
                cursor.DecisionRef,
                expectedStatus);
            if (!string.Equals(
                    progressEvent.EventContent.OutcomeDisposition,
                    cursor.OutcomeDisposition,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Recovered frontier outcome changed its disposition.");
            }
            candidates.Add(new(cursor.FrontierState, state));
        }

        foreach (PaperFrontierCertificationCursor cursor
            in ReadCertificationCursors(root, frontierRef))
        {
            Validate(cursor);
            RequireProgressLineage(
                context.Source.Frontier,
                cursor.FrontierRef,
                cursor.NodeId,
                cursor.ClaimId);
            PaperFormalizationFrontierEvent certificationEvent =
                ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                    root,
                    cursor.CertificationEvent,
                    "Recovered truth-release certification event");
            PaperFormalizationFrontierEvent manifestEvent =
                ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                    root,
                    cursor.ManifestEvent,
                    "Recovered frontier manifest event");
            PaperFrontierCertifiedClaimManifest manifest =
                ReadStoredEnvelope<PaperFrontierCertifiedClaimManifest>(
                    root,
                    cursor.CertifiedManifest,
                    "Recovered certified frontier manifest");
            PaperFrontierReadySet readySet =
                ReadStoredEnvelope<PaperFrontierReadySet>(
                    root,
                    cursor.ReadySet,
                    "Recovered frontier ready set");
            PaperFormalizationFrontierState state =
                ReadStoredState(root, cursor.FrontierState);
            Validate(manifest);
            Validate(readySet);
            PaperFormalizationFrontierLifecycleService.Validate(
                certificationEvent,
                context.Source.Frontier);
            PaperFormalizationFrontierLifecycleService.Validate(
                manifestEvent,
                context.Source.Frontier);
            PaperFormalizationFrontierLifecycleService.Validate(
                state,
                context.Source.Frontier);
            PaperFormalizationFrontierNodeState nodeState =
                state.StateContent.NodeStates.Single(value =>
                    string.Equals(
                        value.NodeId,
                        cursor.NodeId,
                        StringComparison.Ordinal));
            if (!string.Equals(
                    certificationEvent.EventContent.ArtifactFamily,
                    PaperFormalizationFrontierLifecycleService
                        .TruthReleaseCertificationFamily,
                    StringComparison.Ordinal)
                || !string.Equals(
                    certificationEvent.EventContent.ArtifactRef,
                    cursor.CertifiedClaimRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    certificationEvent.EventContent
                        .CertifiedTruthReleaseDigest,
                    cursor.CertifyingReleaseDigest,
                    StringComparison.Ordinal)
                || !string.Equals(
                    manifestEvent.EventContent.ArtifactFamily,
                    PaperFormalizationFrontierLifecycleService
                        .CertifiedClaimManifestFamily,
                    StringComparison.Ordinal)
                || !string.Equals(
                    manifestEvent.EventContent.ArtifactRef,
                    manifest.ManifestId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    manifestEvent.EventContent.PredecessorEventRef,
                    certificationEvent.EventId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    manifest.ManifestContent.CertifyingReleaseDigest,
                    cursor.CertifyingReleaseDigest,
                    StringComparison.Ordinal)
                || !string.Equals(
                    readySet.ReadySetContent.TriggerManifestRef,
                    manifest.ManifestId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    readySet.ReadySetContent.FrontierStateRef,
                    state.StateId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    nodeState.Status,
                    "manifested",
                    StringComparison.Ordinal)
                || !string.Equals(
                    nodeState.LatestEventRef,
                    manifestEvent.EventId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    nodeState.CertifiedTruthReleaseDigest,
                    cursor.CertifyingReleaseDigest,
                    StringComparison.Ordinal)
                || !state.StateContent.AppliedEventRefs.Contains(
                    certificationEvent.EventId,
                    StringComparer.Ordinal)
                || !state.StateContent.AppliedEventRefs.Contains(
                    manifestEvent.EventId,
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Recovered frontier certification cursor is not a complete manifested state.");
            }
            candidates.Add(new(cursor.FrontierState, state));
        }

        int maximumVersion = candidates.Max(value =>
            value.State.StateContent.Version);
        PaperFrontierProgressStateCandidate[] latest = candidates
            .Where(value =>
                value.State.StateContent.Version == maximumVersion)
            .ToArray();
        string[] latestIds = latest.Select(value => value.State.StateId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (latestIds.Length != 1)
        {
            throw new InvalidDataException(
                "Frontier progress cursors contain divergent equal-version states.");
        }
        PaperFrontierProgressStateCandidate selected = latest[0];
        foreach (PaperFrontierProgressStateCandidate candidate in candidates)
        {
            RequireEventSubset(candidate.State, selected.State);
        }
        if (!string.Equals(
                current.StateId,
                selected.State.StateId,
                StringComparison.Ordinal))
        {
            WriteCurrentStateCursor(
                root,
                context.Source.Frontier,
                selected.Stored,
                selected.State);
        }
    }

    private static void RepairProgressPointer(
        string root,
        PaperFrontierFormalizationProgressContext context,
        PaperFrontierNodeSelectionStoredArtifact storedState)
    {
        PaperFormalizationFrontierState replay =
            ReadStoredState(root, storedState);
        PaperFormalizationFrontierLifecycleService.Validate(
            replay,
            context.Source.Frontier);
        PaperFormalizationFrontierState current =
            ReadOrInitializeCurrentState(root, context.Source);
        if (current.StateContent.Version < replay.StateContent.Version)
        {
            RequireEventSubset(current, replay);
            WriteCurrentStateCursor(
                root,
                context.Source.Frontier,
                storedState,
                replay);
        }
        else if (current.StateContent.Version == replay.StateContent.Version)
        {
            if (!string.Equals(
                    current.StateId,
                    replay.StateId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Equal-version frontier progress states have divergent identities.");
            }
        }
        else
        {
            RequireEventSubset(replay, current);
        }
    }

    private static void ValidateTransportReplay(
        string root,
        PaperFrontierFormalizationProgressContext context,
        PaperFrontierFormalizeTransportCursor cursor,
        string dispatchRef)
    {
        Validate(cursor);
        RequireProgressCoordinates(
            context,
            cursor.FormalizationRequestRef,
            cursor.SelectionRef,
            cursor.FrontierRef,
            cursor.NodeId,
            cursor.ClaimId);
        if (!string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalize transport replay changed the dispatch.");
        }
        PaperFormalizationFrontierEvent progressEvent =
            ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                root,
                cursor.TransportEvent,
                "Formalize transport event");
        PaperFormalizationFrontierState state =
            ReadStoredState(root, cursor.FrontierState);
        ValidateProgressEventState(
            context.Source.Frontier,
            progressEvent,
            state,
            cursor.NodeId,
            PaperFormalizationFrontierLifecycleService.FormalizeTransportFamily,
            dispatchRef,
            "transport-recorded");
    }

    private static void ValidateOutcomeReplay(
        string root,
        PaperFrontierFormalizationProgressContext context,
        PaperFrontierFormalizationOutcomeCursor cursor,
        string decisionRef,
        PaperFormalizationDecision decision,
        string disposition)
    {
        Validate(cursor);
        RequireProgressCoordinates(
            context,
            cursor.FormalizationRequestRef,
            cursor.SelectionRef,
            cursor.FrontierRef,
            cursor.NodeId,
            cursor.ClaimId);
        if (!string.Equals(cursor.DispatchRef, decision.DispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, decision.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DecisionRef, decisionRef, StringComparison.Ordinal)
            || !string.Equals(cursor.OutcomeClass, decision.OutcomeClass, StringComparison.Ordinal)
            || !string.Equals(cursor.OutcomeDisposition, disposition, StringComparison.Ordinal)
            || !string.Equals(cursor.Route, decision.Route, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalization outcome replay changed its classified decision.");
        }
        PaperFormalizationFrontierEvent progressEvent =
            ReadStoredEnvelope<PaperFormalizationFrontierEvent>(
                root,
                cursor.OutcomeEvent,
                "Formalization outcome event");
        PaperFormalizationFrontierState state =
            ReadStoredState(root, cursor.FrontierState);
        ValidateProgressEventState(
            context.Source.Frontier,
            progressEvent,
            state,
            cursor.NodeId,
            PaperFormalizationFrontierLifecycleService.FormalizationOutcomeFamily,
            decisionRef,
            OutcomeStatus(disposition));
        if (!string.Equals(
                progressEvent.EventContent.OutcomeDisposition,
                disposition,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalization outcome event changed its disposition.");
        }
    }

    private static void ValidateCertificationReplay(
        string root,
        PaperFrontierFormalizationProgressContext context,
        PaperFrontierCertificationCursor cursor,
        string evaluationRef,
        string certifiedClaimRef,
        PaperCertifiedClaim claim)
    {
        Validate(cursor);
        if (!string.Equals(
                cursor.FormalizationRequestRef,
                context.Binding.BindingContent.FormalizationRequestRef,
                StringComparison.Ordinal)
            || !string.Equals(cursor.EvaluationRef, evaluationRef, StringComparison.Ordinal)
            || !string.Equals(cursor.CertifiedClaimRef, certifiedClaimRef, StringComparison.Ordinal)
            || !string.Equals(cursor.CertifyingReleaseRef, claim.CertifyingReleaseRef, StringComparison.Ordinal)
            || !string.Equals(cursor.CertifyingReleaseDigest, claim.CertifyingReleaseDigest, StringComparison.Ordinal)
            || !string.Equals(cursor.FrontierRef, context.Source.Frontier.FrontierId, StringComparison.Ordinal)
            || !string.Equals(cursor.NodeId, context.Source.Node.NodeId, StringComparison.Ordinal)
            || !string.Equals(cursor.ClaimId, context.Source.Node.ClaimId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier certification replay changed its exact claim resolution.");
        }
        RecoverProgressState(root, context);
    }

    private static void ValidateProgressEventState(
        PaperFormalizationFrontier frontier,
        PaperFormalizationFrontierEvent progressEvent,
        PaperFormalizationFrontierState state,
        string nodeId,
        string expectedFamily,
        string expectedArtifactRef,
        string expectedStatus)
    {
        PaperFormalizationFrontierLifecycleService.Validate(
            progressEvent,
            frontier);
        PaperFormalizationFrontierLifecycleService.Validate(state, frontier);
        PaperFormalizationFrontierNodeState nodeState =
            state.StateContent.NodeStates.Single(value =>
                string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
        if (!string.Equals(
                progressEvent.EventContent.NodeId,
                nodeId,
                StringComparison.Ordinal)
            || !string.Equals(
                progressEvent.EventContent.ArtifactFamily,
                expectedFamily,
                StringComparison.Ordinal)
            || !string.Equals(
                progressEvent.EventContent.ArtifactRef,
                expectedArtifactRef,
                StringComparison.Ordinal)
            || !state.StateContent.AppliedEventRefs.Contains(
                progressEvent.EventId,
                StringComparer.Ordinal)
            || !string.Equals(
                nodeState.Status,
                expectedStatus,
                StringComparison.Ordinal)
            || !string.Equals(
                nodeState.LatestEventRef,
                progressEvent.EventId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier progress event and state do not form one admitted transition.");
        }
    }

    private static void RequireProgressCoordinates(
        PaperFrontierFormalizationProgressContext context,
        string requestRef,
        string selectionRef,
        string frontierRef,
        string nodeId,
        string claimId)
    {
        if (!string.Equals(
                requestRef,
                context.Binding.BindingContent.FormalizationRequestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionRef,
                context.Binding.BindingContent.SelectionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                frontierRef,
                context.Source.Frontier.FrontierId,
                StringComparison.Ordinal)
            || !string.Equals(
                nodeId,
                context.Source.Node.NodeId,
                StringComparison.Ordinal)
            || !string.Equals(
                claimId,
                context.Source.Node.ClaimId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier progress cursor changed request, selection, frontier, node, or claim.");
        }
    }

    private static void RequireProgressLineage(
        PaperFormalizationFrontier frontier,
        string frontierRef,
        string nodeId,
        string claimId)
    {
        if (!string.Equals(frontierRef, frontier.FrontierId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Recovered progress cursor changed its frontier.");
        }
        PaperFormalizationFrontierNode node =
            PaperFormalizationFrontierService.RequireNode(frontier, nodeId);
        if (!string.Equals(node.ClaimId, claimId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Recovered progress cursor changed its frontier claim.");
        }
    }

    private static void RequireNodeStatus(
        PaperFormalizationFrontierState state,
        string nodeId,
        string expected,
        string operation)
    {
        PaperFormalizationFrontierNodeState nodeState =
            state.StateContent.NodeStates.Single(value =>
                string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
        if (!string.Equals(nodeState.Status, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{operation} requires frontier node status {expected}, got {nodeState.Status}.");
        }
    }

    private static string CurrentStateReference(
        string root,
        PaperFrontierFormalizationProgressContext context)
    {
        using FileStream frontierLock = AcquireFrontierLock(
            root,
            context.Source.Frontier.FrontierId);
        RecoverProgressState(root, context);
        return ReadCurrentStateCursor(CurrentStateCursorPath(
            root,
            context.Source.Frontier.FrontierId)).State.ArtifactRef;
    }

    private static string OutcomeStatus(string disposition) =>
        disposition switch
        {
            "candidate-produced" => "certification-pending",
            "counterexample" => "theory-revision-required",
            "missing-prerequisite" => "frontier-revision-required",
            "already-known" => "novelty-reaudit-required",
            "proof-search-exhausted" => "proof-architecture-revision",
            _ => throw new InvalidDataException(
                $"Unsupported frontier outcome disposition {disposition}.")
        };

    private static PaperFrontierFormalizeTransportCursor ReadTransportCursor(
        string path)
    {
        PaperFrontierFormalizeTransportCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperFrontierFormalizeTransportCursor>(
                    ReadBoundedFile(
                        path,
                        MaximumControlBytes,
                        "Frontier Formalize transport cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperFrontierFormalizationOutcomeCursor ReadOutcomeCursor(
        string path)
    {
        PaperFrontierFormalizationOutcomeCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperFrontierFormalizationOutcomeCursor>(
                    ReadBoundedFile(
                        path,
                        MaximumControlBytes,
                        "Frontier formalization outcome cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperFrontierCertificationCursor ReadCertificationCursor(
        string path)
    {
        PaperFrontierCertificationCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperFrontierCertificationCursor>(
                    ReadBoundedFile(
                        path,
                        MaximumControlBytes,
                        "Frontier certification cursor"));
        Validate(cursor);
        return cursor;
    }

    private static IReadOnlyList<PaperFrontierFormalizeTransportCursor>
        ReadTransportCursors(string root, string frontierRef) =>
        ReadCursorDirectory(
            ProgressCursorDirectory(root, "transports", frontierRef),
            ReadTransportCursor);

    private static IReadOnlyList<PaperFrontierFormalizationOutcomeCursor>
        ReadOutcomeCursors(string root, string frontierRef) =>
        ReadCursorDirectory(
            ProgressCursorDirectory(root, "outcomes", frontierRef),
            ReadOutcomeCursor);

    private static IReadOnlyList<PaperFrontierCertificationCursor>
        ReadCertificationCursors(string root, string frontierRef) =>
        ReadCursorDirectory(
            ProgressCursorDirectory(root, "certifications", frontierRef),
            ReadCertificationCursor);

    private static IReadOnlyList<T> ReadCursorDirectory<T>(
        string directory,
        Func<string, T> reader)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }
        return Directory.EnumerateFiles(
                directory,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(reader)
            .ToArray();
    }

    private static string ProgressCursorPath(
        string root,
        string family,
        string frontierRef,
        string nodeId) =>
        Path.Combine(
            ProgressCursorDirectory(root, family, frontierRef),
            Hex(nodeId) + ".json");

    private static string ProgressCursorDirectory(
        string root,
        string family,
        string frontierRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontier-formalization-progress",
            family,
            Hex(frontierRef));

    private static PaperFrontierFormalizeTransportRecorded ToTransportRecorded(
        PaperFrontierFormalizeTransportCursor cursor,
        bool replayed) =>
        new(
            PaperFrontierFormalizationProgressSchemas.TransportRecorded,
            PaperFrontierFormalizationProgressStatuses.Recorded,
            cursor.FormalizationRequestRef,
            cursor.DispatchRef,
            cursor.FrontierRef,
            cursor.NodeId,
            cursor.ClaimId,
            cursor.FrontierState.ArtifactRef,
            cursor.TransportEvent.ArtifactRef,
            replayed);

    private static PaperFrontierFormalizationOutcomeRecorded ToOutcomeRecorded(
        PaperFrontierFormalizationOutcomeCursor cursor,
        bool replayed) =>
        new(
            PaperFrontierFormalizationProgressSchemas.OutcomeRecorded,
            PaperFrontierFormalizationProgressStatuses.Recorded,
            cursor.FormalizationRequestRef,
            cursor.ResultRef,
            cursor.DecisionRef,
            cursor.FrontierRef,
            cursor.NodeId,
            cursor.ClaimId,
            cursor.OutcomeClass,
            cursor.OutcomeDisposition,
            cursor.FrontierState.ArtifactRef,
            cursor.OutcomeEvent.ArtifactRef,
            replayed);

    private static PaperFrontierCertificationRecorded ToCertificationRecorded(
        string root,
        PaperFrontierCertificationCursor cursor,
        bool replayed)
    {
        PaperFrontierReadySet readySet =
            ReadStoredEnvelope<PaperFrontierReadySet>(
                root,
                cursor.ReadySet,
                "Frontier ready set");
        Validate(readySet);
        return new(
            PaperFrontierFormalizationProgressSchemas.CertificationRecorded,
            PaperFrontierFormalizationProgressStatuses.Recorded,
            cursor.FormalizationRequestRef,
            cursor.EvaluationRef,
            cursor.CertifiedClaimRef,
            cursor.FrontierRef,
            cursor.NodeId,
            cursor.ClaimId,
            cursor.CertifiedManifest.ArtifactRef,
            cursor.ReadySet.ArtifactRef,
            readySet.ReadySetContent.ReadyNodes,
            cursor.FrontierState.ArtifactRef,
            replayed);
    }
}
