local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = {
    "paper_agent_task_no_progress",
    "paper_agent_task_blocked",
  },
  produces = {
    "paper_theory_audit_reviewer_no_progress",
    "paper_theory_audit_reviewer_blocked",
    "paper_theory_audit_reviewer_replacement_requested",
  },
  stall_window = "5m",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "theory-audit" then
    return
  end
  if payload.agent_role ~= "paper-theory-independent-referee"
      or payload.context_mode ~= "fresh-theory-review"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.paper_id) ~= "string"
      or type(payload.blocker_code) ~= "string"
      or payload.blocker_code == "" then
    error("route-theory-audit-agent-failure: reviewer failure identity is invalid")
  end

  local queue
  if payload.status == "no-progress" then
    queue = "paper_theory_audit_reviewer_no_progress"
  elseif payload.status == "blocked" then
    queue = "paper_theory_audit_reviewer_blocked"
  else
    error("route-theory-audit-agent-failure: unsupported reviewer status")
  end

  raise(queue, {
    schema = "paper-theory-audit-agent-failure.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    status = payload.status,
    summary = payload.summary,
    blocker_code = payload.blocker_code,
    next_route = "theory-audit",
    run_id = payload.run_id,
    provenance = payload.provenance,
    replayed = payload.replayed == true,
    dedup_key = "paper-a3-reviewer-failure:v1:" .. payload.task_ref,
  })

  raise("paper_theory_audit_reviewer_replacement_requested", {
    schema = "paper-theory-audit-reviewer-replacement-requested.v1",
    failed_task_ref = payload.task_ref,
    failed_result_ref = payload.result_ref,
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    blocker_code = payload.blocker_code,
    next_route = "theory-audit",
    dedup_key = "paper-a3-reviewer-replacement:v1:" .. payload.task_ref,
  })
end

return M
