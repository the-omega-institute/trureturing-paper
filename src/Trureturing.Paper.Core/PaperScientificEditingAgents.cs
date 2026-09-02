using System.Globalization;
using System.Text;

namespace Trureturing.Paper.Core;

internal sealed record PaperScientificEditingContext(
    PaperAgentTask SourceTask,
    PaperAgentTaskCursor SourceAgentCursor,
    PaperManuscriptAuthoringAgentAdmissionCursor SourceAuthoringCursor,
    PaperManuscriptAuthoringAgentDispatch SourceDispatch,
    PaperManuscriptAuthoringContext AuthoringContext,
    PaperScientificManuscriptDraft SourceDraft,
    PaperScientificManuscript SourceManuscript,
    byte[] SourceMainTex,
    byte[] SourceBibliography,
    string SourceAuthoringCursorRef,
    IReadOnlyList<PaperAgentInputArtifact> ExactInputs);

public static partial class PaperManuscriptAuthoringAgentService
{
    private const int ScientificEditingExactInputCount = 19;

    private static readonly HashSet<string> ScientificEditDimensions = new(
        [
            "contribution-framing",
            "literature-boundary",
            "logical-sequencing",
            "proof-exposition",
            "provenance-exposition",
            "limitations-and-implications"
        ],
        StringComparer.Ordinal);

    public static PaperScientificEditingAgentTaskStaged StageScientificEditingTask(
        string repositoryRoot,
        string sourceAuthoringTaskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(sourceAuthoringTaskRef, nameof(sourceAuthoringTaskRef));
        PaperScientificEditingContext context =
            LoadScientificEditingContext(root, sourceAuthoringTaskRef);
        var dispatch = new PaperScientificEditingAgentDispatch(
            PaperScientificEditingAgentSchemas.Dispatch,
            sourceAuthoringTaskRef,
            context.SourceAgentCursor.ResultRef,
            context.SourceAuthoringCursorRef,
            context.SourceManuscript.ManuscriptId,
            context.SourceAuthoringCursor.Manuscript.EnvelopeRef,
            context.SourceAgentCursor.Outputs[0].ArtifactRef,
            context.SourceAuthoringCursor.MainTex.ArtifactRef,
            context.SourceAuthoringCursor.Bibliography.ArtifactRef,
            context.SourceAuthoringCursor.EvaluationRef,
            context.SourceAuthoringCursor.ClaimManifestRef,
            context.SourceAuthoringCursor.EligibilityRef,
            context.SourceAuthoringCursor.ManuscriptPlanRef,
            context.SourceAuthoringCursor.CompletionRef,
            context.SourceManuscript.ManuscriptContent.FrontierRef,
            context.SourceAuthoringCursor.PaperId,
            context.SourceAuthoringCursor.TheoryProgramRef,
            context.SourceManuscript.ManuscriptContent.TheoremPackageRef,
            context.SourceManuscript.ManuscriptContent.TheoryAuditRef,
            context.SourceManuscript.ManuscriptContent.LiteratureResearchRef,
            context.SourceManuscript.ManuscriptContent.SelectedReleaseRef,
            context.SourceManuscript.ManuscriptContent.SelectedReleaseDigest,
            context.ExactInputs,
            context.SourceAuthoringCursor.AdmittedAt);
        Validate(dispatch);

        byte[] dispatchBytes = CanonicalJson.Serialize(dispatch);
        string dispatchRef = Reference(dispatchBytes);
        string dispatchPath = DomainArtifactPath(
            root,
            "scientific-editing-dispatches",
            "raw",
            dispatchRef,
            ".json");
        _ = PutImmutable(dispatchPath, dispatchBytes);
        string dispatchRelativePath = RelativePath(root, dispatchPath);
        PaperAgentTask task = BuildScientificEditingTask(
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
            $"scientific-editing-{dispatch.PaperId}-{Hex(taskRef)}.json");
        bool replayed = PutImmutable(taskPath, taskBytes);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile(task.Phase);
        return new PaperScientificEditingAgentTaskStaged(
            PaperScientificEditingAgentSchemas.TaskStaged,
            dispatchRef,
            taskRef,
            taskPath,
            sourceAuthoringTaskRef,
            dispatch.SourceManuscriptRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            replayed);
    }

