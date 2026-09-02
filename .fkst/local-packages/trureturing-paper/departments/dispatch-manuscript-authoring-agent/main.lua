local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_certified_claim_manifest_ready" },
  produces = { "paper_agent_task_requested" },
  stall_window = "5m",
}

local function require_digest(value, name)
  if not agent.is_sha256(value) then
    error("dispatch-manuscript-authoring-agent: " .. name .. " must be sha256")
  end
  return value
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.outcome ~= nil and payload.outcome ~= "eligible" then
    return
  end
  local evaluation_ref = require_digest(
    payload.evaluation_ref,
    "evaluation_ref")
  local claim_manifest_ref = require_digest(
    payload.claim_manifest_ref,
    "claim_manifest_ref")
  local eligibility_ref = require_digest(
    payload.eligibility_ref,
    "eligibility_ref")
  local manuscript_plan_ref = require_digest(
    payload.manuscript_plan_ref,
    "manuscript_plan_ref")

  local paths = research.paths(agent.repository_root())
  local staged = research.run(paths, {
    "stage-manuscript-authoring-task",
    "--repository-root", paths.root,
    "--evaluation-ref", evaluation_ref,
    "--claim-manifest-ref", claim_manifest_ref,
    "--eligibility-ref", eligibility_ref,
  }, paths.agent_cli)
  if type(staged) ~= "table"
      or staged.schema ~= "paper-manuscript-authoring-agent-task-staged.v1"
      or not agent.is_sha256(staged.dispatch_ref)
      or not agent.is_sha256(staged.task_ref)
      or not agent.is_sha256(staged.theory_program_ref)
      or not agent.is_sha256(staged.completion_ref)
      or staged.evaluation_ref ~= evaluation_ref
      or staged.claim_manifest_ref ~= claim_manifest_ref
      or staged.eligibility_ref ~= eligibility_ref
      or staged.manuscript_plan_ref ~= manuscript_plan_ref
      or staged.phase ~= "manuscript-authoring"
      or staged.agent_role ~= "paper-manuscript-author"
      or staged.context_mode ~= "certified-claims-only"
      or type(staged.task_path) ~= "string"
      or staged.task_path == "" then
    error("dispatch-manuscript-authoring-agent: Agent CLI returned an invalid staged task")
  end

  local registered = research.run(paths, {
    "register-task",
    "--repository-root", paths.root,
    "--task", staged.task_path,
  }, paths.agent_cli)
  if type(registered) ~= "table"
      or registered.schema ~= "paper-agent-task-registered.v1"
      or registered.task_ref ~= staged.task_ref
      or registered.paper_id ~= staged.paper_id
      or registered.theory_program_ref ~= staged.theory_program_ref
      or registered.phase ~= staged.phase
      or registered.agent_role ~= staged.agent_role
      or registered.context_mode ~= staged.context_mode then
    error("dispatch-manuscript-authoring-agent: task registration changed staged identity")
  end

  raise("paper_agent_task_requested", {
    task_ref = registered.task_ref,
    paper_id = registered.paper_id,
    theory_program_ref = registered.theory_program_ref,
    phase = registered.phase,
    agent_role = registered.agent_role,
    context_mode = registered.context_mode,
    manuscript_dispatch_ref = staged.dispatch_ref,
    completion_ref = staged.completion_ref,
    evaluation_ref = staged.evaluation_ref,
    claim_manifest_ref = staged.claim_manifest_ref,
    eligibility_ref = staged.eligibility_ref,
    manuscript_plan_ref = staged.manuscript_plan_ref,
    replayed = staged.replayed == true or registered.replayed == true,
    dedup_key = "paper-agent-task:v1:" .. registered.task_ref,
  })
end

return M
