local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = {
    "paper_theory_deepening_ready",
    "paper_candidate_split_proposed",
    "paper_candidate_merge_research_requested",
    "paper_research_ledger_entry_ready",
  },
  stall_window = "5m",
}

local function require_artifact(value, name)
  if type(value) ~= "table"
      or type(value.schema) ~= "string"
      or not agent.is_sha256(value.artifact_ref)
      or type(value.content_path) ~= "string"
      or not agent.is_sha256(value.envelope_ref)
      or type(value.envelope_path) ~= "string" then
    error("admit-theory-deepening-agent: invalid " .. name .. " coordinate")
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "theory-deepening" then
    return
  end
  if payload.status ~= "completed"
      or payload.agent_role ~= "paper-theory-developer"
      or payload.context_mode ~= "contextual-theory-execution"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref) then
    error("admit-theory-deepening-agent: completed event identity is invalid")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local admitted = research.run(paths, {
    "admit-deepening-result",
    "--repository-root", paths.root,
    "--task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-theory-deepening-agent-result-admitted.v1"
      or admitted.task_ref ~= payload.task_ref
      or admitted.result_ref ~= payload.result_ref
      or admitted.paper_id ~= payload.paper_id
      or admitted.theory_program_ref ~= payload.theory_program_ref
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.request_ref)
      or type(admitted.round) ~= "number"
      or admitted.round < 1
      or (admitted.maturity ~= "developing" and admitted.maturity ~= "audit-candidate")
      or (admitted.next_route ~= "theory-deepening" and admitted.next_route ~= "theory-audit") then
    error("admit-theory-deepening-agent: Agent CLI returned an invalid admission")
  end
  if payload.next_route ~= admitted.next_route then
    error("admit-theory-deepening-agent: admitted route changed the completed result")
  end
  require_artifact(admitted.iteration, "iteration")
  require_artifact(admitted.theorem_package, "theorem package")
  require_artifact(admitted.delta, "computed delta")
  if admitted.iteration.schema ~= "paper-theory-iteration.v1"
      or admitted.theorem_package.schema ~= "paper-theorem-package.v1"
      or admitted.delta.schema ~= "paper-theory-deepening-delta.v1" then
    error("admit-theory-deepening-agent: core A2 artifact schemas are invalid")
  end

  for _, split in ipairs(admitted.split_proposals or {}) do
    require_artifact(split, "split proposal")
    if split.schema ~= "paper-candidate-split-proposal.v1" then
      error("admit-theory-deepening-agent: split proposal schema is invalid")
    end
    raise("paper_candidate_split_proposed", {
      schema = "paper-candidate-split-proposed.v1",
      task_ref = admitted.task_ref,
      result_ref = admitted.result_ref,
      paper_id = admitted.paper_id,
      theory_program_ref = admitted.theory_program_ref,
      theorem_package_ref = admitted.theorem_package.artifact_ref,
      split_proposal_ref = split.artifact_ref,
      split_proposal_content_path = split.content_path,
      split_proposal_envelope_ref = split.envelope_ref,
      split_proposal_envelope_path = split.envelope_path,
      dedup_key = "paper-split-proposal:v1:" .. split.artifact_ref,
    })
  end

  for _, target_paper_id in ipairs(admitted.merge_candidate_paper_ids or {}) do
    raise("paper_candidate_merge_research_requested", {
      schema = "paper-candidate-merge-research-requested.v1",
      task_ref = admitted.task_ref,
      result_ref = admitted.result_ref,
      source_paper_id = admitted.paper_id,
      source_theory_program_ref = admitted.theory_program_ref,
      source_theorem_package_ref = admitted.theorem_package.artifact_ref,
      target_paper_id = target_paper_id,
      dedup_key = "paper-merge-research:v1:" .. admitted.theorem_package.artifact_ref .. ":" .. target_paper_id,
    })
  end

  for _, entry in ipairs(admitted.research_ledger_entries or {}) do
    require_artifact(entry, "research ledger entry")
    if entry.schema ~= "paper-research-ledger-entry.v1" then
      error("admit-theory-deepening-agent: research ledger schema is invalid")
    end
    raise("paper_research_ledger_entry_ready", {
      schema = "paper-research-ledger-entry-ready.v1",
      task_ref = admitted.task_ref,
      result_ref = admitted.result_ref,
      paper_id = admitted.paper_id,
      theory_program_ref = admitted.theory_program_ref,
      theorem_package_ref = admitted.theorem_package.artifact_ref,
      ledger_entry_ref = entry.artifact_ref,
      ledger_entry_content_path = entry.content_path,
      ledger_entry_envelope_ref = entry.envelope_ref,
      ledger_entry_envelope_path = entry.envelope_path,
      dedup_key = "paper-research-ledger:v1:" .. entry.artifact_ref,
    })
  end

  raise("paper_theory_deepening_ready", {
    schema = "paper-theory-deepening-ready.v1",
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    request_ref = admitted.request_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    round = admitted.round,
    iteration = admitted.iteration,
    theorem_package = admitted.theorem_package,
    delta = admitted.delta,
    split_proposals = admitted.split_proposals,
    research_ledger_entries = admitted.research_ledger_entries,
    merge_candidate_paper_ids = admitted.merge_candidate_paper_ids,
    maturity = admitted.maturity,
    next_route = admitted.next_route,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-theory-deepening-ready:v1:" .. admitted.task_ref,
  })
end

return M
