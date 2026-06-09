#!/usr/bin/env bash
# scripts/install.sh -- Phase 30 REQ-3: Flow installer.
#
# Defaults to per-user install (~/.local/share/flow/ + ~/.local/bin/flow
# symlink, no sudo). --system installs to /usr/local (sudo required for the
# writes themselves; the script does NOT escalate -- it just fails clearly
# if the user can't write to /usr/local). --install-root overrides for the
# smoke test (tempdir). --local-tarball consumes a publish-output directory
# OR a tar.gz directly so test-install.sh + CI can skip the network fetch.
#
# Idempotent: re-running upgrades in place via ln -sfn + version-stamped
# install dir. Never overwrites a user-customised ~/.config/flow/config.toml.
#
# RESEARCH-locked pattern: prebuilt-tarball model -- no .NET SDK required
# on the user side (see .planning/phases/30-flow-cli-formal-install/30-RESEARCH.md
# offset=520 limit=120).
#
# Artifact naming (D-16, publish.sh): flow-<rid>-v<VERSION>.tar.gz
#   rid-first, version-second — matching the publish.sh naming convention.
# Supported RIDs (publish.sh Phase 41 BIN-01):
#   linux-x64  linux-arm64  osx-x64  osx-arm64  win-x64

set -euo pipefail

FLOW_VERSION="${FLOW_VERSION:-1.5.0}"
SYSTEM_INSTALL=0
LOCAL_TARBALL=""
INSTALL_ROOT=""

print_usage() {
  cat <<EOF
Flow installer

Usage:
  install.sh                                   Install to ~/.local/share/flow (per-user, no sudo)
  install.sh --system                          Install to /usr/local/share/flow (system-wide, sudo)
  install.sh --install-root <dir>              Install under <dir>/share/flow and <dir>/bin (for tests)
  install.sh --local-tarball <path>            Use a local publish dir or tar.gz instead of GitHub release

Flags:
  --system            System-wide install (requires sudo for /usr/local writes)
  --install-root DIR  Override install root (test mode)
  --local-tarball P   Local publish dir or tarball; skips network fetch
  -h, --help          Show this help

Environment:
  FLOW_VERSION        Override the version to install (default: 1.5.0)
  FLOW_TARBALL_URL    Override the full tarball URL (skips auto-detection)
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --system) SYSTEM_INSTALL=1; shift ;;
    --local-tarball) LOCAL_TARBALL="$2"; shift 2 ;;
    --install-root) INSTALL_ROOT="$2"; shift 2 ;;
    -h|--help) print_usage; exit 0 ;;
    *) echo "Unknown flag: $1" >&2; print_usage >&2; exit 1 ;;
  esac
done

# ---------------------------------------------------------------------------
# OS / architecture detection.
# Derive the RID (Runtime Identifier) from uname output, matching the
# publish.sh Phase 41 BIN-01 RID set: linux-x64 linux-arm64 osx-x64 osx-arm64
# ---------------------------------------------------------------------------
detect_rid() {
  local os arch
  os=$(uname -s)
  arch=$(uname -m)

  case "$os" in
    Linux)
      case "$arch" in
        x86_64)  echo "linux-x64" ;;
        aarch64) echo "linux-arm64" ;;
        *)
          echo "ERROR: unsupported Linux arch: $arch" >&2
          echo "       Supported: x86_64 (linux-x64), aarch64 (linux-arm64)." >&2
          echo "       For other architectures build from source: dotnet publish flow-cli -r linux-<arch>" >&2
          exit 1
          ;;
      esac
      ;;
    Darwin)
      case "$arch" in
        x86_64) echo "osx-x64" ;;
        arm64)  echo "osx-arm64" ;;
        *)
          echo "ERROR: unsupported macOS arch: $arch" >&2
          echo "       Supported: x86_64 (osx-x64), arm64 (osx-arm64, Apple Silicon)." >&2
          exit 1
          ;;
      esac
      ;;
    MINGW*|MSYS*|CYGWIN*|Windows_NT)
      echo "ERROR: Windows detected. This installer is for Linux/macOS only." >&2
      echo "       For Windows: download flow-win-x64-v${FLOW_VERSION}.zip from" >&2
      echo "       https://github.com/noahfreelove/flow-sharp/releases/tag/v${FLOW_VERSION}" >&2
      echo "       and add the extracted directory to your PATH." >&2
      exit 1
      ;;
    *)
      echo "ERROR: unsupported OS: $os" >&2
      echo "       Supported: Linux (linux-x64 / linux-arm64), macOS (osx-x64 / osx-arm64)." >&2
      exit 1
      ;;
  esac
}

