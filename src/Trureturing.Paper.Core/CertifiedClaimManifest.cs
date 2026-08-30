using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperClaimManifestSchemas
{
    public const string ManuscriptPlan = "paper-manuscript-plan.v1";
    public const string CertifiedClaimManifest = "paper-certified-claim-manifest.v1";
    public const string ManuscriptEligibility = "paper-manuscript-eligibility.v1";
    public const string ClaimsPending = "paper-manuscript-claims-pending.v1";
    public const string ClaimsIneligible = "paper-manuscript-claims-ineligible.v1";
    public const string Evaluation = "paper-manuscript-claim-evaluation.v1";
    public const string PlanCursor = "paper-manuscript-plan-cursor.v1";
    public const string EvaluationCursor = "paper-manuscript-claim-evaluation-cursor.v1";
    public const string ResolutionCursor = "paper-manuscript-claim-resolution-cursor.v1";
}

public static class PaperClaimManifestOutcomes
{
    public const string Pending = "pending";
    public const string Ineligible = "ineligible";
    public const string Eligible = "eligible";
}

public static class PaperClaimManifestReasons
{
    public const string MissingEvidence = "missing-evidence";
    public const string PaperIdMismatch = "paper-id-mismatch";
    public const string StatementMismatch = "statement-mismatch";
    public const string DuplicateCertifiedGid = "duplicate-certified-gid";
    public const string DuplicateFormalizationRequest = "duplicate-formalization-request";
    public const string SelectedReleaseLineageMismatch = "selected-release-lineage-mismatch";
    public const string SelectedReleaseDeclarationAbsent = "selected-release-declaration-absent";
    public const string SelectedReleaseDeclarationMismatch = "selected-release-declaration-mismatch";
    public const string SelectedReleaseRequestMismatch = "selected-release-request-mismatch";
    public const string SelectedReleaseStatementMismatch = "selected-release-statement-mismatch";
    public const string SelectedReleaseKindMismatch = "selected-release-kind-mismatch";
    public const string SelectedReleaseAxiomMismatch = "selected-release-axiom-mismatch";
    public const string AllFormalClaimsCertified = "all-formal-claims-certified";
}

public sealed record PaperManuscriptFormalClaim(
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string LatexLabel,
    [property: JsonRequired] string ClaimKind,
    [property: JsonRequired] string CertifiedClaimRef,
    [property: JsonRequired] string Statement,
    [property: JsonRequired] string RoleInArgument);

public sealed record PaperManuscriptInformalItem(
    [property: JsonRequired] string ItemId,
    [property: JsonRequired] string LatexLabel,
    [property: JsonRequired] string ItemKind,
    [property: JsonRequired] string Text,
    [property: JsonRequired] string EpistemicStatus);

public sealed record PaperManuscriptPlan(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string ManuscriptTruthReleaseRef,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptFormalClaim> FormalClaims,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptInformalItem> InformalExposition);

public sealed record PaperCertifiedClaimManifestEntry(
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string LatexLabel,
    [property: JsonRequired] string ClaimKind,
    [property: JsonRequired] string RoleInArgument,
    [property: JsonRequired] string CertifiedClaimRef,
    [property: JsonRequired] string CertificationWaitRef,
    [property: JsonRequired] string FormalizationResultRef,
    [property: JsonRequired] string FormalizationRequestRef,
    [property: JsonRequired] string SelectionRef,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] string LiteratureResearchRef,
    [property: JsonRequired] string Gid,
    [property: JsonRequired] string LeanDeclaration,
    [property: JsonRequired] string Statement,
    [property: JsonRequired] string RequestedStatementDigest,
    [property: JsonRequired] string StatementId,
    [property: JsonRequired] string OriginalCertifyingReleaseRef,
    [property: JsonRequired] string OriginalCertifyingReleaseDigest,
    [property: JsonRequired] IReadOnlyList<string> AxiomClosure,
    [property: JsonRequired] string ProofStatus,
    [property: JsonRequired] string EpistemicStatus);

public sealed record PaperCertifiedClaimManifestInformalEntry(
    [property: JsonRequired] string ItemId,
    [property: JsonRequired] string LatexLabel,
    [property: JsonRequired] string ItemKind,
    [property: JsonRequired] string Text,
    [property: JsonRequired] string TextDigest,
    [property: JsonRequired] string EpistemicStatus);

public sealed record PaperCertifiedClaimManifest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string ManuscriptTruthReleaseRef,
    [property: JsonRequired] string ManuscriptTruthReleaseDigest,
    [property: JsonRequired] string SourceRepo,
    [property: JsonRequired] string SourceCommit,
    [property: JsonRequired] string SourceTree,
    [property: JsonRequired] IReadOnlyList<PaperCertifiedClaimManifestEntry> FormalClaims,
    [property: JsonRequired] IReadOnlyList<PaperCertifiedClaimManifestInformalEntry> InformalExposition,
    [property: JsonRequired] int FormalClaimCount,
    [property: JsonRequired] int InformalItemCount,
    [property: JsonRequired] string ManifestStatus);

public sealed record PaperManuscriptEligibility(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string ClaimManifestRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ManuscriptTruthReleaseRef,
    [property: JsonRequired] string ManuscriptTruthReleaseDigest,
    [property: JsonRequired] int FormalClaimCount,
    [property: JsonRequired] int InformalItemCount,
    [property: JsonRequired] bool FormalClaimsCertified,
    [property: JsonRequired] bool ExactReleaseCoherent,
    [property: JsonRequired] bool EpistemicBoundariesExplicit,
    [property: JsonRequired] string Status);

