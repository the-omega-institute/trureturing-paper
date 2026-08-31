local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_formalization_result_recorded" },
  produces = {
    "paper_candidate_pending_certification",
    "paper_intuition_research_requested",
    "paper_sublemma_research_requested",
    "paper_novelty_reassessment_requested",
    "paper_formalization_strategy_revision_requested",
    "paper_formalization_blocked",
  },
  stall_window = "5m",
}

local function text(value)
  if type(value) == "string" then return value end
  return ""
end

local function is_sha256(value)
  return type(value) == "string" and
    #value == 71 and
    value:sub(1, 7) == "sha256:" and
    value:sub(8):match("^[0-9a-f]+$") ~= nil
end

local function repository_root()
  local root = text(env_read("TRURETURING_PAPER_REPOSITORY_ROOT"))
  if root == "" then
    error(
      "classify-formalization-outcome: " ..
      "TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

local route_queues = {
  ["await-certification"] = "paper_candidate_pending_certification",
  ["intuition-research"] = "paper_intuition_research_requested",
  ["sublemma-research"] = "paper_sublemma_research_requested",
  ["novelty-reassessment"] = "paper_novelty_reassessment_requested",
  ["proof-strategy-revision"] =
    "paper_formalization_strategy_revision_requested",
  ["blocked"] = "paper_formalization_blocked",
}

function pipeline(event)
  local payload = event.payload or {}
  local result_ref = text(payload.result_ref)
  if not is_sha256(result_ref) then
    error(
      "classify-formalization-outcome: result_ref must be sha256")
  end

  local paths = research.paths(repository_root())
  local cursor = paths.work ..
    "/formalization-decisions/" .. result_ref:sub(8) .. ".json"
  local result = research.run(paths, {
    "classify-result",
    "--root", paths.store,
    "--result-ref", result_ref,
    "--cursor", cursor,
  }, paths.selection_cli)

  if result.schema ~= "paper-formalization-outcome-classified.v1" then
    error(
      "classify-formalization-outcome: selection CLI returned the wrong schema")
  end
  if result.result_ref ~= result_ref then
    error(
      "classify-formalization-outcome: classifier changed result identity")
  end

  local route = research.required(result.route, "route")
  local queue = route_queues[route]
  if not queue then
    error(
      "classify-formalization-outcome: unsupported route " .. route)
  end

  local decision_ref =
    research.required(result.decision_ref, "decision_ref")
  raise(queue, {
    decision_ref = decision_ref,
    certification_wait_ref = text(result.certification_wait_ref),
    result_ref = result_ref,
    dispatch_ref = research.required(result.dispatch_ref, "dispatch_ref"),
    formalization_request_ref = research.required(
      result.formalization_request_ref,
      "formalization_request_ref"),
    selection_ref = research.required(
      result.selection_ref,
      "selection_ref"),
    paper_research_input_ref = research.required(
      result.paper_research_input_ref,
      "paper_research_input_ref"),
    intuition_proposal_ref = research.required(
      result.intuition_proposal_ref,
      "intuition_proposal_ref"),
    candidate_paper_ref = research.required(
      result.candidate_paper_ref,
      "candidate_paper_ref"),
    literature_research_ref = research.required(
      result.literature_research_ref,
      "literature_research_ref"),
    verification_budget_ref = research.required(
      result.verification_budget_ref,
      "verification_budget_ref"),
    route = route,
    outcome_class = research.required(
      result.outcome_class,
      "outcome_class"),
    claim_status = research.required(
      result.claim_status,
      "claim_status"),
    replayed = result.replayed == true,
    dedup_key = "paper-formalization-decision:v1:" .. decision_ref,
  })
end

return M
