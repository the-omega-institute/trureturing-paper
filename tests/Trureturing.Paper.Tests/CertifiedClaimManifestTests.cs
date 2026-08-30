using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class CertifiedClaimManifestTests
{
    [Fact]
    public void ExactCertifiedClaimsProduceClosedManifestAndEligibility()
    {
        using var directory = new TemporaryFolder();
        PreparedManifest fixture = PrepareCertifiedClaim(directory);
        PaperManuscriptPlan plan = Plan(fixture);
        PaperManuscriptPlanRegistration registered =
            RegisterPlan(directory, fixture, plan, "eligible");

        PaperManuscriptClaimEvaluationRegistration first =
            Evaluate(directory, fixture, registered.ManuscriptPlanRef);

        Assert.False(first.Replayed);
        Assert.Equal(
            PaperClaimManifestOutcomes.Eligible,
            first.Outcome);
        Assert.Equal(
            PaperClaimManifestReasons.AllFormalClaimsCertified,
            first.Reason);
        Assert.NotNull(first.ClaimManifestRef);
        Assert.NotNull(first.EligibilityRef);
        Assert.Null(first.PendingRef);
        Assert.Null(first.IneligibilityRef);

        var store = new PaperResearchInputStore(fixture.Store);
        PaperCertifiedClaimManifest manifest =
            store.Get<PaperCertifiedClaimManifest>(
                first.ClaimManifestRef!);
        Assert.Equal(
            registered.ManuscriptPlanRef,
            manifest.ManuscriptPlanRef);
        Assert.Equal(fixture.PaperId, manifest.PaperId);
        Assert.Equal(
            fixture.ReleaseRef,
            manifest.ManuscriptTruthReleaseRef);
        Assert.Equal(
            fixture.Release.ReleaseDigest,
            manifest.ManuscriptTruthReleaseDigest);
        Assert.Equal(1, manifest.FormalClaimCount);
        Assert.Equal(2, manifest.InformalItemCount);
        Assert.Equal("closed", manifest.ManifestStatus);

        PaperCertifiedClaimManifestEntry formal =
            Assert.Single(manifest.FormalClaims);
        Assert.Equal("theorem", formal.ClaimKind);
        Assert.Equal(fixture.ClaimRef, formal.CertifiedClaimRef);
        Assert.Equal(fixture.Claim.Gid, formal.Gid);
        Assert.Equal(
            fixture.Claim.StatementId,
            formal.StatementId);
        Assert.Equal("certified", formal.ProofStatus);
        Assert.Equal("certified", formal.EpistemicStatus);

        Assert.Equal(
            "conjectured",
            manifest.InformalExposition[0].EpistemicStatus);
        Assert.Equal(
            "explicitly-informal",
            manifest.InformalExposition[1].EpistemicStatus);
        Assert.Equal(
            PaperCertifiedClaimManifestService
                .ExpositionTextDigest(
                    plan.InformalExposition[0].Text),
            manifest.InformalExposition[0].TextDigest);

        PaperManuscriptEligibility eligibility =
            store.Get<PaperManuscriptEligibility>(
                first.EligibilityRef!);
        Assert.True(eligibility.FormalClaimsCertified);
        Assert.True(eligibility.ExactReleaseCoherent);
        Assert.True(
            eligibility.EpistemicBoundariesExplicit);
        Assert.Equal("eligible", eligibility.Status);

        PaperManuscriptClaimEvaluationRegistration replay =
            Evaluate(directory, fixture, registered.ManuscriptPlanRef);
        Assert.True(replay.Replayed);
        Assert.Equal(first.EvaluationRef, replay.EvaluationRef);
        Assert.Equal(
            first.ClaimManifestRef,
            replay.ClaimManifestRef);
        Assert.Equal(
            first.EligibilityRef,
            replay.EligibilityRef);
    }

    [Fact]
    public void MissingClaimIsPendingThenBecomesEligibleWithoutCursorRebinding()
    {
        using var directory = new TemporaryFolder();
        PreparedManifest fixture = PrepareCertifiedClaim(directory);
        PaperManuscriptPlanRegistration registered =
            RegisterPlan(
                directory,
                fixture,
                Plan(fixture),
                "pending");

        string claimPath = ArtifactPath(
            fixture.Store,
            fixture.ClaimRef);
        byte[] claimBytes = File.ReadAllBytes(claimPath);
        File.Delete(claimPath);

        PaperManuscriptClaimEvaluationRegistration pending =
            Evaluate(directory, fixture, registered.ManuscriptPlanRef);

        Assert.Equal(
            PaperClaimManifestOutcomes.Pending,
            pending.Outcome);
        Assert.Equal(
            PaperClaimManifestReasons.MissingEvidence,
            pending.Reason);
        Assert.NotNull(pending.PendingRef);
        Assert.Null(pending.ClaimManifestRef);
        var store = new PaperResearchInputStore(fixture.Store);
        PaperManuscriptClaimsPending evidence =
            store.Get<PaperManuscriptClaimsPending>(
                pending.PendingRef!);
        Assert.Contains(
            fixture.ClaimRef,
            evidence.MissingEvidenceRefs);

        Directory.CreateDirectory(
            System.IO.Path.GetDirectoryName(claimPath)!);
        File.WriteAllBytes(claimPath, claimBytes);

        PaperManuscriptClaimEvaluationRegistration eligible =
            Evaluate(directory, fixture, registered.ManuscriptPlanRef);

        Assert.Equal(
            PaperClaimManifestOutcomes.Eligible,
            eligible.Outcome);
        Assert.NotEqual(
            pending.EvidenceStateRef,
            eligible.EvidenceStateRef);
        Assert.NotEqual(
            pending.CursorPath,
            eligible.CursorPath);
        Assert.NotNull(eligible.ClaimManifestRef);
        Assert.NotNull(eligible.EligibilityRef);
    }

    [Fact]
    public void PaperAndStatementSubstitutionAreTerminallyIneligible()
    {
        using var directory = new TemporaryFolder();
        PreparedManifest fixture = PrepareCertifiedClaim(directory);

        PaperManuscriptPlan wrongPaper =
            Plan(fixture) with
            {
                PaperId = "paper:another-project"
            };
        PaperManuscriptPlanRegistration paperRegistration =
            RegisterPlan(
                directory,
                fixture,
                wrongPaper,
                "wrong-paper");
        PaperManuscriptClaimEvaluationRegistration paper =
            Evaluate(
                directory,
                fixture,
                paperRegistration.ManuscriptPlanRef,
                "wrong-paper");
        Assert.Equal(
            PaperClaimManifestOutcomes.Ineligible,
            paper.Outcome);
        Assert.Equal(
            PaperClaimManifestReasons.PaperIdMismatch,
            paper.Reason);
        Assert.NotNull(paper.IneligibilityRef);

        PaperManuscriptPlan wrongStatement =
            Plan(fixture) with
            {
                FormalClaims =
                [
                    Plan(fixture).FormalClaims[0] with
                    {
                        Statement = "forall x, True"
                    }
                ]
            };
        PaperManuscriptPlanRegistration statementRegistration =
            RegisterPlan(
                directory,
                fixture,
                wrongStatement,
                "wrong-statement");
        PaperManuscriptClaimEvaluationRegistration statement =
            Evaluate(
                directory,
                fixture,
                statementRegistration.ManuscriptPlanRef,
                "wrong-statement");
        Assert.Equal(
            PaperClaimManifestOutcomes.Ineligible,
            statement.Outcome);
        Assert.Equal(
            PaperClaimManifestReasons.StatementMismatch,
            statement.Reason);
        Assert.NotNull(statement.IneligibilityRef);
    }

    [Fact]
    public void SelectedReleaseMustContainEveryClaimUnchanged()
    {
        using var directory = new TemporaryFolder();
        PreparedManifest fixture = PrepareCertifiedClaim(directory);
        var laterWithoutDeclaration =
            new PaperCertificationRelease(
                PaperCertificationSchemas.ReleaseObservation,
                Digest('9'),
                Digest('9'),
                Digest('2'),
                PaperResearchSelectionService
                    .TruthSourceRepository,
                new string('6', 40),
                new string('7', 40),
                [fixture.Release.ReleaseDigest],
                [],
                new PaperCertificationProducer(
                    PaperCertificationService.ProducerService,
                    new string('8', 40)));
        PaperCertificationService.Validate(
            laterWithoutDeclaration);
        var store = new PaperResearchInputStore(fixture.Store);
        string laterRef = store.Put(
            laterWithoutDeclaration);

        PaperManuscriptPlan plan =
            Plan(fixture) with
            {
                ManuscriptTruthReleaseRef = laterRef
            };
        PaperManuscriptPlanRegistration registered =
            RegisterPlan(
                directory,
                fixture,
                plan,
                "missing-in-selected-release");
        PaperManuscriptClaimEvaluationRegistration result =
            Evaluate(
                directory,
                fixture,
                registered.ManuscriptPlanRef,
                "missing-in-selected-release");

        Assert.Equal(
            PaperClaimManifestOutcomes.Ineligible,
            result.Outcome);
        Assert.Equal(
            PaperClaimManifestReasons
                .SelectedReleaseDeclarationAbsent,
            result.Reason);
        PaperManuscriptClaimsIneligible evidence =
            store.Get<PaperManuscriptClaimsIneligible>(
                result.IneligibilityRef!);
        Assert.Equal(fixture.Claim.Gid, evidence.Expected);
        Assert.Equal("absent", evidence.Observed);
    }

    [Fact]
    public void PlanKeepsFormalAndInformalEpistemicClassesDisjoint()
    {
        using var directory = new TemporaryFolder();
        PreparedManifest fixture = PrepareCertifiedClaim(directory);
        PaperManuscriptPlan valid = Plan(fixture);

        PaperManuscriptPlan badConjecture =
            valid with
            {
                InformalExposition =
                [
                    valid.InformalExposition[0] with
                    {
                        EpistemicStatus =
                            "explicitly-informal"
                    }
                ]
            };
        Assert.Throws<InvalidDataException>(
            () => PaperCertifiedClaimManifestService.Validate(
                badConjecture));

        PaperManuscriptPlan duplicateLabel =
            valid with
            {
                InformalExposition =
                [
                    valid.InformalExposition[0] with
                    {
                        LatexLabel =
                            valid.FormalClaims[0].LatexLabel
                    }
                ]
            };
        Assert.Throws<InvalidDataException>(
            () => PaperCertifiedClaimManifestService.Validate(
                duplicateLabel));

        PaperManuscriptPlan wrongFormalPrefix =
            valid with
            {
                FormalClaims =
                [
                    valid.FormalClaims[0] with
                    {
                        ClaimKind = "lemma",
                        LatexLabel = "thm:main"
                    }
                ]
            };
        Assert.Throws<InvalidDataException>(
            () => PaperCertifiedClaimManifestService.Validate(
                wrongFormalPrefix));
    }

    [Fact]
    public void FkstManifestStageConsumesCertifiedClaimsAndPublishesClosedRoutes()
    {
        string root = FindRepositoryRoot();
        string package = System.IO.Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper");
        string register = File.ReadAllText(
            System.IO.Path.Combine(
                package,
                "departments",
                "register-manuscript-plan",
                "main.lua"));
        string refresh = File.ReadAllText(
            System.IO.Path.Combine(
                package,
                "departments",
                "refresh-manuscript-plans",
                "main.lua"));
        string evaluate = File.ReadAllText(
            System.IO.Path.Combine(
                package,
                "departments",
                "evaluate-manuscript-plan",
                "main.lua"));
        string raiser = File.ReadAllText(
            System.IO.Path.Combine(
                package,
                "raisers",
                "manuscript_plans.lua"));
        string researchCore = File.ReadAllText(
            System.IO.Path.Combine(
                package,
                "research_core.lua"));

        Assert.Contains(
            "paper_manuscript_plan_seen",
            register,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_manuscript_claim_evaluation_requested",
            register,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_certified_claim_ready",
            refresh,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_manuscript_claim_evaluation_requested",
            refresh,
            StringComparison.Ordinal);

        foreach (string queue in new[]
        {
            "paper_manuscript_claims_pending",
            "paper_manuscript_claims_ineligible",
            "paper_certified_claim_manifest_ready"
        })
        {
            Assert.Contains(
                queue,
                evaluate,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "inbox/manuscript-plans/*.json",
            raiser,
            StringComparison.Ordinal);
        Assert.Contains(
            "Trureturing.Paper.ClaimManifest.Cli.dll",
            researchCore,
            StringComparison.Ordinal);

        foreach (string source in new[]
        {
            register,
            refresh,
            evaluate
        })
        {
            Assert.Contains(
                "TRURETURING_PAPER_REPOSITORY_ROOT",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "exec_argv",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "github.com",
                source,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "\"git\"",
                source,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static PaperManuscriptPlan Plan(
        PreparedManifest fixture) =>
        new(
            PaperClaimManifestSchemas.ManuscriptPlan,
            fixture.PaperId,
            "A certified manuscript plan",
            fixture.ReleaseRef,
            [
                new PaperManuscriptFormalClaim(
                    "main-theorem",
                    "thm:main",
                    "theorem",
                    fixture.ClaimRef,
                    fixture.Claim.ExpectedStatement,
                    "This is the load-bearing theorem.")
            ],
            [
                new PaperManuscriptInformalItem(
                    "future-conjecture",
                    "conj:future",
                    "conjecture",
                    "A stronger statement may hold under an additional compactness hypothesis.",
                    "conjectured"),
                new PaperManuscriptInformalItem(
                    "scope-limitation",
                    "lim:scope",
                    "limitation",
                    "The present argument does not cover the noncompact boundary case.",
                    "explicitly-informal")
            ]);

    private static PaperManuscriptPlanRegistration RegisterPlan(
        TemporaryFolder directory,
        PreparedManifest fixture,
        PaperManuscriptPlan plan,
        string name)
    {
        byte[] bytes = CanonicalJson.Serialize(plan);
        return PaperCertifiedClaimManifestService.RegisterPlan(
            fixture.Store,
            bytes,
            System.IO.Path.Combine(
                directory.Path,
                "work",
                "manuscript-plans",
                name + ".json"));
    }

    private static PaperManuscriptClaimEvaluationRegistration Evaluate(
        TemporaryFolder directory,
        PreparedManifest fixture,
        string planRef,
        string? suffix = null) =>
        PaperCertifiedClaimManifestService.Evaluate(
            fixture.Store,
            planRef,
            System.IO.Path.Combine(
                directory.Path,
                "work",
                "manuscript-evaluations"),
            System.IO.Path.Combine(
                directory.Path,
                "work",
                "manuscript-resolutions",
                (suffix ?? planRef[7..]) + ".json"));

    private static PreparedManifest PrepareCertifiedClaim(
        TemporaryFolder directory)
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
            "Supplies the load-bearing bridge used by the main theorem.",
            new PaperResearchTarget(
                "forall x, x = x",
                "D5/S0/Test.reflexive",
                ["D5/S0/Carrier/TraceConjugation.trace_conj"],
                ["propext"],
                ["Do not replace the theorem by one closed test case."]),
            "Prove the statement for every term in the carrier.",
            "A well-typed term for which the equality fails.",
            "The result closes the uncertified step in the paper.",
            ["Eq.refl"],
            new PaperResearchFailureSemantics(
                CounterexampleIsUseful: true,
                MissingPrerequisiteIsReportable: true),
            Digest('6'),
            "AlyciaBHZ",
            "2026-08-29T09:00:00Z",
            "2026-08-31T09:00:00Z");
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(content);
        FormalizationRequest request =
            PaperResearchSelectionService
                .BuildFormalizationRequest(
                    selection,
                    researchInput);
        byte[] selectionBytes =
            PaperResearchSelectionJson.Write(selection);
        byte[] requestBytes =
            PaperResearchSelectionJson.Write(request);

        string storePath = System.IO.Path.Combine(
            directory.Path,
            "store");
        PaperFormalizationDispatchRegistration dispatch =
            PaperFormalizationTransportService.PrepareDispatch(
                storePath,
                selection,
                selectionBytes,
                request,
                requestBytes,
                selection.SelectionId,
                request.RequestId,
                System.IO.Path.Combine(
                    directory.Path,
                    "dispatch-cursor.json"));

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
            PaperFormalizationTransportService
                .FormalizeResultDedupPrefix
                + request.RequestId);
        PaperFormalizationResultRegistration result =
            PaperFormalizationTransportService.RecordResult(
                storePath,
                dispatch.CursorPath,
                incoming,
                System.IO.Path.Combine(
                    directory.Path,
                    "result-cursor.json"));
        PaperFormalizationOutcomeRegistration outcome =
            PaperFormalizationOutcomeService.Classify(
                storePath,
                result.ResultRef,
                System.IO.Path.Combine(
                    directory.Path,
                    "decision-cursor.json"));
        string waitRef = outcome.CertificationWaitRef
            ?? throw new InvalidOperationException(
                "Accepted fixture did not create a certification wait.");

        var store = new PaperResearchInputStore(storePath);
        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(waitRef);
        PaperCertificationDeclaration declaration = new(
            wait.Gid,
            "D5.S0.Test.reflexive",
            "theorem",
            wait.FormalizationRequestRef,
            PaperCertificationService
                .RequestedStatementDigest(
                    wait.ExpectedStatement),
            Digest('1'),
            "exact",
            [
                "Classical.choice",
                "Quot.sound",
                "propext"
            ]);
        PaperCertificationRelease release = new(
            PaperCertificationSchemas.ReleaseObservation,
            Digest('5'),
            Digest('5'),
            Digest('2'),
            PaperResearchSelectionService
                .TruthSourceRepository,
            new string('3', 40),
            new string('4', 40),
            [wait.BaseTruthReleaseDigest],
            [declaration],
            new PaperCertificationProducer(
                PaperCertificationService.ProducerService,
                new string('5', 40)));
        PaperCertificationReleaseRegistration observed =
            PaperCertificationService.RegisterRelease(
                storePath,
                CanonicalJson.Serialize(release),
                System.IO.Path.Combine(
                    directory.Path,
                    "release-cursor.json"),
                System.IO.Path.Combine(
                    directory.Path,
                    "wait-cursors"));
        PaperCertificationEvaluationRegistration certified =
            PaperCertificationService.Evaluate(
                storePath,
                waitRef,
                observed.ReleaseRef,
                System.IO.Path.Combine(
                    directory.Path,
                    "certification-evaluation.json"),
                System.IO.Path.Combine(
                    directory.Path,
                    "certification-resolution.json"));
        string claimRef = certified.CertifiedClaimRef
            ?? throw new InvalidOperationException(
                "Exact release did not certify the fixture.");
        PaperCertifiedClaim claim =
            store.Get<PaperCertifiedClaim>(claimRef);

        return new PreparedManifest(
            storePath,
            claimRef,
            claim,
            observed.ReleaseRef,
            release,
            content.PaperId);
    }

    private static string ArtifactPath(
        string storeRoot,
        string reference)
    {
        string hex = reference["sha256:".Length..];
        return System.IO.Path.Combine(
            storeRoot,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string Digest(char value) =>
        "sha256:" + new string(value, 64);

    private static string FindRepositoryRoot()
    {
        foreach (DirectoryInfo start in new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory)
        })
        {
            for (DirectoryInfo? current = start;
                 current is not null;
                 current = current.Parent)
            {
                if (File.Exists(System.IO.Path.Combine(
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

    private sealed record PreparedManifest(
        string Store,
        string ClaimRef,
        PaperCertifiedClaim Claim,
        string ReleaseRef,
        PaperCertificationRelease Release,
        string PaperId);

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "paper-certified-claim-manifest-tests-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