    public static PaperScientificEditingAgentResultAdmitted AdmitScientificEditingResult(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("scientific-editing");
        if (!string.Equals(task.Phase, profile.Phase, StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, profile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, profile.ContextMode, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an FKST-native scientific-editing task can enter this admission bridge.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperScientificEditingAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Scientific-editing task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = Reference(dispatchBytes);
        PaperScientificEditingAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<
                PaperScientificEditingAgentDispatch>(dispatchBytes);
        Validate(dispatch);
        PaperScientificEditingContext context =
            LoadScientificEditingContext(root, dispatch.SourceAuthoringTaskRef);
        ValidateScientificEditingTaskBinding(
            root,
            task,
            dispatch,
            dispatchRef,
            dispatchInput.RepositoryRelativePath,
            context);

        PaperAgentTaskCursor agentCursor = ReadAgentCursor(root, task, taskRef);
        PaperAgentResultWire result = ReadAgentResult(
            root,
            task,
            taskRef,
            agentCursor.ResultRef);
        RequireCursorMatchesResult(agentCursor, result);
        if (!string.Equals(result.Status, "completed", StringComparison.Ordinal)
            || !string.Equals(result.NextRoute, "journal-research", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a completed scientific edit routed to journal research can be admitted.");
        }

        string cursorPath = ScientificEditingCursorPath(root, taskRef);
        if (File.Exists(cursorPath))
        {
            return ReplayScientificEditingAdmission(
                root,
                ReadScientificEditingCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef,
                context);
        }
        if (agentCursor.Outputs.Count != 1
            || !string.Equals(
                agentCursor.Outputs[0].Schema,
                PaperScientificEditingAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Completed scientific editing must return exactly one structured edit draft.");
        }

        PaperAgentStoredOutput output = agentCursor.Outputs[0];
        byte[] editDraftBytes = ReadAgentOutput(root, output.ArtifactRef);
        PaperScientificEditDraft editDraft =
            PaperResearchInputJson.DeserializeStrict<PaperScientificEditDraft>(
                editDraftBytes);
        PaperScientificManuscriptDraft structuredDraft =
            ValidateScientificEditDraft(
                root,
                editDraft,
                dispatch,
                dispatchRef,
                context,
                result.CompletedAt);
        PaperScientificEditDelta delta = ComputeScientificEditDelta(
            context.SourceDraft,
            editDraft,
            output.ArtifactRef,
            result.CompletedAt);

        PaperManuscriptRenderedSources rendered = RenderSources(
            root,
            structuredDraft,
            context.AuthoringContext);
        ValidateProtectedManuscriptSegments(
            Encoding.UTF8.GetString(context.SourceMainTex),
            Encoding.UTF8.GetString(rendered.MainTexBytes),
            context.AuthoringContext.ClaimManifest);
        if (!rendered.ClaimBindings.SequenceEqual(
                context.SourceManuscript.ManuscriptContent.ClaimBindings))
        {
            throw new InvalidDataException(
                "Scientific editing changed a certified claim binding.");
        }

        PaperManuscriptSourceFile mainTex = StoreSource(
            root,
            "scientifically-edited-main-tex",
            "text/x-tex",
            ".tex",
            rendered.MainTexBytes);
        PaperManuscriptSourceFile bibliography = StoreSource(
            root,
            "scientifically-edited-bibliography",
            "application/x-bibtex",
            ".bib",
            rendered.BibliographyBytes);
        PaperManuscriptAuthoringStoredArtifact storedDelta = StoreDomain(
            root,
            "scientific-edit-deltas",
            delta.Schema,
            delta.DeltaId,
            delta.DeltaContent,
            delta);

        var content = new PaperScientificallyEditedManuscriptContent(
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.SourceAuthoringTaskRef,
            dispatch.SourceManuscriptRef,
            delta.DeltaId,
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
            editDraft.Title,
            mainTex,
            bibliography,
            rendered.ClaimBindings,
            editDraft.Sections.Select(value => value.SectionId).ToArray(),
            rendered.CitationKeys,
            context.AuthoringContext.ClaimManifest.FormalClaimCount,
            context.AuthoringContext.ClaimManifest.InformalItemCount,
            "scientifically-edited-journal-neutral",
            result.CompletedAt);
        var manuscript = new PaperScientificallyEditedManuscript(
            PaperScientificEditingAgentSchemas.EditedManuscript,
            Reference(CanonicalJson.Serialize(content)),
            content);
        Validate(
            manuscript,
            delta,
            context,
            rendered.MainTexBytes,
            rendered.BibliographyBytes);
        PaperManuscriptAuthoringStoredArtifact storedManuscript = StoreDomain(
            root,
            "scientifically-edited-manuscripts",
            manuscript.Schema,
            manuscript.ManuscriptId,
            manuscript.ManuscriptContent,
            manuscript);

        var cursor = new PaperScientificEditingAgentAdmissionCursor(
            PaperScientificEditingAgentSchemas.AdmissionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.SourceAuthoringTaskRef,
            dispatch.SourceManuscriptRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.ClaimManifestRef,
            dispatch.ManuscriptPlanRef,
            storedDelta,
            storedManuscript,
            mainTex,
            bibliography,
            delta.DeltaContent.ChangedProseBlockCount,
            delta.DeltaContent.ChangedProofBlockCount,
            delta.DeltaContent.ChangedSectionIds,
            "journal-research",
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
            return ReplayScientificEditingAdmission(
                root,
                ReadScientificEditingCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef,
                context);
        }
        return ScientificEditingRecorded(cursor, replayed: false);
    }

    public static void Validate(PaperScientificEditingAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperScientificEditingAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        foreach (string digest in new[]
        {
            dispatch.SourceAuthoringTaskRef,
            dispatch.SourceAuthoringResultRef,
            dispatch.SourceAuthoringCursorRef,
            dispatch.SourceManuscriptRef,
            dispatch.SourceManuscriptEnvelopeRef,
            dispatch.SourceDraftRef,
            dispatch.SourceMainTexRef,
            dispatch.SourceBibliographyRef,
            dispatch.EvaluationRef,
            dispatch.ClaimManifestRef,
            dispatch.EligibilityRef,
            dispatch.ManuscriptPlanRef,
            dispatch.CompletionRef,
            dispatch.FrontierRef,
            dispatch.TheoryProgramRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
            dispatch.LiteratureResearchRef,
            dispatch.SelectedReleaseRef,
            dispatch.SelectedReleaseDigest
        })
        {
            RequireDigest(digest, nameof(dispatch));
        }
        RequirePaperId(dispatch.PaperId);
        if (dispatch.ExactInputs is null
            || dispatch.ExactInputs.Count != ScientificEditingExactInputCount)
        {
            throw new InvalidDataException(
                $"Scientific-editing dispatch must contain exactly {ScientificEditingExactInputCount} inputs.");
        }
        RequireExactInputs(dispatch.ExactInputs);
        string[] requiredRefs =
        [
            dispatch.SourceAuthoringCursorRef,
            dispatch.SourceManuscriptEnvelopeRef,
            dispatch.SourceDraftRef,
            dispatch.SourceMainTexRef,
            dispatch.SourceBibliographyRef,
            dispatch.EvaluationRef,
            dispatch.ClaimManifestRef,
            dispatch.EligibilityRef,
            dispatch.ManuscriptPlanRef,
            dispatch.CompletionRef,
            dispatch.TheoryProgramRef,
            dispatch.TheoremPackageRef,
            dispatch.TheoryAuditRef,
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
                    "Scientific-editing dispatch omitted a required exact evidence reference.");
            }
        }
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    public static void Validate(PaperScientificEditDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        RequireExact(
            delta.Schema,
            PaperScientificEditingAgentSchemas.Delta,
            nameof(delta.Schema));
        PaperScientificEditDeltaContent content = delta.DeltaContent
            ?? throw new InvalidDataException("Scientific edit delta content is required.");
        RequireIdentity(delta.DeltaId, content, nameof(delta.DeltaId));
        RequireDigest(content.SourceManuscriptRef, nameof(content.SourceManuscriptRef));
        RequireDigest(content.SourceDraftRef, nameof(content.SourceDraftRef));
        RequireDigest(content.EditedDraftRef, nameof(content.EditedDraftRef));
        RequireStringList(
            content.ChangedSectionIds,
            nameof(content.ChangedSectionIds),
            minimum: 3,
            maximum: 8,
            maximumItemLength: 64);
        RequireStringList(
            content.SubstantiveDimensions,
            nameof(content.SubstantiveDimensions),
            minimum: 3,
            maximum: ScientificEditDimensions.Count,
            maximumItemLength: 64);
        if (content.SubstantiveDimensions.Any(value =>
                !ScientificEditDimensions.Contains(value))
            || content.ChangedProseBlockCount < 2
            || content.ChangedProofBlockCount < 1
            || content.ChangedProseBlockCount + content.ChangedProofBlockCount < 3
            || !content.ClaimIdentityPreserved
            || !content.EvidenceBoundaryPreserved
            || !content.Passed)
        {
            throw new InvalidDataException(
                "Scientific edit delta does not demonstrate a claim-preserving substantive revision.");
        }
        foreach (string required in new[]
        {
            "contribution-framing",
            "proof-exposition",
            "limitations-and-implications"
        })
        {
            if (!content.SubstantiveDimensions.Contains(required, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Scientific editing must substantively address {required}.");
            }
        }
        ParseUtc(content.ComputedAt, nameof(content.ComputedAt));
    }

    public static void Validate(PaperScientificEditingAgentAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperScientificEditingAgentSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        foreach (string digest in new[]
        {
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.SourceAuthoringTaskRef,
            cursor.SourceManuscriptRef,
            cursor.TheoryProgramRef,
            cursor.ClaimManifestRef,
            cursor.ManuscriptPlanRef
        })
        {
            RequireDigest(digest, nameof(cursor));
        }
        RequirePaperId(cursor.PaperId);
        ValidateStoredArtifact(cursor.EditDelta, PaperScientificEditingAgentSchemas.Delta);
        ValidateStoredArtifact(
            cursor.EditedManuscript,
            PaperScientificEditingAgentSchemas.EditedManuscript);
        ValidateSourceCoordinate(
            cursor.MainTex,
            "scientifically-edited-main-tex",
            "text/x-tex");
        ValidateSourceCoordinate(
            cursor.Bibliography,
            "scientifically-edited-bibliography",
            "application/x-bibtex");
        RequireStringList(
            cursor.ChangedSectionIds,
            nameof(cursor.ChangedSectionIds),
            minimum: 3,
            maximum: 8,
            maximumItemLength: 64);
        if (cursor.ChangedProseBlockCount < 2
            || cursor.ChangedProofBlockCount < 1
            || !string.Equals(cursor.NextRoute, "journal-research", StringComparison.Ordinal)
            || !ProvenanceValues.Contains(cursor.Provenance))
        {
            throw new InvalidDataException(
                "Scientific-editing cursor progress, route, or provenance is invalid.");
        }
        RequireRunId(cursor.RunId);
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    internal static void Validate(
        PaperScientificallyEditedManuscript manuscript,
        PaperScientificEditDelta delta,
        PaperScientificEditingContext context,
        ReadOnlySpan<byte> mainTexBytes,
        ReadOnlySpan<byte> bibliographyBytes)
    {
        ArgumentNullException.ThrowIfNull(manuscript);
        RequireExact(
            manuscript.Schema,
            PaperScientificEditingAgentSchemas.EditedManuscript,
            nameof(manuscript.Schema));
        PaperScientificallyEditedManuscriptContent content =
            manuscript.ManuscriptContent
            ?? throw new InvalidDataException(
                "Scientifically edited manuscript content is required.");
        RequireIdentity(
            manuscript.ManuscriptId,
            content,
            nameof(manuscript.ManuscriptId));
        Validate(delta);
        foreach (string digest in new[]
        {
            content.TaskRef,
            content.ResultRef,
            content.DispatchRef,
            content.SourceAuthoringTaskRef,
            content.SourceManuscriptRef,
            content.EditDeltaRef,
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
        ValidateSourceFile(
            content.MainTex,
            mainTexBytes,
            "scientifically-edited-main-tex",
            "text/x-tex");
        ValidateSourceFile(
            content.Bibliography,
            bibliographyBytes,
            "scientifically-edited-bibliography",
            "application/x-bibtex");
        ValidateClaimBindings(
            content.ClaimBindings,
            context.AuthoringContext.ClaimManifest);
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
        if (!string.Equals(content.SourceAuthoringTaskRef, context.SourceTask.Schema == PaperAgentSchemas.Task ? context.SourceAuthoringCursor.TaskRef : string.Empty, StringComparison.Ordinal)
            || !string.Equals(content.SourceManuscriptRef, context.SourceManuscript.ManuscriptId, StringComparison.Ordinal)
            || !string.Equals(content.EditDeltaRef, delta.DeltaId, StringComparison.Ordinal)
            || !string.Equals(content.CompletionRef, context.SourceAuthoringCursor.CompletionRef, StringComparison.Ordinal)
            || !string.Equals(content.EvaluationRef, context.SourceAuthoringCursor.EvaluationRef, StringComparison.Ordinal)
            || !string.Equals(content.ClaimManifestRef, context.SourceAuthoringCursor.ClaimManifestRef, StringComparison.Ordinal)
            || !string.Equals(content.EligibilityRef, context.SourceAuthoringCursor.EligibilityRef, StringComparison.Ordinal)
            || !string.Equals(content.ManuscriptPlanRef, context.SourceAuthoringCursor.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(content.FrontierRef, context.SourceManuscript.ManuscriptContent.FrontierRef, StringComparison.Ordinal)
            || !string.Equals(content.PaperId, context.SourceAuthoringCursor.PaperId, StringComparison.Ordinal)
            || !string.Equals(content.TheoryProgramRef, context.SourceAuthoringCursor.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(content.TheoremPackageRef, context.SourceManuscript.ManuscriptContent.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(content.TheoryAuditRef, context.SourceManuscript.ManuscriptContent.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(content.LiteratureResearchRef, context.SourceManuscript.ManuscriptContent.LiteratureResearchRef, StringComparison.Ordinal)
            || !string.Equals(content.SelectedReleaseRef, context.SourceManuscript.ManuscriptContent.SelectedReleaseRef, StringComparison.Ordinal)
            || !string.Equals(content.SelectedReleaseDigest, context.SourceManuscript.ManuscriptContent.SelectedReleaseDigest, StringComparison.Ordinal)
            || !string.Equals(content.Title, context.SourceManuscript.ManuscriptContent.Title, StringComparison.Ordinal)
            || content.FormalClaimCount != context.SourceManuscript.ManuscriptContent.FormalClaimCount
            || content.InformalItemCount != context.SourceManuscript.ManuscriptContent.InformalItemCount
            || !string.Equals(content.EditingStatus, "scientifically-edited-journal-neutral", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientifically edited manuscript changed its certified source lineage.");
        }
        if (!content.ClaimBindings.SequenceEqual(
                context.SourceManuscript.ManuscriptContent.ClaimBindings))
        {
            throw new InvalidDataException(
                "Scientifically edited manuscript changed certified claim bindings.");
        }
        ParseUtc(content.CreatedAt, nameof(content.CreatedAt));
        ValidateRenderedBinding(
            Encoding.UTF8.GetString(mainTexBytes),
            context.AuthoringContext,
            content.ClaimBindings,
            content.CitationKeys);
        ValidateProtectedManuscriptSegments(
            Encoding.UTF8.GetString(context.SourceMainTex),
            Encoding.UTF8.GetString(mainTexBytes),
            context.AuthoringContext.ClaimManifest);
    }

    private static PaperAgentTask BuildScientificEditingTask(
        PaperScientificEditingAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperScientificEditingContext context)
    {
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("scientific-editing");
        PaperAgentInputArtifact[] inputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperScientificEditingAgentSchemas.Dispatch,
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
                PaperScientificEditingAgentSchemas.Draft,
                "outputs/scientific-edit-draft.json")],
            ["journal-research", "scientific-editing", "blocked"],
            BuildScientificEditingInstruction(dispatch, context),
            ScientificEditingForbiddenShortcuts(),
            dispatch.RequestedAt);
    }

    private static string BuildScientificEditingInstruction(
        PaperScientificEditingAgentDispatch dispatch,
        PaperScientificEditingContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Perform one deep scientific editing pass on the supplied journal-neutral mathematical manuscript.");
        builder.AppendLine("Write exactly one paper-scientific-edit-draft.v1 JSON object to outputs/scientific-edit-draft.json.");
        builder.AppendLine($"Use dispatch_ref={Reference(CanonicalJson.Serialize(dispatch))}, source_manuscript_ref={dispatch.SourceManuscriptRef}, claim_manifest_ref={dispatch.ClaimManifestRef}, and manuscript_plan_ref={dispatch.ManuscriptPlanRef}.");
        builder.AppendLine($"Preserve paper_id={dispatch.PaperId}, theory_program_ref={dispatch.TheoryProgramRef}, and title={context.SourceDraft.Title}.");
        builder.AppendLine("Return the same eight repository-owned sections and the same block shape as the source structured draft. Keep every formal-claim and informal-item anchor unchanged. Keep each proof block bound to the same claim ID.");
        builder.AppendLine("Improve the scientific contribution framing, theorem dependency narrative, proof exposition, prior-work boundary, formal provenance explanation, limitations, and implications using only the exact supplied evidence.");
        builder.AppendLine("At minimum, revise two prose blocks, one proof block, and three sections. The actual edit dimensions are recomputed by the repository and must include contribution-framing, proof-exposition, and limitations-and-implications.");
        builder.AppendLine("Citations may use only evidence-bound related-work indexes already authorized by the literature artifact. The repository regenerates the bibliography and rejects unsupported metadata.");
        builder.AppendLine("List the exact edit_dimensions, at least three concise revision_summary entries, and any remaining_risks. The completed result must route to journal-research.");
        builder.AppendLine("Do not emit a complete LaTeX document. The repository regenerates theorem environments, proof wrappers, labels, provenance markers, and document-level commands.");
        return builder.ToString();
    }

    private static string[] ScientificEditingForbiddenShortcuts() =>
    [
        "Do not add, omit, reorder, merge, split, weaken, strengthen, paraphrase, or reclassify a formal claim.",
        "Do not change a formal-claim target, proof target, informal-item target, section identity, section title, or block shape.",
        "Do not alter any theorem statement, LaTeX label, certified-claim reference, GID, statement ID, requested-statement digest, selected truth release, or axiom closure.",
        "Do not invent a citation, theorem, experiment, numerical result, application, limitation, or comparison absent from the exact evidence.",
        "Do not write theorem, lemma, proposition, corollary, definition, example, remark, proof, section, label, macro, file-I/O, or bibliography commands.",
        "Do not run Lean, Formalize, Git, GitHub, network access, journal selection, peer review, language editing, or cover-letter authoring.",
        "Do not claim an edit dimension that is not witnessed by changed source blocks. Repository validation computes the delta."
    ];

    private static PaperScientificManuscriptDraft ValidateScientificEditDraft(
        string root,
        PaperScientificEditDraft edit,
        PaperScientificEditingAgentDispatch dispatch,
        string dispatchRef,
        PaperScientificEditingContext context,
        string completedAt)
    {
        ArgumentNullException.ThrowIfNull(edit);
        RequireExact(edit.Schema, PaperScientificEditingAgentSchemas.Draft, nameof(edit.Schema));
        if (!string.Equals(edit.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(edit.SourceManuscriptRef, dispatch.SourceManuscriptRef, StringComparison.Ordinal)
            || !string.Equals(edit.ClaimManifestRef, dispatch.ClaimManifestRef, StringComparison.Ordinal)
            || !string.Equals(edit.ManuscriptPlanRef, dispatch.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(edit.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(edit.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(edit.Title, context.SourceDraft.Title, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific edit changed its dispatch, source manuscript, certified plan, paper, program, or title.");
        }
        RequireStringList(
            edit.EditDimensions,
            nameof(edit.EditDimensions),
            minimum: 3,
            maximum: ScientificEditDimensions.Count,
            maximumItemLength: 64);
        if (edit.EditDimensions.Any(value => !ScientificEditDimensions.Contains(value)))
        {
            throw new InvalidDataException(
                "Scientific edit declared an unsupported edit dimension.");
        }
        RequireStringList(
            edit.RevisionSummary,
            nameof(edit.RevisionSummary),
            minimum: 3,
            maximum: 16,
            maximumItemLength: 2048);
        RequireStringList(
            edit.RemainingRisks,
            nameof(edit.RemainingRisks),
            minimum: 0,
            maximum: 16,
            maximumItemLength: 2048);
        ValidateScientificDraftShape(context.SourceDraft.Sections, edit.Sections);

        var structured = new PaperScientificManuscriptDraft(
            PaperManuscriptAuthoringAgentSchemas.Draft,
            context.SourceAuthoringCursor.DispatchRef,
            dispatch.ClaimManifestRef,
            dispatch.ManuscriptPlanRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            edit.Title,
            edit.AbstractLatex,
            edit.Keywords,
            edit.Sections,
            edit.References,
            edit.CreatedAt);
        ValidateDraft(
            root,
            structured,
            context.SourceDispatch,
            context.SourceAuthoringCursor.DispatchRef,
            context.AuthoringContext,
            completedAt);
        DateTimeOffset requested = ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
        DateTimeOffset created = ParseUtc(edit.CreatedAt, nameof(edit.CreatedAt));
        DateTimeOffset completed = ParseUtc(completedAt, nameof(completedAt));
        if (created < requested || created > completed)
        {
            throw new InvalidDataException(
                "Scientific edit created_at must lie between task request and completion.");
        }
        return structured;
    }

    private static void ValidateScientificDraftShape(
        IReadOnlyList<PaperManuscriptDraftSection> source,
        IReadOnlyList<PaperManuscriptDraftSection> edited)
    {
        if (source.Count != edited.Count)
        {
            throw new InvalidDataException(
                "Scientific editing cannot change the repository-owned section count.");
        }
        for (int sectionIndex = 0; sectionIndex < source.Count; sectionIndex++)
        {
            PaperManuscriptDraftSection before = source[sectionIndex];
            PaperManuscriptDraftSection after = edited[sectionIndex];
            if (before.Order != after.Order
                || !string.Equals(before.SectionId, after.SectionId, StringComparison.Ordinal)
                || !string.Equals(before.Title, after.Title, StringComparison.Ordinal)
                || before.Blocks.Count != after.Blocks.Count)
            {
                throw new InvalidDataException(
                    "Scientific editing cannot change section identity, title, order, or block count.");
            }
            for (int blockIndex = 0; blockIndex < before.Blocks.Count; blockIndex++)
            {
                PaperManuscriptDraftBlock oldBlock = before.Blocks[blockIndex];
                PaperManuscriptDraftBlock newBlock = after.Blocks[blockIndex];
                if (oldBlock.Order != newBlock.Order
                    || !string.Equals(oldBlock.Kind, newBlock.Kind, StringComparison.Ordinal)
                    || !string.Equals(oldBlock.TargetId, newBlock.TargetId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Scientific editing cannot change block order, kind, or target identity.");
                }
                if (oldBlock.Kind is PaperManuscriptDraftBlockKinds.FormalClaim
                    or PaperManuscriptDraftBlockKinds.InformalItem
                    && !string.Equals(oldBlock.Latex, newBlock.Latex, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Scientific editing cannot add text to repository-owned claim anchors.");
                }
            }
        }
    }

    private static PaperScientificEditDelta ComputeScientificEditDelta(
        PaperScientificManuscriptDraft source,
        PaperScientificEditDraft edited,
        string editedDraftRef,
        string computedAt)
    {
        var changedSections = new HashSet<string>(StringComparer.Ordinal);
        int proseChanges = 0;
        int proofChanges = 0;
        for (int sectionIndex = 0; sectionIndex < source.Sections.Count; sectionIndex++)
        {
            PaperManuscriptDraftSection before = source.Sections[sectionIndex];
            PaperManuscriptDraftSection after = edited.Sections[sectionIndex];
            for (int blockIndex = 0; blockIndex < before.Blocks.Count; blockIndex++)
            {
                PaperManuscriptDraftBlock oldBlock = before.Blocks[blockIndex];
                PaperManuscriptDraftBlock newBlock = after.Blocks[blockIndex];
                if (string.Equals(oldBlock.Latex, newBlock.Latex, StringComparison.Ordinal))
                {
                    continue;
                }
                changedSections.Add(before.SectionId);
                if (oldBlock.Kind == PaperManuscriptDraftBlockKinds.Prose)
                {
                    proseChanges++;
                }
                else if (oldBlock.Kind == PaperManuscriptDraftBlockKinds.Proof)
                {
                    proofChanges++;
                }
            }
        }
        bool abstractChanged = !string.Equals(
            source.AbstractLatex,
            edited.AbstractLatex,
            StringComparison.Ordinal);
        bool keywordsChanged = !source.Keywords.SequenceEqual(
            edited.Keywords,
            StringComparer.Ordinal);
        bool citationSetChanged = !source.References.SequenceEqual(edited.References);
        if (abstractChanged)
        {
            changedSections.Add("introduction");
        }
        var dimensions = new HashSet<string>(StringComparer.Ordinal);
        if (abstractChanged
            || changedSections.Contains("introduction")
            || changedSections.Contains("main-results"))
        {
            dimensions.Add("contribution-framing");
        }
        if (citationSetChanged || changedSections.Contains("prior-work"))
        {
            dimensions.Add("literature-boundary");
        }
        if (changedSections.Contains("setting")
            || changedSections.Contains("main-results"))
        {
            dimensions.Add("logical-sequencing");
        }
        if (proofChanges > 0 || changedSections.Contains("proof-architecture"))
        {
            dimensions.Add("proof-exposition");
        }
        if (changedSections.Contains("formalization"))
        {
            dimensions.Add("provenance-exposition");
        }
        if (changedSections.Contains("boundaries")
            || changedSections.Contains("discussion"))
        {
            dimensions.Add("limitations-and-implications");
        }
        string[] normalizedDimensions = dimensions
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] declaredDimensions = edited.EditDimensions
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!normalizedDimensions.SequenceEqual(declaredDimensions, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific edit dimensions do not match the repository-computed revision delta.");
        }
        var content = new PaperScientificEditDeltaContent(
            edited.SourceManuscriptRef,
            Reference(CanonicalJson.Serialize(source)),
            editedDraftRef,
            changedSections.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            proseChanges,
            proofChanges,
            abstractChanged,
            keywordsChanged,
            citationSetChanged,
            normalizedDimensions,
            true,
            true,
            proseChanges >= 2
                && proofChanges >= 1
                && changedSections.Count >= 3
                && normalizedDimensions.Length >= 3,
            computedAt);
        var delta = new PaperScientificEditDelta(
            PaperScientificEditingAgentSchemas.Delta,
            Reference(CanonicalJson.Serialize(content)),
            content);
        Validate(delta);
        return delta;
    }

    private static void ValidateProtectedManuscriptSegments(
        string source,
        string edited,
        PaperCertifiedClaimManifest manifest)
    {
        foreach (PaperCertifiedClaimManifestEntry claim in manifest.FormalClaims)
        {
            string begin = FormalBeginMarker(claim);
            string end = FormalEndMarker(claim);
            if (!string.Equals(
                    ProtectedSegment(source, begin, end),
                    ProtectedSegment(edited, begin, end),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Scientific editing changed protected formal claim {claim.ClaimId}.");
            }
        }
        foreach (PaperCertifiedClaimManifestInformalEntry item in manifest.InformalExposition)
        {
            string begin = InformalBeginMarker(item);
            string end = InformalEndMarker(item);
            if (!string.Equals(
                    ProtectedSegment(source, begin, end),
                    ProtectedSegment(edited, begin, end),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Scientific editing changed protected informal item {item.ItemId}.");
            }
        }
    }

    private static string ProtectedSegment(
        string source,
        string beginMarker,
        string endMarker)
    {
        int begin = source.IndexOf(beginMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, StringComparison.Ordinal);
        if (begin < 0 || end < begin
            || begin != source.LastIndexOf(beginMarker, StringComparison.Ordinal)
            || end != source.LastIndexOf(endMarker, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Protected manuscript marker is absent, duplicated, or out of order.");
        }
        int finish = end + endMarker.Length;
        return source[begin..finish];
    }
}
