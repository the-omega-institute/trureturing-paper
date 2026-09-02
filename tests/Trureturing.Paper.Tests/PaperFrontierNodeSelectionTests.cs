using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperFrontierNodeSelectionTests
{
    [Fact]
    public void IndependentWaveZeroNodesAccumulateInOneFrontierState()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFormalizationFrontierNode definition =
            repository.Node("def:object");
        PaperFormalizationFrontierNode sharpness =
            repository.Node("thm:sharp");

        PaperFrontierNodeSelectionAdmitted first =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                definition.NodeId);
        PaperFrontierNodeSelectionAdmitted second =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                sharpness.NodeId);

        Assert.False(first.Replayed);
        Assert.False(second.Replayed);
        Assert.NotEqual(first.SelectionRef, second.SelectionRef);
        Assert.NotEqual(
            first.FormalizationRequestRef,
            second.FormalizationRequestRef);
        Assert.Equal(
            "D0/S0/Paper/Trureturing/Base/DescentObject.def_object",
            first.Gid);
        Assert.Equal(
            "D0/S0/Paper/Trureturing/Base/SharpObstruction.thm_sharp",
            second.Gid);

        PaperResearchSelection selection =
            PaperResearchSelectionJson.ReadSelection(
                File.ReadAllBytes(first.SelectionPath));
        FormalizationRequest request =
            PaperResearchSelectionJson.ReadFormalizationRequest(
                File.ReadAllBytes(first.FormalizationRequestPath));
        PaperResearchSelectionService.Validate(selection);
        PaperResearchSelectionService.Validate(request);
        Assert.Equal(
            definition.FormalStatement,
            selection.SelectionContent.Target.LemmaStatement);
        Assert.Equal(first.SelectionRef, selection.SelectionId);
        Assert.Equal(first.FormalizationRequestRef, request.RequestId);
        Assert.Equal(
            selection.SelectionContent.CandidatePaperRef,
            request.PaperContext.ResearchCandidateId);
        Assert.Equal(
            repository.ResearchInputRef,
            selection.SelectionContent.PaperResearchInputRef);
        Assert.Equal(
            repository.TruthReleaseDigest,
            request.TruthRelease.ReleaseDigest);
        Assert.Empty(selection.SelectionContent.ReuseApi);
        Assert.Equal(
            2,
            selection.SelectionContent.Target.AllowedAssumptions.Count);
        Assert.Equal(
            selection.SelectionContent.Target.AllowedAssumptions,
            request.Target.AllowedAssumptions);

        PaperFrontierCurrentStateCursor stateCursor =
            repository.ReadCurrentStateCursor();
        PaperFormalizationFrontierState state =
            repository.ReadState(stateCursor.State);
        PaperFormalizationFrontierLifecycleService.Validate(
            state,
            repository.Frontier);
        Assert.Equal(4, state.StateContent.Version);
        Assert.Equal(
            "request-recorded",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == definition.NodeId).Status);
        Assert.Equal(
            "request-recorded",
            state.StateContent.NodeStates.Single(value =>
                value.NodeId == sharpness.NodeId).Status);
        Assert.Equal(4, state.StateContent.AppliedEventRefs.Count);

        string finalStateRef = state.StateId;
        PaperFrontierNodeSelectionAdmitted replay =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                definition.NodeId);
        Assert.True(replay.Replayed);
        Assert.Equal(first.SelectionRef, replay.SelectionRef);
        Assert.Equal(
            first.FormalizationRequestRef,
            replay.FormalizationRequestRef);
        Assert.Equal(
            finalStateRef,
            repository.ReadCurrentStateCursor().State.ArtifactRef);
        Assert.True(
            repository.BindingLookupExists(first.FormalizationRequestRef));
        Assert.True(
            repository.BindingLookupExists(second.FormalizationRequestRef));
    }

    [Fact]
    public void LaterNodeRecoversCursorCommittedBeforeStatePointer()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFormalizationFrontierNode definition =
            repository.Node("def:object");
        PaperFormalizationFrontierNode sharpness =
            repository.Node("thm:sharp");
        PaperFrontierNodeSelectionAdmitted first =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                definition.NodeId);
        File.Delete(CurrentStateCursorPath(repository));
        File.Delete(BindingLookupPath(
            repository,
            first.FormalizationRequestRef));

        PaperFrontierNodeSelectionAdmitted admitted =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                sharpness.NodeId);

        Assert.False(admitted.Replayed);
        PaperFormalizationFrontierState state = repository.ReadState(
            repository.ReadCurrentStateCursor().State);
        Assert.Equal(4, state.StateContent.Version);
        Assert.Equal(
            2,
            state.StateContent.NodeStates.Count(value =>
                value.Status == "request-recorded"));
        Assert.Equal(4, state.StateContent.AppliedEventRefs.Count);
        Assert.True(
            repository.BindingLookupExists(first.FormalizationRequestRef));
    }

    [Fact]
    public void ReplayRepairsMissingRequestBindingLookup()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFrontierNodeSelectionAdmitted admitted =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);
        string lookupPath = BindingLookupPath(
            repository,
            admitted.FormalizationRequestRef);
        File.Delete(lookupPath);

        PaperFrontierNodeSelectionAdmitted replay =
            PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId);

        Assert.True(replay.Replayed);
        Assert.True(File.Exists(lookupPath));
        Assert.True(
            repository.BindingLookupExists(admitted.FormalizationRequestRef));
    }

    [Fact]
    public void DependentNodeCannotBypassTheReleasedReadySet()
    {
        using var repository = new FrontierSelectionTestRepository();
        PaperFormalizationFrontierNode main = repository.Node("thm:main");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                main.NodeId));

        Assert.Contains("did not release", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactPlanningInputDriftFailsBeforeSelection()
    {
        using var repository = new FrontierSelectionTestRepository();
        File.WriteAllBytes(
            repository.TamperableInputPath,
            Encoding.UTF8.GetBytes("tampered-frontier-input"));

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperFrontierNodeSelectionService.Admit(
                repository.Root,
                repository.PlanningTaskRef,
                repository.Node("def:object").NodeId));

        Assert.Contains(
            "content-address",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CurrentStateCursorPath(
        FrontierSelectionTestRepository repository) =>
        Path.Combine(
            repository.Root,
            "work",
            "paper-frontiers",
            "current-state",
            Hex(repository.Frontier.FrontierId) + ".json");

    private static string BindingLookupPath(
        FrontierSelectionTestRepository repository,
        string requestRef) =>
        Path.Combine(
            repository.Root,
            "work",
            "paper-frontier-formalization-bindings",
            "by-request",
            Hex(requestRef) + ".json");

    private static string Hex(string reference) =>
        reference["sha256:".Length..];
}
