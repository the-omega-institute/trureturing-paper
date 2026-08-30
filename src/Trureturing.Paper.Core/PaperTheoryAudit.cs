using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperTheoryAuditSchemas
{
    public const string AuditRequest = "paper-theory-audit-request.v1";
    public const string Audit = "paper-theory-audit.v1";
}

public sealed record PaperTheoryAuditRequestContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryAuthorRunRef,
    [property: JsonRequired] int MinimumIndependentOpinions,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] PaperCodexPhaseContract Contract,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperTheoryAuditRequest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] PaperTheoryAuditRequestContent RequestContent);

public sealed record PaperTheoryAuditMetrics(
    [property: JsonRequired] int AbstractionQuality,
    [property: JsonRequired] int TheoremDepth,
    [property: JsonRequired] int LogicalClosure,
    [property: JsonRequired] int ProofPlausibility,
    [property: JsonRequired] int Novelty,
    [property: JsonRequired] int Significance,
    [property: JsonRequired] int FormalizationReadiness,
    [property: JsonRequired] int JournalFloor,
    [property: JsonRequired] int OverlapHygiene);

public sealed record PaperTheoryAuditOpinion(
    [property: JsonRequired] string ReviewerRunRef,
    [property: JsonRequired] string ReviewSessionRef,
    [property: JsonRequired] string ReviewerRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] IReadOnlyList<string> EvidenceRefs,
    [property: JsonRequired] PaperTheoryAuditMetrics Metrics,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] IReadOnlyList<string> Blockers,
    [property: JsonRequired] IReadOnlyList<string> RequiredRevisions,
    [property: JsonRequired] string NoveltyEvidence,
    [property: JsonRequired] IReadOnlyList<string> ProofAudit,
    [property: JsonRequired] IReadOnlyList<string> OverlapFindings,
    [property: JsonRequired] string ReviewedAt);

public sealed record PaperTheoryAuditContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string AuditRequestRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] IReadOnlyList<PaperTheoryAuditOpinion> Opinions,
    [property: JsonRequired] PaperTheoryAuditMetrics AggregateMetrics,
    [property: JsonRequired] string Verdict,
    [property: JsonRequired] bool Passed,
    [property: JsonRequired] IReadOnlyList<string> BlockerLedger,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryAudit(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string AuditId,
    [property: JsonRequired] PaperTheoryAuditContent AuditContent);

