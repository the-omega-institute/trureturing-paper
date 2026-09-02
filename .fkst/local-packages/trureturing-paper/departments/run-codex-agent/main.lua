local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_requested" },
  produces = {
    "paper_agent_task_completed",
    "paper_agent_task_no_progress",
    "paper_agent_task_blocked",
  },
  stall_window = "4h",
}

function pipeline(event)
  local payload = event.payload or {}
  local task_ref = payload.task_ref
  if not agent.is_sha256(task_ref) then
    error("run-codex-agent: task_ref must be sha256")
  end
  local root = agent.repository_root()
  local paths = research.paths(root)
  local recorded = nil
  with_lock(agent.lock_key(task_ref), function()
    recorded = agent.execute(paths, task_ref)
  end)
  if type(recorded) ~= "table" then
    error("run-codex-agent: task lock returned no recorded result")
  end
  if payload.paper_id ~= nil and payload.paper_id ~= recorded.paper_id then
    error("run-codex-agent: event paper identity differs from the registered task")
  end
  if payload.theory_program_ref ~= nil
      and payload.theory_program_ref ~= recorded.theory_program_ref then
    error("run-codex-agent: event theory program differs from the registered task")
  end
  if payload.phase ~= nil and payload.phase ~= recorded.phase then
    error("run-codex-agent: event phase differs from the registered task")
  end

  local queue = agent.result_queue(recorded.status)
  raise(queue, {
    schema = "paper-agent-result-recorded.v1",
    task_ref = recorded.task_ref,
    result_ref = recorded.result_ref,
    paper_id = recorded.paper_id,
    theory_program_ref = recorded.theory_program_ref,
    phase = recorded.phase,
    agent_role = recorded.agent_role,
    context_mode = recorded.context_mode,
    status = recorded.status,
    summary = recorded.summary,
    outputs = recorded.outputs,
    next_route = recorded.next_route,
    blocker_code = recorded.blocker_code,
    run_id = recorded.run_id,
    provenance = recorded.provenance,
    replayed = recorded.replayed == true,
    dedup_key = "paper-agent-result:v1:" .. recorded.task_ref,
  })
end

return M
