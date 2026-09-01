using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

internal sealed record PaperManuscriptSectionSpec(
    int Order,
    string SectionId,
    string Title);

public static partial class PaperManuscriptAuthoringAgentService
{
    private static readonly PaperManuscriptSectionSpec[] RequiredSections =
    [
        new(1, "introduction", "Introduction"),
        new(2, "prior-work", "Prior work and contribution boundary"),
        new(3, "setting", "Setting and definitions"),
        new(4, "main-results", "Main results"),
        new(5, "proof-architecture", "Proof architecture"),
        new(6, "formalization", "Formalization and certified provenance"),
        new(7, "boundaries", "Boundaries, sharpness, and counterexamples"),
        new(8, "discussion", "Discussion")
    ];

    private static readonly HashSet<string> DraftBlockKinds = new(
        [
            PaperManuscriptDraftBlockKinds.Prose,
            PaperManuscriptDraftBlockKinds.FormalClaim,
            PaperManuscriptDraftBlockKinds.Proof,
            PaperManuscriptDraftBlockKinds.InformalItem
        ],
        StringComparer.Ordinal);

    private static readonly string[] ForbiddenLatexFragments =
    [
        "\\documentclass",
        "\\usepackage",
        "\\begin{document}",
        "\\end{document}",
        "\\title",
        "\\author",
        "\\date",
        "\\maketitle",
        "\\section",
        "\\subsection",
        "\\subsubsection",
        "\\label",
        "\\input",
        "\\include",
        "\\includeonly",
        "\\write",
        "\\openout",
        "\\read",
        "\\catcode",
        "\\csname",
        "\\endcsname",
        "\\newcommand",
        "\\renewcommand",
        "\\providecommand",
        "\\def",
        "\\edef",
        "\\gdef",
        "\\xdef",
        "\\let",
        "\\futurelet",
        "\\special",
        "\\directlua",
        "\\immediate",
        "\\bibliography",
        "\\bibliographystyle",
        "\\addbibresource",
        "\\begin{theorem}",
        "\\end{theorem}",
        "\\begin{lemma}",
        "\\end{lemma}",
        "\\begin{proposition}",
        "\\end{proposition}",
        "\\begin{corollary}",
        "\\end{corollary}",
        "\\begin{definition}",
        "\\end{definition}",
        "\\begin{example}",
        "\\end{example}",
        "\\begin{remark}",
        "\\end{remark}",
        "\\begin{proof}",
        "\\end{proof}",
        "TRURETURING-FORMAL-CLAIM",
        "TRURETURING-INFORMAL-ITEM"
    ];

    private static readonly Regex CitationPattern = new(
        "\\\\cite\\{(?<keys>[A-Za-z0-9_.:,\\- ]+)\\}",
        RegexOptions.CultureInvariant);

