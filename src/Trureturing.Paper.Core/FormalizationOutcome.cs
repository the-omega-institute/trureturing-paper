using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperFormalizationOutcomeSchemas
{
    public const string Decision = "paper-formalization-decision.v1";
    public const string CertificationWait = "paper-certification-wait.v1";
    public const string DecisionCursor = "paper-formalization-decision-cursor.v1";
}

public static class PaperFormalizationOutcomeRoutes
{
    public const string AwaitCertification = "await-certification";
    public const string IntuitionResearch = "intuition-research";
    public const string SublemmaResearch = "sublemma-research";
    public const string NoveltyReassessment = "novelty-reassessment";
    public const string ProofStrategyRevision = "proof-strategy-revision";
    public const string Blocked = "blocked";
}

public sealed record PaperFormalizationDecision(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] string IntuitionProposalRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string VerificationBudgetRef,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchCandidateId,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string ResultStatus,
    [property: JsonRequired] string BindingStatus,
    [property: JsonRequired] string VerdictToken,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] string OutcomeClass,
    [property: JsonRequired] string Route,
    [property: JsonRequired] string ClaimStatus,
    [property: JsonRequired] string Rationale);

public sealed record PaperCertificationWait(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DecisionRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string RequestBlobRef,
    [property: JsonRequired] string SelectionBlobRef,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] string IntuitionProposalRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string VerificationBudgetRef,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string BaseTruthReleaseDigest,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchCandidateId,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string ExpectedStatement,
    [property: JsonRequired] string? DesiredGenerality,
    [property: JsonRequired] IReadOnlyList<string> KnownDependencies,
    [property: JsonRequired] IReadOnlyList<string> AllowedAssumptions,
    [property: JsonRequired] IReadOnlyList<string>? ForbiddenWeakenings,
    [property: JsonRequired] string ClaimStatus);

public sealed record PaperFormalizationDecisionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DecisionRef,
    [property: JsonRequired] string? CertificationWaitRef);

public sealed record PaperFormalizationOutcomeRegistration(
    string DecisionRef,
    string ResultRef,
    string DispatchRef,
    string FormalizationRequestRef,
    string SelectionRef,
    string PaperResearchInputRef,
    string IntuitionProposalRef,
    string CandidatePaperRef,
    string LiteratureResearchRef,
    string VerificationBudgetRef,
    string Route,
    string OutcomeClass,
    string ClaimStatus,
    string? CertificationWaitRef,
    string CursorPath,
    bool Replayed);

