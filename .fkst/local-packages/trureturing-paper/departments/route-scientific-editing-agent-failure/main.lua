local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = {
    "paper_agent_task_no_progress",
    "paper_agent_task_blocked",
  },
  produces = {
    "paper_scientific_editing_no_progress",
    "paper_scientific_editing_blocked",
    "paper_scientific_editing_retry_requested",
  },
  stall_window = "5m",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "scientific-editing" then
    return
  end
  if payload.agent_role ~= "paper-scientific-editor"
      or payload.context_mode ~= "claim-preserving-edit"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.paper_id) ~= "string"
      or payload.paper_id == ""
      or type(payload.blocker_code) ~= "string"
      or payload.blocker_code == ""
      or type(payload.summary) ~= "string"
      or payload.summary == "" then
    error("route-scientific-editing-agent-failure: result identity is invalid")
  end

  local base = {
    schema = "paper-scientific-editing-agent-failure.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    source_authoring_task_ref = payload.source_authoring_task_ref or "",
    source_manuscript_ref = payload.source_manuscript_ref or "",
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    status = payload.status,
    blocker_code = payload.blocker_code,
    summary = payload.summary,
    next_route = payload.next_route,
  }

  if payload.status == "no-progress"
      and payload.next_route == "scientific-editing" then
    raise("paper_scientific_editing_no_progress", base)
    raise("paper_scientific_editing_retry_requested", {
      schema = "paper-scientific-editing-retry-requested.v1",
      failed_task_ref = payload.task_ref,
      failed_result_ref = payload.result_ref,
      paper_id = payload.paper_id,
      theory_program_ref = payload.theory_program_ref,
      blocker_code = payload.blocker_code,
      next_route = "scientific-editing",
      dedup_key = "paper-scientific-editing-retry:v1:" .. payload.task_ref,
    })
    return
  end

  if payload.status == "blocked" and payload.next_route == "blocked" then
    raise("paper_scientific_editing_blocked", base)
    return
  end

  error("route-scientific-editing-agent-failure: status and next_route disagree")
end

return M
