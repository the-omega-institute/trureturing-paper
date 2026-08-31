from pathlib import Path

core = Path("src/Trureturing.Paper.Core/PaperTheoryDeepening.cs")
text = core.read_text()
old = """            [
                program.TheoryProgramId,
                scope.ScopeId,
                inventory.InventoryId,
                .. priorRefs
            ],"""
new = """            [
                program.TheoryProgramId,
                program.ProgramContent.CandidatePaperRef,
                program.ProgramContent.LiteratureResearchRef,
                program.ProgramContent.IntuitionProposalRef,
                program.ProgramContent.PaperResearchInputRef,
                scope.ScopeId,
                inventory.InventoryId,
                .. priorRefs
            ],"""
if old not in text:
    raise SystemExit("deepening contract input block not found")
core.write_text(text.replace(old, new, 1))

tests = Path("tests/Trureturing.Paper.Tests/PaperTheoryDeepeningAgentTests.cs")
text = tests.read_text()
text = text.replace(
    """        private readonly Evidence _program;
        private readonly Evidence _scope;
        private readonly Evidence _inventory;
        private readonly Evidence _request;""",
    """        private readonly Evidence _candidate;
        private readonly Evidence _literature;
        private readonly Evidence _intuition;
        private readonly Evidence _researchInput;
        private readonly Evidence _program;
        private readonly Evidence _scope;
        private readonly Evidence _inventory;
        private readonly Evidence _request;""",
    1,
)
marker = """            Directory.CreateDirectory(Path.Combine(Root, "artifacts", "evidence"));

            PaperCandidateBatch batch = PaperPortfolioService.CreateBatch("""
insertion = """            Directory.CreateDirectory(Path.Combine(Root, "artifacts", "evidence"));

            _candidate = PutEvidence(
                "paper-candidate.v1",
                "candidate.json",
                new { schema = "paper-candidate.v1", paper_id = paperId, candidate = "structural obstruction theory" });
            _literature = PutEvidence(
                "paper-literature-research.v1",
                "literature.json",
                new { schema = "paper-literature-research.v1", paper_id = paperId, boundary = "nearest prior descent results" });
            _intuition = PutEvidence(
                "paper-intuition-proposal.v1",
                "intuition.json",
                new { schema = "paper-intuition-proposal.v1", paper_id = paperId, proposal = "canonical obstruction controls descent" });
            _researchInput = PutEvidence(
                PaperResearchInputSchemas.ResearchInput,
                "research-input.json",
                new { schema = PaperResearchInputSchemas.ResearchInput, paper_id = paperId, exact_release = "bound" });

            PaperCandidateBatch batch = PaperPortfolioService.CreateBatch("""
if marker not in text:
    raise SystemExit("test constructor marker not found")
text = text.replace(marker, insertion, 1)
text = text.replace(
    """                    Digest($"research-{paperId}"),""",
    """                    _researchInput.ArtifactRef,""",
    1,
)
text = text.replace(
    """                            Digest($"candidate-{paperId}"),
                            Digest($"literature-{paperId}"),
                            Digest($"intuition-{paperId}"),""",
    """                            _candidate.ArtifactRef,
                            _literature.ArtifactRef,
                            _intuition.ArtifactRef,""",
    1,
)
text = text.replace(
    """                [_program.ToInput(), _scope.ToInput(), _inventory.ToInput(), _request.ToInput()],""",
    """                [
                    _program.ToInput(),
                    _candidate.ToInput(),
                    _literature.ToInput(),
                    _intuition.ToInput(),
                    _researchInput.ToInput(),
                    _scope.ToInput(),
                    _inventory.ToInput(),
                    _request.ToInput()
                ],""",
    1,
)
helper_marker = """        private Evidence PutContent<T>(
            string schema,"""
helper = """        private Evidence PutEvidence<T>(string schema, string fileName, T value)
        {
            byte[] bytes = CanonicalJson.Serialize(value);
            string reference = PaperResearchInputStore.Reference(bytes);
            string relative = "artifacts/evidence/" + fileName;
            File.WriteAllBytes(
                Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)),
                bytes);
            return new(schema, reference, relative);
        }

        private Evidence PutContent<T>(
            string schema,"""
if helper_marker not in text:
    raise SystemExit("test helper marker not found")
text = text.replace(helper_marker, helper, 1)
text = text.replace(
    'Assert.Contains("between four and sixty-four", error.Message, StringComparison.OrdinalIgnoreCase);',
    'Assert.Contains("inventory", error.Message, StringComparison.OrdinalIgnoreCase);',
    1,
)
tests.write_text(text)

doc = Path("docs/FKST_A2_NATIVE_AGENT.md")
text = doc.read_text()
old_doc = """paper-theory-program.v1
paper-theory-scope.v1
paper-theory-inventory.v1
paper-theory-deepening-request.v1"""
new_doc = """paper-theory-program.v1
paper candidate evidence
paper literature research evidence
paper Intuition proposal
paper-research-input.v1
paper-theory-scope.v1
paper-theory-inventory.v1
paper-theory-deepening-request.v1"""
if old_doc not in text:
    raise SystemExit("A2 documentation evidence block not found")
doc.write_text(text.replace(old_doc, new_doc, 1))