public sealed record PaperManuscriptClaimsPending(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] IReadOnlyList<string> MissingEvidenceRefs,
    [property: JsonRequired] string Status);

public sealed record PaperManuscriptClaimsIneligible(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string Expected,
    [property: JsonRequired] string Observed,
    [property: JsonRequired] string Status);

public sealed record PaperManuscriptClaimEvaluation(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string EvidenceStateRef,
    [property: JsonRequired] string Outcome,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] string? ClaimManifestRef,
    [property: JsonRequired] string? EligibilityRef,
    [property: JsonRequired] string? PendingRef,
    [property: JsonRequired] string? IneligibilityRef);

public sealed record PaperManuscriptPlanCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ManuscriptTruthReleaseRef);

public sealed record PaperManuscriptClaimEvidencePresence(
    [property: JsonRequired] string Reference,
    [property: JsonRequired] bool Present);

public sealed record PaperManuscriptClaimEvidenceState(
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] PaperManuscriptClaimEvidencePresence ManuscriptTruthRelease,
    [property: JsonRequired] IReadOnlyList<PaperManuscriptClaimEvidencePresence> CertifiedClaims);

public sealed record PaperManuscriptClaimEvaluationCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string EvidenceStateRef,
    [property: JsonRequired] string EvaluationRef);

public sealed record PaperManuscriptClaimResolutionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ManuscriptPlanRef,
    [property: JsonRequired] string Outcome,
    [property: JsonRequired] string EvaluationRef,
    [property: JsonRequired] string? ClaimManifestRef,
    [property: JsonRequired] string? EligibilityRef,
    [property: JsonRequired] string? IneligibilityRef);

public sealed record PaperManuscriptPlanRegistration(
    string ManuscriptPlanRef,
    string PaperId,
    string ManuscriptTruthReleaseRef,
    string CursorPath,
    bool Replayed);

public sealed record PaperManuscriptClaimEvaluationRegistration(
    string EvaluationRef,
    string ManuscriptPlanRef,
    string EvidenceStateRef,
    string Outcome,
    string Reason,
    string? ClaimManifestRef,
    string? EligibilityRef,
    string? PendingRef,
    string? IneligibilityRef,
    string CursorPath,
    bool Replayed);

