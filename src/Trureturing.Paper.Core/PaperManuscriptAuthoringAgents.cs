using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

internal sealed record PaperManuscriptRenderedSources(
    byte[] MainTexBytes,
    byte[] BibliographyBytes,
    IReadOnlyList<PaperManuscriptClaimBinding> ClaimBindings,
    IReadOnlyList<string> CitationKeys);

public static partial class PaperManuscriptAuthoringAgentService
{
    private const int MaximumControlBytes = 4 * 1024 * 1024;
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;
    private const int ExactInputCount = 14;

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PaperIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex SchemaPattern = new(
        "^[a-z][a-z0-9.-]*\\.v[0-9]+$",
        RegexOptions.CultureInvariant);
    private static readonly Regex RelativePathPattern = new(
        "^[A-Za-z0-9._+@=-]+(?:/[A-Za-z0-9._+@=-]+)*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex CitationKeyPattern = new(
        "^[A-Za-z][A-Za-z0-9_.:-]{0,127}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex IdentifierPattern = new(
        "^[A-Za-z][A-Za-z0-9_.:-]{0,127}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex LatexLabelPattern = new(
        "^[A-Za-z][A-Za-z0-9_.-]*:[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ProvenanceValues = new(
        ["produced", "adopted"],
        StringComparer.Ordinal);

    public static PaperManuscriptAuthoringAgentTaskStaged StageTask(
        string repositoryRoot,
        string evaluationRef,
        string claimManifestRef,
        string eligibilityRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        PaperManuscriptAuthoringContext context =
            PaperFrontierNodeSelectionService.LoadManuscriptAuthoringContext(
                root,
                evaluationRef,
                claimManifestRef,
                eligibilityRef);
        var dispatch = new PaperManuscriptAuthoringAgentDispatch(
            PaperManuscriptAuthoringAgentSchemas.Dispatch,
            evaluationRef,
            claimManifestRef,
            eligibilityRef,
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
            context.Completion.CompletedAt);
        Validate(dispatch);

        byte[] dispatchBytes = CanonicalJson.Serialize(dispatch);
        string dispatchRef = Reference(dispatchBytes);
        string dispatchPath = DomainArtifactPath(
            root,
            "dispatches",
            "raw",
            dispatchRef,
            ".json");
        _ = PutImmutable(dispatchPath, dispatchBytes);
        string dispatchRelativePath = RelativePath(root, dispatchPath);
        PaperAgentTask task = BuildTask(
            dispatch,
            dispatchRef,
            dispatchRelativePath,
            context);
        PaperAgentRuntimeService.Validate(task);

        byte[] taskBytes = CanonicalJson.Serialize(task);
        string taskRef = Reference(taskBytes);
        string taskPath = Path.Combine(
            root,
            "inbox",
            "agent-tasks",
            $"manuscript-authoring-{dispatch.PaperId}-{Hex(taskRef)}.json");
        bool replayed = PutImmutable(taskPath, taskBytes);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile(task.Phase);
        return new PaperManuscriptAuthoringAgentTaskStaged(
            PaperManuscriptAuthoringAgentSchemas.TaskStaged,
            dispatchRef,
            taskRef,
            taskPath,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.CompletionRef,
            dispatch.EvaluationRef,
            dispatch.ClaimManifestRef,
            dispatch.EligibilityRef,
            dispatch.ManuscriptPlanRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            replayed);
    }

