using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class CertificationJoinTests
{
    [Fact]
    public void ExactLaterReleaseCertifiesAndReplays()
    {
        using var directory = new TemporaryFolder();
        PreparedCertification fixture = PrepareWait(directory);
        RegisterWait(fixture, directory);
        PaperCertificationRelease release = ExactRelease(
            fixture,
            Digest('9'),
            [fixture.Wait.BaseTruthReleaseDigest]);

        PaperCertificationReleaseRegistration observed =
            RegisterRelease(fixture, directory, release, "release-9");
        Assert.Contains(
            fixture.WaitRef,
            observed.CertificationWaitRefs);

        PaperCertificationEvaluationRegistration first =
            Evaluate(fixture, directory, observed.ReleaseRef, "release-9");

        Assert.False(first.Replayed);
        Assert.Equal(
            PaperCertificationOutcomes.Certified,
            first.Outcome);
        Assert.Equal(
            PaperCertificationReasons.ExactCertification,
            first.Reason);
        Assert.Equal("certified", first.ClaimStatus);
        Assert.NotNull(first.CertifiedClaimRef);
        Assert.Null(first.MismatchRef);

        var store = new PaperResearchInputStore(fixture.Store);
        PaperCertifiedClaim claim =
            store.Get<PaperCertifiedClaim>(
                first.CertifiedClaimRef!);
        Assert.Equal(fixture.WaitRef, claim.CertificationWaitRef);
        Assert.Equal(
            fixture.Wait.FormalizationRequestRef,
            claim.FormalizationRequestRef);
        Assert.Equal(release.ReleaseDigest, claim.CertifyingReleaseDigest);
        Assert.Equal(fixture.Wait.Gid, claim.Gid);
        Assert.Equal("exact", claim.StatementCorrespondence);
        Assert.Equal("certified", claim.ClaimStatus);

        PaperCertificationEvaluationRegistration replay =
            Evaluate(fixture, directory, observed.ReleaseRef, "release-9");
        Assert.True(replay.Replayed);
        Assert.Equal(first.EvaluationRef, replay.EvaluationRef);
        Assert.Equal(first.CertifiedClaimRef, replay.CertifiedClaimRef);
    }

    [Fact]
    public void SameReleaseAndAbsentDeclarationRemainPending()
    {
        using var directory = new TemporaryFolder();
        PreparedCertification fixture = PrepareWait(directory);
        RegisterWait(fixture, directory);

        PaperCertificationRelease same = ExactRelease(
            fixture,
            fixture.Wait.BaseTruthReleaseDigest,
            []);
        PaperCertificationReleaseRegistration sameObserved =
            RegisterRelease(fixture, directory, same, "same");
        PaperCertificationEvaluationRegistration sameResult =
            Evaluate(fixture, directory, sameObserved.ReleaseRef, "same");

        Assert.Equal(
            PaperCertificationOutcomes.StillPending,
            sameResult.Outcome);
        Assert.Equal(
            PaperCertificationReasons.SameRelease,
            sameResult.Reason);
        Assert.Null(sameResult.CertifiedClaimRef);
        Assert.Null(sameResult.MismatchRef);

        PaperCertificationRelease absent = ExactRelease(
            fixture,
            Digest('8'),
            [fixture.Wait.BaseTruthReleaseDigest]) with
        {
            Declarations = []
        };
        PaperCertificationReleaseRegistration absentObserved =
            RegisterRelease(fixture, directory, absent, "absent");
        PaperCertificationEvaluationRegistration absentResult =
            Evaluate(fixture, directory, absentObserved.ReleaseRef, "absent");

        Assert.Equal(
            PaperCertificationOutcomes.StillPending,
            absentResult.Outcome);
        Assert.Equal(
            PaperCertificationReasons.DeclarationAbsent,
            absentResult.Reason);
        Assert.Equal(
            "pending-certification",
            absentResult.ClaimStatus);
    }

    [Fact]
    public void LineageAndRequestMismatchProduceReleaseScopedEvidence()
    {
        using var directory = new TemporaryFolder();
        PreparedCertification fixture = PrepareWait(directory);
        RegisterWait(fixture, directory);

        PaperCertificationRelease unrelated = ExactRelease(
            fixture,
            Digest('7'),
            [Digest('1')]);
        PaperCertificationReleaseRegistration unrelatedObserved =
            RegisterRelease(fixture, directory, unrelated, "unrelated");
        PaperCertificationEvaluationRegistration lineage =
            Evaluate(
                fixture,
                directory,
                unrelatedObserved.ReleaseRef,
                "unrelated");

        Assert.Equal(
            PaperCertificationOutcomes.Mismatch,
            lineage.Outcome);
        Assert.Equal(
            PaperCertificationReasons.ReleaseLineageMismatch,
            lineage.Reason);
        Assert.NotNull(lineage.MismatchRef);
        Assert.Equal(
            "pending-certification",
            lineage.ClaimStatus);

        PaperCertificationRelease wrongRequest = ExactRelease(
            fixture,
            Digest('6'),
            [fixture.Wait.BaseTruthReleaseDigest]) with
        {
            Declarations =
            [
                ExactDeclaration(fixture) with
                {
                    FormalizationRequestRef = Digest('5')
                }
            ]
        };
        PaperCertificationReleaseRegistration wrongRequestObserved =
            RegisterRelease(
                fixture,
                directory,
                wrongRequest,
                "wrong-request");
        PaperCertificationEvaluationRegistration request =
            Evaluate(
                fixture,
                directory,
                wrongRequestObserved.ReleaseRef,
                "wrong-request");

        Assert.Equal(
            PaperCertificationOutcomes.Mismatch,
            request.Outcome);
        Assert.Equal(
            PaperCertificationReasons.RequestMismatch,
            request.Reason);
        Assert.NotNull(request.MismatchRef);

        var store = new PaperResearchInputStore(fixture.Store);
        PaperCertificationMismatch mismatch =
            store.Get<PaperCertificationMismatch>(
                request.MismatchRef!);
        Assert.Equal(
            fixture.Wait.FormalizationRequestRef,
            mismatch.Expected);
        Assert.Equal(Digest('5'), mismatch.Observed);
        Assert.Equal(
            "pending-certification",
            mismatch.ClaimStatus);
    }

    [Fact]
    public void StatementKindAndAxiomDriftCannotCertify()
    {
        using var directory = new TemporaryFolder();
        PreparedCertification fixture = PrepareWait(directory);
        RegisterWait(fixture, directory);

        AssertMismatch(
            fixture,
            directory,
            "statement",
            ExactDeclaration(fixture) with
            {
                RequestedStatementDigest = Digest('4')
            },
            PaperCertificationReasons.RequestedStatementMismatch,
            Digest('4'));

        AssertMismatch(
            fixture,
            directory,
            "correspondence",
            ExactDeclaration(fixture) with
            {
                StatementCorrespondence = "mismatch"
            },
            PaperCertificationReasons.StatementCorrespondenceMismatch,
            "mismatch");

        AssertMismatch(
            fixture,
            directory,
            "kind",
            ExactDeclaration(fixture) with
            {
                Kind = "def"
            },
            PaperCertificationReasons.DeclarationKindIneligible,
            "def");

        AssertMismatch(
            fixture,
            directory,
            "axiom",
            ExactDeclaration(fixture) with
            {
                AxiomClosure =
                [
                    "Classical.choice",
                    "Quot.sound",
                    "propext",
                    "unsafeAxiom"
                ]
            },
            PaperCertificationReasons.AxiomPolicyMismatch,
            "unsafeAxiom");
    }

    [Fact]
    public void RegistrationJoinsPeersInEitherArrivalOrder()
    {
        using var directory = new TemporaryFolder();
        PreparedCertification fixture = PrepareWait(directory);
        PaperCertificationRelease release = ExactRelease(
            fixture,
            Digest('9'),
            [fixture.Wait.BaseTruthReleaseDigest]);

        PaperCertificationReleaseRegistration observed =
            RegisterRelease(fixture, directory, release, "first");
        Assert.Empty(observed.CertificationWaitRefs);

        PaperCertificationWaitRegistration wait =
            RegisterWait(fixture, directory);
        Assert.Contains(observed.ReleaseRef, wait.ReleaseRefs);

        PaperCertificationWaitRegistration replay =
            RegisterWait(fixture, directory);
        Assert.True(replay.Replayed);
        Assert.Equal(
            wait.ReleaseRefs.ToArray(),
            replay.ReleaseRefs.ToArray());
    }

    [Fact]
    public void FirstCertifiedResolutionCannotBeRebound()
    {
        using var directory = new TemporaryFolder();
        PreparedCertification fixture = PrepareWait(directory);
        RegisterWait(fixture, directory);

        PaperCertificationRelease firstRelease = ExactRelease(
            fixture,
            Digest('8'),
            [fixture.Wait.BaseTruthReleaseDigest]);
        PaperCertificationReleaseRegistration firstObserved =
            RegisterRelease(fixture, directory, firstRelease, "first");
        PaperCertificationEvaluationRegistration first =
            Evaluate(
                fixture,
                directory,
                firstObserved.ReleaseRef,
                "first");
        Assert.Equal(
            PaperCertificationOutcomes.Certified,
            first.Outcome);

        PaperCertificationRelease secondRelease = ExactRelease(
            fixture,
            Digest('9'),
            [
                Digest('8'),
                fixture.Wait.BaseTruthReleaseDigest
            ]);
        PaperCertificationReleaseRegistration secondObserved =
            RegisterRelease(fixture, directory, secondRelease, "second");

        Assert.Throws<InvalidDataException>(
            () => Evaluate(
                fixture,
                directory,
                secondObserved.ReleaseRef,
                "second"));
    }

    [Fact]
    public void FkstJoinIsSymmetricAndPublishesOnlyClosedOutcomes()
    {
        string root = FindRepositoryRoot();
        string package = Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper");
        string registerWait = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "register-certification-wait",
            "main.lua"));
        string observeRelease = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "observe-certification-release",
            "main.lua"));
        string evaluate = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "evaluate-certification-release",
            "main.lua"));
        string raiser = File.ReadAllText(Path.Combine(
            package,
            "raisers",
            "certification_releases.lua"));
        string researchCore = File.ReadAllText(Path.Combine(
            package,
            "research_core.lua"));

        Assert.Contains(
            "paper_candidate_pending_certification",
            registerWait,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_certification_evaluation_requested",
            registerWait,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_certification_release_seen",
            observeRelease,
            StringComparison.Ordinal);
        Assert.Contains(
            "paper_certification_evaluation_requested",
            observeRelease,
            StringComparison.Ordinal);

        foreach (string queue in new[]
        {
            "paper_candidate_still_pending_certification",
            "paper_certification_mismatch",
            "paper_certified_claim_ready"
        })
        {
            Assert.Contains(queue, evaluate, StringComparison.Ordinal);
        }

        Assert.Contains(
            "inbox/certification-releases/*.json",
            raiser,
            StringComparison.Ordinal);
        Assert.Contains(
            "Trureturing.Paper.Certification.Cli.dll",
            researchCore,
            StringComparison.Ordinal);

        foreach (string source in new[]
        {
            registerWait,
            observeRelease,
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
                "git ",
                source,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertMismatch(
        PreparedCertification fixture,
        TemporaryFolder directory,
        string suffix,
        PaperCertificationDeclaration declaration,
        string expectedReason,
        string expectedObserved)
    {
        char digestCharacter = suffix switch
        {
            "statement" => '3',
            "correspondence" => '4',
            "kind" => '5',
            "axiom" => '6',
            _ => throw new ArgumentOutOfRangeException(nameof(suffix))
        };
        PaperCertificationRelease release = ExactRelease(
            fixture,
            Digest(digestCharacter),
            [fixture.Wait.BaseTruthReleaseDigest]) with
        {
            Declarations = [declaration]
        };
        PaperCertificationReleaseRegistration observed =
            RegisterRelease(
                fixture,
                directory,
                release,
                suffix);
        PaperCertificationEvaluationRegistration result =
            Evaluate(
                fixture,
                directory,
                observed.ReleaseRef,
                suffix);

        Assert.Equal(
            PaperCertificationOutcomes.Mismatch,
            result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.NotNull(result.MismatchRef);

        var store = new PaperResearchInputStore(fixture.Store);
        PaperCertificationMismatch mismatch =
            store.Get<PaperCertificationMismatch>(
                result.MismatchRef!);
        Assert.Equal(expectedObserved, mismatch.Observed);
    }

    private static PaperCertificationWaitRegistration RegisterWait(
        PreparedCertification fixture,
        TemporaryFolder directory) =>
        PaperCertificationService.RegisterWait(
            fixture.Store,
            fixture.WaitRef,
            Path.Combine(
                directory.Path,
                "work",
                "certification-waits",
                fixture.WaitRef[7..] + ".json"),
            Path.Combine(
                directory.Path,
                "work",
                "certification-releases"));

    private static PaperCertificationReleaseRegistration RegisterRelease(
        PreparedCertification fixture,
        TemporaryFolder directory,
        PaperCertificationRelease release,
        string suffix) =>
        PaperCertificationService.RegisterRelease(
            fixture.Store,
            CanonicalJson.Serialize(release),
            Path.Combine(
                directory.Path,
                "work",
                "certification-releases",
                suffix + ".json"),
            Path.Combine(
                directory.Path,
                "work",
                "certification-waits"));

    private static PaperCertificationEvaluationRegistration Evaluate(
        PreparedCertification fixture,
        TemporaryFolder directory,
        string releaseRef,
        string suffix) =>
        PaperCertificationService.Evaluate(
            fixture.Store,
            fixture.WaitRef,
            releaseRef,
            Path.Combine(
                directory.Path,
                "work",
                "certification-evaluations",
                suffix + ".json"),
            Path.Combine(
                directory.Path,
                "work",
                "certification-resolutions",
                fixture.WaitRef[7..] + ".json"));

    private static PaperCertificationRelease ExactRelease(
        PreparedCertification fixture,
        string releaseDigest,
        IReadOnlyList<string> ancestors) =>
        new(
            PaperCertificationSchemas.ReleaseObservation,
            releaseDigest,
            releaseDigest,
            Digest('2'),
            PaperResearchSelectionService.TruthSourceRepository,
            new string('3', 40),
            new string('4', 40),
            ancestors,
            [ExactDeclaration(fixture)],
            new PaperCertificationProducer(
                PaperCertificationService.ProducerService,
                new string('5', 40)));

    private static PaperCertificationDeclaration ExactDeclaration(
        PreparedCertification fixture) =>
        new(
            fixture.Wait.Gid,
            "D5.S0.Test.reflexive",
            "theorem",
            fixture.Wait.FormalizationRequestRef,
            PaperCertificationService.RequestedStatementDigest(
                fixture.Wait.ExpectedStatement),
            Digest('1'),
            "exact",
            [
                "Classical.choice",
                "Quot.sound",
                "propext"
            ]);

    private static PreparedCertification PrepareWait(
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
                CounterexampleIsUseful: true,
                MissingPrerequisiteIsReportable: true),
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
                Path.Combine(directory.Path, "result-cursor.json"));

        PaperFormalizationOutcomeRegistration outcome =
            PaperFormalizationOutcomeService.Classify(
                storePath,
                result.ResultRef,
                Path.Combine(directory.Path, "decision-cursor.json"));
        string waitRef = outcome.CertificationWaitRef
            ?? throw new InvalidOperationException(
                "Accepted fixture did not create a certification wait.");
        var store = new PaperResearchInputStore(storePath);
        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(waitRef);
        return new PreparedCertification(
            storePath,
            waitRef,
            wait);
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

    private sealed record PreparedCertification(
        string Store,
        string WaitRef,
        PaperCertificationWait Wait);

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "paper-certification-join-tests-"
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
