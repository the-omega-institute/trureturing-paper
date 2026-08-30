local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_certified_claim_ready" },
  produces = { "paper_manuscript_claim_evaluation_requested" },
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
      "refresh-manuscript-plans: " ..
      "TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

function pipeline(event)
  local payload = event.payload or {}
  local certified_claim_ref = text(payload.certified_claim_ref)
  if not is_sha256(certified_claim_ref) then
    error(
      "refresh-manuscript-plans: certified_claim_ref must be sha256")
  end

  local paths = research.paths(repository_root())
  local plan_dir = paths.work .. "/manuscript-plans"
  research.ensure_dir(plan_dir)
  local result = research.run(paths, {
    "list-plans",
    "--cursor-directory", plan_dir,
  }, paths.claim_manifest_cli)

  if result.schema ~= "paper-manuscript-plans-listed.v1" then
    error(
      "refresh-manuscript-plans: claim-manifest CLI returned the wrong schema")
  end

  for _, plan_ref in ipairs(result.manuscript_plan_refs or {}) do
    if not is_sha256(plan_ref) then
      error(
        "refresh-manuscript-plans: plan list contains a malformed reference")
    end
    raise("paper_manuscript_claim_evaluation_requested", {
      manuscript_plan_ref = plan_ref,
      trigger_ref = certified_claim_ref,
      trigger_kind = "certified-claim-ready",
      dedup_key = "paper-manuscript-evaluate:v1:" ..
        plan_ref .. ":" .. certified_claim_ref,
    })
  end
end

return M
