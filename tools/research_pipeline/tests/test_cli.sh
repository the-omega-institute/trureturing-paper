#!/bin/sh
set -eu
BIN=${BIN:-./bin/fkst-pipeline}
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT HUP INT TERM

expect_fail() {
  if "$@" >/dev/null 2>&1; then
    echo "expected failure: $*" >&2
    exit 1
  fi
}

cat > "$TMP/novelty-bad.tsv" <<'EOT'
prior_id	source_checked_at	prior_statement	hypotheses_delta	conclusion_delta	method_delta	counterexample_or_boundary	significance	novelty_status
P1	TODO	TODO	TODO	TODO	TODO	TODO	TODO	pending
EOT
expect_fail "$BIN" gate novelty "$TMP/novelty-bad.tsv"
DATE=$(date +%Y-%m-%d)
cat > "$TMP/novelty.tsv" <<EOT
prior_id	source_checked_at	prior_statement	hypotheses_delta	conclusion_delta	method_delta	counterexample_or_boundary	significance	novelty_status
P1	$DATE	Prior theorem A	weaker compactness assumption	stronger uniqueness result	constructive observer argument	fails without separation	classifies a new completion regime	supported
P2	$DATE	Prior theorem B	adds explicit functoriality	naturality conclusion	machine-checked factorization	counterexample for nonfunctorial map	enables compositional reuse	supported
EOT
"$BIN" gate novelty "$TMP/novelty.tsv" >/dev/null

mkdir -p "$TMP/Fkst"
cat > "$TMP/Fkst/Main.lean" <<'EOT'
theorem verified_main : True := by trivial
EOT
cat > "$TMP/claims.tsv" <<'EOT'
claim_id	latex_label	lean_declaration	lean_file	declaration_kind	proof_status	evidence_hash	exposition_status
C1	thm:main	verified_main	Fkst/Main.lean	theorem	verified	0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef	formal
EOT
"$BIN" gate claims "$TMP" "$TMP/claims.tsv" >/dev/null
sed 's/verified_main/missing_decl/' "$TMP/claims.tsv" > "$TMP/claims-bad.tsv"
expect_fail "$BIN" gate claims "$TMP" "$TMP/claims-bad.tsv"

cat > "$TMP/journals-one.tsv" <<EOT
journal_id	policy_tier	scope_fit	article_type_fit	requirements_url	checked_at	format_profile	disqualifiers	eligible	rationale
J1	2	logic and formal mathematics	research article	https://example.test/j1	$DATE	generic	length limit checked	yes	exact scope match
EOT
expect_fail "$BIN" gate journals "$TMP/journals-one.tsv"
cat > "$TMP/journals.tsv" <<EOT
journal_id	policy_tier	scope_fit	article_type_fit	requirements_url	checked_at	format_profile	disqualifiers	eligible	rationale
J1	2	logic and formal mathematics	research article	https://example.test/j1	$DATE	generic	length limit checked	yes	exact scope match
J2	1	pure mathematics and foundations	research article	https://example.test/j2	$DATE	generic	formalization emphasis assessed	yes	main theorem fits scope
EOT
"$BIN" gate journals "$TMP/journals.tsv" >/dev/null

for n in 1 2 3; do
  verdict=revise; blocking=1
  [ "$n" -eq 3 ] && verdict=pass && blocking=0
  REPORT_HASH=$(printf '%064d' "$n")
  MANUSCRIPT_HASH=$(printf '%064d' "$((n + 10))")
  cat > "$TMP/review$n.tsv" <<EOT
reviewer_id	round	focus	verdict	blocking_issue_count	report_hash	manuscript_hash
reviewer-$n	$n	focus-$n	$verdict	$blocking	$REPORT_HASH	$MANUSCRIPT_HASH
EOT
done
"$BIN" gate reviews "$TMP/review1.tsv" "$TMP/review2.tsv" "$TMP/review3.tsv" >/dev/null
cp "$TMP/review2.tsv" "$TMP/review2-bad.tsv"
HASH1=$(printf '%064d' 11)
HASH2=$(printf '%064d' 12)
sed "s/$HASH2/$HASH1/" "$TMP/review2-bad.tsv" > "$TMP/review2-bad.next.tsv"
mv "$TMP/review2-bad.next.tsv" "$TMP/review2-bad.tsv"
expect_fail "$BIN" gate reviews "$TMP/review1.tsv" "$TMP/review2-bad.tsv" "$TMP/review3.tsv"

printf '%s\n' '{"status":"blocked"}' > "$TMP/receipt-bad.json"
expect_fail "$BIN" gate receipts "$TMP/receipt-bad.json"
printf '%s\n' '{"status":"pass"}' > "$TMP/receipt-good.json"
"$BIN" gate receipts "$TMP/receipt-good.json" >/dev/null

printf '%s\n' '\placeholder{author}' > "$TMP/text-bad.tex"
expect_fail "$BIN" gate clean-text "$TMP/text-bad.tex"
printf '%s\n' 'Final author text' > "$TMP/text-good.tex"
"$BIN" gate clean-text "$TMP/text-good.tex" >/dev/null

printf 'bad' > "$TMP/bad.pdf"
expect_fail "$BIN" gate pdf "$TMP/bad.pdf"
printf '%%PDF-1.4\n' > "$TMP/good.pdf"
dd if=/dev/zero bs=1 count=2048 >> "$TMP/good.pdf" 2>/dev/null
"$BIN" gate pdf "$TMP/good.pdf" >/dev/null

mkdir -p "$TMP/project"
cat > "$TMP/project/pipeline.tsv" <<EOT
@root=$TMP/project
@state=.state
@event_log=events.jsonl
@run_log_dir=runs
@max_transitions=8
first	second	second	2	0	false	-
second	DONE	STOP	2	0	printf x > result.txt	result.txt
EOT
"$BIN" run --config "$TMP/project/pipeline.tsv" --dry-run > "$TMP/plan.txt"
grep -q 'PLAN stage=first' "$TMP/plan.txt"
grep -q 'PLAN complete' "$TMP/plan.txt"
[ ! -e "$TMP/project/.state" ] || { echo 'dry-run mutated pipeline state' >&2; exit 1; }
[ ! -e "$TMP/project/events.jsonl" ] || { echo 'dry-run wrote event log' >&2; exit 1; }
"$BIN" run --config "$TMP/project/pipeline.tsv" >/dev/null
[ -s "$TMP/project/result.txt" ] || { echo 'pipeline transition test failed' >&2; exit 1; }
grep -q 'current=DONE' "$TMP/project/.state"
grep -q '"stage":"first"' "$TMP/project/events.jsonl"
grep -q '"outcome":"fail"' "$TMP/project/events.jsonl"

echo 'all tests passed'
