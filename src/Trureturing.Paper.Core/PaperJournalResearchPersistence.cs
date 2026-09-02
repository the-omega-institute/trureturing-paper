namespace Trureturing.Paper.Core;

internal sealed record PaperJournalResearchContext(
    PaperAgentTask SourceTask,
    PaperAgentTaskCursor SourceAgentCursor,
    PaperScientificEditingAgentAdmissionCursor SourceScientificCursor,
    PaperScientificEditingAgentDispatch SourceScientificDispatch,
    PaperScientificEditingContext ScientificEditingContext,
    PaperScientificEditDraft SourceEditDraft,
    PaperScientificManuscriptDraft StructuredEditedDraft,
    PaperScientificEditDelta SourceEditDelta,
    PaperScientificallyEditedManuscript SourceEditedManuscript,
    byte[] SourceMainTex,
    byte[] SourceBibliography,
    string SourceScientificCursorRef,
    IReadOnlyList<PaperAgentInputArtifact> ExactInputs);

public static partial class PaperManuscriptAuthoringAgentService
{
    private const int JournalResearchMinimumExactInputCount = 27;
    private const int JournalResearchMaximumExactInputCount = 28;

    private static PaperJournalResearchContext LoadJournalResearchContext(
        string root,
        string sourceScientificEditingTaskRef)
    {
        PaperAgentTask sourceTask = ReadRegisteredTask(
            root,
            sourceScientificEditingTaskRef);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("scientific-editing");
        if (!string.Equals(sourceTask.Phase, profile.Phase, StringComparison.Ordinal)
            || !string.Equals(sourceTask.AgentRole, profile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(sourceTask.ContextMode, profile.ContextMode, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal research requires an admitted scientific-editing source task.");
        }

        PaperAgentTaskCursor sourceAgentCursor = ReadAgentCursor(
            root,
            sourceTask,
            sourceScientificEditingTaskRef);
        PaperAgentResultWire sourceResult = ReadAgentResult(
            root,
            sourceTask,
            sourceScientificEditingTaskRef,
            sourceAgentCursor.ResultRef);
        RequireCursorMatchesResult(sourceAgentCursor, sourceResult);
        if (!string.Equals(sourceResult.Status, "completed", StringComparison.Ordinal)
            || !string.Equals(sourceResult.NextRoute, "journal-research", StringComparison.Ordinal)
            || sourceAgentCursor.Outputs.Count != 1
            || !string.Equals(
                sourceAgentCursor.Outputs[0].Schema,
                PaperScientificEditingAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal research requires one completed scientific-editing result routed to journal research.");
        }

        string sourceCursorPath = ScientificEditingCursorPath(
            root,
            sourceScientificEditingTaskRef);
        byte[] sourceCursorBytes = ReadBoundedFile(
            sourceCursorPath,
            MaximumControlBytes,
            "Scientific-editing admission cursor");
        string sourceCursorRef = Reference(sourceCursorBytes);
        PaperScientificEditingAgentAdmissionCursor sourceCursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperScientificEditingAgentAdmissionCursor>(sourceCursorBytes);
        Validate(sourceCursor);
        if (!string.Equals(sourceCursor.TaskRef, sourceScientificEditingTaskRef, StringComparison.Ordinal)
            || !string.Equals(sourceCursor.ResultRef, sourceAgentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(sourceCursor.NextRoute, "journal-research", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scientific-editing admission cursor changed the journal-research source identity.");
        }

        string sourceDispatchPath = DomainArtifactPath(
            root,
            "scientific-editing-dispatches",
            "raw",
            sourceCursor.DispatchRef,
            ".json");
        byte[] sourceDispatchBytes = ReadImmutable(
            sourceDispatchPath,
            sourceCursor.DispatchRef,
            "Scientific-editing dispatch");
        PaperScientificEditingAgentDispatch sourceDispatch =
            PaperResearchInputJson.DeserializeStrict<
                PaperScientificEditingAgentDispatch>(sourceDispatchBytes);
        Validate(sourceDispatch);
        PaperScientificEditingContext scientificContext =
            LoadScientificEditingContext(
                root,
                sourceDispatch.SourceAuthoringTaskRef);
        ValidateScientificEditingTaskBinding(
            root,
            sourceTask,
            sourceDispatch,
            sourceCursor.DispatchRef,
            RelativePath(root, sourceDispatchPath),
            scientificContext);

        PaperAgentStoredOutput sourceDraftOutput = sourceAgentCursor.Outputs[0];
        byte[] sourceDraftBytes = ReadAgentOutput(
            root,
            sourceDraftOutput.ArtifactRef);
        PaperScientificEditDraft sourceEditDraft =
            PaperResearchInputJson.DeserializeStrict<PaperScientificEditDraft>(
                sourceDraftBytes);
        PaperScientificManuscriptDraft structuredEditedDraft =
            ValidateScientificEditDraft(
                root,
                sourceEditDraft,
                sourceDispatch,
                sourceCursor.DispatchRef,
                scientificContext,
                sourceAgentCursor.CompletedAt);

        PaperScientificEditDelta sourceEditDelta =
            ReadScientificEditingStoredEnvelope<PaperScientificEditDelta>(
                root,
                sourceCursor.EditDelta,
                PaperScientificEditingAgentSchemas.Delta,
                "Scientific edit delta");
        PaperScientificallyEditedManuscript sourceEditedManuscript =
            ReadScientificEditingStoredEnvelope<PaperScientificallyEditedManuscript>(
                root,
                sourceCursor.EditedManuscript,
                PaperScientificEditingAgentSchemas.EditedManuscript,
                "Scientifically edited manuscript");
        byte[] sourceMainTex = ReadSource(root, sourceCursor.MainTex);
        byte[] sourceBibliography = ReadSource(root, sourceCursor.Bibliography);
        if (!string.Equals(
                sourceEditDelta.DeltaId,
                sourceCursor.EditDelta.ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(
                sourceEditedManuscript.ManuscriptId,
                sourceCursor.EditedManuscript.ArtifactRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal-research source envelope changed semantic identity.");
        }
        Validate(
            sourceEditedManuscript,
            sourceEditDelta,
            scientificContext,
            sourceMainTex,
            sourceBibliography);

        PaperAgentInputArtifact[] exactInputs = sourceTask.ExactInputs
            .Concat(
            [
                new PaperAgentInputArtifact(
                    PaperAgentSchemas.Task,
                    sourceScientificEditingTaskRef,
                    RelativePath(
                        root,
                        AgentArtifactPath(
                            root,
                            "tasks",
                            sourceScientificEditingTaskRef))),
                new PaperAgentInputArtifact(
                    PaperAgentSchemas.AgentResult,
                    sourceAgentCursor.ResultRef,
                    RelativePath(
                        root,
                        AgentArtifactPath(
                            root,
                            "results",
                            sourceAgentCursor.ResultRef))),
                new PaperAgentInputArtifact(
                    PaperScientificEditingAgentSchemas.AdmissionCursor,
                    sourceCursorRef,
                    RelativePath(root, sourceCursorPath)),
                new PaperAgentInputArtifact(
                    PaperScientificEditingAgentSchemas.Draft,
                    sourceDraftOutput.ArtifactRef,
                    RelativePath(
                        root,
                        AgentArtifactPath(
                            root,
                            "outputs",
                            sourceDraftOutput.ArtifactRef))),
                new PaperAgentInputArtifact(
                    PaperScientificEditingAgentSchemas.Delta,
                    sourceCursor.EditDelta.EnvelopeRef,
                    sourceCursor.EditDelta.EnvelopePath),
                new PaperAgentInputArtifact(
                    PaperScientificEditingAgentSchemas.EditedManuscript,
                    sourceCursor.EditedManuscript.EnvelopeRef,
                    sourceCursor.EditedManuscript.EnvelopePath),
                new PaperAgentInputArtifact(
                    "paper-scientifically-edited-main-tex.v1",
                    sourceCursor.MainTex.ArtifactRef,
                    sourceCursor.MainTex.RepositoryRelativePath),
                new PaperAgentInputArtifact(
                    "paper-scientifically-edited-bibliography.v1",
                    sourceCursor.Bibliography.ArtifactRef,
                    sourceCursor.Bibliography.RepositoryRelativePath)
            ])
            .GroupBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        if (exactInputs.Length < JournalResearchMinimumExactInputCount
            || exactInputs.Length > JournalResearchMaximumExactInputCount
            || exactInputs.Select(value => value.ArtifactRef)
                .Distinct(StringComparer.Ordinal).Count() != exactInputs.Length
            || exactInputs.Select(value => value.RepositoryRelativePath)
                .Distinct(StringComparer.Ordinal).Count() != exactInputs.Length)
        {
            throw new InvalidDataException(
                "Journal-research exact evidence closure is incomplete or contains duplicates.");
        }
        foreach (PaperAgentInputArtifact input in exactInputs)
        {
            _ = ReadExactInput(root, input);
        }

        return new PaperJournalResearchContext(
            sourceTask,
            sourceAgentCursor,
            sourceCursor,
            sourceDispatch,
            scientificContext,
            sourceEditDraft,
            structuredEditedDraft,
            sourceEditDelta,
            sourceEditedManuscript,
            sourceMainTex,
            sourceBibliography,
            sourceCursorRef,
            exactInputs);
    }

    private static PaperJournalResearchAgentAdmissionCursor ReadJournalResearchCursor(
        string path)
    {
        PaperJournalResearchAgentAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperJournalResearchAgentAdmissionCursor>(
                    ReadBoundedFile(
                        path,
                        MaximumControlBytes,
                        "Journal-research admission cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperJournalResearchAgentResultAdmitted ReplayJournalResearchAdmission(
        string root,
        PaperJournalResearchAgentAdmissionCursor cursor,
        string taskRef,
        PaperAgentTaskCursor agentCursor,
        PaperJournalResearchAgentDispatch dispatch,
        string dispatchRef,
        PaperJournalResearchContext context)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, taskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(
                cursor.SourceScientificEditingTaskRef,
                dispatch.SourceScientificEditingTaskRef,
                StringComparison.Ordinal)
            || !string.Equals(
                cursor.SourceEditedManuscriptRef,
                dispatch.SourceEditedManuscriptRef,
                StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                cursor.TheoryProgramRef,
                dispatch.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(cursor.RunId, agentCursor.RunId, StringComparison.Ordinal)
            || !string.Equals(cursor.Provenance, agentCursor.Provenance, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal-research replay changed task, source manuscript, paper, program, or run identity.");
        }

        PaperJournalResearchDossier dossier =
            ReadJournalStoredEnvelope<PaperJournalResearchDossier>(
                root,
                cursor.Dossier,
                PaperJournalResearchAgentSchemas.Dossier,
                "Journal-research dossier");
        PaperJournalVenueScorecard[] scorecards = cursor.Scorecards
            .Select(stored => ReadJournalStoredEnvelope<PaperJournalVenueScorecard>(
                root,
                stored,
                PaperJournalResearchAgentSchemas.VenueScorecard,
                "Journal venue scorecard"))
            .ToArray();
        PaperJournalTargetSelection selection =
            ReadJournalStoredEnvelope<PaperJournalTargetSelection>(
                root,
                cursor.TargetSelection,
                PaperJournalResearchAgentSchemas.TargetSelection,
                "Journal target selection");
        if (!string.Equals(dossier.DossierId, cursor.Dossier.ArtifactRef, StringComparison.Ordinal)
            || scorecards.Where((value, index) => !string.Equals(
                    value.ScorecardId,
                    cursor.Scorecards[index].ArtifactRef,
                    StringComparison.Ordinal)).Any()
            || !string.Equals(
                selection.SelectionId,
                cursor.TargetSelection.ArtifactRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Journal-research replay changed stored artifact identity.");
        }
        ValidateJournalResearchArtifacts(
            dossier,
            scorecards,
            selection,
            dispatch,
            context);
        return JournalResearchRecorded(cursor, replayed: true);
    }

    private static T ReadJournalStoredEnvelope<T>(
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

    private static PaperJournalResearchAgentResultAdmitted JournalResearchRecorded(
        PaperJournalResearchAgentAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperJournalResearchAgentSchemas.ResultAdmitted,
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.SourceScientificEditingTaskRef,
            cursor.SourceEditedManuscriptRef,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.Dossier,
            cursor.Scorecards,
            cursor.TargetSelection,
            cursor.SelectedVenueId,
            cursor.SelectedJournalName,
            cursor.SelectedPublicationTier,
            cursor.SelectedArticleType,
            cursor.NextRoute,
            cursor.RunId,
            cursor.Provenance,
            cursor.AdmittedAt,
            replayed);

    private static string JournalResearchCursorPath(
        string root,
        string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-journal-research",
            "cursors",
            Hex(taskRef) + ".json");
}
