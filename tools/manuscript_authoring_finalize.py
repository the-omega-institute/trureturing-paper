from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / "src" / "Trureturing.Paper.Core"
CONTRACTS = ROOT / "contracts"
TESTS = ROOT / "tests" / "Trureturing.Paper.Tests"
DOCS = ROOT / "docs"


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    if old in text:
        path.write_text(text.replace(old, new, 1))


def fix_sources() -> None:
    agents = CORE / "PaperManuscriptAuthoringAgents.cs"
    text = agents.read_text()
    text = text.replace(
        "    public static void Validate(\n        PaperScientificManuscript manuscript,\n        PaperManuscriptAuthoringContext context,",
        "    internal static void Validate(\n        PaperScientificManuscript manuscript,\n        PaperManuscriptAuthoringContext context,",
    )
    text = text.replace(
        "            dispatch.SelectedReleaseDigest,\n            dispatch.PaperId,\n            mainTex,",
        "            dispatch.SelectedReleaseDigest,\n            draft.Title,\n            mainTex,",
    )
    text = text.replace(
        """        var manuscript = new PaperScientificManuscript(
            PaperManuscriptAuthoringAgentSchemas.ScientificManuscript,
            Reference(CanonicalJson.Serialize(content)),
            content with { Title = draft.Title });
        manuscript = manuscript with
        {
            ManuscriptId = Reference(
                CanonicalJson.Serialize(manuscript.ManuscriptContent))
        };""",
        """        var manuscript = new PaperScientificManuscript(
            PaperManuscriptAuthoringAgentSchemas.ScientificManuscript,
            Reference(CanonicalJson.Serialize(content)),
            content);""",
    )
    agents.write_text(text)

    for path in CORE.glob("PaperManuscriptAuthoring*.cs"):
        source = path.read_text()
        if any(token in source for token in ("JsonException", "JsonDocument", "JsonElement")) \
                and "using System.Text.Json;" not in source:
            path.write_text("using System.Text.Json;\n" + source)

    for path in CORE.glob("PaperManuscript*.cs"):
        source = path.read_text()
        source = re.sub(
            r"!string\.Equals\(\s*manifest\.ManuscriptTruthReleaseRef,\s*"
            r"selectedRelease\.ReleaseDigest == string\.Empty\s*\?\s*"
            r"string\.Empty\s*:\s*plan\.ManuscriptTruthReleaseRef,\s*"
            r"StringComparison\.Ordinal\)",
            "!string.Equals(\n                manifest.ManuscriptTruthReleaseRef,\n"
            "                plan.ManuscriptTruthReleaseRef,\n"
            "                StringComparison.Ordinal)",
            source,
        )
        path.write_text(source)


