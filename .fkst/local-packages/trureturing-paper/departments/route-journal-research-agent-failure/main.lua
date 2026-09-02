local M = {}
local agent = require("agent_runtime")

M.spec = {
  consumes = {
    "paper_agent_task_no_progress",
    "paper_agent_task_blocked",
  },
  produces = {
    "paper_journal_research_no_progress",
    "paper_journal_research_blocked",
    "paper_journal_research_retry_requested",
  },
  stall_window = "5m",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "journal-research" then
    return
  end
  if payload.agent_role ~= "paper-journal-researcher"
      or payload.context_mode ~= "source-bundle-only"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.paper_id) ~= "string"
      or payload.paper_id == ""
      or type(payload.blocker_code) ~= "string"
      or payload.blocker_code == ""
      or type(payload.summary) ~= "string"
      or payload.summary == "" then
    error("route-journal-research-agent-failure: result identity is invalid")
  end

  local base = {
    schema = "paper-journal-research-agent-failure.v1",
    task_ref = payload.task_ref,
    result_ref = payload.result_ref,
    source_scientific_editing_task_ref = payload.source_scientific_editing_task_ref or "",
    source_edited_manuscript_ref = payload.source_edited_manuscript_ref or "",
    paper_id = payload.paper_id,
    theory_program_ref = payload.theory_program_ref,
    status = payload.status,
    blocker_code = payload.blocker_code,
    summary = payload.summary,
    next_route = payload.next_route,
  }

  if payload.status == "no-progress"
      and payload.next_route == "journal-research" then
    raise("paper_journal_research_no_progress", base)
    raise("paper_journal_research_retry_requested", {
      schema = "paper-journal-research-retry-requested.v1",
      task_ref = payload.task_ref,
      result_ref = payload.result_ref,
      paper_id = payload.paper_id,
      theory_program_ref = payload.theory_program_ref,
      blocker_code = payload.blocker_code,
      next_route = "journal-research",
      dedup_key = "paper-journal-research-retry:v1:" .. payload.result_ref,
    })
    return
  end

  if payload.status == "blocked" and payload.next_route == "blocked" then
    raise("paper_journal_research_blocked", base)
    return
  end
  error("route-journal-research-agent-failure: status and route are inconsistent")
end

return M
