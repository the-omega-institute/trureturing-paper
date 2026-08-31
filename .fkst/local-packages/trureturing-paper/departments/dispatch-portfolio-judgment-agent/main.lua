local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_portfolio_judgment_requested" },
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
    error("dispatch-portfolio-judgment-agent: dispatch_path is required")
  end
  if not agent.is_sha256(payload.portfolio_ref)
      or not agent.is_sha256(payload.candidate_batch_ref)
      or type(payload.cycle_number) ~= "number"
      or payload.cycle_number < 1 then
    error("dispatch-portfolio-judgment-agent: exact portfolio, batch, and cycle identity is required")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local staged = research.run(paths, {
    "stage-portfolio-judgment-task",
    "--repository-root", paths.root,
    "--dispatch", dispatch_path,
  }, paths.agent_cli)
  if type(staged) ~= "table"
      or staged.schema ~= "paper-portfolio-judgment-agent-task-staged.v1"
      or not agent.is_sha256(staged.dispatch_ref)
      or not agent.is_sha256(staged.task_ref)
      or not agent.is_sha256(staged.portfolio_ref)
      or not agent.is_sha256(staged.candidate_batch_ref)
      or type(staged.cycle_number) ~= "number"
      or staged.cycle_number < 1
      or type(staged.compared_paper_count) ~= "number"
      or staged.compared_paper_count < 2
      or staged.phase ~= "portfolio-judgment"
      or staged.agent_role ~= "paper-portfolio-judge"
      or staged.context_mode ~= "cross-paper-comparison" then
    error("dispatch-portfolio-judgment-agent: Agent CLI returned an invalid staged task")
  end
  if staged.portfolio_ref ~= payload.portfolio_ref
      or staged.candidate_batch_ref ~= payload.candidate_batch_ref
      or staged.cycle_number ~= payload.cycle_number then
    error("dispatch-portfolio-judgment-agent: staged task changed event identity")
  end

  local registered = research.run(paths, {
    "register-task",
    "--repository-root", paths.root,
    "--task", staged.task_path,
  }, paths.agent_cli)
  if type(registered) ~= "table"
      or registered.schema ~= "paper-agent-task-registered.v1"
      or registered.task_ref ~= staged.task_ref
      or registered.theory_program_ref ~= staged.portfolio_ref
      or registered.phase ~= staged.phase
      or registered.agent_role ~= staged.agent_role
      or registered.context_mode ~= staged.context_mode then
    error("dispatch-portfolio-judgment-agent: registration changed staged task identity")
  end

  raise("paper_agent_task_requested", {
    task_ref = registered.task_ref,
    paper_id = registered.paper_id,
    theory_program_ref = registered.theory_program_ref,
    phase = registered.phase,
    agent_role = registered.agent_role,
    context_mode = registered.context_mode,
    portfolio_dispatch_ref = staged.dispatch_ref,
    portfolio_ref = staged.portfolio_ref,
    candidate_batch_ref = staged.candidate_batch_ref,
    cycle_number = staged.cycle_number,
    compared_paper_count = staged.compared_paper_count,
    replayed = staged.replayed == true or registered.replayed == true,
    dedup_key = "paper-agent-task:v1:" .. registered.task_ref,
  })
end

return M
