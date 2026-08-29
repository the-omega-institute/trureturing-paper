#!/bin/sh
set -eu
artifact_root=${FKST_PIPELINE_ARTIFACT_ROOT:-.fkst-pipeline/artifacts}
mkdir -p "$artifact_root"
adapter=${FKST_LEAN_WRITEBACK_COMMAND:-}
if [ -z "$adapter" ]; then
  printf '%s\n' '{"status":"blocked","reason":"FKST_LEAN_WRITEBACK_COMMAND is not configured"}' \
    > "$artifact_root/lean-writeback.json"
  exit 78
fi
FKST_CANDIDATE_THEOREM=Papers/research/candidate-theorem.md \
FKST_CLAIM_MANIFEST=Papers/research/claim-manifest.tsv \
FKST_LEAN_WRITEBACK_RECEIPT="$artifact_root/lean-writeback.json" \
  sh -lc "$adapter"
test -s "$artifact_root/lean-writeback.json"
