local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = {
    "paper_formalization_frontier_ready",
    "paper_frontier_node_selection_requested",
  },
  stall_window = "5m",
}

local function require_artifact(value, expected_schema, name)
  if type(value) ~= "table"
      or value.schema ~= expected_schema
      or not agent.is_sha256(value.artifact_ref)
      or type(value.content_path) ~= "string"
      or not agent.is_sha256(value.envelope_ref)
      or type(value.envelope_path) ~= "string" then
    error("admit-frontier-planning-agent: invalid " .. name)
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "frontier-planning" then
    return
  end
  if payload.status ~= "completed"
      or payload.agent_role ~= "paper-formalization-frontier-planner"
      or payload.context_mode ~= "promotion-bound-planning"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref)
      or type(payload.paper_id) ~= "string"
      or payload.paper_id == "" then
    error("admit-frontier-planning-agent: completed event identity is invalid")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local admitted = research.run(paths, {
    "admit-frontier-planning-result",
    "--repository-root", paths.root,
    "--task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-frontier-planning-agent-result-admitted.v1"
      or admitted.task_ref ~= payload.task_ref
      or admitted.result_ref ~= payload.result_ref
      or admitted.paper_id ~= payload.paper_id
      or admitted.theory_program_ref ~= payload.theory_program_ref
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.portfolio_task_ref)
      or not agent.is_sha256(admitted.portfolio_result_ref)
      or not agent.is_sha256(admitted.portfolio_ref)
      or not agent.is_sha256(admitted.judgment_evidence_ref)
      or not agent.is_sha256(admitted.updated_portfolio_ref)
      or not agent.is_sha256(admitted.theorem_package_ref)
      or not agent.is_sha256(admitted.theory_audit_ref)
      or not agent.is_sha256(admitted.scorecard_ref)
      or not agent.is_sha256(admitted.portfolio_decision_ref)
      or type(admitted.cycle_number) ~= "number"
      or admitted.cycle_number < 1
      or type(admitted.initial_node_routes) ~= "table"
      or #admitted.initial_node_routes < 1 then
    error("admit-frontier-planning-agent: Agent CLI returned an invalid admission")
  end
  require_artifact(
    admitted.frontier,
    "paper-formalization-frontier.v1",
    "formalization frontier")
  require_artifact(
    admitted.initial_state,
    "paper-formalization-frontier-state.v1",
    "initial frontier state")

  for index, route in ipairs(admitted.initial_node_routes) do
    if type(route) ~= "table"
        or route.dispatch_order ~= index
        or not agent.is_sha256(route.node_id)
        or type(route.claim_id) ~= "string"
        or route.claim_id == ""
        or type(route.formalization_kind) ~= "string"
        or route.formalization_kind == ""
        or route.parallel_wave ~= 0
        or type(route.priority) ~= "number"
        or route.priority < 0
        or route.priority > 100
        or route.next_route ~= "governed-selection" then
      error("admit-frontier-planning-agent: invalid initial node route")
    end
    raise("paper_frontier_node_selection_requested", {
      schema = "paper-frontier-node-selection-requested.v1",
      task_ref = admitted.task_ref,
      result_ref = admitted.result_ref,
      dispatch_ref = admitted.dispatch_ref,
      portfolio_task_ref = admitted.portfolio_task_ref,
      portfolio_result_ref = admitted.portfolio_result_ref,
      portfolio_ref = admitted.portfolio_ref,
      cycle_number = admitted.cycle_number,
      judgment_evidence_ref = admitted.judgment_evidence_ref,
      updated_portfolio_ref = admitted.updated_portfolio_ref,
      paper_id = admitted.paper_id,
      theory_program_ref = admitted.theory_program_ref,
      theorem_package_ref = admitted.theorem_package_ref,
      theory_audit_ref = admitted.theory_audit_ref,
      scorecard_ref = admitted.scorecard_ref,
      portfolio_decision_ref = admitted.portfolio_decision_ref,
      frontier = admitted.frontier,
      initial_state = admitted.initial_state,
      dispatch_order = route.dispatch_order,
      node_id = route.node_id,
      claim_id = route.claim_id,
      formalization_kind = route.formalization_kind,
      parallel_wave = route.parallel_wave,
      priority = route.priority,
      next_route = route.next_route,
      dedup_key = "paper-frontier-node-selection:v1:" .. admitted.frontier.artifact_ref .. ":" .. route.node_id,
    })
  end

  raise("paper_formalization_frontier_ready", {
    schema = "paper-formalization-frontier-ready.v1",
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    portfolio_task_ref = admitted.portfolio_task_ref,
    portfolio_result_ref = admitted.portfolio_result_ref,
    portfolio_ref = admitted.portfolio_ref,
    cycle_number = admitted.cycle_number,
    judgment_evidence_ref = admitted.judgment_evidence_ref,
    updated_portfolio_ref = admitted.updated_portfolio_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    theorem_package_ref = admitted.theorem_package_ref,
    theory_audit_ref = admitted.theory_audit_ref,
    scorecard_ref = admitted.scorecard_ref,
    portfolio_decision_ref = admitted.portfolio_decision_ref,
    frontier = admitted.frontier,
    initial_state = admitted.initial_state,
    initial_node_routes = admitted.initial_node_routes,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-formalization-frontier-ready:v1:" .. admitted.frontier.artifact_ref,
  })
end

return M