public static class PaperCertifiedClaimManifestService
{
    public const string Certified = "certified";
    public const string ExplicitlyInformal = "explicitly-informal";
    public const string Conjectured = "conjectured";
    public const string Eligible = "eligible";
    public const string Ineligible = "ineligible";
    public const string PendingCertifiedClaims = "pending-certified-claims";
    public const string Closed = "closed";

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex IdentifierPattern = new(
        "^[A-Za-z][A-Za-z0-9_.:-]{0,127}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LatexLabelPattern = new(
        "^[A-Za-z][A-Za-z0-9_.-]*:[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> FormalClaimKinds =
    [
        "theorem",
        "lemma",
        "corollary"
    ];

    private static readonly HashSet<string> InformalItemKinds =
    [
        "conjecture",
        "definition",
        "example",
        "remark",
        "motivation",
        "discussion",
        "limitation"
    ];

    private static readonly HashSet<string> IneligibilityReasons =
    [
        PaperClaimManifestReasons.PaperIdMismatch,
        PaperClaimManifestReasons.StatementMismatch,
        PaperClaimManifestReasons.DuplicateCertifiedGid,
        PaperClaimManifestReasons.DuplicateFormalizationRequest,
        PaperClaimManifestReasons.SelectedReleaseLineageMismatch,
        PaperClaimManifestReasons.SelectedReleaseDeclarationAbsent,
        PaperClaimManifestReasons.SelectedReleaseDeclarationMismatch,
        PaperClaimManifestReasons.SelectedReleaseRequestMismatch,
        PaperClaimManifestReasons.SelectedReleaseStatementMismatch,
        PaperClaimManifestReasons.SelectedReleaseKindMismatch,
        PaperClaimManifestReasons.SelectedReleaseAxiomMismatch
    ];

    public static PaperManuscriptPlan ReadPlan(ReadOnlySpan<byte> bytes)
    {
        PaperManuscriptPlan plan =
            PaperResearchInputJson.DeserializeStrict<PaperManuscriptPlan>(bytes);
        Validate(plan);
        byte[] canonical = CanonicalJson.Serialize(plan);
        if (!canonical.AsSpan().SequenceEqual(bytes))
        {
            throw new InvalidDataException(
                "Paper manuscript plan bytes are not canonical JSON.");
        }
        return plan;
    }

    public static PaperManuscriptPlanRegistration RegisterPlan(
        string durableRoot,
        ReadOnlySpan<byte> planBytes,
        string cursorPath)
    {
        PaperManuscriptPlan plan = ReadPlan(planBytes);
        string planRef = PaperResearchInputStore.Reference(planBytes);
        var store = new PaperResearchInputStore(durableRoot);
        string storedRef = store.Put(plan);
        if (storedRef != planRef)
        {
            throw new InvalidDataException(
                "Manuscript plan canonical bytes changed during storage.");
        }

        string fullCursorPath = Path.GetFullPath(cursorPath);
        var cursor = new PaperManuscriptPlanCursor(
            PaperClaimManifestSchemas.PlanCursor,
            planRef,
            plan.PaperId,
            plan.ManuscriptTruthReleaseRef);
        bool replayed = WriteOrReplay(
            fullCursorPath,
            cursor,
            current =>
                current.ManuscriptPlanRef == planRef
                && current.PaperId == plan.PaperId
                && current.ManuscriptTruthReleaseRef
                    == plan.ManuscriptTruthReleaseRef,
            "One manuscript-plan cursor cannot be rebound.");

        return new PaperManuscriptPlanRegistration(
            planRef,
            plan.PaperId,
            plan.ManuscriptTruthReleaseRef,
            fullCursorPath,
            replayed);
    }

    public static IReadOnlyList<string> ListPlanRefs(
        string planCursorDirectory)
    {
        string full = Path.GetFullPath(planCursorDirectory);
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
                PaperManuscriptPlanCursor cursor =
                    PaperResearchInputJson.DeserializeStrict<
                        PaperManuscriptPlanCursor>(
                            File.ReadAllBytes(path));
                Validate(cursor);
                return cursor.ManuscriptPlanRef;
            })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static PaperManuscriptClaimEvaluationRegistration Evaluate(
        string durableRoot,
        string planRef,
        string evaluationDirectory,
        string resolutionCursorPath)
    {
        RequireDigest(planRef, nameof(planRef));
        var store = new PaperResearchInputStore(durableRoot);
        PaperManuscriptPlan plan =
            store.Get<PaperManuscriptPlan>(planRef);
        Validate(plan);

        bool releasePresent = TryGet(
            store,
            plan.ManuscriptTruthReleaseRef,
            out PaperCertificationRelease? selectedRelease);

        var claimPresence =
            new List<PaperManuscriptClaimEvidencePresence>(
                plan.FormalClaims.Count);
        var claims =
            new Dictionary<string, PaperCertifiedClaim>(
                StringComparer.Ordinal);
        foreach (PaperManuscriptFormalClaim item in plan.FormalClaims)
        {
            bool present = TryGet(
                store,
                item.CertifiedClaimRef,
                out PaperCertifiedClaim? claim);
            claimPresence.Add(
                new PaperManuscriptClaimEvidencePresence(
                    item.CertifiedClaimRef,
                    present));
            if (present)
            {
                claims.Add(item.CertifiedClaimRef, claim!);
            }
        }

        var evidenceState = new PaperManuscriptClaimEvidenceState(
            planRef,
            new PaperManuscriptClaimEvidencePresence(
                plan.ManuscriptTruthReleaseRef,
                releasePresent),
            claimPresence);
        string evidenceStateRef =
            PaperResearchInputStore.Reference(
                CanonicalJson.Serialize(evidenceState));

        string? manifestRef = null;
        string? eligibilityRef = null;
        string? pendingRef = null;
        string? ineligibilityRef = null;
        string outcome;
        string reason;

        string[] missing = evidenceState.CertifiedClaims
            .Where(static value => !value.Present)
            .Select(static value => value.Reference)
            .Concat(
                releasePresent
                    ? Array.Empty<string>()
                    : new[] { plan.ManuscriptTruthReleaseRef })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            var pending = new PaperManuscriptClaimsPending(
                PaperClaimManifestSchemas.ClaimsPending,
                planRef,
                plan.PaperId,
                missing,
                PendingCertifiedClaims);
            Validate(pending);
            pendingRef = store.Put(pending);
            outcome = PaperClaimManifestOutcomes.Pending;
            reason = PaperClaimManifestReasons.MissingEvidence;
        }
        else
        {
            if (selectedRelease is null)
            {
                throw new InvalidOperationException(
                    "Present manuscript release was not materialized.");
            }
            PaperCertificationService.Validate(selectedRelease);

            IneligibilityMaterial? ineligibility =
                CheckEligibility(
                    store,
                    plan,
                    selectedRelease,
                    claims);
            if (ineligibility is not null)
            {
                var artifact = new PaperManuscriptClaimsIneligible(
                    PaperClaimManifestSchemas.ClaimsIneligible,
                    planRef,
                    plan.PaperId,
                    ineligibility.Reason,
                    ineligibility.ClaimId,
                    ineligibility.Expected,
                    ineligibility.Observed,
                    Ineligible);
                Validate(artifact);
                ineligibilityRef = store.Put(artifact);
                outcome = PaperClaimManifestOutcomes.Ineligible;
                reason = ineligibility.Reason;
            }
            else
            {
                PaperCertifiedClaimManifest manifest =
                    BuildManifest(
                        planRef,
                        plan,
                        selectedRelease,
                        claims);
                Validate(manifest, plan, selectedRelease, claims);
                manifestRef = store.Put(manifest);

                var eligibility = new PaperManuscriptEligibility(
                    PaperClaimManifestSchemas.ManuscriptEligibility,
                    planRef,
                    manifestRef,
                    plan.PaperId,
                    plan.ManuscriptTruthReleaseRef,
                    selectedRelease.ReleaseDigest,
                    manifest.FormalClaimCount,
                    manifest.InformalItemCount,
                    FormalClaimsCertified: true,
                    ExactReleaseCoherent: true,
                    EpistemicBoundariesExplicit: true,
                    Eligible);
                Validate(eligibility, manifest);
                eligibilityRef = store.Put(eligibility);
                outcome = PaperClaimManifestOutcomes.Eligible;
                reason =
                    PaperClaimManifestReasons.AllFormalClaimsCertified;
            }
        }

