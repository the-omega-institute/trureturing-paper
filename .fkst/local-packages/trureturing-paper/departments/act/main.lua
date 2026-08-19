-- act — assemble the paper for a blessed snapshot and record the publication.
--
-- FKST supplies generic delivery, process execution, files, and locks. This Lua
-- belongs to trureturing-paper and only invokes the prebuilt local C# assembler.
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
  if not file.exists(pth.cli_dll) then
    error("act: prebuilt local assembler is missing: " .. pth.cli_dll)
  end

  local res = exec_argv({
    argv = {
      "dotnet", pth.cli_dll,
      "assemble",
      "--recipe", pth.recipe,
      "--frozen-bundle", pth.bundle,
      "--output", pth.tex,
    },
    cwd = pth.repo_root,
    timeout = 600,
  })
  if res.exit_code ~= 0 then
    error("act: assemble exit=" .. tostring(res.exit_code)
      .. " stderr=" .. tostring(res.stderr))
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
