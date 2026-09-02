using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

internal sealed class JournalResearchTestRepository : IDisposable
{
    public JournalResearchTestRepository()
    {
        Source = new ManuscriptAuthoringTestRepository();
        SourceDraft = Source.BuildValidDraft();
        _ = Source.RecordCompletedDraft(SourceDraft);
        SourceAdmission = PaperManuscriptAuthoringAgentService.AdmitResult(
            Source.Root,
            Source.Staged.TaskRef);

        ScientificStaged = PaperManuscriptAuthoringAgentService.StageScientificEditingTask(
            Source.Root,
            Source.Staged.TaskRef);
        ScientificRegistration = PaperAgentRuntimeService.RegisterTask(
            Source.Root,
            ScientificStaged.TaskPath);
        ScientificPrepared = PaperAgentRuntimeService.PrepareRun(
            Source.Root,
            ScientificStaged.TaskRef);
        ScientificEdit = BuildScientificEdit();
        _ = RecordScientificEdit(ScientificEdit);
        ScientificAdmission = PaperManuscriptAuthoringAgentService.AdmitScientificEditingResult(
            Source.Root,
            ScientificStaged.TaskRef);

        JournalStaged = PaperManuscriptAuthoringAgentService.StageJournalResearchTask(
            Source.Root,
            ScientificStaged.TaskRef);
        JournalRegistration = PaperAgentRuntimeService.RegisterTask(
            Source.Root,
            JournalStaged.TaskPath);
        JournalPrepared = PaperAgentRuntimeService.PrepareRun(
            Source.Root,
            JournalStaged.TaskRef);
    }

    public ManuscriptAuthoringTestRepository Source { get; }
    public string Root => Source.Root;
    public PaperScientificManuscriptDraft SourceDraft { get; }
    public PaperManuscriptAuthoringAgentResultAdmitted SourceAdmission { get; }
    public PaperScientificEditingAgentTaskStaged ScientificStaged { get; }
    public PaperAgentTaskRegistration ScientificRegistration { get; }
    public PaperAgentRunPrepared ScientificPrepared { get; }
    public PaperScientificEditDraft ScientificEdit { get; }
    public PaperScientificEditingAgentResultAdmitted ScientificAdmission { get; }
    public PaperJournalResearchAgentTaskStaged JournalStaged { get; }
    public PaperAgentTaskRegistration JournalRegistration { get; }
    public PaperAgentRunPrepared JournalPrepared { get; }

    public PaperJournalResearchDraft BuildValidJournalDraft()
    {
        var venues = new[]
        {
            Venue(
                "journal-alpha",
                "Journal Alpha",
                "Alpha Mathematical Society",
                "1234-567X",
                "https://journals.example.org/alpha",
                publicationTier: 1,
                scopeFit: "exact",
                feeStatus: "none",
                mandatoryFeeMinorUnits: 0,
                feeCurrency: "none",
                accessModel: "diamond-open-access"),
            Venue(
                "journal-beta",
                "Journal Beta",
                "Beta Press",
                "2345-6789",
                "https://journals.example.org/beta",
                publicationTier: 2,
                scopeFit: "strong",
                feeStatus: "optional",
                mandatoryFeeMinorUnits: 250000,
                feeCurrency: "USD",
                accessModel: "hybrid"),
            Venue(
                "journal-gamma",
                "Journal Gamma",
                "Gamma Publishing",
                "3456-7890",
                "https://journals.example.org/gamma",
                publicationTier: 3,
                scopeFit: "strong",
                feeStatus: "none",
                mandatoryFeeMinorUnits: 0,
                feeCurrency: "none",
                accessModel: "subscription")
        };
        var sources = venues.SelectMany(SourcesForVenue).ToArray();
        return new PaperJournalResearchDraft(
            PaperJournalResearchAgentSchemas.Draft,
            JournalStaged.DispatchRef,
            JournalStaged.SourceEditedManuscriptRef,
            JournalStaged.PaperId,
            JournalStaged.TheoryProgramRef,
            "research-article",
            venues,
            sources,
            [
                "The portfolio includes two independently evidenced Tier 2 or stronger journals whose scope and source-format policies fit the certified theorem package.",
                "The candidates differ in publication tier, access model, and fee structure, allowing deterministic repository selection without an Agent-chosen winner."
            ],
            [
                "Final submission templates can change after this research snapshot and must be refreshed before journal-specific rendering."
            ],
            "2026-08-31T12:50:00Z");
    }

