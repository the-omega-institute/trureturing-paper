#!/bin/sh
set -eu
artifact_root=${FKST_PIPELINE_ARTIFACT_ROOT:-.fkst-pipeline/artifacts}
src=${FKST_COVER_LETTER_TEX:-Papers/draft/cover_letter.tex}
[ -s "$src" ] || { echo "missing cover-letter source: $src" >&2; exit 66; }
base=$(basename "$src" .tex)
dir=$(dirname "$src")
build=$artifact_root/latex/cover-letter
rm -rf "$build"
mkdir -p "$build" "$artifact_root"
cp -R "$dir"/. "$build"/
if command -v latexmk >/dev/null 2>&1; then
  (cd "$build" && latexmk -pdf -interaction=nonstopmode -halt-on-error "$base.tex")
elif command -v tectonic >/dev/null 2>&1; then
  (cd "$build" && tectonic "$base.tex")
else
  echo 'no supported LaTeX compiler' >&2
  exit 78
fi
cp "$build/$base.pdf" "$artifact_root/cover-letter.pdf"
