#!/bin/bash
# Phase 29 closure A/B render orchestration.
# Renders each fixture under (a) Phase 28 baseline and (b) Phase 29 output,
# with randomized A/B mapping per fixture and a sealed answer key.
#
# PREREQUISITE: Phase 28 baseline binaries must exist. Either:
#   (a) check out the Phase 28 closure commit before running Phase 28 rendering, or
#   (b) reuse pre-rendered Phase 28 baseline WAVs at
#       examples/output/realism_ab/phase28_baseline/{instrument}_rendered.wav
# The script assumes path (b) — pre-rendered baseline WAVs already exist on disk.
#
# Usage:
#   bash examples/scripts/realism_ab_render.sh           # randomizes A/B per fixture
#   bash examples/scripts/realism_ab_render.sh --seed N  # deterministic randomization

set -euo pipefail

FIXTURES=("piano" "brass" "sax" "strings" "flute" "drums")
REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUTPUT_DIR="${REPO_ROOT}/examples/output/realism_ab"
BASELINE_DIR="${OUTPUT_DIR}/phase28_baseline"
ANSWER_KEY="${OUTPUT_DIR}/answer_key.txt"

# Allow deterministic randomization for testing (--seed N).
# Default seed is the current PID — composer-listen runs get a fresh shuffle.
SEED=$$
if [[ "${1:-}" == "--seed" && -n "${2:-}" ]]; then SEED="$2"; fi
RANDOM=$SEED

mkdir -p "$OUTPUT_DIR"

if [[ ! -d "$BASELINE_DIR" ]]; then
  echo "ERROR: Phase 28 baseline directory missing at $BASELINE_DIR" >&2
  echo "" >&2
  echo "Render Phase 28 baselines first: check out the Phase 28 closure commit," >&2
  echo "run each fixture, then move outputs into:" >&2
  echo "  $BASELINE_DIR/<instrument>_rendered.wav" >&2
  exit 1
fi

echo "# Phase 29 A/B Answer Key" > "$ANSWER_KEY"
echo "# Sealed at $(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$ANSWER_KEY"
echo "# Seed: $SEED" >> "$ANSWER_KEY"
echo "" >> "$ANSWER_KEY"

for fixture in "${FIXTURES[@]}"; do
  echo "Rendering $fixture under Phase 29..."
  dotnet run --project "${REPO_ROOT}/flow-interpreter" \
    "${REPO_ROOT}/examples/tests/realism_ab/${fixture}.flow"

  # The fixture writes to ${OUTPUT_DIR}/${fixture}_rendered.wav by convention.
  # Stage it, then randomize the A/B label.
  phase29_src="${OUTPUT_DIR}/${fixture}_rendered.wav"
  phase28_src="${BASELINE_DIR}/${fixture}_rendered.wav"

  if [[ ! -f "$phase29_src" ]]; then
    echo "ERROR: $fixture fixture did not produce $phase29_src" >&2
    exit 1
  fi
  if [[ ! -f "$phase28_src" ]]; then
    echo "ERROR: missing Phase 28 baseline at $phase28_src" >&2
    exit 1
  fi

  # Randomize: 50/50 chance Phase 29 = A or B
  if (( RANDOM % 2 == 0 )); then
    cp "$phase29_src" "${OUTPUT_DIR}/A_${fixture}.wav"
    cp "$phase28_src" "${OUTPUT_DIR}/B_${fixture}.wav"
    echo "$fixture: A" >> "$ANSWER_KEY"
  else
    cp "$phase29_src" "${OUTPUT_DIR}/B_${fixture}.wav"
    cp "$phase28_src" "${OUTPUT_DIR}/A_${fixture}.wav"
    echo "$fixture: B" >> "$ANSWER_KEY"
  fi
done

echo ""
echo "A/B WAVs rendered to $OUTPUT_DIR/"
echo "Answer key written to $ANSWER_KEY (DO NOT VIEW BEFORE COMPLETING A/B LISTEN)"
echo ""
echo "Next steps:"
echo "  1. Composer listens to all 12 WAVs (A_*.wav vs B_*.wav per fixture)."
echo "  2. Composer writes A/B guesses into 29-VERIFICATION.md 'Blind A/B Sign-off' section."
echo "  3. Composer runs: cat $ANSWER_KEY (unseal)."
echo "  4. Tally correct: >= 5/6 = Gate A passes."
echo ""
echo "Closure: commit answer_key.txt to a SEPARATE COMMIT after the A/B listen completes,"
echo "so future readers can verify both the listen-time guesses AND the original key."
