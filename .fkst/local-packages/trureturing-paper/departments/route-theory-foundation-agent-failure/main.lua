local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = {
    "paper_agent_task_no_progress",
    "paper_agent_task_blocked",
  },
  produces = {
    "paper_theory_scope_no_progress",
    "paper_theory_scope_blocked",
    "paper_theory_inventory_no_progress",
    "paper_theory_inventory_blocked",
  },
  stall_window = "5m",
}

local queues = {
  ["theory-scope:no-progress"] = "paper_theory_scope_no_progress",
  ["theory-scope:blocked"] = "paper_theory_scope_blocked",
  ["theory-inventory:no-progress"] = "paper_theory_inventory_no_progress",
  ["theory-inventory:blocked"] = "paper_theory_inventory_blocked",
}

local expected_routes = {
  ["theory-scope:no-progress"] = "theory-scope",
  ["theory-scope:blocked"] = "blocked",
  ["theory-inventory:no-progress"] = "theory-inventory",
  ["theory-inventory:blocked"] = "blocked",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "theory-scope" and payload.phase ~= "theory-inventory" then
    return
  end
  if payload.status ~= "no-progress" and payload.status ~= "blocked" then
    error("route-theory-foundation-agent-failure: unsupported status")
  end
  if not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref) then
    error("route-theory-foundation-agent-failure: result identity must be content-addressed")
  end
  local key = payload.phase .. ":" .. payload.status
  local queue = queues[key]
  if not queue then
    error("route-theory-foundation-agent-failure: no domain route for result")
  end
  if payload.next_route ~= expected_routes[key] then
    error("route-theory-foundation-agent-failure: status selected an invalid domain route")
  end

  raise(queue, {
    schema = "paper-theory-foundation-agent-failure.v1",
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
    dedup_key = "paper-theory-foundation-failure:v1:" .. payload.task_ref,
  })
end

return M
