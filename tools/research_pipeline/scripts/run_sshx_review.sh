#!/bin/sh
set -eu
round=${1:?round is required}
case "$round" in 1|2|3) ;; *) echo 'round must be 1, 2, or 3' >&2; exit 64;; esac
mkdir -p Papers/research/reviews
adapter=${FKST_SSHX_REVIEW_COMMAND:-}
if [ -z "$adapter" ]; then
  if [ -x ./scripts/sshx_review.sh ]; then adapter=./scripts/sshx_review.sh
  elif [ -x ./tools/sshx_review.sh ]; then adapter=./tools/sshx_review.sh
  elif command -v sshx-review >/dev/null 2>&1; then adapter=sshx-review
  else
    echo 'SSHX review adapter is not configured. Set FKST_SSHX_REVIEW_COMMAND or provide a repository adapter.' >&2
    exit 78
  fi
fi
FKST_REVIEW_ROUND="$round" \
FKST_REVIEW_CONFIG="tools/research_pipeline/config/reviewers/round-$round.conf" \
FKST_MANUSCRIPT_PDF=".fkst-pipeline/artifacts/manuscript.pdf" \
FKST_REVIEW_REPORT_OUTPUT="Papers/research/reviews/round-$round.md" \
FKST_REVIEW_OUTPUT="Papers/research/reviews/round-$round.tsv" \
  sh -lc "$adapter"
test -s "Papers/research/reviews/round-$round.md"
test -s "Papers/research/reviews/round-$round.tsv"
