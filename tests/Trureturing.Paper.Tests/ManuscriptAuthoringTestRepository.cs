using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

internal sealed class ManuscriptAuthoringTestRepository : IDisposable
{
    private readonly FrontierSelectionTestRepository _repository;

    public ManuscriptAuthoringTestRepository()
    {
        _repository = new FrontierSelectionTestRepository();
        var releases = new CompletionReleaseLedger(_repository);
        CompleteFrontier(_repository, releases);
        _ = releases.RegisterCommonDescendant("authoring-common-release");

        Completion = PaperFrontierNodeSelectionService.EvaluateFrontierCompletion(
            _repository.Root,
            _repository.Frontier.FrontierId);
        Assert.Equal(PaperFrontierCompletionStatuses.Completed, Completion.Status);
        string storePath = StorePath(_repository);
        Evaluation = PaperCertifiedClaimManifestService.Evaluate(
            storePath,
            Completion.ManuscriptPlanRef,
            Path.Combine(_repository.Root, "work", "authoring-manuscript-evaluations"),
            Path.Combine(
                _repository.Root,
                "work",
                "authoring-manuscript-resolutions",
                Hex(Completion.ManuscriptPlanRef) + ".json"));
        Assert.Equal(PaperClaimManifestOutcomes.Eligible, Evaluation.Outcome);
        ClaimManifestRef = Evaluation.ClaimManifestRef
            ?? throw new InvalidOperationException("Authoring fixture lacks claim manifest.");
        EligibilityRef = Evaluation.EligibilityRef
            ?? throw new InvalidOperationException("Authoring fixture lacks eligibility receipt.");

        Staged = PaperManuscriptAuthoringAgentService.StageTask(
            _repository.Root,
            Evaluation.EvaluationRef,
            ClaimManifestRef,
            EligibilityRef);
        Registration = PaperAgentRuntimeService.RegisterTask(
            _repository.Root,
            Staged.TaskPath);
        Prepared = PaperAgentRuntimeService.PrepareRun(
            _repository.Root,
            Staged.TaskRef);
    }

    public string Root => _repository.Root;
    public PaperFrontierCompletionEvaluated Completion { get; }
    public PaperManuscriptClaimEvaluationRegistration Evaluation { get; }
    public string ClaimManifestRef { get; }
    public string EligibilityRef { get; }
    public PaperManuscriptAuthoringAgentTaskStaged Staged { get; }
    public PaperAgentTaskRegistration Registration { get; }
    public PaperAgentRunPrepared Prepared { get; }