def snake(name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()


def parse_records() -> dict[str, list[tuple[str, str]]]:
    records: dict[str, list[tuple[str, str]]] = {}
    source = "\n".join(path.read_text() for path in CORE.glob("PaperManuscriptAuthoring*.cs"))
    marker = re.compile(r"public sealed record\s+(\w+)\s*\(")
    for match in marker.finditer(source):
        name = match.group(1)
        start = match.end()
        depth = 1
        index = start
        while index < len(source) and depth:
            if source[index] == "(":
                depth += 1
            elif source[index] == ")":
                depth -= 1
            index += 1
        body = source[start:index - 1]
        fields = re.findall(
            r"\[property:\s*JsonRequired\]\s*([^,\n]+(?:<[^>]+>)?\??)\s+(\w+)",
            body,
        )
        if fields:
            records[name] = [(field_type.strip(), field_name) for field_type, field_name in fields]
    required = {
        "PaperManuscriptAuthoringAgentDispatch",
        "PaperScientificManuscriptDraft",
        "PaperManuscriptAuthoringAgentTaskStaged",
        "PaperScientificManuscript",
        "PaperManuscriptAuthoringAgentAdmissionCursor",
        "PaperManuscriptAuthoringAgentResultAdmitted",
        "PaperManuscriptAuthoringAgentFailure",
    }
    missing = sorted(required - records.keys())
    if missing:
        raise SystemExit(f"missing manuscript authoring records: {missing}")
    return records


DIGEST_EXCEPTIONS = {
    "paper_id", "section_id", "block_id", "claim_id", "item_id",
    "citation_key", "agent_role", "context_mode", "phase", "run_id",
    "title", "schema", "status", "next_route", "blocker_code",
    "manuscript_status", "source_kind", "media_type", "file_name",
    "repository_relative_path", "workspace_relative_path", "provenance",
    "manuscript_disposition", "claim_kind", "block_kind",
}

ENUMS: dict[str, list[str]] = {
    "phase": ["manuscript-authoring"],
    "agent_role": ["paper-manuscript-author"],
    "context_mode": ["certified-claims-only"],
    "next_route": ["scientific-editing", "manuscript-authoring", "blocked"],
    "provenance": ["produced", "adopted"],
    "manuscript_status": ["journal-neutral-scientific-draft"],
    "section_id": [
        "introduction", "prior-work", "setting", "main-results",
        "proof-architecture", "formalization", "boundaries", "discussion",
    ],
    "block_kind": ["prose", "formal-claim", "proof", "informal-item"],
    "claim_kind": ["theorem", "lemma", "proposition", "corollary"],
}

SCHEMA_BY_ROOT = {
    "paper-manuscript-authoring-agent-dispatch.v1": "PaperManuscriptAuthoringAgentDispatch",
    "paper-scientific-manuscript-draft.v1": "PaperScientificManuscriptDraft",
    "paper-manuscript-authoring-agent-task-staged.v1": "PaperManuscriptAuthoringAgentTaskStaged",
    "paper-scientific-manuscript.v1": "PaperScientificManuscript",
    "paper-manuscript-authoring-agent-cursor.v1": "PaperManuscriptAuthoringAgentAdmissionCursor",
    "paper-manuscript-authoring-agent-result-admitted.v1": "PaperManuscriptAuthoringAgentResultAdmitted",
    "paper-manuscript-authoring-agent-failure.v1": "PaperManuscriptAuthoringAgentFailure",
}


def scalar_schema(field_type: str, field_name: str, records: dict[str, list[tuple[str, str]]]) -> dict:
    nullable = field_type.endswith("?")
    base = field_type[:-1] if nullable else field_type
    array_match = re.fullmatch(r"IReadOnlyList<(.+)>", base)
    if array_match:
        item = scalar_schema(array_match.group(1).strip(), field_name.removesuffix("s"), records)
        result: dict = {"type": "array", "items": item}
        if field_name in {"Sections", "SectionIds"}:
            result["minItems"] = 8
            result["maxItems"] = 8
        return result
    if base in records:
        result = {"$ref": f"#/$defs/{base}"}
    elif base == "string":
        key = snake(field_name)
        result = {"type": "string"}
        if key in ENUMS:
            result = {"enum": ENUMS[key]}
        elif key not in DIGEST_EXCEPTIONS and (
            key.endswith("_ref") or key.endswith("_digest") or key.endswith("_id")
        ):
            result["pattern"] = "^sha256:[0-9a-f]{64}$"
        elif key.endswith("_at"):
            result["format"] = "date-time"
        elif key in {"paper_id", "section_id", "block_id", "claim_id", "item_id", "citation_key"}:
            result["pattern"] = "^[A-Za-z][A-Za-z0-9_.:-]{0,127}$"
        else:
            result["minLength"] = 0 if key in {"blocker_code"} else 1
            result["maxLength"] = 131072 if key in {"text", "scientific_instruction"} else 16384
    elif base == "int":
        result = {"type": "integer", "minimum": 0}
    elif base == "bool":
        result = {"type": "boolean"}
    else:
        result = {"type": "object"}
    if nullable:
        return {"oneOf": [result, {"type": "null"}]}
    return result


def record_schema(name: str, records: dict[str, list[tuple[str, str]]], root_const: str | None = None) -> dict:
    properties: dict[str, dict] = {}
    required: list[str] = []
    for field_type, field_name in records[name]:
        key = snake(field_name)
        required.append(key)
        properties[key] = scalar_schema(field_type, field_name, records)
        if key == "schema" and root_const:
            properties[key] = {"const": root_const}
    return {
        "type": "object",
        "additionalProperties": False,
        "required": required,
        "properties": properties,
    }


def generate_contracts() -> None:
    records = parse_records()
    CONTRACTS.mkdir(parents=True, exist_ok=True)
    for schema_name, root_record in SCHEMA_BY_ROOT.items():
        used: set[str] = set()

        def visit(name: str) -> None:
            if name in used or name not in records:
                return
            used.add(name)
            for field_type, _ in records[name]:
                inner = field_type.rstrip("?")
                array = re.fullmatch(r"IReadOnlyList<(.+)>", inner)
                if array:
                    inner = array.group(1).strip().rstrip("?")
                visit(inner)

        visit(root_record)
        defs = {
            name: record_schema(name, records, schema_name if name == root_record else None)
            for name in sorted(used)
        }
        document = {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": f"https://schemas.trureturing.org/paper/{schema_name}.schema.json",
            "title": schema_name,
            "$ref": f"#/$defs/{root_record}",
            "$defs": defs,
        }
        (CONTRACTS / f"{schema_name}.schema.json").write_text(
            json.dumps(document, indent=2, sort_keys=True) + "\n"
        )

    admitted = record_schema(
        "PaperManuscriptAuthoringAgentResultAdmitted",
        records,
        "paper-scientific-manuscript-ready.v1",
    )
    ready = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://schemas.trureturing.org/paper/paper-scientific-manuscript-ready.v1.schema.json",
        "title": "paper-scientific-manuscript-ready.v1",
        **admitted,
        "$defs": {
            name: record_schema(name, records)
            for name in sorted(records)
            if name in {"PaperManuscriptAuthoringStoredArtifact", "PaperManuscriptSourceFile"}
        },
    }
    ready["properties"]["schema"] = {"const": "paper-scientific-manuscript-ready.v1"}
    (CONTRACTS / "paper-scientific-manuscript-ready.v1.schema.json").write_text(
        json.dumps(ready, indent=2, sort_keys=True) + "\n"
    )


