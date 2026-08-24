using System.Net;
using System.Text;
using StrataLint.Scribe;

namespace Trureturing.Paper.Core;

public sealed record ExamplePaperArtifacts(byte[] Latex, byte[] Html);

public static class ExamplePaperPublisher
{
    public const string CertifiedDeclarationId =
        "D5/S0/Carrier/TraceConjugation.trace_conj";

    private const string DescribeAnchor =
        "describe:trace-invariance-under-conjugation";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static ExamplePaperArtifacts Produce(
        PaperTruthReleasePort truthPort,
        PaperIntuitionPort intuitionPort,
        FrozenInputs frozenInputs)
    {
        ArgumentNullException.ThrowIfNull(truthPort);
        ArgumentNullException.ThrowIfNull(intuitionPort);
        ArgumentNullException.ThrowIfNull(frozenInputs);

        PaperTruthIndex truth = PaperTruthIndex.Build(truthPort);
        PaperIntuitionIndex intuition = PaperIntuitionIndex.Build(intuitionPort, truth);
        PaperTruthEntry certified = truth.GetDeclaration(CertifiedDeclarationId);
        SourceSnapshot snapshot = SourceSnapshotReader.ReadAndVerify(frozenInputs.Snapshot);
        if (!string.Equals(truth.SourceCommit, snapshot.SourceCommit, StringComparison.Ordinal)
            || !string.Equals(truth.SourceTree, snapshot.SourceTree, StringComparison.Ordinal))
        {
            throw new ClaimGateException(
                "Certified port is bound to a different blessed source snapshot.");
        }

        var recipe = new PaperRecipe(
            "recipe.v1",
            "trace-conjugation-example",
            "Trace Invariance Under Conjugation",
            [new RecipeClaim(certified.DeclarationId, DescribeAnchor)]);

        // This remains the authority boundary: the selected port entry must still pass the
        // frozen-ledger, blessed-report, axiom-whitelist and closed-truth-graph claim gate.
        PaperDocument document = PaperAssembler.AssembleDocument(recipe, frozenInputs);
        TheoremBlock theorem = document.Theorems.Single();
        FrozenDeclaration frozen = frozenInputs.Declarations.Single(declaration =>
            string.Equals(
                declaration.DeclarationGid,
                certified.DeclarationId,
                StringComparison.Ordinal));
        if (!certified.AxiomClosure.SequenceEqual(
                frozen.DeclaredAxioms.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new ClaimGateException(
                "Certified port axiom closure does not match the gated frozen declaration.");
        }

        FrozenTruthGraph graph = TruthGraphReader.ReadAndVerify(
            frozenInputs.TruthGraph ?? throw new ClaimGateException(
                "Example publication requires a frozen truth graph."),
            snapshot);
        ClosedTruthBinding binding = TruthGraphReader.RequireClosedTheorem(
            graph,
            certified.DeclarationId,
            theorem.DescribeAnchor);
        if (!string.Equals(
                certified.StatementId,
                PaperPortIdentity.StatementId(theorem.Statement),
                StringComparison.Ordinal)
            || !string.Equals(
                certified.RepoPath,
                binding.FormalTruthRepoPath,
                StringComparison.Ordinal)
            || !string.Equals(
                certified.MdbookPath,
                binding.DocumentRepoPath,
                StringComparison.Ordinal))
        {
            throw new ClaimGateException(
                "Certified port citation does not match the gated truth-graph binding.");
        }

        return new ExamplePaperArtifacts(
            LatexDocumentWriter.Write(document),
            WriteHtml(document, certified, intuition, truth, snapshot));
    }

    private static byte[] WriteHtml(
        PaperDocument document,
        PaperTruthEntry certified,
        PaperIntuitionIndex intuition,
        PaperTruthIndex truth,
        SourceSnapshot snapshot)
    {
        TheoremBlock theorem = document.Theorems.Single();
        string axiomClosure = certified.AxiomClosure.Count == 0
            ? "none"
            : string.Join(", ", certified.AxiomClosure.Select(Html));
        var builder = new StringBuilder();
        builder.Append("<!doctype html>\n")
            .Append("<html lang=\"en\">\n<head>\n")
            .Append("  <meta charset=\"utf-8\">\n")
            .Append("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("  <meta name=\"description\" content=\"A claim-gated example paper assembled from a certified truth-release port.\">\n")
            .Append("  <title>").Append(Html(document.Title)).Append("</title>\n")
            .Append("  <style>\n")
            .Append(Css)
            .Append("  </style>\n</head>\n<body>\n")
            .Append("  <header class=\"masthead\"><a href=\"#paper\" class=\"wordmark\">TRURETURING / PAPER</a><span>Example release ")
            .Append(Html(ShortDigest(truth.ReleaseDigest))).Append("</span></header>\n")
            .Append("  <main id=\"paper\" class=\"paper\">\n")
            .Append("    <article>\n")
            .Append("      <header class=\"paper-header\">\n")
            .Append("        <p class=\"kicker\">A certified note from the frozen truth graph</p>\n")
            .Append("        <h1>").Append(Html(document.Title)).Append("</h1>\n")
            .Append("        <p class=\"byline\">The Omega Institute <span>&middot;</span> Reproducible example cycle</p>\n")
            .Append("      </header>\n")
            .Append("      <section class=\"abstract\" aria-labelledby=\"abstract-title\">\n")
            .Append("        <h2 id=\"abstract-title\">Abstract</h2>\n")
            .Append("        <p>We present a minimal, mechanically assembled note on trace invariance under conjugation. The factual result is selected from a typed certified release, then checked again against the blessed snapshot, its closed truth-graph node, and the frozen axiom closure before publication.</p>\n")
            .Append("      </section>\n")
            .Append("      <section aria-labelledby=\"setting-title\">\n")
            .Append("        <p class=\"section-number\">01</p><h2 id=\"setting-title\">Setting</h2>\n")
            .Append("        <p>Conjugation acts on a golden integer in coordinates by sending <span class=\"inline-math\">(a, b)</span> to <span class=\"inline-math\">(a + b, &minus;b)</span>. The certified carrier theorem records the invariant that survives this action.</p>\n")
            .Append("      </section>\n")
            .Append("      <section aria-labelledby=\"result-title\">\n")
            .Append("        <p class=\"section-number\">02</p><h2 id=\"result-title\">Certified result</h2>\n")
            .Append("        <p>").Append(Html(theorem.Narrative)).Append(" The declaration is certified by the frozen release.<a class=\"cite\" href=\"#ref-1\" aria-label=\"Reference 1\">[1]</a></p>\n")
            .Append("        <div class=\"theorem\" aria-label=\"Certified theorem\">\n")
            .Append("          <div class=\"theorem-label\"><span>Certified theorem</span><span>closed</span></div>\n")
            .Append("          <p class=\"formula\">").Append(WriteFormula(theorem.Statement)).Append("</p>\n")
            .Append("          <dl><div><dt>Declaration</dt><dd>").Append(Html(certified.DeclarationId)).Append("</dd></div>")
            .Append("<div><dt>Axiom closure</dt><dd>").Append(axiomClosure).Append("</dd></div></dl>\n")
            .Append("        </div>\n")
            .Append("      </section>\n")
            .Append("      <section aria-labelledby=\"directions-title\">\n")
            .Append("        <p class=\"section-number\">03</p><h2 id=\"directions-title\">Research directions</h2>\n")
            .Append("        <p class=\"advisory-intro\"><strong>Advisory, not certified.</strong> These candidate bridges are planning signals from the separate Intuition index. They are not stated or cited as facts.</p>\n")
            .Append("        <div class=\"directions\">\n");

        foreach (PaperIntuitionEntry candidate in intuition.UnsettledCandidates())
        {
            builder.Append("          <article class=\"direction\"><div><span class=\"status\">advisory &middot; ")
                .Append(Html(candidate.Status)).Append("</span><span class=\"proposal\">")
                .Append(Html(candidate.ProposalId)).Append("</span></div><h3>")
                .Append(Html(candidate.RelationType)).Append("</h3><p>Explore ")
                .Append(Html(string.Join(", ", candidate.Outputs)))
                .Append(" from the certified input ")
                .Append(Html(string.Join(", ", candidate.Inputs)))
                .Append(".</p><p class=\"falsifier\"><strong>Falsifier:</strong> ")
                .Append(Html(candidate.Falsifier)).Append("</p></article>\n");
        }

        builder.Append("        </div>\n")
            .Append("      </section>\n")
            .Append("      <section class=\"references\" aria-labelledby=\"references-title\">\n")
            .Append("        <p class=\"section-number\">04</p><h2 id=\"references-title\">Certified source</h2>\n")
            .Append("        <ol><li id=\"ref-1\"><span>").Append(Html(certified.DeclarationId))
            .Append("</span><span>").Append(Html(certified.RepoPath))
            .Append(" &middot; statement ").Append(Html(ShortDigest(certified.StatementId))).Append("</span></li></ol>\n")
            .Append("      </section>\n")
            .Append("    </article>\n")
            .Append("  </main>\n")
            .Append("  <footer class=\"provenance\"><div><span>Provenance</span><p>Assembled from a typed local-dev release port and re-checked by the paper claim gate.</p></div><dl>")
            .Append("<div><dt>source_commit</dt><dd>").Append(Html(snapshot.SourceCommit)).Append("</dd></div>")
            .Append("<div><dt>release digest</dt><dd>").Append(Html(truth.ReleaseDigest)).Append("</dd></div>")
            .Append("<div><dt>certified declarations</dt><dd>").Append(truth.Declarations.Count).Append("</dd></div>")
            .Append("<div><dt>advisory candidates</dt><dd>").Append(intuition.Candidates.Count).Append("</dd></div>")
            .Append("</dl></footer>\n</body>\n</html>\n");
        return StrictUtf8.GetBytes(builder.ToString());
    }

    private static string WriteFormula(Formula formula) => formula switch
    {
        Formula.Symbol symbol => $"<var>{Html(symbol.Name.Value)}</var>",
        Formula.FunctionCall call =>
            $"<span class=\"fn\">{Html(call.Name.Value)}</span>(" +
            string.Join(", ", call.Arguments.Select(WriteFormula)) + ")",
        Formula.Relation relation when relation.Operator == FormulaRelationOperator.Equal =>
            $"{WriteFormula(relation.Left)} <span class=\"relation\">=</span> {WriteFormula(relation.Right)}",
        _ => throw new ClaimGateException(
            $"Example HTML publisher does not support formula node {formula.GetType().Name}.")
    };

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string ShortDigest(string value) =>
        value.Length <= 22 ? value : $"{value[..15]}...{value[^4..]}";

    private const string Css = """
    :root { color-scheme: light; --ink: #171815; --muted: #66685f; --paper: #f7f6f1; --line: #d5d2c7; --accent: #176b5b; --advisory: #8b4b20; --advisory-bg: #f5e9dc; }
    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body { margin: 0; background: var(--paper); color: var(--ink); font-family: Georgia, "Times New Roman", serif; line-height: 1.7; }
    .masthead { min-height: 58px; padding: 0 5vw; border-bottom: 1px solid var(--line); display: flex; align-items: center; justify-content: space-between; gap: 24px; font: 11px/1.2 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform: uppercase; color: var(--muted); }
    .wordmark { color: var(--ink); font-weight: 700; text-decoration: none; letter-spacing: 0; }
    .paper { width: min(760px, calc(100% - 40px)); margin: 0 auto; padding: 88px 0 72px; }
    .paper-header { padding-bottom: 52px; border-bottom: 1px solid var(--ink); }
    .kicker, .section-number { margin: 0 0 14px; color: var(--accent); font: 700 11px/1.2 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform: uppercase; }
    h1 { max-width: 680px; margin: 0; font-size: 74px; font-weight: 400; line-height: .98; letter-spacing: 0; }
    .byline { margin: 28px 0 0; color: var(--muted); font-size: 15px; }
    .byline span { padding: 0 8px; color: var(--line); }
    section { padding: 58px 0 16px; }
    section h2 { margin: 0 0 24px; font-size: 29px; font-weight: 400; line-height: 1.15; letter-spacing: 0; }
    section > p { font-size: 18px; }
    .abstract { display: grid; grid-template-columns: 130px 1fr; gap: 30px; padding-bottom: 42px; border-bottom: 1px solid var(--line); }
    .abstract h2 { font-size: 14px; font-weight: 700; text-transform: uppercase; }
    .abstract p { margin: -8px 0 0; font-size: 20px; line-height: 1.65; }
    .inline-math, .formula { font-family: "Times New Roman", serif; font-style: italic; }
    .cite { margin-left: 2px; color: var(--accent); font: 700 12px/1 ui-monospace, SFMono-Regular, Menlo, monospace; text-decoration: none; vertical-align: super; }
    .theorem { margin-top: 34px; padding: 28px 30px; border: 1px solid var(--ink); background: #fff; }
    .theorem-label { display: flex; justify-content: space-between; gap: 20px; color: var(--accent); font: 700 10px/1.2 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform: uppercase; }
    .formula { margin: 36px 0; text-align: center; font-size: 30px; line-height: 1.3; overflow-wrap: anywhere; }
    .fn { font-style: normal; }
    .relation { padding: 0 8px; }
    .theorem dl { margin: 0; padding-top: 20px; border-top: 1px solid var(--line); }
    .theorem dl div { display: grid; grid-template-columns: 130px 1fr; gap: 12px; padding: 4px 0; }
    dt { color: var(--muted); }
    dd { margin: 0; overflow-wrap: anywhere; }
    .theorem dt, .theorem dd, .provenance dt, .provenance dd { font: 11px/1.55 ui-monospace, SFMono-Regular, Menlo, monospace; }
    .advisory-intro { padding-left: 18px; border-left: 3px solid var(--advisory); color: #63391f; }
    .directions { margin-top: 30px; border-top: 1px solid #caa887; }
    .direction { padding: 26px 0; border-bottom: 1px solid #caa887; }
    .direction > div { display: flex; justify-content: space-between; gap: 20px; }
    .status, .proposal { color: var(--advisory); font: 700 10px/1.3 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform: uppercase; }
    .proposal { color: var(--muted); text-transform: none; }
    .direction h3 { margin: 13px 0 7px; font-size: 21px; font-weight: 400; }
    .direction p { margin: 5px 0; }
    .falsifier { color: #63391f; font-size: 14px; }
    .references ol { margin: 0; padding: 0; list-style: none; border-top: 1px solid var(--line); }
    .references li { display: grid; grid-template-columns: 1fr 1fr; gap: 26px; padding: 19px 0; border-bottom: 1px solid var(--line); font-size: 13px; }
    .references li span:last-child { color: var(--muted); overflow-wrap: anywhere; }
    .provenance { padding: 36px max(5vw, 24px) 44px; border-top: 1px solid var(--ink); display: grid; grid-template-columns: minmax(220px, 1fr) minmax(380px, 2fr); gap: 8vw; background: #eeece5; }
    .provenance > div > span { color: var(--accent); font: 700 11px/1.2 ui-monospace, SFMono-Regular, Menlo, monospace; text-transform: uppercase; }
    .provenance p { max-width: 420px; margin: 12px 0 0; font-size: 14px; color: var(--muted); }
    .provenance dl { margin: 0; }
    .provenance dl div { display: grid; grid-template-columns: 155px 1fr; gap: 16px; padding: 5px 0; }
    @media (max-width: 650px) { .masthead span { display: none; } .paper { width: min(100% - 32px, 760px); padding-top: 58px; } h1 { font-size: 46px; } .abstract { grid-template-columns: 1fr; gap: 8px; } .abstract p { margin: 0; } .theorem { padding: 22px 18px; } .theorem dl div, .provenance dl div { grid-template-columns: 1fr; gap: 2px; } .direction > div, .references li { grid-template-columns: 1fr; display: grid; gap: 6px; } .provenance { grid-template-columns: 1fr; gap: 28px; } }
    @media (prefers-reduced-motion: reduce) { html { scroll-behavior: auto; } }
""";
}