        var evaluation = new PaperManuscriptClaimEvaluation(
            PaperClaimManifestSchemas.Evaluation,
            planRef,
            evidenceStateRef,
            outcome,
            reason,
            manifestRef,
            eligibilityRef,
            pendingRef,
            ineligibilityRef);
        Validate(evaluation);
        string evaluationRef = store.Put(evaluation);

        string cursorPath = EvaluationCursorPath(
            evaluationDirectory,
            planRef,
            evidenceStateRef);
        bool replayed = WriteEvaluationCursor(
            cursorPath,
            planRef,
            evidenceStateRef,
            evaluationRef);

        if (outcome != PaperClaimManifestOutcomes.Pending)
        {
            WriteResolutionCursor(
                Path.GetFullPath(resolutionCursorPath),
                evaluation);
        }

        return new PaperManuscriptClaimEvaluationRegistration(
            evaluationRef,
            planRef,
            evidenceStateRef,
            outcome,
            reason,
            manifestRef,
            eligibilityRef,
            pendingRef,
            ineligibilityRef,
            cursorPath,
            replayed);
    }

    public static void Validate(PaperManuscriptPlan value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperClaimManifestSchemas.ManuscriptPlan);
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireText(value.Title, nameof(value.Title), 1024);
        RequireDigest(
            value.ManuscriptTruthReleaseRef,
            nameof(value.ManuscriptTruthReleaseRef));

        ArgumentNullException.ThrowIfNull(value.FormalClaims);
        if (value.FormalClaims.Count == 0
            || value.FormalClaims.Count > 512)
        {
            throw new InvalidDataException(
                "A manuscript plan must contain between 1 and 512 formal claims.");
        }

        ArgumentNullException.ThrowIfNull(value.InformalExposition);
        if (value.InformalExposition.Count > 2048)
        {
            throw new InvalidDataException(
                "A manuscript plan cannot contain more than 2048 informal items.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var labels = new HashSet<string>(StringComparer.Ordinal);
        var claimRefs = new HashSet<string>(StringComparer.Ordinal);

        foreach (PaperManuscriptFormalClaim claim in value.FormalClaims)
        {
            ArgumentNullException.ThrowIfNull(claim);
            RequireIdentifier(claim.ClaimId, nameof(claim.ClaimId));
            RequireLatexLabel(
                claim.LatexLabel,
                nameof(claim.LatexLabel));
            if (!FormalClaimKinds.Contains(claim.ClaimKind))
            {
                throw new InvalidDataException(
                    "Formal claim kind must be theorem, lemma, or corollary.");
            }
            RequireFormalLabelPrefix(
                claim.ClaimKind,
                claim.LatexLabel);
            RequireDigest(
                claim.CertifiedClaimRef,
                nameof(claim.CertifiedClaimRef));
            RequireText(
                claim.Statement,
                nameof(claim.Statement),
                16384);
            RequireText(
                claim.RoleInArgument,
                nameof(claim.RoleInArgument),
                8192);
            if (!ids.Add(claim.ClaimId))
            {
                throw new InvalidDataException(
                    "Manuscript item IDs must be unique.");
            }
            if (!labels.Add(claim.LatexLabel))
            {
                throw new InvalidDataException(
                    "Manuscript LaTeX labels must be unique.");
            }
            if (!claimRefs.Add(claim.CertifiedClaimRef))
            {
                throw new InvalidDataException(
                    "A certified claim may appear only once in a manuscript plan.");
            }
        }

        foreach (PaperManuscriptInformalItem item
            in value.InformalExposition)
        {
            ArgumentNullException.ThrowIfNull(item);
            RequireIdentifier(item.ItemId, nameof(item.ItemId));
            RequireLatexLabel(
                item.LatexLabel,
                nameof(item.LatexLabel));
            if (!InformalItemKinds.Contains(item.ItemKind))
            {
                throw new InvalidDataException(
                    "Informal item kind is unsupported.");
            }
            RequireText(item.Text, nameof(item.Text), 32768);
            string expectedStatus =
                item.ItemKind == "conjecture"
                    ? Conjectured
                    : ExplicitlyInformal;
            if (item.EpistemicStatus != expectedStatus)
            {
                throw new InvalidDataException(
                    $"Informal item '{item.ItemId}' must use epistemic_status={expectedStatus}.");
            }
            if (!ids.Add(item.ItemId))
            {
                throw new InvalidDataException(
                    "Manuscript item IDs must be unique.");
            }
            if (!labels.Add(item.LatexLabel))
            {
                throw new InvalidDataException(
                    "Manuscript LaTeX labels must be unique.");
            }
        }
    }

    public static void Validate(PaperManuscriptPlanCursor value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperClaimManifestSchemas.PlanCursor);
        RequireDigest(
            value.ManuscriptPlanRef,
            nameof(value.ManuscriptPlanRef));
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireDigest(
            value.ManuscriptTruthReleaseRef,
            nameof(value.ManuscriptTruthReleaseRef));
    }

    public static void Validate(
        PaperCertifiedClaimManifest value,
        PaperManuscriptPlan plan,
        PaperCertificationRelease selectedRelease,
        IReadOnlyDictionary<string, PaperCertifiedClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selectedRelease);
        ArgumentNullException.ThrowIfNull(claims);
        RequireSchema(
            value.Schema,
            PaperClaimManifestSchemas.CertifiedClaimManifest);
        RequireDigest(
            value.ManuscriptPlanRef,
            nameof(value.ManuscriptPlanRef));
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireText(value.Title, nameof(value.Title), 1024);
        RequireDigest(
            value.ManuscriptTruthReleaseRef,
            nameof(value.ManuscriptTruthReleaseRef));
        RequireDigest(
            value.ManuscriptTruthReleaseDigest,
            nameof(value.ManuscriptTruthReleaseDigest));
        RequireText(value.SourceRepo, nameof(value.SourceRepo), 512);
        RequireGitSha1(value.SourceCommit, nameof(value.SourceCommit));
        RequireGitSha1(value.SourceTree, nameof(value.SourceTree));

        if (value.ManuscriptPlanRef
                != PaperResearchInputStore.Reference(
                    CanonicalJson.Serialize(plan))
            || value.PaperId != plan.PaperId
            || value.Title != plan.Title
            || value.ManuscriptTruthReleaseRef
                != plan.ManuscriptTruthReleaseRef
            || value.ManuscriptTruthReleaseDigest
                != selectedRelease.ReleaseDigest
            || value.SourceRepo != selectedRelease.SourceRepo
            || value.SourceCommit != selectedRelease.SourceCommit
            || value.SourceTree != selectedRelease.SourceTree
            || value.FormalClaimCount != plan.FormalClaims.Count
            || value.InformalItemCount
                != plan.InformalExposition.Count
            || value.ManifestStatus != Closed)
        {
            throw new InvalidDataException(
                "Certified claim manifest is not bound to its plan and selected release.");
        }

        if (value.FormalClaims.Count != plan.FormalClaims.Count
            || value.InformalExposition.Count
                != plan.InformalExposition.Count)
        {
            throw new InvalidDataException(
                "Certified claim manifest counts do not match its arrays.");
        }

        for (int index = 0;
             index < value.FormalClaims.Count;
             index++)
        {
            PaperCertifiedClaimManifestEntry entry =
                value.FormalClaims[index];
            PaperManuscriptFormalClaim planned =
                plan.FormalClaims[index];
            if (!claims.TryGetValue(
                    planned.CertifiedClaimRef,
                    out PaperCertifiedClaim? claim))
            {
                throw new InvalidDataException(
                    "Certified claim manifest references unresolved evidence.");
            }
            Validate(entry, planned, claim);
        }

        for (int index = 0;
             index < value.InformalExposition.Count;
             index++)
        {
            Validate(
                value.InformalExposition[index],
                plan.InformalExposition[index]);
        }
    }

    public static void Validate(
        PaperManuscriptEligibility value,
        PaperCertifiedClaimManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(manifest);
        RequireSchema(
            value.Schema,
            PaperClaimManifestSchemas.ManuscriptEligibility);
        RequireDigest(
            value.ManuscriptPlanRef,
            nameof(value.ManuscriptPlanRef));
        RequireDigest(
            value.ClaimManifestRef,
            nameof(value.ClaimManifestRef));
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireDigest(
            value.ManuscriptTruthReleaseRef,
            nameof(value.ManuscriptTruthReleaseRef));
        RequireDigest(
            value.ManuscriptTruthReleaseDigest,
            nameof(value.ManuscriptTruthReleaseDigest));
        if (value.Status != Eligible
            || !value.FormalClaimsCertified
            || !value.ExactReleaseCoherent
            || !value.EpistemicBoundariesExplicit
            || value.ManuscriptPlanRef
                != manifest.ManuscriptPlanRef
            || value.ClaimManifestRef
                != PaperResearchInputStore.Reference(
                    CanonicalJson.Serialize(manifest))
            || value.PaperId != manifest.PaperId
            || value.ManuscriptTruthReleaseRef
                != manifest.ManuscriptTruthReleaseRef
            || value.ManuscriptTruthReleaseDigest
                != manifest.ManuscriptTruthReleaseDigest
            || value.FormalClaimCount
                != manifest.FormalClaimCount
            || value.InformalItemCount
                != manifest.InformalItemCount)
        {
            throw new InvalidDataException(
                "Manuscript eligibility is not exactly bound to an eligible manifest.");
        }
    }

    public static void Validate(PaperManuscriptClaimsPending value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperClaimManifestSchemas.ClaimsPending);
        RequireDigest(
            value.ManuscriptPlanRef,
            nameof(value.ManuscriptPlanRef));
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        RequireStrictDigestList(
            value.MissingEvidenceRefs,
            nameof(value.MissingEvidenceRefs));
        if (value.MissingEvidenceRefs.Count == 0
            || value.Status != PendingCertifiedClaims)
        {
            throw new InvalidDataException(
                "Pending manuscript claims must list missing evidence.");
        }
    }

    public static void Validate(PaperManuscriptClaimsIneligible value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperClaimManifestSchemas.ClaimsIneligible);
        RequireDigest(
            value.ManuscriptPlanRef,
            nameof(value.ManuscriptPlanRef));
        RequireText(value.PaperId, nameof(value.PaperId), 512);
        if (!IneligibilityReasons.Contains(value.Reason))
        {
            throw new InvalidDataException(
                "Manuscript ineligibility reason is unsupported.");
        }
        RequireIdentifier(value.ClaimId, nameof(value.ClaimId));
        RequireText(value.Expected, nameof(value.Expected), 32768);
        RequireText(value.Observed, nameof(value.Observed), 32768);
        if (value.Status != Ineligible)
        {
            throw new InvalidDataException(
                "Manuscript ineligibility status must be ineligible.");
        }
    }

    public static void Validate(PaperManuscriptClaimEvaluation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSchema(
            value.Schema,
            PaperClaimManifestSchemas.Evaluation);
        RequireDigest(
            value.ManuscriptPlanRef,
            nameof(value.ManuscriptPlanRef));
        RequireDigest(
            value.EvidenceStateRef,
            nameof(value.EvidenceStateRef));

        bool hasManifest = value.ClaimManifestRef is not null;
        bool hasEligibility = value.EligibilityRef is not null;
        bool hasPending = value.PendingRef is not null;
        bool hasIneligibility = value.IneligibilityRef is not null;
        foreach (string? reference in new[]
        {
            value.ClaimManifestRef,
            value.EligibilityRef,
            value.PendingRef,
            value.IneligibilityRef
        })
        {
            if (reference is not null)
            {
                RequireDigest(reference, nameof(value));
            }
        }

        switch (value.Outcome)
        {
            case PaperClaimManifestOutcomes.Pending:
                if (value.Reason
                        != PaperClaimManifestReasons.MissingEvidence
                    || !hasPending
                    || hasManifest
                    || hasEligibility
                    || hasIneligibility)
                {
                    throw new InvalidDataException(
                        "Pending evaluation must carry only pending evidence.");
                }
                break;
            case PaperClaimManifestOutcomes.Ineligible:
                if (!IneligibilityReasons.Contains(value.Reason)
                    || !hasIneligibility
                    || hasManifest
                    || hasEligibility
                    || hasPending)
                {
                    throw new InvalidDataException(
                        "Ineligible evaluation must carry only ineligibility evidence.");
                }
                break;
            case PaperClaimManifestOutcomes.Eligible:
                if (value.Reason
                        != PaperClaimManifestReasons
                            .AllFormalClaimsCertified
                    || !hasManifest
                    || !hasEligibility
                    || hasPending
                    || hasIneligibility)
                {
                    throw new InvalidDataException(
                        "Eligible evaluation must carry a manifest and eligibility receipt.");
                }
                break;
            default:
                throw new InvalidDataException(
                    "Manuscript claim evaluation outcome is unsupported.");
        }
    }

    private static PaperCertifiedClaimManifest BuildManifest(
        string planRef,
        PaperManuscriptPlan plan,
        PaperCertificationRelease selectedRelease,
        IReadOnlyDictionary<string, PaperCertifiedClaim> claims)
    {
        var formal = new List<PaperCertifiedClaimManifestEntry>(
            plan.FormalClaims.Count);
        foreach (PaperManuscriptFormalClaim item in plan.FormalClaims)
        {
            PaperCertifiedClaim claim =
                claims[item.CertifiedClaimRef];
            formal.Add(new PaperCertifiedClaimManifestEntry(
                item.ClaimId,
                item.LatexLabel,
                item.ClaimKind,
                item.RoleInArgument,
                item.CertifiedClaimRef,
                claim.CertificationWaitRef,
                claim.FormalizationResultRef,
                claim.FormalizationRequestRef,
                claim.SelectionRef,
                claim.PaperResearchInputRef,
                claim.CandidatePaperRef,
                claim.LiteratureResearchRef,
                claim.Gid,
                claim.LeanDeclaration,
                item.Statement,
                claim.RequestedStatementDigest,
                claim.StatementId,
                claim.CertifyingReleaseRef,
                claim.CertifyingReleaseDigest,
                claim.AxiomClosure.ToArray(),
                Certified,
                Certified));
        }

        var informal =
            plan.InformalExposition
                .Select(item =>
                    new PaperCertifiedClaimManifestInformalEntry(
                        item.ItemId,
                        item.LatexLabel,
                        item.ItemKind,
                        item.Text,
                        ExpositionTextDigest(item.Text),
                        item.EpistemicStatus))
                .ToArray();

        return new PaperCertifiedClaimManifest(
            PaperClaimManifestSchemas.CertifiedClaimManifest,
            planRef,
            plan.PaperId,
            plan.Title,
            plan.ManuscriptTruthReleaseRef,
            selectedRelease.ReleaseDigest,
            selectedRelease.SourceRepo,
            selectedRelease.SourceCommit,
            selectedRelease.SourceTree,
            formal,
            informal,
            formal.Count,
            informal.Length,
            Closed);
    }

    private static IneligibilityMaterial? CheckEligibility(
        PaperResearchInputStore store,
        PaperManuscriptPlan plan,
        PaperCertificationRelease selectedRelease,
        IReadOnlyDictionary<string, PaperCertifiedClaim> claims)
    {
        var seenGids = new HashSet<string>(StringComparer.Ordinal);
        var seenRequests = new HashSet<string>(StringComparer.Ordinal);

        foreach (PaperManuscriptFormalClaim item in plan.FormalClaims)
        {
            PaperCertifiedClaim claim =
                claims[item.CertifiedClaimRef];
            ValidateCertifiedClaimClosure(store, claim);

            if (claim.PaperId != plan.PaperId)
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons.PaperIdMismatch,
                    plan.PaperId,
                    claim.PaperId);
            }
            if (claim.ExpectedStatement != item.Statement
                || claim.RequestedStatementDigest
                    != PaperCertificationService
                        .RequestedStatementDigest(item.Statement))
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons.StatementMismatch,
                    claim.ExpectedStatement,
                    item.Statement);
            }
            if (!seenGids.Add(claim.Gid))
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons.DuplicateCertifiedGid,
                    "one manuscript claim per certified GID",
                    claim.Gid);
            }
            if (!seenRequests.Add(
                    claim.FormalizationRequestRef))
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .DuplicateFormalizationRequest,
                    "one manuscript claim per Formalize request",
                    claim.FormalizationRequestRef);
            }

            bool releaseIsSame =
                selectedRelease.ReleaseDigest
                    == claim.CertifyingReleaseDigest;
            bool releaseDescendsFromClaim =
                selectedRelease.AncestorReleaseDigests.Contains(
                    claim.CertifyingReleaseDigest,
                    StringComparer.Ordinal);
            if (!releaseIsSame && !releaseDescendsFromClaim)
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .SelectedReleaseLineageMismatch,
                    claim.CertifyingReleaseDigest,
                    selectedRelease.ReleaseDigest);
            }

            PaperCertificationDeclaration? declaration =
                selectedRelease.Declarations.SingleOrDefault(
                    value => value.Gid == claim.Gid);
            if (declaration is null)
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .SelectedReleaseDeclarationAbsent,
                    claim.Gid,
                    "absent");
            }
            if (declaration.LeanDeclaration
                    != claim.LeanDeclaration)
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .SelectedReleaseDeclarationMismatch,
                    claim.LeanDeclaration,
                    declaration.LeanDeclaration);
            }
            if (declaration.FormalizationRequestRef
                    != claim.FormalizationRequestRef)
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .SelectedReleaseRequestMismatch,
                    claim.FormalizationRequestRef,
                    declaration.FormalizationRequestRef);
            }
            if (declaration.RequestedStatementDigest
                    != claim.RequestedStatementDigest
                || declaration.StatementId != claim.StatementId
                || declaration.StatementCorrespondence != "exact")
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .SelectedReleaseStatementMismatch,
                    claim.StatementId,
                    declaration.StatementId);
            }
            if (declaration.Kind != "theorem")
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .SelectedReleaseKindMismatch,
                    "theorem",
                    declaration.Kind);
            }
            if (!declaration.AxiomClosure.SequenceEqual(
                    claim.AxiomClosure,
                    StringComparer.Ordinal))
            {
                return IneligibleFor(
                    item,
                    PaperClaimManifestReasons
                        .SelectedReleaseAxiomMismatch,
                    string.Join(",", claim.AxiomClosure),
                    string.Join(",", declaration.AxiomClosure));
            }
        }

        return null;
    }

    private static void ValidateCertifiedClaimClosure(
        PaperResearchInputStore store,
        PaperCertifiedClaim claim)
    {
        PaperCertificationWait wait =
            store.Get<PaperCertificationWait>(
                claim.CertificationWaitRef);
        PaperFormalizationDecision decision =
            store.Get<PaperFormalizationDecision>(
                wait.DecisionRef);
        PaperFormalizationOutcomeService.Validate(
            wait,
            decision);

        PaperCertificationRelease release =
            store.Get<PaperCertificationRelease>(
                claim.CertifyingReleaseRef);
        PaperCertificationService.Validate(release);
        PaperCertificationDeclaration declaration =
            release.Declarations.SingleOrDefault(
                value => value.Gid == claim.Gid)
            ?? throw new InvalidDataException(
                "Certified claim has no declaration in its certifying release.");
        PaperCertificationService.Validate(
            claim,
            wait,
            release,
            declaration);
    }

    private static void Validate(
        PaperCertifiedClaimManifestEntry value,
        PaperManuscriptFormalClaim planned,
        PaperCertifiedClaim claim)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.ClaimId != planned.ClaimId
            || value.LatexLabel != planned.LatexLabel
            || value.ClaimKind != planned.ClaimKind
            || value.RoleInArgument != planned.RoleInArgument
            || value.CertifiedClaimRef
                != planned.CertifiedClaimRef
            || value.CertificationWaitRef
                != claim.CertificationWaitRef
            || value.FormalizationResultRef
                != claim.FormalizationResultRef
            || value.FormalizationRequestRef
                != claim.FormalizationRequestRef
            || value.SelectionRef != claim.SelectionRef
            || value.PaperResearchInputRef
                != claim.PaperResearchInputRef
            || value.CandidatePaperRef
                != claim.CandidatePaperRef
            || value.LiteratureResearchRef
                != claim.LiteratureResearchRef
            || value.Gid != claim.Gid
            || value.LeanDeclaration != claim.LeanDeclaration
            || value.Statement != planned.Statement
            || value.RequestedStatementDigest
                != claim.RequestedStatementDigest
            || value.StatementId != claim.StatementId
            || value.OriginalCertifyingReleaseRef
                != claim.CertifyingReleaseRef
            || value.OriginalCertifyingReleaseDigest
                != claim.CertifyingReleaseDigest
            || !value.AxiomClosure.SequenceEqual(
                claim.AxiomClosure,
                StringComparer.Ordinal)
            || value.ProofStatus != Certified
            || value.EpistemicStatus != Certified)
        {
            throw new InvalidDataException(
                "Formal manifest entry is not exactly bound to its plan and certified claim.");
        }
    }

    private static void Validate(
        PaperCertifiedClaimManifestInformalEntry value,
        PaperManuscriptInformalItem planned)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.ItemId != planned.ItemId
            || value.LatexLabel != planned.LatexLabel
            || value.ItemKind != planned.ItemKind
            || value.Text != planned.Text
            || value.TextDigest
                != ExpositionTextDigest(planned.Text)
            || value.EpistemicStatus
                != planned.EpistemicStatus)
        {
            throw new InvalidDataException(
                "Informal manifest entry is not exactly bound to its plan.");
        }
    }

    private static bool TryGet<T>(
        PaperResearchInputStore store,
        string reference,
        out T? value)
        where T : class
    {
        try
        {
            value = store.Get<T>(reference);
            return true;
        }
        catch (FileNotFoundException)
        {
            value = null;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            value = null;
            return false;
        }
    }

    public static string ExpositionTextDigest(string text)
    {
        RequireText(text, nameof(text), 32768);
        byte[] domain = Encoding.UTF8.GetBytes(
            "trureturing:paper-exposition-text:v1\0");
        byte[] material = Encoding.UTF8.GetBytes(text);
        byte[] combined = new byte[domain.Length + material.Length];
        domain.CopyTo(combined, 0);
        material.CopyTo(combined, domain.Length);
        return "sha256:" + Convert.ToHexStringLower(
            SHA256.HashData(combined));
    }

    private static IneligibilityMaterial IneligibleFor(
        PaperManuscriptFormalClaim item,
        string reason,
        string expected,
        string observed) =>
        new(
            reason,
            item.ClaimId,
            Nonempty(expected),
            Nonempty(observed));

    private static string Nonempty(string value) =>
        string.IsNullOrEmpty(value) ? "(empty)" : value;

    private static string EvaluationCursorPath(
        string directory,
        string planRef,
        string evidenceStateRef)
    {
        string full = Path.GetFullPath(directory);
        string planHex = planRef["sha256:".Length..];
        string evidenceHex =
            evidenceStateRef["sha256:".Length..];
        return Path.Combine(
            full,
            planHex,
            evidenceHex + ".json");
    }

    private static bool WriteEvaluationCursor(
        string path,
        string planRef,
        string evidenceStateRef,
        string evaluationRef)
    {
        var cursor = new PaperManuscriptClaimEvaluationCursor(
            PaperClaimManifestSchemas.EvaluationCursor,
            planRef,
            evidenceStateRef,
            evaluationRef);
        return WriteOrReplay(
            path,
            cursor,
            current =>
                current.ManuscriptPlanRef == planRef
                && current.EvidenceStateRef == evidenceStateRef
                && current.EvaluationRef == evaluationRef,
            "One manuscript evidence state cannot be rebound to another evaluation.");
    }

    private static void WriteResolutionCursor(
        string path,
        PaperManuscriptClaimEvaluation evaluation)
    {
        var cursor = new PaperManuscriptClaimResolutionCursor(
            PaperClaimManifestSchemas.ResolutionCursor,
            evaluation.ManuscriptPlanRef,
            evaluation.Outcome,
            PaperResearchInputStore.Reference(
                CanonicalJson.Serialize(evaluation)),
            evaluation.ClaimManifestRef,
            evaluation.EligibilityRef,
            evaluation.IneligibilityRef);
        _ = WriteOrReplay(
            path,
            cursor,
            current =>
                current.ManuscriptPlanRef
                    == cursor.ManuscriptPlanRef
                && current.Outcome == cursor.Outcome
                && current.EvaluationRef
                    == cursor.EvaluationRef
                && current.ClaimManifestRef
                    == cursor.ClaimManifestRef
                && current.EligibilityRef
                    == cursor.EligibilityRef
                && current.IneligibilityRef
                    == cursor.IneligibilityRef,
            "One manuscript plan cannot resolve to multiple terminal claim evaluations.");
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
            T current =
                PaperResearchInputJson.DeserializeStrict<T>(
                    File.ReadAllBytes(path));
            if (!same(current))
            {
                throw new InvalidDataException(
                    mismatchMessage);
            }
            return true;
        }

        PaperResearchInputStore.WriteAtomic(
            path,
            bytes,
            overwrite: false);
        return false;
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
        if (value is null
            || value.Length != 40
            || value.Any(character =>
                character is not ((>= '0' and <= '9')
                    or (>= 'a' and <= 'f'))))
        {
            throw new InvalidDataException(
                $"{name} must be a 40-character lowercase Git object id.");
        }
    }

    private static void RequireIdentifier(
        string value,
        string name)
    {
        if (!IdentifierPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} is not a canonical manuscript identifier.");
        }
    }

    private static void RequireLatexLabel(
        string value,
        string name)
    {
        if (!LatexLabelPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} is not a canonical LaTeX label.");
        }
    }

    private static void RequireFormalLabelPrefix(
        string kind,
        string label)
    {
        string expected = kind switch
        {
            "theorem" => "thm:",
            "lemma" => "lem:",
            "corollary" => "cor:",
            _ => throw new InvalidDataException(
                "Unsupported formal claim kind.")
        };
        if (!label.StartsWith(
                expected,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Formal {kind} label must start with '{expected}'.");
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
                && string.CompareOrdinal(
                    previous,
                    value) >= 0)
            {
                throw new InvalidDataException(
                    $"{name} must be sorted and unique.");
            }
            previous = value;
        }
    }

    private sealed record IneligibilityMaterial(
        string Reason,
        string ClaimId,
        string Expected,
        string Observed);
}
