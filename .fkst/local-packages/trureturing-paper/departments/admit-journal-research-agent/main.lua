local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = { "paper_journal_target_ready" },
  stall_window = "5m",
}

local function require_stored(value, schema, name)
  if type(value) ~= "table"
      or value.schema ~= schema
      or not agent.is_sha256(value.artifact_ref)
      or type(value.content_path) ~= "string"
      or value.content_path == ""
      or not agent.is_sha256(value.envelope_ref)
      or type(value.envelope_path) ~= "string"
      or value.envelope_path == "" then
    error("admit-journal-research-agent: invalid " .. name .. " coordinate")
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "journal-research" then
    return
  end
  if payload.status ~= "completed"
      or payload.agent_role ~= "paper-journal-researcher"
      or payload.context_mode ~= "source-bundle-only"
      or payload.next_route ~= "journal-style-editing"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.paper_id) ~= "string"
      or payload.paper_id == "" then
    error("admit-journal-research-agent: completed event identity is invalid")
  end

  local paths = research.paths(agent.repository_root())
  local admitted = research.run(paths, {
    "admit-journal-research-result",
    "--repository-root", paths.root,
    "--task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-journal-research-agent-result-admitted.v1"
      or admitted.task_ref ~= payload.task_ref
      or admitted.result_ref ~= payload.result_ref
      or admitted.paper_id ~= payload.paper_id
      or admitted.theory_program_ref ~= payload.theory_program_ref
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.source_scientific_editing_task_ref)
      or not agent.is_sha256(admitted.source_edited_manuscript_ref)
      or type(admitted.selected_venue_id) ~= "string"
      or admitted.selected_venue_id == ""
      or type(admitted.selected_journal_name) ~= "string"
      or admitted.selected_journal_name == ""
      or type(admitted.selected_publication_tier) ~= "number"
      or admitted.selected_publication_tier < 1
      or admitted.selected_publication_tier > 2
      or type(admitted.selected_article_type) ~= "string"
      or admitted.selected_article_type == ""
      or admitted.next_route ~= "journal-style-editing" then
    error("admit-journal-research-agent: Agent CLI returned an invalid admission")
  end
  require_stored(
    admitted.dossier,
    "paper-journal-research-dossier.v1",
    "journal dossier")
  require_stored(
    admitted.target_selection,
    "paper-journal-target-selection.v1",
    "journal target selection")
  if type(admitted.scorecards) ~= "table" or #admitted.scorecards < 2 then
    error("admit-journal-research-agent: at least two venue scorecards are required")
  end
  for _, scorecard in ipairs(admitted.scorecards) do
    require_stored(
      scorecard,
      "paper-journal-venue-scorecard.v1",
      "journal venue scorecard")
  end

  raise("paper_journal_target_ready", {
    schema = "paper-journal-target-ready.v1",
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    source_scientific_editing_task_ref = admitted.source_scientific_editing_task_ref,
    source_edited_manuscript_ref = admitted.source_edited_manuscript_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    dossier = admitted.dossier,
    scorecards = admitted.scorecards,
    target_selection = admitted.target_selection,
    selected_venue_id = admitted.selected_venue_id,
    selected_journal_name = admitted.selected_journal_name,
    selected_publication_tier = admitted.selected_publication_tier,
    selected_article_type = admitted.selected_article_type,
    next_route = admitted.next_route,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-journal-target-ready:v1:" .. admitted.target_selection.artifact_ref,
  })
end

return M
