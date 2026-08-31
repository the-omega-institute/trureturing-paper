local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_theory_deepening_requested" },
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
  if dispatch_path == "" then
    error("dispatch-theory-deepening-agent: dispatch_path is required")
  end
  if payload.paper_id == nil
      or not agent.is_sha256(payload.theory_program_ref)
      or not agent.is_sha256(payload.request_ref) then
    error("dispatch-theory-deepening-agent: exact paper, program, and request identity is required")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local staged = research.run(paths, {
    "stage-deepening-task",
    "--repository-root", paths.root,
    "--dispatch", dispatch_path,
  }, paths.agent_cli)
  if type(staged) ~= "table"
      or staged.schema ~= "paper-theory-deepening-agent-task-staged.v1"
      or not agent.is_sha256(staged.dispatch_ref)
      or not agent.is_sha256(staged.task_ref)
      or not agent.is_sha256(staged.theory_program_ref)
      or not agent.is_sha256(staged.request_ref)
      or staged.round == nil
      or staged.round < 1
      or staged.phase ~= "theory-deepening"
      or staged.agent_role ~= "paper-theory-developer"
      or staged.context_mode ~= "contextual-theory-execution" then
    error("dispatch-theory-deepening-agent: Agent CLI returned an invalid staged task")
  end
  if payload.paper_id ~= staged.paper_id
      or payload.theory_program_ref ~= staged.theory_program_ref
      or payload.request_ref ~= staged.request_ref then
    error("dispatch-theory-deepening-agent: staged task changed event identity")
  end
  if payload.round ~= nil and payload.round ~= staged.round then
    error("dispatch-theory-deepening-agent: staged task changed the A2 round")
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
    error("dispatch-theory-deepening-agent: registration changed staged task identity")
  end

  raise("paper_agent_task_requested", {
    task_ref = registered.task_ref,
    paper_id = registered.paper_id,
    theory_program_ref = registered.theory_program_ref,
    phase = registered.phase,
    agent_role = registered.agent_role,
    context_mode = registered.context_mode,
    deepening_dispatch_ref = staged.dispatch_ref,
    deepening_request_ref = staged.request_ref,
    round = staged.round,
    replayed = staged.replayed == true or registered.replayed == true,
    dedup_key = "paper-agent-task:v1:" .. registered.task_ref,
  })
end

return M
