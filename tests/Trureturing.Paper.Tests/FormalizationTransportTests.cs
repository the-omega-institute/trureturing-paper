using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class FormalizationTransportTests
{
    [Fact]
    public void PrepareDispatchPersistsCanonicalBindingAndReplays()
    {
        using var directory = new TemporaryFolder();
        TransportFixture fixture = CreateFixture();
        string cursor = Path.Combine(
            directory.Path,
            "work",
            "dispatch.json");

        PaperFormalizationDispatchRegistration first =
            PaperFormalizationTransportService.PrepareDispatch(
                Path.Combine(directory.Path, "store"),
                fixture.Selection,
                fixture.SelectionBytes,
                fixture.Request,
                fixture.RequestBytes,
                fixture.Selection.SelectionId,
                fixture.Request.RequestId,
                cursor);

        Assert.False(first.Replayed);
        Assert.Equal(fixture.Request.RequestId, first.FormalizationRequestRef);
        Assert.Equal(fixture.Selection.SelectionId, first.SelectionRef);
        Assert.Equal(fixture.Request.TruthRelease.SourceCommit, first.SourceCommit);

        var store = new PaperResearchInputStore(
            Path.Combine(directory.Path, "store"));
        PaperFormalizationDispatch dispatch =
            store.Get<PaperFormalizationDispatch>(first.DispatchRef);
        Assert.Equal(
            PaperFormalizationSchemas.Dispatch,
            dispatch.Schema);
        Assert.Equal(
            PaperResearchInputStore.Reference(fixture.RequestBytes),
            dispatch.RequestBlobRef);
        Assert.Equal(
            PaperResearchInputStore.Reference(fixture.SelectionBytes),
            dispatch.SelectionBlobRef);

        PaperFormalizationDispatchRegistration replay =
            PaperFormalizationTransportService.PrepareDispatch(
                Path.Combine(directory.Path, "store"),
                fixture.Selection,
                fixture.SelectionBytes,
                fixture.Request,
                fixture.RequestBytes,
                fixture.Selection.SelectionId,
                fixture.Request.RequestId,
                cursor);

        Assert.True(replay.Replayed);
        Assert.Equal(first.DispatchRef, replay.DispatchRef);
    }

    [Fact]
    public void PrepareDispatchRejectsSubstitutedEventReference()
    {
        using var directory = new TemporaryFolder();
        TransportFixture fixture = CreateFixture();

        Assert.Throws<InvalidDataException>(
            () => PaperFormalizationTransportService.PrepareDispatch(
                Path.Combine(directory.Path, "store"),
                fixture.Selection,
                fixture.SelectionBytes,
                fixture.Request,
                fixture.RequestBytes,
                fixture.Selection.SelectionId,
                Digest('9'),
                Path.Combine(directory.Path, "dispatch.json")));
    }

    [Fact]
    public void AcceptedResultRequiresExactContextAndReplays()
    {
        using var directory = new TemporaryFolder();
        PreparedFixture prepared = Prepare(directory);
        FormalizeSolveResultWire incoming =
            ExactResult(prepared.Fixture, "accepted", "candidate produced");

        PaperFormalizationResultRegistration first =
            PaperFormalizationTransportService.RecordResult(
                prepared.Store,
                prepared.Dispatch.CursorPath,
                incoming,
                prepared.ResultCursor);

        Assert.False(first.Replayed);
        Assert.Equal("verified", first.BindingStatus);
        Assert.Equal("accepted", first.Status);

        var store = new PaperResearchInputStore(prepared.Store);
        PaperFormalizationResult stored =
            store.Get<PaperFormalizationResult>(first.ResultRef);
        Assert.Equal(prepared.Dispatch.DispatchRef, stored.DispatchRef);
        Assert.Equal("verified", stored.BindingStatus);
        Assert.Equal(
            prepared.Fixture.Request.RequestId,
            stored.ObservedRequestId);

        PaperFormalizationResultRegistration replay =
            PaperFormalizationTransportService.RecordResult(
                prepared.Store,
                prepared.Dispatch.CursorPath,
                incoming,
                prepared.ResultCursor);

        Assert.True(replay.Replayed);
        Assert.Equal(first.ResultRef, replay.ResultRef);
    }

    [Fact]
    public void AcceptedResultRejectsCrossReleaseSubstitution()
    {
        using var directory = new TemporaryFolder();
        PreparedFixture prepared = Prepare(directory);
        FormalizeSolveResultWire substituted =
            ExactResult(prepared.Fixture, "accepted", "candidate produced") with
            {
                TruthReleaseDigest = Digest('9')
            };

        Assert.Throws<InvalidDataException>(
            () => PaperFormalizationTransportService.RecordResult(
                prepared.Store,
                prepared.Dispatch.CursorPath,
                substituted,
                prepared.ResultCursor));
    }

    [Fact]
    public void TypedRequestReferenceRejectionIsRecordedWithoutPromotion()
    {
        using var directory = new TemporaryFolder();
        PreparedFixture prepared = Prepare(directory);
        TransportFixture fixture = prepared.Fixture;
        var incoming = new FormalizeSolveResultWire(
            fixture.Request.RequestId,
            fixture.Request.RequestId,
            Digest('9'),
            fixture.Selection.SelectionId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "abstained",
            1,
            "REQUEST_REF_MISMATCH: event reference does not match request.request_id",
            string.Empty,
            PaperFormalizationTransportService.FormalizeResultDedupPrefix
                + fixture.Request.RequestId);

        PaperFormalizationResultRegistration registration =
            PaperFormalizationTransportService.RecordResult(
                prepared.Store,
                prepared.Dispatch.CursorPath,
                incoming,
                prepared.ResultCursor);

        Assert.Equal("abstained", registration.Status);
        Assert.Equal(
            "rejected-before-context",
            registration.BindingStatus);
    }

    [Fact]
    public void OneRequestCannotBeReboundToDifferentTerminalResults()
    {
        using var directory = new TemporaryFolder();
        PreparedFixture prepared = Prepare(directory);
        FormalizeSolveResultWire abstained =
            ExactResult(
                prepared.Fixture,
                "abstained",
                "BASE_SKILL_SEAM_UNAVAILABLE (exit 2)");
        _ = PaperFormalizationTransportService.RecordResult(
            prepared.Store,
            prepared.Dispatch.CursorPath,
            abstained,
            prepared.ResultCursor);

        FormalizeSolveResultWire accepted =
            ExactResult(
                prepared.Fixture,
                "accepted",
                "candidate produced");

        Assert.Throws<InvalidDataException>(
            () => PaperFormalizationTransportService.RecordResult(
                prepared.Store,
                prepared.Dispatch.CursorPath,
                accepted,
                prepared.ResultCursor));
    }

    [Fact]
    public void FkstPackageRoutesOnlyThroughQualifiedFormalizeQueues()
    {
        string root = FindRepositoryRoot();
        string package = Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper");
        string dispatch = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "dispatch-formalization",
            "main.lua"));
        string record = File.ReadAllText(Path.Combine(
            package,
            "departments",
            "record-formalization-result",
            "main.lua"));

        Assert.Contains(
            "trureturing-formalize.solve_request",
            dispatch,
            StringComparison.Ordinal);
        Assert.Contains(
            "trureturing-formalize.solve_result",
            record,
            StringComparison.Ordinal);
        Assert.Contains(
            "TRURETURING_PAPER_REPOSITORY_ROOT",
            record,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "exec_argv",
            dispatch,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "exec_argv",
            record,
            StringComparison.Ordinal);
    }

    private static PreparedFixture Prepare(TemporaryFolder directory)
    {
        TransportFixture fixture = CreateFixture();
        string store = Path.Combine(directory.Path, "store");
        PaperFormalizationDispatchRegistration dispatch =
            PaperFormalizationTransportService.PrepareDispatch(
                store,
                fixture.Selection,
                fixture.SelectionBytes,
                fixture.Request,
                fixture.RequestBytes,
                fixture.Selection.SelectionId,
                fixture.Request.RequestId,
                Path.Combine(directory.Path, "dispatch-cursor.json"));
        return new PreparedFixture(
            fixture,
            store,
            dispatch,
            Path.Combine(directory.Path, "result-cursor.json"));
    }

    private static FormalizeSolveResultWire ExactResult(
        TransportFixture fixture,
        string status,
        string verdict) => new(
            fixture.Request.RequestId,
            fixture.Request.RequestId,
            fixture.Request.RequestId,
            fixture.Selection.SelectionId,
            fixture.Request.TruthRelease.SourceRepo,
            fixture.Request.TruthRelease.SourceCommit,
            fixture.Request.TruthRelease.SourceTree,
            fixture.Request.TruthRelease.ReleaseDigest,
            fixture.Request.PaperContext.PaperId,
            fixture.Request.PaperContext.ResearchCandidateId,
            fixture.Request.Target.PreferredGid!,
            status,
            1,
            verdict,
            string.Empty,
            PaperFormalizationTransportService.FormalizeResultDedupPrefix
                + fixture.Request.RequestId);

    private static TransportFixture CreateFixture()
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
        return new TransportFixture(
            selection,
            request,
            PaperResearchSelectionJson.Write(selection),
            PaperResearchSelectionJson.Write(request));
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

    private sealed record TransportFixture(
        PaperResearchSelection Selection,
        FormalizationRequest Request,
        byte[] SelectionBytes,
        byte[] RequestBytes);

    private sealed record PreparedFixture(
        TransportFixture Fixture,
        string Store,
        PaperFormalizationDispatchRegistration Dispatch,
        string ResultCursor);

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "paper-formalization-tests-" + Guid.NewGuid().ToString("N"));
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
