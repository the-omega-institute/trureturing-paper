namespace Trureturing.Paper.Core;

internal sealed record PaperFrontierFormalizationProgressContext(
    PaperFrontierNodeSelectionSource Source,
    PaperFrontierNodeSelectionAdmissionCursor SelectionCursor,
    PaperFrontierFormalizationBinding Binding);

public static partial class PaperFrontierNodeSelectionService
{
    public static PaperFrontierFormalizeTransportRecorded RecordFormalizeTransport(
        string repositoryRoot,
        string formalizationRequestRef,
        string dispatchRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(formalizationRequestRef, nameof(formalizationRequestRef));
        RequireDigest(dispatchRef, nameof(dispatchRef));

        PaperFrontierFormalizationProgressContext? context =
            TryLoadProgressContext(root, formalizationRequestRef);
        if (context is null)
        {
            return new(
                PaperFrontierFormalizationProgressSchemas.TransportRecorded,
                PaperFrontierFormalizationProgressStatuses.NotFrontierBound,
                formalizationRequestRef,
                dispatchRef,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false);
        }

        var store = ResearchStore(root);
        PaperFormalizationDispatch dispatch =
            store.Get<PaperFormalizationDispatch>(dispatchRef);
        PaperFormalizationTransportService.Validate(dispatch);
        ValidateDispatchBinding(context, dispatchRef, dispatch);

        using FileStream frontierLock = AcquireFrontierLock(
            root,
            context.Source.Frontier.FrontierId);
        RecoverProgressState(root, context);

        string cursorPath = ProgressCursorPath(
            root,
            "transports",
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId);
        if (File.Exists(cursorPath))
        {
            PaperFrontierFormalizeTransportCursor existing =
                ReadTransportCursor(cursorPath);
            ValidateTransportReplay(root, context, existing, dispatchRef);
            RepairProgressPointer(root, context, existing.FrontierState);
            return ToTransportRecorded(existing, replayed: true);
        }

        PaperFormalizationFrontierState current =
            ReadOrInitializeCurrentState(root, context.Source);
        RequireNodeStatus(
            current,
            context.Source.Node.NodeId,
            "request-recorded",
            "Formalize transport");

        string recordedAt = ProgressTimestamp(context.Binding, 1);
        PaperFormalizationFrontierEvent transportEvent =
            PaperFormalizationFrontierLifecycleService.CreateEvent(
                context.Source.Frontier,
                current,
                context.Source.Node.NodeId,
                PaperFormalizationFrontierLifecycleService.FormalizeTransportFamily,
                PaperFormalizationSchemas.Dispatch,
                dispatchRef,
                string.Empty,
                string.Empty,
                $"Canonical Formalize dispatch {dispatchRef} transported request {formalizationRequestRef} for frontier node {context.Source.Node.NodeId}.",
                recordedAt);
        PaperFormalizationFrontierState next =
            PaperFormalizationFrontierLifecycleService.ApplyEvent(
                context.Source.Frontier,
                current,
                transportEvent,
                recordedAt);

        PaperFrontierNodeSelectionStoredArtifact storedEvent = StoreEnvelope(
            root,
            "progress-events",
            transportEvent.Schema,
            transportEvent.EventId,
            transportEvent);
        PaperFrontierNodeSelectionStoredArtifact storedState = StoreEnvelope(
            root,
            "progress-states",
            next.Schema,
            next.StateId,
            next);

        var cursor = new PaperFrontierFormalizeTransportCursor(
            PaperFrontierFormalizationProgressSchemas.TransportCursor,
            formalizationRequestRef,
            dispatchRef,
            dispatch.SelectionRef,
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId,
            context.Source.Node.ClaimId,
            storedEvent,
            storedState,
            recordedAt);
        Validate(cursor);
        PutImmutable(cursorPath, CanonicalJson.Serialize(cursor));
        WriteCurrentStateCursor(
            root,
            context.Source.Frontier,
            storedState,
            next);
        return ToTransportRecorded(cursor, replayed: false);
    }

