using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierCompletionTests
{
    [Fact]
    public void IncompleteFrontierProducesTypedPendingEvidence()
    {
        using var repository = new FrontierSelectionTestRepository();
        var releases = new CompletionReleaseLedger(repository);
        PaperFrontierNodeSelectionAdmitted definition =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        _ = releases.Certify(
            Prepare(repository, definition),
            "completion-pending-definition");

        PaperFrontierCompletionEvaluated pending =
            PaperFrontierNodeSelectionService.EvaluateFrontierCompletion(
                repository.Root,
                repository.Frontier.FrontierId);

        Assert.Equal(PaperFrontierCompletionStatuses.Pending, pending.Status);
        Assert.Equal(
            PaperFrontierCompletionReasons.LoadBearingClaimsIncomplete,
            pending.Reason);
        Assert.Equal("paper-a", pending.PaperId);
        Assert.Equal(4, pending.MissingNodeIds.Count);
        Assert.Contains(
            repository.Node("thm:main").NodeId,
            pending.MissingNodeIds);
        Assert.Contains(
            repository.Node("cor:classification").NodeId,
            pending.MissingNodeIds);
        var store = Store(repository);
        PaperFrontierCompletionPending evidence =
            store.Get<PaperFrontierCompletionPending>(pending.PendingRef);
        PaperFrontierNodeSelectionService.Validate(evidence);
        Assert.Equal(pending.MissingNodeIds, evidence.MissingNodeIds);
        Assert.Contains(
            repository.Frontier.FrontierId,
            PaperFrontierNodeSelectionService.ListFrontierCompletionCandidates(
                repository.Root));
    }

    [Fact]
    public void CommonDescendantReleaseBuildsPlanAndPassesManifestGate()
    {
        using var repository = new FrontierSelectionTestRepository();
        var releases = new CompletionReleaseLedger(repository);
        CompleteFrontier(repository, releases);

        PaperFrontierCompletionEvaluated beforeMerge =
            PaperFrontierNodeSelectionService.EvaluateFrontierCompletion(
                repository.Root,
                repository.Frontier.FrontierId);
        Assert.Equal(PaperFrontierCompletionStatuses.Pending, beforeMerge.Status);
        Assert.Equal(
            PaperFrontierCompletionReasons.CoherentTruthReleaseAbsent,
            beforeMerge.Reason);
        Assert.Empty(beforeMerge.MissingNodeIds);

        PaperCertificationReleaseRegistration commonRelease =
            releases.RegisterCommonDescendant("completion-common-release");
        Assert.Contains(
            repository.Frontier.FrontierId,
            PaperFrontierNodeSelectionService.ListFrontierCompletionCandidates(
                repository.Root));

        PaperFrontierCompletionEvaluated completed =
            PaperFrontierNodeSelectionService.EvaluateFrontierCompletion(
                repository.Root,
                repository.Frontier.FrontierId);

        Assert.Equal(PaperFrontierCompletionStatuses.Completed, completed.Status);
        Assert.Equal(PaperFrontierCompletionReasons.Complete, completed.Reason);
        Assert.Equal("paper-a", completed.PaperId);
        Assert.Equal(commonRelease.ReleaseRef, completed.ManuscriptTruthReleaseRef);
        Assert.Equal(
            commonRelease.ReleaseDigest,
            completed.ManuscriptTruthReleaseDigest);
        Assert.Equal(4, completed.FormalClaimCount);
        Assert.Equal(1, completed.InformalItemCount);
        Assert.Empty(completed.MissingNodeIds);
        Assert.DoesNotContain(
            repository.Frontier.FrontierId,
            PaperFrontierNodeSelectionService.ListFrontierCompletionCandidates(
                repository.Root));

        PaperResearchInputStore store = Store(repository);
        PaperFrontierCompletionReceipt receipt =
            store.Get<PaperFrontierCompletionReceipt>(completed.CompletionRef);
        PaperFrontierNodeSelectionService.Validate(receipt);
        Assert.Equal(5, receipt.RequiredNodeIds.Count);
        Assert.Equal(5, receipt.Claims.Count);
        Assert.All(receipt.Claims, claim => Assert.True(claim.LoadBearing));
        Assert.Equal(
            repository.Frontier.FrontierContent.Nodes
                .Select(node => node.NodeId)
                .ToHashSet(StringComparer.Ordinal),
            receipt.RequiredNodeIds.ToHashSet(StringComparer.Ordinal));

        PaperManuscriptPlan plan =
            store.Get<PaperManuscriptPlan>(completed.ManuscriptPlanRef);
        PaperCertifiedClaimManifestService.Validate(plan);
        Assert.Equal("paper-a", plan.PaperId);
        Assert.Equal("Structural descent equivalence", plan.Title);
        Assert.Equal(commonRelease.ReleaseRef, plan.ManuscriptTruthReleaseRef);
        Assert.Equal(4, plan.FormalClaims.Count);
        Assert.Single(plan.InformalExposition);
        Assert.Equal(
            new[] { "corollary", "lemma", "theorem", "theorem" },
            plan.FormalClaims
                .Select(claim => claim.ClaimKind)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        foreach (PaperManuscriptFormalClaim claim in plan.FormalClaims)
        {
            Assert.Equal(
                repository.Node(claim.ClaimId).FormalStatement,
                claim.Statement);
        }
        PaperManuscriptInformalItem definition =
            Assert.Single(plan.InformalExposition);
        Assert.Equal("definition", definition.ItemKind);
        Assert.Equal("def:object", definition.ItemId);
        Assert.Equal(
            repository.Node("def:object").FormalStatement,
            definition.Text);

        PaperManuscriptClaimEvaluationRegistration evaluation =
            PaperCertifiedClaimManifestService.Evaluate(
                StorePath(repository),
                completed.ManuscriptPlanRef,
                Path.Combine(repository.Root, "work", "completion-manuscript-evaluations"),
                Path.Combine(
                    repository.Root,
                    "work",
                    "completion-manuscript-resolutions",
                    Hex(completed.ManuscriptPlanRef) + ".json"));
        Assert.Equal(PaperClaimManifestOutcomes.Eligible, evaluation.Outcome);
        Assert.NotNull(evaluation.ClaimManifestRef);
        Assert.NotNull(evaluation.EligibilityRef);
        PaperCertifiedClaimManifest manifest =
            store.Get<PaperCertifiedClaimManifest>(
                evaluation.ClaimManifestRef!);
        Assert.Equal(4, manifest.FormalClaimCount);
        Assert.Equal(1, manifest.InformalItemCount);
        Assert.Equal(commonRelease.ReleaseRef, manifest.ManuscriptTruthReleaseRef);
        Assert.All(
            manifest.FormalClaims,
            claim => Assert.Equal("certified", claim.ProofStatus));

        PaperFrontierCompletionEvaluated replay =
            PaperFrontierNodeSelectionService.EvaluateFrontierCompletion(
                repository.Root,
                repository.Frontier.FrontierId);
        Assert.True(replay.Replayed);
        Assert.Equal(completed.CompletionRef, replay.CompletionRef);
        Assert.Equal(completed.ManuscriptPlanRef, replay.ManuscriptPlanRef);
        Assert.Equal(
            completed.ManuscriptTruthReleaseRef,
            replay.ManuscriptTruthReleaseRef);
    }

    [Fact]
    public void ManuscriptPlanValidationAcceptsAuditedPropositions()
    {
        var plan = new PaperManuscriptPlan(
            PaperClaimManifestSchemas.ManuscriptPlan,
            "paper-proposition",
            "A proposition-bearing theorem package",
            Digest("proposition-release"),
            [
                new PaperManuscriptFormalClaim(
                    "prop:bridge",
                    "prop:bridge",
                    "proposition",
                    Digest("proposition-certified-claim"),
                    "The audited bridge proposition is valid under the exact registered hypotheses.",
                    "Load-bearing proposition in the audited proof architecture.")
            ],
            []);

        PaperCertifiedClaimManifestService.Validate(plan);
    }

    private static void CompleteFrontier(
        FrontierSelectionTestRepository repository,
        CompletionReleaseLedger releases)
    {
        PaperFrontierNodeSelectionAdmitted definition =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        PaperFrontierNodeSelectionAdmitted sharpness =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("thm:sharp").NodeId);

        PaperFrontierCertificationRecorded definitionCertification =
            releases.Certify(
                Prepare(repository, definition),
                "completion-definition-release");
        _ = releases.Certify(
            Prepare(repository, sharpness),
            "completion-sharpness-release");

        PaperFrontierNodeSelectionAdmitted reduction = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                definitionCertification.ReadySetRef).NodeAdmissions);
        PaperFrontierCertificationRecorded reductionCertification =
            releases.Certify(
                Prepare(repository, reduction),
                "completion-reduction-release");

        PaperFrontierNodeSelectionAdmitted main = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                reductionCertification.ReadySetRef).NodeAdmissions);
        PaperFrontierCertificationRecorded mainCertification =
            releases.Certify(
                Prepare(repository, main),
                "completion-main-release");

        PaperFrontierNodeSelectionAdmitted classification = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                mainCertification.ReadySetRef).NodeAdmissions);
        _ = releases.Certify(
            Prepare(repository, classification),
            "completion-classification-release");

        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        Assert.All(
            state.StateContent.NodeStates,
            node => Assert.Equal("manifested", node.Status));
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
        string storePath = StorePath(repository);
        PaperFormalizationDispatchRegistration dispatch =
            PaperFormalizationTransportService.PrepareDispatch(
                storePath,
                selection,
                PaperResearchSelectionJson.Write(selection),
                request,
                PaperResearchSelectionJson.Write(request),
                selection.SelectionId,
                request.RequestId,
                Path.Combine(
                    repository.Root,
                    "work",
                    "completion-formalization-dispatch",
                    Hex(request.RequestId) + ".json"));
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
                    "completion-formalization-results",
                    Hex(request.RequestId) + ".json"));
        PaperFormalizationOutcomeRegistration outcome =
            PaperFormalizationOutcomeService.Classify(
                storePath,
                result.ResultRef,
                Path.Combine(
                    repository.Root,
                    "work",
                    "completion-formalization-decisions",
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

    private static PaperResearchInputStore Store(
        FrontierSelectionTestRepository repository) =>
        new(StorePath(repository));

    private static string StorePath(
        FrontierSelectionTestRepository repository) =>
        Path.Combine(repository.Root, "artifacts", "research-input");

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

    private sealed class CompletionReleaseLedger(
        FrontierSelectionTestRepository repository)
    {
        private readonly Dictionary<string, PaperCertificationDeclaration> _declarations =
            new(StringComparer.Ordinal);
        private readonly List<string> _claimReleaseDigests = [];

        public PaperFrontierCertificationRecorded Certify(
            PreparedProgress prepared,
            string releaseSeed)
        {
            string waitRef = prepared.Outcome.CertificationWaitRef
                ?? throw new InvalidOperationException(
                    "Candidate-produced outcome did not create a certification wait.");
            PaperResearchInputStore store = Store(repository);
            PaperCertificationWait wait =
                store.Get<PaperCertificationWait>(waitRef);
            PaperCertificationDeclaration declaration = Declaration(
                wait,
                releaseSeed);
            _declarations[declaration.Gid] = declaration;
            string releaseDigest = Digest(releaseSeed);
            PaperCertificationRelease release = Release(
                releaseDigest,
                [wait.BaseTruthReleaseDigest],
                [declaration],
                releaseSeed);
            PaperCertificationReleaseRegistration registered = Register(
                release,
                releaseSeed);
            PaperCertificationEvaluationRegistration evaluation =
                PaperCertificationService.Evaluate(
                    StorePath(repository),
                    waitRef,
                    registered.ReleaseRef,
                    Path.Combine(
                        repository.Root,
                        "work",
                        "completion-certification-evaluations",
                        Hex(waitRef) + "-" + Hex(registered.ReleaseRef) + ".json"),
                    Path.Combine(
                        repository.Root,
                        "work",
                        "completion-certification-resolutions",
                        Hex(waitRef) + ".json"));
            _claimReleaseDigests.Add(releaseDigest);
            return PaperFrontierNodeSelectionService.RecordCertification(
                repository.Root,
                evaluation.EvaluationRef,
                evaluation.CertifiedClaimRef
                    ?? throw new InvalidOperationException(
                        "Exact isolated release did not certify its frontier claim."));
        }

        public PaperCertificationReleaseRegistration RegisterCommonDescendant(
            string releaseSeed)
        {
            string releaseDigest = Digest(releaseSeed);
            string[] ancestors = _claimReleaseDigests
                .Append(repository.TruthReleaseDigest)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            PaperCertificationDeclaration[] declarations = _declarations.Values
                .OrderBy(value => value.Gid, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(5, declarations.Length);
            return Register(
                Release(
                    releaseDigest,
                    ancestors,
                    declarations,
                    releaseSeed),
                releaseSeed);
        }

        private PaperCertificationReleaseRegistration Register(
            PaperCertificationRelease release,
            string releaseSeed)
        {
            string directory = Path.Combine(
                repository.Root,
                "work",
                "research-input",
                "certification-releases");
            Directory.CreateDirectory(directory);
            return PaperCertificationService.RegisterRelease(
                StorePath(repository),
                CanonicalJson.Serialize(release),
                Path.Combine(directory, releaseSeed + ".json"),
                Path.Combine(
                    repository.Root,
                    "work",
                    "research-input",
                    "certification-waits"));
        }

        private static PaperCertificationDeclaration Declaration(
            PaperCertificationWait wait,
            string seed) =>
            new(
                wait.Gid,
                "D0.S0.Paper.Completed." +
                    wait.Gid.Split('.').Last().Replace('-', '_'),
                "theorem",
                wait.FormalizationRequestRef,
                PaperCertificationService.RequestedStatementDigest(
                    wait.ExpectedStatement),
                Digest(seed + "-statement"),
                "exact",
                [
                    "Classical.choice",
                    "Quot.sound",
                    "propext"
                ]);

        private static PaperCertificationRelease Release(
            string releaseDigest,
            IReadOnlyList<string> ancestors,
            IReadOnlyList<PaperCertificationDeclaration> declarations,
            string seed) =>
            new(
                PaperCertificationSchemas.ReleaseObservation,
                releaseDigest,
                releaseDigest,
                Digest(seed + "-publication"),
                PaperResearchSelectionService.TruthSourceRepository,
                new string('c', 40),
                new string('d', 40),
                ancestors
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                declarations
                    .OrderBy(value => value.Gid, StringComparer.Ordinal)
                    .ToArray(),
                new PaperCertificationProducer(
                    PaperCertificationService.ProducerService,
                    new string('e', 40)));
    }
}
