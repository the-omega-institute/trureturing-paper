local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_certification_evaluation_requested" },
  produces = {
    "paper_candidate_still_pending_certification",
    "paper_certification_mismatch",
    "paper_certified_claim_ready",
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
      "evaluate-certification-release: " ..
      "TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

local outcome_queues = {
  ["still-pending"] = "paper_candidate_still_pending_certification",
  ["mismatch"] = "paper_certification_mismatch",
  ["certified"] = "paper_certified_claim_ready",
}

function pipeline(event)
  local payload = event.payload or {}
  local wait_ref = text(payload.certification_wait_ref)
  local release_ref = text(payload.release_ref)
  if not is_sha256(wait_ref) or not is_sha256(release_ref) then
    error(
      "evaluate-certification-release: wait and release refs must be sha256")
  end

  local paths = research.paths(repository_root())
  local evaluation_dir = paths.work .. "/certification-evaluations"
  local resolution_dir = paths.work .. "/certification-resolutions"
  research.ensure_dir(evaluation_dir)
  research.ensure_dir(resolution_dir)

  local pair_name = wait_ref:sub(8) .. "-" .. release_ref:sub(8)
  local result = research.run(paths, {
    "evaluate-release",
    "--root", paths.store,
    "--wait-ref", wait_ref,
    "--release-ref", release_ref,
    "--cursor", evaluation_dir .. "/" .. pair_name .. ".json",
    "--resolution-cursor", resolution_dir .. "/" ..
      wait_ref:sub(8) .. ".json",
  }, paths.certification_cli)

  if result.schema ~= "paper-certification-release-evaluated.v1" then
    error(
      "evaluate-certification-release: certification CLI returned the wrong schema")
  end
  if result.certification_wait_ref ~= wait_ref or
      result.release_ref ~= release_ref then
    error(
      "evaluate-certification-release: evaluation changed pair identity")
  end

  local outcome = research.required(result.outcome, "outcome")
  local queue = outcome_queues[outcome]
  if not queue then
    error(
      "evaluate-certification-release: unsupported outcome " .. outcome)
  end

  raise(queue, {
    evaluation_ref = research.required(
      result.evaluation_ref,
      "evaluation_ref"),
    certification_wait_ref = wait_ref,
    release_ref = release_ref,
    reason = research.required(result.reason, "reason"),
    claim_status = research.required(
      result.claim_status,
      "claim_status"),
    certified_claim_ref = text(result.certified_claim_ref),
    mismatch_ref = text(result.mismatch_ref),
    replayed = result.replayed == true,
    dedup_key = "paper-certification-evaluation:v1:" ..
      research.required(result.evaluation_ref, "evaluation_ref"),
  })
end

return M