    public PaperAgentResultRecorded RecordCompletedJournalResearch(
        PaperJournalResearchDraft draft,
        string status = "completed",
        string nextRoute = "journal-style-editing",
        string blockerCode = "")
    {
        if (status == "completed")
        {
            string outputPath = Path.Combine(
                JournalPrepared.WorkspacePath,
                "outputs",
                "journal-research-draft.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, CanonicalJson.Serialize(draft));
        }
        PaperAgentTask task = PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
            File.ReadAllBytes(JournalStaged.TaskPath));
        var result = new PaperAgentResultWire(
            PaperAgentSchemas.AgentResult,
            JournalStaged.TaskRef,
            JournalRegistration.PaperId,
            JournalRegistration.TheoryProgramRef,
            JournalRegistration.Phase,
            JournalRegistration.AgentRole,
            JournalRegistration.ContextMode,
            status,
            status == "completed"
                ? "Current source-backed venue research was completed for the edited manuscript."
                : "The journal research worker could not complete an admissible evidence portfolio.",
            status == "completed"
                ? [new PaperAgentOutputWire(
                    PaperJournalResearchAgentSchemas.Draft,
                    "outputs/journal-research-draft.json")]
                : [],
            nextRoute,
            blockerCode,
            task.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
            "2026-08-31T13:00:00Z");
        string stdout = PaperAgentRuntimeService.ResultBegin + "\n"
            + Encoding.UTF8.GetString(CanonicalJson.Serialize(result))
            + "\n" + PaperAgentRuntimeService.ResultEnd + "\n";
        File.WriteAllText(
            JournalPrepared.StdoutPath,
            stdout,
            new UTF8Encoding(false));
        return PaperAgentRuntimeService.RecordResult(
            Root,
            JournalStaged.TaskRef,
            JournalPrepared.StdoutPath,
            "codex-journal-research-test-run",
            "produced");
    }

    public void Dispose() => Source.Dispose();

    private PaperScientificEditDraft BuildScientificEdit()
    {
        PaperManuscriptDraftSection[] sections = SourceDraft.Sections
            .Select(section => section with
            {
                Blocks = section.Blocks.Select(block =>
                    ReviseBlock(section.SectionId, block)).ToArray()
            })
            .ToArray();
        return new PaperScientificEditDraft(
            PaperScientificEditingAgentSchemas.Draft,
            ScientificStaged.DispatchRef,
            ScientificStaged.SourceManuscriptRef,
            SourceAdmission.ClaimManifestRef,
            SourceAdmission.ManuscriptPlanRef,
            SourceAdmission.PaperId,
            SourceAdmission.TheoryProgramRef,
            SourceDraft.Title,
            "We establish a release-coherent obstruction theory for structured descent and organize its contribution around four certified formal claims and one explicitly informal definition. The revision distinguishes imported descent machinery from the paper-specific equivalence, realization, and classification results. It also makes the proof dependency chain, sharpness construction, and certified generality explicit while reserving venue claims for source-backed journal research.",
            SourceDraft.Keywords,
            sections,
            SourceDraft.References,
            [
                "contribution-framing",
                "literature-boundary",
                "proof-exposition",
                "limitations-and-implications"
            ],
            [
                "Reframed the contribution around the exact obstruction equivalence.",
                "Clarified the proof dependency chain and the sharpness role.",
                "Separated certified results from venue-selection questions."
            ],
            [
                "Journal policies and current venue fit remain to be researched from dated sources."
            ],
            "2026-08-31T12:20:00Z");
    }

