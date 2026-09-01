local core = require("core")
local t = fkst.test

local A = string.rep("a", 64) -- a valid 64-hex digest
local B = string.rep("b", 64)
local C = string.rep("c", 64)
local KEY_A = { truth_graph_sha256 = A, document_graph_sha256 = B }
local KEY_B = { truth_graph_sha256 = A, document_graph_sha256 = C }

return {
  test_paths_derives_host_facts = function()
    local p = core.paths("/repo/Papers/frozen-bundle/source-snapshot.v1.json")
    t.eq(p.repo_root, "/repo/")
    t.eq(p.snap, "/repo/Papers/frozen-bundle/source-snapshot.v1.json")
    t.eq(p.document_digest, "/repo/Papers/frozen-bundle/document-graph.v1.sha256")
    t.eq(p.bundle, "/repo/Papers/frozen-bundle")
    t.eq(p.recipe, "/repo/Papers/recipe.v1.json")
    t.eq(p.tex, "/repo/Papers/paper.tex")
    t.eq(p.pubs, "/repo/Papers/publications.jsonl")
    t.eq(p.cli_project, "/repo/src/Trureturing.Paper.Cli")
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
    local key = core.blessed_digest({ truth_graph_sha256 = A }, B .. "\n")
    t.eq(key.truth_graph_sha256, A)
    t.eq(key.document_graph_sha256, B)
    t.is_nil(core.blessed_digest(nil))
    t.is_nil(core.blessed_digest({}))
  end,

  test_ledger_empty = function()
    t.eq(core.ledger_has_digest("", KEY_A), false)
    t.eq(core.ledger_has_digest(nil, KEY_A), false)
  end,
  test_ledger_present = function()
    t.is_true(core.ledger_has_digest(core.receipt_line(KEY_A, "o", 1), KEY_A))
  end,
  test_ledger_absent = function()
    t.eq(core.ledger_has_digest(core.receipt_line(KEY_A, "o", 1), KEY_B), false)
  end,
  test_ledger_multiline = function()
    local text = core.receipt_line(KEY_A, "o", 1) .. core.receipt_line(KEY_B, "o", 2)
    t.is_true(core.ledger_has_digest(text, KEY_B))
  end,
  test_ledger_skips_malformed = function()
    t.is_true(core.ledger_has_digest("{bad\n" .. core.receipt_line(KEY_A, "o", 1), KEY_A))
  end,

  test_needs_publish_true_when_absent = function()
    t.is_true(core.needs_publish(KEY_A, ""))
  end,
  test_needs_publish_false_when_present = function()
    t.eq(core.needs_publish(KEY_A, core.receipt_line(KEY_A, "o", 1)), false)
  end,
  test_document_graph_only_change_needs_publish = function()
    t.is_true(core.needs_publish(KEY_B, core.receipt_line(KEY_A, "o", 1)))
  end,
  test_needs_publish_false_when_invalid_digest = function()
    t.eq(core.needs_publish("abc", ""), false)
    t.eq(core.needs_publish(nil, ""), false)
  end,

  test_receipt_line_is_json = function()
    local decoded = json.decode(core.receipt_line(KEY_A, "Papers/paper.tex", 1786803322))
    t.eq(decoded.truth_graph_sha256, A)
    t.eq(decoded.document_graph_sha256, B)
    t.eq(decoded.out, "Papers/paper.tex")
    t.eq(decoded.recorded_at_unix, 1786803322)
  end,
}
