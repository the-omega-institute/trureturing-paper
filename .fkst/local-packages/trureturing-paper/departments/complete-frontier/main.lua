local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_frontier_certified_claim_manifest_ready" },
  produces = {
    "paper_frontier_completion_pending",
    "paper_frontier_completion_ready",
    "paper_manuscript_plan_registered",
    "paper_manuscript_claim_evaluation_requested",
  },
  stall_window = "5m",
}

local function require_digest(value, name)
  if not agent.is_sha256(value) then
    error("complete-frontier: " .. name .. " must be sha256")
  end
  return value
end

local function require_digest_array(values, name)
  if type(values) ~= "table" then
    error("complete-frontier: " .. name .. " must be an array")
  end
  for _, value in ipairs(values) do
    require_digest(value, name)
  end
  return values
end

function pipeline(event)
  local payload = event.payload or {}
  local frontier_ref = require_digest(payload.frontier_ref, "frontier_ref")
  require_digest(payload.node_id, "node_id")
  require_digest(payload.certified_manifest_ref, "certified_manifest_ref")
  require_digest(payload.frontier_state_ref, "frontier_state_ref")

  local paths = research.paths(agent.repository_root())
  local evaluated = nil
  with_lock(
    "paper-frontier-completion:v1:" .. frontier_ref,
    function()
      evaluated = research.run(paths, {
        "evaluate-frontier-completion",
        "--repository-root", paths.root,
        "--frontier-ref", frontier_ref,
      }, paths.frontier_selection_cli)
    end)

  if type(evaluated) ~= "table"
      or evaluated.schema ~= "paper-frontier-completion-evaluated.v1"
      or evaluated.frontier_ref ~= frontier_ref
      or not agent.is_sha256(evaluated.frontier_state_ref)
      or type(evaluated.reason) ~= "string"
      or evaluated.reason == "" then
    error("complete-frontier: completion CLI returned an invalid result")
  end

  if evaluated.status == "pending" then
    require_digest(evaluated.pending_ref, "pending_ref")
    require_digest_array(evaluated.missing_node_ids, "missing_node_ids")
    raise("paper_frontier_completion_pending", {
      schema = "paper-frontier-completion-pending-ready.v1",
      frontier_ref = frontier_ref,
      frontier_state_ref = evaluated.frontier_state_ref,
      pending_ref = evaluated.pending_ref,
      missing_node_ids = evaluated.missing_node_ids,
      reason = evaluated.reason,
      replayed = evaluated.replayed == true,
      dedup_key = "paper-frontier-completion-pending:v1:" ..
        evaluated.pending_ref,
    })
    return
  end

  if evaluated.status ~= "completed"
      or not agent.is_sha256(evaluated.completion_ref)
      or not agent.is_sha256(evaluated.manuscript_plan_ref)
      or not agent.is_sha256(evaluated.manuscript_truth_release_ref)
      or not agent.is_sha256(evaluated.manuscript_truth_release_digest)
      or type(evaluated.formal_claim_count) ~= "number"
      or evaluated.formal_claim_count < 1
      or type(evaluated.informal_item_count) ~= "number"
      or evaluated.informal_item_count < 0
      or type(evaluated.missing_node_ids) ~= "table"
      or #evaluated.missing_node_ids ~= 0 then
    error("complete-frontier: completed frontier result is invalid")
  end

  raise("paper_frontier_completion_ready", {
    schema = "paper-frontier-completion-ready.v1",
    frontier_ref = frontier_ref,
    frontier_state_ref = evaluated.frontier_state_ref,
    completion_ref = evaluated.completion_ref,
    manuscript_plan_ref = evaluated.manuscript_plan_ref,
    manuscript_truth_release_ref = evaluated.manuscript_truth_release_ref,
    manuscript_truth_release_digest = evaluated.manuscript_truth_release_digest,
    formal_claim_count = evaluated.formal_claim_count,
    informal_item_count = evaluated.informal_item_count,
    replayed = evaluated.replayed == true,
    dedup_key = "paper-frontier-completion-ready:v1:" ..
      evaluated.completion_ref,
  })

  raise("paper_manuscript_plan_registered", {
    manuscript_plan_ref = evaluated.manuscript_plan_ref,
    manuscript_truth_release_ref = evaluated.manuscript_truth_release_ref,
    frontier_ref = frontier_ref,
    completion_ref = evaluated.completion_ref,
    replayed = evaluated.replayed == true,
    dedup_key = "paper-manuscript-plan:v1:" ..
      evaluated.manuscript_plan_ref,
  })

  raise("paper_manuscript_claim_evaluation_requested", {
    manuscript_plan_ref = evaluated.manuscript_plan_ref,
    trigger_ref = evaluated.completion_ref,
    trigger_kind = "frontier-completion",
    frontier_ref = frontier_ref,
    dedup_key = "paper-manuscript-evaluate:v1:" ..
      evaluated.manuscript_plan_ref .. ":" .. evaluated.completion_ref,
  })
end

return M
