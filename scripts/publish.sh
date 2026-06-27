#!/usr/bin/env bash
# scripts/publish.sh -- Phase 41 BIN-01: 5-RID self-contained single-file
# publish wrapper for flow-cli (generalizes the Phase 30 single-RID linux-x64 form).
#
# Produces self-contained single-file binaries for all 5 supported RIDs:
#   linux-x64  linux-arm64  osx-x64  osx-arm64  win-x64
# All 5 cross-compile from this Linux host: Flow's binaries are MANAGED-ONLY — the
# audio backends P/Invoke SYSTEM libraries (libpulse / AudioToolbox.framework / the
# WASAPI COM stack via NAudio) that are NEVER bundled, so there is no native
# cross-toolchain requirement (RESEARCH §Cross-Platform Binaries; D-14).
#
# Per RID this script:
#   - cleans publish/flow-<rid>/
#   - runs `dotnet publish flow-cli -r <rid> --self-contained` single-file, NO trimming
#     (D-15: -p:PublishTrimmed=false on EVERY rid — the reflection-heavy
#      InternalFunctionRegistry would silently break under trimming; grep gate in the
#      41-05 plan forbids PublishTrimmed=true)
#   - verifies the binary exists (`flow` for linux/osx, `flow.exe` for win-x64)
#   - verifies the stdlib .flow files copied into the output dir
#   - enforces the per-RID SPEC-2 120 MB size budget (single-RID number, applied per-RID
#     NOT summed)
#   - packages: tar.gz (linux/osx) or zip (win-x64) named flow-<rid>-v1.5.0.{tar.gz,zip}
#     (D-16)
#   - emits a .sha256 sidecar per archive (D-16, tampered-binary mitigation
#     T-41-05-TAMPER — the human verifies each checksum before attaching to the v1.5.0
#     GitHub Release, D-04 gate)
#
# Runtime SMOKE policy (D-02, Pitfall 4 — never fake a cross-OS exec):
#   - linux-x64   : `flow version` runs natively here.
#   - linux-arm64 : `flow version` runs via qemu-aarch64 user emulation IF available
#                   (qemu-aarch64 binary present AND binfmt_misc registered), otherwise
#                   skip-with-reason (the artifact is still built + checksummed; the
#                   missing-qemu case does NOT fail the script).
#   - osx-x64 / osx-arm64 / win-x64 : NEVER executed here — a Linux box cannot run them.
#                   Their execution smoke is the 41-HUMAN-UAT.md gate (rows 3-5, D-05).
#
# Usage:
#   bash scripts/publish.sh
#
# Exit codes:
#   0   all 5 RIDs published + packaged + checksummed; linux smoke green-or-skipped
#   1   any step failed (build, missing stdlib, per-RID size cap exceeded, missing
#       expected binary, linux-x64 version smoke failed)

set -euo pipefail

# WR-05: clean up a half-written archive on interrupt. Without this an interrupted
# tar/zip leaves a partial flow-<rid>-v1.5.0.{tar.gz,zip} (no .sha256 sidecar) in
# $PUBLISH_ROOT that can confuse a human comparing the release artifact set. The
# trap removes whatever archive is in flight, then exits 130 (script interrupted).
_CURRENT_ARCHIVE=""
trap 'if [[ -n "$_CURRENT_ARCHIVE" ]]; then rm -f "$_CURRENT_ARCHIVE"; fi; exit 130' INT TERM

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_ROOT="$PROJECT_ROOT/publish"
VERSION="${VERSION:-v1.5.0}"
SIZE_BUDGET_MB=120

# The 5 supported RIDs. All cross-compile from Linux (managed-only binaries).
RIDS=(linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64)

# stdlib .flow files that MUST land in every output dir (engine ships them via
# CopyToPublishDirectory=PreserveNewest on flow-lang.csproj).
STDLIB_FILES=(std.flow collections.flow audio.flow bars.flow notation.flow composition.flow \
              patterns.flow generative.flow improv.flow)