    private PaperAgentResultRecorded RecordScientificEdit(PaperScientificEditDraft edit)
    {
        string outputPath = Path.Combine(
            ScientificPrepared.WorkspacePath,
            "outputs",
            "scientific-edit-draft.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, CanonicalJson.Serialize(edit));
        PaperAgentTask task = PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
            File.ReadAllBytes(ScientificStaged.TaskPath));
        var result = new PaperAgentResultWire(
            PaperAgentSchemas.AgentResult,
            ScientificStaged.TaskRef,
            ScientificRegistration.PaperId,
            ScientificRegistration.TheoryProgramRef,
            ScientificRegistration.Phase,
            ScientificRegistration.AgentRole,
            ScientificRegistration.ContextMode,
            "completed",
            "A substantive claim-preserving scientific edit was completed.",
            [new PaperAgentOutputWire(
                PaperScientificEditingAgentSchemas.Draft,
                "outputs/scientific-edit-draft.json")],
            "journal-research",
            string.Empty,
            task.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
            "2026-08-31T12:30:00Z");
        string stdout = PaperAgentRuntimeService.ResultBegin + "\n"
            + Encoding.UTF8.GetString(CanonicalJson.Serialize(result))
            + "\n" + PaperAgentRuntimeService.ResultEnd + "\n";
        File.WriteAllText(
            ScientificPrepared.StdoutPath,
            stdout,
            new UTF8Encoding(false));
        return PaperAgentRuntimeService.RecordResult(
            Root,
            ScientificStaged.TaskRef,
            ScientificPrepared.StdoutPath,
            "codex-scientific-editing-journal-fixture-run",
            "produced");
    }

    private static PaperManuscriptDraftBlock ReviseBlock(
        string sectionId,
        PaperManuscriptDraftBlock block)
    {
        if (block.Kind == PaperManuscriptDraftBlockKinds.Proof
            && block.Order == 2)
        {
            return block with
            {
                Latex = "The proof begins from the certified dependency interface and explicitly identifies the reduction step, preservation of registered hypotheses, and the obstruction argument that yields the conclusion. The Lean declaration and selected truth release remain the formal evidence."
            };
        }
        if (block.Kind != PaperManuscriptDraftBlockKinds.Prose || block.Order != 1)
        {
            return block;
        }
        return sectionId switch
        {
            "introduction" => block with
            {
                Latex = "The contribution is organized around the exact obstruction equivalence, its realization theorem, and the classification consequence certified in one coherent release."
            },
            "prior-work" => block with
            {
                Latex = "Established descent and cocycle tools remain imported. The canonical obstruction, exact equivalence, realization result, and minimal-failure classification form the audited increment."
            },
            "boundaries" => block with
            {
                Latex = "The sharpness theorem fixes the certified boundary, while broader observables and weaker compatibility assumptions remain open research questions."
            },
            "discussion" => block with
            {
                Latex = "The manuscript now states the significance of the obstruction classification and delegates venue fit, current policy, format, and fees to a separate source-backed journal stage."
            },
            _ => block
        };
    }

    private static PaperJournalVenueCandidateDraft Venue(
        string venueId,
        string journalName,
        string publisher,
        string issn,
        string canonicalUrl,
        int publicationTier,
        string scopeFit,
        string feeStatus,
        long mandatoryFeeMinorUnits,
        string feeCurrency,
        string accessModel)
    {
        string prefix = venueId;
        return new PaperJournalVenueCandidateDraft(
            venueId,
            journalName,
            publisher,
            issn,
            canonicalUrl,
            "research-article",
            publicationTier,
            scopeFit,
            ArticleTypeSupported: true,
            LatexPolicy: "latex-accepted",
            MaximumAbstractWords: 350,
            MaximumMainTextWords: 15000,
            ProofAppendixAllowed: true,
            SupplementaryMaterialAllowed: true,
            feeStatus,
            mandatoryFeeMinorUnits,
            feeCurrency,
            DataPolicy: "not-applicable",
            CodePolicy: "required",
            PreprintPolicy: "allowed",
            AiPolicy: "disclosure-required",
            PeerReviewModel: "single-anonymized",
            accessModel,
            SourceIds:
            [
                prefix + "-official-core",
                prefix + "-official-policy",
                prefix + "-tier",
                prefix + "-comparable"
            ],
            ComparablePaperSourceIds: [prefix + "-comparable"],
            Rationale:
            [
                "The journal publishes theorem-driven mathematical work aligned with obstruction, descent, and formal verification themes.",
                "Its current research-article and LaTeX policies permit the certified source package without changing theorem identities."
            ],
            Risks: publicationTier > 2
                ? ["blocking: publication tier below required floor"]
                : []);
    }