public static class PaperTheoryAuditService
{
    public const string FreshContextMode = "fresh-theory-review";
    public const int MinimumIndependentOpinions = 2;

    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ReviewerRoles = new(
        ["mathematical-referee", "novelty-referee", "scope-referee", "formalization-referee"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> OpinionVerdicts = new(
        ["pass", "deepen", "split", "merge", "park", "archive"],
        StringComparer.Ordinal);

    public static PaperTheoryAuditRequest CreateAuditRequest(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoremPackage package,
        string theoryAuthorRunRef,
        string requestedAt)
    {
        ValidateFoundation(program, scope, inventory, package);
        RequireDigest(theoryAuthorRunRef, nameof(theoryAuthorRunRef));
        ParseUtc(requestedAt, nameof(requestedAt));
        if (!string.Equals(
                package.TheoremPackageContent.Maturity,
                "audit-candidate",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an audit-candidate theorem package may request independent theory audit.");
        }

        var contract = new PaperCodexPhaseContract(
            [program.TheoryProgramId, scope.ScopeId, inventory.InventoryId, package.TheoremPackageId],
            [PaperTheoryAuditSchemas.Audit],
            [
                "Review the theorem package from a fresh context with no prior verdict history.",
                "Audit the canonical abstraction, theorem depth, logical closure, and proof plausibility.",
                "Verify the concrete novelty increment against the stated prior-work boundary.",
                "Test publication significance, journal floor, and overlap hygiene.",
                "Assess whether the dependency DAG is ready to be decomposed into a formalization frontier.",
                "Return explicit blockers and one routing verdict: pass, deepen, split, merge, park, or archive."
            ],
            [
                "Do not edit the theorem package or any source artifact.",
                "Do not use previous audit verdicts, review conversations, or pipeline acceptance history.",
                "Do not share a reviewer run or review session with the theory author or another opinion.",
                "Do not pass on prose quality, theorem count, or asserted novelty without theorem-level evidence.",
                "Do not run Lean, dispatch Formalize, certify claims, or assemble a manuscript."
            ],
            [PaperTheoryAuditSchemas.Audit],
            [
                "At least two independent fresh opinions are present.",
                "Every opinion uses exactly the authorized evidence set.",
                "All opinions pass with no blocker and every aggregate metric reaches its calibrated threshold.",
                "Novelty and proof findings cite concrete theorem-package evidence."
            ],
            [
                "A reviewer reuses the theory-author run, another reviewer run, or another review session.",
                "Any opinion depends on prior audit history or unauthorized evidence.",
                "Any load-bearing theorem remains logically open, derivative, overlapping, or below publication floor."
            ]);
        var content = new PaperTheoryAuditRequestContent(
            program.TheoryProgramId,
            scope.ScopeId,
            inventory.InventoryId,
            package.TheoremPackageId,
            program.ProgramContent.PaperId,
            theoryAuthorRunRef,
            MinimumIndependentOpinions,
            FreshContextMode,
            contract,
            requestedAt);
        ValidateContract(contract);
        return new(PaperTheoryAuditSchemas.AuditRequest, Reference(content), content);
    }

    public static PaperTheoryAudit CreateAudit(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoremPackage package,
        PaperTheoryAuditRequest request,
        IReadOnlyList<PaperTheoryAuditOpinion> opinions,
        string createdAt)
    {
        ValidateFoundation(program, scope, inventory, package);
        Validate(request, program, scope, inventory, package);
        ParseUtc(createdAt, nameof(createdAt));
        if (opinions is null
            || opinions.Count < request.RequestContent.MinimumIndependentOpinions)
        {
            throw new InvalidDataException(
                "Theory audit has fewer than the required independent opinions.");
        }

        string[] expectedEvidence =
        [program.TheoryProgramId, scope.ScopeId, inventory.InventoryId, package.TheoremPackageId];
        var reviewerRuns = new HashSet<string>(StringComparer.Ordinal);
        var sessions = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperTheoryAuditOpinion opinion in opinions)
        {
            ValidateOpinion(opinion, expectedEvidence);
            if (string.Equals(
                    opinion.ReviewerRunRef,
                    request.RequestContent.TheoryAuthorRunRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "A theory audit reviewer cannot reuse the theory-author run.");
            }
            if (!reviewerRuns.Add(opinion.ReviewerRunRef))
            {
                throw new InvalidDataException(
                    "Independent theory opinions must use distinct reviewer runs.");
            }
            if (!sessions.Add(opinion.ReviewSessionRef))
            {
                throw new InvalidDataException(
                    "Independent theory opinions must use distinct review sessions.");
            }
        }

        PaperTheoryAuditOpinion[] normalized = opinions
            .OrderBy(opinion => opinion.ReviewerRole, StringComparer.Ordinal)
            .ThenBy(opinion => opinion.ReviewerRunRef, StringComparer.Ordinal)
            .ToArray();
        PaperTheoryAuditMetrics aggregate = AggregateMinimum(normalized);
        string[] blockers = normalized
            .SelectMany(opinion => opinion.Blockers)
            .Concat(normalized.SelectMany(opinion => opinion.RequiredRevisions))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool passed = normalized.All(opinion =>
                string.Equals(opinion.Verdict, "pass", StringComparison.Ordinal))
            && blockers.Length == 0
            && MetricsPass(aggregate);
        string verdict = passed ? "pass" : SelectFailureVerdict(normalized, aggregate);

        var content = new PaperTheoryAuditContent(
            program.TheoryProgramId,
            scope.ScopeId,
            inventory.InventoryId,
            package.TheoremPackageId,
            request.RequestId,
            program.ProgramContent.PaperId,
            normalized,
            aggregate,
            verdict,
            passed,
            blockers,
            createdAt);
        ValidateAuditContent(content, request);
        return new(PaperTheoryAuditSchemas.Audit, Reference(content), content);
    }

    public static void Validate(PaperTheoryAuditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireExact(request.Schema, PaperTheoryAuditSchemas.AuditRequest, "schema");
        PaperTheoryAuditRequestContent c = request.RequestContent
            ?? throw new InvalidDataException("request_content is required.");
        RequireDigest(c.TheoryProgramRef, "theory_program_ref");
        RequireDigest(c.ScopeRef, "scope_ref");
        RequireDigest(c.InventoryRef, "inventory_ref");
        RequireDigest(c.TheoremPackageRef, "theorem_package_ref");
        RequireText(c.PaperId, "paper_id", 512);
        RequireDigest(c.TheoryAuthorRunRef, "theory_author_run_ref");
        if (c.MinimumIndependentOpinions < MinimumIndependentOpinions)
        {
            throw new InvalidDataException(
                "minimum_independent_opinions must be at least two.");
        }
        RequireExact(c.ContextMode, FreshContextMode, "context_mode");
        ValidateContract(c.Contract);
        ParseUtc(c.RequestedAt, "requested_at");
        RequireIdentity(request.RequestId, c, nameof(request.RequestId));
    }

    public static void Validate(
        PaperTheoryAuditRequest request,
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoremPackage package)
    {
        Validate(request);
        if (!string.Equals(request.RequestContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(request.RequestContent.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(request.RequestContent.InventoryRef, inventory.InventoryId, StringComparison.Ordinal)
            || !string.Equals(request.RequestContent.TheoremPackageRef, package.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(request.RequestContent.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory audit request does not address the supplied paper package.");
        }
        RequireSameSet(
            request.RequestContent.Contract.ExactInputRefs,
            [program.TheoryProgramId, scope.ScopeId, inventory.InventoryId, package.TheoremPackageId],
            "audit exact_input_refs");
    }

    public static void Validate(PaperTheoryAudit audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        RequireExact(audit.Schema, PaperTheoryAuditSchemas.Audit, "schema");
        PaperTheoryAuditContent content = audit.AuditContent
            ?? throw new InvalidDataException("audit_content is required.");
        ValidateAuditContent(content, null);
        RequireIdentity(audit.AuditId, content, nameof(audit.AuditId));
    }

    public static bool MetricsPass(PaperTheoryAuditMetrics metrics)
    {
        ValidateMetrics(metrics);
        return metrics.AbstractionQuality >= 8
            && metrics.TheoremDepth >= 8
            && metrics.LogicalClosure >= 8
            && metrics.ProofPlausibility >= 8
            && metrics.Novelty >= 7
            && metrics.Significance >= 7
            && metrics.FormalizationReadiness >= 7
            && metrics.JournalFloor >= 7
            && metrics.OverlapHygiene >= 8;
    }

    private static void ValidateAuditContent(
        PaperTheoryAuditContent content,
        PaperTheoryAuditRequest? request)
    {
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        RequireDigest(content.ScopeRef, "scope_ref");
        RequireDigest(content.InventoryRef, "inventory_ref");
        RequireDigest(content.TheoremPackageRef, "theorem_package_ref");
        RequireDigest(content.AuditRequestRef, "audit_request_ref");
        RequireText(content.PaperId, "paper_id", 512);
        if (content.Opinions is null || content.Opinions.Count < MinimumIndependentOpinions)
        {
            throw new InvalidDataException("Theory audit requires at least two opinions.");
        }
        string[] expectedEvidence =
        [content.TheoryProgramRef, content.ScopeRef, content.InventoryRef, content.TheoremPackageRef];
        var runs = new HashSet<string>(StringComparer.Ordinal);
        var sessions = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperTheoryAuditOpinion opinion in content.Opinions)
        {
            ValidateOpinion(opinion, expectedEvidence);
            if (!runs.Add(opinion.ReviewerRunRef) || !sessions.Add(opinion.ReviewSessionRef))
            {
                throw new InvalidDataException(
                    "Audit opinions must use distinct reviewer runs and sessions.");
            }
            if (request is not null
                && string.Equals(
                    opinion.ReviewerRunRef,
                    request.RequestContent.TheoryAuthorRunRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Audit opinion reused the theory-author run.");
            }
        }
        PaperTheoryAuditMetrics recomputed = AggregateMinimum(content.Opinions);
        if (recomputed != content.AggregateMetrics)
        {
            throw new InvalidDataException(
                "aggregate_metrics must equal the coordinate-wise minimum of independent opinions.");
        }
        string[] recomputedLedger = content.Opinions
            .SelectMany(opinion => opinion.Blockers)
            .Concat(content.Opinions.SelectMany(opinion => opinion.RequiredRevisions))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!recomputedLedger.SequenceEqual(content.BlockerLedger, StringComparer.Ordinal))
        {
            throw new InvalidDataException("blocker_ledger is not the canonical opinion union.");
        }
        bool recomputedPass = content.Opinions.All(opinion =>
                string.Equals(opinion.Verdict, "pass", StringComparison.Ordinal))
            && recomputedLedger.Length == 0
            && MetricsPass(recomputed);
        if (content.Passed != recomputedPass)
        {
            throw new InvalidDataException("passed does not match the independent audit gate.");
        }
        string expectedVerdict = recomputedPass
            ? "pass"
            : SelectFailureVerdict(content.Opinions, recomputed);
        if (!string.Equals(content.Verdict, expectedVerdict, StringComparison.Ordinal))
        {
            throw new InvalidDataException("verdict does not match the independent audit evidence.");
        }
        if (request is not null
            && (!string.Equals(content.AuditRequestRef, request.RequestId, StringComparison.Ordinal)
                || content.Opinions.Count < request.RequestContent.MinimumIndependentOpinions))
        {
            throw new InvalidDataException(
                "Audit changed its request or independent-opinion requirement.");
        }
        ParseUtc(content.CreatedAt, "created_at");
    }

    private static void ValidateOpinion(
        PaperTheoryAuditOpinion opinion,
        IReadOnlyList<string> expectedEvidence)
    {
        ArgumentNullException.ThrowIfNull(opinion);
        RequireDigest(opinion.ReviewerRunRef, "reviewer_run_ref");
        RequireDigest(opinion.ReviewSessionRef, "review_session_ref");
        if (!ReviewerRoles.Contains(opinion.ReviewerRole))
        {
            throw new InvalidDataException(
                $"Unsupported theory-audit reviewer role {opinion.ReviewerRole}.");
        }
        RequireExact(opinion.ContextMode, FreshContextMode, "context_mode");
        RequireSameSet(opinion.EvidenceRefs, expectedEvidence, "evidence_refs");
        ValidateMetrics(opinion.Metrics);
        if (!OpinionVerdicts.Contains(opinion.Verdict))
        {
            throw new InvalidDataException(
                $"Unsupported theory-audit verdict {opinion.Verdict}.");
        }
        RequireTextList(opinion.Blockers, "blockers", 16384, 0);
        RequireTextList(opinion.RequiredRevisions, "required_revisions", 16384, 0);
        RequireText(opinion.NoveltyEvidence, "novelty_evidence", 32768, 80);
        RequireTextList(opinion.ProofAudit, "proof_audit", 16384, 2);
        RequireTextList(opinion.OverlapFindings, "overlap_findings", 16384, 1);
        ParseUtc(opinion.ReviewedAt, "reviewed_at");
        if (string.Equals(opinion.Verdict, "pass", StringComparison.Ordinal)
            && (opinion.Blockers.Count != 0
                || opinion.RequiredRevisions.Count != 0
                || !MetricsPass(opinion.Metrics)))
        {
            throw new InvalidDataException(
                "A pass opinion cannot carry blockers, revisions, or sub-threshold metrics.");
        }
    }

    private static PaperTheoryAuditMetrics AggregateMinimum(
        IReadOnlyList<PaperTheoryAuditOpinion> opinions) =>
        new(
            opinions.Min(opinion => opinion.Metrics.AbstractionQuality),
            opinions.Min(opinion => opinion.Metrics.TheoremDepth),
            opinions.Min(opinion => opinion.Metrics.LogicalClosure),
            opinions.Min(opinion => opinion.Metrics.ProofPlausibility),
            opinions.Min(opinion => opinion.Metrics.Novelty),
            opinions.Min(opinion => opinion.Metrics.Significance),
            opinions.Min(opinion => opinion.Metrics.FormalizationReadiness),
            opinions.Min(opinion => opinion.Metrics.JournalFloor),
            opinions.Min(opinion => opinion.Metrics.OverlapHygiene));

    private static string SelectFailureVerdict(
        IReadOnlyList<PaperTheoryAuditOpinion> opinions,
        PaperTheoryAuditMetrics aggregate)
    {
        string[] precedence = ["archive", "park", "merge", "split", "deepen"];
        foreach (string verdict in precedence)
        {
            if (opinions.Any(opinion =>
                    string.Equals(opinion.Verdict, verdict, StringComparison.Ordinal)))
            {
                return verdict;
            }
        }
        return MetricsPass(aggregate) ? "deepen" : "deepen";
    }

    private static void ValidateFoundation(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoremPackage package)
    {
        PaperPortfolioService.Validate(program);
        PaperTheoryFoundationService.Validate(scope, program);
        PaperTheoryFoundationService.Validate(inventory);
        PaperTheoryDeepeningService.Validate(package);
        if (!string.Equals(inventory.InventoryContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(inventory.InventoryContent.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.InventoryRef, inventory.InventoryId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory audit foundation does not describe one paper program.");
        }
    }

    private static void ValidateMetrics(PaperTheoryAuditMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        int[] values =
        [
            metrics.AbstractionQuality,
            metrics.TheoremDepth,
            metrics.LogicalClosure,
            metrics.ProofPlausibility,
            metrics.Novelty,
            metrics.Significance,
            metrics.FormalizationReadiness,
            metrics.JournalFloor,
            metrics.OverlapHygiene
        ];
        if (values.Any(value => value is < 0 or > 10))
        {
            throw new InvalidDataException("Theory audit metrics must be between zero and ten.");
        }
    }

    private static void ValidateContract(PaperCodexPhaseContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        RequireDigestList(contract.ExactInputRefs, "exact_input_refs", 4);
        RequireTextList(contract.PermittedArtifactFamilies, "permitted_artifact_families", 512, 1);
        RequireTextList(contract.ScientificTasks, "scientific_tasks", 8192, 1);
        RequireTextList(contract.ForbiddenShortcuts, "forbidden_shortcuts", 8192, 1);
        RequireTextList(contract.RequiredOutputSchemas, "required_output_schemas", 512, 1);
        RequireTextList(contract.PassConditions, "pass_conditions", 8192, 1);
        RequireTextList(contract.FailConditions, "fail_conditions", 8192, 1);
    }

    private static void RequireSameSet(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        string name)
    {
        if (actual is null
            || actual.Count != expected.Count
            || !actual.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"{name} changed its exact evidence set.");
        }
    }

    private static void RequireDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireDigest(value, name);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximumLength,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, maximumLength);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireText(
        string value,
        string name,
        int maximumLength,
        int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }

    private static string Reference<T>(T content) =>
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content));

    private static void RequireIdentity<T>(string reference, T content, string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, Reference(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} does not address canonical content bytes.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static DateTimeOffset ParseUtc(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }
}