cd "$PROJECT_ROOT"
mkdir -p "$PUBLISH_ROOT"

# ---------------------------------------------------------------------------
# Per-RID publish + package + checksum loop.
# ---------------------------------------------------------------------------
for RID in "${RIDS[@]}"; do
  OUT="$PUBLISH_ROOT/flow-$RID"

  echo
  echo "================================================================"
  echo "==> [$RID] Publishing flow-cli (self-contained, single-file, no-trim)..."
  echo "================================================================"

  # Clean previous publish for this RID to avoid stale artifacts.
  rm -rf "$OUT"
  mkdir -p "$OUT"

  # Exact flag set locked in 30-RESEARCH.md + 41-RESEARCH.md.
  # D-15: -p:PublishTrimmed=false on EVERY rid (NEVER PublishTrimmed=true).
  dotnet publish flow-cli/flow-cli.csproj \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:DebugType=embedded \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$OUT"

  # Expected binary name: `flow.exe` on Windows, `flow` elsewhere.
  case "$RID" in
    win-x64) BIN_NAME="flow.exe" ;;
    *)       BIN_NAME="flow" ;;
  esac

  if [[ ! -f "$OUT/$BIN_NAME" ]]; then
    echo "ERROR: [$RID] $OUT/$BIN_NAME was not produced." >&2
    exit 1
  fi
  # Linux/osx binaries should also be executable; win .exe carries no unix +x bit.
  case "$RID" in
    win-x64) : ;;
    *) if [[ ! -x "$OUT/$BIN_NAME" ]]; then
         echo "ERROR: [$RID] $OUT/$BIN_NAME is not executable." >&2
         exit 1
       fi ;;
  esac

  # Verify stdlib .flow files made it into the output dir.
  for f in "${STDLIB_FILES[@]}"; do
    if [[ ! -f "$OUT/$f" ]]; then
      echo "ERROR: [$RID] stdlib file $OUT/$f missing from publish output." >&2
      echo "  Hint: confirm CopyToPublishDirectory=PreserveNewest on flow-lang.csproj for $f." >&2
      exit 1
    fi
  done

  # Enforce SPEC-2 size budget PER RID (120 MB is a single-RID number, not a sum).
  SIZE_BYTES=$(du -sb "$OUT" | awk '{print $1}')
  SIZE_MB=$(( SIZE_BYTES / 1024 / 1024 ))
  echo "==> [$RID] Publish size: ${SIZE_MB} MB (per-RID budget: ${SIZE_BUDGET_MB} MB)"
  if [[ $SIZE_MB -gt $SIZE_BUDGET_MB ]]; then
    echo "ERROR: [$RID] Publish output exceeds SPEC-2 budget (${SIZE_MB} MB > ${SIZE_BUDGET_MB} MB)." >&2
    exit 1
  fi

  # Package: zip for win-x64, tar.gz for everything else. Archive name carries the
  # version per D-16: flow-<rid>-v1.5.0.{tar.gz,zip}.
  # WR-05: mark the archive as in-flight so the INT/TERM trap removes it if the
  # tar/zip is interrupted mid-write. Cleared once the archive + its checksum are
  # fully written (a complete artifact must survive a later interrupt).
  case "$RID" in
    win-x64)
      ARCHIVE="$PUBLISH_ROOT/flow-$RID-$VERSION.zip"
      rm -f "$ARCHIVE"
      _CURRENT_ARCHIVE="$ARCHIVE"
      ( cd "$OUT" && zip -q -r "$ARCHIVE" . )
      ;;
    *)
      ARCHIVE="$PUBLISH_ROOT/flow-$RID-$VERSION.tar.gz"
      rm -f "$ARCHIVE"
      _CURRENT_ARCHIVE="$ARCHIVE"
      tar -czf "$ARCHIVE" -C "$OUT" .
      ;;
  esac

  if [[ ! -f "$ARCHIVE" ]]; then
    echo "ERROR: [$RID] archive $ARCHIVE was not produced." >&2
    exit 1
  fi

  # .sha256 sidecar (D-16, T-41-05-TAMPER). Write the bare-filename form so the human
  # can `cd publish && sha256sum -c flow-<rid>-v1.5.0.<ext>.sha256` after download.
  ( cd "$PUBLISH_ROOT" && sha256sum "$(basename "$ARCHIVE")" > "$(basename "$ARCHIVE").sha256" )
  # Archive + checksum are complete — no longer in-flight; the trap must not delete it.
  _CURRENT_ARCHIVE=""
  echo "==> [$RID] Archive: $ARCHIVE"
  echo "==> [$RID] Checksum: $(cat "$ARCHIVE.sha256")"
