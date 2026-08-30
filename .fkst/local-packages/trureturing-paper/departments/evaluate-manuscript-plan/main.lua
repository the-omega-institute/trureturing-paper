local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_manuscript_claim_evaluation_requested" },
  produces = {
    "paper_manuscript_claims_pending",
    "paper_manuscript_claims_ineligible",
    "paper_certified_claim_manifest_ready",
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
      "evaluate-manuscript-plan: " ..
      "TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

local outcome_queues = {
  ["pending"] = "paper_manuscript_claims_pending",
  ["ineligible"] = "paper_manuscript_claims_ineligible",
  ["eligible"] = "paper_certified_claim_manifest_ready",
}

function pipeline(event)
  local payload = event.payload or {}
  local plan_ref = text(payload.manuscript_plan_ref)
  if not is_sha256(plan_ref) then
    error(
      "evaluate-manuscript-plan: manuscript_plan_ref must be sha256")
  end

  local paths = research.paths(repository_root())
  local evaluation_dir = paths.work .. "/manuscript-evaluations"
  local resolution_dir = paths.work .. "/manuscript-resolutions"
  research.ensure_dir(evaluation_dir)
  research.ensure_dir(resolution_dir)

  local result = research.run(paths, {
    "evaluate-plan",
    "--root", paths.store,
    "--plan-ref", plan_ref,
    "--evaluation-directory", evaluation_dir,
    "--resolution-cursor", resolution_dir .. "/" ..
      plan_ref:sub(8) .. ".json",
  }, paths.claim_manifest_cli)

  if result.schema ~= "paper-manuscript-claim-evaluated.v1" then
    error(
      "evaluate-manuscript-plan: claim-manifest CLI returned the wrong schema")
  end
  if result.manuscript_plan_ref ~= plan_ref then
    error(
      "evaluate-manuscript-plan: evaluator changed plan identity")
  end

  local outcome = research.required(result.outcome, "outcome")
  local queue = outcome_queues[outcome]
  if not queue then
    error(
      "evaluate-manuscript-plan: unsupported outcome " .. outcome)
  end

  local evaluation_ref = research.required(
    result.evaluation_ref,
    "evaluation_ref")
  if not is_sha256(evaluation_ref) then
    error(
      "evaluate-manuscript-plan: evaluation_ref must be sha256")
  end

  local event_payload = {
    evaluation_ref = evaluation_ref,
    manuscript_plan_ref = plan_ref,
    evidence_state_ref = research.required(
      result.evidence_state_ref,
      "evidence_state_ref"),
    outcome = outcome,
    reason = research.required(result.reason, "reason"),
    claim_manifest_ref = text(result.claim_manifest_ref),
    eligibility_ref = text(result.eligibility_ref),
    pending_ref = text(result.pending_ref),
    ineligibility_ref = text(result.ineligibility_ref),
    replayed = result.replayed == true,
    dedup_key = "paper-manuscript-claim-evaluation:v1:" ..
      evaluation_ref,
  }

  if outcome == "eligible" then
    if not is_sha256(event_payload.claim_manifest_ref) or
        not is_sha256(event_payload.eligibility_ref) then
      error(
        "evaluate-manuscript-plan: eligible result lacks manifest evidence")
    end
  elseif outcome == "pending" then
    if not is_sha256(event_payload.pending_ref) then
      error(
        "evaluate-manuscript-plan: pending result lacks pending evidence")
    end
  elseif not is_sha256(event_payload.ineligibility_ref) then
    error(
      "evaluate-manuscript-plan: ineligible result lacks blocking evidence")
  end

  raise(queue, event_payload)
end

return M
