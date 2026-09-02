local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_completed" },
  produces = {
    "paper_theory_audit_opinion_ready",
    "paper_theory_audit_waiting",
    "paper_theory_audit_ready",
    "paper_candidate_scorecard_ready",
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
    error("admit-theory-audit-agent: invalid " .. name)
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if payload.phase ~= "theory-audit" then
    return
  end
  if payload.status ~= "completed"
      or payload.agent_role ~= "paper-theory-independent-referee"
      or payload.context_mode ~= "fresh-theory-review"
      or not agent.is_sha256(payload.task_ref)
      or not agent.is_sha256(payload.result_ref)
      or not agent.is_sha256(payload.theory_program_ref) then
    error("admit-theory-audit-agent: completed reviewer event identity is invalid")
  end

  local root = agent.repository_root()
  local paths = research.paths(root)
  local admitted = research.run(paths, {
    "admit-audit-opinion",
    "--repository-root", paths.root,
    "--task-ref", payload.task_ref,
  }, paths.agent_cli)
  if type(admitted) ~= "table"
      or admitted.schema ~= "paper-theory-audit-agent-result-admitted.v1"
      or admitted.task_ref ~= payload.task_ref
      or admitted.result_ref ~= payload.result_ref
      or admitted.paper_id ~= payload.paper_id
      or admitted.theory_program_ref ~= payload.theory_program_ref
      or not agent.is_sha256(admitted.dispatch_ref)
      or not agent.is_sha256(admitted.plan_ref)
      or not agent.is_sha256(admitted.audit_request_ref)
      or type(admitted.reviewer_slot) ~= "number"
      or type(admitted.reviewer_role) ~= "string"
      or (admitted.aggregate_status ~= "waiting" and admitted.aggregate_status ~= "ready") then
    error("admit-theory-audit-agent: Agent CLI returned an invalid opinion admission")
  end
  require_artifact(admitted.opinion, "paper-theory-audit-opinion.v1", "opinion")

  raise("paper_theory_audit_opinion_ready", {
    schema = "paper-theory-audit-opinion-ready.v1",
    task_ref = admitted.task_ref,
    result_ref = admitted.result_ref,
    dispatch_ref = admitted.dispatch_ref,
    review_plan_ref = admitted.plan_ref,
    audit_request_ref = admitted.audit_request_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    reviewer_slot = admitted.reviewer_slot,
    reviewer_role = admitted.reviewer_role,
    opinion = admitted.opinion,
    run_id = admitted.run_id,
    provenance = admitted.provenance,
    admitted_at = admitted.admitted_at,
    replayed = admitted.replayed == true,
    dedup_key = "paper-a3-opinion-ready:v1:" .. admitted.opinion.artifact_ref,
  })

  if admitted.aggregate_status == "waiting" then
    if type(admitted.missing_task_refs) ~= "table" or #admitted.missing_task_refs < 1 then
      error("admit-theory-audit-agent: waiting aggregate has no missing reviewer tasks")
    end
    raise("paper_theory_audit_waiting", {
      schema = "paper-theory-audit-waiting.v1",
      review_plan_ref = admitted.plan_ref,
      audit_request_ref = admitted.audit_request_ref,
      paper_id = admitted.paper_id,
      theory_program_ref = admitted.theory_program_ref,
      admitted_opinion_ref = admitted.opinion.artifact_ref,
      missing_task_refs = admitted.missing_task_refs,
      next_route = "theory-audit",
      dedup_key = "paper-a3-waiting:v1:" .. admitted.plan_ref .. ":" .. admitted.opinion.artifact_ref,
    })
    return
  end

  require_artifact(admitted.audit, "paper-theory-audit.v1", "aggregate audit")
  require_artifact(admitted.scorecard, "paper-candidate-scorecard.v1", "candidate scorecard")
  if type(admitted.verdict) ~= "string"
      or type(admitted.passed) ~= "boolean"
      or type(admitted.promotion_eligible) ~= "boolean"
      or type(admitted.next_route) ~= "string" then
    error("admit-theory-audit-agent: ready aggregate metadata is invalid")
  end

  raise("paper_candidate_scorecard_ready", {
    schema = "paper-candidate-scorecard-ready.v1",
    review_plan_ref = admitted.plan_ref,
    audit_request_ref = admitted.audit_request_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    audit_ref = admitted.audit.artifact_ref,
    scorecard = admitted.scorecard,
    verdict = admitted.verdict,
    promotion_eligible = admitted.promotion_eligible,
    next_route = admitted.next_route,
    dedup_key = "paper-candidate-scorecard-ready:v1:" .. admitted.scorecard.artifact_ref,
  })

  raise("paper_theory_audit_ready", {
    schema = "paper-theory-audit-ready.v1",
    review_plan_ref = admitted.plan_ref,
    audit_request_ref = admitted.audit_request_ref,
    paper_id = admitted.paper_id,
    theory_program_ref = admitted.theory_program_ref,
    audit = admitted.audit,
    scorecard = admitted.scorecard,
    verdict = admitted.verdict,
    passed = admitted.passed,
    promotion_eligible = admitted.promotion_eligible,
    next_route = admitted.next_route,
    replayed = admitted.replayed == true,
    dedup_key = "paper-theory-audit-ready:v1:" .. admitted.audit.artifact_ref,
  })
end

return M
