using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperManuscriptAuthoringAgentTests
{
    [Fact]
    public void StageTaskBindsTheCompleteEligibleEvidenceClosure()
    {
        using var repository = new ManuscriptAuthoringTestRepository();

        PaperAgentTask task =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
                File.ReadAllBytes(repository.Staged.TaskPath));
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("manuscript-authoring");

        Assert.Equal("manuscript-authoring", task.Phase);
        Assert.Equal("paper-manuscript-author", task.AgentRole);
        Assert.Equal("certified-claims-only", task.ContextMode);
        Assert.Equal("workspace-write", profile.Sandbox);
        Assert.Equal(15, task.ExactInputs.Count);
        Assert.Equal(
            15,
            task.ExactInputs.Select(value => value.ArtifactRef)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Contains(
            task.ExactInputs,
            value => value.Schema
                == PaperManuscriptAuthoringAgentSchemas.Dispatch
                && value.ArtifactRef == repository.Staged.DispatchRef);
        Assert.Single(task.ExpectedOutputs);
        Assert.Equal(
            PaperManuscriptAuthoringAgentSchemas.Draft,
            task.ExpectedOutputs[0].Schema);
        Assert.Equal(
            "outputs/scientific-manuscript-draft.json",
            task.ExpectedOutputs[0].WorkspaceRelativePath);
        Assert.Equal(
            new[] { "blocked", "manuscript-authoring", "scientific-editing" },
            task.AllowedNextRoutes.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Contains(
            "repository alone inserts",
            task.ScientificInstruction,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            task.ForbiddenShortcuts,
            value => value.Contains(
                "Do not add, omit",
                StringComparison.Ordinal));
    }

    [Fact]
    public void CompletedDraftRendersEveryCertifiedClaimAndReplays()
    {
        using var repository = new ManuscriptAuthoringTestRepository();
        PaperScientificManuscriptDraft draft = repository.BuildValidDraft();
        PaperAgentResultRecorded recorded =
            repository.RecordCompletedDraft(draft);

        PaperManuscriptAuthoringAgentResultAdmitted admitted =
            PaperManuscriptAuthoringAgentService.AdmitResult(
                repository.Root,
                repository.Staged.TaskRef);
        byte[] mainTexBytes = repository.ReadSource(admitted.MainTex);
        byte[] bibliographyBytes = repository.ReadSource(admitted.Bibliography);
        string mainTex = Encoding.UTF8.GetString(mainTexBytes);
        PaperScientificManuscript manuscript =
            PaperResearchInputJson.DeserializeStrict<PaperScientificManuscript>(
                File.ReadAllBytes(Path.Combine(
                    repository.Root,
                    admitted.Manuscript.EnvelopePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar))));
        PaperCertifiedClaimManifest manifest =
            new PaperResearchInputStore(Path.Combine(
                repository.Root,
                "artifacts",
                "research-input")).Get<PaperCertifiedClaimManifest>(
                    repository.ClaimManifestRef);

        Assert.Equal("completed", recorded.Status);
        Assert.Equal("scientific-editing", admitted.NextRoute);
        Assert.Equal(4, admitted.FormalClaimCount);
        Assert.Equal(1, admitted.InformalItemCount);
        Assert.Equal(4, manuscript.ManuscriptContent.ClaimBindings.Count);
        Assert.Equal(
            manifest.FormalClaims.Select(value => value.ClaimId),
            manuscript.ManuscriptContent.ClaimBindings
                .Select(value => value.ClaimId));
        Assert.Equal(
            4,
            Count(mainTex, "% TRURETURING-FORMAL-CLAIM-BEGIN"));
        Assert.Equal(
            4,
            Count(mainTex, "% TRURETURING-FORMAL-CLAIM-END"));
        Assert.Equal(
            1,
            Count(mainTex, "% TRURETURING-INFORMAL-ITEM-BEGIN"));
        Assert.Equal(
            1,
            Count(mainTex, "% TRURETURING-INFORMAL-ITEM-END"));
        Assert.Contains("\\documentclass[11pt]{article}", mainTex, StringComparison.Ordinal);
        Assert.Contains("\\begin{definition}", mainTex, StringComparison.Ordinal);
        Assert.Contains("Epistemic status: explicitly-informal", mainTex, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "outputs/scientific-manuscript-draft.json",
            mainTex,
            StringComparison.Ordinal);
        Assert.Contains(
            "No evidence-bound bibliographic records",
            Encoding.UTF8.GetString(bibliographyBytes),
            StringComparison.Ordinal);
        foreach (PaperManuscriptClaimBinding binding
            in manuscript.ManuscriptContent.ClaimBindings)
        {
            Assert.Equal(1, Count(mainTex, binding.BeginMarker));
            Assert.Equal(1, Count(mainTex, binding.EndMarker));
            Assert.Equal(
                1,
                Count(mainTex, $"\\label{{{binding.LatexLabel}}}"));
        }

        PaperManuscriptAuthoringAgentResultAdmitted replay =
            PaperManuscriptAuthoringAgentService.AdmitResult(
                repository.Root,
                repository.Staged.TaskRef);
        Assert.True(replay.Replayed);
        Assert.Equal(
            admitted.Manuscript.ArtifactRef,
            replay.Manuscript.ArtifactRef);
        Assert.Equal(admitted.MainTex.ArtifactRef, replay.MainTex.ArtifactRef);
        Assert.Equal(
            admitted.Bibliography.ArtifactRef,
            replay.Bibliography.ArtifactRef);
    }

    [Fact]
    public void DraftCannotOmitACertifiedClaimAnchor()
    {
        using var repository = new ManuscriptAuthoringTestRepository();
        PaperScientificManuscriptDraft draft = repository.BuildValidDraft();
        PaperManuscriptDraftSection[] sections = draft.Sections
            .Select(section => section.SectionId == "main-results"
                ? section with
                {
                    Blocks = section.Blocks
                        .Where(block => block.Kind
                            != PaperManuscriptDraftBlockKinds.FormalClaim
                            || block.TargetId
                                != draft.Sections.Single(value =>
                                    value.SectionId == "main-results")
                                    .Blocks.Last(value => value.Kind
                                        == PaperManuscriptDraftBlockKinds.FormalClaim)
                                    .TargetId)
                        .Select((block, index) => block with { Order = index + 1 })
                        .ToArray()
                }
                : section)
            .ToArray();
        _ = repository.RecordCompletedDraft(
            draft with { Sections = sections });

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitResult(
                repository.Root,
                repository.Staged.TaskRef));

        Assert.Contains(
            "cover the complete manuscript plan",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DraftCannotInjectTheoremEnvironments()
    {
        using var repository = new ManuscriptAuthoringTestRepository();
        PaperScientificManuscriptDraft draft = repository.BuildValidDraft();
        PaperManuscriptDraftSection[] sections = draft.Sections
            .Select(section => section.SectionId == "introduction"
                ? section with
                {
                    Blocks = section.Blocks.Select((block, index) => index == 0
                        ? block with
                        {
                            Latex = block.Latex
                                + " \\begin{theorem}An injected claim.\\end{theorem}"
                        }
                        : block).ToArray()
                }
                : section)
            .ToArray();
        _ = repository.RecordCompletedDraft(
            draft with { Sections = sections });

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitResult(
                repository.Root,
                repository.Staged.TaskRef));

        Assert.Contains(
            "forbidden LaTeX control sequence",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DraftCannotInventBibliographyFromOpaqueLiteratureEvidence()
    {
        using var repository = new ManuscriptAuthoringTestRepository();
        PaperScientificManuscriptDraft draft = repository.BuildValidDraft();
        PaperManuscriptDraftSection[] sections = draft.Sections
            .Select(section => section.SectionId == "prior-work"
                ? section with
                {
                    Blocks = section.Blocks.Select((block, index) => index == 0
                        ? block with { Latex = block.Latex + " \\cite{Invented2026}." }
                        : block).ToArray()
                }
                : section)
            .ToArray();
        _ = repository.RecordCompletedDraft(draft with
        {
            Sections = sections,
            References =
            [
                new PaperManuscriptDraftReference(
                    "Invented2026",
                    1,
                    PaperTheoryTestFactory.Digest("literature-paper-a"),
                    "This invented citation would falsely support the novelty comparison.")
            ]
        });

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitResult(
                repository.Root,
                repository.Staged.TaskRef));

        Assert.Contains(
            "Opaque literature evidence",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string source, string needle)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(
                   needle,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }
        return count;
    }
}
