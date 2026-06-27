#!/usr/bin/env bash
#
# Flow interpreter benchmark harness driver.
#
# Builds the solution in Release mode once, then runs each bench_*.flow
# script N=5 times under /usr/bin/time -f "%e %M" so we can pin wall-clock
# seconds + max RSS MB before/after each upcoming optimization bundle.
#
# Usage:
#   bash bench/run.sh                          # label defaults to "current"
#   bash bench/run.sh --label baseline         # capture baseline
#   bash bench/run.sh --label bundle-a         # post-optimization compare
#
# Linux + .NET 10 only (uses GNU /usr/bin/time -f, NOT BSD/macOS time).

set -euo pipefail

# ---------- arg parsing -------------------------------------------------------

LABEL="current"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --label)
            if [[ $# -lt 2 ]]; then
                echo "error: --label requires a value" >&2
                exit 2
            fi
            LABEL="$2"
            shift 2
            ;;
        -h|--help)
            sed -n '3,16p' "$0"
            exit 0
            ;;
        *)
            echo "error: unknown argument: $1" >&2
            echo "usage: bash bench/run.sh [--label NAME]" >&2
            exit 2
            ;;
    esac
done

# Sanitize label so it can safely appear in a filename.
if [[ ! "$LABEL" =~ ^[A-Za-z0-9._-]+$ ]]; then
    echo "error: --label must match [A-Za-z0-9._-]+ (got '$LABEL')" >&2
    exit 2
fi

# ---------- environment checks -----------------------------------------------

if ! command -v /usr/bin/time >/dev/null 2>&1; then
    echo "error: /usr/bin/time not found. This harness targets Linux GNU time -f." >&2
    echo "       BSD/macOS time does not accept -f and is not supported." >&2
    exit 3
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo "error: dotnet not found on PATH." >&2
    exit 3
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BENCH_DIR="$REPO_ROOT/bench"
SOLUTION="$REPO_ROOT/flow-sharp.sln"
INTERPRETER_PROJ="$REPO_ROOT/flow-interpreter"

if [[ ! -f "$SOLUTION" ]]; then
    echo "error: solution not found at $SOLUTION" >&2
    exit 3
fi

# ---------- tmpdir + trap ----------------------------------------------------

TMPDIR="$(mktemp -d -t flow-bench-XXXXXX)"
trap 'rm -rf "$TMPDIR"' EXIT

# ---------- build once -------------------------------------------------------

echo "[run.sh] building $SOLUTION in Release mode..."
if ! dotnet build -c Release "$SOLUTION" >"$TMPDIR/build.log" 2>&1; then
    echo "error: dotnet build failed. Last 20 lines:" >&2
    tail -n 20 "$TMPDIR/build.log" >&2
    exit 4
fi
echo "[run.sh] build OK."

# ---------- discover bench scripts -------------------------------------------

# Top-level bench_*.flow only — exclude the bench_parse_imports/ subtree.
mapfile -t SCRIPTS < <(find "$BENCH_DIR" -maxdepth 1 -name 'bench_*.flow' -type f | sort)

if [[ ${#SCRIPTS[@]} -eq 0 ]]; then
    echo "error: no bench_*.flow scripts found in $BENCH_DIR" >&2
    exit 5
fi

# ---------- run each script N times ------------------------------------------

N_RUNS=5
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
RESULTS_FILE="$BENCH_DIR/results-${LABEL}-${TIMESTAMP}.txt"
ISO_DATE="$(date -Iseconds)"

DOTNET_VERSION="$(dotnet --version 2>/dev/null || echo unknown)"
GIT_REV="$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)"

{
    echo "# Flow benchmark run — label: $LABEL — $ISO_DATE"
    echo ""
    echo "Build: dotnet $DOTNET_VERSION, git rev $GIT_REV, $N_RUNS runs per script."
    echo ""
    echo "| Script | Mean (s) | Stddev (s) | Mean RSS (MB) | Stddev RSS (MB) |"
    echo "|--------|---------:|-----------:|--------------:|----------------:|"
} | tee "$RESULTS_FILE"

for script in "${SCRIPTS[@]}"; do
    script_name="$(basename "$script")"
    elapsed_samples=()
    rss_samples_kb=()

    for ((i = 1; i <= N_RUNS; i++)); do
        timing_file="$TMPDIR/timing-${script_name}-${i}.txt"
        # /usr/bin/time prints %e %M to STDERR after the program exits.
        # We discard program STDOUT (PASSED sentinel + total= line) and
        # capture only the time output via 2> redirect.
        if ! /usr/bin/time -f "%e %M" -o "$timing_file" \
            dotnet run --project "$INTERPRETER_PROJ" -c Release --no-build -- "$script" \
            >/dev/null 2>"$TMPDIR/stderr-${script_name}-${i}.txt"; then
            echo "" >&2
            echo "error: run $i of $script_name failed. Tail of stderr:" >&2
            tail -n 10 "$TMPDIR/stderr-${script_name}-${i}.txt" >&2
            exit 6
        fi

        # The timing line is the LAST line of -o file. Parse "%e %M".
        timing_line="$(tail -n 1 "$timing_file")"
        read -r elapsed rss_kb <<<"$timing_line"
        elapsed_samples+=("$elapsed")
        rss_samples_kb+=("$rss_kb")
    done

    # Compute mean + sample stddev via awk (no bc dep).
    # RSS reported in MB = KB/1024 (one decimal).
    stats="$(awk -v e_list="${elapsed_samples[*]}" -v r_list="${rss_samples_kb[*]}" '
        function mean_stddev(values, out_mean, out_stddev,    i, n, s, m, ss) {
            n = split(values, arr, " ")
            s = 0
            for (i = 1; i <= n; i++) s += arr[i]
            m = s / n
            ss = 0
            for (i = 1; i <= n; i++) ss += (arr[i] - m) * (arr[i] - m)
            # sample stddev uses n-1; guard the n=1 case (would never happen but safe).
            stddev = (n > 1) ? sqrt(ss / (n - 1)) : 0
            return m "|" stddev
        }
        BEGIN {
            split(mean_stddev(e_list), e_parts, "|")
            split(mean_stddev(r_list), r_parts, "|")
            printf "%.3f|%.3f|%.1f|%.1f\n", \
                e_parts[1], e_parts[2], \
                r_parts[1] / 1024.0, r_parts[2] / 1024.0
        }
    ')"

    IFS='|' read -r e_mean e_stddev r_mean r_stddev <<<"$stats"

    row="| $script_name | $e_mean | $e_stddev | $r_mean | $r_stddev |"
    echo "$row" | tee -a "$RESULTS_FILE"
done

{
    echo ""
    echo "Build: dotnet $DOTNET_VERSION, git rev $GIT_REV"
    echo "Runs:  $N_RUNS per script"
    echo "Host:  $(uname -srm)"
} | tee -a "$RESULTS_FILE"

echo ""
echo "$RESULTS_FILE"
