local M = {}
local research = require("research_core")

M.spec = {
  consumes = { "paper_research_join_requested" },
  produces = { "paper_research_input_ready" },
  stall_window = "5m",
}

function pipeline(event)
  local root = event.payload and event.payload.repo_root or nil
  if type(root) ~= "string" or root == "" then
    error("join-research-input: missing repo_root")
  end
  local paths = research.paths(root)
  research.ensure_dir(paths.work)
  local result = research.run(paths, {
    "join",
    "--root", paths.store,
    "--topology-cursor", paths.work .. "/topology-cursor.v1.json",
    "--intuition-cursor", paths.work .. "/intuition-cursor.v1.json",
    "--cursor", paths.work .. "/research-join-cursor.v1.json",
  })
  if result.status == "waiting" then
    log.info("paper research input is waiting for the matching release peer")
    return
  end
  if result.status ~= "ready" then
    error("join-research-input: unexpected status " .. tostring(result.status))
  end
  raise("paper_research_input_ready", {
    research_input_ref = result.research_input_ref,
    truth_release_digest = result.truth_release_digest,
    topology_digest = result.topology_digest,
    replayed = result.replayed,
  })
end

return M
