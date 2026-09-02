local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_scientifically_edited_manuscript_ready" },
  produces = { "paper_agent_task_requested" },
  stall_window = "5m",
}

function pipeline(event)
  local payload = event.payload or {}
  if payload.next_route ~= "journal-research"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.paper_id) ~= "string"
      or payload.paper_id == ""
      or type(payload.edited_manuscript) ~= "table"
      or payload.edited_manuscript.schema ~= "paper-scientifically-edited-manuscript.v1"
      or not agent.is_sha256(payload.edited_manuscript.artifact_ref) then
    error("dispatch-journal-research-agent: exact edited manuscript identity is required")
  end

  local paths = research.paths(agent.repository_root())
  local staged = research.run(paths, {
    "stage-journal-research-task",
    "--repository-root", paths.root,
    "--source-scientific-editing-task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(staged) ~= "table"
      or staged.schema ~= "paper-journal-research-agent-task-staged.v1"
      or not agent.is_sha256(staged.dispatch_ref)
      or not agent.is_sha256(staged.task_ref)
      or staged.source_scientific_editing_task_ref ~= payload.task_ref
      or staged.source_edited_manuscript_ref ~= payload.edited_manuscript.artifact_ref
      or staged.paper_id ~= payload.paper_id
      or staged.theory_program_ref ~= payload.theory_program_ref
      or staged.phase ~= "journal-research"
      or staged.agent_role ~= "paper-journal-researcher"
      or staged.context_mode ~= "source-bundle-only" then
    error("dispatch-journal-research-agent: Agent CLI returned an invalid staged task")
  end

  local registered = research.run(paths, {
    "register-task",
    "--repository-root", paths.root,
    "--task", staged.task_path,
  }, paths.agent_cli)
  if type(registered) ~= "table"
      or registered.schema ~= "paper-agent-task-registered.v1"
      or registered.task_ref ~= staged.task_ref
      or registered.paper_id ~= staged.paper_id
      or registered.theory_program_ref ~= staged.theory_program_ref
      or registered.phase ~= staged.phase
      or registered.agent_role ~= staged.agent_role
      or registered.context_mode ~= staged.context_mode then
    error("dispatch-journal-research-agent: registration changed staged task identity")
  end

  raise("paper_agent_task_requested", {
    task_ref = registered.task_ref,
    paper_id = registered.paper_id,
    theory_program_ref = registered.theory_program_ref,
    phase = registered.phase,
    agent_role = registered.agent_role,
    context_mode = registered.context_mode,
    journal_research_dispatch_ref = staged.dispatch_ref,
    source_scientific_editing_task_ref = staged.source_scientific_editing_task_ref,
    source_edited_manuscript_ref = staged.source_edited_manuscript_ref,
    replayed = staged.replayed == true or registered.replayed == true,
    dedup_key = "paper-agent-task:v1:" .. registered.task_ref,
  })
end

return M
