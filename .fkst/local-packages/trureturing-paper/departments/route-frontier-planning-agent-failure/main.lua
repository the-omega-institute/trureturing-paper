local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_no_progress", "paper_agent_task_blocked" },
  produces = {
    "paper_frontier_planning_no_progress",
    "paper_frontier_planning_blocked",
    "paper_frontier_planning_retry_requested",
  },
  stall_window = "5m",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "frontier-planning" then
    return
  end
  if payload.agent_role ~= "paper-formalization-frontier-planner"
      or payload.context_mode ~= "promotion-bound-planning"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.paper_id) ~= "string"
      or payload.paper_id == ""
      or type(payload.blocker_code) ~= "string"
      or payload.blocker_code == ""
      or type(payload.summary) ~= "string"
      or payload.summary == "" then
    error("route-frontier-planning-agent-failure: result identity is invalid")
  end

  local queue
  if payload.status == "no-progress" then
    queue = "paper_frontier_planning_no_progress"
  elseif payload.status == "blocked" then
    queue = "paper_frontier_planning_blocked"
  else
    error("route-frontier-planning-agent-failure: unsupported status")
  end

  local portfolio_task_ref = payload.portfolio_task_ref or ""
  local failure = {
    schema = "paper-frontier-planning-agent-failure.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    portfolio_task_ref = portfolio_task_ref,
    status = payload.status,
    blocker_code = payload.blocker_code,
    summary = payload.summary,
    next_route = "frontier-planning",
    dedup_key = "paper-frontier-planning-failure:v1:" .. payload.result_ref,
  }
  raise(queue, failure)
  raise("paper_frontier_planning_retry_requested", {
    schema = "paper-frontier-planning-retry-requested.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    portfolio_task_ref = portfolio_task_ref,
    blocker_code = payload.blocker_code,
    next_route = "frontier-planning",
    dedup_key = "paper-frontier-planning-retry:v1:" .. payload.task_ref .. ":" .. payload.blocker_code,
  })
end

return M
