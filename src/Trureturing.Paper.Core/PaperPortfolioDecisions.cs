using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperPortfolioDecisionSchemas
{
    public const string Scorecard = "paper-candidate-scorecard.v1";
    public const string Decision = "paper-portfolio-decision.v1";
}

public sealed record PaperCandidateScorecardContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] PaperTheoryAuditMetrics Metrics,
    [property: JsonRequired] int CompositeScore,
    [property: JsonRequired] bool PromotionEligible,
    [property: JsonRequired] string RecommendedAction,
    [property: JsonRequired] IReadOnlyList<string> Risks,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperCandidateScorecard(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ScorecardId,
    [property: JsonRequired] PaperCandidateScorecardContent ScorecardContent);

public sealed record PaperPortfolioDecisionPolicy(
    [property: JsonRequired] int PromotionCapacity,
    [property: JsonRequired] int MinimumComparedPapers);

public sealed record PaperPortfolioPaperDecision(
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] int Rank,
    [property: JsonRequired] int CompositeScore,
    [property: JsonRequired] string Action,
    [property: JsonRequired] string Reason);

public sealed record PaperPortfolioDecisionContent(
    [property: JsonRequired] string PortfolioRef,
    [property: JsonRequired] string CandidateBatchRef,
    [property: JsonRequired] int CycleNumber,
    [property: JsonRequired] PaperPortfolioDecisionPolicy Policy,
    [property: JsonRequired] IReadOnlyList<string> EvaluatedScorecardRefs,
    [property: JsonRequired] IReadOnlyList<PaperPortfolioPaperDecision> Decisions,
    [property: JsonRequired] string DecidedAt);

public sealed record PaperPortfolioDecision(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DecisionId,
    [property: JsonRequired] PaperPortfolioDecisionContent DecisionContent);