done

# ---------------------------------------------------------------------------
# Runtime smoke (linux RIDs only — D-02, Pitfall 4).
# ---------------------------------------------------------------------------
echo
echo "================================================================"
echo "==> Runtime smoke tests (linux RIDs only)"
echo "================================================================"

# linux-x64 : native exec. This MUST pass (failure exits 1).
echo "==> [linux-x64] Smoke: ./flow version"
"$PUBLISH_ROOT/flow-linux-x64/flow" version

# linux-arm64 : best-effort qemu-aarch64 user emulation. This is ATTEMPTED but NEVER
# fatal — qemu-user also needs the aarch64 glibc loader (/lib/ld-linux-aarch64.so.1)
# and an aarch64 sysroot, which an x64 host may not provide even when the qemu binary
# is binfmt-registered. Any failure (no qemu, missing loader, emulation crash) downgrades
# to skip-with-reason; the artifact is still built + checksummed, and real arm64 exec is
# the 41-HUMAN-UAT.md gate / real arm64 hardware.
ARM64_BIN="$PUBLISH_ROOT/flow-linux-arm64/flow"
arm64_smoke_ok=0
if command -v qemu-aarch64 >/dev/null 2>&1; then
  echo "==> [linux-arm64] Smoke (best-effort qemu-aarch64): qemu-aarch64 ./flow version"
  # Invoke the emulator EXPLICITLY (do not rely on binfmt transparent dispatch, whose
  # failure mode is an opaque non-zero exit) and tolerate failure without aborting.
  if qemu-aarch64 "$ARM64_BIN" version; then
    arm64_smoke_ok=1
  fi
fi
if [[ $arm64_smoke_ok -eq 1 ]]; then
  echo "==> [linux-arm64] exec smoke PASSED via qemu-aarch64 user emulation."
else
  echo "==> [linux-arm64] exec smoke SKIPPED — qemu-aarch64 absent or could not emulate" \
       "this host's arm64 binary (e.g. missing /lib/ld-linux-aarch64.so.1 sysroot)." \
       "Non-fatal: the cross-compile artifact is built + checksummed; arm64 exec is" \
       "verified on real arm64 hardware or via the 41-HUMAN-UAT.md gate."
fi

# osx-x64 / osx-arm64 / win-x64 : NEVER executed here (Pitfall 4 — the Linux box
# cannot run them). Their execution smoke is the 41-HUMAN-UAT.md gate (rows 3-5, D-05).
echo "==> [osx-x64 / osx-arm64 / win-x64] exec smoke NOT run here —" \
     "cross-compiled artifacts can only be exercised on their target OS."
echo "    Their execution smoke is the 41-HUMAN-UAT.md gate (rows 3-5, D-05) — never faked."

# ---------------------------------------------------------------------------
# Summary.
# ---------------------------------------------------------------------------
echo
echo "================================================================"
echo "==> Publish OK. Artifacts under $PUBLISH_ROOT:"
echo "================================================================"
ls -1 "$PUBLISH_ROOT"/flow-*-"$VERSION".tar.gz "$PUBLISH_ROOT"/flow-*-"$VERSION".zip 2>/dev/null
echo "--- checksums ---"
ls -1 "$PUBLISH_ROOT"/*"$VERSION"*.sha256 2>/dev/null
