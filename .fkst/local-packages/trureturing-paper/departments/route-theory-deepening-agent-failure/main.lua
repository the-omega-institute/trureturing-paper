local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = {
    "paper_agent_task_no_progress",
    "paper_agent_task_blocked",
  },
  produces = {
    "paper_theory_deepening_no_progress",
    "paper_theory_deepening_blocked",
  },
  stall_window = "5m",
}

local queues = {
  ["no-progress"] = "paper_theory_deepening_no_progress",
  ["blocked"] = "paper_theory_deepening_blocked",
}

local expected_routes = {
  ["no-progress"] = "theory-deepening",
  ["blocked"] = "blocked",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "theory-deepening" then
    return
  end
  local queue = queues[payload.status]
  local expected_route = expected_routes[payload.status]
  if not queue
      or payload.next_route ~= expected_route
      or payload.agent_role ~= "paper-theory-developer"
      or payload.context_mode ~= "contextual-theory-execution"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref) then
    error("route-theory-deepening-agent-failure: invalid typed A2 result")
  end

  raise(queue, {
    schema = "paper-theory-deepening-agent-failure.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    phase = payload.phase,
    status = payload.status,
    summary = payload.summary,
    blocker_code = payload.blocker_code,
    next_route = payload.next_route,
    run_id = payload.run_id,
    provenance = payload.provenance,
    replayed = payload.replayed == true,
    dedup_key = "paper-theory-deepening-failure:v1:" .. payload.task_ref,
  })
end

return M
