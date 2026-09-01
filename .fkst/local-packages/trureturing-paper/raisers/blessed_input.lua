-- Watch bundle inputs so both a document graph and its digest change reach observe.
-- The two-digest key makes unrelated bundle changes harmless re-fires.
return {
  type = "file_watch",
  glob = "Papers/frozen-bundle/*",
  produces = "paper_snapshot_seen",
}
