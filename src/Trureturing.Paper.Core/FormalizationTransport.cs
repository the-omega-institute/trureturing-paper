using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperFormalizationSchemas
{
    public const string Dispatch = "paper-formalization-dispatch.v1";
    public const string DispatchCursor = "paper-formalization-dispatch-cursor.v1";
    public const string Result = "paper-formalization-result.v1";
    public const string ResultCursor = "paper-formalization-result-cursor.v1";
}

public sealed record PaperFormalizationDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string RequestBlobRef,
    [property: JsonRequired] string SelectionBlobRef,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchCandidateId,
    [property: JsonRequired] string Gid);

public sealed record PaperFormalizationDispatchCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string DispatchRef);

public sealed record FormalizeSolveResultWire(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string ObservedRequestId,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchCandidateId,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string Status,
    [property: JsonRequired] int Rounds,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] string ErrorClass,
    [property: JsonRequired] string DedupKey);

public sealed record PaperFormalizationResult(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string BindingStatus,
    [property: JsonRequired] string Id,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string ObservedRequestId,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchCandidateId,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string Status,
    [property: JsonRequired] int Rounds,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] string ErrorClass,
    [property: JsonRequired] string UpstreamDedupKey);

public sealed record PaperFormalizationResultCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string ResultRef);

public sealed record PaperFormalizationDispatchRegistration(
    string DispatchRef,
    string FormalizationRequestRef,
    string SelectionRef,
    string SourceRepo,
    string SourceCommit,
    string SourceTree,
    string TruthReleaseDigest,
    string PaperId,
    string ResearchCandidateId,
    string Gid,
    string CursorPath,
    bool Replayed);

public sealed record PaperFormalizationResultRegistration(
    string ResultRef,
    string DispatchRef,
    string FormalizationRequestRef,
    string SelectionRef,
    string Status,
    string BindingStatus,
    string CursorPath,
    bool Replayed);