    public PaperScientificManuscriptDraft BuildValidDraft()
    {
        PaperResearchInputStore store = Store(_repository);
        PaperManuscriptPlan plan = store.Get<PaperManuscriptPlan>(
            Completion.ManuscriptPlanRef);
        PaperCertifiedClaimManifest manifest =
            store.Get<PaperCertifiedClaimManifest>(ClaimManifestRef);
        var mainBlocks = new List<PaperManuscriptDraftBlock>
        {
            Prose(1, "The certified theorem package is stated below in the exact order fixed by the manuscript plan. Each statement is inserted later by the repository renderer from the certified manifest, while this draft supplies only the surrounding exposition.")
        };
        int mainOrder = 2;
        foreach (PaperManuscriptFormalClaim claim in plan.FormalClaims)
        {
            mainBlocks.Add(new(
                mainOrder++,
                PaperManuscriptDraftBlockKinds.FormalClaim,
                claim.ClaimId,
                string.Empty));
        }

        var proofBlocks = new List<PaperManuscriptDraftBlock>
        {
            Prose(1, "The proof architecture follows the audited dependency graph. The individual proof narratives below explain how the certified declarations connect, without changing any hypothesis or conclusion in the formal claim ledger.")
        };
        int proofOrder = 2;
        foreach (PaperManuscriptFormalClaim claim in plan.FormalClaims)
        {
            proofBlocks.Add(new(
                proofOrder++,
                PaperManuscriptDraftBlockKinds.Proof,
                claim.ClaimId,
                $"Begin from the exact certified dependencies recorded for {claim.ClaimId}. Apply the audited reduction steps in their registered order, verify that each interface preserves the stated hypotheses, and conclude precisely the registered statement. The repository binds this proof narrative to the immutable claim label and certification evidence."));
        }

        var settingBlocks = new List<PaperManuscriptDraftBlock>
        {
            Prose(1, "We fix the objects and interfaces selected during A0 and A1, and retain the abstraction sharpened during A2. The resulting setting separates imported tools from the paper-specific obstruction mechanism and prepares the exact statements used later.")
        };
        int settingOrder = 2;
        var boundaryBlocks = new List<PaperManuscriptDraftBlock>
        {
            Prose(1, "The sharpness construction and the explicit epistemic ledger delimit the theorem package. These boundaries identify where the hypotheses are used and prevent broader conclusions than the certified declarations support.")
        };
        int boundaryOrder = 2;
        foreach (PaperManuscriptInformalItem item in plan.InformalExposition)
        {
            var block = new PaperManuscriptDraftBlock(
                item.ItemKind == "definition" ? settingOrder++ : boundaryOrder++,
                PaperManuscriptDraftBlockKinds.InformalItem,
                item.ItemId,
                string.Empty);
            if (item.ItemKind == "definition")
            {
                settingBlocks.Add(block);
            }
            else
            {
                boundaryBlocks.Add(block);
            }
        }

        return new PaperScientificManuscriptDraft(
            PaperManuscriptAuthoringAgentSchemas.Draft,
            Staged.DispatchRef,
            ClaimManifestRef,
            Completion.ManuscriptPlanRef,
            "paper-a",
            Staged.TheoryProgramRef,
            plan.Title,
            "We develop a certified obstruction framework for the descent problem selected by the Paper portfolio. The manuscript organizes an audited theorem package around an exact reduction lemma, a structural equivalence, an independent sharpness theorem, and a classification corollary. Every theorem-level statement is drawn from one coherent descendant truth release, while definitions and explanatory material retain explicit informal status. The resulting document separates mathematical certification from expository proof narrative and records the precise boundary between established claims, imported tools, and limitations.",
            ["certified mathematics", "descent", "formalization", "obstruction theory"],
            [
                new(1, "introduction", "Introduction", [
                    Prose(1, "The paper studies when a structured observable descends from compatible local data. Its contribution is a coherent theorem package whose exact claims were selected, audited, formalized, and certified before manuscript construction."),
                    Prose(2, "The presentation therefore treats theorem identity as fixed evidence and concentrates the authoring stage on motivation, logical organization, and a readable account of the proof architecture.")
                ]),
                new(2, "prior-work", "Prior work and contribution boundary", [
                    Prose(1, "Classical descent and cocycle arguments supply known tools. The audited novelty boundary lies in the canonical obstruction formulation, the exact equivalence, the independent realization theorem, and the resulting classification of minimal failures."),
                    Prose(2, "The current evidence bundle contains no structured citable records, so this draft deliberately introduces no bibliographic claim. A later literature stage may add only source-indexed references.")
                ]),
                new(3, "setting", "Setting and definitions", settingBlocks),
                new(4, "main-results", "Main results", mainBlocks),
                new(5, "proof-architecture", "Proof architecture", proofBlocks),
                new(6, "formalization", "Formalization and certified provenance", [
                    Prose(1, $"All formal claims in this draft resolve to the certified manifest {ClaimManifestRef}. Their declarations coexist in the selected truth release, and the repository renderer records the GID, statement identity, and certified-claim reference beside each theorem environment."),
                    Prose(2, "The authoring agent supplies no theorem text and cannot change the selected release, the axiom closure, the dependency graph, or the claim classification. Those coordinates are reconstructed independently during admission.")
                ]),
                new(7, "boundaries", "Boundaries, sharpness, and counterexamples", boundaryBlocks),
                new(8, "discussion", "Discussion", [
                    Prose(1, "The completed framework demonstrates how a portfolio-selected theory can move from abstract development to a release-coherent mathematical paper. The current manuscript remains journal-neutral and awaits independent scientific editing and venue-specific adaptation."),
                    Prose(2, "Future work should evaluate applications of the obstruction formalism, broaden the evidence-backed literature comparison, and preserve the same claim ledger whenever the exposition or journal format changes.")
                ])
            ],
            [],
            "2026-08-31T12:00:00Z");
    }

