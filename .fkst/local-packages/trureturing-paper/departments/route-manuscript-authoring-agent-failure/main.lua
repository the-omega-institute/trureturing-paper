local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = {
    "paper_agent_task_no_progress",
    "paper_agent_task_blocked",
  },
  produces = {
    "paper_manuscript_authoring_no_progress",
    "paper_manuscript_authoring_blocked",
    "paper_manuscript_authoring_retry_requested",
  },
  stall_window = "5m",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "manuscript-authoring" then
    return
  end
  if payload.agent_role ~= "paper-manuscript-author"
      or payload.context_mode ~= "certified-claims-only"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.blocker_code) ~= "string"
      or payload.blocker_code == ""
      or type(payload.summary) ~= "string"
      or payload.summary == "" then
    error("route-manuscript-authoring-agent-failure: result identity is invalid")
  end

  local queue = nil
  if payload.status == "no-progress"
      and payload.next_route == "manuscript-authoring" then
    queue = "paper_manuscript_authoring_no_progress"
  elseif payload.status == "blocked"
      and payload.next_route == "blocked" then
    queue = "paper_manuscript_authoring_blocked"
  else
    error("route-manuscript-authoring-agent-failure: status and next route disagree")
  end

  local failure = {
    schema = "paper-manuscript-authoring-agent-failure.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    status = payload.status,
    blocker_code = payload.blocker_code,
    summary = payload.summary,
    next_route = payload.next_route,
    dedup_key = "paper-manuscript-authoring-failure:v1:" .. payload.result_ref,
  }
  raise(queue, failure)

  if payload.status == "no-progress" then
    raise("paper_manuscript_authoring_retry_requested", {
      schema = "paper-manuscript-authoring-retry-requested.v1",
      prior_task_ref = payload.task_ref,
      prior_result_ref = payload.result_ref,
      paper_id = payload.paper_id,
      theory_program_ref = payload.theory_program_ref,
      blocker_code = payload.blocker_code,
      summary = payload.summary,
      next_route = "manuscript-authoring",
      dedup_key = "paper-manuscript-authoring-retry:v1:" .. payload.result_ref,
    })
  end
end

return M
