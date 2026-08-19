local core = require("core")
local t = fkst.test

local A = string.rep("a", 64)
local B = string.rep("b", 64)

return {
  test_paths_derives_host_facts = function()
    local p = core.paths("/repo/Papers/frozen-bundle/source-snapshot.v1.json")
    t.eq(p.repo_root, "/repo/")
    t.eq(p.snap, "/repo/Papers/frozen-bundle/source-snapshot.v1.json")
    t.eq(p.bundle, "/repo/Papers/frozen-bundle")
    t.eq(p.recipe, "/repo/Papers/recipe.v1.json")
    t.eq(p.tex, "/repo/Papers/paper.tex")
    t.eq(p.pubs, "/repo/Papers/publications.jsonl")
    t.eq(
      p.cli_dll,
      "/repo/src/Trureturing.Paper.Cli/bin/Release/net10.0/Trureturing.Paper.Cli.dll")
  end,
  test_paths_rejects_non_bundle = function()
    local p, err = core.paths("/repo/other/file.json")
    t.is_nil(p); t.is_true(type(err) == "string")
  end,
  test_paths_rejects_empty = function()
    t.is_nil(core.paths(""))
  end,

  test_is_digest = function()
    t.is_true(core.is_digest(A))
    t.eq(core.is_digest("7d17"), false)
    t.eq(core.is_digest(string.rep("A", 64)), false)
    t.eq(core.is_digest(string.rep("g", 64)), false)
    t.eq(core.is_digest(nil), false)
  end,

  test_blessed_digest = function()
    t.eq(core.blessed_digest({ truth_graph_sha256 = A }), A)
    t.is_nil(core.blessed_digest(nil))
    t.is_nil(core.blessed_digest({}))
  end,

  test_ledger_empty = function()
    t.eq(core.ledger_has_digest("", A), false)
    t.eq(core.ledger_has_digest(nil, A), false)
  end,
  test_ledger_present = function()
    t.is_true(core.ledger_has_digest(core.receipt_line(A, "o", 1), A))
  end,
  test_ledger_absent = function()
    t.eq(core.ledger_has_digest(core.receipt_line(A, "o", 1), B), false)
  end,
  test_ledger_multiline = function()
    local text = core.receipt_line(A, "o", 1) .. core.receipt_line(B, "o", 2)
    t.is_true(core.ledger_has_digest(text, B))
  end,
  test_ledger_skips_malformed = function()
    t.is_true(core.ledger_has_digest("{bad\n" .. core.receipt_line(A, "o", 1), A))
  end,

  test_needs_publish_true_when_absent = function()
    t.is_true(core.needs_publish(A, ""))
  end,
  test_needs_publish_false_when_present = function()
    t.eq(core.needs_publish(A, core.receipt_line(A, "o", 1)), false)
  end,
  test_needs_publish_false_when_invalid_digest = function()
    t.eq(core.needs_publish("abc", ""), false)
    t.eq(core.needs_publish(nil, ""), false)
  end,

  test_receipt_line_is_json = function()
    local decoded = json.decode(core.receipt_line(A, "Papers/paper.tex", 1786803322))
    t.eq(decoded.snapshot_digest, A)
    t.eq(decoded.out, "Papers/paper.tex")
    t.eq(decoded.recorded_at_unix, 1786803322)
  end,
}
