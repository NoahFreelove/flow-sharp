#!/usr/bin/env bash
# =============================================================================
# Phase 42 Plan 02 — .flow caller index for AUDIT-05 false-positive guard.
#
# Usage:
#   scripts/audit/flow-callers.sh [--out-dir DIR]
#
# Builds the .flow stdlib + examples + tests caller index needed by Plan 03
# AUDIT.md §4 (Dead-End Builtins) to suppress false-positive candidates
# (RESEARCH §Pitfall 1: a C# builtin with zero C# callers may still be
# reached via a .flow consumer).
#
# Emits two files under --out-dir:
#   - flow-proc-decls.txt   one proc name per line, sorted-unique
#   - flow-call-sites.txt   frequency table of every identifier-like token
#                           followed by space or '(' across all .flow files
#                           (count + token, descending)
#
# Default --out-dir:
#   .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data
#
# Exit codes:
#   0  Always.
#   2  Usage / setup error.
# =============================================================================

set -euo pipefail
shopt -s globstar nullglob

usage() {
    cat <<EOF
Usage: $0 [--out-dir DIR]

Options:
  --out-dir DIR  Where to write the inventory files. Default:
                 .planning/phases/42-type-system-stdlib-audit/42-AUDIT-data
                 (relative to repo root).
  -h, --help     Show this help.

Produces (under --out-dir):
  flow-proc-decls.txt   sorted-unique proc names declared in any .flow file
                        under flow-lang/, examples/, tests/
  flow-call-sites.txt   frequency table of identifier-followed-by-space-or-'('
                        tokens across all .flow files (count<space>token,
                        sorted descending by count)
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

# Locate repo root by walking up until we find flow-sharp.sln.
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
    OUT_DIR="$REPO_ROOT/.planning/phases/42-type-system-stdlib-audit/42-AUDIT-data"
else
    OUT_DIR="$OUT_DIR_ARG"
fi

mkdir -p "$OUT_DIR"

# Resolve the .flow file set. Use bash arrays so globstar expansion handles
# nested examples/**/*.flow correctly. nullglob (set above) means a missing
# directory expands to nothing instead of the literal pattern.
cd "$REPO_ROOT"

FLOW_FILES=()
for f in flow-lang/*.flow examples/**/*.flow tests/test_*.flow; do
    if [[ -f "$f" ]]; then
        FLOW_FILES+=("$f")
    fi
done

if [[ ${#FLOW_FILES[@]} -eq 0 ]]; then
    echo "ERROR: no .flow files found under flow-lang/, examples/, or tests/" >&2
    exit 2
fi

# (1) Proc declarations: every line starting with `internal proc ` or `proc `,
# stripped down to just the proc name. Sorted-unique. RESEARCH §Pattern 4:
# a C# dead-end candidate cleared by `grep -c "^NAME$" flow-proc-decls.txt > 0`
# is a true dead-end; non-zero count means a .flow consumer exists.
grep -rEh "^(internal[[:space:]]+)?proc[[:space:]]+" "${FLOW_FILES[@]}" 2>/dev/null \
    | sed -E 's/.*proc[[:space:]]+([a-zA-Z_][a-zA-Z0-9_]*).*/\1/' \
    | sort -u \
    > "$OUT_DIR/flow-proc-decls.txt"

# (2) Call-site frequency table: every identifier-like token followed by space
# or '('. This catches both prefix-form `(funcName arg)` and proc-call style.
# Plan 03 uses this as a frequency-weighted call-site index — a "dead-end"
# candidate with a high frequency-table entry here is almost certainly being
# reached via .flow callers.
grep -rho "[a-zA-Z_][a-zA-Z0-9_]*[ (]" "${FLOW_FILES[@]}" 2>/dev/null \
    | sed 's/[ (]$//' \
    | sort \
    | uniq -c \
    | sort -rn \
    > "$OUT_DIR/flow-call-sites.txt"

PROC_N=$(wc -l < "$OUT_DIR/flow-proc-decls.txt")
SITES_N=$(wc -l < "$OUT_DIR/flow-call-sites.txt")
FILE_N=${#FLOW_FILES[@]}

echo "Phase 42 Plan 02 — .flow caller index"
echo "Scanned: $FILE_N .flow files under flow-lang/, examples/, tests/"
echo "flow-proc-decls.txt: $PROC_N unique proc names"
echo "flow-call-sites.txt: $SITES_N unique call-site tokens"

exit 0