    private static void ValidateDraft(
        string root,
        PaperScientificManuscriptDraft draft,
        PaperManuscriptAuthoringAgentDispatch dispatch,
        string dispatchRef,
        PaperManuscriptAuthoringContext context,
        string completedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireExact(
            draft.Schema,
            PaperManuscriptAuthoringAgentSchemas.Draft,
            nameof(draft.Schema));
        if (!string.Equals(draft.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(
                draft.ClaimManifestRef,
                dispatch.ClaimManifestRef,
                StringComparison.Ordinal)
            || !string.Equals(
                draft.ManuscriptPlanRef,
                dispatch.ManuscriptPlanRef,
                StringComparison.Ordinal)
            || !string.Equals(draft.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                draft.TheoryProgramRef,
                dispatch.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(draft.Title, context.Plan.Title, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific manuscript draft changed its dispatch, manifest, plan, paper, program, or title.");
        }

        ValidateAuthoredLatex(
            draft.AbstractLatex,
            nameof(draft.AbstractLatex),
            minimumLength: 200,
            maximumLength: 8192);
        RequireStringList(
            draft.Keywords,
            nameof(draft.Keywords),
            minimum: 3,
            maximum: 10,
            maximumItemLength: 128);
        if (draft.Keywords.Any(value => value.Contains('\\') || value.Contains('%')))
        {
            throw new InvalidDataException(
                "Manuscript keywords must be plain text.");
        }
        ValidateSections(draft.Sections, context.Plan);
        ValidateReferences(root, draft, dispatch, context);

        DateTimeOffset requested = ParseUtc(
            dispatch.RequestedAt,
            nameof(dispatch.RequestedAt));
        DateTimeOffset created = ParseUtc(
            draft.CreatedAt,
            nameof(draft.CreatedAt));
        DateTimeOffset completed = ParseUtc(
            completedAt,
            nameof(completedAt));
        if (created < requested || created > completed)
        {
            throw new InvalidDataException(
                "Manuscript draft created_at must lie between task request and result completion.");
        }
    }

    private static void ValidateSections(
        IReadOnlyList<PaperManuscriptDraftSection>? sections,
        PaperManuscriptPlan plan)
    {
        if (sections is null || sections.Count != RequiredSections.Length)
        {
            throw new InvalidDataException(
                "Scientific manuscript draft must contain exactly eight canonical sections.");
        }
        var formalAnchors = new List<string>();
        var proofs = new List<string>();
        var informalAnchors = new List<string>();
        for (int sectionIndex = 0; sectionIndex < RequiredSections.Length; sectionIndex++)
        {
            PaperManuscriptDraftSection section = sections[sectionIndex]
                ?? throw new InvalidDataException(
                    "Manuscript sections cannot contain null.");
            PaperManuscriptSectionSpec expected = RequiredSections[sectionIndex];
            if (section.Order != expected.Order
                || !string.Equals(
                    section.SectionId,
                    expected.SectionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    section.Title,
                    expected.Title,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Manuscript section order, identity, or title changed the repository-owned structure.");
            }
            if (section.Blocks is null || section.Blocks.Count < 1)
            {
                throw new InvalidDataException(
                    $"Manuscript section {section.SectionId} has no blocks.");
            }
            bool hasProse = false;
            for (int blockIndex = 0; blockIndex < section.Blocks.Count; blockIndex++)
            {
                PaperManuscriptDraftBlock block = section.Blocks[blockIndex]
                    ?? throw new InvalidDataException(
                        "Manuscript section blocks cannot contain null.");
                if (block.Order != blockIndex + 1
                    || !DraftBlockKinds.Contains(block.Kind))
                {
                    throw new InvalidDataException(
                        "Manuscript block order or kind is invalid.");
                }
                switch (block.Kind)
                {
                    case PaperManuscriptDraftBlockKinds.Prose:
                        RequireEmpty(block.TargetId, "prose target_id");
                        ValidateAuthoredLatex(
                            block.Latex,
                            $"{section.SectionId} prose",
                            minimumLength: 40,
                            maximumLength: 32768);
                        hasProse = true;
                        break;
                    case PaperManuscriptDraftBlockKinds.FormalClaim:
                        if (!string.Equals(
                                section.SectionId,
                                "main-results",
                                StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                "Formal claim anchors may appear only in main-results.");
                        }
                        RequireIdentifier(block.TargetId, "formal claim target_id");
                        RequireEmpty(block.Latex, "formal claim latex");
                        formalAnchors.Add(block.TargetId);
                        break;
                    case PaperManuscriptDraftBlockKinds.Proof:
                        if (!string.Equals(
                                section.SectionId,
                                "proof-architecture",
                                StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                "Proof blocks may appear only in proof-architecture.");
                        }
                        RequireIdentifier(block.TargetId, "proof target_id");
                        ValidateAuthoredLatex(
                            block.Latex,
                            $"proof for {block.TargetId}",
                            minimumLength: 80,
                            maximumLength: 65536);
                        proofs.Add(block.TargetId);
                        break;
                    case PaperManuscriptDraftBlockKinds.InformalItem:
                        RequireIdentifier(block.TargetId, "informal item target_id");
                        RequireEmpty(block.Latex, "informal item latex");
                        PaperManuscriptInformalItem item =
                            plan.InformalExposition.SingleOrDefault(value =>
                                string.Equals(
                                    value.ItemId,
                                    block.TargetId,
                                    StringComparison.Ordinal))
                            ?? throw new InvalidDataException(
                                "Informal item anchor is absent from the manuscript plan.");
                        string requiredSection = item.ItemKind == "definition"
                            ? "setting"
                            : "boundaries";
                        if (!string.Equals(
                                section.SectionId,
                                requiredSection,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                $"Informal {item.ItemKind} {item.ItemId} must appear in {requiredSection}.");
                        }
                        informalAnchors.Add(block.TargetId);
                        break;
                }
            }
            if (!hasProse)
            {
                throw new InvalidDataException(
                    $"Manuscript section {section.SectionId} requires substantive prose.");
            }
        }

        string[] expectedClaims = plan.FormalClaims
            .Select(value => value.ClaimId)
            .ToArray();
        if (!formalAnchors.SequenceEqual(expectedClaims, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Formal claim anchors must cover the complete manuscript plan in exact order.");
        }
        if (!proofs.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                expectedClaims.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Every formal manuscript claim requires exactly one proof block.");
        }
        string[] expectedInformal = plan.InformalExposition
            .Select(value => value.ItemId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!informalAnchors.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                expectedInformal,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Informal item anchors must cover every manuscript-plan item exactly once.");
        }
    }

    private static void ValidateReferences(
        string root,
        PaperScientificManuscriptDraft draft,
        PaperManuscriptAuthoringAgentDispatch dispatch,
        PaperManuscriptAuthoringContext context)
    {
        if (draft.References is null || draft.References.Count > 512)
        {
            throw new InvalidDataException(
                "Manuscript references must contain between zero and 512 entries.");
        }
        LiteratureResearchArtifact? literature =
            TryReadLiteratureResearch(root, dispatch, context);
        if (literature is null && draft.References.Count != 0)
        {
            throw new InvalidDataException(
                "Opaque literature evidence cannot authorize bibliographic entries.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var indexes = new HashSet<int>();
        string? previousKey = null;
        foreach (PaperManuscriptDraftReference reference in draft.References)
        {
            ArgumentNullException.ThrowIfNull(reference);
            RequireCitationKey(reference.CitationKey);
            if (previousKey is not null
                && string.CompareOrdinal(previousKey, reference.CitationKey) >= 0)
            {
                throw new InvalidDataException(
                    "Manuscript reference keys must be sorted and unique.");
            }
            previousKey = reference.CitationKey;
            if (!keys.Add(reference.CitationKey)
                || !indexes.Add(reference.RelatedWorkIndex)
                || !string.Equals(
                    reference.SourceRef,
                    dispatch.LiteratureResearchRef,
                    StringComparison.Ordinal)
                || literature is null
                || reference.RelatedWorkIndex < 1
                || reference.RelatedWorkIndex > literature.RelatedWork.Count)
            {
                throw new InvalidDataException(
                    "Manuscript reference is not a unique index into the exact literature evidence.");
            }
            RelatedWork work = literature.RelatedWork[reference.RelatedWorkIndex - 1];
            RequireText(work.Title, "related work title", 1, 4096);
            RequireStringList(
                work.Authors,
                "related work authors",
                minimum: 1,
                maximum: 128,
                maximumItemLength: 1024);
            RequireText(work.Venue, "related work venue", 1, 2048);
            if (work.Year is < 1000 or > 3000
                || !Uri.TryCreate(work.Url, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttps
                    && uri.Scheme != Uri.UriSchemeHttp))
            {
                throw new InvalidDataException(
                    "Related-work metadata is incomplete or has an invalid URL.");
            }
            RequireText(reference.Usage, nameof(reference.Usage), 20, 4096);
        }

        string combined = string.Join(
            "\n",
            draft.Sections.SelectMany(value => value.Blocks)
                .Where(value => !string.IsNullOrEmpty(value.Latex))
                .Select(value => value.Latex)
                .Prepend(draft.AbstractLatex));
        string[] citations = ExtractCitationKeys(combined);
        if (!citations.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                keys.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript citations and evidence-bound reference entries must match exactly.");
        }
    }

    private static LiteratureResearchArtifact? TryReadLiteratureResearch(
        string root,
        PaperManuscriptAuthoringAgentDispatch dispatch,
        PaperManuscriptAuthoringContext context)
    {
        PaperAgentInputArtifact input = context.ExactInputs.Single(value =>
            string.Equals(
                value.Schema,
                CandidateArtifactSchemas.LiteratureResearch,
                StringComparison.Ordinal));
        string full = Path.GetFullPath(Path.Combine(
            root,
            input.RepositoryRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        byte[] bytes = ReadBoundedFile(
            full,
            MaximumArtifactBytes,
            "Manuscript literature evidence");
        if (!string.Equals(
                Reference(bytes),
                dispatch.LiteratureResearchRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript literature evidence failed content-address verification.");
        }
        try
        {
            LiteratureResearchArtifact value =
                PaperResearchInputJson.DeserializeStrict<
                    LiteratureResearchArtifact>(bytes);
            if (!string.Equals(
                    value.Schema,
                    CandidateArtifactSchemas.LiteratureResearch,
                    StringComparison.Ordinal)
                || value.RelatedWork is null)
            {
                throw new InvalidDataException(
                    "Structured literature evidence has the wrong schema or related-work array.");
            }
            return value;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string[] ExtractCitationKeys(string latex)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string stripped = CitationPattern.Replace(latex, match =>
        {
            foreach (string key in match.Groups["keys"].Value.Split(','))
            {
                string trimmed = key.Trim();
                RequireCitationKey(trimmed);
                keys.Add(trimmed);
            }
            return string.Empty;
        });
        if (stripped.Contains("\\cite", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only canonical \\cite{key} citation syntax is supported.");
        }
        return keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateAuthoredLatex(
        string value,
        string name,
        int minimumLength,
        int maximumLength)
    {
        RequireText(value, name, minimumLength, maximumLength);
        if (value.Contains('%') || value.Contains("^^", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} contains a forbidden TeX comment or character-code escape.");
        }
        foreach (string forbidden in ForbiddenLatexFragments)
        {
            if (value.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{name} contains forbidden LaTeX control sequence '{forbidden}'.");
            }
        }
        _ = ExtractCitationKeys(value);
    }

    private static void RequireExactInputs(
        IReadOnlyList<PaperAgentInputArtifact> inputs)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperAgentInputArtifact input in inputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            RequireSchema(input.Schema, nameof(input.Schema));
            RequireDigest(input.ArtifactRef, nameof(input.ArtifactRef));
            RequireRelativePath(
                input.RepositoryRelativePath,
                nameof(input.RepositoryRelativePath));
            if (!refs.Add(input.ArtifactRef)
                || !paths.Add(input.RepositoryRelativePath))
            {
                throw new InvalidDataException(
                    "Manuscript-authoring exact input refs and paths must be unique.");
            }
        }
    }

    private static void RequireEmpty(string value, string name)
    {
        if (!string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException($"{name} must be empty.");
        }
    }

    private static void RequireIdentifier(string value, string name)
    {
        if (!IdentifierPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} is not canonical.");
        }
    }

    private static void RequireCitationKey(string value)
    {
        if (!CitationKeyPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                "Citation key is not canonical.");
        }
    }
}