public static class PaperPortfolioDecisionService
{
    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ScorecardActions = new(
        ["promote", "continue-deepening", "split", "merge", "park", "archive"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> PortfolioActions = new(
        ["promote-to-frontier", "hold", "continue-deepening", "split", "merge", "park", "archive"],
        StringComparer.Ordinal);

    public static PaperCandidateScorecard CreateScorecard(
        PaperTheoremPackage package,
        PaperTheoryAudit audit,
        string createdAt)
    {
        PaperTheoryDeepeningService.Validate(package);
        PaperTheoryAuditService.Validate(audit);
        ParseUtc(createdAt, nameof(createdAt));
        if (!string.Equals(
                audit.AuditContent.TheoremPackageRef,
                package.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                audit.AuditContent.TheoryProgramRef,
                package.TheoremPackageContent.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(
                audit.AuditContent.PaperId,
                package.TheoremPackageContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Candidate scorecard inputs do not describe one paper package.");
        }

        PaperTheoryAuditMetrics metrics = audit.AuditContent.AggregateMetrics;
        int composite = Composite(metrics);
        bool eligible = audit.AuditContent.Passed
            && string.Equals(
                package.TheoremPackageContent.Maturity,
                "audit-candidate",
                StringComparison.Ordinal);
        string action = eligible
            ? "promote"
            : AuditVerdictToScorecardAction(audit.AuditContent.Verdict);
        string[] risks = audit.AuditContent.BlockerLedger
            .Concat(MetricRisks(metrics))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var content = new PaperCandidateScorecardContent(
            package.TheoremPackageContent.TheoryProgramRef,
            package.TheoremPackageId,
            audit.AuditId,
            package.TheoremPackageContent.PaperId,
            metrics,
            composite,
            eligible,
            action,
            risks,
            createdAt);
        ValidateScorecardContent(content);
        return new(PaperPortfolioDecisionSchemas.Scorecard, Reference(content), content);
    }

    public static PaperPortfolioDecision CreatePortfolioDecision(
        PaperResearchPortfolio portfolio,
        IReadOnlyList<PaperCandidateScorecard> scorecards,
        PaperPortfolioDecisionPolicy policy,
        string decidedAt)
    {
        PaperPortfolioService.Validate(portfolio);
        ArgumentNullException.ThrowIfNull(scorecards);
        ValidatePolicy(policy, portfolio.PortfolioContent.Policy.MaxParallelPapers);
        ParseUtc(decidedAt, nameof(decidedAt));
        if (scorecards.Count < policy.MinimumComparedPapers)
        {
            throw new InvalidDataException(
                "Portfolio competition has fewer than the required compared papers.");
        }

        var states = portfolio.PortfolioContent.CandidateStates.ToDictionary(
            state => state.PaperId,
            StringComparer.Ordinal);
        var papers = new HashSet<string>(StringComparer.Ordinal);
        var programs = new HashSet<string>(StringComparer.Ordinal);
        var refs = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperCandidateScorecard scorecard in scorecards)
        {
            Validate(scorecard);
            PaperCandidateScorecardContent scorecardContent = scorecard.ScorecardContent;
            if (!papers.Add(scorecardContent.PaperId)
                || !programs.Add(scorecardContent.TheoryProgramRef)
                || !refs.Add(scorecard.ScorecardId))
            {
                throw new InvalidDataException(
                    "Portfolio competition requires distinct papers, programs, and scorecards.");
            }
            if (!states.TryGetValue(scorecardContent.PaperId, out PaperCandidateState? state)
                || !string.Equals(
                    state.TheoryProgramRef,
                    scorecardContent.TheoryProgramRef,
                    StringComparison.Ordinal)
                || !string.Equals(state.Phase, "audit-pending", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Every compared scorecard must belong to an audit-pending portfolio paper.");
            }
        }

        PaperCandidateScorecard[] ranked = scorecards
            .OrderByDescending(scorecard => scorecard.ScorecardContent.CompositeScore)
            .ThenBy(scorecard => scorecard.ScorecardContent.PaperId, StringComparer.Ordinal)
            .ToArray();
        int promotionsGranted = 0;
        PaperPortfolioPaperDecision[] decisions = ranked
            .Select((scorecard, index) =>
            {
                PaperCandidateScorecardContent s = scorecard.ScorecardContent;
                string action;
                string reason;
                if (s.PromotionEligible && promotionsGranted < policy.PromotionCapacity)
                {
                    promotionsGranted++;
                    action = "promote-to-frontier";
                    reason = $"rank {index + 1}; passed theory audit within promotion capacity";
                }
                else if (s.PromotionEligible)
                {
                    action = "hold";
                    reason = $"rank {index + 1}; passed theory audit but promotion capacity is exhausted";
                }
                else
                {
                    action = ScorecardToPortfolioAction(s.RecommendedAction);
                    reason = $"rank {index + 1}; audit route={s.RecommendedAction}; risks={s.Risks.Count}";
                }
                return new PaperPortfolioPaperDecision(
                    s.PaperId,
                    s.TheoryProgramRef,
                    scorecard.ScorecardId,
                    index + 1,
                    s.CompositeScore,
                    action,
                    reason);
            })
            .ToArray();
        string[] evaluated = ranked
            .Select(scorecard => scorecard.ScorecardId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var content = new PaperPortfolioDecisionContent(
            portfolio.PortfolioId,
            portfolio.PortfolioContent.CandidateBatchRef,
            portfolio.PortfolioContent.NextCycleNumber,
            policy,
            evaluated,
            decisions,
            decidedAt);
        ValidateDecisionContent(content, portfolio, scorecards);
        return new(PaperPortfolioDecisionSchemas.Decision, Reference(content), content);
    }

    public static PaperCandidateState ApplyDecision(
        PaperCandidateState state,
        PaperPortfolioPaperDecision decision,
        string appliedAt)
    {
        PaperPortfolioService.Validate(state);
        ValidateDecision(decision);
        ParseUtc(appliedAt, nameof(appliedAt));
        if (!string.Equals(state.PaperId, decision.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                state.TheoryProgramRef,
                decision.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(state.Phase, "audit-pending", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio decision does not address this audit-pending paper state.");
        }
        string nextPhase = decision.Action switch
        {
            "promote-to-frontier" => "frontier-pending",
            "hold" => "audit-pending",
            "continue-deepening" => "theory-deepening",
            "split" => "theory-deepening",
            "merge" => "theory-deepening",
            "park" => "parked",
            "archive" => "archived",
            _ => throw new InvalidDataException(
                $"Unsupported portfolio decision action {decision.Action}.")
        };
        bool mathematicalAdvance = string.Equals(
            decision.Action,
            "promote-to-frontier",
            StringComparison.Ordinal);
        return state with
        {
            Phase = nextPhase,
            CompletedCycles = state.CompletedCycles + 1,
            ConsecutiveNoProgressCycles = mathematicalAdvance
                ? 0
                : state.ConsecutiveNoProgressCycles +
                    (string.Equals(decision.Action, "hold", StringComparison.Ordinal) ? 0 : 1),
            LastProgressAt = mathematicalAdvance ? appliedAt : state.LastProgressAt,
            StatusReason = $"portfolio decision {decision.Action}: {decision.Reason}"
        };
    }

    public static void Validate(PaperCandidateScorecard scorecard)
    {
        ArgumentNullException.ThrowIfNull(scorecard);
        RequireExact(scorecard.Schema, PaperPortfolioDecisionSchemas.Scorecard, "schema");
        ValidateScorecardContent(scorecard.ScorecardContent);
        RequireIdentity(
            scorecard.ScorecardId,
            scorecard.ScorecardContent,
            nameof(scorecard.ScorecardId));
    }

    public static void Validate(PaperPortfolioDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        RequireExact(decision.Schema, PaperPortfolioDecisionSchemas.Decision, "schema");
        PaperPortfolioDecisionContent content = decision.DecisionContent
            ?? throw new InvalidDataException("decision_content is required.");
        RequireDigest(content.PortfolioRef, "portfolio_ref");
        RequireDigest(content.CandidateBatchRef, "candidate_batch_ref");
        if (content.CycleNumber < 1)
        {
            throw new InvalidDataException("cycle_number must be positive.");
        }
        ValidatePolicy(content.Policy, 32);
        RequireDigestList(
            content.EvaluatedScorecardRefs,
            "evaluated_scorecard_refs",
            content.Policy.MinimumComparedPapers);
        if (content.Decisions is null
            || content.Decisions.Count != content.EvaluatedScorecardRefs.Count)
        {
            throw new InvalidDataException(
                "Portfolio decisions must cover every evaluated scorecard exactly once.");
        }
        for (int index = 0; index < content.Decisions.Count; index++)
        {
            PaperPortfolioPaperDecision item = content.Decisions[index];
            ValidateDecision(item);
            if (item.Rank != index + 1)
            {
                throw new InvalidDataException("Portfolio decision ranks must be contiguous.");
            }
        }
        ParseUtc(content.DecidedAt, "decided_at");
        RequireIdentity(decision.DecisionId, content, nameof(decision.DecisionId));
    }

    public static int Composite(PaperTheoryAuditMetrics metrics)
    {
        _ = PaperTheoryAuditService.MetricsPass(metrics);
        return metrics.AbstractionQuality
            + (2 * metrics.TheoremDepth)
            + (2 * metrics.LogicalClosure)
            + metrics.ProofPlausibility
            + (2 * metrics.Novelty)
            + (2 * metrics.Significance)
            + metrics.FormalizationReadiness
            + metrics.JournalFloor
            + metrics.OverlapHygiene;
    }

    private static void ValidateScorecardContent(PaperCandidateScorecardContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        RequireDigest(content.TheoremPackageRef, "theorem_package_ref");
        RequireDigest(content.TheoryAuditRef, "theory_audit_ref");
        RequireText(content.PaperId, "paper_id", 512);
        int expectedComposite = Composite(content.Metrics);
        if (content.CompositeScore != expectedComposite)
        {
            throw new InvalidDataException(
                "composite_score does not match calibrated theory metrics.");
        }
        if (!ScorecardActions.Contains(content.RecommendedAction))
        {
            throw new InvalidDataException(
                $"Unsupported scorecard action {content.RecommendedAction}.");
        }
        if (content.PromotionEligible
            != string.Equals(content.RecommendedAction, "promote", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "promotion_eligible and recommended_action are inconsistent.");
        }
        RequireTextList(content.Risks, "risks", 16384, 0);
        ParseUtc(content.CreatedAt, "created_at");
    }

    private static void ValidateDecisionContent(
        PaperPortfolioDecisionContent content,
        PaperResearchPortfolio portfolio,
        IReadOnlyList<PaperCandidateScorecard> scorecards)
    {
        if (!string.Equals(content.PortfolioRef, portfolio.PortfolioId, StringComparison.Ordinal)
            || !string.Equals(
                content.CandidateBatchRef,
                portfolio.PortfolioContent.CandidateBatchRef,
                StringComparison.Ordinal)
            || content.CycleNumber != portfolio.PortfolioContent.NextCycleNumber)
        {
            throw new InvalidDataException(
                "Portfolio decision changed its portfolio, batch, or cycle coordinate.");
        }
        ValidatePolicy(content.Policy, portfolio.PortfolioContent.Policy.MaxParallelPapers);
        string[] expectedRefs = scorecards
            .Select(scorecard => scorecard.ScorecardId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!expectedRefs.SequenceEqual(content.EvaluatedScorecardRefs, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "evaluated_scorecard_refs do not match the compared scorecards.");
        }
        if (content.Decisions.Count != scorecards.Count)
        {
            throw new InvalidDataException(
                "Portfolio decision must cover every compared paper.");
        }
        var byRef = scorecards.ToDictionary(
            scorecard => scorecard.ScorecardId,
            StringComparer.Ordinal);
        int promotionCount = 0;
        for (int index = 0; index < content.Decisions.Count; index++)
        {
            PaperPortfolioPaperDecision decision = content.Decisions[index];
            ValidateDecision(decision);
            if (decision.Rank != index + 1
                || !byRef.TryGetValue(decision.ScorecardRef, out PaperCandidateScorecard? scorecard)
                || !string.Equals(
                    decision.PaperId,
                    scorecard.ScorecardContent.PaperId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    decision.TheoryProgramRef,
                    scorecard.ScorecardContent.TheoryProgramRef,
                    StringComparison.Ordinal)
                || decision.CompositeScore != scorecard.ScorecardContent.CompositeScore)
            {
                throw new InvalidDataException(
                    "Portfolio paper decision does not match its ranked scorecard.");
            }
            if (string.Equals(decision.Action, "promote-to-frontier", StringComparison.Ordinal))
            {
                promotionCount++;
                if (!scorecard.ScorecardContent.PromotionEligible)
                {
                    throw new InvalidDataException(
                        "An ineligible paper cannot be promoted to formalization frontier.");
                }
            }
        }
        if (promotionCount > content.Policy.PromotionCapacity)
        {
            throw new InvalidDataException(
                "Portfolio decision exceeds promotion capacity.");
        }
        ParseUtc(content.DecidedAt, "decided_at");
    }

    private static void ValidateDecision(PaperPortfolioPaperDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        RequireText(decision.PaperId, "paper_id", 512);
        RequireDigest(decision.TheoryProgramRef, "theory_program_ref");
        RequireDigest(decision.ScorecardRef, "scorecard_ref");
        if (decision.Rank < 1
            || decision.CompositeScore is < 0 or > 130
            || !PortfolioActions.Contains(decision.Action))
        {
            throw new InvalidDataException(
                "Portfolio paper decision rank, score, or action is invalid.");
        }
        RequireText(decision.Reason, "reason", 8192);
    }

    private static void ValidatePolicy(
        PaperPortfolioDecisionPolicy policy,
        int maximumParallelPapers)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.PromotionCapacity < 1
            || policy.PromotionCapacity > maximumParallelPapers
            || policy.MinimumComparedPapers < 2
            || policy.MinimumComparedPapers > 32)
        {
            throw new InvalidDataException(
                "Portfolio decision policy is outside its bounded ranges.");
        }
    }

    private static string AuditVerdictToScorecardAction(string verdict) =>
        verdict switch
        {
            "pass" => "promote",
            "deepen" => "continue-deepening",
            "split" => "split",
            "merge" => "merge",
            "park" => "park",
            "archive" => "archive",
            _ => throw new InvalidDataException($"Unsupported theory-audit verdict {verdict}.")
        };

    private static string ScorecardToPortfolioAction(string action) =>
        action switch
        {
            "promote" => "promote-to-frontier",
            "continue-deepening" => "continue-deepening",
            "split" => "split",
            "merge" => "merge",
            "park" => "park",
            "archive" => "archive",
            _ => throw new InvalidDataException($"Unsupported scorecard action {action}.")
        };

    private static IEnumerable<string> MetricRisks(PaperTheoryAuditMetrics metrics)
    {
        if (metrics.AbstractionQuality < 8) yield return "abstraction-quality-below-8";
        if (metrics.TheoremDepth < 8) yield return "theorem-depth-below-8";
        if (metrics.LogicalClosure < 8) yield return "logical-closure-below-8";
        if (metrics.ProofPlausibility < 8) yield return "proof-plausibility-below-8";
        if (metrics.Novelty < 7) yield return "novelty-below-7";
        if (metrics.Significance < 7) yield return "significance-below-7";
        if (metrics.FormalizationReadiness < 7) yield return "formalization-readiness-below-7";
        if (metrics.JournalFloor < 7) yield return "journal-floor-below-7";
        if (metrics.OverlapHygiene < 8) yield return "overlap-hygiene-below-8";
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

    private static void RequireText(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximumLength} characters.");
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
