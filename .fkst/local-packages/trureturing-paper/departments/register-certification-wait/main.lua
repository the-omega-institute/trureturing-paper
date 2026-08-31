local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_candidate_pending_certification" },
  produces = {
    "paper_certification_wait_registered",
    "paper_certification_evaluation_requested",
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
      "register-certification-wait: " ..
      "TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

function pipeline(event)
  local payload = event.payload or {}
  local wait_ref = text(payload.certification_wait_ref)
  if not is_sha256(wait_ref) then
    error(
      "register-certification-wait: certification_wait_ref must be sha256")
  end

  local paths = research.paths(repository_root())
  local wait_dir = paths.work .. "/certification-waits"
  local release_dir = paths.work .. "/certification-releases"
  research.ensure_dir(wait_dir)
  research.ensure_dir(release_dir)

  local result = research.run(paths, {
    "register-wait",
    "--root", paths.store,
    "--wait-ref", wait_ref,
    "--cursor", wait_dir .. "/" .. wait_ref:sub(8) .. ".json",
    "--release-cursor-directory", release_dir,
  }, paths.certification_cli)

  if result.schema ~= "paper-certification-wait-registered.v1" then
    error(
      "register-certification-wait: certification CLI returned the wrong schema")
  end
  if result.certification_wait_ref ~= wait_ref then
    error(
      "register-certification-wait: registration changed wait identity")
  end

  raise("paper_certification_wait_registered", {
    certification_wait_ref = wait_ref,
    replayed = result.replayed == true,
    dedup_key = "paper-certification-wait:v1:" .. wait_ref,
  })

  local releases = result.release_refs or {}
  for _, release_ref in ipairs(releases) do
    if not is_sha256(release_ref) then
      error(
        "register-certification-wait: release_refs contains a malformed reference")
    end
    raise("paper_certification_evaluation_requested", {
      certification_wait_ref = wait_ref,
      release_ref = release_ref,
      dedup_key =
        "paper-certification-pair:v1:" .. wait_ref .. ":" .. release_ref,
    })
  end
end

return M
