-- observe — detect a newly blessed frozen bundle and, if this truth-graph digest
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
  local blessed_dig = core.blessed_digest(json.decode(file.read(pth.snap)))
  if not core.is_digest(blessed_dig) then
    error("observe: blessed snapshot has no valid truth_graph_sha256")
  end
  local ledger = ""
  if file.exists(pth.pubs) then ledger = file.read(pth.pubs) end
  -- Republish if the digest is unrecorded OR the output was deleted/lost: the ledger is
  -- publication history, but the current artifact must also exist. act's record stays idempotent
  -- by digest, so re-materializing a deleted paper.tex does not append a second receipt.
  if not core.needs_publish(blessed_dig, ledger) and file.exists(pth.tex) then
    log.info("observe: truth_graph " .. blessed_dig .. " already published and materialized")
    return
  end
  raise("paper_reproject", {
    snapshot_digest = blessed_dig,
    snapshot_path = pth.snap,
  })
end

return M
