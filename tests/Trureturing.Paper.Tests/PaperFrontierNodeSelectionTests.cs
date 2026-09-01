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
}
