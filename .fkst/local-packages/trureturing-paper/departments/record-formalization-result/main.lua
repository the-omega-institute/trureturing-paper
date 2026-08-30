local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "trureturing-formalize.solve_result" },
  produces = { "paper_formalization_result_recorded" },
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
      "record-formalization-result: TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

function pipeline(event)
  local payload = event.payload or {}
  local request_ref = text(payload.formalization_request_ref)
  if not is_sha256(request_ref) then
    error(
      "record-formalization-result: formalization_request_ref must be sha256")
  end

  local paths = research.paths(repository_root())
  local suffix = request_ref:sub(8)
  local dispatch_cursor = paths.work ..
    "/formalization-dispatch/" .. suffix .. ".json"
  local result_cursor = paths.work ..
    "/formalization-results/" .. suffix .. ".json"

  local result = research.run(paths, {
    "record-result",
    "--root", paths.store,
    "--dispatch-cursor", dispatch_cursor,
    "--result-cursor", result_cursor,
    "--id", text(payload.id),
    "--formalization-request-ref", request_ref,
    "--observed-request-id", text(payload.observed_request_id),
    "--selection-ref", text(payload.selection_ref),
    "--source-repo", text(payload.source_repo),
    "--source-commit", text(payload.source_commit),
    "--source-tree", text(payload.source_tree),
    "--truth-release-digest", text(payload.truth_release_digest),
    "--paper-id", text(payload.paper_id),
    "--research-candidate-id", text(payload.research_candidate_id),
    "--gid", text(payload.gid),
    "--status", text(payload.status),
    "--rounds", tostring(payload.rounds or ""),
    "--verdict", text(payload.verdict),
    "--error-class", text(payload.error_class),
    "--dedup-key", text(payload.dedup_key),
  }, paths.selection_cli)

  if result.schema ~= "paper-formalization-result-recorded.v1" then
    error("record-formalization-result: selection CLI returned the wrong schema")
  end
  if result.formalization_request_ref ~= request_ref then
    error("record-formalization-result: recorded result changed request identity")
  end

  raise("paper_formalization_result_recorded", {
    result_ref = research.required(result.result_ref, "result_ref"),
    dispatch_ref = research.required(result.dispatch_ref, "dispatch_ref"),
    formalization_request_ref = request_ref,
    selection_ref = research.required(result.selection_ref, "selection_ref"),
    status = research.required(result.status, "status"),
    binding_status = research.required(
      result.binding_status,
      "binding_status"),
    replayed = result.replayed == true,
    dedup_key = "paper-formalization-result-recorded:v1:" ..
      research.required(result.result_ref, "result_ref"),
  })
end

return M