    public static PaperManuscriptAuthoringAgentResultAdmitted AdmitResult(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("manuscript-authoring");
        if (!string.Equals(task.Phase, profile.Phase, StringComparison.Ordinal)
            || !string.Equals(
                task.AgentRole,
                profile.AgentRole,
                StringComparison.Ordinal)
            || !string.Equals(
                task.ContextMode,
                profile.ContextMode,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an FKST-native manuscript-authoring task can enter this admission bridge.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperManuscriptAuthoringAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Manuscript-authoring task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = Reference(dispatchBytes);
        PaperManuscriptAuthoringAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<
                PaperManuscriptAuthoringAgentDispatch>(dispatchBytes);
        Validate(dispatch);
        PaperManuscriptAuthoringContext context =
            PaperFrontierNodeSelectionService.LoadManuscriptAuthoringContext(
                root,
                dispatch.EvaluationRef,
                dispatch.ClaimManifestRef,
                dispatch.EligibilityRef);
        ValidateTaskBinding(
            root,
            task,
            dispatch,
            dispatchRef,
            dispatchInput.RepositoryRelativePath,
            context);

        PaperAgentTaskCursor agentCursor =
            ReadAgentCursor(root, task, taskRef);
        PaperAgentResultWire result = ReadAgentResult(
            root,
            task,
            taskRef,
            agentCursor.ResultRef);
        RequireCursorMatchesResult(agentCursor, result);
        if (!string.Equals(result.Status, "completed", StringComparison.Ordinal)
            || !string.Equals(
                result.NextRoute,
                "scientific-editing",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a completed manuscript draft routed to scientific editing can be admitted.");
        }

        string cursorPath = AdmissionCursorPath(root, taskRef);
        if (File.Exists(cursorPath))
        {
            return ReplayAdmission(
                root,
                ReadAdmissionCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef,
                context);
        }
        if (agentCursor.Outputs.Count != 1
            || !string.Equals(
                agentCursor.Outputs[0].Schema,
                PaperManuscriptAuthoringAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Completed manuscript authoring must return exactly one structured draft.");
        }

        PaperAgentStoredOutput output = agentCursor.Outputs[0];
        byte[] draftBytes = ReadAgentOutput(root, output.ArtifactRef);
        PaperScientificManuscriptDraft draft =
            PaperResearchInputJson.DeserializeStrict<
                PaperScientificManuscriptDraft>(draftBytes);
        ValidateDraft(
            root,
            draft,
            dispatch,
            dispatchRef,
            context,
            result.CompletedAt);

        PaperManuscriptRenderedSources rendered = RenderSources(
            root,
            draft,
            context);
        PaperManuscriptSourceFile mainTex = StoreSource(
            root,
            "main-tex",
            "text/x-tex",
            ".tex",
            rendered.MainTexBytes);
        PaperManuscriptSourceFile bibliography = StoreSource(
            root,
            "bibliography",
            "application/x-bibtex",
            ".bib",
            rendered.BibliographyBytes);

        var content = new PaperScientificManuscriptContent(
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.CompletionRef,
            dispatch.EvaluationRef,
            dispatch.ClaimManifestRef,
            dispatch.EligibilityRef,
            dispatch.ManuscriptPlanRef,
            dispatch.FrontierRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.LiteratureResearchRef,
            dispatch.SelectedReleaseRef,
            dispatch.SelectedReleaseDigest,
            draft.Title,
            mainTex,
            bibliography,
            rendered.ClaimBindings,
            draft.Sections.Select(value => value.SectionId).ToArray(),
            rendered.CitationKeys,
            context.ClaimManifest.FormalClaimCount,
            context.ClaimManifest.InformalItemCount,
            "journal-neutral-scientific-draft",
            result.CompletedAt);
        var manuscript = new PaperScientificManuscript(
            PaperManuscriptAuthoringAgentSchemas.ScientificManuscript,
            Reference(CanonicalJson.Serialize(content)),
            content with { Title = draft.Title });
        manuscript = manuscript with
        {
            ManuscriptId = Reference(
                CanonicalJson.Serialize(manuscript.ManuscriptContent))
        };
        Validate(manuscript, context, rendered.MainTexBytes, rendered.BibliographyBytes);
        PaperManuscriptAuthoringStoredArtifact storedManuscript = StoreDomain(
            root,
            "manuscripts",
            manuscript.Schema,
            manuscript.ManuscriptId,
            manuscript.ManuscriptContent,
            manuscript);

        var cursor = new PaperManuscriptAuthoringAgentAdmissionCursor(
            PaperManuscriptAuthoringAgentSchemas.AdmissionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.CompletionRef,
            dispatch.EvaluationRef,
            dispatch.ClaimManifestRef,
            dispatch.EligibilityRef,
            dispatch.ManuscriptPlanRef,
            storedManuscript,
            mainTex,
            bibliography,
            manuscript.ManuscriptContent.FormalClaimCount,
            manuscript.ManuscriptContent.InformalItemCount,
            "scientific-editing",
            agentCursor.RunId,
            agentCursor.Provenance,
            result.CompletedAt);
        Validate(cursor);
        try
        {
            PaperResearchInputStore.WriteAtomic(
                cursorPath,
                CanonicalJson.Serialize(cursor),
                overwrite: false);
        }
        catch (IOException) when (File.Exists(cursorPath))
        {
            return ReplayAdmission(
                root,
                ReadAdmissionCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef,
                context);
        }
        return Recorded(cursor, replayed: false);
    }

    public static void Validate(PaperManuscriptAuthoringAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperManuscriptAuthoringAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        foreach (string digest in new[]
        {
            dispatch.EvaluationRef,
            dispatch.ClaimManifestRef,
            dispatch.EligibilityRef,
            dispatch.ManuscriptPlanRef,
            dispatch.CompletionRef,
            dispatch.FrontierRef,
            dispatch.TheoryProgramRef,
            dispatch.ScopeRef,
            dispatch.InventoryRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.CandidatePaperRef,
            dispatch.LiteratureResearchRef,
            dispatch.SelectedReleaseRef,
            dispatch.SelectedReleaseDigest
        })
        {
            RequireDigest(digest, nameof(dispatch));
        }
        RequirePaperId(dispatch.PaperId);
        if (dispatch.ExactInputs is null
            || dispatch.ExactInputs.Count != ExactInputCount)
        {
            throw new InvalidDataException(
                $"Manuscript-authoring dispatch must contain exactly {ExactInputCount} inputs.");
        }
        RequireExactInputs(dispatch.ExactInputs);
        string[] requiredRefs =
        [
            dispatch.EvaluationRef,
            dispatch.ClaimManifestRef,
            dispatch.EligibilityRef,
            dispatch.ManuscriptPlanRef,
            dispatch.CompletionRef,
            dispatch.TheoryProgramRef,
            dispatch.ScopeRef,
            dispatch.InventoryRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.CandidatePaperRef,
            dispatch.LiteratureResearchRef,
            dispatch.SelectedReleaseRef
        ];
        foreach (string reference in requiredRefs)
        {
            if (!dispatch.ExactInputs.Any(value => string.Equals(
                    value.ArtifactRef,
                    reference,
                    StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Manuscript-authoring dispatch omitted a required exact evidence reference.");
            }
        }
        if (!dispatch.ExactInputs.Any(value => string.Equals(
                value.Schema,
                PaperFormalizationFrontierSchemas.Frontier,
                StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Manuscript-authoring dispatch omitted the admitted formalization frontier.");
        }
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    internal static void Validate(
        PaperScientificManuscript manuscript,
        PaperManuscriptAuthoringContext context,
        ReadOnlySpan<byte> mainTexBytes,
        ReadOnlySpan<byte> bibliographyBytes)
    {
        ArgumentNullException.ThrowIfNull(manuscript);
        RequireExact(
            manuscript.Schema,
            PaperManuscriptAuthoringAgentSchemas.ScientificManuscript,
            nameof(manuscript.Schema));
        PaperScientificManuscriptContent content = manuscript.ManuscriptContent
            ?? throw new InvalidDataException(
                "manuscript_content is required.");
        RequireIdentity(
            manuscript.ManuscriptId,
            content,
            nameof(manuscript.ManuscriptId));
        foreach (string digest in new[]
        {
            content.TaskRef,
            content.ResultRef,
            content.DispatchRef,
            content.CompletionRef,
            content.EvaluationRef,
            content.ClaimManifestRef,
            content.EligibilityRef,
            content.ManuscriptPlanRef,
            content.FrontierRef,
            content.TheoryProgramRef,
            content.TheoremPackageRef,
            content.TheoryAuditRef,
            content.LiteratureResearchRef,
            content.SelectedReleaseRef,
            content.SelectedReleaseDigest
        })
        {
            RequireDigest(digest, nameof(content));
        }
        RequirePaperId(content.PaperId);
        RequireText(content.Title, nameof(content.Title), 1, 1024);
        ValidateSourceFile(content.MainTex, mainTexBytes, "main-tex", "text/x-tex");
        ValidateSourceFile(
            content.Bibliography,
            bibliographyBytes,
            "bibliography",
            "application/x-bibtex");
        ValidateClaimBindings(content.ClaimBindings, context.ClaimManifest);
        RequireStringList(
            content.SectionIds,
            nameof(content.SectionIds),
            minimum: 8,
            maximum: 8,
            maximumItemLength: 64);
        RequireStringList(
            content.CitationKeys,
            nameof(content.CitationKeys),
            minimum: 0,
            maximum: 512,
            maximumItemLength: 128);
        if (!string.Equals(
                content.CompletionRef,
                context.CompletionCursor.CompletionRef,
                StringComparison.Ordinal)
            || !string.Equals(
                content.EvaluationRef,
                Reference(CanonicalJson.Serialize(context.Evaluation)),
                StringComparison.Ordinal)
            || !string.Equals(
                content.ClaimManifestRef,
                Reference(CanonicalJson.Serialize(context.ClaimManifest)),
                StringComparison.Ordinal)
            || !string.Equals(
                content.EligibilityRef,
                Reference(CanonicalJson.Serialize(context.Eligibility)),
                StringComparison.Ordinal)
            || !string.Equals(
                content.ManuscriptPlanRef,
                context.Evaluation.ManuscriptPlanRef,
                StringComparison.Ordinal)
            || !string.Equals(
                content.FrontierRef,
                context.Completion.FrontierRef,
                StringComparison.Ordinal)
            || !string.Equals(
                content.PaperId,
                context.Plan.PaperId,
                StringComparison.Ordinal)
            || !string.Equals(
                content.TheoryProgramRef,
                context.Planning.Program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                content.TheoremPackageRef,
                context.Planning.TheoremPackage.TheoremPackageId,
                StringComparison.Ordinal)
            || !string.Equals(
                content.TheoryAuditRef,
                context.Planning.Audit.AuditId,
                StringComparison.Ordinal)
            || !string.Equals(
                content.LiteratureResearchRef,
                context.Planning.Program.ProgramContent.LiteratureResearchRef,
                StringComparison.Ordinal)
            || !string.Equals(
                content.SelectedReleaseRef,
                context.Plan.ManuscriptTruthReleaseRef,
                StringComparison.Ordinal)
            || !string.Equals(
                content.SelectedReleaseDigest,
                context.SelectedRelease.ReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(content.Title, context.Plan.Title, StringComparison.Ordinal)
            || content.FormalClaimCount != context.ClaimManifest.FormalClaimCount
            || content.InformalItemCount != context.ClaimManifest.InformalItemCount
            || !string.Equals(
                content.AuthoringStatus,
                "journal-neutral-scientific-draft",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific manuscript changed its certified paper lineage or item counts.");
        }
        ParseUtc(content.CreatedAt, nameof(content.CreatedAt));
        ValidateRenderedBinding(
            Encoding.UTF8.GetString(mainTexBytes),
            context,
            content.ClaimBindings,
            content.CitationKeys);
    }

    public static void Validate(PaperManuscriptAuthoringAgentAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperManuscriptAuthoringAgentSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        foreach (string digest in new[]
        {
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.TheoryProgramRef,
            cursor.CompletionRef,
            cursor.EvaluationRef,
            cursor.ClaimManifestRef,
            cursor.EligibilityRef,
            cursor.ManuscriptPlanRef
        })
        {
            RequireDigest(digest, nameof(cursor));
        }
        RequirePaperId(cursor.PaperId);
        ValidateStoredArtifact(
            cursor.Manuscript,
            PaperManuscriptAuthoringAgentSchemas.ScientificManuscript);
        ValidateSourceCoordinate(cursor.MainTex, "main-tex", "text/x-tex");
        ValidateSourceCoordinate(
            cursor.Bibliography,
            "bibliography",
            "application/x-bibtex");
        if (cursor.FormalClaimCount < 1
            || cursor.InformalItemCount < 0
            || !string.Equals(
                cursor.NextRoute,
                "scientific-editing",
                StringComparison.Ordinal)
            || !ProvenanceValues.Contains(cursor.Provenance))
        {
            throw new InvalidDataException(
                "Manuscript authoring cursor counts, route, or provenance are invalid.");
        }
        RequireRunId(cursor.RunId);
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static PaperAgentTask BuildTask(
        PaperManuscriptAuthoringAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperManuscriptAuthoringContext context)
    {
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("manuscript-authoring");
        PaperAgentInputArtifact[] inputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperManuscriptAuthoringAgentSchemas.Dispatch,
                dispatchRef,
                dispatchRelativePath))
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        return new PaperAgentTask(
            PaperAgentSchemas.Task,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            inputs,
            [new PaperAgentExpectedOutput(
                PaperManuscriptAuthoringAgentSchemas.Draft,
                "outputs/scientific-manuscript-draft.json")],
            ["scientific-editing", "manuscript-authoring", "blocked"],
            BuildInstruction(dispatch, context),
            ManuscriptForbiddenShortcuts(),
            dispatch.RequestedAt);
    }

    private static string BuildInstruction(
        PaperManuscriptAuthoringAgentDispatch dispatch,
        PaperManuscriptAuthoringContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Author one journal-neutral mathematical manuscript draft from the exact certified evidence supplied.");
        builder.AppendLine("Write exactly one paper-scientific-manuscript-draft.v1 JSON object to outputs/scientific-manuscript-draft.json.");
        builder.AppendLine($"Use dispatch_ref={Reference(CanonicalJson.Serialize(dispatch))}.");
        builder.AppendLine($"Use claim_manifest_ref={dispatch.ClaimManifestRef} and manuscript_plan_ref={dispatch.ManuscriptPlanRef}.");
        builder.AppendLine($"Use paper_id={dispatch.PaperId}, theory_program_ref={dispatch.TheoryProgramRef}, and title={context.Plan.Title}.");
        builder.AppendLine("Return exactly eight ordered sections: introduction, prior-work, setting, main-results, proof-architecture, formalization, boundaries, discussion.");
        builder.AppendLine("Each section requires substantive prose. Main-results must contain one formal-claim anchor for every certified formal claim, in manifest order. Proof-architecture must contain one proof block for every formal claim. Every informal plan item must appear once through an informal-item anchor.");
        builder.AppendLine("Formal-claim and informal-item anchors carry target_id and empty latex. The repository alone inserts their exact statements, LaTeX environments, labels, certification markers, and epistemic status.");
        builder.AppendLine("Proof and prose blocks may contain bounded LaTeX fragments. Do not include document-level commands, section commands, labels, theorem environments, proof environments, file input, macro definitions, comments, or bibliography commands.");
        builder.AppendLine("Citations may use only \\cite{key}. Each draft reference must point by one-based related_work_index to the supplied literature-research artifact. The repository renders the bibliographic metadata from that artifact.");
        builder.AppendLine("Explain the research gap and contribution boundary conservatively, reconstruct the audited proof architecture, describe formal certification, and state limitations without adding a new mathematical claim.");
        builder.AppendLine("The completed result must route to scientific-editing. Use manuscript-authoring only for a no-progress retry and blocked only for a genuine evidence blocker.");
        return builder.ToString();
    }

    private static string[] ManuscriptForbiddenShortcuts() =>
    [
        "Do not add, omit, merge, split, weaken, strengthen, paraphrase, or reclassify a formal claim.",
        "Do not write theorem, lemma, proposition, corollary, definition, example, remark, or proof environments.",
        "Do not invent a citation, author, title, venue, year, URL, theorem source, experiment, or numerical result.",
        "Do not alter the selected truth release, certified claim manifest, theorem package, A3 audit, or literature evidence.",
        "Do not run Lean, Formalize, Git, GitHub, network access, journal selection, peer review, or cover-letter authoring.",
        "Do not emit a complete LaTeX document. Return only the declared structured JSON draft.",
        "Do not compute artifact IDs, source hashes, claim markers, or manuscript identity. Repository validation owns them."
    ];
}
