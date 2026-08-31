local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = {
    "paper_portfolio_judgment_ready",
    "paper_formalization_frontier_requested",
    "paper_theory_deepening_requested",
    "paper_candidate_split_requested",
    "paper_candidate_merge_requested",
    "paper_candidate_parked",
    "paper_candidate_archived",
    "paper_candidate_held",
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
    error("admit-portfolio-judgment-agent: invalid " .. name)
  end
end

local function route_queue(next_route)
  local routes = {
    ["frontier-planning"] = "paper_formalization_frontier_requested",
    ["portfolio-judgment"] = "paper_candidate_held",
    ["theory-deepening"] = "paper_theory_deepening_requested",
    ["portfolio-split"] = "paper_candidate_split_requested",
    ["portfolio-merge"] = "paper_candidate_merge_requested",
    ["parked"] = "paper_candidate_parked",
    ["archived"] = "paper_candidate_archived",
  }
  return routes[next_route]
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "portfolio-judgment" then
    return
  end
  if payload.status ~= "completed"
      or payload.agent_role ~= "paper-portfolio-judge"
      or payload.context_mode ~= "cross-paper-comparison"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref) then
    error("admit-portfolio-judgment-agent: completed event identity is invalid")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local admitted = research.run(paths, {
    "admit-portfolio-judgment-result",
    "--repository-root", paths.root,
    "--task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-portfolio-judgment-agent-result-admitted.v1"
      or admitted.task_ref ~= payload.task_ref
      or admitted.result_ref ~= payload.result_ref
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.portfolio_ref)
      or not agent.is_sha256(admitted.candidate_batch_ref)
      or type(admitted.cycle_number) ~= "number"
      or admitted.cycle_number < 1
      or type(admitted.routes) ~= "table"
      or #admitted.routes < 2 then
    error("admit-portfolio-judgment-agent: Agent CLI returned an invalid admission")
  end
  if payload.theory_program_ref ~= admitted.portfolio_ref then
    error("admit-portfolio-judgment-agent: admitted portfolio changed task identity")
  end
  require_artifact(admitted.evidence, "paper-portfolio-judgment-evidence.v1", "judgment evidence")
  require_artifact(admitted.decision, "paper-portfolio-decision.v1", "portfolio decision")
  require_artifact(admitted.updated_portfolio, "paper-research-portfolio.v1", "updated portfolio")

  for _, route in ipairs(admitted.routes) do
    if type(route) ~= "table"
        or type(route.rank) ~= "number"
        or type(route.paper_id) ~= "string"
        or not agent.is_sha256(route.theory_program_ref)
        or not agent.is_sha256(route.scorecard_ref)
        or type(route.action) ~= "string"
        or type(route.next_route) ~= "string"
        or type(route.reason) ~= "string" then
      error("admit-portfolio-judgment-agent: invalid per-paper route")
    end
    local queue = route_queue(route.next_route)
    if queue == nil then
      error("admit-portfolio-judgment-agent: unsupported per-paper route")
    end
    raise(queue, {
      schema = "paper-portfolio-route-ready.v1",
      task_ref = admitted.task_ref,
      result_ref = admitted.result_ref,
      dispatch_ref = admitted.dispatch_ref,
      portfolio_ref = admitted.portfolio_ref,
      candidate_batch_ref = admitted.candidate_batch_ref,
      cycle_number = admitted.cycle_number,
      decision_ref = admitted.decision.artifact_ref,
      updated_portfolio_ref = admitted.updated_portfolio.artifact_ref,
      judgment_evidence_ref = admitted.evidence.artifact_ref,
      rank = route.rank,
      paper_id = route.paper_id,
      theory_program_ref = route.theory_program_ref,
      scorecard_ref = route.scorecard_ref,
      action = route.action,
      next_route = route.next_route,
      reason = route.reason,
      dedup_key = "paper-portfolio-route:v1:" .. admitted.decision.artifact_ref .. ":" .. route.paper_id,
    })
  end

  raise("paper_portfolio_judgment_ready", {
    schema = "paper-portfolio-judgment-ready.v1",
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    portfolio_ref = admitted.portfolio_ref,
    candidate_batch_ref = admitted.candidate_batch_ref,
    cycle_number = admitted.cycle_number,
    evidence = admitted.evidence,
    decision = admitted.decision,
    updated_portfolio = admitted.updated_portfolio,
    routes = admitted.routes,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-portfolio-judgment-ready:v1:" .. admitted.decision.artifact_ref,
  })
end

return M