    public PaperAgentResultRecorded RecordCompletedDraft(
        PaperScientificManuscriptDraft draft)
    {
        string outputPath = Path.Combine(
            Prepared.Workspace,
            "outputs",
            "scientific-manuscript-draft.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, CanonicalJson.Serialize(draft));
        var result = new PaperAgentResultWire(
            PaperAgentSchemas.AgentResult,
            Staged.TaskRef,
            Registration.PaperId,
            Registration.TheoryProgramRef,
            Registration.Phase,
            Registration.AgentRole,
            Registration.ContextMode,
            "completed",
            "A journal-neutral scientific manuscript draft was completed from the certified claim ledger.",
            [new PaperAgentOutputWire(
                PaperManuscriptAuthoringAgentSchemas.Draft,
                "outputs/scientific-manuscript-draft.json")],
            "scientific-editing",
            string.Empty,
            Registration.ExactInputRefs,
            "2026-08-31T12:10:00Z");
        string stdout = "PAPER_AGENT_RESULT_BEGIN\n"
            + Encoding.UTF8.GetString(CanonicalJson.Serialize(result))
            + "\nPAPER_AGENT_RESULT_END\n";
        File.WriteAllText(Prepared.StdoutPath, stdout, Encoding.UTF8);
        return PaperAgentRuntimeService.RecordResult(
            Root,
            Staged.TaskRef,
            Prepared.StdoutPath,
            "codex-manuscript-authoring-test-run",
            "produced");
    }

    public byte[] ReadSource(PaperManuscriptSourceFile source) =>
        File.ReadAllBytes(Path.Combine(
            Root,
            source.RepositoryRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar)));

    public void Dispose() => _repository.Dispose();

    private static PaperManuscriptDraftBlock Prose(int order, string latex) =>
        new(order, PaperManuscriptDraftBlockKinds.Prose, string.Empty, latex);

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
                "authoring-definition-release");
        _ = releases.Certify(
            Prepare(repository, sharpness),
            "authoring-sharpness-release");
        PaperFrontierNodeSelectionAdmitted reduction = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                definitionCertification.ReadySetRef).NodeAdmissions);
        PaperFrontierCertificationRecorded reductionCertification =
            releases.Certify(
                Prepare(repository, reduction),
                "authoring-reduction-release");
        PaperFrontierNodeSelectionAdmitted main = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                reductionCertification.ReadySetRef).NodeAdmissions);
        PaperFrontierCertificationRecorded mainCertification =
            releases.Certify(
                Prepare(repository, main),
                "authoring-main-release");
        PaperFrontierNodeSelectionAdmitted classification = Assert.Single(
            PaperFrontierNodeSelectionService.AdmitReadyWave(
                repository.Root,
                repository.Frontier.FrontierId,
                mainCertification.ReadySetRef).NodeAdmissions);
        _ = releases.Certify(
            Prepare(repository, classification),
            "authoring-classification-release");
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
                    "authoring-formalization-dispatch",
                    Hex(request.RequestId) + ".json"));
        _ = PaperFrontierNodeSelectionService.RecordFormalizeTransport(
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
                    "authoring-formalization-results",
                    Hex(request.RequestId) + ".json"));
        PaperFormalizationOutcomeRegistration outcome =
            PaperFormalizationOutcomeService.Classify(
                storePath,
                result.ResultRef,
                Path.Combine(
                    repository.Root,
                    "work",
                    "authoring-formalization-decisions",
                    Hex(result.ResultRef) + ".json"));
        _ = PaperFrontierNodeSelectionService.RecordFormalizationOutcome(
            repository.Root,
            outcome.DecisionRef);
        return new(request, outcome);
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
        FormalizationRequest Request,
        PaperFormalizationOutcomeRegistration Outcome);

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
            PaperCertificationReleaseRegistration registered = Register(
                Release(
                    releaseDigest,
                    [wait.BaseTruthReleaseDigest],
                    [declaration],
                    releaseSeed),
                releaseSeed);
            PaperCertificationEvaluationRegistration evaluation =
                PaperCertificationService.Evaluate(
                    StorePath(repository),
                    waitRef,
                    registered.ReleaseRef,
                    Path.Combine(
                        repository.Root,
                        "work",
                        "authoring-certification-evaluations",
                        Hex(waitRef) + "-" + Hex(registered.ReleaseRef) + ".json"),
                    Path.Combine(
                        repository.Root,
                        "work",
                        "authoring-certification-resolutions",
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
                "D0.S0.Paper.Authored." +
                    wait.Gid.Split('.').Last().Replace('-', '_'),
                "theorem",
                wait.FormalizationRequestRef,
                PaperCertificationService.RequestedStatementDigest(
                    wait.ExpectedStatement),
                Digest(seed + "-statement"),
                "exact",
                ["Classical.choice", "Quot.sound", "propext"]);

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
