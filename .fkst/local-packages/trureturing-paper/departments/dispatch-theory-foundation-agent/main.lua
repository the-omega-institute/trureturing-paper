local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = {
    "paper_theory_scope_requested",
    "paper_theory_inventory_requested",
  },
  produces = { "paper_agent_task_requested" },
  stall_window = "5m",
}

local function text(value)
  if type(value) == "string" then return value end
  return ""
end

function pipeline(event)
  local payload = event.payload or {}
  local dispatch_path = text(payload.dispatch_path)
  local expected_kind = text(payload.kind)
  local paper_id = text(payload.paper_id)
  local theory_program_ref = payload.theory_program_ref
  local request_ref = payload.request_ref
  if dispatch_path == "" then
    error("dispatch-theory-foundation-agent: dispatch_path is required")
  end
  if expected_kind ~= "scope" and expected_kind ~= "inventory" then
    error("dispatch-theory-foundation-agent: kind must be scope or inventory")
  end
  if paper_id == ""
      or not agent.is_sha256(theory_program_ref)
      or not agent.is_sha256(request_ref) then
    error("dispatch-theory-foundation-agent: exact paper, program, and request identity is required")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local staged = research.run(paths, {
    "stage-foundation-task",
    "--repository-root", paths.root,
    "--dispatch", dispatch_path,
  }, paths.agent_cli)
  if type(staged) ~= "table"
      or staged.schema ~= "paper-theory-foundation-agent-task-staged.v1"
      or staged.kind ~= expected_kind
      or not agent.is_sha256(staged.dispatch_ref)
      or not agent.is_sha256(staged.task_ref)
      or not agent.is_sha256(staged.theory_program_ref)
      or not agent.is_sha256(staged.request_ref) then
    error("dispatch-theory-foundation-agent: Agent CLI returned an invalid staged task")
  end
  if paper_id ~= staged.paper_id then
    error("dispatch-theory-foundation-agent: paper identity changed")
  end
  if theory_program_ref ~= staged.theory_program_ref then
    error("dispatch-theory-foundation-agent: theory program changed")
  end
  if request_ref ~= staged.request_ref then
    error("dispatch-theory-foundation-agent: domain request changed")
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
    error("dispatch-theory-foundation-agent: registration changed staged task identity")
  end

  raise("paper_agent_task_requested", {
    task_ref = registered.task_ref,
    paper_id = registered.paper_id,
    theory_program_ref = registered.theory_program_ref,
    phase = registered.phase,
    agent_role = registered.agent_role,
    context_mode = registered.context_mode,
    foundation_kind = staged.kind,
    foundation_dispatch_ref = staged.dispatch_ref,
    foundation_request_ref = staged.request_ref,
    replayed = staged.replayed == true or registered.replayed == true,
    dedup_key = "paper-agent-task:v1:" .. registered.task_ref,
  })
end

return M