    public static PaperFrontierFormalizationOutcomeRecorded RecordFormalizationOutcome(
        string repositoryRoot,
        string decisionRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(decisionRef, nameof(decisionRef));
        var store = ResearchStore(root);
        PaperFormalizationDecision decision =
            store.Get<PaperFormalizationDecision>(decisionRef);
        PaperFormalizationOutcomeService.Validate(decision);

        PaperFrontierFormalizationProgressContext? context =
            TryLoadProgressContext(root, decision.FormalizationRequestRef);
        if (context is null)
        {
            return new(
                PaperFrontierFormalizationProgressSchemas.OutcomeRecorded,
                PaperFrontierFormalizationProgressStatuses.NotFrontierBound,
                decision.FormalizationRequestRef,
                decision.ResultRef,
                decisionRef,
                string.Empty,
                string.Empty,
                string.Empty,
                decision.OutcomeClass,
                string.Empty,
                string.Empty,
                string.Empty,
                false);
        }

        string? disposition = MapOutcomeDisposition(decision.OutcomeClass);
        if (disposition is null)
        {
            return new(
                PaperFrontierFormalizationProgressSchemas.OutcomeRecorded,
                PaperFrontierFormalizationProgressStatuses.Ignored,
                decision.FormalizationRequestRef,
                decision.ResultRef,
                decisionRef,
                context.Source.Frontier.FrontierId,
                context.Source.Node.NodeId,
                context.Source.Node.ClaimId,
                decision.OutcomeClass,
                string.Empty,
                CurrentStateReference(root, context),
                string.Empty,
                false);
        }

        ValidateDecisionBinding(context, decisionRef, decision);
        PaperFrontierFormalizeTransportCursor transport =
            RequireTransportCursor(root, context);
        if (!string.Equals(
                transport.DispatchRef,
                decision.DispatchRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalization outcome changed the frontier transport dispatch.");
        }

        using FileStream frontierLock = AcquireFrontierLock(
            root,
            context.Source.Frontier.FrontierId);
        RecoverProgressState(root, context);

        string cursorPath = ProgressCursorPath(
            root,
            "outcomes",
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId);
        if (File.Exists(cursorPath))
        {
            PaperFrontierFormalizationOutcomeCursor existing =
                ReadOutcomeCursor(cursorPath);
            ValidateOutcomeReplay(
                root,
                context,
                existing,
                decisionRef,
                decision,
                disposition);
            RepairProgressPointer(root, context, existing.FrontierState);
            return ToOutcomeRecorded(existing, replayed: true);
        }

        PaperFormalizationFrontierState current =
            ReadOrInitializeCurrentState(root, context.Source);
        RequireNodeStatus(
            current,
            context.Source.Node.NodeId,
            "transport-recorded",
            "Formalization outcome");

        string recordedAt = ProgressTimestamp(context.Binding, 2);
        PaperFormalizationFrontierEvent outcomeEvent =
            PaperFormalizationFrontierLifecycleService.CreateEvent(
                context.Source.Frontier,
                current,
                context.Source.Node.NodeId,
                PaperFormalizationFrontierLifecycleService.FormalizationOutcomeFamily,
                PaperFormalizationOutcomeSchemas.Decision,
                decisionRef,
                disposition,
                string.Empty,
                $"Formalize result {decision.ResultRef} was classified as {decision.OutcomeClass} and admitted with frontier disposition {disposition}.",
                recordedAt);
        PaperFormalizationFrontierState next =
            PaperFormalizationFrontierLifecycleService.ApplyEvent(
                context.Source.Frontier,
                current,
                outcomeEvent,
                recordedAt);

        PaperFrontierNodeSelectionStoredArtifact storedEvent = StoreEnvelope(
            root,
            "progress-events",
            outcomeEvent.Schema,
            outcomeEvent.EventId,
            outcomeEvent);
        PaperFrontierNodeSelectionStoredArtifact storedState = StoreEnvelope(
            root,
            "progress-states",
            next.Schema,
            next.StateId,
            next);

        var cursor = new PaperFrontierFormalizationOutcomeCursor(
            PaperFrontierFormalizationProgressSchemas.OutcomeCursor,
            decision.FormalizationRequestRef,
            decision.DispatchRef,
            decision.ResultRef,
            decisionRef,
            decision.SelectionRef,
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId,
            context.Source.Node.ClaimId,
            decision.OutcomeClass,
            disposition,
            decision.Route,
            storedEvent,
            storedState,
            recordedAt);
        Validate(cursor);
        PutImmutable(cursorPath, CanonicalJson.Serialize(cursor));
        WriteCurrentStateCursor(
            root,
            context.Source.Frontier,
            storedState,
            next);
        return ToOutcomeRecorded(cursor, replayed: false);
    }

    public static PaperFrontierCertificationRecorded RecordCertification(
        string repositoryRoot,
        string evaluationRef,
        string certifiedClaimRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(evaluationRef, nameof(evaluationRef));
        RequireDigest(certifiedClaimRef, nameof(certifiedClaimRef));
        var store = ResearchStore(root);

        PaperCertificationEvaluation evaluation =
            store.Get<PaperCertificationEvaluation>(evaluationRef);
        PaperCertificationService.Validate(evaluation);
        PaperCertifiedClaim claim =
            store.Get<PaperCertifiedClaim>(certifiedClaimRef);
        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(claim.CertificationWaitRef);
        PaperFormalizationDecision decision =
            store.Get<PaperFormalizationDecision>(wait.DecisionRef);
        PaperFormalizationOutcomeService.Validate(wait, decision);
        PaperCertificationRelease release =
            store.Get<PaperCertificationRelease>(claim.CertifyingReleaseRef);
        PaperCertificationService.Validate(release);
        PaperCertificationDeclaration declaration =
            release.Declarations.SingleOrDefault(value =>
                string.Equals(value.Gid, claim.Gid, StringComparison.Ordinal)
                && string.Equals(
                    value.FormalizationRequestRef,
                    claim.FormalizationRequestRef,
                    StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Certified frontier claim is absent from its certifying release.");
        PaperCertificationService.Validate(claim, wait, release, declaration);

        if (!string.Equals(
                evaluation.CertifiedClaimRef,
                certifiedClaimRef,
                StringComparison.Ordinal)
            || !string.Equals(
                evaluation.CertificationWaitRef,
                claim.CertificationWaitRef,
                StringComparison.Ordinal)
            || !string.Equals(
                evaluation.ReleaseRef,
                claim.CertifyingReleaseRef,
                StringComparison.Ordinal)
            || !string.Equals(
                evaluation.Outcome,
                PaperCertificationOutcomes.Certified,
                StringComparison.Ordinal)
            || !string.Equals(
                evaluation.ClaimStatus,
                PaperCertificationService.Certified,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Certification evaluation and certified claim do not describe one exact resolution.");
        }

        PaperFrontierFormalizationProgressContext? context =
            TryLoadProgressContext(root, claim.FormalizationRequestRef);
        if (context is null)
        {
            return new(
                PaperFrontierFormalizationProgressSchemas.CertificationRecorded,
                PaperFrontierFormalizationProgressStatuses.NotFrontierBound,
                claim.FormalizationRequestRef,
                evaluationRef,
                certifiedClaimRef,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                string.Empty,
                false);
        }

        ValidateCertifiedClaimBinding(
            context,
            evaluationRef,
            evaluation,
            certifiedClaimRef,
            claim,
            wait,
            release);
        PaperFrontierFormalizationOutcomeCursor outcome =
            RequireOutcomeCursor(root, context);
        if (!string.Equals(
                outcome.OutcomeDisposition,
                "candidate-produced",
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.DecisionRef,
                claim.DecisionRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only the candidate-produced frontier outcome may be certified.");
        }

        using FileStream frontierLock = AcquireFrontierLock(
            root,
            context.Source.Frontier.FrontierId);
        RecoverProgressState(root, context);

        string cursorPath = ProgressCursorPath(
            root,
            "certifications",
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId);
        if (File.Exists(cursorPath))
        {
            PaperFrontierCertificationCursor existing =
                ReadCertificationCursor(cursorPath);
            ValidateCertificationReplay(
                root,
                context,
                existing,
                evaluationRef,
                certifiedClaimRef,
                claim);
            RepairProgressPointer(root, context, existing.FrontierState);
            return ToCertificationRecorded(root, existing, replayed: true);
        }

        PaperFormalizationFrontierState current =
            ReadOrInitializeCurrentState(root, context.Source);
        RequireNodeStatus(
            current,
            context.Source.Node.NodeId,
            "certification-pending",
            "Truth-release certification");

        string certifiedAt = ProgressTimestamp(context.Binding, 3);
        PaperFormalizationFrontierEvent certificationEvent =
            PaperFormalizationFrontierLifecycleService.CreateEvent(
                context.Source.Frontier,
                current,
                context.Source.Node.NodeId,
                PaperFormalizationFrontierLifecycleService.TruthReleaseCertificationFamily,
                PaperCertificationSchemas.CertifiedClaim,
                certifiedClaimRef,
                string.Empty,
                claim.CertifyingReleaseDigest,
                $"Exact certified claim {certifiedClaimRef} joined frontier node {context.Source.Node.NodeId} to descendant truth release {claim.CertifyingReleaseDigest}.",
                certifiedAt);
        PaperFormalizationFrontierState certifiedState =
            PaperFormalizationFrontierLifecycleService.ApplyEvent(
                context.Source.Frontier,
                current,
                certificationEvent,
                certifiedAt);

        string manifestedAt = ProgressTimestamp(context.Binding, 4);
        PaperFrontierCertifiedClaimManifest manifest =
            CreateCertifiedManifest(
                context,
                evaluationRef,
                certifiedClaimRef,
                claim,
                manifestedAt);
        PaperFormalizationFrontierEvent manifestEvent =
            PaperFormalizationFrontierLifecycleService.CreateEvent(
                context.Source.Frontier,
                certifiedState,
                context.Source.Node.NodeId,
                PaperFormalizationFrontierLifecycleService.CertifiedClaimManifestFamily,
                PaperFrontierFormalizationProgressSchemas.CertifiedManifest,
                manifest.ManifestId,
                string.Empty,
                string.Empty,
                $"Certified frontier manifest {manifest.ManifestId} made claim {context.Source.Node.ClaimId} dependency-visible.",
                manifestedAt);
        PaperFormalizationFrontierState manifestedState =
            PaperFormalizationFrontierLifecycleService.ApplyEvent(
                context.Source.Frontier,
                certifiedState,
                manifestEvent,
                manifestedAt);

        PaperFrontierReadySet readySet = CreateReadySet(
            root,
            context,
            manifest,
            manifestedState,
            manifestedAt);

        PaperFrontierNodeSelectionStoredArtifact storedCertificationEvent =
            StoreEnvelope(
                root,
                "progress-events",
                certificationEvent.Schema,
                certificationEvent.EventId,
                certificationEvent);
        PaperFrontierNodeSelectionStoredArtifact storedManifest =
            StoreEnvelope(
                root,
                "certified-manifests",
                manifest.Schema,
                manifest.ManifestId,
                manifest);
        PaperFrontierNodeSelectionStoredArtifact storedManifestEvent =
            StoreEnvelope(
                root,
                "progress-events",
                manifestEvent.Schema,
                manifestEvent.EventId,
                manifestEvent);
        PaperFrontierNodeSelectionStoredArtifact storedState =
            StoreEnvelope(
                root,
                "progress-states",
                manifestedState.Schema,
                manifestedState.StateId,
                manifestedState);
        PaperFrontierNodeSelectionStoredArtifact storedReadySet =
            StoreEnvelope(
                root,
                "ready-sets",
                readySet.Schema,
                readySet.ReadySetId,
                readySet);

        var cursor = new PaperFrontierCertificationCursor(
            PaperFrontierFormalizationProgressSchemas.CertificationCursor,
            claim.FormalizationRequestRef,
            evaluationRef,
            certifiedClaimRef,
            claim.CertifyingReleaseRef,
            claim.CertifyingReleaseDigest,
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId,
            context.Source.Node.ClaimId,
            storedCertificationEvent,
            storedManifest,
            storedManifestEvent,
            storedState,
            storedReadySet,
            manifestedAt);
        Validate(cursor);
        PutImmutable(cursorPath, CanonicalJson.Serialize(cursor));
        WriteCurrentStateCursor(
            root,
            context.Source.Frontier,
            storedState,
            manifestedState);
        return ToCertificationRecorded(root, cursor, replayed: false);
    }

    private static PaperFrontierFormalizationProgressContext?
        TryLoadProgressContext(
            string root,
            string formalizationRequestRef)
    {
        string lookupPath = BindingLookupPath(root, formalizationRequestRef);
        if (!File.Exists(lookupPath))
        {
            return null;
        }

        PaperFrontierFormalizationBindingLookup lookup =
            PaperResearchInputJson.DeserializeStrict<
                PaperFrontierFormalizationBindingLookup>(
                    ReadBoundedFile(
                        lookupPath,
                        MaximumControlBytes,
                        "Frontier formalization binding lookup"));
        Validate(lookup);
        if (!string.Equals(
                lookup.FormalizationRequestRef,
                formalizationRequestRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier binding lookup changed the formalization request identity.");
        }

        PaperFrontierFormalizationBinding binding =
            PaperResearchInputJson.DeserializeStrict<
                PaperFrontierFormalizationBinding>(
                    ReadRepositoryArtifact(
                        root,
                        lookup.BindingPath,
                        lookup.BindingBlobRef,
                        "Frontier formalization binding"));
        Validate(binding);
        if (!string.Equals(
                binding.BindingId,
                lookup.BindingRef,
                StringComparison.Ordinal)
            || !string.Equals(
                binding.BindingContent.FormalizationRequestRef,
                formalizationRequestRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier binding lookup and binding disagree.");
        }

        _ = Admit(
            root,
            binding.BindingContent.FrontierPlanningTaskRef,
            binding.BindingContent.NodeId);
        PaperFrontierNodeSelectionSource source = LoadSource(
            root,
            binding.BindingContent.FrontierPlanningTaskRef,
            binding.BindingContent.NodeId);
        PaperFrontierNodeSelectionAdmissionCursor selectionCursor =
            ReadAdmissionCursor(AdmissionCursorPath(
                root,
                source.Frontier.FrontierId,
                source.Node.NodeId));
        ValidateReplay(root, selectionCursor, source);

        if (!string.Equals(
                selectionCursor.Binding.ArtifactRef,
                binding.BindingId,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionCursor.Binding.BlobRef,
                lookup.BindingBlobRef,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionCursor.Binding.RepositoryRelativePath,
                lookup.BindingPath,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionCursor.FormalizationRequestRef,
                formalizationRequestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionCursor.SelectionRef,
                binding.BindingContent.SelectionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionCursor.FrontierRef,
                binding.BindingContent.FrontierRef,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionCursor.NodeId,
                binding.BindingContent.NodeId,
                StringComparison.Ordinal)
            || !string.Equals(
                selectionCursor.ClaimId,
                binding.BindingContent.ClaimId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier progress binding is not the admitted node selection binding.");
        }

        return new(source, selectionCursor, binding);
    }

    private static void ValidateDispatchBinding(
        PaperFrontierFormalizationProgressContext context,
        string dispatchRef,
        PaperFormalizationDispatch dispatch)
    {
        PaperFrontierFormalizationBindingContent binding =
            context.Binding.BindingContent;
        if (!string.Equals(
                dispatch.FormalizationRequestRef,
                binding.FormalizationRequestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SelectionRef,
                binding.SelectionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceRepo,
                PaperResearchSelectionService.TruthSourceRepository,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceCommit,
                binding.SourceCommit,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.SourceTree,
                binding.SourceTree,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.TruthReleaseDigest,
                binding.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.PaperId,
                context.Source.Program.ProgramContent.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.ResearchCandidateId,
                context.Source.Program.ProgramContent.CandidatePaperRef,
                StringComparison.Ordinal)
            || !string.Equals(
                dispatch.Gid,
                binding.Gid,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Formalize dispatch {dispatchRef} changed the frontier request, source, paper, candidate, or GID.");
        }
    }

    private static void ValidateDecisionBinding(
        PaperFrontierFormalizationProgressContext context,
        string decisionRef,
        PaperFormalizationDecision decision)
    {
        PaperFrontierFormalizationBindingContent binding =
            context.Binding.BindingContent;
        if (!string.Equals(
                decision.FormalizationRequestRef,
                binding.FormalizationRequestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.SelectionRef,
                binding.SelectionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.VerificationBudgetRef,
                binding.VerificationBudgetRef,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.TruthReleaseDigest,
                binding.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.SourceCommit,
                binding.SourceCommit,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.SourceTree,
                binding.SourceTree,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.PaperId,
                context.Source.Program.ProgramContent.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.ResearchCandidateId,
                context.Source.Program.ProgramContent.CandidatePaperRef,
                StringComparison.Ordinal)
            || !string.Equals(
                decision.Gid,
                binding.Gid,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Formalization decision {decisionRef} changed the frontier binding.");
        }
    }

    private static void ValidateCertifiedClaimBinding(
        PaperFrontierFormalizationProgressContext context,
        string evaluationRef,
        PaperCertificationEvaluation evaluation,
        string certifiedClaimRef,
        PaperCertifiedClaim claim,
        PaperCertificationWait wait,
        PaperCertificationRelease release)
    {
        PaperFrontierFormalizationBindingContent binding =
            context.Binding.BindingContent;
        if (!string.Equals(
                claim.FormalizationRequestRef,
                binding.FormalizationRequestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.SelectionRef,
                binding.SelectionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.VerificationBudgetRef,
                binding.VerificationBudgetRef,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.FormalizationResultRef,
                wait.ResultRef,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.DecisionRef,
                wait.DecisionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.PaperId,
                context.Source.Program.ProgramContent.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.ResearchCandidateId,
                context.Source.Program.ProgramContent.CandidatePaperRef,
                StringComparison.Ordinal)
            || !string.Equals(claim.Gid, binding.Gid, StringComparison.Ordinal)
            || !string.Equals(
                evaluation.CertifiedClaimRef,
                certifiedClaimRef,
                StringComparison.Ordinal)
            || !string.Equals(
                release.ReleaseDigest,
                claim.CertifyingReleaseDigest,
                StringComparison.Ordinal)
            || string.Equals(
                release.ReleaseDigest,
                binding.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !release.AncestorReleaseDigests.Contains(
                binding.TruthReleaseDigest,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Certification evaluation {evaluationRef} is not an exact descendant-release certification of the frontier request.");
        }
    }

    private static PaperFrontierCertifiedClaimManifest CreateCertifiedManifest(
        PaperFrontierFormalizationProgressContext context,
        string evaluationRef,
        string certifiedClaimRef,
        PaperCertifiedClaim claim,
        string manifestedAt)
    {
        var content = new PaperFrontierCertifiedClaimManifestContent(
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId,
            context.Source.Node.ClaimId,
            claim.FormalizationRequestRef,
            claim.SelectionRef,
            claim.FormalizationResultRef,
            claim.DecisionRef,
            evaluationRef,
            certifiedClaimRef,
            claim.CertifyingReleaseRef,
            claim.CertifyingReleaseDigest,
            claim.PaperId,
            claim.Gid,
            claim.LeanDeclaration,
            claim.DeclarationKind,
            claim.StatementId,
            claim.AxiomClosure.ToArray(),
            manifestedAt);
        var manifest = new PaperFrontierCertifiedClaimManifest(
            PaperFrontierFormalizationProgressSchemas.CertifiedManifest,
            ContentReference(content),
            content);
        Validate(manifest);
        return manifest;
    }

    private static PaperFrontierReadySet CreateReadySet(
        string root,
        PaperFrontierFormalizationProgressContext context,
        PaperFrontierCertifiedClaimManifest manifest,
        PaperFormalizationFrontierState state,
        string createdAt)
    {
        var previouslyReleased = new HashSet<string>(
            context.Source.PlanningCursor.InitialNodeRoutes.Select(
                value => value.NodeId),
            StringComparer.Ordinal);
        foreach (PaperFrontierCertificationCursor cursor
            in ReadCertificationCursors(
                root,
                context.Source.Frontier.FrontierId))
        {
            PaperFrontierReadySet prior = ReadStoredEnvelope<
                PaperFrontierReadySet>(
                    root,
                    cursor.ReadySet,
                    "Prior frontier ready set");
            Validate(prior);
            foreach (PaperFrontierReadyNode node
                in prior.ReadySetContent.ReadyNodes)
            {
                previouslyReleased.Add(node.NodeId);
            }
        }

        var stateByNode = state.StateContent.NodeStates.ToDictionary(
            value => value.NodeId,
            StringComparer.Ordinal);
        PaperFormalizationFrontierNode[] ready =
            context.Source.Frontier.FrontierContent.Nodes
                .Where(node =>
                    string.Equals(
                        stateByNode[node.NodeId].Status,
                        PaperFormalizationFrontierService.InitialNodeStatus,
                        StringComparison.Ordinal)
                    && !previouslyReleased.Contains(node.NodeId)
                    && node.DependencyNodeIds.Count > 0
                    && node.DependencyNodeIds.All(dependency =>
                        string.Equals(
                            stateByNode[dependency].Status,
                            "manifested",
                            StringComparison.Ordinal)))
                .OrderBy(node => node.ParallelWave)
                .ThenByDescending(node => node.Priority)
                .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                .ToArray();
        PaperFrontierReadyNode[] routes = ready
            .Select((node, index) => new PaperFrontierReadyNode(
                index + 1,
                node.NodeId,
                node.ClaimId,
                node.FormalizationKind,
                node.ParallelWave,
                node.Priority,
                "governed-selection"))
            .ToArray();
        var content = new PaperFrontierReadySetContent(
            context.Source.Frontier.FrontierId,
            context.Source.Node.NodeId,
            manifest.ManifestId,
            state.StateId,
            routes,
            createdAt);
        var readySet = new PaperFrontierReadySet(
            PaperFrontierFormalizationProgressSchemas.ReadySet,
            ContentReference(content),
            content);
        Validate(readySet);
        return readySet;
    }

    private static string? MapOutcomeDisposition(string outcomeClass) =>
        outcomeClass switch
        {
            "candidate-produced" => "candidate-produced",
            "counterexample" or "statement-inconsistent"
                or "generality-too-strong" => "counterexample",
            "missing-prerequisite" => "missing-prerequisite",
            "already-implied-by-library" => "already-known",
            "proof-search-exhausted" or "candidate-invalid"
                => "proof-search-exhausted",
            _ => null
        };

    private static PaperResearchInputStore ResearchStore(string root) =>
        new(Path.Combine(root, "artifacts", "research-input"));

    private static string ProgressTimestamp(
        PaperFrontierFormalizationBinding binding,
        int seconds) =>
        ParseUtc(binding.BindingContent.CreatedAt, "created_at")
            .AddSeconds(seconds)
            .ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                System.Globalization.CultureInfo.InvariantCulture);
}
