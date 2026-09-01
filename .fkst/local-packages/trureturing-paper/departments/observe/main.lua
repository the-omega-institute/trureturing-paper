-- observe — detect a newly blessed frozen bundle and, if this graph-digest pair
-- has not been published, trigger an assembly. Dispatch is folded in (one
-- deterministic assembly per blessed snapshot needs no separate dispatch lane).
local M = {}
local core = require("core")

M.spec = {
  consumes = { "paper_snapshot_seen" },
  produces = { "paper_reproject" },
  stall_window = "5m",
}

function pipeline(event)
  local snap_path = event.payload and event.payload.path
  local pth, perr = core.paths(snap_path)
  if not pth then
    error("observe: " .. tostring(perr))
  end
  -- The blessed snapshot is authoritative: a decode failure or invalid digest must
  -- fail closed (propagate), never be treated as "nothing to do".
  local blessed = core.blessed_digest(
    json.decode(file.read(pth.snap)), file.read(pth.document_digest))
  if not core.is_blessing(blessed) then
    error("observe: bundle has no valid truth/document graph digest pair")
  end
  local ledger = ""
  if file.exists(pth.pubs) then ledger = file.read(pth.pubs) end
  -- Republish if the digest is unrecorded OR the output was deleted/lost: the ledger is
  -- publication history, but the current artifact must also exist. act's record stays idempotent
  -- by digest, so re-materializing a deleted paper.tex does not append a second receipt.
  if not core.needs_publish(blessed, ledger) and file.exists(pth.tex) then
    log.info("observe: graph pair already published and materialized")
    return
  end
  raise("paper_reproject", {
    truth_graph_sha256 = blessed.truth_graph_sha256,
    document_graph_sha256 = blessed.document_graph_sha256,
    snapshot_path = pth.snap,
  })
end

return M
