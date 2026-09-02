using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperScientificEditingAgentTests
{
    [Fact]
    public void StageTaskCarriesExactSourceAndCertifiedEvidence()
    {
        using ScientificEditingFixture fixture = CreateFixture();
        PaperAgentTask task = PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
            File.ReadAllBytes(fixture.Staged.TaskPath));

        Assert.Equal("scientific-editing", task.Phase);
        Assert.Equal("paper-scientific-editor", task.AgentRole);
        Assert.Equal("claim-preserving-edit", task.ContextMode);
        Assert.Equal(20, task.ExactInputs.Count);
        Assert.Contains(
            task.ExactInputs,
            input => input.Schema == PaperManuscriptAuthoringAgentSchemas.ScientificManuscript
                && input.ArtifactRef == fixture.SourceAdmission.Manuscript.EnvelopeRef);
        Assert.Contains(
            task.ExactInputs,
            input => input.Schema == PaperManuscriptAuthoringAgentSchemas.Draft);
        Assert.Contains(
            task.ExactInputs,
            input => input.Schema == "paper-manuscript-main-tex.v1"
                && input.ArtifactRef == fixture.SourceAdmission.MainTex.ArtifactRef);
        Assert.Equal(
            new[] { "blocked", "journal-research", "scientific-editing" },
            task.AllowedNextRoutes.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Single(task.ExpectedOutputs);
        Assert.Equal(
            PaperScientificEditingAgentSchemas.Draft,
            task.ExpectedOutputs[0].Schema);
    }

    [Fact]
    public void SubstantiveEditPreservesCertifiedClaimsAndAdvances()
    {
        using ScientificEditingFixture fixture = CreateFixture();
        PaperScientificEditDraft edit = fixture.BuildValidEdit();
        _ = fixture.RecordCompletedEdit(edit);

        PaperScientificEditingAgentResultAdmitted admitted =
            PaperManuscriptAuthoringAgentService.AdmitScientificEditingResult(
                fixture.Root,
                fixture.Staged.TaskRef);

        Assert.Equal("journal-research", admitted.NextRoute);
        Assert.True(admitted.ChangedProseBlockCount >= 2);
        Assert.True(admitted.ChangedProofBlockCount >= 1);
        Assert.True(admitted.ChangedSectionIds.Count >= 3);
        Assert.Equal(
            PaperScientificEditingAgentSchemas.Delta,
            admitted.EditDelta.Schema);
        Assert.Equal(
            PaperScientificEditingAgentSchemas.EditedManuscript,
            admitted.EditedManuscript.Schema);

        byte[] sourceBytes = fixture.Source.ReadSource(
            fixture.SourceAdmission.MainTex);
        byte[] editedBytes = fixture.Source.ReadSource(admitted.MainTex);
        string source = Encoding.UTF8.GetString(sourceBytes);
        string edited = Encoding.UTF8.GetString(editedBytes);
        Assert.NotEqual(source, edited);
        Assert.Contains(
            "The revised contribution is organized around the exact obstruction equivalence",
            edited,
            StringComparison.Ordinal);
        Assert.Equal(
            Count(source, "% TRURETURING-FORMAL-CLAIM-BEGIN"),
            Count(edited, "% TRURETURING-FORMAL-CLAIM-BEGIN"));
        Assert.Equal(
            Count(source, "% TRURETURING-INFORMAL-ITEM-BEGIN"),
            Count(edited, "% TRURETURING-INFORMAL-ITEM-BEGIN"));

        PaperScientificEditingAgentResultAdmitted replay =
            PaperManuscriptAuthoringAgentService.AdmitScientificEditingResult(
                fixture.Root,
                fixture.Staged.TaskRef);
        Assert.True(replay.Replayed);
        Assert.Equal(
            admitted.EditedManuscript.ArtifactRef,
            replay.EditedManuscript.ArtifactRef);
        Assert.Equal(admitted.MainTex.ArtifactRef, replay.MainTex.ArtifactRef);
        Assert.Equal(
            admitted.EditDelta.ArtifactRef,
            replay.EditDelta.ArtifactRef);
    }

    [Fact]
    public void NoOpEditCannotClaimScientificProgress()
    {
        using ScientificEditingFixture fixture = CreateFixture();
        PaperScientificEditDraft edit = fixture.BuildValidEdit() with
        {
            AbstractLatex = fixture.SourceDraft.AbstractLatex,
            Keywords = fixture.SourceDraft.Keywords,
            Sections = fixture.SourceDraft.Sections,
            References = fixture.SourceDraft.References,
            EditDimensions =
            [
                "contribution-framing",
                "proof-exposition",
                "limitations-and-implications"
            ]
        };
        _ = fixture.RecordCompletedEdit(edit);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitScientificEditingResult(
                fixture.Root,
                fixture.Staged.TaskRef));

        Assert.Contains(
            "edit dimensions",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EditorCannotRetargetCertifiedClaimAnchor()
    {
        using ScientificEditingFixture fixture = CreateFixture();
        PaperScientificEditDraft valid = fixture.BuildValidEdit();
        PaperManuscriptDraftSection main = valid.Sections.Single(
            section => section.SectionId == "main-results");
        PaperManuscriptDraftBlock anchor = main.Blocks.First(
            block => block.Kind == PaperManuscriptDraftBlockKinds.FormalClaim);
        PaperManuscriptDraftSection changedMain = main with
        {
            Blocks = main.Blocks.Select(block => block == anchor
                ? block with { TargetId = "thm:invented" }
                : block).ToArray()
        };
        PaperScientificEditDraft edit = valid with
        {
            Sections = valid.Sections.Select(section =>
                section.SectionId == "main-results" ? changedMain : section).ToArray()
        };
        _ = fixture.RecordCompletedEdit(edit);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitScientificEditingResult(
                fixture.Root,
                fixture.Staged.TaskRef));

        Assert.Contains(
            "block order, kind, or target",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EditorCannotInjectTheoremEnvironment()
    {
        using ScientificEditingFixture fixture = CreateFixture();
        PaperScientificEditDraft valid = fixture.BuildValidEdit();
        PaperManuscriptDraftSection introduction = valid.Sections[0];
        PaperManuscriptDraftSection changedIntroduction = introduction with
        {
            Blocks = introduction.Blocks.Select((block, index) => index == 0
                ? block with
                {
                    Latex = "\\begin{theorem}This injected statement attempts to bypass the certified claim ledger and must be rejected by the authored LaTeX gate.\\end{theorem}"
                }
                : block).ToArray()
        };
        PaperScientificEditDraft edit = valid with
        {
            Sections = valid.Sections.Select((section, index) =>
                index == 0 ? changedIntroduction : section).ToArray()
        };
        _ = fixture.RecordCompletedEdit(edit);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => PaperManuscriptAuthoringAgentService.AdmitScientificEditingResult(
                fixture.Root,
                fixture.Staged.TaskRef));

        Assert.Contains(
            "forbidden LaTeX",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ScientificEditingFixture CreateFixture()
    {
        var source = new ManuscriptAuthoringTestRepository();
        PaperScientificManuscriptDraft sourceDraft = source.BuildValidDraft();
        _ = source.RecordCompletedDraft(sourceDraft);
        PaperManuscriptAuthoringAgentResultAdmitted sourceAdmission =
            PaperManuscriptAuthoringAgentService.AdmitResult(
                source.Root,
                source.Staged.TaskRef);
        PaperScientificEditingAgentTaskStaged staged =
            PaperManuscriptAuthoringAgentService.StageScientificEditingTask(
                source.Root,
                source.Staged.TaskRef);
        PaperAgentTaskRegistration registration =
            PaperAgentRuntimeService.RegisterTask(source.Root, staged.TaskPath);
        PaperAgentRunPrepared prepared =
            PaperAgentRuntimeService.PrepareRun(source.Root, staged.TaskRef);
        return new(
            source,
            sourceDraft,
            sourceAdmission,
            staged,
            registration,
            prepared);
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

    private sealed class ScientificEditingFixture(
        ManuscriptAuthoringTestRepository source,
        PaperScientificManuscriptDraft sourceDraft,
        PaperManuscriptAuthoringAgentResultAdmitted sourceAdmission,
        PaperScientificEditingAgentTaskStaged staged,
        PaperAgentTaskRegistration registration,
        PaperAgentRunPrepared prepared) : IDisposable
    {
        public ManuscriptAuthoringTestRepository Source { get; } = source;
        public string Root => Source.Root;
        public PaperScientificManuscriptDraft SourceDraft { get; } = sourceDraft;
        public PaperManuscriptAuthoringAgentResultAdmitted SourceAdmission { get; } = sourceAdmission;
        public PaperScientificEditingAgentTaskStaged Staged { get; } = staged;
        public PaperAgentTaskRegistration Registration { get; } = registration;
        public PaperAgentRunPrepared Prepared { get; } = prepared;

        public PaperScientificEditDraft BuildValidEdit()
        {
            PaperManuscriptDraftSection[] sections = SourceDraft.Sections
                .Select(section => section with
                {
                    Blocks = section.Blocks.Select(block =>
                        ReviseBlock(section.SectionId, block)).ToArray()
                })
                .ToArray();
            return new PaperScientificEditDraft(
                PaperScientificEditingAgentSchemas.Draft,
                Staged.DispatchRef,
                Staged.SourceManuscriptRef,
                SourceAdmission.ClaimManifestRef,
                SourceAdmission.ManuscriptPlanRef,
                SourceAdmission.PaperId,
                SourceAdmission.TheoryProgramRef,
                SourceDraft.Title,
                "We establish a release-coherent obstruction theory for structured descent and organize its scientific contribution around four certified formal claims and one explicitly informal definition. The revision sharpens the distinction between imported descent machinery and the paper-specific equivalence, realization, and classification results. It also makes the proof dependency chain, the role of the sharpness construction, and the limits of the certified generality explicit. Every theorem statement remains fixed by the claim manifest, while the surrounding exposition now states the significance and remaining application gap more directly.",
                SourceDraft.Keywords,
                sections,
                SourceDraft.References,
                [
                    "contribution-framing",
                    "literature-boundary",
                    "proof-exposition",
                    "limitations-and-implications"
                ],
                [
                    "Reframed the introduction around the exact obstruction equivalence and its classification consequence.",
                    "Made the nearest prior-work boundary explicit without introducing unsupported citations.",
                    "Reconstructed one load-bearing proof narrative around its certified dependency interfaces.",
                    "Separated proven sharpness from open application and generalization questions."
                ],
                [
                    "The admitted evidence contains no structured bibliography, so venue-level comparison remains for the journal-research stage."
                ],
                "2026-08-31T12:20:00Z");
        }

        public PaperAgentResultRecorded RecordCompletedEdit(
            PaperScientificEditDraft edit)
        {
            string outputPath = Path.Combine(
                Prepared.WorkspacePath,
                "outputs",
                "scientific-edit-draft.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, CanonicalJson.Serialize(edit));
            PaperAgentTask task = PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(
                File.ReadAllBytes(Staged.TaskPath));
            var result = new PaperAgentResultWire(
                PaperAgentSchemas.AgentResult,
                Staged.TaskRef,
                Registration.PaperId,
                Registration.TheoryProgramRef,
                Registration.Phase,
                Registration.AgentRole,
                Registration.ContextMode,
                "completed",
                "A substantive claim-preserving scientific edit was completed.",
                [new PaperAgentOutputWire(
                    PaperScientificEditingAgentSchemas.Draft,
                    "outputs/scientific-edit-draft.json")],
                "journal-research",
                string.Empty,
                task.ExactInputs.Select(input => input.ArtifactRef).ToArray(),
                "2026-08-31T12:30:00Z");
            string stdout = PaperAgentRuntimeService.ResultBegin + "\n"
                + Encoding.UTF8.GetString(CanonicalJson.Serialize(result))
                + "\n" + PaperAgentRuntimeService.ResultEnd + "\n";
            File.WriteAllText(
                Prepared.StdoutPath,
                stdout,
                new UTF8Encoding(false));
            return PaperAgentRuntimeService.RecordResult(
                Root,
                Staged.TaskRef,
                Prepared.StdoutPath,
                "codex-scientific-editing-test-run",
                "produced");
        }

        public void Dispose() => Source.Dispose();

        private static PaperManuscriptDraftBlock ReviseBlock(
            string sectionId,
            PaperManuscriptDraftBlock block)
        {
            if (block.Kind == PaperManuscriptDraftBlockKinds.Proof
                && block.Order == 2)
            {
                return block with
                {
                    Latex = "The proof begins with the certified dependency interface recorded for this claim. The revised narrative now identifies the reduction step, the preservation of each registered hypothesis, and the exact point at which the obstruction criterion supplies the conclusion. This account remains expository because the Lean declaration and selected truth release provide the formal evidence."
                };
            }
            if (block.Kind != PaperManuscriptDraftBlockKinds.Prose
                || block.Order != 1)
            {
                return block;
            }
            return sectionId switch
            {
                "introduction" => block with
                {
                    Latex = "The revised contribution is organized around the exact obstruction equivalence, its independent realization theorem, and the classification consequence certified in one coherent release. This framing distinguishes the paper-specific theorem package from the descent tools it imports."
                },
                "prior-work" => block with
                {
                    Latex = "The prior-work boundary is stated theorem by theorem. Established descent and cocycle tools remain imported, whereas the canonical obstruction, exact equivalence, realization result, and minimal-failure classification form the audited increment supported by the current evidence bundle."
                },
                "boundaries" => block with
                {
                    Latex = "The sharpness theorem fixes the exact boundary of the certified equivalence, while the explicit informal ledger prevents extension beyond the registered hypotheses. Applications to broader observables and weaker compatibility assumptions remain open research questions rather than manuscript claims."
                },
                "discussion" => block with
                {
                    Latex = "The edited manuscript clarifies the mathematical significance of moving from a local compatibility condition to a certified obstruction classification. Its current scope is deliberately limited to the audited setting, and the next stage must evaluate journal fit and evidence-backed applications without changing the claim ledger."
                },
                _ => block
            };
        }
    }
}
