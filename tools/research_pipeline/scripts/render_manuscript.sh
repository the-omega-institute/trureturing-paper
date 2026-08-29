#!/bin/sh
set -eu
artifact_root=${FKST_PIPELINE_ARTIFACT_ROOT:-.fkst-pipeline/artifacts}
src=${FKST_MANUSCRIPT_TEX:-Papers/draft/main.tex}
[ -s "$src" ] || { echo "missing manuscript source: $src" >&2; exit 66; }
base=$(basename "$src" .tex)
dir=$(dirname "$src")
build=$artifact_root/latex/manuscript
rm -rf "$build"
mkdir -p "$build" "$artifact_root"
cp -R "$dir"/. "$build"/
if command -v latexmk >/dev/null 2>&1; then
  bib_mode=
  if ! command -v bibtex >/dev/null 2>&1; then
    if grep -R -E '\\cite[a-zA-Z*]*\{' "$build" --include='*.tex' >/dev/null 2>&1; then
      echo 'manuscript contains citations but bibtex is unavailable' >&2
      exit 78
    fi
    bib_mode=-bibtex-
  fi
  (cd "$build" && latexmk -pdf $bib_mode -interaction=nonstopmode -halt-on-error "$base.tex")
elif command -v tectonic >/dev/null 2>&1; then
  (cd "$build" && tectonic "$base.tex")
else
  echo 'no supported LaTeX compiler' >&2
  exit 78
fi
cp "$build/$base.pdf" "$artifact_root/manuscript.pdf"
