local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_manuscript_plan_seen" },
  produces = {
    "paper_manuscript_plan_registered",
    "paper_manuscript_claim_evaluation_requested",
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
      "register-manuscript-plan: " ..
      "TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

local function observed_path(event, root)
  local path = text(event.payload and event.payload.path)
  path = path:gsub("\\", "/")
  local prefix = root .. "inbox/manuscript-plans/"
  if path:sub(1, #prefix) ~= prefix then
    error(
      "register-manuscript-plan: observed path is outside the deployment inbox")
  end
  local name = path:sub(#prefix + 1)
  if name == "" or name:find("/", 1, true) or
      not name:match("^[A-Za-z0-9._-]+%.json$") then
    error(
      "register-manuscript-plan: observed filename is not canonical")
  end
  return path, name
end

function pipeline(event)
  local root = repository_root()
  local plan_path, observed_name = observed_path(event, root)
  local paths = research.paths(root)
  local plan_dir = paths.work .. "/manuscript-plans"
  research.ensure_dir(plan_dir)

  local result = research.run(paths, {
    "register-plan",
    "--root", paths.store,
    "--plan", plan_path,
    "--cursor", plan_dir .. "/" .. observed_name,
  }, paths.claim_manifest_cli)

  if result.schema ~= "paper-manuscript-plan-registered.v1" then
    error(
      "register-manuscript-plan: claim-manifest CLI returned the wrong schema")
  end
  local plan_ref = research.required(
    result.manuscript_plan_ref,
    "manuscript_plan_ref")
  if not is_sha256(plan_ref) then
    error(
      "register-manuscript-plan: manuscript_plan_ref must be sha256")
  end

  raise("paper_manuscript_plan_registered", {
    manuscript_plan_ref = plan_ref,
    paper_id = research.required(result.paper_id, "paper_id"),
    manuscript_truth_release_ref = research.required(
      result.manuscript_truth_release_ref,
      "manuscript_truth_release_ref"),
    replayed = result.replayed == true,
    dedup_key = "paper-manuscript-plan:v1:" .. plan_ref,
  })

  raise("paper_manuscript_claim_evaluation_requested", {
    manuscript_plan_ref = plan_ref,
    trigger_ref = plan_ref,
    trigger_kind = "plan-registered",
    dedup_key = "paper-manuscript-evaluate:v1:" .. plan_ref .. ":" .. plan_ref,
  })
end

return M
