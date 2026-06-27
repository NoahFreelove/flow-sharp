#!/usr/bin/env bash
#
# sync-runtime.sh — (re)generate flow-site/static/wasm/ from the Phase 48 WASM AppBundle.
#
# Publishes flow-lang under FlowTarget=Web, then copies the resulting AppBundle into
# static/wasm/ PRESERVING the `flow-runtime.js`-at-root + `_framework/`-sibling layout
# (HANDOFF §1/§8 — flattening or renaming breaks the runtime's relative `./_framework/dotnet.js`
# import + `dotnet.boot.js` fetch, which was Phase 48's Plan 48-06 boot blocker).
#
# The committed static/wasm/ is the source of truth for CF Pages (the build container is
# pure-Node — no .NET SDK / wasm-tools — per RESEARCH Open Q2). Re-run this only to refresh
# the runtime after a flow-lang change, then commit the result.
#
# The dev-harness index.html is intentionally EXCLUDED — flowlang.dev builds its own UI.
#
# Prerequisites: .NET 10 SDK + `dotnet workload install wasm-tools`.
# Usage:  bash flow-site/scripts/sync-runtime.sh

set -euo pipefail

# Resolve repo root relative to this script (flow-site/scripts/ → repo root is two up).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

CSPROJ="${REPO_ROOT}/flow-lang/flow-lang.csproj"
APPBUNDLE="${REPO_ROOT}/flow-lang/bin/Release/net10.0/browser-wasm/AppBundle"
DEST="${REPO_ROOT}/flow-site/static/wasm"

echo "==> Publishing flow-lang for the Web target (FlowTarget=Web)"
dotnet publish "${CSPROJ}" -p:FlowTarget=Web -c Release

if [ ! -f "${APPBUNDLE}/flow-runtime.js" ]; then
  echo "ERROR: ${APPBUNDLE}/flow-runtime.js not found after publish." >&2
  exit 1
fi

echo "==> Syncing AppBundle -> ${DEST} (layout preserved; index.html excluded)"
rm -rf "${DEST}"
mkdir -p "${DEST}/_framework"

# flow-runtime.js + package.json at the wasm root (NOT index.html — that is the dev harness).
cp "${APPBUNDLE}/flow-runtime.js" "${DEST}/flow-runtime.js"
cp "${APPBUNDLE}/package.json"    "${DEST}/package.json"

# _framework/ sibling — the Mono-WASM loader + boot manifest + all .wasm assemblies.
cp -r "${APPBUNDLE}/_framework/." "${DEST}/_framework/"

echo "==> Done. static/wasm/ regenerated:"
echo "    $(ls "${DEST}" | tr '\n' ' ')"
echo "    _framework/: $(ls "${DEST}/_framework" | wc -l) files"
