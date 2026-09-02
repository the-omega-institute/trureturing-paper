local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_certification_evaluation_requested" },
  produces = {
    "paper_candidate_still_pending_certification",
    "paper_certification_mismatch",
    "paper_certified_claim_ready",
    "paper_frontier_certified_claim_manifest_ready",
    "paper_frontier_ready_set_ready",
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

local function validate_ready_nodes(nodes)
  if type(nodes) ~= "table" then
    error("evaluate-certification-release: ready_nodes must be an array")
  end
  for index, node in ipairs(nodes) do
    if type(node) ~= "table" or
        node.dispatch_order ~= index or
        not is_sha256(node.node_id) or
        type(node.claim_id) ~= "string" or
        node.claim_id == "" or
        type(node.formalization_kind) ~= "string" or
        node.formalization_kind == "" or
        type(node.parallel_wave) ~= "number" or
        node.parallel_wave < 1 or
        type(node.priority) ~= "number" or
        node.priority < 0 or
        node.priority > 100 or
        node.next_route ~= "governed-selection" then
      error("evaluate-certification-release: frontier ready node is invalid")
    end
  end
end

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

  local evaluation_ref = research.required(
    result.evaluation_ref,
    "evaluation_ref")
  local certified_claim_ref = text(result.certified_claim_ref)
  local progress = nil
  if outcome == "certified" then
    if not is_sha256(certified_claim_ref) then
      error(
        "evaluate-certification-release: certified outcome lacks a certified claim")
    end
    progress = research.run(paths, {
      "record-certification",
      "--repository-root", paths.root,
      "--evaluation-ref", evaluation_ref,
      "--certified-claim-ref", certified_claim_ref,
    }, paths.frontier_lifecycle_cli)
    if type(progress) ~= "table" or
        progress.schema ~= "paper-frontier-certification-recorded.v1" or
        progress.evaluation_ref ~= evaluation_ref or
        progress.certified_claim_ref ~= certified_claim_ref or
        (progress.status ~= "recorded" and
         progress.status ~= "not-frontier-bound") then
      error("evaluate-certification-release: frontier certification recorder returned an invalid result")
    end

    if progress.status == "recorded" then
      if not is_sha256(progress.frontier_ref) or
          not is_sha256(progress.node_id) or
          type(progress.claim_id) ~= "string" or
          progress.claim_id == "" or
          not is_sha256(progress.certified_manifest_ref) or
          not is_sha256(progress.ready_set_ref) or
          not is_sha256(progress.frontier_state_ref) then
        error("evaluate-certification-release: frontier certification identity is invalid")
      end
      validate_ready_nodes(progress.ready_nodes)
      raise("paper_frontier_certified_claim_manifest_ready", {
        schema = "paper-frontier-certification-ready.v1",
        formalization_request_ref = progress.formalization_request_ref,
        evaluation_ref = evaluation_ref,
        certified_claim_ref = certified_claim_ref,
        frontier_ref = progress.frontier_ref,
        node_id = progress.node_id,
        claim_id = progress.claim_id,
        certified_manifest_ref = progress.certified_manifest_ref,
        frontier_state_ref = progress.frontier_state_ref,
        replayed = progress.replayed == true,
        dedup_key = "paper-frontier-certified-manifest:v1:" ..
          progress.frontier_ref .. ":" .. progress.node_id,
      })
      if #progress.ready_nodes > 0 then
        raise("paper_frontier_ready_set_ready", {
          schema = "paper-frontier-ready-set-ready.v1",
          frontier_ref = progress.frontier_ref,
          trigger_node_id = progress.node_id,
          trigger_manifest_ref = progress.certified_manifest_ref,
          ready_set_ref = progress.ready_set_ref,
          frontier_state_ref = progress.frontier_state_ref,
          ready_nodes = progress.ready_nodes,
          replayed = progress.replayed == true,
          dedup_key = "paper-frontier-ready-set:v1:" ..
            progress.ready_set_ref,
        })
      end
    end
  end

  raise(queue, {
    evaluation_ref = evaluation_ref,
    certification_wait_ref = wait_ref,
    release_ref = release_ref,
    reason = research.required(result.reason, "reason"),
    claim_status = research.required(
      result.claim_status,
      "claim_status"),
    certified_claim_ref = certified_claim_ref,
    mismatch_ref = text(result.mismatch_ref),
    frontier_ref = progress and progress.frontier_ref or "",
    frontier_node_id = progress and progress.node_id or "",
    frontier_state_ref = progress and progress.frontier_state_ref or "",
    replayed = result.replayed == true,
    dedup_key = "paper-certification-evaluation:v1:" .. evaluation_ref,
  })
end

return M
