using System.Globalization;

namespace Trureturing.Paper.Core;

public static partial class PaperManuscriptAuthoringAgentService
{
    private static readonly IReadOnlyDictionary<string, int> JournalScoreWeights =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["scope"] = 18,
            ["theorem-package"] = 15,
            ["article-type"] = 10,
            ["comparable-paper"] = 10,
            ["format"] = 10,
            ["length"] = 10,
            ["policy"] = 10,
            ["fee"] = 5,
            ["evidence-completeness"] = 7,
            ["evidence-recency"] = 5
        };

    private static PaperJournalVenueScorecard[] ComputeVenueScorecards(
        PaperJournalResearchDossier dossier,
        PaperJournalResearchContext context,
        string computedAt)
    {
        Validate(dossier);
        DateTimeOffset computed = ParseUtc(computedAt, nameof(computedAt));
        DateTimeOffset cutoff = ParseUtc(
            dossier.DossierContent.EvidenceCutoff,
            nameof(dossier.DossierContent.EvidenceCutoff));
        PaperTheoremPackage theoremPackage =
            context.ScientificEditingContext.AuthoringContext.Planning.TheoremPackage;
        PaperTheoryDeepeningService.Validate(theoremPackage);

        string paperText = string.Join(
            " ",
            new[]
            {
                context.SourceEditDraft.Title,
                context.SourceEditDraft.AbstractLatex,
                theoremPackage.TheoremPackageContent.NoveltySummary,
                theoremPackage.TheoremPackageContent.PublicationSignificance
            }.Concat(theoremPackage.TheoremPackageContent.Claims.SelectMany(
                claim => new[] { claim.Title, claim.Statement })));
        HashSet<string> paperTokens = JournalTokens(paperText);
        int abstractWords = CountAbstractWords(context);

        var sourcesByVenue = dossier.DossierContent.Sources
            .GroupBy(source => source.VenueId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);

        var result = new List<PaperJournalVenueScorecard>();
        foreach (PaperJournalVenueEvidence venue in dossier.DossierContent.Venues
            .OrderBy(value => value.VenueId, StringComparer.Ordinal))
        {
            PaperJournalSourceSnapshot[] sources = sourcesByVenue.TryGetValue(
                venue.VenueId,
                out PaperJournalSourceSnapshot[]? found)
                ? found
                : [];

            int scopeFit = venue.ScopeFit switch
            {
                "exact" => 100,
                "strong" => 85,
                "partial" => 50,
                _ => 0
            };
            HashSet<string> scopeTokens = JournalTokens(string.Join(
                " ",
                sources.Where(source => source.Roles.Contains(
                        "official-scope",
                        StringComparer.Ordinal))
                    .Select(source => source.NormalizedText)));
            int scopeOverlap = paperTokens.Intersect(scopeTokens).Count();
            int theoremPackageFit = Math.Min(100, scopeOverlap * 10);
            if (scopeFit >= 85 && theoremPackageFit < 50)
            {
                theoremPackageFit = 50;
            }

            int articleTypeFit = venue.ArticleTypeSupported
                && string.Equals(
                    venue.TargetArticleType,
                    dossier.DossierContent.Policy.DesiredArticleType,
                    StringComparison.Ordinal)
                ? 100
                : 0;

            PaperJournalSourceSnapshot[] comparableSources = sources
                .Where(source => venue.ComparablePaperSourceIds.Contains(
                    source.SourceId,
                    StringComparer.Ordinal))
                .ToArray();
            HashSet<string> comparableTokens = JournalTokens(string.Join(
                " ",
                comparableSources.Select(source => source.NormalizedText)));
            int comparableOverlap = paperTokens.Intersect(comparableTokens).Count();
            int comparablePaperScore = Math.Min(
                100,
                comparableSources.Length * 35 + Math.Min(30, comparableOverlap * 5));

            int formatFeasibility = venue.LatexPolicy switch
            {
                "latex-required" => 100,
                "latex-accepted" => 100,
                "source-upload-accepted" => 80,
                "word-only" => 20,
                _ => 0
            };

            int mainTextScore = LimitScore(
                dossier.DossierContent.ManuscriptWordCount,
                venue.MaximumMainTextWords);
            int abstractScore = LimitScore(abstractWords, venue.MaximumAbstractWords);
            int lengthFeasibility = Math.Min(mainTextScore, abstractScore);

            int policyCompatibility = ComputePolicyCompatibility(venue);
            int feeFeasibility = venue.FeeStatus switch
            {
                "none" => 100,
                "optional" => 90,
                "mandatory-known" => 65,
                _ => 0
            };

            int representedRoles = RequiredJournalSourceRoles.Count(role =>
                sources.Any(source => source.Roles.Contains(role, StringComparer.Ordinal)));
            int evidenceCompleteness = representedRoles == RequiredJournalSourceRoles.Length
                ? 100
                : representedRoles * 100 / RequiredJournalSourceRoles.Length;

            bool allFresh = sources.Length > 0
                && sources.All(source => ParseUtc(
                        source.RetrievedAt,
                        nameof(source.RetrievedAt)) >= cutoff
                    && ParseUtc(source.RetrievedAt, nameof(source.RetrievedAt)) <= computed);
            int evidenceRecency = allFresh ? 100 : 0;

            var blockers = new List<string>();
            if (venue.ClaimedPublicationTier > dossier.DossierContent.Policy.MaximumPublicationTier)
            {
                blockers.Add("publication-tier-below-policy-floor");
            }
            if (scopeFit < 80)
            {
                blockers.Add("scope-fit-insufficient");
            }
            if (theoremPackageFit < 40)
            {
                blockers.Add("theorem-package-fit-insufficient");
            }
            if (articleTypeFit < 100)
            {
                blockers.Add("target-article-type-unsupported");
            }
            if (comparablePaperScore < 35)
            {
                blockers.Add("recent-comparable-paper-evidence-insufficient");
            }
            if (formatFeasibility < 70)
            {
                blockers.Add("source-format-incompatible");
            }
            if (lengthFeasibility < 60)
            {
                blockers.Add("manuscript-length-incompatible");
            }
            if (policyCompatibility < 80)
            {
                blockers.Add("submission-policy-incompatible-or-unknown");
            }
            if (feeFeasibility == 0)
            {
                blockers.Add("fee-status-unresolved");
            }
            if (evidenceCompleteness < 100)
            {
                blockers.Add("required-source-role-missing");
            }
            if (evidenceRecency < 100)
            {
                blockers.Add("journal-evidence-stale");
            }
            blockers.AddRange(venue.Risks
                .Where(risk => risk.StartsWith("blocking:", StringComparison.Ordinal))
                .Select(risk => "agent-declared-" + CanonicalBlocker(risk[9..])));
            string[] normalizedBlockers = blockers
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            int overall = WeightedJournalScore(
                scopeFit,
                theoremPackageFit,
                articleTypeFit,
                comparablePaperScore,
                formatFeasibility,
                lengthFeasibility,
                policyCompatibility,
                feeFeasibility,
                evidenceCompleteness,
                evidenceRecency);
            var content = new PaperJournalVenueScorecardContent(
                dossier.DossierId,
                venue.VenueId,
                venue.JournalName,
                venue.TargetArticleType,
                venue.ClaimedPublicationTier,
                scopeFit,
                theoremPackageFit,
                articleTypeFit,
                comparablePaperScore,
                formatFeasibility,
                lengthFeasibility,
                policyCompatibility,
                feeFeasibility,
                evidenceCompleteness,
                evidenceRecency,
                overall,
                normalizedBlockers.Length == 0,
                normalizedBlockers,
                computedAt);
            var scorecard = new PaperJournalVenueScorecard(
                PaperJournalResearchAgentSchemas.VenueScorecard,
                Reference(CanonicalJson.Serialize(content)),
                content);
            Validate(scorecard);
            result.Add(scorecard);
        }
        return result
            .OrderBy(value => value.ScorecardContent.VenueId, StringComparer.Ordinal)
            .ToArray();
    }

    private static PaperJournalTargetSelection SelectJournalTarget(
        PaperJournalResearchDossier dossier,
        IReadOnlyList<PaperJournalVenueScorecard> scorecards,
        string selectedAt)
    {
        Validate(dossier);
        ParseUtc(selectedAt, nameof(selectedAt));
        ArgumentNullException.ThrowIfNull(scorecards);
        if (scorecards.Count != dossier.DossierContent.Venues.Count)
        {
            throw new InvalidDataException(
                "Journal target selection requires one scorecard for every venue.");
        }
        foreach (PaperJournalVenueScorecard scorecard in scorecards)
        {
            Validate(scorecard);
        }
        PaperJournalVenueScorecard[] ranked = scorecards
            .OrderByDescending(value => value.ScorecardContent.Eligible)
            .ThenBy(value => value.ScorecardContent.PublicationTier)
            .ThenByDescending(value => value.ScorecardContent.OverallScore)
            .ThenByDescending(value => value.ScorecardContent.EvidenceCompletenessScore)
            .ThenByDescending(value => value.ScorecardContent.PolicyCompatibilityScore)
            .ThenBy(value => value.ScorecardContent.JournalName, StringComparer.Ordinal)
            .ThenBy(value => value.ScorecardContent.VenueId, StringComparer.Ordinal)
            .ToArray();
        PaperJournalVenueScorecard winner = ranked.FirstOrDefault(value =>
                value.ScorecardContent.Eligible)
            ?? throw new InvalidDataException(
                "Journal target selection has no eligible Tier 2 or stronger venue.");
        PaperJournalVenueEvidence venue = dossier.DossierContent.Venues.Single(value =>
            string.Equals(
                value.VenueId,
                winner.ScorecardContent.VenueId,
                StringComparison.Ordinal));
        var content = new PaperJournalTargetSelectionContent(
            dossier.DossierId,
            dossier.DossierContent.SourceEditedManuscriptRef,
            dossier.DossierContent.PaperId,
            dossier.DossierContent.TheoryProgramRef,
            dossier.DossierContent.Policy.MaximumPublicationTier,
            venue.VenueId,
            venue.JournalName,
            venue.TargetArticleType,
            venue.ClaimedPublicationTier,
            winner.ScorecardId,
            ranked.Select(value => value.ScorecardId).ToArray(),
            venue.SourceIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            "journal-style-editing",
            selectedAt);
        var selection = new PaperJournalTargetSelection(
            PaperJournalResearchAgentSchemas.TargetSelection,
            Reference(CanonicalJson.Serialize(content)),
            content);
        Validate(selection);
        return selection;
    }

    private static int LimitScore(int actual, int maximum)
    {
        if (maximum <= 0)
        {
            return 0;
        }
        if (actual <= maximum)
        {
            return 100;
        }
        return actual <= maximum + Math.Max(1, maximum / 10) ? 60 : 0;
    }

    private static int ComputePolicyCompatibility(PaperJournalVenueEvidence venue)
    {
        if (venue.DataPolicy == "unknown"
            || venue.CodePolicy == "unknown"
            || venue.PreprintPolicy == "unknown"
            || venue.AiPolicy == "unknown"
            || venue.PeerReviewModel == "unknown"
            || venue.AccessModel == "unknown")
        {
            return 0;
        }
        if (venue.AiPolicy == "prohibited" || venue.PreprintPolicy == "prohibited")
        {
            return 0;
        }
        int score = 100;
        if (venue.AiPolicy == "disclosure-required")
        {
            score -= 5;
        }
        if (venue.PreprintPolicy == "restricted")
        {
            score -= 10;
        }
        return score;
    }

    private static int WeightedJournalScore(
        int scope,
        int theoremPackage,
        int articleType,
        int comparablePaper,
        int format,
        int length,
        int policy,
        int fee,
        int evidenceCompleteness,
        int evidenceRecency)
    {
        int weighted =
            scope * JournalScoreWeights["scope"]
            + theoremPackage * JournalScoreWeights["theorem-package"]
            + articleType * JournalScoreWeights["article-type"]
            + comparablePaper * JournalScoreWeights["comparable-paper"]
            + format * JournalScoreWeights["format"]
            + length * JournalScoreWeights["length"]
            + policy * JournalScoreWeights["policy"]
            + fee * JournalScoreWeights["fee"]
            + evidenceCompleteness * JournalScoreWeights["evidence-completeness"]
            + evidenceRecency * JournalScoreWeights["evidence-recency"];
        return (int)Math.Round(weighted / 100.0, MidpointRounding.AwayFromZero);
    }

    private static string CanonicalBlocker(string value)
    {
        string canonical = string.Join(
            '-',
            value.ToLowerInvariant()
                .Split(
                    [' ', '_', '/', ':'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray()))
                .Where(part => part.Length > 0));
        return string.IsNullOrEmpty(canonical) ? "unspecified-risk" : canonical;
    }
}
