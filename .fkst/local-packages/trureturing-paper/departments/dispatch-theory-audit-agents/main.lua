local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_theory_audit_requested" },
  produces = {
    "paper_theory_audit_reviewers_staged",
    "paper_agent_task_requested",
  },
  stall_window = "5m",
}

local function require_stored_artifact(value, name)
  if type(value) ~= "table"
      or type(value.schema) ~= "string"
      or not agent.is_sha256(value.artifact_ref)
      or type(value.content_path) ~= "string"
      or not agent.is_sha256(value.envelope_ref)
      or type(value.envelope_path) ~= "string" then
    error("dispatch-theory-audit-agents: invalid " .. name)
  end
end

function pipeline(event)
  local payload = event.payload or {}
  if type(payload.dispatch_path) ~= "string" or payload.dispatch_path == "" then
    error("dispatch-theory-audit-agents: dispatch_path is required")
  end
  local root = agent.repository_root()
  local paths = research.paths(root)
  local staged = research.run(paths, {
    "stage-audit-tasks",
    "--repository-root", paths.root,
    "--dispatch", payload.dispatch_path,
  }, paths.agent_cli)
  if type(staged) ~= "table"
      or staged.schema ~= "paper-theory-audit-agent-tasks-staged.v1"
      or not agent.is_sha256(staged.dispatch_ref)
      or not agent.is_sha256(staged.theory_program_ref)
      or not agent.is_sha256(staged.audit_request_ref)
      or not agent.is_sha256(staged.theorem_package_ref)
      or type(staged.paper_id) ~= "string"
      or type(staged.reviewers) ~= "table"
      or #staged.reviewers < 2 then
    error("dispatch-theory-audit-agents: Agent CLI returned an invalid staged review plan")
  end
  require_stored_artifact(staged.review_plan, "review plan")
  if staged.review_plan.schema ~= "paper-theory-audit-review-plan.v1" then
    error("dispatch-theory-audit-agents: review plan schema is invalid")
  end

  for _, reviewer in ipairs(staged.reviewers) do
    if type(reviewer) ~= "table"
        or type(reviewer.slot) ~= "number"
        or type(reviewer.reviewer_role) ~= "string"
        or not agent.is_sha256(reviewer.task_ref)
        or type(reviewer.task_path) ~= "string" then
      error("dispatch-theory-audit-agents: invalid planned reviewer")
    end
    local task_path = paths.root .. "/" .. reviewer.task_path
    local registration = research.run(paths, {
      "register-task",
      "--repository-root", paths.root,
      "--task", task_path,
    }, paths.agent_cli)
    if type(registration) ~= "table"
        or registration.schema ~= "paper-agent-task-registered.v1"
        or registration.task_ref ~= reviewer.task_ref
        or registration.paper_id ~= staged.paper_id
        or registration.theory_program_ref ~= staged.theory_program_ref
        or registration.phase ~= "theory-audit"
        or registration.agent_role ~= "paper-theory-independent-referee"
        or registration.context_mode ~= "fresh-theory-review" then
      error("dispatch-theory-audit-agents: reviewer task registration is invalid")
    end
    raise("paper_agent_task_requested", {
      schema = "paper-agent-task-requested.v1",
      task_ref = registration.task_ref,
      paper_id = registration.paper_id,
      theory_program_ref = registration.theory_program_ref,
      phase = registration.phase,
      agent_role = registration.agent_role,
      context_mode = registration.context_mode,
      audit_request_ref = staged.audit_request_ref,
      review_plan_ref = staged.review_plan.artifact_ref,
      reviewer_slot = reviewer.slot,
      reviewer_role = reviewer.reviewer_role,
      dedup_key = "paper-a3-reviewer-task:v1:" .. registration.task_ref,
    })
  end

  raise("paper_theory_audit_reviewers_staged", {
    schema = "paper-theory-audit-reviewers-staged.v1",
    dispatch_ref = staged.dispatch_ref,
    review_plan = staged.review_plan,
    paper_id = staged.paper_id,
    theory_program_ref = staged.theory_program_ref,
    audit_request_ref = staged.audit_request_ref,
    theorem_package_ref = staged.theorem_package_ref,
    reviewers = staged.reviewers,
    replayed = staged.replayed == true,
    dedup_key = "paper-a3-review-plan:v1:" .. staged.review_plan.artifact_ref,
  })
end

return M