public static class PaperFormalizationOutcomeService
{
    private const string PendingCertification = "pending-certification";
    private const string Ineligible = "ineligible";

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GitSha1Pattern = new(
        "^[0-9a-f]{40}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GidPattern = new(
        "^D[0-9]+/S[0-9]+/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*(?:\\.[A-Za-z_][A-Za-z0-9_']*)?$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Routes =
    [
        PaperFormalizationOutcomeRoutes.AwaitCertification,
        PaperFormalizationOutcomeRoutes.IntuitionResearch,
        PaperFormalizationOutcomeRoutes.SublemmaResearch,
        PaperFormalizationOutcomeRoutes.NoveltyReassessment,
        PaperFormalizationOutcomeRoutes.ProofStrategyRevision,
        PaperFormalizationOutcomeRoutes.Blocked
    ];

    private static readonly HashSet<string> OutcomeClasses =
    [
        "candidate-produced",
        "counterexample",
        "statement-inconsistent",
        "missing-prerequisite",
        "generality-too-strong",
        "already-implied-by-library",
        "candidate-invalid",
        "proof-search-exhausted",
        "infrastructure-blocked",
        "request-rejected",
        "unclassified"
    ];

    private static readonly HashSet<string> RequestRejectionTokens =
    [
        "SCHEMA_INVALID",
        "REQUEST_ID_MISMATCH",
        "REQUEST_REF_MISMATCH",
        "REQUEST_COORDINATE_MISMATCH",
        "REQUEST_GID_MISMATCH",
        "INVALID_REQUEST_REF",
        "INVALID_SELECTION_REF",
        "MISSING_INPUT"
    ];

    private static readonly HashSet<string> InfrastructureTokens =
    [
        "BASE_SKILL_SEAM_UNAVAILABLE",
        "BASE_EXPORT_UNAVAILABLE",
        "GIT_ERROR",
        "INPUT_UNAVAILABLE",
        "REQUEST_UNAVAILABLE",
        "DIRTY_WORKTREE",
        "WRONG_BASE_COMMIT",
        "MISSING_RUNTIME_BINDING",
        "CLI_ERROR",
        "USAGE_ERROR",
        "INTERNAL_ERROR"
    ];

    private static readonly HashSet<string> CandidateInvalidTokens =
    [
        "STRAY_PATHS",
        "PATCH_DIGEST_MISMATCH",
        "VERIFICATION_DIGEST_MISMATCH",
        "BUNDLE_FILE_MISSING",
        "SHA256SUMS_DIGEST_MISMATCH",
        "SHA256SUMS_INCOMPLETE",
        "EMPTY_CHANGESET"
    ];

    private static readonly HashSet<string> ProofSearchTokens =
    [
        "PROOF_SEARCH_EXHAUSTED",
        "SEARCH_EXHAUSTED",
        "VERIFICATION_FAILED",
        "TIMEOUT"
    ];

    public static PaperFormalizationOutcomeRegistration Classify(
        string durableRoot,
        string resultRef,
        string cursorPath)
    {
        RequireDigest(resultRef, nameof(resultRef));
        var store = new PaperResearchInputStore(durableRoot);

        PaperFormalizationResult result =
            store.Get<PaperFormalizationResult>(resultRef);
        PaperFormalizationTransportService.Validate(result);

        PaperFormalizationDispatch dispatch =
            store.Get<PaperFormalizationDispatch>(result.DispatchRef);
        PaperFormalizationTransportService.Validate(dispatch);

        byte[] requestBytes = ReadBlob(
            durableRoot,
            dispatch.RequestBlobRef);
        byte[] selectionBytes = ReadBlob(
            durableRoot,
            dispatch.SelectionBlobRef);
        FormalizationRequest request =
            PaperResearchSelectionJson.ReadFormalizationRequest(requestBytes);
        PaperResearchSelection selection =
            PaperResearchSelectionJson.ReadSelection(selectionBytes);

        ValidateChain(result, dispatch, request, selection);
        Classification classification = ClassifyResult(result, selection);

        var decision = new PaperFormalizationDecision(
            PaperFormalizationOutcomeSchemas.Decision,
            resultRef,
            result.DispatchRef,
            result.FormalizationRequestRef,
            result.SelectionRef,
            selection.SelectionContent.PaperResearchInputRef,
            selection.SelectionContent.IntuitionProposalRef,
            selection.SelectionContent.CandidatePaperRef,
            selection.SelectionContent.LiteratureResearchRef,
            selection.SelectionContent.VerificationBudgetRef,
            dispatch.SourceRepo,
            dispatch.SourceCommit,
            dispatch.SourceTree,
            dispatch.TruthReleaseDigest,
            dispatch.PaperId,
            dispatch.ResearchCandidateId,
            dispatch.Gid,
            result.Status,
            result.BindingStatus,
            classification.VerdictToken,
            result.Verdict,
            classification.OutcomeClass,
            classification.Route,
            classification.ClaimStatus,
            classification.Rationale);
        Validate(decision);
        string decisionRef = store.Put(decision);

        string? waitRef = null;
        if (classification.Route
            == PaperFormalizationOutcomeRoutes.AwaitCertification)
        {
            var wait = new PaperCertificationWait(
                PaperFormalizationOutcomeSchemas.CertificationWait,
                decisionRef,
                resultRef,
                result.DispatchRef,
                result.FormalizationRequestRef,
                result.SelectionRef,
                dispatch.RequestBlobRef,
                dispatch.SelectionBlobRef,
                selection.SelectionContent.PaperResearchInputRef,
                selection.SelectionContent.IntuitionProposalRef,
                selection.SelectionContent.CandidatePaperRef,
                selection.SelectionContent.LiteratureResearchRef,
                selection.SelectionContent.VerificationBudgetRef,
                dispatch.SourceRepo,
                dispatch.SourceCommit,
                dispatch.SourceTree,
                dispatch.TruthReleaseDigest,
                dispatch.PaperId,
                dispatch.ResearchCandidateId,
                dispatch.Gid,
                request.Target.Statement,
                request.Target.DesiredGenerality,
                request.Target.KnownDependencies.ToArray(),
                request.Target.AllowedAssumptions.ToArray(),
                request.Target.ForbiddenWeakenings?.ToArray(),
                PendingCertification);
            Validate(wait, decision);
            waitRef = store.Put(wait);
        }

        string fullCursorPath = Path.GetFullPath(cursorPath);
        bool replayed = WriteCursor(
            fullCursorPath,
            resultRef,
            decisionRef,
            waitRef);

        return new PaperFormalizationOutcomeRegistration(
            decisionRef,
            resultRef,
            result.DispatchRef,
            result.FormalizationRequestRef,
            result.SelectionRef,
            selection.SelectionContent.PaperResearchInputRef,
            selection.SelectionContent.IntuitionProposalRef,
            selection.SelectionContent.CandidatePaperRef,
            selection.SelectionContent.LiteratureResearchRef,
            selection.SelectionContent.VerificationBudgetRef,
            classification.Route,
            classification.OutcomeClass,
            classification.ClaimStatus,
            waitRef,
            fullCursorPath,
            replayed);
    }

    public static void Validate(PaperFormalizationDecision value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperFormalizationOutcomeSchemas.Decision);
        RequireDigest(value.ResultRef, nameof(value.ResultRef));
        RequireDigest(value.DispatchRef, nameof(value.DispatchRef));
        RequireDigest(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        RequireDigest(value.SelectionRef, nameof(value.SelectionRef));
        RequireDigest(
            value.PaperResearchInputRef,
            nameof(value.PaperResearchInputRef));
        RequireDigest(
            value.IntuitionProposalRef,
            nameof(value.IntuitionProposalRef));
        RequireDigest(
            value.CandidatePaperRef,
            nameof(value.CandidatePaperRef));
        RequireDigest(
            value.LiteratureResearchRef,
            nameof(value.LiteratureResearchRef));
        RequireDigest(
            value.VerificationBudgetRef,
            nameof(value.VerificationBudgetRef));
        RequireSource(value);
        if (value.ResultStatus is not ("accepted" or "abstained"))
        {
            throw new InvalidDataException(
                "Decision result_status must be accepted or abstained.");
        }
        if (value.BindingStatus
            is not ("verified" or "rejected-before-context"))
        {
            throw new InvalidDataException(
                "Decision binding_status is unsupported.");
        }
        RequireText(value.VerdictToken, nameof(value.VerdictToken), 128);
        if (!Regex.IsMatch(
                value.VerdictToken,
                "^[A-Z0-9_]+$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException(
                "Decision verdict_token must use the closed machine-token alphabet.");
        }
        RequireText(value.Verdict, nameof(value.Verdict), 16384);
        if (!OutcomeClasses.Contains(value.OutcomeClass))
        {
            throw new InvalidDataException(
                "Decision outcome_class is unsupported.");
        }
        if (!Routes.Contains(value.Route))
        {
            throw new InvalidDataException(
                "Decision route is unsupported.");
        }
        if (value.ClaimStatus != PendingCertification
            && value.ClaimStatus != Ineligible)
        {
            throw new InvalidDataException(
                "Decision claim_status is unsupported.");
        }
        RequireText(value.Rationale, nameof(value.Rationale), 4096);
        if (value.CandidatePaperRef != value.ResearchCandidateId)
        {
            throw new InvalidDataException(
                "Decision candidate_paper_ref must equal research_candidate_id.");
        }

        bool awaitsCertification =
            value.Route
            == PaperFormalizationOutcomeRoutes.AwaitCertification;
        if (awaitsCertification)
        {
            if (value.ResultStatus != "accepted"
                || value.BindingStatus != "verified"
                || value.OutcomeClass != "candidate-produced"
                || value.ClaimStatus != PendingCertification)
            {
                throw new InvalidDataException(
                    "Only an accepted, verified candidate can await certification.");
            }
        }
        else
        {
            if (value.ResultStatus != "abstained"
                || value.ClaimStatus != Ineligible)
            {
                throw new InvalidDataException(
                    "A non-certification route must remain an ineligible abstention.");
            }
        }

        if (value.BindingStatus == "rejected-before-context"
            && (value.Route != PaperFormalizationOutcomeRoutes.Blocked
                || value.OutcomeClass != "request-rejected"))
        {
            throw new InvalidDataException(
                "A pre-context rejection must remain a blocked request diagnostic.");
        }
    }

    public static void Validate(
        PaperCertificationWait value,
        PaperFormalizationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(decision);
        Validate(decision);
        RequireSchema(
            value.Schema,
            PaperFormalizationOutcomeSchemas.CertificationWait);
        RequireDigest(value.DecisionRef, nameof(value.DecisionRef));
        RequireDigest(value.ResultRef, nameof(value.ResultRef));
        RequireDigest(value.DispatchRef, nameof(value.DispatchRef));
        RequireDigest(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        RequireDigest(value.SelectionRef, nameof(value.SelectionRef));
        RequireDigest(value.RequestBlobRef, nameof(value.RequestBlobRef));
        RequireDigest(value.SelectionBlobRef, nameof(value.SelectionBlobRef));
        RequireDigest(
            value.PaperResearchInputRef,
            nameof(value.PaperResearchInputRef));
        RequireDigest(
            value.IntuitionProposalRef,
            nameof(value.IntuitionProposalRef));
        RequireDigest(
            value.CandidatePaperRef,
            nameof(value.CandidatePaperRef));
        RequireDigest(
            value.LiteratureResearchRef,
            nameof(value.LiteratureResearchRef));
        RequireDigest(
            value.VerificationBudgetRef,
            nameof(value.VerificationBudgetRef));
        RequireSource(value);
        RequireText(
            value.ExpectedStatement,
            nameof(value.ExpectedStatement),
            16384);
        if (value.DesiredGenerality is not null)
        {
            RequireText(
                value.DesiredGenerality,
                nameof(value.DesiredGenerality),
                8192);
        }
        RequireTextList(
            value.KnownDependencies,
            nameof(value.KnownDependencies),
            4096);
        RequireTextList(
            value.AllowedAssumptions,
            nameof(value.AllowedAssumptions),
            4096);
        if (value.ForbiddenWeakenings is not null)
        {
            RequireTextList(
                value.ForbiddenWeakenings,
                nameof(value.ForbiddenWeakenings),
                8192);
        }
        if (value.ClaimStatus != PendingCertification)
        {
            throw new InvalidDataException(
                "A certification wait must remain pending-certification.");
        }

        if (!string.Equals(
                value.DecisionRef,
                PaperResearchInputStore.Reference(
                    CanonicalJson.Serialize(decision)),
                StringComparison.Ordinal)
            || value.ResultRef != decision.ResultRef
            || value.DispatchRef != decision.DispatchRef
            || value.FormalizationRequestRef
                != decision.FormalizationRequestRef
            || value.SelectionRef != decision.SelectionRef
            || value.PaperResearchInputRef
                != decision.PaperResearchInputRef
            || value.IntuitionProposalRef
                != decision.IntuitionProposalRef
            || value.CandidatePaperRef != decision.CandidatePaperRef
            || value.LiteratureResearchRef
                != decision.LiteratureResearchRef
            || value.VerificationBudgetRef
                != decision.VerificationBudgetRef
            || value.SourceRepo != decision.SourceRepo
            || value.SourceCommit != decision.SourceCommit
            || value.SourceTree != decision.SourceTree
            || value.BaseTruthReleaseDigest
                != decision.TruthReleaseDigest
            || value.PaperId != decision.PaperId
            || value.ResearchCandidateId
                != decision.ResearchCandidateId
            || value.Gid != decision.Gid)
        {
            throw new InvalidDataException(
                "The certification wait is not bound to its outcome decision.");
        }
    }

    private static Classification ClassifyResult(
        PaperFormalizationResult result,
        PaperResearchSelection selection)
    {
        if (result.Status == "accepted")
        {
            return new Classification(
                "ACCEPTED",
                "candidate-produced",
                PaperFormalizationOutcomeRoutes.AwaitCertification,
                PendingCertification,
                "Formalize returned a request-bound candidate. Paper must wait for a later certified truth release before treating the claim as true.");
        }

        string token = ExtractVerdictToken(result.Verdict);
        if (result.BindingStatus == "rejected-before-context")
        {
            return new Classification(
                token.Length == 0 ? "REQUEST_REJECTED" : token,
                "request-rejected",
                PaperFormalizationOutcomeRoutes.Blocked,
                Ineligible,
                "Formalize rejected the request before reconstructing the complete exact-release context. This result is diagnostic evidence only.");
        }

        if (result.ErrorClass.Length != 0)
        {
            return new Classification(
                result.ErrorClass.ToUpperInvariant().Replace('-', '_'),
                "infrastructure-blocked",
                PaperFormalizationOutcomeRoutes.Blocked,
                Ineligible,
                "The Formalize boundary reported an infrastructure failure. No mathematical conclusion follows.");
        }

        if (token is "COUNTEREXAMPLE" or "COUNTEREXAMPLE_FOUND")
        {
            return selection.SelectionContent.FailureSemantics
                .CounterexampleIsUseful
                ? new Classification(
                    token,
                    "counterexample",
                    PaperFormalizationOutcomeRoutes.IntuitionResearch,
                    Ineligible,
                    "A typed counterexample is useful under the selected failure policy and must reshape the conjecture or argument.")
                : new Classification(
                    token,
                    "counterexample",
                    PaperFormalizationOutcomeRoutes.Blocked,
                    Ineligible,
                    "A counterexample was returned, but the governed selection did not authorize it as a research route.");
        }

        if (token == "STATEMENT_INCONSISTENT")
        {
            return new Classification(
                token,
                "statement-inconsistent",
                PaperFormalizationOutcomeRoutes.IntuitionResearch,
                Ineligible,
                "The selected statement is inconsistent with the certified context and requires conjecture repair.");
        }

        if (token == "GENERALITY_TOO_STRONG")
        {
            return new Classification(
                token,
                "generality-too-strong",
                PaperFormalizationOutcomeRoutes.IntuitionResearch,
                Ineligible,
                "The requested generality is too strong and must be revised without silently weakening the selected claim.");
        }

        if (token is "MISSING_PREREQUISITE"
            or "MISSING_PREREQUISITE_FOUND")
        {
            return selection.SelectionContent.FailureSemantics
                .MissingPrerequisiteIsReportable
                ? new Classification(
                    token,
                    "missing-prerequisite",
                    PaperFormalizationOutcomeRoutes.SublemmaResearch,
                    Ineligible,
                    "Formalize identified a reportable prerequisite gap that should become a separately governed sublemma.")
                : new Classification(
                    token,
                    "missing-prerequisite",
                    PaperFormalizationOutcomeRoutes.Blocked,
                    Ineligible,
                    "A prerequisite is missing, but the governed selection did not authorize automatic sublemma expansion.");
        }

        if (token is "ALREADY_IMPLIED_BY_LIBRARY"
            or "ALREADY_PROVED"
            or "THEOREM_ALREADY_PRESENT")
        {
            return new Classification(
                token,
                "already-implied-by-library",
                PaperFormalizationOutcomeRoutes.NoveltyReassessment,
                Ineligible,
                "The proposed claim appears to be already available. Paper must reassess novelty and contribution before proceeding.");
        }

        if (CandidateInvalidTokens.Contains(token))
        {
            return new Classification(
                token,
                "candidate-invalid",
                PaperFormalizationOutcomeRoutes.ProofStrategyRevision,
                Ineligible,
                "The produced candidate failed a structural or integrity gate and requires a new proof strategy.");
        }

        if (ProofSearchTokens.Contains(token))
        {
            return new Classification(
                token,
                "proof-search-exhausted",
                PaperFormalizationOutcomeRoutes.ProofStrategyRevision,
                Ineligible,
                "The bounded proof attempt did not establish the selected theorem. Paper may revise the strategy or budget without promoting the claim.");
        }

        if (RequestRejectionTokens.Contains(token))
        {
            return new Classification(
                token,
                "request-rejected",
                PaperFormalizationOutcomeRoutes.Blocked,
                Ineligible,
                "The request or its transport binding was rejected. The scientific claim remains ineligible.");
        }

        if (InfrastructureTokens.Contains(token))
        {
            return new Classification(
                token,
                "infrastructure-blocked",
                PaperFormalizationOutcomeRoutes.Blocked,
                Ineligible,
                "A required Formalize capability or execution boundary is unavailable. No mathematical conclusion follows.");
        }

        return new Classification(
            token.Length == 0 ? "UNCLASSIFIED" : token,
            "unclassified",
            PaperFormalizationOutcomeRoutes.Blocked,
            Ineligible,
            "The Formalize outcome is outside Paper's closed routing vocabulary and requires explicit review.");
    }

    private static void ValidateChain(
        PaperFormalizationResult result,
        PaperFormalizationDispatch dispatch,
        FormalizationRequest request,
        PaperResearchSelection selection)
    {
        if (result.DispatchRef
            != PaperResearchInputStore.Reference(
                CanonicalJson.Serialize(dispatch)))
        {
            throw new InvalidDataException(
                "The result dispatch_ref does not address the resolved dispatch.");
        }
        if (dispatch.FormalizationRequestRef != request.RequestId
            || dispatch.SelectionRef != selection.SelectionId
            || dispatch.RequestBlobRef
                != PaperResearchInputStore.Reference(
                    PaperResearchSelectionJson.Write(request))
            || dispatch.SelectionBlobRef
                != PaperResearchInputStore.Reference(
                    PaperResearchSelectionJson.Write(selection)))
        {
            throw new InvalidDataException(
                "The dispatch does not address the resolved request and selection.");
        }

        ValidateSelectionRequestBinding(selection, request);

        bool dispatchMatchesRequest =
            dispatch.SourceRepo == request.TruthRelease.SourceRepo
            && dispatch.SourceCommit == request.TruthRelease.SourceCommit
            && dispatch.SourceTree == request.TruthRelease.SourceTree
            && dispatch.TruthReleaseDigest
                == request.TruthRelease.ReleaseDigest
            && dispatch.PaperId == request.PaperContext.PaperId
            && dispatch.ResearchCandidateId
                == request.PaperContext.ResearchCandidateId
            && dispatch.Gid == request.Target.PreferredGid;
        if (!dispatchMatchesRequest)
        {
            throw new InvalidDataException(
                "The dispatch and formalization request do not describe one exact state.");
        }

        if (result.FormalizationRequestRef
                != dispatch.FormalizationRequestRef
            || result.SelectionRef != dispatch.SelectionRef)
        {
            throw new InvalidDataException(
                "The result is correlated to another dispatch.");
        }

        if (result.BindingStatus == "verified")
        {
            bool exact =
                result.ObservedRequestId
                    == dispatch.FormalizationRequestRef
                && result.SourceRepo == dispatch.SourceRepo
                && result.SourceCommit == dispatch.SourceCommit
                && result.SourceTree == dispatch.SourceTree
                && result.TruthReleaseDigest
                    == dispatch.TruthReleaseDigest
                && result.PaperId == dispatch.PaperId
                && result.ResearchCandidateId
                    == dispatch.ResearchCandidateId
                && result.Gid == dispatch.Gid;
            if (!exact)
            {
                throw new InvalidDataException(
                    "A verified result does not reproduce the complete dispatch context.");
            }
        }
        else if (result.Status != "abstained")
        {
            throw new InvalidDataException(
                "A pre-context result must be an abstention.");
        }
    }

    private static void ValidateSelectionRequestBinding(
        PaperResearchSelection selection,
        FormalizationRequest request)
    {
        PaperResearchSelectionContent content = selection.SelectionContent;
        bool scalarMatch =
            request.TruthRelease.ReleaseDigest
                == content.TruthReleaseDigest
            && request.PaperContext.PaperId == content.PaperId
            && request.PaperContext.ResearchCandidateId
                == content.CandidatePaperRef
            && request.PaperContext.RoleInArgument
                == content.RoleInArgument
            && request.PaperContext.WhyLoadBearing
                == content.ExpectedContribution
            && request.Target.PreferredGid
                == content.Target.LemmaGidIntent
            && request.Target.Statement
                == content.Target.LemmaStatement
            && request.Target.DesiredGenerality
                == content.ClaimBoundary
            && request.FailureSemantics.CounterexampleIsUseful
                == content.FailureSemantics.CounterexampleIsUseful
            && request.FailureSemantics
                    .MissingPrerequisiteIsReportable
                == content.FailureSemantics
                    .MissingPrerequisiteIsReportable;

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
                "The request is no longer faithful to the governed selection.");
        }
    }

