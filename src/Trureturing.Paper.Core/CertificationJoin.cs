using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperCertificationSchemas
{
    public const string ReleaseObservation = "paper-certification-release.v1";
    public const string Evaluation = "paper-certification-evaluation.v1";
    public const string Mismatch = "paper-certification-mismatch.v1";
    public const string CertifiedClaim = "paper-certified-claim.v1";
    public const string WaitCursor = "paper-certification-wait-cursor.v1";
    public const string ReleaseCursor = "paper-certification-release-cursor.v1";
    public const string EvaluationCursor = "paper-certification-evaluation-cursor.v1";
    public const string ResolutionCursor = "paper-certification-resolution-cursor.v1";
}

public static class PaperCertificationOutcomes
{
    public const string StillPending = "still-pending";
    public const string Mismatch = "mismatch";
    public const string Certified = "certified";
}

public static class PaperCertificationReasons
{
    public const string SameRelease = "same-release";
    public const string DeclarationAbsent = "declaration-absent";
    public const string ReleaseLineageMismatch = "release-lineage-mismatch";
    public const string RequestMismatch = "request-mismatch";
    public const string RequestedStatementMismatch = "requested-statement-mismatch";
    public const string StatementCorrespondenceMismatch = "statement-correspondence-mismatch";
    public const string DeclarationKindIneligible = "declaration-kind-ineligible";
    public const string AxiomPolicyMismatch = "axiom-policy-mismatch";
    public const string ExactCertification = "exact-certification";
}

public sealed record PaperCertificationProducer(
    [property: JsonRequired] string Service,
    [property: JsonRequired] string Commit);

public sealed record PaperCertificationDeclaration(
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string LeanDeclaration,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string RequestedStatementDigest,
    [property: JsonRequired] string StatementId,
    [property: JsonRequired] string StatementCorrespondence,
    [property: JsonRequired] IReadOnlyList<string> AxiomClosure);

public sealed record PaperCertificationRelease(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ReleaseDigest,
    [property: JsonRequired] string BundleRef,
    [property: JsonRequired] string PublicationRef,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] IReadOnlyList<string> AncestorReleaseDigests,
    [property: JsonRequired] IReadOnlyList<PaperCertificationDeclaration> Declarations,
    [property: JsonRequired] PaperCertificationProducer Producer);

public sealed record PaperCertificationEvaluation(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CertificationWaitRef,
    [property: JsonRequired] string ReleaseRef,
    [property: JsonRequired] string BaseTruthReleaseDigest,
    [property: JsonRequired] string ObservedReleaseDigest,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string Outcome,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string ClaimStatus,
    string? CertifiedClaimRef,
    string? MismatchRef);

public sealed record PaperCertificationMismatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CertificationWaitRef,
    [property: JsonRequired] string ReleaseRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string Expected,
    [property: JsonRequired] string Observed,
    [property: JsonRequired] string ClaimStatus);

public sealed record PaperCertifiedClaim(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CertificationWaitRef,
    [property: JsonRequired] string DecisionRef,
    [property: JsonRequired] string FormalizationResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] string IntuitionProposalRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string VerificationBudgetRef,
    [property: JsonRequired] string CertifyingReleaseRef,
    [property: JsonRequired] string CertifyingReleaseDigest,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchCandidateId,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string LeanDeclaration,
    [property: JsonRequired] string DeclarationKind,
    [property: JsonRequired] string ExpectedStatement,
    [property: JsonRequired] string RequestedStatementDigest,
    [property: JsonRequired] string StatementId,
    [property: JsonRequired] string StatementCorrespondence,
    [property: JsonRequired] IReadOnlyList<string> AxiomClosure,
    [property: JsonRequired] string ClaimStatus);

public sealed record PaperCertificationWaitCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CertificationWaitRef);

public sealed record PaperCertificationReleaseCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ReleaseRef,
    [property: JsonRequired] string ReleaseDigest);

public sealed record PaperCertificationEvaluationCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CertificationWaitRef,
    [property: JsonRequired] string ReleaseRef,
    [property: JsonRequired] string EvaluationRef);

public sealed record PaperCertificationResolutionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CertificationWaitRef,
    [property: JsonRequired] string CertifiedClaimRef);

public sealed record PaperCertificationWaitRegistration(
    string CertificationWaitRef,
    string CursorPath,
    IReadOnlyList<string> ReleaseRefs,
    bool Replayed);

public sealed record PaperCertificationReleaseRegistration(
    string ReleaseRef,
    string ReleaseDigest,
    string CursorPath,
    IReadOnlyList<string> CertificationWaitRefs,
    bool Replayed);

public sealed record PaperCertificationEvaluationRegistration(
    string EvaluationRef,
    string CertificationWaitRef,
    string ReleaseRef,
    string Outcome,
    string Reason,
    string ClaimStatus,
    string? CertifiedClaimRef,
    string? MismatchRef,
    string CursorPath,
    bool Replayed);

public static class PaperCertificationService
{
    public const string ProducerService =
        "trureturing-paper-truth-release-adapter";

    public const string PendingCertification = "pending-certification";
    public const string Certified = "certified";

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GitSha1Pattern = new(
        "^[0-9a-f]{40}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex GidPattern = new(
        "^D[0-9]+/S[0-9]+/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*(?:\\.[A-Za-z_][A-Za-z0-9_']*)?$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> EligibleDeclarationKinds =
    [
        "theorem"
    ];

    private static readonly HashSet<string> AllowedAxioms =
    [
        "Classical.choice",
        "Quot.sound",
        "propext"
    ];

    public static string RequestedStatementDigest(string statement)
    {
        RequireText(statement, nameof(statement), 16384);
        byte[] domain = Encoding.UTF8.GetBytes(
            "trureturing:paper-request-statement:v1\0");
        byte[] material = Encoding.UTF8.GetBytes(statement);
        byte[] combined = new byte[domain.Length + material.Length];
        domain.CopyTo(combined, 0);
        material.CopyTo(combined, domain.Length);
        return "sha256:" + Convert.ToHexStringLower(
            SHA256.HashData(combined));
    }

    public static PaperCertificationRelease ReadRelease(
        ReadOnlySpan<byte> bytes)
    {
        PaperCertificationRelease release =
            PaperResearchInputJson.DeserializeStrict<
                PaperCertificationRelease>(bytes);
        Validate(release);
        byte[] canonical = CanonicalJson.Serialize(release);
        if (!canonical.AsSpan().SequenceEqual(bytes))
        {
            throw new InvalidDataException(
                "Paper certification release bytes are not canonical JSON.");
        }
        return release;
    }

    public static PaperCertificationWaitRegistration RegisterWait(
        string durableRoot,
        string waitRef,
        string cursorPath,
        string releaseCursorDirectory)
    {
        RequireDigest(waitRef, nameof(waitRef));
        var store = new PaperResearchInputStore(durableRoot);
        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(waitRef);
        PaperFormalizationDecision decision =
            store.Get<PaperFormalizationDecision>(wait.DecisionRef);
        PaperFormalizationOutcomeService.Validate(wait, decision);

        string fullCursorPath = Path.GetFullPath(cursorPath);
        bool replayed = WriteWaitCursor(
            fullCursorPath,
            waitRef);
        IReadOnlyList<string> releaseRefs =
            ReadReleaseRefs(releaseCursorDirectory);

        return new PaperCertificationWaitRegistration(
            waitRef,
            fullCursorPath,
            releaseRefs,
            replayed);
    }

    public static PaperCertificationReleaseRegistration RegisterRelease(
        string durableRoot,
        ReadOnlySpan<byte> releaseBytes,
        string cursorPath,
        string waitCursorDirectory)
    {
        PaperCertificationRelease release = ReadRelease(releaseBytes);
        string releaseRef =
            PaperResearchInputStore.Reference(releaseBytes);
        var store = new PaperResearchInputStore(durableRoot);
        string storedRef = store.Put(release);
        if (!string.Equals(
                storedRef,
                releaseRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Certification release canonical bytes changed during storage.");
        }

        string fullCursorPath = Path.GetFullPath(cursorPath);
        bool replayed = WriteReleaseCursor(
            fullCursorPath,
            releaseRef,
            release.ReleaseDigest);
        IReadOnlyList<string> waitRefs =
            ReadWaitRefs(waitCursorDirectory);

        return new PaperCertificationReleaseRegistration(
            releaseRef,
            release.ReleaseDigest,
            fullCursorPath,
            waitRefs,
            replayed);
    }

    public static PaperCertificationEvaluationRegistration Evaluate(
        string durableRoot,
        string waitRef,
        string releaseRef,
        string cursorPath,
        string resolutionCursorPath)
    {
        RequireDigest(waitRef, nameof(waitRef));
        RequireDigest(releaseRef, nameof(releaseRef));
        var store = new PaperResearchInputStore(durableRoot);

        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(waitRef);
        PaperFormalizationDecision decision =
            store.Get<PaperFormalizationDecision>(wait.DecisionRef);
        PaperFormalizationOutcomeService.Validate(wait, decision);

        PaperCertificationRelease release =
            store.Get<PaperCertificationRelease>(releaseRef);
        Validate(release);

        EvaluationMaterial material =
            EvaluateMaterial(wait, release);
        string? mismatchRef = null;
        string? certifiedClaimRef = null;

        if (material.Outcome == PaperCertificationOutcomes.Mismatch)
        {
            var mismatch = new PaperCertificationMismatch(
                PaperCertificationSchemas.Mismatch,
                waitRef,
                releaseRef,
                wait.FormalizationRequestRef,
                wait.Gid,
                material.Reason,
                material.Expected,
                material.Observed,
                PendingCertification);
            Validate(mismatch);
            mismatchRef = store.Put(mismatch);
        }
        else if (material.Outcome == PaperCertificationOutcomes.Certified)
        {
            PaperCertificationDeclaration declaration =
                material.Declaration
                ?? throw new InvalidOperationException(
                    "Certified evaluation is missing a declaration.");
            var claim = new PaperCertifiedClaim(
                PaperCertificationSchemas.CertifiedClaim,
                waitRef,
                wait.DecisionRef,
                wait.ResultRef,
                wait.DispatchRef,
                wait.FormalizationRequestRef,
                wait.SelectionRef,
                wait.PaperResearchInputRef,
                wait.IntuitionProposalRef,
                wait.CandidatePaperRef,
                wait.LiteratureResearchRef,
                wait.VerificationBudgetRef,
                releaseRef,
                release.ReleaseDigest,
                release.SourceRepo,
                release.SourceCommit,
                release.SourceTree,
                wait.PaperId,
                wait.ResearchCandidateId,
                wait.Gid,
                declaration.LeanDeclaration,
                declaration.Kind,
                wait.ExpectedStatement,
                declaration.RequestedStatementDigest,
                declaration.StatementId,
                declaration.StatementCorrespondence,
                declaration.AxiomClosure.ToArray(),
                Certified);
            Validate(claim, wait, release, declaration);
            certifiedClaimRef = store.Put(claim);
            WriteResolutionCursor(
                Path.GetFullPath(resolutionCursorPath),
                waitRef,
                certifiedClaimRef);
        }

        var evaluation = new PaperCertificationEvaluation(
            PaperCertificationSchemas.Evaluation,
            waitRef,
            releaseRef,
            wait.BaseTruthReleaseDigest,
            release.ReleaseDigest,
            wait.Gid,
            material.Outcome,
            material.Reason,
            material.Outcome == PaperCertificationOutcomes.Certified
                ? Certified
                : PendingCertification,
            certifiedClaimRef,
            mismatchRef);
        Validate(evaluation);
        string evaluationRef = store.Put(evaluation);

        string fullCursorPath = Path.GetFullPath(cursorPath);
        bool replayed = WriteEvaluationCursor(
            fullCursorPath,
            waitRef,
            releaseRef,
            evaluationRef);

        return new PaperCertificationEvaluationRegistration(
            evaluationRef,
            waitRef,
            releaseRef,
            evaluation.Outcome,
            evaluation.Reason,
            evaluation.ClaimStatus,
            evaluation.CertifiedClaimRef,
            evaluation.MismatchRef,
            fullCursorPath,
            replayed);
    }

    public static void Validate(PaperCertificationRelease value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperCertificationSchemas.ReleaseObservation);
        RequireDigest(value.ReleaseDigest, nameof(value.ReleaseDigest));
        RequireDigest(value.BundleRef, nameof(value.BundleRef));
        RequireDigest(value.PublicationRef, nameof(value.PublicationRef));
        if (value.BundleRef != value.ReleaseDigest)
        {
            throw new InvalidDataException(
                "Certification release bundle_ref must equal release_digest.");
        }
        RequireSource(
            value.SourceRepo,
            value.SourceCommit,
            value.SourceTree);
        RequireStrictDigestList(
            value.AncestorReleaseDigests,
            nameof(value.AncestorReleaseDigests));
        if (value.AncestorReleaseDigests.Contains(
                value.ReleaseDigest,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "A certification release cannot list itself as an ancestor.");
        }

        ArgumentNullException.ThrowIfNull(value.Declarations);
        string? previousGid = null;
        foreach (PaperCertificationDeclaration declaration
            in value.Declarations)
        {
            Validate(declaration);
            if (previousGid is not null
                && string.CompareOrdinal(
                    previousGid,
                    declaration.Gid) >= 0)
            {
                throw new InvalidDataException(
                    "Certification declarations must be sorted and unique by GID.");
            }
            previousGid = declaration.Gid;
        }

        ArgumentNullException.ThrowIfNull(value.Producer);
        if (value.Producer.Service != ProducerService)
        {
            throw new InvalidDataException(
                "Certification release producer service is unsupported.");
        }
        RequireGitSha1(
            value.Producer.Commit,
            nameof(value.Producer.Commit));
    }

    public static void Validate(PaperCertificationDeclaration value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireGid(value.Gid, nameof(value.Gid));
        RequireText(
            value.LeanDeclaration,
            nameof(value.LeanDeclaration),
            1024);
        RequireText(value.Kind, nameof(value.Kind), 64);
        RequireDigest(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        RequireDigest(
            value.RequestedStatementDigest,
            nameof(value.RequestedStatementDigest));
        RequireDigest(value.StatementId, nameof(value.StatementId));
        if (value.StatementCorrespondence
            is not ("exact" or "mismatch"))
        {
            throw new InvalidDataException(
                "statement_correspondence must be exact or mismatch.");
        }
        RequireStrictTextList(
            value.AxiomClosure,
            nameof(value.AxiomClosure),
            1024);
    }

    public static void Validate(PaperCertificationEvaluation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperCertificationSchemas.Evaluation);
        RequireDigest(
            value.CertificationWaitRef,
            nameof(value.CertificationWaitRef));
        RequireDigest(value.ReleaseRef, nameof(value.ReleaseRef));
        RequireDigest(
            value.BaseTruthReleaseDigest,
            nameof(value.BaseTruthReleaseDigest));
        RequireDigest(
            value.ObservedReleaseDigest,
            nameof(value.ObservedReleaseDigest));
        RequireGid(value.Gid, nameof(value.Gid));
        if (value.Outcome is not (
            PaperCertificationOutcomes.StillPending
            or PaperCertificationOutcomes.Mismatch
            or PaperCertificationOutcomes.Certified))
        {
            throw new InvalidDataException(
                "Certification evaluation outcome is unsupported.");
        }
        RequireText(value.Reason, nameof(value.Reason), 128);
        if (value.ClaimStatus
            is not (PendingCertification or Certified))
        {
            throw new InvalidDataException(
                "Certification evaluation claim_status is unsupported.");
        }

        bool hasClaim = value.CertifiedClaimRef is not null;
        bool hasMismatch = value.MismatchRef is not null;
        if (hasClaim)
        {
            RequireDigest(
                value.CertifiedClaimRef!,
                nameof(value.CertifiedClaimRef));
        }
        if (hasMismatch)
        {
            RequireDigest(
                value.MismatchRef!,
                nameof(value.MismatchRef));
        }

        if (value.Outcome == PaperCertificationOutcomes.Certified)
        {
            if (!hasClaim
                || hasMismatch
                || value.ClaimStatus != Certified
                || value.Reason
                    != PaperCertificationReasons.ExactCertification)
            {
                throw new InvalidDataException(
                    "A certified evaluation must carry only a certified claim.");
            }
        }
        else if (value.Outcome
            == PaperCertificationOutcomes.Mismatch)
        {
            if (!hasMismatch
                || hasClaim
                || value.ClaimStatus != PendingCertification)
            {
                throw new InvalidDataException(
                    "A mismatch evaluation must carry only mismatch evidence.");
            }
        }
        else if (hasClaim
            || hasMismatch
            || value.ClaimStatus != PendingCertification)
        {
            throw new InvalidDataException(
                "A still-pending evaluation cannot carry terminal evidence.");
        }
    }

    public static void Validate(PaperCertificationMismatch value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperCertificationSchemas.Mismatch);
        RequireDigest(
            value.CertificationWaitRef,
            nameof(value.CertificationWaitRef));
        RequireDigest(value.ReleaseRef, nameof(value.ReleaseRef));
        RequireDigest(
            value.FormalizationRequestRef,
            nameof(value.FormalizationRequestRef));
        RequireGid(value.Gid, nameof(value.Gid));
        RequireText(value.Reason, nameof(value.Reason), 128);
        RequireText(value.Expected, nameof(value.Expected), 16384);
        RequireText(value.Observed, nameof(value.Observed), 16384);
        if (value.ClaimStatus != PendingCertification)
        {
            throw new InvalidDataException(
                "Certification mismatch cannot change claim eligibility.");
        }
    }