    private static IEnumerable<PaperJournalSourceSnapshotDraft> SourcesForVenue(
        PaperJournalVenueCandidateDraft venue)
    {
        yield return Snapshot(
            venue.VenueId + "-official-core",
            venue.VenueId,
            [
                "official-scope",
                "official-author-guidelines",
                "official-article-types",
                "official-formatting"
            ],
            "official",
            venue.CanonicalUrl + "/authors",
            venue.JournalName + " author scope and format",
            [
                A("journal-name", venue.JournalName, $"Journal name: {venue.JournalName}."),
                A("publisher", venue.Publisher, $"Publisher: {venue.Publisher}."),
                A("issn", venue.Issn.ToUpperInvariant(), $"ISSN: {venue.Issn.ToUpperInvariant()}."),
                A("scope-fit", venue.ScopeFit, "Scope fit: " + venue.ScopeFit + ". The journal publishes obstruction theory, descent, classification, formal proof, and theorem-driven mathematical research."),
                A("target-article-type", venue.TargetArticleType, "Target article type: research-article."),
                A("article-type-supported", "true", "Research articles are supported: true."),
                A("latex-policy", venue.LatexPolicy, "LaTeX policy: latex-accepted.")
            ]);
        yield return Snapshot(
            venue.VenueId + "-official-policy",
            venue.VenueId,
            ["official-length", "official-fees", "official-policies"],
            "official",
            venue.CanonicalUrl + "/policies",
            venue.JournalName + " length fees and policies",
            [
                A("maximum-abstract-words", venue.MaximumAbstractWords.ToString(), "Maximum abstract words: 350."),
                A("maximum-main-text-words", venue.MaximumMainTextWords.ToString(), "Maximum main text words: 15000."),
                A("proof-appendix-allowed", "true", "Proof appendix allowed: true."),
                A("supplementary-material-allowed", "true", "Supplementary material allowed: true."),
                A("fee-status", venue.FeeStatus, "Fee status: " + venue.FeeStatus + "."),
                A("mandatory-fee-minor-units", venue.MandatoryFeeMinorUnits.ToString(), "Mandatory fee minor units: " + venue.MandatoryFeeMinorUnits + "."),
                A("fee-currency", venue.FeeCurrency, "Fee currency: " + venue.FeeCurrency + "."),
                A("data-policy", venue.DataPolicy, "Data policy: not-applicable."),
                A("code-policy", venue.CodePolicy, "Code policy: required."),
                A("preprint-policy", venue.PreprintPolicy, "Preprint policy: allowed."),
                A("ai-policy", venue.AiPolicy, "AI policy: disclosure-required."),
                A("peer-review-model", venue.PeerReviewModel, "Peer review model: single-anonymized."),
                A("access-model", venue.AccessModel, "Access model: " + venue.AccessModel + ".")
            ]);
        yield return Snapshot(
            venue.VenueId + "-tier",
            venue.VenueId,
            ["independent-tier"],
            "independent-index",
            "https://index.example.org/journals/" + venue.VenueId,
            venue.JournalName + " independent tier record",
            [
                A("publication-tier", venue.ClaimedPublicationTier.ToString(), "Independent publication tier: " + venue.ClaimedPublicationTier + ".")
            ]);
        yield return Snapshot(
            venue.VenueId + "-comparable",
            venue.VenueId,
            ["recent-comparable"],
            "journal-article",
            venue.CanonicalUrl + "/articles/obstruction-classification",
            "A recent theorem-driven obstruction classification",
            [
                A("comparable-title", "A recent theorem-driven obstruction classification", "Comparable title: A recent theorem-driven obstruction classification."),
                A("comparable-publication-date", "2026-07-15", "Comparable publication date: 2026-07-15."),
                A("comparable-article-type", "research-article", "Comparable article type: research-article."),
                A("comparable-doi", "10.1000/example." + venue.VenueId, "Comparable DOI: 10.1000/example." + venue.VenueId + ".")
            ]);
    }

    private static PaperJournalSourceSnapshotDraft Snapshot(
        string sourceId,
        string venueId,
        IReadOnlyList<string> roles,
        string authority,
        string url,
        string title,
        IReadOnlyList<PaperJournalSourceAssertion> assertions)
    {
        string text = string.Join(
            " ",
            assertions.Select(assertion => assertion.EvidenceText))
            + " This dated source snapshot is retained as a content-addressed evidence record for deterministic journal screening.";
        return new PaperJournalSourceSnapshotDraft(
            sourceId,
            venueId,
            roles,
            authority,
            url,
            title,
            "2026-08-31T12:45:00Z",
            "2026-08-20T00:00:00Z",
            text,
            CanonicalJson.Sha256Reference(Encoding.UTF8.GetBytes(text)),
            assertions);
    }

    private static PaperJournalSourceAssertion A(
        string fact,
        string value,
        string evidence) => new(fact, value, evidence);
}
