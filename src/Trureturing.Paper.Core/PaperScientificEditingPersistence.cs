namespace Trureturing.Paper.Core;

public static partial class PaperManuscriptAuthoringAgentService
{
    private static PaperScientificEditingContext LoadScientificEditingContext(
        string root,
        string sourceAuthoringTaskRef)
    {
        PaperAgentTask sourceTask = ReadRegisteredTask(
            root,
            sourceAuthoringTaskRef);
        PaperAgentProfile authoringProfile =
            PaperAgentRuntimeService.GetProfile("manuscript-authoring");
        if (!string.Equals(sourceTask.Phase, authoringProfile.Phase, StringComparison.Ordinal)
            || !string.Equals(sourceTask.AgentRole, authoringProfile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(sourceTask.ContextMode, authoringProfile.ContextMode, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific editing requires an admitted manuscript-authoring source task.");
        }
        PaperAgentTaskCursor sourceAgentCursor = ReadAgentCursor(
            root,
            sourceTask,
            sourceAuthoringTaskRef);
        PaperAgentResultWire sourceResult = ReadAgentResult(
            root,
            sourceTask,
            sourceAuthoringTaskRef,
            sourceAgentCursor.ResultRef);
        RequireCursorMatchesResult(sourceAgentCursor, sourceResult);
        if (!string.Equals(sourceResult.Status, "completed", StringComparison.Ordinal)
            || !string.Equals(sourceResult.NextRoute, "scientific-editing", StringComparison.Ordinal)
            || sourceAgentCursor.Outputs.Count != 1
            || !string.Equals(
                sourceAgentCursor.Outputs[0].Schema,
                PaperManuscriptAuthoringAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific editing requires one completed manuscript-authoring result routed to scientific editing.");
        }

        string sourceCursorPath = AdmissionCursorPath(root, sourceAuthoringTaskRef);
        byte[] sourceCursorBytes = ReadBoundedFile(
            sourceCursorPath,
            MaximumControlBytes,
            "Manuscript-authoring admission cursor");
        string sourceCursorRef = Reference(sourceCursorBytes);
        PaperManuscriptAuthoringAgentAdmissionCursor sourceCursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperManuscriptAuthoringAgentAdmissionCursor>(sourceCursorBytes);
        Validate(sourceCursor);
        if (!string.Equals(sourceCursor.TaskRef, sourceAuthoringTaskRef, StringComparison.Ordinal)
            || !string.Equals(sourceCursor.ResultRef, sourceAgentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(sourceCursor.NextRoute, "scientific-editing", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript-authoring admission cursor changed the scientific-editing source identity.");
        }

        string sourceDispatchPath = DomainArtifactPath(
            root,
            "dispatches",
            "raw",
            sourceCursor.DispatchRef,
            ".json");
        byte[] sourceDispatchBytes = ReadImmutable(
            sourceDispatchPath,
            sourceCursor.DispatchRef,
            "Manuscript-authoring dispatch");
        PaperManuscriptAuthoringAgentDispatch sourceDispatch =
            PaperResearchInputJson.DeserializeStrict<
                PaperManuscriptAuthoringAgentDispatch>(sourceDispatchBytes);
        Validate(sourceDispatch);
        PaperManuscriptAuthoringContext authoringContext =
            PaperFrontierNodeSelectionService.LoadManuscriptAuthoringContext(
                root,
                sourceDispatch.EvaluationRef,
                sourceDispatch.ClaimManifestRef,
                sourceDispatch.EligibilityRef);
        ValidateTaskBinding(
            root,
            sourceTask,
            sourceDispatch,
            sourceCursor.DispatchRef,
            RelativePath(root, sourceDispatchPath),
            authoringContext);

        PaperAgentStoredOutput sourceDraftOutput = sourceAgentCursor.Outputs[0];
        byte[] sourceDraftBytes = ReadAgentOutput(
            root,
            sourceDraftOutput.ArtifactRef);
        PaperScientificManuscriptDraft sourceDraft =
            PaperResearchInputJson.DeserializeStrict<
                PaperScientificManuscriptDraft>(sourceDraftBytes);
        ValidateDraft(
            root,
            sourceDraft,
            sourceDispatch,
            sourceCursor.DispatchRef,
            authoringContext,
            sourceAgentCursor.CompletedAt);

        byte[] sourceMainTex = ReadSource(root, sourceCursor.MainTex);
        byte[] sourceBibliography = ReadSource(root, sourceCursor.Bibliography);
        byte[] manuscriptEnvelopeBytes = ReadImmutable(
            ResolveRepositoryFile(
                root,
                sourceCursor.Manuscript.EnvelopePath,
                "Scientific manuscript envelope"),
            sourceCursor.Manuscript.EnvelopeRef,
            "Scientific manuscript envelope");
        PaperScientificManuscript sourceManuscript =
            PaperResearchInputJson.DeserializeStrict<PaperScientificManuscript>(
                manuscriptEnvelopeBytes);
        if (!string.Equals(
                sourceManuscript.ManuscriptId,
                sourceCursor.Manuscript.ArtifactRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific-editing source manuscript envelope changed its semantic identity.");
        }
        Validate(
            sourceManuscript,
            authoringContext,
            sourceMainTex,
            sourceBibliography);
        if (!string.Equals(
                sourceManuscript.ManuscriptContent.MainTex.ArtifactRef,
                sourceCursor.MainTex.ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(
                sourceManuscript.ManuscriptContent.Bibliography.ArtifactRef,
                sourceCursor.Bibliography.ArtifactRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific-editing source manuscript changed its admitted source files.");
        }

        PaperAgentInputArtifact[] exactInputs = sourceDispatch.ExactInputs
            .Concat(
            [
                new PaperAgentInputArtifact(
                    PaperManuscriptAuthoringAgentSchemas.AdmissionCursor,
                    sourceCursorRef,
                    RelativePath(root, sourceCursorPath)),
                new PaperAgentInputArtifact(
                    PaperManuscriptAuthoringAgentSchemas.Draft,
                    sourceDraftOutput.ArtifactRef,
                    RelativePath(
                        root,
                        AgentArtifactPath(
                            root,
                            "outputs",
                            sourceDraftOutput.ArtifactRef))),
                new PaperAgentInputArtifact(
                    PaperManuscriptAuthoringAgentSchemas.ScientificManuscript,
                    sourceCursor.Manuscript.EnvelopeRef,
                    sourceCursor.Manuscript.EnvelopePath),
                new PaperAgentInputArtifact(
                    "paper-manuscript-main-tex.v1",
                    sourceCursor.MainTex.ArtifactRef,
                    sourceCursor.MainTex.RepositoryRelativePath),
                new PaperAgentInputArtifact(
                    "paper-manuscript-bibliography.v1",
                    sourceCursor.Bibliography.ArtifactRef,
                    sourceCursor.Bibliography.RepositoryRelativePath)
            ])
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        if (exactInputs.Length != ScientificEditingExactInputCount
            || exactInputs.Select(value => value.ArtifactRef).Distinct(StringComparer.Ordinal).Count()
                != exactInputs.Length
            || exactInputs.Select(value => value.RepositoryRelativePath).Distinct(StringComparer.Ordinal).Count()
                != exactInputs.Length)
        {
            throw new InvalidDataException(
                "Scientific-editing exact evidence closure is incomplete or contains duplicates.");
        }
        foreach (PaperAgentInputArtifact input in exactInputs)
        {
            _ = ReadExactInput(root, input);
        }

        return new PaperScientificEditingContext(
            sourceTask,
            sourceAgentCursor,
            sourceCursor,
            sourceDispatch,
            authoringContext,
            sourceDraft,
            sourceManuscript,
            sourceMainTex,
            sourceBibliography,
            sourceCursorRef,
            exactInputs);
    }

    private static void ValidateScientificEditingTaskBinding(
        string root,
        PaperAgentTask task,
        PaperScientificEditingAgentDispatch dispatch,
        string dispatchRef,
        string dispatchPath,
        PaperScientificEditingContext context)
    {
        PaperAgentRuntimeService.Validate(task);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("scientific-editing");
        if (!string.Equals(task.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(task.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(task.Phase, profile.Phase, StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, profile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, profile.ContextMode, StringComparison.Ordinal)
            || !string.Equals(task.RequestedAt, dispatch.RequestedAt, StringComparison.Ordinal)
            || task.ExpectedOutputs.Count != 1
            || !string.Equals(
                task.ExpectedOutputs[0].Schema,
                PaperScientificEditingAgentSchemas.Draft,
                StringComparison.Ordinal)
            || !string.Equals(
                task.ExpectedOutputs[0].WorkspaceRelativePath,
                "outputs/scientific-edit-draft.json",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific-editing task changed its phase, source identity, timestamp, or output contract.");
        }
        string[] expectedRoutes = ["blocked", "journal-research", "scientific-editing"];
        if (!task.AllowedNextRoutes
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expectedRoutes, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific-editing task changed its closed route set.");
        }
        PaperAgentInputArtifact[] expectedInputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperScientificEditingAgentSchemas.Dispatch,
                dispatchRef,
                dispatchPath))
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        if (task.ExactInputs.Count != expectedInputs.Length)
        {
            throw new InvalidDataException(
                "Scientific-editing task changed its exact input count.");
        }
        for (int index = 0; index < expectedInputs.Length; index++)
        {
            PaperAgentInputArtifact expected = expectedInputs[index];
            PaperAgentInputArtifact actual = task.ExactInputs[index];
            if (!string.Equals(actual.Schema, expected.Schema, StringComparison.Ordinal)
                || !string.Equals(actual.ArtifactRef, expected.ArtifactRef, StringComparison.Ordinal)
                || !string.Equals(
                    actual.RepositoryRelativePath,
                    expected.RepositoryRelativePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Scientific-editing task changed its exact evidence closure.");
            }
            _ = ReadExactInput(root, actual);
        }
        if (!string.Equals(dispatch.SourceAuthoringResultRef, context.SourceAgentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SourceAuthoringCursorRef, context.SourceAuthoringCursorRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SourceManuscriptRef, context.SourceManuscript.ManuscriptId, StringComparison.Ordinal)
            || !string.Equals(dispatch.SourceManuscriptEnvelopeRef, context.SourceAuthoringCursor.Manuscript.EnvelopeRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SourceDraftRef, context.SourceAgentCursor.Outputs[0].ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SourceMainTexRef, context.SourceAuthoringCursor.MainTex.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SourceBibliographyRef, context.SourceAuthoringCursor.Bibliography.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.EvaluationRef, context.SourceAuthoringCursor.EvaluationRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.ClaimManifestRef, context.SourceAuthoringCursor.ClaimManifestRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.EligibilityRef, context.SourceAuthoringCursor.EligibilityRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.ManuscriptPlanRef, context.SourceAuthoringCursor.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.CompletionRef, context.SourceAuthoringCursor.CompletionRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.FrontierRef, context.SourceManuscript.ManuscriptContent.FrontierRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.PaperId, context.SourceAuthoringCursor.PaperId, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryProgramRef, context.SourceAuthoringCursor.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoremPackageRef, context.SourceManuscript.ManuscriptContent.TheoremPackageRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryAuditRef, context.SourceManuscript.ManuscriptContent.TheoryAuditRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.LiteratureResearchRef, context.SourceManuscript.ManuscriptContent.LiteratureResearchRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SelectedReleaseRef, context.SourceManuscript.ManuscriptContent.SelectedReleaseRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SelectedReleaseDigest, context.SourceManuscript.ManuscriptContent.SelectedReleaseDigest, StringComparison.Ordinal)
            || !dispatch.ExactInputs.SequenceEqual(context.ExactInputs))
        {
            throw new InvalidDataException(
                "Scientific-editing task dispatch changed its admitted manuscript lineage.");
        }
    }

    private static PaperScientificEditingAgentResultAdmitted ReplayScientificEditingAdmission(
        string root,
        PaperScientificEditingAgentAdmissionCursor cursor,
        string taskRef,
        PaperAgentTaskCursor agentCursor,
        PaperScientificEditingAgentDispatch dispatch,
        string dispatchRef,
        PaperScientificEditingContext context)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, taskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.SourceAuthoringTaskRef, dispatch.SourceAuthoringTaskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.SourceManuscriptRef, dispatch.SourceManuscriptRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ClaimManifestRef, dispatch.ClaimManifestRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ManuscriptPlanRef, dispatch.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(cursor.RunId, agentCursor.RunId, StringComparison.Ordinal)
            || !string.Equals(cursor.Provenance, agentCursor.Provenance, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific-editing replay changed task, source manuscript, evidence, or run identity.");
        }
        PaperScientificEditDelta delta =
            ReadScientificEditingStoredEnvelope<PaperScientificEditDelta>(
                root,
                cursor.EditDelta,
                PaperScientificEditingAgentSchemas.Delta,
                "Scientific edit delta");
        PaperScientificallyEditedManuscript manuscript =
            ReadScientificEditingStoredEnvelope<PaperScientificallyEditedManuscript>(
                root,
                cursor.EditedManuscript,
                PaperScientificEditingAgentSchemas.EditedManuscript,
                "Scientifically edited manuscript");
        byte[] mainTex = ReadSource(root, cursor.MainTex);
        byte[] bibliography = ReadSource(root, cursor.Bibliography);
        if (!string.Equals(delta.DeltaId, cursor.EditDelta.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(manuscript.ManuscriptId, cursor.EditedManuscript.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(manuscript.ManuscriptContent.MainTex.ArtifactRef, cursor.MainTex.ArtifactRef, StringComparison.Ordinal)
            || !string.Equals(manuscript.ManuscriptContent.Bibliography.ArtifactRef, cursor.Bibliography.ArtifactRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific-editing replay changed stored artifact identity.");
        }
        Validate(manuscript, delta, context, mainTex, bibliography);
        return ScientificEditingRecorded(cursor, replayed: true);
    }

    private static T ReadScientificEditingStoredEnvelope<T>(
        string root,
        PaperManuscriptAuthoringStoredArtifact stored,
        string expectedSchema,
        string name)
    {
        ValidateStoredArtifact(stored, expectedSchema);
        byte[] bytes = ReadImmutable(
            ResolveRepositoryFile(root, stored.EnvelopePath, name),
            stored.EnvelopeRef,
            name);
        return PaperResearchInputJson.DeserializeStrict<T>(bytes);
    }

    private static PaperScientificEditingAgentAdmissionCursor ReadScientificEditingCursor(
        string path)
    {
        PaperScientificEditingAgentAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperScientificEditingAgentAdmissionCursor>(
                    ReadBoundedFile(
                        path,
                        MaximumControlBytes,
                        "Scientific-editing admission cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperScientificEditingAgentResultAdmitted ScientificEditingRecorded(
        PaperScientificEditingAgentAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperScientificEditingAgentSchemas.ResultAdmitted,
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.SourceAuthoringTaskRef,
            cursor.SourceManuscriptRef,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.ClaimManifestRef,
            cursor.ManuscriptPlanRef,
            cursor.EditDelta,
            cursor.EditedManuscript,
            cursor.MainTex,
            cursor.Bibliography,
            cursor.ChangedProseBlockCount,
            cursor.ChangedProofBlockCount,
            cursor.ChangedSectionIds,
            cursor.NextRoute,
            cursor.RunId,
            cursor.Provenance,
            cursor.AdmittedAt,
            replayed);

    private static string ScientificEditingCursorPath(
        string root,
        string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-scientific-editing",
            "cursors",
            Hex(taskRef) + ".json");
}
