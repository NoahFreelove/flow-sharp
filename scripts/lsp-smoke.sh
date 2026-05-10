#!/usr/bin/env bash
# scripts/lsp-smoke.sh -- Boot-and-shutdown smoke test for flow-lsp binary.
#
# Usage: scripts/lsp-smoke.sh <path-to-binary>
#
# Sends framed LSP initialize + shutdown + exit messages to the binary over
# stdio and asserts it responds and exits cleanly within a timeout.
#
# Acceptance: completes within $LSP_SMOKE_TIMEOUT_SEC (default 15s) and exits 0
# when the binary emits at least one LSP-framed response on stdout and returns
# a sensible exit code (0 or 1).
#
# Used by .github/workflows/publish-extension.yml per-platform CI, and safe
# for developers to run locally against a freshly `dotnet publish`-ed binary.
set -euo pipefail

BIN="${1:?Usage: lsp-smoke.sh <binary>}"
TIMEOUT="${LSP_SMOKE_TIMEOUT_SEC:-15}"

if [[ ! -e "$BIN" ]]; then
  echo "ERROR: binary not found: $BIN" >&2
  exit 2
fi

# Ensure executable bit on POSIX (VSIX/archive extraction can strip it).
case "$(uname -s)" in
  Linux*|Darwin*) chmod +x "$BIN" 2>/dev/null || true ;;
esac

# Python helper handles LSP Content-Length framing; python3 is available on
# every GitHub Actions runner (Linux/macOS/Windows Git Bash via setup-python
# or pre-installed). Hand-rolling framing in bash is error-prone.
python3 - "$BIN" "$TIMEOUT" <<'PY'
import json, subprocess, sys

binpath, timeout = sys.argv[1], float(sys.argv[2])

def frame(obj):
    body = json.dumps(obj).encode('utf-8')
    return f"Content-Length: {len(body)}\r\n\r\n".encode('ascii') + body

initialize = {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
        "processId": None,
        "rootUri": None,
        "capabilities": {},
    },
}
initialized = {"jsonrpc": "2.0", "method": "initialized", "params": {}}
shutdown = {"jsonrpc": "2.0", "id": 2, "method": "shutdown", "params": None}
exit_notif = {"jsonrpc": "2.0", "method": "exit"}

msg = frame(initialize) + frame(initialized) + frame(shutdown) + frame(exit_notif)

p = subprocess.Popen(
    [binpath],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
)
try:
    out, err = p.communicate(input=msg, timeout=timeout)
except subprocess.TimeoutExpired:
    p.kill()
    try:
        _, err = p.communicate(timeout=2)
    except Exception:
        err = b""
    sys.stderr.write("ERROR: flow-lsp binary did not exit within timeout\n")
    sys.stderr.write("stderr:\n" + err.decode('utf-8', 'replace') + "\n")
    sys.exit(3)

# Accept exit 0 or 1. Some LSP servers return non-zero on shutdown when
# handlers were not fully registered; the point of this smoke test is that
# the binary boots, reads messages, does not crash or hang.
if p.returncode not in (0, 1):
    sys.stderr.write(f"ERROR: flow-lsp exited with code {p.returncode}\n")
    sys.stderr.write("stderr: " + err.decode('utf-8', 'replace') + "\n")
    sys.exit(p.returncode)

if b"Content-Length" not in out:
    sys.stderr.write("ERROR: no LSP-framed response on stdout\n")
    sys.stderr.write("stdout head: " + out.decode('utf-8', 'replace')[:500] + "\n")
    sys.stderr.write("stderr head: " + err.decode('utf-8', 'replace')[:500] + "\n")
    sys.exit(4)

print("OK: flow-lsp smoke test passed (exit={}, stdout bytes={})".format(
    p.returncode, len(out)))
PY