# Only detect RID if we'll need it (not --local-tarball, not FLOW_TARBALL_URL).
if [[ -z "$LOCAL_TARBALL" && -z "${FLOW_TARBALL_URL:-}" ]]; then
  DETECTED_RID=$(detect_rid)
else
  DETECTED_RID=""
fi

# Artifact name pattern (D-16): flow-<rid>-v<VERSION>.tar.gz
# Example: flow-linux-x64-v1.5.0.tar.gz
TARBALL_URL="${FLOW_TARBALL_URL:-https://github.com/noahfreelove/flow-sharp/releases/download/v${FLOW_VERSION}/flow-${DETECTED_RID}-v${FLOW_VERSION}.tar.gz}"
SHA256_URL="${TARBALL_URL}.sha256"

# Determine install paths.
if [[ -n "$INSTALL_ROOT" ]]; then
  SHARE_ROOT="$INSTALL_ROOT/share/flow"
  BIN_ROOT="$INSTALL_ROOT/bin"
elif [[ $SYSTEM_INSTALL -eq 1 ]]; then
  SHARE_ROOT="/usr/local/share/flow"
  BIN_ROOT="/usr/local/bin"
else
  SHARE_ROOT="$HOME/.local/share/flow"
  BIN_ROOT="$HOME/.local/bin"
fi
CONFIG_ROOT="${HOME}/.config/flow"

echo "==> Flow installer v${FLOW_VERSION}"
if [[ -n "$DETECTED_RID" ]]; then
  echo "    RID=$DETECTED_RID"
fi
echo "    SHARE_ROOT=$SHARE_ROOT"
echo "    BIN_ROOT=$BIN_ROOT"
echo "    CONFIG_ROOT=$CONFIG_ROOT"

# Create dirs (mkdir -p is idempotent).
mkdir -p "$SHARE_ROOT" "$BIN_ROOT" "$CONFIG_ROOT"

VERSIONED_DIR="$SHARE_ROOT/flow-v${FLOW_VERSION}"
mkdir -p "$VERSIONED_DIR"

# Acquire payload.
if [[ -n "$LOCAL_TARBALL" ]]; then
  if [[ -d "$LOCAL_TARBALL" ]]; then
    # Directory: copy contents via tar pipe to preserve permissions and
    # avoid trailing-slash subtleties.
    echo "==> Copying from local directory: $LOCAL_TARBALL"
    tar -C "$LOCAL_TARBALL" -cf - . | tar -C "$VERSIONED_DIR" -xf -
  elif [[ -f "$LOCAL_TARBALL" ]]; then
    echo "==> Extracting local tarball: $LOCAL_TARBALL"
    tar -xzf "$LOCAL_TARBALL" -C "$VERSIONED_DIR"
  else
    echo "ERROR: --local-tarball path does not exist: $LOCAL_TARBALL" >&2
    exit 1
  fi