    private static string ExtractVerdictToken(string verdict)
    {
        var token = new StringBuilder();
        bool started = false;
        foreach (char character in verdict)
        {
            if (!started && char.IsWhiteSpace(character))
            {
                continue;
            }
            started = true;
            if (char.IsLetterOrDigit(character)
                || character is '_' or '-')
            {
                token.Append(character);
                continue;
            }
            break;
        }

        return token.ToString()
            .Replace('-', '_')
            .ToUpperInvariant();
    }

    private static byte[] ReadBlob(
        string durableRoot,
        string reference)
    {
        RequireDigest(reference, nameof(reference));
        string hex = reference["sha256:".Length..];
        string path = Path.Combine(
            Path.GetFullPath(durableRoot),
            "blobs",
            hex[..2],
            hex + ".json");
        byte[] bytes = File.ReadAllBytes(path);
        if (PaperResearchInputStore.Reference(bytes) != reference)
        {
            throw new InvalidDataException(
                $"Blob {reference} failed digest verification.");
        }
        return bytes;
    }

    private static bool WriteCursor(
        string path,
        string resultRef,
        string decisionRef,
        string? waitRef)
    {
        var cursor = new PaperFormalizationDecisionCursor(
            PaperFormalizationOutcomeSchemas.DecisionCursor,
            resultRef,
            decisionRef,
            waitRef);
        Validate(cursor);
        byte[] bytes = CanonicalJson.Serialize(cursor);

        if (File.Exists(path))
        {
            PaperFormalizationDecisionCursor current =
                PaperResearchInputJson.DeserializeStrict<
                    PaperFormalizationDecisionCursor>(
                        File.ReadAllBytes(path));
            Validate(current);
            if (current != cursor)
            {
                throw new InvalidDataException(
                    "One Formalize result cannot be rebound to another Paper outcome decision.");
            }
            return true;
        }

        PaperResearchInputStore.WriteAtomic(
            path,
            bytes,
            overwrite: false);
        return false;
    }

