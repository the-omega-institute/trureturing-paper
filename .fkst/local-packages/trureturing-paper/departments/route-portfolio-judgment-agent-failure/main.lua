local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_no_progress", "paper_agent_task_blocked" },
  produces = {
    "paper_portfolio_judgment_no_progress",
    "paper_portfolio_judgment_blocked",
    "paper_portfolio_judgment_retry_requested",
  },
  stall_window = "5m",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "portfolio-judgment" then
    return
  end
  if payload.agent_role ~= "paper-portfolio-judge"
      or payload.context_mode ~= "cross-paper-comparison"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.blocker_code) ~= "string"
      or payload.blocker_code == ""
      or type(payload.summary) ~= "string"
      or payload.summary == "" then
    error("route-portfolio-judgment-agent-failure: result identity is invalid")
  end

  local queue
  if payload.status == "no-progress" then
    queue = "paper_portfolio_judgment_no_progress"
  elseif payload.status == "blocked" then
    queue = "paper_portfolio_judgment_blocked"
  else
    error("route-portfolio-judgment-agent-failure: unsupported status")
  end

  local failure = {
    schema = "paper-portfolio-judgment-agent-failure.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    portfolio_ref = payload.theory_program_ref,
    cycle_number = payload.cycle_number or 0,
    status = payload.status,
    blocker_code = payload.blocker_code,
    summary = payload.summary,
    next_route = "portfolio-judgment",
    dedup_key = "paper-portfolio-judgment-failure:v1:" .. payload.result_ref,
  }
  raise(queue, failure)
  raise("paper_portfolio_judgment_retry_requested", {
    schema = "paper-portfolio-judgment-retry-requested.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    portfolio_ref = payload.theory_program_ref,
    cycle_number = payload.cycle_number or 0,
    blocker_code = payload.blocker_code,
    next_route = "portfolio-judgment",
    dedup_key = "paper-portfolio-judgment-retry:v1:" .. payload.task_ref .. ":" .. payload.blocker_code,
  })
end

return M
