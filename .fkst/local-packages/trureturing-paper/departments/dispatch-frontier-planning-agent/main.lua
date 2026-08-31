local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_formalization_frontier_requested" },
  produces = { "paper_agent_task_requested" },
  stall_window = "5m",
}

local function text(value)
  if type(value) == "string" then return value end
  return ""
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.schema ~= "paper-portfolio-route-ready.v1"
      or payload.action ~= "promote-to-frontier"
      or payload.next_route ~= "frontier-planning"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.dispatch_ref)
      or not agent.is_sha256(payload.portfolio_ref)
      or not agent.is_sha256(payload.candidate_batch_ref)
      or not agent.is_sha256(payload.decision_ref)
      or not agent.is_sha256(payload.updated_portfolio_ref)
      or not agent.is_sha256(payload.judgment_evidence_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or not agent.is_sha256(payload.scorecard_ref)
      or type(payload.cycle_number) ~= "number"
      or payload.cycle_number < 1
      or text(payload.paper_id) == "" then
    error("dispatch-frontier-planning-agent: exact promoted portfolio route is required")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local staged = research.run(paths, {
    "stage-frontier-planning-task",
    "--repository-root", paths.root,
    "--portfolio-task-ref", payload.task_ref,
    "--paper-id", payload.paper_id,
  }, paths.agent_cli)
  if type(staged) ~= "table"
      or staged.schema ~= "paper-frontier-planning-agent-task-staged.v1"
      or not agent.is_sha256(staged.dispatch_ref)
      or not agent.is_sha256(staged.task_ref)
      or not agent.is_sha256(staged.portfolio_task_ref)
      or not agent.is_sha256(staged.portfolio_result_ref)
      or not agent.is_sha256(staged.portfolio_ref)
      or not agent.is_sha256(staged.theory_program_ref)
      or not agent.is_sha256(staged.theorem_package_ref)
      or not agent.is_sha256(staged.scorecard_ref)
      or not agent.is_sha256(staged.portfolio_decision_ref)
      or type(staged.cycle_number) ~= "number"
      or staged.cycle_number < 1
      or staged.phase ~= "frontier-planning"
      or staged.agent_role ~= "paper-formalization-frontier-planner"
      or staged.context_mode ~= "promotion-bound-planning" then
    error("dispatch-frontier-planning-agent: Agent CLI returned an invalid staged task")
  end
  if staged.portfolio_task_ref ~= payload.task_ref
      or staged.portfolio_result_ref ~= payload.result_ref
      or staged.portfolio_ref ~= payload.portfolio_ref
      or staged.cycle_number ~= payload.cycle_number
      or staged.paper_id ~= payload.paper_id
      or staged.theory_program_ref ~= payload.theory_program_ref
      or staged.scorecard_ref ~= payload.scorecard_ref
      or staged.portfolio_decision_ref ~= payload.decision_ref then
    error("dispatch-frontier-planning-agent: staged task changed the promotion route")
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
    error("dispatch-frontier-planning-agent: registration changed staged task identity")
  end

  raise("paper_agent_task_requested", {
    task_ref = registered.task_ref,
    paper_id = registered.paper_id,
    theory_program_ref = registered.theory_program_ref,
    phase = registered.phase,
    agent_role = registered.agent_role,
    context_mode = registered.context_mode,
    frontier_dispatch_ref = staged.dispatch_ref,
    portfolio_task_ref = staged.portfolio_task_ref,
    portfolio_result_ref = staged.portfolio_result_ref,
    portfolio_ref = staged.portfolio_ref,
    cycle_number = staged.cycle_number,
    theorem_package_ref = staged.theorem_package_ref,
    scorecard_ref = staged.scorecard_ref,
    portfolio_decision_ref = staged.portfolio_decision_ref,
    replayed = staged.replayed == true or registered.replayed == true,
    dedup_key = "paper-agent-task:v1:" .. registered.task_ref,
  })
end

return M
