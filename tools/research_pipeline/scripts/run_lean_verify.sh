#!/bin/sh
set -eu
artifact_root=${FKST_PIPELINE_ARTIFACT_ROOT:-.fkst-pipeline/artifacts}
mkdir -p "$artifact_root"
receipt=$artifact_root/lean-verify.json
log=$artifact_root/lean-build.log
adapter=${FKST_LEAN_VERIFY_COMMAND:-}
if [ -n "$adapter" ]; then
  FKST_LEAN_VERIFY_RECEIPT="$receipt" FKST_LEAN_VERIFY_LOG="$log" sh -lc "$adapter"
  test -s "$receipt"
  exit 0
fi
formalization_root=${FKST_FORMALIZATION_ROOT:-}
if [ -n "$formalization_root" ] && { [ -f "$formalization_root/lakefile.lean" ] || [ -f "$formalization_root/lakefile.toml" ]; } \
   && command -v lake >/dev/null 2>&1; then
  if (cd "$formalization_root" && lake build) >"$log" 2>&1; then
    printf '{"status":"pass","command":"lake build","formalization_root":"%s"}\n' "$formalization_root" > "$receipt"
    exit 0
  fi
  printf '{"status":"fail","command":"lake build","log":"%s"}\n' "$log" > "$receipt"
  exit 1
fi
printf '%s\n' '{"status":"blocked","reason":"Configure FKST_LEAN_VERIFY_COMMAND or FKST_FORMALIZATION_ROOT"}' > "$receipt"
exit 78