    private static void Validate(
        PaperFormalizationDecisionCursor value)
    {
        RequireSchema(
            value.Schema,
            PaperFormalizationOutcomeSchemas.DecisionCursor);
        RequireDigest(value.ResultRef, nameof(value.ResultRef));
        RequireDigest(value.DecisionRef, nameof(value.DecisionRef));
        if (value.CertificationWaitRef is not null)
        {
            RequireDigest(
                value.CertificationWaitRef,
                nameof(value.CertificationWaitRef));
        }
    }

    private static void RequireSource(
        PaperFormalizationDecision value)
    {
        if (value.SourceRepo
            != PaperResearchSelectionService.TruthSourceRepository)
        {
            throw new InvalidDataException(
                "Decision source_repo is unsupported.");
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

    private static void RequireSource(
        PaperCertificationWait value)
    {
        if (value.SourceRepo
            != PaperResearchSelectionService.TruthSourceRepository)
        {
            throw new InvalidDataException(
                "Certification wait source_repo is unsupported.");
        }
        RequireGitSha1(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitSha1(value.SourceTree, nameof(value.SourceTree));
        RequireDigest(
            value.BaseTruthReleaseDigest,
            nameof(value.BaseTruthReleaseDigest));
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireText(
            value.ResearchCandidateId,
            nameof(value.ResearchCandidateId),
            512);
        RequireGid(value.Gid, nameof(value.Gid));
    }

    private static void RequireSchema(
        string actual,
        string expected)
    {
        if (actual != expected)
        {
            throw new InvalidDataException(
                $"Expected schema {expected}, got {actual}.");
        }
    }

    private static void RequireDigest(
        string value,
        string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireGitSha1(
        string value,
        string name)
    {
        if (!GitSha1Pattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be a 40-character lowercase Git object id.");
        }
    }

    private static void RequireGid(
        string value,
        string name)
    {
        if (!GidPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be a canonical theorem GID.");
        }
    }

    private static void RequireText(
        string value,
        string name,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximum)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximum} characters.");
        }
    }

    private static void RequireTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximumItemLength)
    {
        if (values is null)
        {
            throw new InvalidDataException(
                $"{name} must be an array.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, maximumItemLength);
            if (!seen.Add(value))
            {
                throw new InvalidDataException(
                    $"{name} must not contain duplicate values.");
            }
        }
    }

    private sealed record Classification(
        string VerdictToken,
        string OutcomeClass,
        string Route,
        string ClaimStatus,
        string Rationale);
}
