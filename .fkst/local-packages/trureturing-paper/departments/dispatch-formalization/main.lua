local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "formalization_request_ready" },
  produces = {
    "paper_frontier_formalize_transport_ready",
    "trureturing-formalize.solve_request",
  },
  stall_window = "5m",
}

local function is_sha256(value)
  return type(value) == "string" and
    #value == 71 and
    value:sub(1, 7) == "sha256:" and
    value:sub(8):match("^[0-9a-f]+$") ~= nil
end

local function same_if_present(payload, name, actual)
  local claimed = payload[name]
  if claimed ~= nil and claimed ~= "" and claimed ~= actual then
    error("dispatch-formalization: event " .. name .. " disagrees with canonical dispatch")
  end
end

function pipeline(event)
  local payload = event.payload or {}
  local request_path = research.required(
    payload.request_path,
    "request_path")
  local selection_path = research.required(
    payload.selection_path,
    "selection_path")
  local request_ref = research.required(
    payload.formalization_request_ref,
    "formalization_request_ref")
  local selection_ref = research.required(
    payload.selection_ref,
    "selection_ref")
  if not is_sha256(request_ref) or not is_sha256(selection_ref) then
    error("dispatch-formalization: request and selection references must be sha256 values")
  end

  local root, root_error = research.repo_root(
    request_path,
    "/artifacts/research-selections/")
  if not root then
    error("dispatch-formalization: " .. tostring(root_error))
  end
  local selection_root, selection_error = research.repo_root(
    selection_path,
    "/artifacts/research-selections/")
  if not selection_root or selection_root ~= root then
    error(
      "dispatch-formalization: selection and request are outside one Paper repository: " ..
      tostring(selection_error))
  end

  local paths = research.paths(root)
  local cursor = paths.work .. "/formalization-dispatch/" ..
    request_ref:sub(8) .. ".json"
  local result = research.run(paths, {
    "prepare-dispatch",
    "--selection", selection_path,
    "--request", request_path,
    "--root", paths.store,
    "--selection-ref", selection_ref,
    "--request-ref", request_ref,
    "--cursor", cursor,
  }, paths.selection_cli)

  if result.schema ~= "paper-formalization-dispatch-ready.v1" then
    error("dispatch-formalization: selection CLI returned the wrong schema")
  end
  if result.formalization_request_ref ~= request_ref or
      result.selection_ref ~= selection_ref then
    error("dispatch-formalization: canonical dispatch changed the event identity")
  end

  same_if_present(payload, "truth_release_digest", result.truth_release_digest)
  same_if_present(payload, "source_commit", result.source_commit)
  same_if_present(payload, "source_tree", result.source_tree)

  local dispatch_ref = research.required(result.dispatch_ref, "dispatch_ref")
  local progress = research.run(paths, {
    "record-transport",
    "--repository-root", paths.root,
    "--formalization-request-ref", request_ref,
    "--dispatch-ref", dispatch_ref,
  }, paths.frontier_lifecycle_cli)
  if type(progress) ~= "table" or
      progress.schema ~= "paper-frontier-formalize-transport-recorded.v1" or
      progress.formalization_request_ref ~= request_ref or
      progress.dispatch_ref ~= dispatch_ref or
      (progress.status ~= "recorded" and
       progress.status ~= "not-frontier-bound") then
    error("dispatch-formalization: frontier transport recorder returned an invalid result")
  end

  if progress.status == "recorded" then
    if not is_sha256(progress.frontier_ref) or
        not is_sha256(progress.node_id) or
        type(progress.claim_id) ~= "string" or
        progress.claim_id == "" or
        not is_sha256(progress.frontier_state_ref) or
        not is_sha256(progress.transport_event_ref) then
      error("dispatch-formalization: frontier transport identity is invalid")
    end
    raise("paper_frontier_formalize_transport_ready", {
      schema = "paper-frontier-formalize-transport-ready.v1",
      formalization_request_ref = request_ref,
      dispatch_ref = dispatch_ref,
      selection_ref = selection_ref,
      frontier_ref = progress.frontier_ref,
      node_id = progress.node_id,
      claim_id = progress.claim_id,
      frontier_state_ref = progress.frontier_state_ref,
      transport_event_ref = progress.transport_event_ref,
      replayed = progress.replayed == true,
      dedup_key = "paper-frontier-formalize-transport:v1:" ..
        progress.frontier_ref .. ":" .. progress.node_id,
    })
  end

  raise("trureturing-formalize.solve_request", {
    request_path = research.required(result.request_path, "request_path"),
    formalization_request_ref = request_ref,
    selection_ref = selection_ref,
    source_repo = research.required(result.source_repo, "source_repo"),
    source_commit = research.required(result.source_commit, "source_commit"),
    source_tree = research.required(result.source_tree, "source_tree"),
    truth_release_digest = research.required(
      result.truth_release_digest,
      "truth_release_digest"),
    gid = research.required(result.gid, "gid"),
    dispatch_ref = dispatch_ref,
    frontier_ref = progress.frontier_ref or "",
    frontier_node_id = progress.node_id or "",
    dedup_key = "paper-formalize-solve-request:v1:" .. request_ref,
  })
end

return M