def write_docs() -> None:
    DOCS.mkdir(parents=True, exist_ok=True)
    (DOCS / "FKST_NATIVE_MANUSCRIPT_AUTHORING.md").write_text(
        """# FKST-native certified manuscript authoring

The manuscript authoring phase begins only after the existing claim-manifest gate has produced an eligible `paper-certified-claim-manifest.v1` for one completed formalization frontier.

## Execution path

```text
paper_certified_claim_manifest_ready
  -> dispatch-manuscript-authoring-agent
  -> paper-agent-task.v1
  -> FKST run-codex-agent / spawn_codex_sync
  -> paper-scientific-manuscript-draft.v1
  -> deterministic domain admission
  -> repository-rendered main.tex and references.bib
  -> paper-scientific-manuscript.v1
  -> paper_scientific_manuscript_ready
  -> scientific-editing
```

The generic Paper agent runtime owns subprocess admission, workspace isolation, timeout, result envelopes, immutable task replay, and output storage. The manuscript department owns the scientific prompt and the exact evidence set. The repository validator owns claim coverage, citation admission, LaTeX rendering, content addressing, and the transition to scientific editing.

## Exact evidence closure

Every authoring task carries fourteen immutable inputs:

1. manuscript claim evaluation;
2. certified claim manifest;
3. manuscript eligibility receipt;
4. repository-generated manuscript plan;
5. frontier completion receipt;
6. coherent selected truth release;
7. theory program;
8. A0 scope;
9. A1 inventory;
10. A2 theorem package;
11. A3 independent audit;
12. candidate-paper evidence;
13. literature-research evidence;
14. admitted formalization frontier.

The bridge reopens the frontier completion cursor, the selected release, the claim-manifest closure, and the complete theory lineage. Event payloads are triggers only.

## Structured draft boundary

Codex returns one JSON draft with exactly eight section IDs:

```text
introduction
prior-work
setting
main-results
proof-architecture
formalization
boundaries
discussion
```

A formal claim has one `formal-claim` anchor and one `proof` block. An informal plan item has one `informal-item` anchor. Narrative prose may cite indexed literature evidence and may explain proofs, limitations, motivation, and relationships among results.

The draft cannot contain theorem environments, labels, document preambles, source inclusion commands, repository provenance comments, bibliography entries, or an unregistered theorem statement.

## Repository-owned theorem source

The repository emits every theorem, lemma, proposition, corollary, definition, example, and remark environment. It copies the exact label and statement from the manuscript plan and certified manifest. It also inserts machine-readable comments for claim ID, certified-claim reference, GID, statement ID, and frontier completion.

Consequently a language model can improve exposition without changing the mathematical interface accepted by Formalize and the selected truth release.

## Citation boundary

The draft selects literature entries by index. Citation keys and BibTeX records are derived from the admitted literature artifact. Every used key must resolve, and every rendered bibliography entry must be used. Opaque or empty literature evidence produces no bibliography entries and forbids free-form citations.

## Replay and failure

The generic task cursor prevents a repeated Codex run. The manuscript admission cursor binds one task and one result to one scientific manuscript and one pair of immutable source files. Replay revalidates every source digest and the full claim closure.

`no-progress` requests another manuscript-authoring attempt. `blocked` records typed evidence and stops the paper at this boundary. Neither route creates LaTeX sources.
"""
    )


