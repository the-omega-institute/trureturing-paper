#!/bin/sh
set -eu
round=${1:?round is required}
case "$round" in 1|2|3) ;; *) echo 'round must be 1, 2, or 3' >&2; exit 64;; esac
mkdir -p Papers/research/revisions
adapter=${FKST_REVISION_COMMAND:-}
[ -n "$adapter" ] || { echo 'revision adapter is not configured' >&2; exit 78; }
FKST_REVIEW_ROUND="$round" \
FKST_REVIEW_INPUT="Papers/research/reviews/round-$round.md" \
FKST_REVIEW_SUMMARY="Papers/research/reviews/round-$round.tsv" \
FKST_MANUSCRIPT_TEX="${FKST_MANUSCRIPT_TEX:-Papers/draft/main.tex}" \
FKST_REVISION_OUTPUT="Papers/research/revisions/round-$round.json" \
  sh -lc "$adapter"
test -s "Papers/research/revisions/round-$round.json"
./tools/research_pipeline/scripts/render_manuscript.sh
