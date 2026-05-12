#!/usr/bin/env bash
# scripts/publish.sh -- Phase 30 REQ-2: self-contained Linux x64 single-file
# publish wrapper for flow-cli.
#
# Flag set locked in .planning/phases/30-flow-cli-formal-install/30-RESEARCH.md
# (dotnet publish Profile section). Mirror of the flag set in
# flow-cli/Properties/PublishProfiles/linux-x64.pubxml.
#
# Size budget: 120 MB total (SPEC-2 cap; enforced by `du -sb` post-publish).
#
# Usage:
#   bash scripts/publish.sh
#
# Exit codes:
#   0   publish OK, size <= 120 MB, smoke test passed
#   1   any step failed (build, missing stdlib in output, size cap exceeded,
#       version smoke failed)

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$PROJECT_ROOT/publish/flow-linux-x64"
SIZE_BUDGET_MB=120

cd "$PROJECT_ROOT"

# Clean previous publish to avoid stale artifacts.
rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

echo "==> Publishing flow-cli (self-contained linux-x64, single-file)..."
dotnet publish flow-cli/flow-cli.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:DebugType=embedded \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o "$OUT_DIR"

# Verify the binary exists and is executable.
if [[ ! -x "$OUT_DIR/flow" ]]; then
  echo "ERROR: $OUT_DIR/flow was not produced or is not executable." >&2
  exit 1
fi

# Verify stdlib .flow files made it in.
for f in std.flow collections.flow audio.flow bars.flow notation.flow composition.flow; do
  if [[ ! -f "$OUT_DIR/$f" ]]; then
    echo "ERROR: stdlib file $OUT_DIR/$f missing from publish output." >&2
    echo "  Hint: confirm CopyToPublishDirectory=PreserveNewest on flow-lang.csproj for $f." >&2
    exit 1
  fi
done

# Enforce SPEC-2 size budget.
SIZE_BYTES=$(du -sb "$OUT_DIR" | awk '{print $1}')
SIZE_MB=$(( SIZE_BYTES / 1024 / 1024 ))
echo "==> Publish size: ${SIZE_MB} MB (budget: ${SIZE_BUDGET_MB} MB)"
if [[ $SIZE_MB -gt $SIZE_BUDGET_MB ]]; then
  echo "ERROR: Publish output exceeds SPEC-2 budget (${SIZE_MB} MB > ${SIZE_BUDGET_MB} MB)." >&2
  exit 1
fi

# Smoke: run the published binary's version subcommand.
echo "==> Smoke test: ./flow version"
"$OUT_DIR/flow" version

echo "==> Publish OK. Binary: $OUT_DIR/flow"
