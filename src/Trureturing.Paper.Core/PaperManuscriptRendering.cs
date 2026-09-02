using System.Text;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static partial class PaperManuscriptAuthoringAgentService
{
    private static PaperManuscriptRenderedSources RenderSources(
        string root,
        PaperScientificManuscriptDraft draft,
        PaperManuscriptAuthoringContext context)
    {
        var formalById = context.ClaimManifest.FormalClaims.ToDictionary(
            value => value.ClaimId,
            StringComparer.Ordinal);
        var informalById = context.ClaimManifest.InformalExposition.ToDictionary(
            value => value.ItemId,
            StringComparer.Ordinal);
        var bindings = new List<PaperManuscriptClaimBinding>();
        var source = new StringBuilder();
        source.AppendLine("\\documentclass[11pt]{article}");
        source.AppendLine("\\usepackage[T1]{fontenc}");
        source.AppendLine("\\usepackage[utf8]{inputenc}");
        source.AppendLine("\\usepackage{amsmath,amssymb,amsthm}");
        source.AppendLine("\\usepackage{hyperref}");
        source.AppendLine("\\usepackage{url}");
        source.AppendLine("\\newtheorem{theorem}{Theorem}[section]");
        source.AppendLine("\\newtheorem{lemma}[theorem]{Lemma}");
        source.AppendLine("\\newtheorem{proposition}[theorem]{Proposition}");
        source.AppendLine("\\newtheorem{corollary}[theorem]{Corollary}");
        source.AppendLine("\\theoremstyle{definition}");
        source.AppendLine("\\newtheorem{definition}[theorem]{Definition}");
        source.AppendLine("\\newtheorem{example}[theorem]{Example}");
        source.AppendLine("\\theoremstyle{remark}");
        source.AppendLine("\\newtheorem{remark}[theorem]{Remark}");
        source.AppendLine();
        source.AppendLine($"\\title{{{EscapeLatexText(draft.Title)}}}");
        source.AppendLine("\\author{Anonymous}");
        source.AppendLine("\\date{}");
        source.AppendLine("\\begin{document}");
        source.AppendLine("\\maketitle");
        source.AppendLine("\\begin{abstract}");
        source.AppendLine(draft.AbstractLatex.Trim());
        source.AppendLine("\\end{abstract}");
        source.AppendLine();
        source.AppendLine(
            "\\noindent\\textbf{Keywords:} "
            + string.Join(", ", draft.Keywords.Select(EscapeLatexText)));
        source.AppendLine();

        int claimOrder = 0;
        foreach (PaperManuscriptDraftSection section in draft.Sections)
        {
            source.AppendLine($"\\section{{{EscapeLatexText(section.Title)}}}");
            source.AppendLine();
            foreach (PaperManuscriptDraftBlock block in section.Blocks)
            {
                switch (block.Kind)
                {
                    case PaperManuscriptDraftBlockKinds.Prose:
                        source.AppendLine(block.Latex.Trim());
                        source.AppendLine();
                        break;
                    case PaperManuscriptDraftBlockKinds.FormalClaim:
                    {
                        PaperCertifiedClaimManifestEntry entry =
                            formalById[block.TargetId];
                        claimOrder++;
                        string environment = FormalEnvironment(entry.ClaimKind);
                        string beginMarker = FormalBeginMarker(entry);
                        string endMarker = FormalEndMarker(entry);
                        source.AppendLine(beginMarker);
                        source.AppendLine(
                            $"\\begin{{{environment}}}\\label{{{entry.LatexLabel}}}");
                        source.AppendLine(EscapeLatexText(entry.Statement));
                        source.AppendLine($"\\end{{{environment}}}");
                        source.AppendLine(endMarker);
                        source.AppendLine();
                        bindings.Add(new PaperManuscriptClaimBinding(
                            claimOrder,
                            entry.ClaimId,
                            entry.LatexLabel,
                            entry.ClaimKind,
                            environment,
                            entry.CertifiedClaimRef,
                            entry.Gid,
                            entry.StatementId,
                            entry.RequestedStatementDigest,
                            beginMarker,
                            endMarker));
                        break;
                    }
                    case PaperManuscriptDraftBlockKinds.Proof:
                    {
                        PaperCertifiedClaimManifestEntry entry =
                            formalById[block.TargetId];
                        source.AppendLine(
                            $"\\begin{{proof}}[Proof of \\autoref{{{entry.LatexLabel}}}]");
                        source.AppendLine(block.Latex.Trim());
                        source.AppendLine("\\end{proof}");
                        source.AppendLine();
                        break;
                    }
                    case PaperManuscriptDraftBlockKinds.InformalItem:
                    {
                        PaperCertifiedClaimManifestInformalEntry entry =
                            informalById[block.TargetId];
                        string environment = InformalEnvironment(entry.ItemKind);
                        string beginMarker = InformalBeginMarker(entry);
                        string endMarker = InformalEndMarker(entry);
                        source.AppendLine(beginMarker);
                        source.AppendLine(
                            $"\\begin{{{environment}}}\\label{{{entry.LatexLabel}}}");
                        source.AppendLine(EscapeLatexText(entry.Text));
                        source.AppendLine();
                        source.AppendLine(
                            "\\emph{Epistemic status: "
                            + EscapeLatexText(entry.EpistemicStatus)
                            + ".}");
                        source.AppendLine($"\\end{{{environment}}}");
                        source.AppendLine(endMarker);
                        source.AppendLine();
                        break;
                    }
                    default:
                        throw new InvalidDataException(
                            $"Unsupported manuscript block kind {block.Kind}.");
                }
            }
        }

        LiteratureResearchArtifact? literature =
            TryReadLiteratureResearch(
                root,
                new PaperManuscriptAuthoringAgentDispatch(
                    PaperManuscriptAuthoringAgentSchemas.Dispatch,
                    Reference(CanonicalJson.Serialize(context.Evaluation)),
                    Reference(CanonicalJson.Serialize(context.ClaimManifest)),
                    Reference(CanonicalJson.Serialize(context.Eligibility)),
                    context.Evaluation.ManuscriptPlanRef,
                    context.CompletionCursor.CompletionRef,
                    context.CompletionCursor.FrontierRef,
                    context.Plan.PaperId,
                    context.Planning.Program.TheoryProgramId,
                    context.Planning.Scope.ScopeId,
                    context.Planning.Inventory.InventoryId,
                    context.Planning.TheoremPackage.TheoremPackageId,
                    context.Planning.Audit.AuditId,
                    context.Planning.Program.ProgramContent.CandidatePaperRef,
                    context.Planning.Program.ProgramContent.LiteratureResearchRef,
                    context.Plan.ManuscriptTruthReleaseRef,
                    context.SelectedRelease.ReleaseDigest,
                    context.ExactInputs,
                    context.Completion.CompletedAt),
                context);
        string[] citationKeys = draft.References
            .Select(value => value.CitationKey)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        byte[] bibliography = RenderBibliography(draft.References, literature);
        if (citationKeys.Length > 0)
        {
            source.AppendLine("\\bibliographystyle{plain}");
            source.AppendLine("\\bibliography{references}");
        }
        source.AppendLine("\\end{document}");
        byte[] mainTex = Encoding.UTF8.GetBytes(source.ToString());
        ValidateRenderedBinding(
            source.ToString(),
            context,
            bindings,
            citationKeys);
        return new(mainTex, bibliography, bindings, citationKeys);
    }

    private static byte[] RenderBibliography(
        IReadOnlyList<PaperManuscriptDraftReference> references,
        LiteratureResearchArtifact? literature)
    {
        var builder = new StringBuilder();
        if (references.Count == 0)
        {
            builder.AppendLine(
                "@comment{No evidence-bound bibliographic records were supplied for this draft.}");
            return Encoding.UTF8.GetBytes(builder.ToString());
        }
        if (literature is null)
        {
            throw new InvalidDataException(
                "Bibliography rendering requires structured literature evidence.");
        }
        foreach (PaperManuscriptDraftReference reference in references)
        {
            RelatedWork work = literature.RelatedWork[reference.RelatedWorkIndex - 1];
            builder.AppendLine($"@misc{{{reference.CitationKey},");
            builder.AppendLine(
                $"  author = {{{string.Join(" and ", work.Authors.Select(EscapeBibText))}}},");
            builder.AppendLine($"  title = {{{EscapeBibText(work.Title)}}},");
            builder.AppendLine($"  howpublished = {{{EscapeBibText(work.Venue)}}},");
            builder.AppendLine($"  year = {{{work.Year}}},");
            builder.AppendLine($"  note = {{\\url{{{EscapeBibUrl(work.Url)}}}}}");
            builder.AppendLine("}");
            builder.AppendLine();
        }
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string FormalEnvironment(string claimKind) =>
        claimKind switch
        {
            "theorem" => "theorem",
            "lemma" => "lemma",
            "proposition" => "proposition",
            "corollary" => "corollary",
            _ => throw new InvalidDataException(
                $"Unsupported certified manuscript claim kind {claimKind}.")
        };

    private static string InformalEnvironment(string itemKind) =>
        itemKind switch
        {
            "definition" => "definition",
            "example" => "example",
            "remark" => "remark",
            "conjecture" => "remark",
            "motivation" => "remark",
            "discussion" => "remark",
            "limitation" => "remark",
            _ => throw new InvalidDataException(
                $"Unsupported informal manuscript item kind {itemKind}.")
        };

    private static string FormalBeginMarker(
        PaperCertifiedClaimManifestEntry entry) =>
        "% TRURETURING-FORMAL-CLAIM-BEGIN"
        + $" claim_id={entry.ClaimId}"
        + $" certified_claim_ref={entry.CertifiedClaimRef}"
        + $" gid={entry.Gid}"
        + $" statement_id={entry.StatementId}";

    private static string FormalEndMarker(
        PaperCertifiedClaimManifestEntry entry) =>
        "% TRURETURING-FORMAL-CLAIM-END"
        + $" claim_id={entry.ClaimId}"
        + $" certified_claim_ref={entry.CertifiedClaimRef}";

    private static string InformalBeginMarker(
        PaperCertifiedClaimManifestInformalEntry entry) =>
        "% TRURETURING-INFORMAL-ITEM-BEGIN"
        + $" item_id={entry.ItemId}"
        + $" text_digest={entry.TextDigest}"
        + $" epistemic_status={entry.EpistemicStatus}";

    private static string InformalEndMarker(
        PaperCertifiedClaimManifestInformalEntry entry) =>
        "% TRURETURING-INFORMAL-ITEM-END"
        + $" item_id={entry.ItemId}"
        + $" text_digest={entry.TextDigest}";

    private static string EscapeLatexText(string value)
    {
        var builder = new StringBuilder(value.Length + 32);
        foreach (char character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\textbackslash{}",
                '{' => "\\{",
                '}' => "\\}",
                '#' => "\\#",
                '$' => "\\$",
                '%' => "\\%",
                '&' => "\\&",
                '_' => "\\_",
                '^' => "\\textasciicircum{}",
                '~' => "\\textasciitilde{}",
                _ => character.ToString()
            });
        }
        return builder.ToString();
    }

    private static string EscapeBibText(string value) =>
        EscapeLatexText(value)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static string EscapeBibUrl(string value) =>
        value.Replace("{", "%7B", StringComparison.Ordinal)
            .Replace("}", "%7D", StringComparison.Ordinal)
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("#", "%23", StringComparison.Ordinal)
            .Replace(" ", "%20", StringComparison.Ordinal);

    private static void ValidateRenderedBinding(
        string source,
        PaperManuscriptAuthoringContext context,
        IReadOnlyList<PaperManuscriptClaimBinding> bindings,
        IReadOnlyList<string> citationKeys)
    {
        if (Count(source, "\\documentclass[11pt]{article}") != 1
            || Count(source, "\\begin{document}") != 1
            || Count(source, "\\end{document}") != 1
            || Count(source, "% TRURETURING-FORMAL-CLAIM-BEGIN")
                != context.ClaimManifest.FormalClaimCount
            || Count(source, "% TRURETURING-FORMAL-CLAIM-END")
                != context.ClaimManifest.FormalClaimCount
            || Count(source, "% TRURETURING-INFORMAL-ITEM-BEGIN")
                != context.ClaimManifest.InformalItemCount
            || Count(source, "% TRURETURING-INFORMAL-ITEM-END")
                != context.ClaimManifest.InformalItemCount)
        {
            throw new InvalidDataException(
                "Rendered LaTeX document-level or epistemic marker counts are invalid.");
        }
        ValidateClaimBindings(bindings, context.ClaimManifest);
        foreach (PaperManuscriptClaimBinding binding in bindings)
        {
            if (Count(source, binding.BeginMarker) != 1
                || Count(source, binding.EndMarker) != 1
                || Count(source, $"\\label{{{binding.LatexLabel}}}") != 1)
            {
                throw new InvalidDataException(
                    $"Rendered source changed or duplicated formal claim {binding.ClaimId}.");
            }
        }
        foreach (PaperCertifiedClaimManifestInformalEntry item
            in context.ClaimManifest.InformalExposition)
        {
            if (Count(source, InformalBeginMarker(item)) != 1
                || Count(source, InformalEndMarker(item)) != 1
                || Count(source, $"\\label{{{item.LatexLabel}}}") != 1)
            {
                throw new InvalidDataException(
                    $"Rendered source changed or duplicated informal item {item.ItemId}.");
            }
        }
        string[] observedCitations = ExtractCitationKeys(source);
        if (!observedCitations.SequenceEqual(
                citationKeys.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Rendered source changed the evidence-bound citation set.");
        }
    }

    private static void ValidateClaimBindings(
        IReadOnlyList<PaperManuscriptClaimBinding>? bindings,
        PaperCertifiedClaimManifest manifest)
    {
        if (bindings is null
            || bindings.Count != manifest.FormalClaims.Count)
        {
            throw new InvalidDataException(
                "Scientific manuscript claim bindings do not cover the certified manifest.");
        }
        for (int index = 0; index < bindings.Count; index++)
        {
            PaperManuscriptClaimBinding binding = bindings[index]
                ?? throw new InvalidDataException(
                    "Scientific manuscript claim bindings cannot contain null.");
            PaperCertifiedClaimManifestEntry entry =
                manifest.FormalClaims[index];
            if (binding.Order != index + 1
                || !string.Equals(binding.ClaimId, entry.ClaimId, StringComparison.Ordinal)
                || !string.Equals(binding.LatexLabel, entry.LatexLabel, StringComparison.Ordinal)
                || !string.Equals(binding.ClaimKind, entry.ClaimKind, StringComparison.Ordinal)
                || !string.Equals(binding.Environment, FormalEnvironment(entry.ClaimKind), StringComparison.Ordinal)
                || !string.Equals(binding.CertifiedClaimRef, entry.CertifiedClaimRef, StringComparison.Ordinal)
                || !string.Equals(binding.Gid, entry.Gid, StringComparison.Ordinal)
                || !string.Equals(binding.StatementId, entry.StatementId, StringComparison.Ordinal)
                || !string.Equals(binding.RequestedStatementDigest, entry.RequestedStatementDigest, StringComparison.Ordinal)
                || !string.Equals(binding.BeginMarker, FormalBeginMarker(entry), StringComparison.Ordinal)
                || !string.Equals(binding.EndMarker, FormalEndMarker(entry), StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Scientific manuscript claim binding changed certified claim identity.");
            }
        }
    }

    private static int Count(string value, string needle)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }
        return count;
    }
}
