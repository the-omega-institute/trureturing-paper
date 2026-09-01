local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = { "paper_scientific_manuscript_ready" },
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
    error("admit-manuscript-authoring-agent: invalid " .. role .. " source coordinate")
  end
end

local function require_manuscript(value)
  if type(value) ~= "table"
      or value.schema ~= "paper-scientific-manuscript.v1"
      or not agent.is_sha256(value.artifact_ref)
      or type(value.content_path) ~= "string"
      or value.content_path == ""
      or not agent.is_sha256(value.envelope_ref)
      or type(value.envelope_path) ~= "string"
      or value.envelope_path == "" then
    error("admit-manuscript-authoring-agent: invalid manuscript artifact coordinate")
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "manuscript-authoring" then
    return
  end
  if payload.status ~= "completed"
      or payload.agent_role ~= "paper-manuscript-author"
      or payload.context_mode ~= "certified-claims-only"
      or payload.next_route ~= "scientific-editing"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref) then
    error("admit-manuscript-authoring-agent: completed event identity is invalid")
  end

  local paths = research.paths(agent.repository_root())
  local admitted = research.run(paths, {
    "admit-manuscript-authoring-result",
    "--repository-root", paths.root,
    "--task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-manuscript-authoring-agent-result-admitted.v1"
      or admitted.task_ref ~= payload.task_ref
      or admitted.result_ref ~= payload.result_ref
      or admitted.paper_id ~= payload.paper_id
      or admitted.theory_program_ref ~= payload.theory_program_ref
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.completion_ref)
      or not agent.is_sha256(admitted.evaluation_ref)
      or not agent.is_sha256(admitted.claim_manifest_ref)
      or not agent.is_sha256(admitted.eligibility_ref)
      or not agent.is_sha256(admitted.manuscript_plan_ref)
      or type(admitted.formal_claim_count) ~= "number"
      or admitted.formal_claim_count < 1
      or type(admitted.informal_item_count) ~= "number"
      or admitted.informal_item_count < 0
      or admitted.next_route ~= "scientific-editing" then
    error("admit-manuscript-authoring-agent: Agent CLI returned an invalid admission")
  end
  require_manuscript(admitted.manuscript)
  require_source(admitted.main_tex, "main-tex", "text/x-tex")
  require_source(
    admitted.bibliography,
    "bibliography",
    "application/x-bibtex")

  raise("paper_scientific_manuscript_ready", {
    schema = "paper-scientific-manuscript-ready.v1",
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    completion_ref = admitted.completion_ref,
    evaluation_ref = admitted.evaluation_ref,
    claim_manifest_ref = admitted.claim_manifest_ref,
    eligibility_ref = admitted.eligibility_ref,
    manuscript_plan_ref = admitted.manuscript_plan_ref,
    manuscript = admitted.manuscript,
    main_tex = admitted.main_tex,
    bibliography = admitted.bibliography,
    formal_claim_count = admitted.formal_claim_count,
    informal_item_count = admitted.informal_item_count,
    next_route = admitted.next_route,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-scientific-manuscript-ready:v1:" ..
      admitted.manuscript.artifact_ref,
  })
end

return M
