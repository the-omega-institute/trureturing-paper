namespace Trureturing.Paper.Core;

internal sealed record PaperFrontierRecoveredState(
    PaperFrontierNodeSelectionStoredArtifact Stored,
    PaperFormalizationFrontierState State);

public static partial class PaperFrontierNodeSelectionService
{
    private static void RecoverCurrentStateCursor(
        string root,
        PaperFrontierNodeSelectionSource source)
    {
        var candidates = new List<PaperFrontierRecoveredState>();
        string currentPath = CurrentStateCursorPath(
            root,
            source.Frontier.FrontierId);
        PaperFrontierCurrentStateCursor? currentCursor = null;
        if (File.Exists(currentPath))
        {
            currentCursor = ReadCurrentStateCursor(currentPath);
            if (!string.Equals(
                    currentCursor.FrontierRef,
                    source.Frontier.FrontierId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Current frontier state cursor changed its frontier identity.");
            }
            PaperFormalizationFrontierState currentState =
                ReadStoredState(root, currentCursor.State);
            PaperFormalizationFrontierLifecycleService.Validate(
                currentState,
                source.Frontier);
            if (currentCursor.Version != currentState.StateContent.Version
                || !string.Equals(
                    currentCursor.UpdatedAt,
                    currentState.StateContent.UpdatedAt,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Current frontier state cursor does not match its immutable state.");
            }
            candidates.Add(new(currentCursor.State, currentState));
        }

        string cursorDirectory = Path.GetDirectoryName(
            AdmissionCursorPath(
                root,
                source.Frontier.FrontierId,
                source.Node.NodeId))!;
        if (Directory.Exists(cursorDirectory))
        {
            foreach (string cursorFile in Directory
                .EnumerateFiles(cursorDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(value => value, StringComparer.Ordinal))
            {
                PaperFrontierNodeSelectionAdmissionCursor cursor =
                    ReadAdmissionCursor(cursorFile);
                if (!string.Equals(
                        Path.GetFileNameWithoutExtension(cursorFile),
                        Hex(cursor.NodeId),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        cursor.FrontierPlanningTaskRef,
                        source.PlanningCursor.TaskRef,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        cursor.FrontierPlanningResultRef,
                        source.PlanningCursor.ResultRef,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        cursor.FrontierPlanningDispatchRef,
                        source.PlanningCursor.DispatchRef,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        cursor.FrontierRef,
                        source.Frontier.FrontierId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        cursor.InitialStateRef,
                        source.InitialState.StateId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "A recovered frontier node cursor changed its planning lineage or file identity.");
                }

                PaperFrontierNodeSelectionSource cursorSource = LoadSource(
                    root,
                    source.PlanningCursor.TaskRef,
                    cursor.NodeId);
                ValidateReplay(root, cursor, cursorSource);
                WriteBindingLookup(
                    root,
                    cursor.FormalizationRequestRef,
                    cursor.Binding);
                PaperFormalizationFrontierState cursorState =
                    ReadStoredState(root, cursor.FrontierState);
                PaperFormalizationFrontierLifecycleService.Validate(
                    cursorState,
                    source.Frontier);
                candidates.Add(new(cursor.FrontierState, cursorState));
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        int maximumVersion = candidates.Max(value => value.State.StateContent.Version);
        PaperFrontierRecoveredState[] latest = candidates
            .Where(value => value.State.StateContent.Version == maximumVersion)
            .ToArray();
        string[] latestStateIds = latest
            .Select(value => value.State.StateId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (latestStateIds.Length != 1)
        {
            throw new InvalidDataException(
                "Recovered frontier node cursors contain divergent equal-version states.");
        }
        PaperFrontierRecoveredState selected = latest[0];
        foreach (PaperFrontierRecoveredState candidate in candidates)
        {
            RequireEventSubset(candidate.State, selected.State);
        }

        if (currentCursor is null
            || !string.Equals(
                currentCursor.State.ArtifactRef,
                selected.State.StateId,
                StringComparison.Ordinal))
        {
            WriteCurrentStateCursor(
                root,
                source.Frontier,
                selected.Stored,
                selected.State);
        }
    }
}
