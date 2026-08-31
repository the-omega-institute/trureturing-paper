using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class FormalizationOutcomeTests
{
    [Fact]
    public void AcceptedVerifiedResultOpensCertificationWaitAndReplays()
    {
        using var directory = new TemporaryFolder();
        PreparedOutcome fixture = Prepare(
            directory,
            "accepted",
            "candidate produced");

        PaperFormalizationOutcomeRegistration first =
            PaperFormalizationOutcomeService.Classify(
                fixture.Store,
                fixture.ResultRef,
                fixture.DecisionCursor);

        Assert.False(first.Replayed);
        Assert.Equal(
            PaperFormalizationOutcomeRoutes.AwaitCertification,
            first.Route);
        Assert.Equal("candidate-produced", first.OutcomeClass);
        Assert.Equal("pending-certification", first.ClaimStatus);
        Assert.NotNull(first.CertificationWaitRef);

        var store = new PaperResearchInputStore(fixture.Store);
        PaperFormalizationDecision decision =
            store.Get<PaperFormalizationDecision>(first.DecisionRef);
        Assert.Equal("ACCEPTED", decision.VerdictToken);
        Assert.Equal(fixture.ResultRef, decision.ResultRef);
        Assert.Equal(
            fixture.Selection.SelectionContent.PaperResearchInputRef,
            decision.PaperResearchInputRef);

        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(
                first.CertificationWaitRef!);
        Assert.Equal(first.DecisionRef, wait.DecisionRef);
        Assert.Equal(
            fixture.Request.Target.Statement,
            wait.ExpectedStatement);
        Assert.Equal(
            fixture.Request.TruthRelease.ReleaseDigest,
            wait.BaseTruthReleaseDigest);
        Assert.Equal(
            fixture.Request.Target.AllowedAssumptions.ToArray(),
            wait.AllowedAssumptions.ToArray());
        Assert.Equal(
            "pending-certification",
            wait.ClaimStatus);

        PaperFormalizationOutcomeRegistration replay =
            PaperFormalizationOutcomeService.Classify(
                fixture.Store,
                fixture.ResultRef,
                fixture.DecisionCursor);
        Assert.True(replay.Replayed);
        Assert.Equal(first.DecisionRef, replay.DecisionRef);
        Assert.Equal(
            first.CertificationWaitRef,
            replay.CertificationWaitRef);
    }

    [Fact]
    public void ScientificFailuresRouteToTheirPaperResearchStages()
    {
        AssertRoute(
            "COUNTEREXAMPLE: witness contradicts the statement",
            PaperFormalizationOutcomeRoutes.IntuitionResearch,
            "counterexample");
        AssertRoute(
            "STATEMENT_INCONSISTENT: assumptions conflict",
            PaperFormalizationOutcomeRoutes.IntuitionResearch,
            "statement-inconsistent");
        AssertRoute(
            "GENERALITY_TOO_STRONG: boundary cannot be proved",
            PaperFormalizationOutcomeRoutes.IntuitionResearch,
            "generality-too-strong");
        AssertRoute(
            "MISSING_PREREQUISITE: a compactness bridge is absent",
            PaperFormalizationOutcomeRoutes.SublemmaResearch,
            "missing-prerequisite");
        AssertRoute(
            "ALREADY_IMPLIED_BY_LIBRARY: existing theorem closes it",
            PaperFormalizationOutcomeRoutes.NoveltyReassessment,
            "already-implied-by-library");
        AssertRoute(
            "PROOF_SEARCH_EXHAUSTED: bounded search ended",
            PaperFormalizationOutcomeRoutes.ProofStrategyRevision,
            "proof-search-exhausted");
        AssertRoute(
            "PATCH_DIGEST_MISMATCH: candidate bytes changed",
            PaperFormalizationOutcomeRoutes.ProofStrategyRevision,
            "candidate-invalid");
    }

    [Fact]
    public void FailurePolicyCanBlockAutomaticScientificExpansion()
    {
        using var directory = new TemporaryFolder();
        PreparedOutcome counterexample = Prepare(
            directory,
            "abstained",
            "COUNTEREXAMPLE: witness found",
            counterexampleIsUseful: false);

        PaperFormalizationOutcomeRegistration decision =
            PaperFormalizationOutcomeService.Classify(
                counterexample.Store,
                counterexample.ResultRef,
                counterexample.DecisionCursor);

        Assert.Equal(
            PaperFormalizationOutcomeRoutes.Blocked,
            decision.Route);
        Assert.Equal("counterexample", decision.OutcomeClass);
        Assert.Equal("ineligible", decision.ClaimStatus);
        Assert.Null(decision.CertificationWaitRef);
    }

    [Fact]
    public void InfrastructureUnknownAndPreContextOutcomesStayBlocked()
    {
        AssertRoute(
            "BASE_SKILL_SEAM_UNAVAILABLE (exit 2)",
            PaperFormalizationOutcomeRoutes.Blocked,
            "infrastructure-blocked");
        AssertRoute(
            "SOMETHING_NEW: no registered semantic",
            PaperFormalizationOutcomeRoutes.Blocked,
            "unclassified");

        using var directory = new TemporaryFolder();
        PreparedOutcome rejected = Prepare(
            directory,
            "abstained",
            "REQUEST_REF_MISMATCH: substituted request",
            preContext: true);

        PaperFormalizationOutcomeRegistration decision =
            PaperFormalizationOutcomeService.Classify(
                rejected.Store,
                rejected.ResultRef,
                rejected.DecisionCursor);

        Assert.Equal(
            PaperFormalizationOutcomeRoutes.Blocked,
            decision.Route);
        Assert.Equal("request-rejected", decision.OutcomeClass);
        Assert.Equal("ineligible", decision.ClaimStatus);
        Assert.Null(decision.CertificationWaitRef);
    }

    [Fact]
    public void FkstClassifierPublishesOnlyClosedPaperRoutes()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            "classify-formalization-outcome",
            "main.lua"));

        foreach (string queue in new[]
        {
            "paper_candidate_pending_certification",
            "paper_intuition_research_requested",
            "paper_sublemma_research_requested",
            "paper_novelty_reassessment_requested",
            "paper_formalization_strategy_revision_requested",
            "paper_formalization_blocked"
        })
        {
            Assert.Contains(queue, source, StringComparison.Ordinal);
        }
        Assert.Contains(
            "paper_formalization_result_recorded",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TRURETURING_PAPER_REPOSITORY_ROOT",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "exec_argv",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "trureturing-formalize.solve_request",
            source,
            StringComparison.Ordinal);
    }

    private static void AssertRoute(
        string verdict,
        string route,
        string outcomeClass)
    {
        using var directory = new TemporaryFolder();
        PreparedOutcome fixture = Prepare(
            directory,
            "abstained",
            verdict);

        PaperFormalizationOutcomeRegistration decision =
            PaperFormalizationOutcomeService.Classify(
                fixture.Store,
                fixture.ResultRef,
                fixture.DecisionCursor);

        Assert.Equal(route, decision.Route);
        Assert.Equal(outcomeClass, decision.OutcomeClass);
        Assert.Equal("ineligible", decision.ClaimStatus);
        Assert.Null(decision.CertificationWaitRef);
    }

    private static PreparedOutcome Prepare(
        TemporaryFolder directory,
        string status,
        string verdict,
        bool counterexampleIsUseful = true,
        bool missingPrerequisiteIsReportable = true,
        bool preContext = false,
        string errorClass = "")
    {
        PaperResearchInput researchInput = new(
            PaperResearchInputSchemas.ResearchInput,
            Digest('a'),
            Digest('b'),
            new string('1', 40),
            new string('2', 40),
            Digest('c'),
            Digest('d'),
            Digest('e'));
        var content = new PaperResearchSelectionContent(
            researchInput.TruthReleaseDigest,
            researchInput.TopologyDigest,
            CanonicalJson.Sha256Reference(
                CanonicalJson.Serialize(researchInput)),
            Digest('f'),
            Digest('8'),
            Digest('7'),
            "paper:trace-norm-compatibility",
            "Supplies the load-bearing bridge used by the main classification theorem.",
            new PaperResearchTarget(
                "forall x, x = x",
                "D5/S0/Test.reflexive",
                ["D5/S0/Carrier/TraceConjugation.trace_conj"],
                ["propext"],
                ["Do not replace the theorem by a single closed test case."]),
            "Prove the statement for every term in the declared carrier.",
            "A well-typed term for which the proposed equality fails.",
            "The bridge closes the only uncertified step in the paper's main argument.",
            ["Eq.refl", "D5/S0/Carrier/TraceConjugation.trace_conj"],
            new PaperResearchFailureSemantics(
                counterexampleIsUseful,
                missingPrerequisiteIsReportable),
            Digest('6'),
            "AlyciaBHZ",
            "2026-08-29T09:00:00Z",
            "2026-08-31T09:00:00Z");
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(content);
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(
                selection,
                researchInput);
        byte[] selectionBytes =
            PaperResearchSelectionJson.Write(selection);
        byte[] requestBytes =
            PaperResearchSelectionJson.Write(request);

        string storePath = Path.Combine(directory.Path, "store");
        PaperFormalizationDispatchRegistration dispatch =
            PaperFormalizationTransportService.PrepareDispatch(
                storePath,
                selection,
                selectionBytes,
                request,
                requestBytes,
                selection.SelectionId,
                request.RequestId,
                Path.Combine(directory.Path, "dispatch-cursor.json"));

        string Optional(string exact) =>
            preContext ? string.Empty : exact;
        var incoming = new FormalizeSolveResultWire(
            request.RequestId,
            request.RequestId,
            Optional(request.RequestId),
            selection.SelectionId,
            Optional(request.TruthRelease.SourceRepo),
            Optional(request.TruthRelease.SourceCommit),
            Optional(request.TruthRelease.SourceTree),
            Optional(request.TruthRelease.ReleaseDigest),
            Optional(request.PaperContext.PaperId),
            Optional(request.PaperContext.ResearchCandidateId),
            Optional(request.Target.PreferredGid!),
            status,
            1,
            verdict,
            errorClass,
            PaperFormalizationTransportService.FormalizeResultDedupPrefix
                + request.RequestId);
        PaperFormalizationResultRegistration result =
            PaperFormalizationTransportService.RecordResult(
                storePath,
                dispatch.CursorPath,
                incoming,
                Path.Combine(directory.Path, "result-cursor.json"));

        return new PreparedOutcome(
            storePath,
            selection,
            request,
            result.ResultRef,
            Path.Combine(directory.Path, "decision-cursor.json"));
    }

    private static string Digest(char value) =>
        "sha256:" + new string(value, 64);

    private static string FindRepositoryRoot()
    {
        foreach (DirectoryInfo start in new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory),
        })
        {
            for (DirectoryInfo? current = start;
                 current is not null;
                 current = current.Parent)
            {
                if (File.Exists(Path.Combine(
                        current.FullName,
                        "Trureturing.Paper.slnx")))
                {
                    return current.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the trureturing-paper repository root.");
    }

    private sealed record PreparedOutcome(
        string Store,
        PaperResearchSelection Selection,
        FormalizationRequest Request,
        string ResultRef,
        string DecisionCursor);

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "paper-formalization-outcome-tests-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
