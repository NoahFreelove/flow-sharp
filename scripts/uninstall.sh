#!/usr/bin/env bash
# scripts/uninstall.sh -- Phase 30 REQ-3 companion: reverses install.sh.
#
# Removes the symlink + versioned install dir. Deliberately preserves
# ~/.config/flow/config.toml because the user may have customised it
# (CLAUDE.md ergonomics: never destroy user data on uninstall).

set -euo pipefail

FLOW_VERSION="${FLOW_VERSION:-0.1.0}"
SYSTEM_INSTALL=0
INSTALL_ROOT=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --system) SYSTEM_INSTALL=1; shift ;;
    --install-root) INSTALL_ROOT="$2"; shift 2 ;;
    -h|--help) echo "Usage: uninstall.sh [--system] [--install-root DIR]"; exit 0 ;;
    *) echo "Unknown flag: $1" >&2; exit 1 ;;
  esac
done

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

echo "==> Removing $BIN_ROOT/flow symlink"
rm -f "$BIN_ROOT/flow"

echo "==> Removing $SHARE_ROOT (versioned install dirs)"
rm -rf "$SHARE_ROOT"

echo "==> Done. Note: $HOME/.config/flow/ preserved (user data)."