    public static void Validate(
        PaperCertifiedClaim value,
        PaperCertificationWait wait,
        PaperCertificationRelease release,
        PaperCertificationDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(declaration);
        RequireSchema(
            value.Schema,
            PaperCertificationSchemas.CertifiedClaim);

        foreach (string digest in new[]
        {
            value.CertificationWaitRef,
            value.DecisionRef,
            value.FormalizationResultRef,
            value.DispatchRef,
            value.FormalizationRequestRef,
            value.SelectionRef,
            value.PaperResearchInputRef,
            value.IntuitionProposalRef,
            value.CandidatePaperRef,
            value.LiteratureResearchRef,
            value.VerificationBudgetRef,
            value.CertifyingReleaseRef,
            value.CertifyingReleaseDigest,
            value.RequestedStatementDigest,
            value.StatementId
        })
        {
            RequireDigest(digest, nameof(value));
        }

        RequireSource(
            value.SourceRepo,
            value.SourceCommit,
            value.SourceTree);
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireText(
            value.ResearchCandidateId,
            nameof(value.ResearchCandidateId),
            512);
        RequireGid(value.Gid, nameof(value.Gid));
        RequireText(
            value.LeanDeclaration,
            nameof(value.LeanDeclaration),
            1024);
        RequireText(
            value.DeclarationKind,
            nameof(value.DeclarationKind),
            64);
        RequireText(
            value.ExpectedStatement,
            nameof(value.ExpectedStatement),
            16384);
        if (value.StatementCorrespondence != "exact")
        {
            throw new InvalidDataException(
                "A certified claim requires exact statement correspondence.");
        }
        RequireStrictTextList(
            value.AxiomClosure,
            nameof(value.AxiomClosure),
            1024);
        RequireAllowedAxioms(value.AxiomClosure);
        if (value.ClaimStatus != Certified)
        {
            throw new InvalidDataException(
                "Certified claim status must be certified.");
        }

        string expectedWaitRef =
            PaperResearchInputStore.Reference(
                CanonicalJson.Serialize(wait));
        string expectedReleaseRef =
            PaperResearchInputStore.Reference(
                CanonicalJson.Serialize(release));
        if (value.CertificationWaitRef != expectedWaitRef
            || value.DecisionRef != wait.DecisionRef
            || value.FormalizationResultRef != wait.ResultRef
            || value.DispatchRef != wait.DispatchRef
            || value.FormalizationRequestRef
                != wait.FormalizationRequestRef
            || value.SelectionRef != wait.SelectionRef
            || value.PaperResearchInputRef
                != wait.PaperResearchInputRef
            || value.IntuitionProposalRef
                != wait.IntuitionProposalRef
            || value.CandidatePaperRef != wait.CandidatePaperRef
            || value.LiteratureResearchRef
                != wait.LiteratureResearchRef
            || value.VerificationBudgetRef
                != wait.VerificationBudgetRef
            || value.CertifyingReleaseRef != expectedReleaseRef
            || value.CertifyingReleaseDigest != release.ReleaseDigest
            || value.SourceRepo != release.SourceRepo
            || value.SourceCommit != release.SourceCommit
            || value.SourceTree != release.SourceTree
            || value.PaperId != wait.PaperId
            || value.ResearchCandidateId
                != wait.ResearchCandidateId
            || value.Gid != wait.Gid
            || value.LeanDeclaration != declaration.LeanDeclaration
            || value.DeclarationKind != declaration.Kind
            || value.ExpectedStatement != wait.ExpectedStatement
            || value.RequestedStatementDigest
                != declaration.RequestedStatementDigest
            || value.StatementId != declaration.StatementId
            || value.StatementCorrespondence
                != declaration.StatementCorrespondence
            || !value.AxiomClosure.SequenceEqual(
                declaration.AxiomClosure,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Certified claim is not exactly bound to the wait and release.");
        }
    }

    private static EvaluationMaterial EvaluateMaterial(
        PaperCertificationWait wait,
        PaperCertificationRelease release)
    {
        if (release.SourceRepo != wait.SourceRepo)
        {
            return Mismatch(
                PaperCertificationReasons.ReleaseLineageMismatch,
                wait.SourceRepo,
                release.SourceRepo);
        }

        if (release.ReleaseDigest == wait.BaseTruthReleaseDigest)
        {
            return Pending(PaperCertificationReasons.SameRelease);
        }

        if (!release.AncestorReleaseDigests.Contains(
                wait.BaseTruthReleaseDigest,
                StringComparer.Ordinal))
        {
            return Mismatch(
                PaperCertificationReasons.ReleaseLineageMismatch,
                wait.BaseTruthReleaseDigest,
                string.Join(",", release.AncestorReleaseDigests));
        }

        PaperCertificationDeclaration? declaration =
            release.Declarations.SingleOrDefault(value =>
                string.Equals(
                    value.Gid,
                    wait.Gid,
                    StringComparison.Ordinal));
        if (declaration is null)
        {
            return Pending(
                PaperCertificationReasons.DeclarationAbsent);
        }

        if (declaration.FormalizationRequestRef
            != wait.FormalizationRequestRef)
        {
            return Mismatch(
                PaperCertificationReasons.RequestMismatch,
                wait.FormalizationRequestRef,
                declaration.FormalizationRequestRef,
                declaration);
        }

        string expectedStatementDigest =
            RequestedStatementDigest(wait.ExpectedStatement);
        if (declaration.RequestedStatementDigest
            != expectedStatementDigest)
        {
            return Mismatch(
                PaperCertificationReasons.RequestedStatementMismatch,
                expectedStatementDigest,
                declaration.RequestedStatementDigest,
                declaration);
        }

        if (declaration.StatementCorrespondence != "exact")
        {
            return Mismatch(
                PaperCertificationReasons.StatementCorrespondenceMismatch,
                "exact",
                declaration.StatementCorrespondence,
                declaration);
        }

        if (!EligibleDeclarationKinds.Contains(declaration.Kind))
        {
            return Mismatch(
                PaperCertificationReasons.DeclarationKindIneligible,
                string.Join(",", EligibleDeclarationKinds
                    .Order(StringComparer.Ordinal)),
                declaration.Kind,
                declaration);
        }

        string[] forbiddenAxioms =
            declaration.AxiomClosure
                .Where(axiom => !AllowedAxioms.Contains(axiom))
                .ToArray();
        if (forbiddenAxioms.Length > 0)
        {
            return Mismatch(
                PaperCertificationReasons.AxiomPolicyMismatch,
                string.Join(",", AllowedAxioms
                    .Order(StringComparer.Ordinal)),
                string.Join(",", forbiddenAxioms),
                declaration);
        }

        return new EvaluationMaterial(
            PaperCertificationOutcomes.Certified,
            PaperCertificationReasons.ExactCertification,
            string.Empty,
            string.Empty,
            declaration);
    }

    private static EvaluationMaterial Pending(string reason) =>
        new(
            PaperCertificationOutcomes.StillPending,
            reason,
            string.Empty,
            string.Empty,
            null);

    private static EvaluationMaterial Mismatch(
        string reason,
        string expected,
        string observed,
        PaperCertificationDeclaration? declaration = null) =>
        new(
            PaperCertificationOutcomes.Mismatch,
            reason,
            expected,
            observed,
            declaration);

    private static bool WriteWaitCursor(
        string path,
        string waitRef)
    {
        var cursor = new PaperCertificationWaitCursor(
            PaperCertificationSchemas.WaitCursor,
            waitRef);
        return WriteOrReplay(
            path,
            cursor,
            current =>
                current.CertificationWaitRef == waitRef,
            "One certification-wait cursor cannot be rebound.");
    }

    private static bool WriteReleaseCursor(
        string path,
        string releaseRef,
        string releaseDigest)
    {
        var cursor = new PaperCertificationReleaseCursor(
            PaperCertificationSchemas.ReleaseCursor,
            releaseRef,
            releaseDigest);
        return WriteOrReplay(
            path,
            cursor,
            current =>
                current.ReleaseRef == releaseRef
                && current.ReleaseDigest == releaseDigest,
            "One certification-release cursor cannot be rebound.");
    }

    private static bool WriteEvaluationCursor(
        string path,
        string waitRef,
        string releaseRef,
        string evaluationRef)
    {
        var cursor = new PaperCertificationEvaluationCursor(
            PaperCertificationSchemas.EvaluationCursor,
            waitRef,
            releaseRef,
            evaluationRef);
        return WriteOrReplay(
            path,
            cursor,
            current =>
                current.CertificationWaitRef == waitRef
                && current.ReleaseRef == releaseRef
                && current.EvaluationRef == evaluationRef,
            "One wait/release pair cannot be rebound to another evaluation.");
    }

    private static void WriteResolutionCursor(
        string path,
        string waitRef,
        string certifiedClaimRef)
    {
        var cursor = new PaperCertificationResolutionCursor(
            PaperCertificationSchemas.ResolutionCursor,
            waitRef,
            certifiedClaimRef);
        _ = WriteOrReplay(
            path,
            cursor,
            current =>
                current.CertificationWaitRef == waitRef
                && current.CertifiedClaimRef == certifiedClaimRef,
            "One certification wait cannot resolve to multiple certified claims.");
    }

    private static bool WriteOrReplay<T>(
        string path,
        T value,
        Func<T, bool> same,
        string mismatchMessage)
    {
        byte[] bytes = CanonicalJson.Serialize(value);
        if (File.Exists(path))
        {
            T current = PaperResearchInputJson.DeserializeStrict<T>(
                File.ReadAllBytes(path));
            if (!same(current))
            {
                throw new InvalidDataException(mismatchMessage);
            }
            return true;
        }
        PaperResearchInputStore.WriteAtomic(
            path,
            bytes,
            overwrite: false);
        return false;
    }

    private static IReadOnlyList<string> ReadReleaseRefs(
        string directory)
    {
        string full = Path.GetFullPath(directory);
        if (!Directory.Exists(full))
        {
            return Array.Empty<string>();
        }
        return Directory.GetFiles(
                full,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                PaperCertificationReleaseCursor cursor =
                    PaperResearchInputJson.DeserializeStrict<
                        PaperCertificationReleaseCursor>(
                            File.ReadAllBytes(path));
                RequireSchema(
                    cursor.Schema,
                    PaperCertificationSchemas.ReleaseCursor);
                RequireDigest(
                    cursor.ReleaseRef,
                    nameof(cursor.ReleaseRef));
                RequireDigest(
                    cursor.ReleaseDigest,
                    nameof(cursor.ReleaseDigest));
                return cursor.ReleaseRef;
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadWaitRefs(
        string directory)
    {
        string full = Path.GetFullPath(directory);
        if (!Directory.Exists(full))
        {
            return Array.Empty<string>();
        }
        return Directory.GetFiles(
                full,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                PaperCertificationWaitCursor cursor =
                    PaperResearchInputJson.DeserializeStrict<
                        PaperCertificationWaitCursor>(
                            File.ReadAllBytes(path));
                RequireSchema(
                    cursor.Schema,
                    PaperCertificationSchemas.WaitCursor);
                RequireDigest(
                    cursor.CertificationWaitRef,
                    nameof(cursor.CertificationWaitRef));
                return cursor.CertificationWaitRef;
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void RequireSource(
        string sourceRepo,
        string sourceCommit,
        string sourceTree)
    {
        if (sourceRepo
            != PaperResearchSelectionService.TruthSourceRepository)
        {
            throw new InvalidDataException(
                "Certification release targets an unexpected truth repository.");
        }
        RequireGitSha1(sourceCommit, nameof(sourceCommit));
        RequireGitSha1(sourceTree, nameof(sourceTree));
    }

    private static void RequireAllowedAxioms(
        IReadOnlyList<string> axioms)
    {
        string? forbidden = axioms.FirstOrDefault(
            axiom => !AllowedAxioms.Contains(axiom));
        if (forbidden is not null)
        {
            throw new InvalidDataException(
                $"Certified claim uses disallowed axiom '{forbidden}'.");
        }
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
                $"{name} is not a canonical theorem GID.");
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

    private static void RequireStrictDigestList(
        IReadOnlyList<string>? values,
        string name)
    {
        if (values is null)
        {
            throw new InvalidDataException(
                $"{name} must be an array.");
        }
        string? previous = null;
        foreach (string value in values)
        {
            RequireDigest(value, name);
            if (previous is not null
                && string.CompareOrdinal(previous, value) >= 0)
            {
                throw new InvalidDataException(
                    $"{name} must be sorted and unique.");
            }
            previous = value;
        }
    }

    private static void RequireStrictTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximum)
    {
        if (values is null)
        {
            throw new InvalidDataException(
                $"{name} must be an array.");
        }
        string? previous = null;
        foreach (string value in values)
        {
            RequireText(value, name, maximum);
            if (previous is not null
                && string.CompareOrdinal(previous, value) >= 0)
            {
                throw new InvalidDataException(
                    $"{name} must be sorted and unique.");
            }
            previous = value;
        }
    }

    private sealed record EvaluationMaterial(
        string Outcome,
        string Reason,
        string Expected,
        string Observed,
        PaperCertificationDeclaration? Declaration);
}
