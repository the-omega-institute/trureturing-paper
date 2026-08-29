#!/bin/sh
set -eu
artifact_root=${FKST_PIPELINE_ARTIFACT_ROOT:-.fkst-pipeline/artifacts}
out=$artifact_root/inventory.md
mkdir -p "$(dirname "$out")"
{
  echo '# trureturing-paper research inventory'
  echo
  printf 'Generated (UTC): '
  date -u +%Y-%m-%dT%H:%M:%SZ
  echo
  echo '## Git state'
  echo '```text'
  git status --short --branch 2>&1 || true
  git log -1 --oneline --decorate 2>&1 || true
  echo '```'
  echo
  echo '## Research, workflow, proof, and manuscript files'
  echo '```text'
  find . -type f \( -name '*.lean' -o -name '*.tex' -o -name '*.cs' -o -name '*.c' -o -name '*.h' \
    -o -name '*.lua' -o -name '*.sh' -o -name '*.json' -o -name '*.toml' -o -name '*.md' \
    -o -name 'Makefile' -o -name 'lakefile.*' -o -name '*.yml' -o -name '*.yaml' \) \
    -not -path './.git/*' -not -path './.lake/*' -not -path './bin/*' -not -path './obj/*' \
    -not -path './.fkst-pipeline/*' | sort
  echo '```'
  echo
  echo '## Workflow symbols'
  echo '```text'
  git grep -n -I -E 'sshx|review|journal|novel|intuition|Lean|lake build|latexmk|pdflatex|cover.letter|state.machine|pipeline' \
    -- . ':!*.pdf' 2>/dev/null | head -2000 || true
  echo '```'
} > "$out"
