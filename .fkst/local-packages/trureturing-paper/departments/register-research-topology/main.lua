local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_topology_input_seen" },
  produces = { "paper_research_join_requested" },
  stall_window = "5m",
}

function pipeline(event)
  local envelope_path = event.payload and event.payload.path or nil
  local root, err = research.repo_root(envelope_path)
  if not root then error("register-research-topology: " .. tostring(err)) end
  local envelope = research.read_envelope(
    envelope_path,
    "paper-topology-input-envelope.v1")
  local paths = research.paths(root)
  research.ensure_dir(paths.work)
  local result = research.run(paths, {
    "register-topology",
    "--root", paths.store,
    "--publication", research.required(envelope.publication_path, "publication_path"),
    "--topology", research.required(envelope.topology_path, "topology_path"),
    "--cursor", paths.work .. "/topology-cursor.v1.json",
  })
  raise("paper_research_join_requested", {
    repo_root = root,
    source = "topology",
    receipt_ref = result.receipt_ref,
    truth_release_digest = result.truth_release_digest,
    topology_digest = result.topology_digest,
  })
end

return M
