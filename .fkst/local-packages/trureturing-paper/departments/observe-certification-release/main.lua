local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_certification_release_seen" },
  produces = {
    "paper_certification_release_registered",
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
      "observe-certification-release: " ..
      "TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

local function observed_path(event, root)
  local path = text(event.payload and event.payload.path)
  path = path:gsub("\\", "/")
  local prefix = root .. "inbox/certification-releases/"
  if path:sub(1, #prefix) ~= prefix then
    error(
      "observe-certification-release: observed path is outside the deployment inbox")
  end
  local name = path:sub(#prefix + 1)
  if name == "" or name:find("/", 1, true) or
      not name:match("^[A-Za-z0-9._-]+%.json$") then
    error(
      "observe-certification-release: observed filename is not canonical")
  end
  return path, name
end

function pipeline(event)
  local root = repository_root()
  local release_path, observed_name = observed_path(event, root)
  local paths = research.paths(root)
  local wait_dir = paths.work .. "/certification-waits"
  local release_dir = paths.work .. "/certification-releases"
  research.ensure_dir(wait_dir)
  research.ensure_dir(release_dir)

  local result = research.run(paths, {
    "observe-release",
    "--root", paths.store,
    "--release", release_path,
    "--cursor", release_dir .. "/" .. observed_name,
    "--wait-cursor-directory", wait_dir,
  }, paths.certification_cli)

  if result.schema ~= "paper-certification-release-registered.v1" then
    error(
      "observe-certification-release: certification CLI returned the wrong schema")
  end
  local release_ref = research.required(result.release_ref, "release_ref")
  if not is_sha256(release_ref) then
    error(
      "observe-certification-release: release_ref must be sha256")
  end

  raise("paper_certification_release_registered", {
    release_ref = release_ref,
    release_digest = research.required(
      result.release_digest,
      "release_digest"),
    replayed = result.replayed == true,
    dedup_key = "paper-certification-release:v1:" .. release_ref,
  })

  local waits = result.certification_wait_refs or {}
  for _, wait_ref in ipairs(waits) do
    if not is_sha256(wait_ref) then
      error(
        "observe-certification-release: wait list contains a malformed reference")
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
