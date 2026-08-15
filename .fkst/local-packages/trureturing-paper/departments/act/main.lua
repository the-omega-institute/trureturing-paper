-- act — assemble the paper for a blessed snapshot and record the publication in
-- one department, atomically (record folded into act, as in pages-publish, so a
-- superseding blessing cannot cause a downstream lane to lose a receipt). The
-- chain is observe -> act; act is terminal.
--
-- Correctness under at-least-once delivery:
--  * Obsolete trigger: if the bundle blessing has moved past this event, drop it
--    (ack) — the current blessing has its own trigger; retrying cannot help.
--  * Real failure (assemble nonzero — including the claim gate rejecting a
--    fabricated/unfrozen GID): raise, so the child exits nonzero for reliable
--    retry / DLQ, never a silent ack.
--  * Stale output: the bundle digest is re-checked after assembly, before
--    recording, so a bundle change during the (slow) assemble cannot record a
--    receipt for a paper that no longer matches the trigger.
--  * Idempotent receipt: the ledger append is dedup'd by digest under a lock.
local M = {}
local core = require("core")

M.spec = {
  consumes = { "paper_reproject" },
  stall_window = "15m",
}

local function current_blessed(pth)
  return core.blessed_digest(json.decode(file.read(pth.snap)))
end

function pipeline(event)
  local p = event.payload or {}
  local digest = p.snapshot_digest
  local pth, perr = core.paths(p.snapshot_path)
  if not pth then
    error("act: " .. tostring(perr))
  end
  if not core.is_digest(digest) then
    error("act: trigger digest is not a valid sha256: " .. tostring(digest))
  end
  if current_blessed(pth) ~= digest then
    log.info("act: bundle blessing now other than " .. digest .. "; obsolete, dropping")
    return
  end
  -- exec_argv: no shell, no quoting. The claim gate runs inside assemble, so a
  -- fabricated or unfrozen GID makes assemble exit nonzero and fail loud here.
  local res = exec_argv({
    argv = {
      "dotnet", "run", "--project", pth.cli_project, "--",
      "assemble", "--recipe", pth.recipe, "--frozen-bundle", pth.bundle, "--output", pth.tex,
    },
    cwd = pth.repo_root,
    timeout = 600,
  })
  if res.exit_code ~= 0 then
    error("act: assemble exit=" .. tostring(res.exit_code) .. " stderr=" .. tostring(res.stderr))
  end
  if not file.exists(pth.tex) then
    error("act: assemble reported success but produced no tex")
  end
  if #file.read(pth.tex) == 0 then
    error("act: assembled tex is empty")
  end
  if current_blessed(pth) ~= digest then
    log.info("act: bundle moved during assemble; not recording stale receipt for " .. digest)
    return
  end
  with_lock("trureturing-paper/publications", function()
    local prior = ""
    if file.exists(pth.pubs) then prior = file.read(pth.pubs) end
    if core.ledger_has_digest(prior, digest) then
      log.info("act: receipt for " .. digest .. " already present; publication is idempotent")
      return
    end
    file.write(pth.pubs, prior .. core.receipt_line(digest, "Papers/paper.tex", now()))
  end)
  log.info("act: assembled + recorded " .. digest)
end

return M
