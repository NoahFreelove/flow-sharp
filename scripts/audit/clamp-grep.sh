#!/usr/bin/env bash
# =============================================================================
# Phase 42 Plan 02 — clamp/advisory inventory for AUDIT-07.
#
# Usage:
#   scripts/audit/clamp-grep.sh [--out-dir DIR]
#
# Fans out `grep -rn` over flow-lang/StandardLibrary/ (and flow-lang/ for
# advisories) to produce four categorized inventories under --out-dir:
#   - input-clamps.txt    Math.Clamp on a direct args[N].As<T>() read
#                         (input-perimeter clamp candidates per RESEARCH §Pitfall 4)
#   - all-clamps.txt      every Math.Clamp + Math.Min/Max chain (triage)
#   - advisory-sites.txt  every RenderingDiagnostics.WarnOnce call site
#   - charitable-sites.txt informal fallback markers (fallback / charitable /
#                          else return input)
# Plus summary.txt with line counts.
#
# Default --out-dir:
#   .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data
#
# Downstream consumers: Phase 42 Plan 03 AUDIT.md §6 (clamp/advisory inventory,
# load-bearing for Phase 44 per ROADMAP line 380).
#
# Exit codes:
#   0  Always. An audit grep producing zero hits is data, not failure
#      (RESEARCH §Pitfall 4 — empty grep set is meaningful).
#   2  Usage / setup error.
# =============================================================================

set -euo pipefail

usage() {
    cat <<EOF
Usage: $0 [--out-dir DIR]

Options:
  --out-dir DIR  Where to write the inventory files. Default:
                 .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data
                 (relative to repo root).
  -h, --help     Show this help.

Produces (under --out-dir):
  input-clamps.txt     Math.Clamp on direct args[N] reads
  all-clamps.txt       every Math.Clamp + Math.Min/Math.Max chain
  advisory-sites.txt   every RenderingDiagnostics.WarnOnce call
  charitable-sites.txt informal fallback/charitable markers
  summary.txt          line counts summary
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
# flow-sharp.sln. Mirrors the FindRepoRoot pattern used by xUnit fixtures
# (e.g. PrngRegistryNewRandomGateTests.cs:84-92).
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT="$SCRIPT_DIR"
while [[ "$REPO_ROOT" != "/" && ! -f "$REPO_ROOT/flow-sharp.sln" ]]; do
    REPO_ROOT=$(dirname "$REPO_ROOT")
done

if [[ ! -f "$REPO_ROOT/flow-sharp.sln" ]]; then
    echo "ERROR: could not locate flow-sharp.sln walking up from $SCRIPT_DIR" >&2
    exit 2
fi

# Resolve OUT_DIR: default relative to repo root if no override; absolute paths
# pass through; relative override is taken relative to cwd (per standard CLI
# convention).
if [[ -z "$OUT_DIR_ARG" ]]; then
    OUT_DIR="$REPO_ROOT/.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data"
else
    OUT_DIR="$OUT_DIR_ARG"
fi

mkdir -p "$OUT_DIR"

STDLIB_DIR="$REPO_ROOT/flow-lang/StandardLibrary/"
FLOWLANG_DIR="$REPO_ROOT/flow-lang/"

if [[ ! -d "$STDLIB_DIR" ]]; then
    echo "ERROR: flow-lang/StandardLibrary/ not found under repo root $REPO_ROOT" >&2
    exit 2
fi

# (1) Input-perimeter clamp candidates: Math.Clamp called on the result of a
# direct args[N].As<...>() read. RESEARCH §Pitfall 4: this heuristic catches
# clamps that "silently fix composer mistakes" at the API surface, distinct
# from output-protection clamps that bound internal algorithm intermediates.
# || true prevents `set -e` from killing us on zero matches — empty grep is data.
grep -rn "Math\.Clamp.*args\[" "$STDLIB_DIR" > "$OUT_DIR/input-clamps.txt" || true

# (2) All clamp sites (for triage; Plan 03 will cull output-protection
# entries from this superset).
grep -rnE "Math\.Clamp|Math\.Min.*Math\.Max" "$STDLIB_DIR" > "$OUT_DIR/all-clamps.txt" || true

# (3) Advisory sites — every WarnOnce call. Scoped to flow-lang/ (not just
# StandardLibrary/) because some advisories live in Runtime/ / Audio/.
grep -rn "RenderingDiagnostics\.WarnOnce" "$FLOWLANG_DIR" > "$OUT_DIR/advisory-sites.txt" || true

# (4) Charitable-fallback markers — informal grep used as triage signal for
# Plan 03's prioritization. Case-insensitive on the keywords would catch
# more but also pull in unrelated noise; staying case-sensitive per RESEARCH
# §Code Examples line 380.
grep -rnE "(fallback|charitable|else.*return.*input)" "$STDLIB_DIR" > "$OUT_DIR/charitable-sites.txt" || true

# (5) Summary with line counts. Helpful for the AUDIT.md §6 summary table.
INPUT_CLAMPS_N=$(wc -l < "$OUT_DIR/input-clamps.txt")
ALL_CLAMPS_N=$(wc -l < "$OUT_DIR/all-clamps.txt")
ADVISORY_N=$(wc -l < "$OUT_DIR/advisory-sites.txt")
CHARITABLE_N=$(wc -l < "$OUT_DIR/charitable-sites.txt")

{
    echo "Phase 42 Plan 02 — clamp/advisory inventory summary"
    echo "Generated: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "Source scope: $STDLIB_DIR (clamps + charitable), $FLOWLANG_DIR (advisories)"
    echo ""
    echo "input-clamps.txt:    $INPUT_CLAMPS_N lines"
    echo "all-clamps.txt:      $ALL_CLAMPS_N lines"
    echo "advisory-sites.txt:  $ADVISORY_N lines"
    echo "charitable-sites.txt: $CHARITABLE_N lines"
} > "$OUT_DIR/summary.txt"

cat "$OUT_DIR/summary.txt"

exit 0
