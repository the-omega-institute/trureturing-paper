-- core.lua — pure lifecycle logic for the trureturing-paper host package.
--
-- No host-authority side effects here. The departments are thin repository-local
-- glue. The C# assembler is compiled by CI/preflight; runtime Lua invokes the
-- prebuilt local DLL and never restores or compiles.
local M = {}

local SNAP_REL = "Papers/frozen%-bundle/source%-snapshot%.v1%.json"

function M.paths(snap_abs)
  if type(snap_abs) ~= "string" or snap_abs == "" then
    return nil, "empty snapshot path"
  end
  local repo_root = snap_abs:gsub(SNAP_REL .. "$", "")
  if repo_root == snap_abs then
    return nil, "snapshot path is not Papers/frozen-bundle/source-snapshot.v1.json"
  end
  return {
    repo_root = repo_root,
    snap = snap_abs,
    bundle = repo_root .. "Papers/frozen-bundle",
    recipe = repo_root .. "Papers/recipe.v1.json",
    tex = repo_root .. "Papers/paper.tex",
    pubs = repo_root .. "Papers/publications.jsonl",
    cli_dll = repo_root
      .. "src/Trureturing.Paper.Cli/bin/Release/net10.0/Trureturing.Paper.Cli.dll",
  }
end

function M.is_digest(d)
  return type(d) == "string" and #d == 64 and d:match("^[0-9a-f]+$") ~= nil
end

function M.blessed_digest(snap)
  if type(snap) ~= "table" then return nil end
  return snap.truth_graph_sha256
end

function M.ledger_has_digest(ledger_text, digest)
  if type(ledger_text) ~= "string" or ledger_text == "" then return false end
  for line in ledger_text:gmatch("[^\n]+") do
    local ok, rec = pcall(json.decode, line)
    if ok and type(rec) == "table" and rec.snapshot_digest == digest then
      return true
    end
  end
  return false
end

function M.needs_publish(blessed_dig, ledger_text)
  if not M.is_digest(blessed_dig) then
    return false
  end
  return not M.ledger_has_digest(ledger_text, blessed_dig)
end

function M.receipt_line(digest, out_rel, ts_unix)
  return string.format(
    '{"snapshot_digest":%q,"out":%q,"recorded_at_unix":%d}\n',
    tostring(digest), tostring(out_rel), math.floor(ts_unix or 0))
end

return M
