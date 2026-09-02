local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_frontier_ready_set_ready" },
  produces = {
    "paper_frontier_ready_wave_selection_ready",
    "paper_frontier_node_selection_ready",
    "formalization_request_ready",
  },
  stall_window = "5m",
}

local function text(value)
  if type(value) == "string" then return value end
  return ""
end

local function require_artifact(value, expected_schema, name)
  if type(value) ~= "table"
      or value.schema ~= expected_schema
      or not agent.is_sha256(value.artifact_ref)
      or not agent.is_sha256(value.blob_ref)
      or type(value.repository_relative_path) ~= "string"
      or value.repository_relative_path == "" then
    error("select-frontier-ready-wave: invalid " .. name)
  end
end

local function validate_ready_nodes(nodes)
  if type(nodes) ~= "table" or #nodes < 1 then
    error("select-frontier-ready-wave: ready_nodes must be non-empty")
  end
  local seen = {}
  for index, node in ipairs(nodes) do
    if type(node) ~= "table"
        or node.dispatch_order ~= index
        or not agent.is_sha256(node.node_id)
        or type(node.claim_id) ~= "string"
        or node.claim_id == ""
        or type(node.formalization_kind) ~= "string"
        or node.formalization_kind == ""
        or type(node.parallel_wave) ~= "number"
        or node.parallel_wave < 1
        or type(node.priority) ~= "number"
        or node.priority < 0
        or node.priority > 100
        or node.next_route ~= "governed-selection"
        or seen[node.node_id] then
      error("select-frontier-ready-wave: invalid ready node")
    end
    seen[node.node_id] = true
  end
end

local function validate_admission(admitted, ready)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-frontier-node-selection-admitted.v1"
      or admitted.dispatch_order ~= ready.dispatch_order
      or admitted.node_id ~= ready.node_id
      or admitted.claim_id ~= ready.claim_id
      or admitted.formalization_kind ~= ready.formalization_kind
      or admitted.parallel_wave ~= ready.parallel_wave
      or admitted.priority ~= ready.priority
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
    error("select-frontier-ready-wave: node admission changed ready-set identity")
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
end

local function raise_node_events(admitted)
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
    dedup_key = "paper-frontier-node-selection-ready:v1:" ..
      admitted.frontier_ref .. ":" .. admitted.node_id,
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
    dedup_key = "paper-formalization-request:v1:" ..
      admitted.formalization_request_ref,
  })
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.schema ~= "paper-frontier-ready-set-ready.v1"
      or not agent.is_sha256(payload.frontier_ref)
      or not agent.is_sha256(payload.trigger_node_id)
      or not agent.is_sha256(payload.trigger_manifest_ref)
      or not agent.is_sha256(payload.ready_set_ref)
      or not agent.is_sha256(payload.frontier_state_ref) then
    error("select-frontier-ready-wave: invalid ready-set event")
  end
  validate_ready_nodes(payload.ready_nodes)

  local paths = research.paths(agent.repository_root())
  local admitted = nil
  with_lock(
    "paper-frontier-state:v1:" .. payload.frontier_ref,
    function()
      admitted = research.run(paths, {
        "admit-frontier-ready-wave",
        "--repository-root", paths.root,
        "--frontier-ref", payload.frontier_ref,
        "--ready-set-ref", payload.ready_set_ref,
      }, paths.frontier_selection_cli)
    end)

  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-frontier-ready-wave-selection-admitted.v1"
      or admitted.ready_set_ref ~= payload.ready_set_ref
      or admitted.frontier_ref ~= payload.frontier_ref
      or admitted.trigger_node_id ~= payload.trigger_node_id
      or admitted.trigger_manifest_ref ~= payload.trigger_manifest_ref
      or admitted.release_state_ref ~= payload.frontier_state_ref
      or not agent.is_sha256(admitted.frontier_planning_task_ref)
      or type(admitted.paper_id) ~= "string"
      or admitted.paper_id == ""
      or not agent.is_sha256(admitted.theory_program_ref)
      or not agent.is_sha256(admitted.theorem_package_ref)
      or type(admitted.node_admissions) ~= "table"
      or #admitted.node_admissions ~= #payload.ready_nodes
      or type(admitted.admitted_at) ~= "string"
      or admitted.admitted_at == "" then
    error("select-frontier-ready-wave: CLI changed ready-set identity")
  end

  local summaries = {}
  for index, node_admission in ipairs(admitted.node_admissions) do
    local ready = payload.ready_nodes[index]
    validate_admission(node_admission, ready)
    if node_admission.frontier_ref ~= payload.frontier_ref
        or node_admission.frontier_planning_task_ref ~=
          admitted.frontier_planning_task_ref
        or node_admission.paper_id ~= admitted.paper_id
        or node_admission.theory_program_ref ~= admitted.theory_program_ref
        or node_admission.theorem_package_ref ~= admitted.theorem_package_ref then
      error("select-frontier-ready-wave: node admission changed batch lineage")
    end
    summaries[index] = {
      dispatch_order = node_admission.dispatch_order,
      node_id = node_admission.node_id,
      claim_id = node_admission.claim_id,
      formalization_kind = node_admission.formalization_kind,
      parallel_wave = node_admission.parallel_wave,
      priority = node_admission.priority,
      authorization_ref = node_admission.authorization.artifact_ref,
      verification_budget_ref =
        node_admission.verification_budget.artifact_ref,
      selection_ref = node_admission.selection_ref,
      formalization_request_ref =
        node_admission.formalization_request_ref,
      binding_ref = node_admission.binding.artifact_ref,
      frontier_state_ref = node_admission.frontier_state.artifact_ref,
      gid = node_admission.gid,
    }
  end

  raise("paper_frontier_ready_wave_selection_ready", {
    schema = "paper-frontier-ready-wave-selection-ready.v1",
    ready_set_ref = admitted.ready_set_ref,
    frontier_ref = admitted.frontier_ref,
    trigger_node_id = admitted.trigger_node_id,
    trigger_manifest_ref = admitted.trigger_manifest_ref,
    release_state_ref = admitted.release_state_ref,
    frontier_planning_task_ref = admitted.frontier_planning_task_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    theorem_package_ref = admitted.theorem_package_ref,
    node_admissions = summaries,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-frontier-ready-wave-selection:v1:" ..
      admitted.ready_set_ref,
  })

  for _, node_admission in ipairs(admitted.node_admissions) do
    node_admission.ready_set_ref = admitted.ready_set_ref
    raise_node_events(node_admission)
  end
end

return M
