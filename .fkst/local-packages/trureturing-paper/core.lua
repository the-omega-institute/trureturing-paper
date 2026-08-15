-- core.lua — pure logic for the trureturing-paper host package.
--
-- No host-authority side effects here (no file.write, exec, raise, log); pure so it
-- is unit-tested directly in tests/core_test.lua. The departments are thin glue.
-- json.decode is a pure parser available in every engine Lua context.
local M = {}

-- The file_watch raiser reports the absolute path of the pinned frozen bundle's
-- blessed snapshot (the CLI-named source-snapshot.v1.json). Every other input and
-- output is a fixed host filesystem fact under the same paper repo root (§6:
-- durable truth is an explicit host filesystem file, never <RT>/marks or cache).
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
    cli_project = repo_root .. "src/Trureturing.Paper.Cli",
  }
end

-- A blessed digest must be a 64-char lowercase-hex SHA-256.
function M.is_digest(d)
  return type(d) == "string" and #d == 64 and d:match("^[0-9a-f]+$") ~= nil
end

-- Dedup key = the blessed snapshot's truth-graph digest (a source-derived host
-- fact). The snapshot file is the CLI's source-snapshot.v1.json; its
-- truth_graph_sha256 identifies which frozen truth this paper is built from.
function M.blessed_digest(snap)
  if type(snap) ~= "table" then return nil end
  return snap.truth_graph_sha256
end

-- Whether the append-only publications ledger already records a receipt for this
-- digest. This is both the observe dedup and the act idempotency check on the
-- durable host fact, so a replay does not re-assemble or re-record. A malformed
-- ledger line is skipped rather than allowed to crash the scan.
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

-- Assemble iff the blessed digest is valid and the publications ledger does not
-- already record it. (The paper's statement faithfulness and non-fabrication are
-- enforced by the assembler's claim gate, not here.)
function M.needs_publish(blessed_dig, ledger_text)
  if not M.is_digest(blessed_dig) then
    return false
  end
  return not M.ledger_has_digest(ledger_text, blessed_dig)
end

-- One JSONL receipt line for the append-only publications ledger (an explicit
-- host filesystem fact; file.write does not itself create a git commit). json has
-- no encode (§2), so the line is built from controlled scalar fields: a validated
-- 64-hex digest, a fixed repo-relative output path, integer Unix seconds (now()).
function M.receipt_line(digest, out_rel, ts_unix)
  return string.format(
    '{"snapshot_digest":%q,"out":%q,"recorded_at_unix":%d}\n',
    tostring(digest), tostring(out_rel), math.floor(ts_unix or 0))
end

return M
