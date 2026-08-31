local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = {
    "paper_theory_scope_ready",
    "paper_theory_inventory_ready",
  },
  stall_window = "5m",
}

local ready_queues = {
  scope = "paper_theory_scope_ready",
  inventory = "paper_theory_inventory_ready",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "theory-scope" and payload.phase ~= "theory-inventory" then
    return
  end
  local task_ref = payload.task_ref
  if not agent.is_sha256(task_ref) then
    error("admit-theory-foundation-agent: task_ref must be sha256")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local admitted = research.run(paths, {
    "admit-foundation-result",
    "--repository-root", paths.root,
    "--task-ref", task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-theory-foundation-agent-result-admitted.v1"
      or admitted.task_ref ~= task_ref
      or not agent.is_sha256(admitted.result_ref)
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.theory_program_ref)
      or not agent.is_sha256(admitted.request_ref)
      or not agent.is_sha256(admitted.domain_ref)
      or not agent.is_sha256(admitted.envelope_ref) then
    error("admit-theory-foundation-agent: Agent CLI returned an invalid admission")
  end
  if payload.result_ref ~= nil and payload.result_ref ~= admitted.result_ref then
    error("admit-theory-foundation-agent: result identity changed")
  end
  if payload.paper_id ~= nil and payload.paper_id ~= admitted.paper_id then
    error("admit-theory-foundation-agent: paper identity changed")
  end
  if payload.theory_program_ref ~= nil
      and payload.theory_program_ref ~= admitted.theory_program_ref then
    error("admit-theory-foundation-agent: theory program changed")
  end

  local queue = ready_queues[admitted.kind]
  if not queue then
    error("admit-theory-foundation-agent: unsupported admitted kind")
  end
  raise(queue, {
    schema = "paper-theory-foundation-ready.v1",
    kind = admitted.kind,
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    request_ref = admitted.request_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    domain_schema = admitted.domain_schema,
    domain_ref = admitted.domain_ref,
    domain_content_path = admitted.domain_content_path,
    envelope_ref = admitted.envelope_ref,
    envelope_path = admitted.envelope_path,
    next_route = admitted.next_route,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-theory-foundation-ready:v1:" .. admitted.task_ref,
  })
end

return M
