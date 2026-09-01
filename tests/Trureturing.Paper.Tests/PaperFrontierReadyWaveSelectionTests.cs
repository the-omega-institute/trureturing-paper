using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierReadyWaveSelectionTests
{
    [Fact]
    public void DependencyReadyWaveCreatesCanonicalSelectionAndRequest()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFrontierNodeSelectionAdmitted rootAdmission =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        PaperFrontierCertificationRecorded rootCertification = Certify(
            repository,
            Prepare(repository, rootAdmission),
            "ready-wave-root-release");

        PaperFrontierReadyWaveSelectionAdmitted wave =
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                rootCertification.ReadySetRef);

        Assert.False(wave.Replayed);
        Assert.Equal(rootCertification.ReadySetRef, wave.ReadySetRef);
        PaperFrontierNodeSelectionAdmitted lemma =
            Assert.Single(wave.NodeAdmissions);
        Assert.Equal("lem:reduction", lemma.ClaimId);
        Assert.Equal(1, lemma.ParallelWave);
        Assert.Equal(95, lemma.Priority);
        Assert.False(lemma.Replayed);
        Assert.True(
            repository.BindingLookupExists(
                lemma.FormalizationRequestRef));

        PaperResearchSelection selection =
            PaperResearchSelectionJson.ReadSelection(
                File.ReadAllBytes(lemma.SelectionPath));
        FormalizationRequest request =
            PaperResearchSelectionJson.ReadFormalizationRequest(
                File.ReadAllBytes(lemma.FormalizationRequestPath));
        PaperResearchSelectionService.Validate(selection);
        PaperResearchSelectionService.Validate(request);
        string dependencyGid =
            "D0/S0/Paper/Trureturing/Base/DescentObject.def_object";
        Assert.Equal(
            new[] { dependencyGid },
            selection.SelectionContent.ReuseApi.ToArray());
        Assert.Equal(
            new[] { dependencyGid },
            selection.SelectionContent.Target.KnownDependencies.ToArray());
        Assert.Equal(
            repository.Node("lem:reduction").FormalStatement,
            request.Target.Statement);
        Assert.Equal(lemma.SelectionRef, request.PaperContext.SelectionRef);
        Assert.Equal(
            lemma.FormalizationRequestRef,
            request.RequestId);

        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        Assert.Equal(8, state.StateContent.Version);
        Assert.Equal(
            "manifested",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("def:object").NodeId).Status);
        Assert.Equal(
            "request-recorded",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("lem:reduction").NodeId).Status);
    }

    [Fact]
    public void ReadyWaveReplayPreservesSelectionsAndState()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFrontierNodeSelectionAdmitted rootAdmission =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        PaperFrontierCertificationRecorded certification = Certify(
            repository,
            Prepare(repository, rootAdmission),
            "ready-wave-replay-release");

        PaperFrontierReadyWaveSelectionAdmitted first =
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                certification.ReadySetRef);
        string stateRef =
            repository.ReadCurrentStateCursor().State.ArtifactRef;
        PaperFrontierReadyWaveSelectionAdmitted replay =
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                certification.ReadySetRef);

        Assert.True(replay.Replayed);
        Assert.Equal(first.ReadySetRef, replay.ReadySetRef);
        Assert.Equal(
            first.NodeAdmissions.Select(value => value.SelectionRef),
            replay.NodeAdmissions.Select(value => value.SelectionRef));
        Assert.Equal(
            first.NodeAdmissions.Select(value =>
                value.FormalizationRequestRef),
            replay.NodeAdmissions.Select(value =>
                value.FormalizationRequestRef));
        Assert.All(replay.NodeAdmissions, value => Assert.True(value.Replayed));
        Assert.Equal(
            stateRef,
            repository.ReadCurrentStateCursor().State.ArtifactRef);
    }

    [Fact]
    public void LaterWaveRequestContinuesThroughExistingFormalizeTransport()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFrontierNodeSelectionAdmitted rootAdmission =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        PaperFrontierCertificationRecorded certification = Certify(
            repository,
            Prepare(repository, rootAdmission),
            "ready-wave-transport-release");
        PaperFrontierNodeSelectionAdmitted lemma = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                certification.ReadySetRef).NodeAdmissions);

        PreparedProgress prepared = Prepare(repository, lemma);

        Assert.Equal(
            PaperFrontierFormalizationProgressStatuses.Recorded,
            prepared.Transport.Status);
        Assert.Equal(lemma.FormalizationRequestRef, prepared.Request.RequestId);
        Assert.Equal("lem:reduction", prepared.Transport.ClaimId);
        Assert.Equal(
            repository.Node("lem:reduction").NodeId,
            prepared.Transport.NodeId);
        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        Assert.Equal(
            "certification-pending",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("lem:reduction").NodeId).Status);
    }

    [Fact]
    public void SuccessiveCertificationReleasesTheNextDependencyWave()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFrontierNodeSelectionAdmitted definition =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        PaperFrontierCertificationRecorded definitionCertification = Certify(
            repository,
            Prepare(repository, definition),
            "successive-definition-release");
        PaperFrontierNodeSelectionAdmitted lemma = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                definitionCertification.ReadySetRef).NodeAdmissions);

        PaperFrontierCertificationRecorded lemmaCertification = Certify(
            repository,
            Prepare(repository, lemma),
            "successive-lemma-release");
        PaperFrontierReadyNode mainReady = Assert.Single(
            lemmaCertification.ReadyNodes);
        Assert.Equal("thm:main", mainReady.ClaimId);
        Assert.Equal(2, mainReady.ParallelWave);

        PaperFrontierNodeSelectionAdmitted main = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                lemmaCertification.ReadySetRef).NodeAdmissions);
        Assert.Equal("thm:main", main.ClaimId);
        Assert.Equal(2, main.ParallelWave);

        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        Assert.Equal(14, state.StateContent.Version);
        Assert.Equal(
            "manifested",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("lem:reduction").NodeId).Status);
        Assert.Equal(
            "request-recorded",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("thm:main").NodeId).Status);
    }

    [Fact]
    public void ReadySetCannotBeReboundToAnotherFrontier()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFrontierNodeSelectionAdmitted definition =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        PaperFrontierCertificationRecorded certification = Certify(
            repository,
            Prepare(repository, definition),
            "wrong-frontier-release");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                Digest("another-frontier"),
                certification.ReadySetRef));

        Assert.Contains(
            "ready set",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PreparedProgress Prepare(
        FrontierSelectionTestRepository repository,
        PaperFrontierNodeSelectionAdmitted admitted)
    {
        PaperResearchSelection selection =
            PaperResearchSelectionJson.ReadSelection(
                File.ReadAllBytes(admitted.SelectionPath));
        FormalizationRequest request =
            PaperResearchSelectionJson.ReadFormalizationRequest(
                File.ReadAllBytes(admitted.FormalizationRequestPath));
        string storePath = Path.Combine(
            repository.Root,
            "artifacts",
            "research-input");
        string dispatchCursor = Path.Combine(
            repository.Root,
            "work",
            "research-input",
            "formalization-dispatch",
            Hex(request.RequestId) + ".json");
        PaperFormalizationDispatchRegistration dispatch =
            PaperFormalizationTransportService.PrepareDispatch(
                storePath,
                selection,
                PaperResearchSelectionJson.Write(selection),
                request,
                PaperResearchSelectionJson.Write(request),
                selection.SelectionId,
                request.RequestId,
                dispatchCursor);
        PaperFrontierFormalizeTransportRecorded transport =
            PaperFrontierNodeSelectionService.RecordFormalizeTransport(
                repository.Root,
                request.RequestId,
                dispatch.DispatchRef);

        var incoming = new FormalizeSolveResultWire(
            request.RequestId,
            request.RequestId,
            request.RequestId,
            selection.SelectionId,
            request.TruthRelease.SourceRepo,
            request.TruthRelease.SourceCommit,
            request.TruthRelease.SourceTree,
            request.TruthRelease.ReleaseDigest,
            request.PaperContext.PaperId,
            request.PaperContext.ResearchCandidateId,
            request.Target.PreferredGid!,
            "accepted",
            1,
            "candidate produced",
            string.Empty,
            PaperFormalizationTransportService.FormalizeResultDedupPrefix
                + request.RequestId);
        PaperFormalizationResultRegistration result =
            PaperFormalizationTransportService.RecordResult(
                storePath,
                dispatch.CursorPath,
                incoming,
                Path.Combine(
                    repository.Root,
                    "work",
                    "research-input",
                    "formalization-results",
                    Hex(request.RequestId) + ".json"));
        PaperFormalizationOutcomeRegistration outcome =
            PaperFormalizationOutcomeService.Classify(
                storePath,
                result.ResultRef,
                Path.Combine(
                    repository.Root,
                    "work",
                    "research-input",
                    "formalization-decisions",
                    Hex(result.ResultRef) + ".json"));
        PaperFrontierFormalizationOutcomeRecorded outcomeProgress =
            PaperFrontierNodeSelectionService.RecordFormalizationOutcome(
                repository.Root,
                outcome.DecisionRef);
        return new(
            selection,
            request,
            dispatch,
            transport,
            result,
            outcome,
            outcomeProgress);
    }

    private static PaperFrontierCertificationRecorded Certify(
        FrontierSelectionTestRepository repository,
        PreparedProgress prepared,
        string releaseSeed)
    {
        string waitRef = prepared.Outcome.CertificationWaitRef
            ?? throw new InvalidOperationException(
                "Candidate-produced outcome did not create a certification wait.");
        string storePath = Path.Combine(
            repository.Root,
            "artifacts",
            "research-input");
        var store = new PaperResearchInputStore(storePath);
        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(waitRef);
        string releaseDigest = Digest(releaseSeed);
        var declaration = new PaperCertificationDeclaration(
            wait.Gid,
            "D0.S0.Paper.Certified." +
                prepared.Request.Target.PreferredGid!
                    .Split('.').Last()
                    .Replace('-', '_'),
            "theorem",
            wait.FormalizationRequestRef,
            PaperCertificationService.RequestedStatementDigest(
                wait.ExpectedStatement),
            Digest(releaseSeed + "-statement"),
            "exact",
            [
                "Classical.choice",
                "Quot.sound",
                "propext"
            ]);
        var release = new PaperCertificationRelease(
            PaperCertificationSchemas.ReleaseObservation,
            releaseDigest,
            releaseDigest,
            Digest(releaseSeed + "-publication"),
            PaperResearchSelectionService.TruthSourceRepository,
            new string('c', 40),
            new string('d', 40),
            [wait.BaseTruthReleaseDigest],
            [declaration],
            new PaperCertificationProducer(
                PaperCertificationService.ProducerService,
                new string('e', 40)));
        string releaseCursorDirectory = Path.Combine(
            repository.Root,
            "work",
            "research-input",
            "certification-releases");
        Directory.CreateDirectory(releaseCursorDirectory);
        PaperCertificationReleaseRegistration registered =
            PaperCertificationService.RegisterRelease(
                storePath,
                CanonicalJson.Serialize(release),
                Path.Combine(
                    releaseCursorDirectory,
                    releaseSeed + ".json"),
                Path.Combine(
                    repository.Root,
                    "work",
                    "research-input",
                    "certification-waits"));
        PaperCertificationEvaluationRegistration evaluation =
            PaperCertificationService.Evaluate(
                storePath,
                waitRef,
                registered.ReleaseRef,
                Path.Combine(
                    repository.Root,
                    "work",
                    "research-input",
                    "certification-evaluations",
                    Hex(waitRef) + "-" + Hex(registered.ReleaseRef) + ".json"),
                Path.Combine(
                    repository.Root,
                    "work",
                    "research-input",
                    "certification-resolutions",
                    Hex(waitRef) + ".json"));
        return PaperFrontierNodeSelectionService.RecordCertification(
            repository.Root,
            evaluation.EvaluationRef,
            evaluation.CertifiedClaimRef
                ?? throw new InvalidOperationException(
                    "Exact release did not produce a certified claim."));
    }

    private static string Digest(string seed) =>
        PaperTheoryTestFactory.Digest(seed);

    private static string Hex(string reference) =>
        reference["sha256:".Length..];

    private sealed record PreparedProgress(
        PaperResearchSelection Selection,
        FormalizationRequest Request,
        PaperFormalizationDispatchRegistration Dispatch,
        PaperFrontierFormalizeTransportRecorded Transport,
        PaperFormalizationResultRegistration Result,
        PaperFormalizationOutcomeRegistration Outcome,
        PaperFrontierFormalizationOutcomeRecorded OutcomeProgress);
}
