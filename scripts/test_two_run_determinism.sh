#!/usr/bin/env bash
# =============================================================================
# Phase 36 Plan 36-01 (D-v1.5-06 / D-36-09) — two-run determinism harness.
#
# Usage:
#   scripts/test_two_run_determinism.sh path/to/script.flow [--render-cmd CMD]
#
# Renders the given .flow file twice via the `flow` CLI, captures both WAV
# outputs, and compares their SHA-256s. Exits 0 iff byte-identical; 1 otherwise.
# Prints both SHA-256s to stdout for diagnostic.
#
# Downstream consumers: Phase 36 generative-primitive plans (36-05/06/07/08/
# 09/11) call this script in their <verify> blocks to gate the two-run cmp-clean
# determinism contract inherited from Phase 18/25/27/28/29/33.
#
# The .flow script under test MUST emit a `(writeWav "path" ...)` call —
# `flow render` honours the script's writeWav target (Phase 30 D-04). The
# harness inspects the script for the writeWav path so it can compare the
# files the script actually produces.
# =============================================================================

set -euo pipefail

usage() {
    cat <<EOF
Usage: $0 <script.flow> [--render-cmd "<cmd>"]

Arguments:
  <script.flow>      Path to the .flow script to render twice (must contain
                     a (writeWav "out.wav" buf) call).

Options:
  --render-cmd CMD   Override the render command (default: "flow render").
                     The script substitutes <SCRIPT> for the .flow path
                     and <OUT> for the -o output path (e.g. for testing
                     against a non-PATH binary: --render-cmd "dotnet run
                     --project flow-cli -- render <SCRIPT> -o <OUT>").

Exit codes:
  0  Two runs produced byte-identical WAV output.
  1  WAV outputs differ.
  2  Usage / setup error.
EOF
}

if [[ $# -lt 1 ]]; then
    usage >&2
    exit 2
fi

SCRIPT=""
RENDER_CMD="flow render"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --render-cmd)
            shift
            RENDER_CMD="$1"
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
            if [[ -z "$SCRIPT" ]]; then
                SCRIPT="$1"
            else
                echo "ERROR: unexpected positional arg: $1" >&2
                usage >&2
                exit 2
            fi
            shift
            ;;
    esac
done

if [[ -z "$SCRIPT" ]]; then
    echo "ERROR: missing script argument" >&2
    usage >&2
    exit 2
fi

if [[ ! -f "$SCRIPT" ]]; then
    echo "ERROR: script not found: $SCRIPT" >&2
    exit 2
fi

# Extract the writeWav target path from the script. Tolerant of leading
# whitespace and various quote styles. Picks the FIRST writeWav target —
# scripts that write multiple WAVs are out of scope for this minimal harness
# (downstream Phase 36 tests can wrap multiple invocations).
WRITE_TARGET=$(grep -oE '\(writeWav[[:space:]]+"[^"]+"' "$SCRIPT" | head -1 | sed -E 's/.*"([^"]+)".*/\1/')

if [[ -z "$WRITE_TARGET" ]]; then
    echo "ERROR: could not find a (writeWav \"path\" ...) call in $SCRIPT" >&2
    echo "       This harness requires the script to emit a WAV file via writeWav." >&2
    exit 2
fi

# Resolve WRITE_TARGET relative to the script's directory (matches `flow render`
# behavior: it runs the script with CWD = directory of the script).
SCRIPT_DIR=$(dirname "$SCRIPT")
if [[ "$WRITE_TARGET" = /* ]]; then
    RESOLVED_TARGET="$WRITE_TARGET"
else
    RESOLVED_TARGET="$SCRIPT_DIR/$WRITE_TARGET"
fi

WORK_DIR=$(mktemp -d "${TMPDIR:-/tmp}/flow_det_XXXXXX")
trap 'rm -rf "$WORK_DIR"' EXIT

RUN_A="$WORK_DIR/run_a.wav"
RUN_B="$WORK_DIR/run_b.wav"

run_once() {
    local label="$1"
    local out="$2"
    # The -o argument is honoured for `flow render` (it warns if the script's
    # writeWav target differs from --output, but still executes the script).
    # We copy the script's actual write target to $out after each run.
    local cmd="${RENDER_CMD//<SCRIPT>/$SCRIPT}"
    cmd="${cmd//<OUT>/$out}"

    # If the command doesn't contain a script substitution and doesn't include
    # an explicit -o, append the standard `flow render` arguments.
    if [[ "$cmd" = "$RENDER_CMD" ]]; then
        # No substitution happened; append default args.
        cmd="$RENDER_CMD $SCRIPT -o $out"
    fi

    # Remove any prior run's WAV so a render failure doesn't silently reuse
    # stale output.
    rm -f "$RESOLVED_TARGET"

    if ! eval "$cmd" >/dev/null 2>&1; then
        echo "ERROR: render failed on $label run" >&2
        exit 2
    fi

    if [[ ! -f "$RESOLVED_TARGET" ]]; then
        echo "ERROR: $label run did not produce expected WAV: $RESOLVED_TARGET" >&2
        exit 2
    fi

    cp "$RESOLVED_TARGET" "$out"
}

run_once "first" "$RUN_A"
run_once "second" "$RUN_B"

SHA_A=$(sha256sum "$RUN_A" | awk '{print $1}')
SHA_B=$(sha256sum "$RUN_B" | awk '{print $1}')

echo "Run A: $SHA_A"
echo "Run B: $SHA_B"

if [[ "$SHA_A" = "$SHA_B" ]]; then
    echo "Two-run determinism: PASS (identical SHA-256)"
    exit 0
else
    echo "Two-run determinism: FAIL (SHA-256 mismatch)" >&2
    exit 1
fi
