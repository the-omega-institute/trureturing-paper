using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierFormalizationProgressTests
{
    [Fact]
    public void ExactCandidateCertificationManifestsNodeAndReleasesNextReadySet()
    {
        using var repository = new FrontierSelectionTestRepository();
        PreparedProgress prepared = Prepare(
            repository,
            "def:object",
            "accepted",
            "candidate produced");

        Assert.Equal(
            PaperFrontierFormalizationProgressStatuses.Recorded,
            prepared.Transport.Status);
        Assert.Equal(
            PaperFrontierFormalizationProgressStatuses.Recorded,
            prepared.OutcomeProgress.Status);
        Assert.Equal(
            "candidate-produced",
            prepared.OutcomeProgress.OutcomeDisposition);

        PaperFrontierCertificationRecorded certification =
            Certify(repository, prepared);

        Assert.Equal(
            PaperFrontierFormalizationProgressStatuses.Recorded,
            certification.Status);
        Assert.False(certification.Replayed);
        PaperFrontierReadyNode ready = Assert.Single(
            certification.ReadyNodes);
        Assert.Equal("lem:reduction", ready.ClaimId);
        Assert.Equal(1, ready.ParallelWave);
        Assert.Equal("governed-selection", ready.NextRoute);

        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        PaperFormalizationFrontierNodeState manifested =
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("def:object").NodeId);
        Assert.Equal("manifested", manifested.Status);
        Assert.Equal(
            Digest("frontier-certified-release"),
            manifested.CertifiedTruthReleaseDigest);
        Assert.Equal(6, state.StateContent.Version);

        PaperFrontierCertificationRecorded replay =
            PaperFrontierNodeSelectionService.RecordCertification(
                repository.Root,
                certification.EvaluationRef,
                certification.CertifiedClaimRef);
        Assert.True(replay.Replayed);
        Assert.Equal(
            certification.CertifiedManifestRef,
            replay.CertifiedManifestRef);
        Assert.Equal(
            certification.ReadySetRef,
            replay.ReadySetRef);
        Assert.Equal(
            certification.FrontierStateRef,
            replay.FrontierStateRef);
        Assert.Equal(
            certification.ReadyNodes,
            replay.ReadyNodes);
    }

    [Theory]
    [InlineData(
        "COUNTEREXAMPLE: witness contradicts the statement",
        "counterexample",
        "theory-revision-required")]
    [InlineData(
        "STATEMENT_INCONSISTENT: assumptions conflict",
        "counterexample",
        "theory-revision-required")]
    [InlineData(
        "GENERALITY_TOO_STRONG: boundary cannot be proved",
        "counterexample",
        "theory-revision-required")]
    [InlineData(
        "MISSING_PREREQUISITE: a compactness bridge is absent",
        "missing-prerequisite",
        "frontier-revision-required")]
    [InlineData(
        "ALREADY_IMPLIED_BY_LIBRARY: existing theorem closes it",
        "already-known",
        "novelty-reaudit-required")]
    [InlineData(
        "PROOF_SEARCH_EXHAUSTED: bounded search ended",
        "proof-search-exhausted",
        "proof-architecture-revision")]
    public void ScientificFormalizeOutcomesBecomeTypedFrontierBackroutes(
        string verdict,
        string expectedDisposition,
        string expectedStatus)
    {
        using var repository = new FrontierSelectionTestRepository();
        PreparedProgress prepared = Prepare(
            repository,
            "def:object",
            "abstained",
            verdict);

        Assert.Equal(
            expectedDisposition,
            prepared.OutcomeProgress.OutcomeDisposition);
        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        Assert.Equal(
            expectedStatus,
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("def:object").NodeId).Status);

        PaperFrontierFormalizationOutcomeRecorded replay =
            PaperFrontierNodeSelectionService.RecordFormalizationOutcome(
                repository.Root,
                prepared.Outcome.DecisionRef);
        Assert.True(replay.Replayed);
        Assert.Equal(
            prepared.OutcomeProgress.FrontierStateRef,
            replay.FrontierStateRef);
    }

    [Fact]
    public void InfrastructureOutcomeStaysOutsideScientificFrontierState()
    {
        using var repository = new FrontierSelectionTestRepository();
        PreparedProgress prepared = Prepare(
            repository,
            "def:object",
            "abstained",
            "BASE_SKILL_SEAM_UNAVAILABLE (exit 2)");

        Assert.Equal(
            PaperFrontierFormalizationProgressStatuses.Ignored,
            prepared.OutcomeProgress.Status);
        Assert.Equal(
            "transport-recorded",
            repository.ReadState(
                repository.ReadCurrentStateCursor().State)
                .StateContent.NodeStates.Single(value =>
                    value.NodeId == repository.Node("def:object").NodeId)
                .Status);
    }

    [Fact]
    public void CertificationRecoversTransportAndOutcomeAfterStatePointerLoss()
    {
        using var repository = new FrontierSelectionTestRepository();
        PreparedProgress prepared = Prepare(
            repository,
            "def:object",
            "accepted",
            "candidate produced");
        File.Delete(CurrentStateCursorPath(repository));

        PaperFrontierCertificationRecorded certification =
            Certify(repository, prepared);

        Assert.Equal(
            PaperFrontierFormalizationProgressStatuses.Recorded,
            certification.Status);
        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        Assert.Equal(6, state.StateContent.Version);
        Assert.Equal(
            "manifested",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == repository.Node("def:object").NodeId).Status);
    }

    [Fact]
    public void LegacyFormalizeRequestWithoutFrontierBindingIsUnaffected()
    {
        using var repository = new FrontierSelectionTestRepository();

        PaperFrontierFormalizeTransportRecorded result =
            PaperFrontierNodeSelectionService.RecordFormalizeTransport(
                repository.Root,
                Digest("legacy-request"),
                Digest("legacy-dispatch"));

        Assert.Equal(
            PaperFrontierFormalizationProgressStatuses.NotFrontierBound,
            result.Status);
        Assert.Equal(string.Empty, result.FrontierRef);
        Assert.False(result.Replayed);
    }

    private static PreparedProgress Prepare(
        FrontierSelectionTestRepository repository,
        string claimId,
        string status,
        string verdict)
    {
        PaperFrontierNodeSelectionAdmitted admitted =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node(claimId).NodeId);
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
            status,
            1,
            verdict,
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
            admitted,
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
        PreparedProgress prepared)
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
        string releaseDigest = Digest("frontier-certified-release");
        var declaration = new PaperCertificationDeclaration(
            wait.Gid,
            "D0.S0.Paper.Certified.frontier_claim",
            "theorem",
            wait.FormalizationRequestRef,
            PaperCertificationService.RequestedStatementDigest(
                wait.ExpectedStatement),
            Digest("frontier-certified-statement"),
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
            Digest("frontier-certification-publication"),
            PaperResearchSelectionService.TruthSourceRepository,
            new string('c', 40),
            new string('d', 40),
            [wait.BaseTruthReleaseDigest],
            [declaration],
            new PaperCertificationProducer(
                PaperCertificationService.ProducerService,
                new string('e', 40)));
        PaperCertificationReleaseRegistration registered =
            PaperCertificationService.RegisterRelease(
                storePath,
                CanonicalJson.Serialize(release),
                Path.Combine(
                    repository.Root,
                    "work",
                    "research-input",
                    "certification-releases",
                    "frontier-certified-release.json"),
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

    private static string CurrentStateCursorPath(
        FrontierSelectionTestRepository repository) =>
        Path.Combine(
            repository.Root,
            "work",
            "paper-frontiers",
            "current-state",
            Hex(repository.Frontier.FrontierId) + ".json");

    private static string Digest(string seed) =>
        PaperTheoryTestFactory.Digest(seed);

    private static string Hex(string reference) =>
        reference["sha256:".Length..];

    private sealed record PreparedProgress(
        PaperFrontierNodeSelectionAdmitted Admission,
        PaperResearchSelection Selection,
        FormalizationRequest Request,
        PaperFormalizationDispatchRegistration Dispatch,
        PaperFrontierFormalizeTransportRecorded Transport,
        PaperFormalizationResultRegistration Result,
        PaperFormalizationOutcomeRegistration Outcome,
        PaperFrontierFormalizationOutcomeRecorded OutcomeProgress);
}
