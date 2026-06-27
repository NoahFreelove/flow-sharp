#!/usr/bin/env bash
# =============================================================================
# Phase 44 Plan 44-00 — strict-mode site extractor.
#
# Usage:
#   scripts/audit/strict-site-grep.sh [--out-dir DIR]
#
# Re-runnable, deterministic extractor that re-emits the raw site lists feeding
# the Phase 44 strict-error-manifest.csv (the authoritative inventory consumed
# by every xUnit [Theory] in Plans 44-05..44-08).
#
# Two passes:
#   Pass A — every RenderingDiagnostics.WarnOnce( call site across
#            flow-lang/StandardLibrary/, flow-lang/Interpreter/, flow-lang/Ast/.
#   Pass B — pin the 13 §6a input-perimeter Math.Clamp sites from
#            flow-lang/StandardLibrary/Transforms/TransformFunctions.cs by
#            exact line number (per RESEARCH §"Site Inventory" Table §6a).
#
# Output (overwritten on every run; not committed):
#   .planning/phases/44-strict-mode/strict-site-raw.txt
#
# Charitable-skip semantics: this script is invoked by an xUnit [Fact] in Plan
# 44-05; if /bin/bash is missing (Windows CI), the Fact early-returns per
# Phase 42 ClampGrepConsistencyTests precedent.
#
# Per CLAUDE.md "Conventions" / D-v1.5-06 PRNG-routing: this script adds ZERO
# PRNG sites and produces byte-identical output across re-runs (modulo wall
# clock in the summary footer — segregated so the inventory body stays cmp-clean
# for two-run determinism).
#
# Exit codes:
#   0  Always. An audit grep producing zero hits is data, not failure.
#   2  Usage / setup error.
# =============================================================================

set -euo pipefail

usage() {
    cat <<EOF
Usage: $0 [--out-dir DIR]

Options:
  --out-dir DIR  Where to write the raw site list. Default:
                 .planning/phases/44-strict-mode (relative to repo root).
  -h, --help     Show this help.

Produces:
  strict-site-raw.txt   Pass A (WarnOnce sites) + Pass B (13 §6a clamp pins) +
                        a footer summary line.
EOF
}

OUT_DIR_ARG=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --out-dir)
            shift
            OUT_DIR_ARG="${1:-}"
            if [[ -z "$OUT_DIR_ARG" ]]; then
                echo "ERROR: --out-dir requires a path argument" >&2
                usage >&2
                exit 2
            fi
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        --*)
            echo "ERROR: unknown flag: $1" >&2
            usage >&2
            exit 2
            ;;
        *)
            echo "ERROR: unexpected positional arg: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

# Locate repo root by walking up from this script's directory until we find
# flow-sharp.sln. Mirrors the FindRepoRoot pattern used by xUnit fixtures.
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT="$SCRIPT_DIR"
while [[ "$REPO_ROOT" != "/" && ! -f "$REPO_ROOT/flow-sharp.sln" ]]; do
    REPO_ROOT=$(dirname "$REPO_ROOT")
done

if [[ ! -f "$REPO_ROOT/flow-sharp.sln" ]]; then
    echo "ERROR: could not locate flow-sharp.sln walking up from $SCRIPT_DIR" >&2
    exit 2
fi

if [[ -z "$OUT_DIR_ARG" ]]; then
    OUT_DIR="$REPO_ROOT/.planning/phases/44-strict-mode"
else
    OUT_DIR="$OUT_DIR_ARG"
fi

mkdir -p "$OUT_DIR"

STDLIB_DIR="$REPO_ROOT/flow-lang/StandardLibrary/"
INTERPRETER_DIR="$REPO_ROOT/flow-lang/Interpreter/"
AST_DIR="$REPO_ROOT/flow-lang/Ast/"
TRANSFORMS_FILE="$REPO_ROOT/flow-lang/StandardLibrary/Transforms/TransformFunctions.cs"

if [[ ! -d "$STDLIB_DIR" ]]; then
    echo "ERROR: flow-lang/StandardLibrary/ not found under repo root $REPO_ROOT" >&2
    exit 2
fi

OUT_FILE="$OUT_DIR/strict-site-raw.txt"

{
    echo "# Phase 44 Plan 44-00 — strict-mode site extractor output"
    echo "# Re-runnable. NOT committed. Re-run: scripts/audit/strict-site-grep.sh"
    echo "# Source-of-truth manifest is the hand-curated strict-error-manifest.csv."
    echo ""
    echo "## Pass A — RenderingDiagnostics.WarnOnce( call sites"
    echo "## (every WarnOnce reference across flow-lang/StandardLibrary,"
    echo "##  flow-lang/Interpreter, flow-lang/Ast; includes doc-only XML refs"
    echo "##  which the curated CSV filters out)"
    echo ""
    # || true so set -e doesn't kill us on zero matches. Sort by file:line for
    # deterministic ordering across reruns (different filesystems may produce
    # different grep -r output ordering otherwise — Pitfall 6 cmp-clean).
    {
        grep -rn "RenderingDiagnostics\.WarnOnce" "$STDLIB_DIR" "$INTERPRETER_DIR" "$AST_DIR" 2>/dev/null || true
    } | LC_ALL=C sort -t: -k1,1 -k2,2n
    echo ""
    echo "## Pass B — 13 §6a input-perimeter Math.Clamp sites"
    echo "## (pinned by exact line number per RESEARCH Site Inventory §6a)"
    echo ""
    if [[ -f "$TRANSFORMS_FILE" ]]; then
        # Pin the 13 lines verbatim (file:line:source).
        for L in 106 107 649 650 657 658 666 667 785 821 904 960 1106; do
            # sed -n 'N p' emits the Nth line; prefix with file:line: per grep -n convention.
            LINE_TEXT=$(sed -n "${L}p" "$TRANSFORMS_FILE")
            printf '%s:%s:%s\n' "$TRANSFORMS_FILE" "$L" "$LINE_TEXT"
        done
    else
        echo "WARNING: $TRANSFORMS_FILE not found — Pass B emitted no rows" >&2
    fi
} > "$OUT_FILE"

WARN_N=$(grep -c "RenderingDiagnostics\.WarnOnce" "$OUT_FILE" || true)
CLAMP_N=13

{
    echo ""
    echo "## Total: $WARN_N WarnOnce references + $CLAMP_N input-perimeter clamps"
} >> "$OUT_FILE"

# Mirror clamp-grep.sh's stdout-summary behavior so xUnit can scrape it.
echo "Phase 44 Plan 44-00 — strict-site-grep summary"
echo "Output: $OUT_FILE"
echo "WarnOnce references: $WARN_N"
echo "§6a input-perimeter clamps: $CLAMP_N (pinned)"

exit 0