public static class PaperFormalizationTransportService
{
    public const string FormalizeResultDedupPrefix =
        "tru-formalize-result:v2:";

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GitSha1Pattern = new(
        "^[0-9a-f]{40}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GidPattern = new(
        "^D[0-9]+/S[0-9]+/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*(?:\\.[A-Za-z_][A-Za-z0-9_']*)?$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> BoundaryErrorClasses =
    [
        "quota-exhausted",
        "auth-degraded",
        "provider-unavailable",
        "provider-throttle"
    ];

    public static PaperFormalizationDispatchRegistration PrepareDispatch(
        string durableRoot,
        PaperResearchSelection selection,
        ReadOnlySpan<byte> selectionBytes,
        FormalizationRequest request,
        ReadOnlySpan<byte> requestBytes,
        string expectedSelectionRef,
        string expectedRequestRef,
        string cursorPath)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(request);
        PaperResearchSelectionService.Validate(selection);
        PaperResearchSelectionService.Validate(request);
        RequireDigest(expectedSelectionRef, nameof(expectedSelectionRef));
        RequireDigest(expectedRequestRef, nameof(expectedRequestRef));

        if (!string.Equals(
                selection.SelectionId,
                expectedSelectionRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The selection file does not match the authorized selection reference.");
        }
        if (!string.Equals(
                request.RequestId,
                expectedRequestRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The request file does not match the formalization request reference.");
        }

        byte[] canonicalSelection =
            PaperResearchSelectionJson.Write(selection);
        byte[] canonicalRequest =
            PaperResearchSelectionJson.Write(request);
        if (!selectionBytes.SequenceEqual(canonicalSelection))
        {
            throw new InvalidDataException(
                "The selected governance artifact is not canonical JSON.");
        }
        if (!requestBytes.SequenceEqual(canonicalRequest))
        {
            throw new InvalidDataException(
                "The formalization request artifact is not canonical JSON.");
        }

        ValidateSelectionRequestBinding(selection, request);

        var store = new PaperResearchInputStore(durableRoot);
        string selectionBlobRef =
            PaperResearchInputStore.Reference(canonicalSelection);
        string requestBlobRef =
            PaperResearchInputStore.Reference(canonicalRequest);
        store.PutBlob(selectionBlobRef, canonicalSelection);
        store.PutBlob(requestBlobRef, canonicalRequest);

        var dispatch = new PaperFormalizationDispatch(
            PaperFormalizationSchemas.Dispatch,
            request.RequestId,
            selection.SelectionId,
            requestBlobRef,
            selectionBlobRef,
            request.TruthRelease.SourceRepo,
            request.TruthRelease.SourceCommit,
            request.TruthRelease.SourceTree,
            request.TruthRelease.ReleaseDigest,
            request.PaperContext.PaperId,
            request.PaperContext.ResearchCandidateId,
            request.Target.PreferredGid!);
        Validate(dispatch);

        string dispatchRef = store.Put(dispatch);
        string fullCursorPath = Path.GetFullPath(cursorPath);
        bool replayed = WriteDispatchCursor(
            fullCursorPath,
            dispatch.FormalizationRequestRef,
            dispatchRef);

        return new PaperFormalizationDispatchRegistration(
            dispatchRef,
            dispatch.FormalizationRequestRef,
            dispatch.SelectionRef,
            dispatch.SourceRepo,
            dispatch.SourceCommit,
            dispatch.SourceTree,
            dispatch.TruthReleaseDigest,
            dispatch.PaperId,
            dispatch.ResearchCandidateId,
            dispatch.Gid,
            fullCursorPath,
            replayed);
    }

    public static PaperFormalizationResultRegistration RecordResult(
        string durableRoot,
        string dispatchCursorPath,
        FormalizeSolveResultWire incoming,
        string resultCursorPath)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        Validate(incoming);

        PaperFormalizationDispatchCursor dispatchCursor =
            ReadDispatchCursor(dispatchCursorPath);
        if (!string.Equals(
                dispatchCursor.FormalizationRequestRef,
                incoming.FormalizationRequestRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The result does not address the dispatch cursor's formalization request.");
        }

        var store = new PaperResearchInputStore(durableRoot);
        PaperFormalizationDispatch dispatch =
            store.Get<PaperFormalizationDispatch>(dispatchCursor.DispatchRef);
        Validate(dispatch);
        if (!string.Equals(
                dispatch.FormalizationRequestRef,
                dispatchCursor.FormalizationRequestRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The dispatch cursor and content-addressed dispatch disagree.");
        }

        ValidateResultBinding(dispatch, incoming, out bool exactContext);
        string bindingStatus =
            exactContext ? "verified" : "rejected-before-context";

        var result = new PaperFormalizationResult(
            PaperFormalizationSchemas.Result,
            dispatchCursor.DispatchRef,
            bindingStatus,
            incoming.Id,
            incoming.FormalizationRequestRef,
            incoming.ObservedRequestId,
            incoming.SelectionRef,
            incoming.SourceRepo,
            incoming.SourceCommit,
            incoming.SourceTree,
            incoming.TruthReleaseDigest,
            incoming.PaperId,
            incoming.ResearchCandidateId,
            incoming.Gid,
            incoming.Status,
            incoming.Rounds,
            incoming.Verdict,
            incoming.ErrorClass,
            incoming.DedupKey);
        Validate(result);

        string resultRef = store.Put(result);
        string fullResultCursorPath = Path.GetFullPath(resultCursorPath);
        bool replayed = WriteResultCursor(
            fullResultCursorPath,
            incoming.FormalizationRequestRef,
            resultRef);

        return new PaperFormalizationResultRegistration(
            resultRef,
            dispatchCursor.DispatchRef,
            incoming.FormalizationRequestRef,
            incoming.SelectionRef,
            incoming.Status,
            bindingStatus,
            fullResultCursorPath,
            replayed);
    }

    public static void Validate(PaperFormalizationDispatch value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, PaperFormalizationSchemas.Dispatch);
        RequireDigest(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        RequireDigest(value.SelectionRef, nameof(value.SelectionRef));
        RequireDigest(value.RequestBlobRef, nameof(value.RequestBlobRef));
        RequireDigest(value.SelectionBlobRef, nameof(value.SelectionBlobRef));
        if (!string.Equals(
                value.SourceRepo,
                PaperResearchSelectionService.TruthSourceRepository,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The dispatch targets an unexpected truth repository.");
        }
        RequireGitSha1(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitSha1(value.SourceTree, nameof(value.SourceTree));
        RequireDigest(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireText(
            value.ResearchCandidateId,
            nameof(value.ResearchCandidateId),
            512);
        RequireGid(value.Gid, nameof(value.Gid));
    }

    public static void Validate(FormalizeSolveResultWire value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireDigest(value.Id, nameof(value.Id));
        RequireDigest(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        if (!string.Equals(
                value.Id,
                value.FormalizationRequestRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalize result id must equal formalization_request_ref.");
        }
        if (value.ObservedRequestId.Length != 0)
        {
            RequireDigest(
                value.ObservedRequestId,
                nameof(value.ObservedRequestId));
        }
        RequireDigest(value.SelectionRef, nameof(value.SelectionRef));
        RequireOptionalText(value.SourceRepo, nameof(value.SourceRepo), 512);
        RequireOptionalGitSha1(
            value.SourceCommit,
            nameof(value.SourceCommit));
        RequireOptionalGitSha1(value.SourceTree, nameof(value.SourceTree));
        RequireOptionalDigest(
            value.TruthReleaseDigest,
            nameof(value.TruthReleaseDigest));
        RequireOptionalText(value.PaperId, nameof(value.PaperId), 512);
        RequireOptionalText(
            value.ResearchCandidateId,
            nameof(value.ResearchCandidateId),
            512);
        if (value.Gid.Length != 0)
        {
            RequireGid(value.Gid, nameof(value.Gid));
        }
        if (value.Status is not ("accepted" or "abstained"))
        {
            throw new InvalidDataException(
                "Formalize result status must be accepted or abstained.");
        }
        if (value.Rounds <= 0)
        {
            throw new InvalidDataException(
                "Formalize result rounds must be positive.");
        }
        RequireText(value.Verdict, nameof(value.Verdict), 16384);
        if (value.ErrorClass.Length != 0
            && !BoundaryErrorClasses.Contains(value.ErrorClass))
        {
            throw new InvalidDataException(
                "Formalize result error_class is unsupported.");
        }
        string expectedDedup =
            FormalizeResultDedupPrefix + value.FormalizationRequestRef;
        if (!string.Equals(
                value.DedupKey,
                expectedDedup,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalize result dedup_key is not request-derived.");
        }
    }

    public static void Validate(PaperFormalizationResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(value.Schema, PaperFormalizationSchemas.Result);
        RequireDigest(value.DispatchRef, nameof(value.DispatchRef));
        if (value.BindingStatus is not ("verified" or "rejected-before-context"))
        {
            throw new InvalidDataException(
                "Paper formalization result binding_status is unsupported.");
        }

        Validate(new FormalizeSolveResultWire(
            value.Id,
            value.FormalizationRequestRef,
            value.ObservedRequestId,
            value.SelectionRef,
            value.SourceRepo,
            value.SourceCommit,
            value.SourceTree,
            value.TruthReleaseDigest,
            value.PaperId,
            value.ResearchCandidateId,
            value.Gid,
            value.Status,
            value.Rounds,
            value.Verdict,
            value.ErrorClass,
            value.UpstreamDedupKey));

        if (value.Status == "accepted"
            && value.BindingStatus != "verified")
        {
            throw new InvalidDataException(
                "An accepted Formalize result must have verified context.");
        }
    }

    private static void ValidateSelectionRequestBinding(
        PaperResearchSelection selection,
        FormalizationRequest request)
    {
        PaperResearchSelectionContent content = selection.SelectionContent;
        bool scalarMatch =
            string.Equals(
                request.TruthRelease.ReleaseDigest,
                content.TruthReleaseDigest,
                StringComparison.Ordinal)
            && string.Equals(
                request.PaperContext.PaperId,
                content.PaperId,
                StringComparison.Ordinal)
            && string.Equals(
                request.PaperContext.ResearchCandidateId,
                content.CandidatePaperRef,
                StringComparison.Ordinal)
            && string.Equals(
                request.PaperContext.RoleInArgument,
                content.RoleInArgument,
                StringComparison.Ordinal)
            && string.Equals(
                request.PaperContext.WhyLoadBearing,
                content.ExpectedContribution,
                StringComparison.Ordinal)
            && string.Equals(
                request.Target.PreferredGid,
                content.Target.LemmaGidIntent,
                StringComparison.Ordinal)
            && string.Equals(
                request.Target.Statement,
                content.Target.LemmaStatement,
                StringComparison.Ordinal)
            && string.Equals(
                request.Target.DesiredGenerality,
                content.ClaimBoundary,
                StringComparison.Ordinal)
            && request.FailureSemantics.CounterexampleIsUseful
                == content.FailureSemantics.CounterexampleIsUseful
            && request.FailureSemantics.MissingPrerequisiteIsReportable
                == content.FailureSemantics.MissingPrerequisiteIsReportable;

        bool listMatch =
            request.Target.KnownDependencies.SequenceEqual(
                content.Target.KnownDependencies,
                StringComparer.Ordinal)
            && request.Target.AllowedAssumptions.SequenceEqual(
                content.Target.AllowedAssumptions,
                StringComparer.Ordinal)
            && request.Target.ForbiddenWeakenings is not null
            && request.Target.ForbiddenWeakenings.SequenceEqual(
                content.Target.ForbiddenWeakenings,
                StringComparer.Ordinal)
            && request.ReuseApi.SequenceEqual(
                content.ReuseApi,
                StringComparer.Ordinal);

        if (!scalarMatch || !listMatch)
        {
            throw new InvalidDataException(
                "The formalization request does not faithfully encode the governed Paper selection.");
        }
    }

    private static void ValidateResultBinding(
        PaperFormalizationDispatch dispatch,
        FormalizeSolveResultWire incoming,
        out bool exactContext)
    {
        if (!string.Equals(
                incoming.FormalizationRequestRef,
                dispatch.FormalizationRequestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                incoming.SelectionRef,
                dispatch.SelectionRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Formalize result is correlated to another request or selection.");
        }

        RequireOptionalMatch(
            incoming.SourceRepo,
            dispatch.SourceRepo,
            nameof(incoming.SourceRepo));
        RequireOptionalMatch(
            incoming.SourceCommit,
            dispatch.SourceCommit,
            nameof(incoming.SourceCommit));
        RequireOptionalMatch(
            incoming.SourceTree,
            dispatch.SourceTree,
            nameof(incoming.SourceTree));
        RequireOptionalMatch(
            incoming.TruthReleaseDigest,
            dispatch.TruthReleaseDigest,
            nameof(incoming.TruthReleaseDigest));
        RequireOptionalMatch(
            incoming.PaperId,
            dispatch.PaperId,
            nameof(incoming.PaperId));
        RequireOptionalMatch(
            incoming.ResearchCandidateId,
            dispatch.ResearchCandidateId,
            nameof(incoming.ResearchCandidateId));
        RequireOptionalMatch(
            incoming.Gid,
            dispatch.Gid,
            nameof(incoming.Gid));

        bool observedExact = string.Equals(
            incoming.ObservedRequestId,
            dispatch.FormalizationRequestRef,
            StringComparison.Ordinal);
        if (incoming.ObservedRequestId.Length != 0
            && !observedExact
            && !(incoming.Status == "abstained"
                && incoming.Verdict.StartsWith(
                    "REQUEST_REF_MISMATCH:",
                    StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "The observed request id differs without a typed request-reference rejection.");
        }

        exactContext =
            observedExact
            && string.Equals(
                incoming.SourceRepo,
                dispatch.SourceRepo,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.SourceCommit,
                dispatch.SourceCommit,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.SourceTree,
                dispatch.SourceTree,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.TruthReleaseDigest,
                dispatch.TruthReleaseDigest,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.PaperId,
                dispatch.PaperId,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.ResearchCandidateId,
                dispatch.ResearchCandidateId,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.Gid,
                dispatch.Gid,
                StringComparison.Ordinal);

        if (incoming.Status == "accepted")
        {
            if (!exactContext)
            {
                throw new InvalidDataException(
                    "An accepted Formalize result must reproduce the complete dispatch context.");
            }
            if (incoming.ErrorClass.Length != 0)
            {
                throw new InvalidDataException(
                    "An accepted Formalize result cannot carry a boundary error class.");
            }
        }
    }

    private static PaperFormalizationDispatchCursor ReadDispatchCursor(
        string path)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
        PaperFormalizationDispatchCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperFormalizationDispatchCursor>(bytes);
        RequireSchema(
            cursor.Schema,
            PaperFormalizationSchemas.DispatchCursor);
        RequireDigest(
            cursor.FormalizationRequestRef,
            nameof(cursor.FormalizationRequestRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        return cursor;
    }

    private static bool WriteDispatchCursor(
        string path,
        string requestRef,
        string dispatchRef)
    {
        var cursor = new PaperFormalizationDispatchCursor(
            PaperFormalizationSchemas.DispatchCursor,
            requestRef,
            dispatchRef);
        byte[] bytes = CanonicalJson.Serialize(cursor);
        if (File.Exists(path))
        {
            PaperFormalizationDispatchCursor current =
                PaperResearchInputJson.DeserializeStrict<
                    PaperFormalizationDispatchCursor>(
                        File.ReadAllBytes(path));
            RequireSchema(
                current.Schema,
                PaperFormalizationSchemas.DispatchCursor);
            if (!string.Equals(
                    current.FormalizationRequestRef,
                    requestRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    current.DispatchRef,
                    dispatchRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "One formalization request cannot be rebound to another dispatch.");
            }
            return true;
        }

        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
        return false;
    }

    private static bool WriteResultCursor(
        string path,
        string requestRef,
        string resultRef)
    {
        var cursor = new PaperFormalizationResultCursor(
            PaperFormalizationSchemas.ResultCursor,
            requestRef,
            resultRef);
        byte[] bytes = CanonicalJson.Serialize(cursor);
        if (File.Exists(path))
        {
            PaperFormalizationResultCursor current =
                PaperResearchInputJson.DeserializeStrict<
                    PaperFormalizationResultCursor>(
                        File.ReadAllBytes(path));
            RequireSchema(
                current.Schema,
                PaperFormalizationSchemas.ResultCursor);
            if (!string.Equals(
                    current.FormalizationRequestRef,
                    requestRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    current.ResultRef,
                    resultRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "One formalization request cannot be rebound to a different terminal result.");
            }
            return true;
        }

        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
        return false;
    }

    private static void RequireSchema(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Expected schema {expected}, got {actual}.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireOptionalDigest(string value, string name)
    {
        if (value.Length != 0)
        {
            RequireDigest(value, name);
        }
    }

    private static void RequireGitSha1(string value, string name)
    {
        if (!GitSha1Pattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be a 40-character lowercase Git object id.");
        }
    }

    private static void RequireOptionalGitSha1(
        string value,
        string name)
    {
        if (value.Length != 0)
        {
            RequireGitSha1(value, name);
        }
    }

    private static void RequireGid(string value, string name)
    {
        if (!GidPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} is not a canonical theorem GID.");
        }
    }

    private static void RequireText(
        string value,
        string name,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximum} characters.");
        }
    }

    private static void RequireOptionalText(
        string value,
        string name,
        int maximum)
    {
        if (value.Length != 0)
        {
            RequireText(value, name, maximum);
        }
    }

    private static void RequireOptionalMatch(
        string actual,
        string expected,
        string name)
    {
        if (actual.Length != 0
            && !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} conflicts with the content-addressed dispatch.");
        }
    }
}
