#!/bin/sh
set -eu
artifact_root=${FKST_PIPELINE_ARTIFACT_ROOT:-.fkst-pipeline/artifacts}
mkdir -p "$artifact_root" Papers/research
adapter=${FKST_INTUITION_COMMAND:-}
if [ -z "$adapter" ]; then
  cat > "$artifact_root/intuition.md" <<'EOT'
# Intuition stage blocked

No project-specific Intuition command is configured. No candidate theorem was
created. Configure `FKST_INTUITION_COMMAND` against the exact-release joined
research input.
EOT
  exit 78
fi
FKST_CANDIDATE_THEOREM_OUTPUT=Papers/research/candidate-theorem.md \
FKST_INTUITION_STATUS_OUTPUT="$artifact_root/intuition.md" \
  sh -lc "$adapter"
test -s Papers/research/candidate-theorem.md
test -s "$artifact_root/intuition.md"
