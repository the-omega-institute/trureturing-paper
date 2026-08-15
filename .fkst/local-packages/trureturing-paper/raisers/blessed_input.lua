-- Watch the pinned frozen bundle's blessed snapshot. A new blessing (a committed
-- change to this file) fires paper_snapshot_seen with the file's absolute path;
-- observe dedups by digest against the publications ledger, so re-firing on an
-- unchanged snapshot is harmless.
return {
  type = "file_watch",
  glob = "Papers/frozen-bundle/source-snapshot.v1.json",
  produces = "paper_snapshot_seen",
}