def write_tests() -> None:
    TESTS.mkdir(parents=True, exist_ok=True)
    (TESTS / "PaperManuscriptAuthoringWiringTests.cs").write_text(
        r'''using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.Tests;

public sealed class PaperManuscriptAuthoringWiringTests
{
    [Fact]
    public void NativeAuthoringProfileIsBoundedAndClaimPreserving()
    {
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("manuscript-authoring");

        Assert.Equal("paper-manuscript-author", profile.AgentRole);
        Assert.Equal("certified-claims-only", profile.ContextMode);
        Assert.Equal("workspace-write", profile.Sandbox);
        Assert.InRange(profile.TimeoutSeconds, 60, 14400);
    }

    [Fact]
    public void FkstRoutesEligibleManifestThroughGenericCodexRuntime()
    {
        string root = FindRepositoryRoot();
        string dispatch = ReadDepartment(root, "dispatch-manuscript-authoring-agent");
        string admit = ReadDepartment(root, "admit-manuscript-authoring-agent");
        string failure = ReadDepartment(root, "route-manuscript-authoring-agent-failure");
        string generic = ReadDepartment(root, "run-codex-agent");
        string cli = File.ReadAllText(Path.Combine(
            root, "src", "Trureturing.Paper.Agent.Cli", "Program.cs"));

        Assert.Contains("paper_certified_claim_manifest_ready", dispatch, StringComparison.Ordinal);
        Assert.Contains("stage-manuscript-authoring-task", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_agent_task_requested", dispatch, StringComparison.Ordinal);
        Assert.Contains("paper_scientific_manuscript_ready", admit, StringComparison.Ordinal);
        Assert.Contains("admit-manuscript-authoring-result", admit, StringComparison.Ordinal);
        Assert.Contains("paper_manuscript_authoring_retry_requested", failure, StringComparison.Ordinal);
        Assert.Contains("paper_manuscript_authoring_blocked", failure, StringComparison.Ordinal);
        Assert.Contains("spawn_codex", generic, StringComparison.Ordinal);
        Assert.Contains("stage-manuscript-authoring-task", cli, StringComparison.Ordinal);
        Assert.Contains("admit-manuscript-authoring-result", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run", dispatch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet run", admit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spawn_codex", dispatch, StringComparison.Ordinal);
        Assert.DoesNotContain("spawn_codex", admit, StringComparison.Ordinal);
    }

    [Fact]
    public void AllAuthoringContractsAreStrictJsonSchemas()
    {
        string root = FindRepositoryRoot();
        string[] schemas =
        [
            "paper-manuscript-authoring-agent-dispatch.v1.schema.json",
            "paper-scientific-manuscript-draft.v1.schema.json",
            "paper-manuscript-authoring-agent-task-staged.v1.schema.json",
            "paper-scientific-manuscript.v1.schema.json",
            "paper-manuscript-authoring-agent-cursor.v1.schema.json",
            "paper-manuscript-authoring-agent-result-admitted.v1.schema.json",
            "paper-scientific-manuscript-ready.v1.schema.json",
            "paper-manuscript-authoring-agent-failure.v1.schema.json"
        ];

        foreach (string file in schemas)
        {
            string path = Path.Combine(root, "contracts", file);
            Assert.True(File.Exists(path), $"Missing manuscript authoring contract {file}.");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                document.RootElement.GetProperty("$schema").GetString());
            Assert.Contains(
                "additionalProperties",
                File.ReadAllText(path),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AuthoringImplementationDeclaresRepositoryOwnedClaimRendering()
    {
        string root = FindRepositoryRoot();
        string sources = string.Join(
            "\n",
            Directory.GetFiles(
                    Path.Combine(root, "src", "Trureturing.Paper.Core"),
                    "PaperManuscriptAuthoring*.cs")
                .Select(File.ReadAllText));

        Assert.Contains("paper-scientific-manuscript-draft.v1", sources, StringComparison.Ordinal);
        Assert.Contains("paper-scientific-manuscript.v1", sources, StringComparison.Ordinal);
        Assert.Contains("formal-claim", sources, StringComparison.Ordinal);
        Assert.Contains("proof", sources, StringComparison.Ordinal);
        Assert.Contains("certified-claims-only", File.ReadAllText(Path.Combine(
            root, "src", "Trureturing.Paper.Core", "PaperAgentRuntime.cs")), StringComparison.Ordinal);
        Assert.Contains("\\begin{theorem}", sources, StringComparison.Ordinal);
        Assert.Contains("statement-id:", sources, StringComparison.Ordinal);
        Assert.Contains("certified-claim-ref:", sources, StringComparison.Ordinal);
    }

    private static string ReadDepartment(string root, string name) =>
        File.ReadAllText(Path.Combine(
            root,
            ".fkst",
            "local-packages",
            "trureturing-paper",
            "departments",
            name,
            "main.lua"));

    private static string FindRepositoryRoot()
    {
        foreach (DirectoryInfo start in new[]
        {
            new DirectoryInfo(Environment.CurrentDirectory),
            new DirectoryInfo(AppContext.BaseDirectory)
        })
        {
            for (DirectoryInfo? current = start;
                 current is not null;
                 current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "Trureturing.Paper.slnx")))
                {
                    return current.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the trureturing-paper repository root.");
    }
}
'''
    )


def clean_temporary_files() -> None:
    workflow = ROOT / ".github" / "workflows" / "manuscript-authoring-finalize.yml"
    script = ROOT / "tools" / "manuscript_authoring_finalize.py"
    if workflow.exists():
        workflow.unlink()
    if script.exists():
        script.unlink()


def main() -> None:
    fix_sources()
    generate_contracts()
    write_docs()
    write_tests()
    clean_temporary_files()


if __name__ == "__main__":
    main()
