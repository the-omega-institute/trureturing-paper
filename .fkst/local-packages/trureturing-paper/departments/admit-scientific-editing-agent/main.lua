local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = { "paper_scientifically_edited_manuscript_ready" },
  stall_window = "5m",
}

local function require_source(value, role, media_type)
  if type(value) ~= "table"
      or value.role ~= role
      or value.media_type ~= media_type
      or not agent.is_sha256(value.artifact_ref)
      or type(value.repository_relative_path) ~= "string"
      or value.repository_relative_path == ""
      or type(value.size_bytes) ~= "number"
      or value.size_bytes < 1 then
    error("admit-scientific-editing-agent: invalid " .. role .. " source coordinate")
  end
end

local function require_stored(value, schema, name)
  if type(value) ~= "table"
      or value.schema ~= schema
      or not agent.is_sha256(value.artifact_ref)
      or type(value.content_path) ~= "string"
      or value.content_path == ""
      or not agent.is_sha256(value.envelope_ref)
      or type(value.envelope_path) ~= "string"
      or value.envelope_path == "" then
    error("admit-scientific-editing-agent: invalid " .. name .. " coordinate")
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "scientific-editing" then
    return
  end
  if payload.status ~= "completed"
      or payload.agent_role ~= "paper-scientific-editor"
      or payload.context_mode ~= "claim-preserving-edit"
      or payload.next_route ~= "journal-research"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref) then
    error("admit-scientific-editing-agent: completed event identity is invalid")
  end

  local paths = research.paths(agent.repository_root())
  local admitted = research.run(paths, {
    "admit-scientific-editing-result",
    "--repository-root", paths.root,
    "--task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-scientific-editing-agent-result-admitted.v1"
      or admitted.task_ref ~= payload.task_ref
      or admitted.result_ref ~= payload.result_ref
      or admitted.paper_id ~= payload.paper_id
      or admitted.theory_program_ref ~= payload.theory_program_ref
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.source_authoring_task_ref)
      or not agent.is_sha256(admitted.source_manuscript_ref)
      or not agent.is_sha256(admitted.claim_manifest_ref)
      or not agent.is_sha256(admitted.manuscript_plan_ref)
      or type(admitted.changed_prose_block_count) ~= "number"
      or admitted.changed_prose_block_count < 2
      or type(admitted.changed_proof_block_count) ~= "number"
      or admitted.changed_proof_block_count < 1
      or type(admitted.changed_section_ids) ~= "table"
      or #admitted.changed_section_ids < 3
      or admitted.next_route ~= "journal-research" then
    error("admit-scientific-editing-agent: Agent CLI returned an invalid admission")
  end
  require_stored(
    admitted.edit_delta,
    "paper-scientific-edit-delta.v1",
    "scientific edit delta")
  require_stored(
    admitted.edited_manuscript,
    "paper-scientifically-edited-manuscript.v1",
    "scientifically edited manuscript")
  require_source(
    admitted.main_tex,
    "scientifically-edited-main-tex",
    "text/x-tex")
  require_source(
    admitted.bibliography,
    "scientifically-edited-bibliography",
    "application/x-bibtex")

  raise("paper_scientifically_edited_manuscript_ready", {
    schema = "paper-scientifically-edited-manuscript-ready.v1",
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    source_authoring_task_ref = admitted.source_authoring_task_ref,
    source_manuscript_ref = admitted.source_manuscript_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    claim_manifest_ref = admitted.claim_manifest_ref,
    manuscript_plan_ref = admitted.manuscript_plan_ref,
    edit_delta = admitted.edit_delta,
    edited_manuscript = admitted.edited_manuscript,
    main_tex = admitted.main_tex,
    bibliography = admitted.bibliography,
    changed_prose_block_count = admitted.changed_prose_block_count,
    changed_proof_block_count = admitted.changed_proof_block_count,
    changed_section_ids = admitted.changed_section_ids,
    next_route = admitted.next_route,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-scientifically-edited-manuscript-ready:v1:" ..
      admitted.edited_manuscript.artifact_ref,
  })
end

return M
