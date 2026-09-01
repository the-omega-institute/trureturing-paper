local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_frontier_node_selection_requested" },
  produces = {
    "paper_frontier_node_selection_ready",
    "formalization_request_ready",
  },
  stall_window = "5m",
}

local function require_artifact(value, expected_schema, name)
  if type(value) ~= "table"
      or value.schema ~= expected_schema
      or not agent.is_sha256(value.artifact_ref)
      or not agent.is_sha256(value.blob_ref)
      or type(value.repository_relative_path) ~= "string"
      or value.repository_relative_path == "" then
    error("select-frontier-node: invalid " .. name)
  end
end

local function require_source_artifact(value, expected_schema, name)
  if type(value) ~= "table"
      or value.schema ~= expected_schema
      or not agent.is_sha256(value.artifact_ref)
      or type(value.content_path) ~= "string"
      or not agent.is_sha256(value.envelope_ref)
      or type(value.envelope_path) ~= "string" then
    error("select-frontier-node: invalid source " .. name)
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.schema ~= "paper-frontier-node-selection-requested.v1"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.dispatch_ref)
      or not agent.is_sha256(payload.portfolio_task_ref)
      or not agent.is_sha256(payload.portfolio_result_ref)
      or not agent.is_sha256(payload.portfolio_ref)
      or not agent.is_sha256(payload.judgment_evidence_ref)
      or not agent.is_sha256(payload.updated_portfolio_ref)
      or type(payload.paper_id) ~= "string"
      or payload.paper_id == ""
      or not agent.is_sha256(payload.theory_program_ref)
      or not agent.is_sha256(payload.theorem_package_ref)
      or not agent.is_sha256(payload.theory_audit_ref)
      or not agent.is_sha256(payload.scorecard_ref)
      or not agent.is_sha256(payload.portfolio_decision_ref)
      or type(payload.dispatch_order) ~= "number"
      or payload.dispatch_order < 1
      or not agent.is_sha256(payload.node_id)
      or type(payload.claim_id) ~= "string"
      or payload.claim_id == ""
      or type(payload.formalization_kind) ~= "string"
      or payload.formalization_kind == ""
      or payload.parallel_wave ~= 0
      or type(payload.priority) ~= "number"
      or payload.priority < 0
      or payload.priority > 100
      or payload.next_route ~= "governed-selection" then
    error("select-frontier-node: invalid frontier node route")
  end
  require_source_artifact(
    payload.frontier,
    "paper-formalization-frontier.v1",
    "frontier")
  require_source_artifact(
    payload.initial_state,
    "paper-formalization-frontier-state.v1",
    "initial state")

  local root = agent.repository_root()
  local paths = research.paths(root)
  local admitted = nil
  with_lock(
    "paper-frontier-state:v1:" .. payload.frontier.artifact_ref,
    function()
      admitted = research.run(paths, {
        "admit-frontier-node-selection",
        "--repository-root", paths.root,
        "--frontier-task-ref", payload.task_ref,
        "--node-id", payload.node_id,
      }, paths.frontier_selection_cli)
    end)

  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-frontier-node-selection-admitted.v1"
      or admitted.frontier_planning_task_ref ~= payload.task_ref
      or admitted.frontier_planning_result_ref ~= payload.result_ref
      or admitted.frontier_planning_dispatch_ref ~= payload.dispatch_ref
      or admitted.frontier_ref ~= payload.frontier.artifact_ref
      or admitted.initial_state_ref ~= payload.initial_state.artifact_ref
      or admitted.paper_id ~= payload.paper_id
      or admitted.theory_program_ref ~= payload.theory_program_ref
      or admitted.theorem_package_ref ~= payload.theorem_package_ref
      or admitted.portfolio_decision_ref ~= payload.portfolio_decision_ref
      or admitted.dispatch_order ~= payload.dispatch_order
      or admitted.node_id ~= payload.node_id
      or admitted.claim_id ~= payload.claim_id
      or admitted.formalization_kind ~= payload.formalization_kind
      or admitted.parallel_wave ~= payload.parallel_wave
      or admitted.priority ~= payload.priority
      or not agent.is_sha256(admitted.selection_ref)
      or not agent.is_sha256(admitted.selection_blob_ref)
      or type(admitted.selection_path) ~= "string"
      or admitted.selection_path == ""
      or not agent.is_sha256(admitted.formalization_request_ref)
      or not agent.is_sha256(admitted.formalization_request_blob_ref)
      or type(admitted.formalization_request_path) ~= "string"
      or admitted.formalization_request_path == ""
      or not agent.is_sha256(admitted.truth_release_digest)
      or type(admitted.source_commit) ~= "string"
      or #admitted.source_commit ~= 40
      or type(admitted.source_tree) ~= "string"
      or #admitted.source_tree ~= 40
      or type(admitted.gid) ~= "string"
      or admitted.gid == ""
      or type(admitted.admitted_at) ~= "string"
      or admitted.admitted_at == "" then
    error("select-frontier-node: selection CLI changed the frontier route identity")
  end
  require_artifact(
    admitted.authorization,
    "paper-frontier-node-selection-authorization.v1",
    "selection authorization")
  require_artifact(
    admitted.verification_budget,
    "paper-frontier-verification-budget.v1",
    "verification budget")
  require_artifact(
    admitted.selection_event,
    "paper-formalization-frontier-event.v1",
    "selection event")
  require_artifact(
    admitted.request_event,
    "paper-formalization-frontier-event.v1",
    "request event")
  require_artifact(
    admitted.frontier_state,
    "paper-formalization-frontier-state.v1",
    "frontier state")
  require_artifact(
    admitted.binding,
    "paper-frontier-formalization-binding.v1",
    "formalization binding")

  raise("paper_frontier_node_selection_ready", {
    schema = "paper-frontier-node-selection-ready.v1",
    frontier_planning_task_ref = admitted.frontier_planning_task_ref,
    frontier_planning_result_ref = admitted.frontier_planning_result_ref,
    frontier_planning_dispatch_ref = admitted.frontier_planning_dispatch_ref,
    frontier_ref = admitted.frontier_ref,
    initial_state_ref = admitted.initial_state_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    theorem_package_ref = admitted.theorem_package_ref,
    portfolio_decision_ref = admitted.portfolio_decision_ref,
    dispatch_order = admitted.dispatch_order,
    node_id = admitted.node_id,
    claim_id = admitted.claim_id,
    formalization_kind = admitted.formalization_kind,
    parallel_wave = admitted.parallel_wave,
    priority = admitted.priority,
    authorization = admitted.authorization,
    verification_budget = admitted.verification_budget,
    selection_ref = admitted.selection_ref,
    selection_blob_ref = admitted.selection_blob_ref,
    selection_path = admitted.selection_path,
    formalization_request_ref = admitted.formalization_request_ref,
    formalization_request_blob_ref = admitted.formalization_request_blob_ref,
    formalization_request_path = admitted.formalization_request_path,
    selection_event = admitted.selection_event,
    request_event = admitted.request_event,
    frontier_state = admitted.frontier_state,
    binding = admitted.binding,
    truth_release_digest = admitted.truth_release_digest,
    source_commit = admitted.source_commit,
    source_tree = admitted.source_tree,
    gid = admitted.gid,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-frontier-node-selection-ready:v1:" .. admitted.frontier_ref .. ":" .. admitted.node_id,
  })

  raise("formalization_request_ready", {
    authorization_id = admitted.authorization.artifact_ref,
    approved_by = "paper-frontier-governance",
    approved_at = admitted.admitted_at,
    selection_ref = admitted.selection_ref,
    formalization_request_ref = admitted.formalization_request_ref,
    truth_release_digest = admitted.truth_release_digest,
    source_commit = admitted.source_commit,
    source_tree = admitted.source_tree,
    selection_path = admitted.selection_path,
    request_path = admitted.formalization_request_path,
    frontier_ref = admitted.frontier_ref,
    frontier_node_id = admitted.node_id,
    frontier_binding_ref = admitted.binding.artifact_ref,
    dedup_key = "paper-formalization-request:v1:" .. admitted.formalization_request_ref,
  })
end

return M