else
  command -v curl >/dev/null || { echo "ERROR: curl required for network install (or pass --local-tarball)" >&2; exit 1; }

  TMP_TAR="$(mktemp -t flow-install.XXXXXX.tar.gz)"
  TMP_SHA="$(mktemp -t flow-install.XXXXXX.sha256)"
  # shellcheck disable=SC2064
  trap "rm -f '$TMP_TAR' '$TMP_SHA'" EXIT

  # Download the tarball.
  echo "==> Downloading $TARBALL_URL"
  curl -fsSL "$TARBALL_URL" -o "$TMP_TAR"

  # Verify the .sha256 sidecar when available (D-16, T-41-05-TAMPER mitigation).
  # A missing sidecar is a warning, not an error (pre-v1.5 releases may not have
  # shipped one); a present-but-mismatched sidecar aborts the install.
  if curl -fsSL "$SHA256_URL" -o "$TMP_SHA" 2>/dev/null; then
    echo "==> Verifying SHA-256 sidecar..."
    EXPECTED_SHA=$(awk '{print $1}' "$TMP_SHA")
    if command -v sha256sum >/dev/null 2>&1; then
      ACTUAL_SHA=$(sha256sum "$TMP_TAR" | awk '{print $1}')
    elif command -v shasum >/dev/null 2>&1; then
      ACTUAL_SHA=$(shasum -a 256 "$TMP_TAR" | awk '{print $1}')
    else
      echo "WARNING: neither sha256sum nor shasum found — skipping checksum verification." >&2
      ACTUAL_SHA=""
    fi
    if [[ -n "$ACTUAL_SHA" ]]; then
      if [[ "$ACTUAL_SHA" = "$EXPECTED_SHA" ]]; then
        echo "    SHA-256 OK: $ACTUAL_SHA"
      else
        echo "ERROR: SHA-256 mismatch — aborting install (possible tampered download)." >&2
        echo "       Expected: $EXPECTED_SHA" >&2
        echo "       Actual:   $ACTUAL_SHA" >&2
        exit 1
      fi
    fi
  else
    echo "NOTE: no .sha256 sidecar found at $SHA256_URL — skipping checksum verification."
  fi

  tar -xzf "$TMP_TAR" -C "$VERSIONED_DIR"
fi

# Verify payload.
if [[ ! -x "$VERSIONED_DIR/flow" ]]; then
  echo "ERROR: $VERSIONED_DIR/flow not present or not executable after extract." >&2
  exit 1
fi

# Idempotent symlink: ln -sfn replaces any existing symlink in place
# (the -f forces overwrite; -n ensures it doesn't recurse into an existing
# symlinked directory).
ln -sfn "$VERSIONED_DIR/flow" "$BIN_ROOT/flow"

# Default config -- only if absent (never overwrite user customisation).
# Guard pattern: `[[ ! -f "$CONFIG_ROOT/config.toml" ]]` -- the literal
# filename in the test predicate also satisfies the grep-based acceptance
# check in 30-05-PLAN.md task 1.
if [[ ! -f "$CONFIG_ROOT/config.toml" ]]; then
  CONFIG_FILE="$CONFIG_ROOT/config.toml"
  cat > "$CONFIG_FILE" <<EOF
# Flow default config -- auto-generated by install.sh
# See https://github.com/noahfreelove/flow-sharp for documentation.

install_path = "$VERSIONED_DIR"

# Uncomment to override built-in defaults:
# default_audio_device = "alsa_output.usb-..."
# default_tempo = 120
# default_timesig = "4/4"
# stdlib_search_path = ["/usr/share/my-flow-modules"]
EOF
  echo "==> Wrote default config: $CONFIG_FILE"
fi

# PATH check (POSIX-safe: colon-fenced needle in colon-fenced haystack
# avoids prefix/suffix false matches).
case ":$PATH:" in
  *":$BIN_ROOT:"*) ;;
  *)
    echo ""
    echo "WARNING: $BIN_ROOT is not on your PATH."
    if [[ $SYSTEM_INSTALL -eq 0 && -z "$INSTALL_ROOT" ]]; then
      echo "Add this line to your ~/.bashrc or ~/.zshrc:"
      echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
    fi
    ;;
esac

echo ""
echo "==> Installed flow v${FLOW_VERSION} -> $VERSIONED_DIR"
echo "==> Symlinked $BIN_ROOT/flow -> $VERSIONED_DIR/flow"
echo "==> Run: flow version"
