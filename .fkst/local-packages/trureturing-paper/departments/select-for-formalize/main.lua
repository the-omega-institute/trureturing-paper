local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_selection_authorized" },
  produces = { "formalization_request_ready" },
  stall_window = "5m",
}

function pipeline(event)
  local observed = event.payload and event.payload.path or nil
  local root, err = research.repo_root(
    observed,
    "/inbox/research-selections/")
  if not root then
    error("select-for-formalize: " .. tostring(err))
  end
  local authorization = research.read_envelope(
    observed,
    "paper-selection-authorization.v1")
  if authorization.approval ~= "submit-once" then
    error("select-for-formalize: explicit submit-once approval is required")
  end
  research.required(authorization.approved_by, "approved_by")
  research.required(authorization.approved_at, "approved_at")
  local authorization_id = research.required(
    authorization.authorization_id,
    "authorization_id")
  if #authorization_id ~= 71 or
      authorization_id:sub(1, 7) ~= "sha256:" or
      not authorization_id:sub(8):match("^[0-9a-f]+$") then
    error("select-for-formalize: authorization_id is not a sha256 reference")
  end

  local paths = research.paths(root)
  local output = root .. "artifacts/research-selections/" ..
    authorization_id:sub(8)
  research.ensure_dir(output)
  local selection_path = output .. "/paper-research-selection.v1.json"
  local request_path = output .. "/formalization-request.v1.json"
  local result = research.run(paths, {
    "select",
    "--content", research.required(
      authorization.selection_content_path,
      "selection_content_path"),
    "--selection-out", selection_path,
    "--request-out", request_path,
  }, paths.selection_cli)
  if result.schema ~= "paper-formalization-handoff.v1" then
    error("select-for-formalize: selection CLI returned the wrong schema")
  end

  raise("formalization_request_ready", {
    authorization_id = authorization_id,
    approved_by = authorization.approved_by,
    approved_at = authorization.approved_at,
    selection_ref = result.selection_ref,
    formalization_request_ref = result.formalization_request_ref,
    selection_path = result.selection_path,
    request_path = result.formalization_request_path,
    dedup_key = "paper-formalization-request:v1:" ..
      result.formalization_request_ref,
  })
end

return M
