#!/bin/sh
set -eu
base=tools/research_pipeline
mkdir -p Papers/research/reviews Papers/research/revisions
copy_if_missing() {
  src=$1
  dst=$2
  if [ ! -e "$dst" ]; then
    cp "$src" "$dst"
    printf 'created %s\n' "$dst"
  else
    printf 'kept existing %s\n' "$dst"
  fi
}
copy_if_missing "$base/schemas/claim_manifest.template.tsv" Papers/research/claim-manifest.tsv
copy_if_missing "$base/schemas/novelty_ledger.template.tsv" Papers/research/novelty-ledger.tsv
copy_if_missing "$base/schemas/journal_candidates.template.tsv" Papers/research/journal-candidates.tsv
if [ ! -e Papers/draft ]; then
  cp -R "$base/templates/paper" Papers/draft
  printf 'created Papers/draft\n'
else
  printf 'kept existing Papers/draft\n'
fi
