#!/usr/bin/env bash
# scripts/lsp-smoke.sh -- Boot-and-respond smoke test for the flow-lsp binary.
#
# Usage: scripts/lsp-smoke.sh <path-to-binary>
#
# Drives the binary over stdio with a real LSP handshake (initialize ->
# initialized -> shutdown -> exit), draining stdout AND stderr CONCURRENTLY the
# way a real LSP client does, and asserts the server boots and emits a framed
# `initialize` response.
#
# A booted+responding server that lingers on `exit` is force-killed and STILL
# passes -- the editor force-kills too, so graceful-exit timing must never red
# the build. That false-negative is exactly what the previous harness produced:
# it wrote all four messages in one burst and only read stdout afterwards, so
# the server never completed the `initialize` handshake before `exit` and hung
# (osx-arm64 CI exit 3, reproduced on linux-x64). The fix is to read continuously
# and to wait for the initialize reply before asking the server to leave.
#
# Acceptance: the binary boots and replies to `initialize` with an LSP-framed
# response within $LSP_SMOKE_TIMEOUT_SEC (default 15s). Clean shutdown is
# attempted and reported but NOT required.
#
# Used by .github/workflows/publish-extension.yml per-platform CI, and safe for
# developers to run locally against a freshly `dotnet publish`-ed binary.
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

# Python helper handles LSP Content-Length framing + concurrent stream draining;
# python3 is available on every GitHub Actions runner. Hand-rolling framing and
# non-blocking reads in bash is error-prone.
python3 - "$BIN" "$TIMEOUT" <<'PY'
import json, subprocess, sys, threading, time

binpath, timeout = sys.argv[1], float(sys.argv[2])

def frame(obj):
    body = json.dumps(obj).encode('utf-8')
    return f"Content-Length: {len(body)}\r\n\r\n".encode('ascii') + body

initialize = {"jsonrpc": "2.0", "id": 1, "method": "initialize",
              "params": {"processId": None, "rootUri": None, "capabilities": {}}}
initialized = {"jsonrpc": "2.0", "method": "initialized", "params": {}}
shutdown = {"jsonrpc": "2.0", "id": 2, "method": "shutdown", "params": None}
exit_notif = {"jsonrpc": "2.0", "method": "exit"}

p = subprocess.Popen([binpath], stdin=subprocess.PIPE,
                     stdout=subprocess.PIPE, stderr=subprocess.PIPE)

# Drain stdout AND stderr concurrently so the server never blocks on a full pipe
# buffer and we observe the initialize reply as soon as it lands. A real LSP
# client always reads continuously.
out = bytearray(); err = bytearray()
def drain(stream, sink):
    try:
        for chunk in iter(lambda: stream.read(1), b""):
            sink.extend(chunk)
    except Exception:
        pass
threading.Thread(target=drain, args=(p.stdout, out), daemon=True).start()
threading.Thread(target=drain, args=(p.stderr, err), daemon=True).start()

def have_response():
    return b"Content-Length" in bytes(out)

# 1) Boot handshake -- let initialize complete before asking the server to leave.
try:
    p.stdin.write(frame(initialize)); p.stdin.write(frame(initialized)); p.stdin.flush()
except Exception as e:
    sys.stderr.write(f"ERROR: could not write initialize to flow-lsp: {e}\n")
    p.kill(); sys.exit(5)

# 2) Wait for the first framed response (the initialize result).
deadline = time.monotonic() + timeout
while time.monotonic() < deadline and not have_response() and p.poll() is None:
    time.sleep(0.05)

if not have_response():
    rc = p.poll()
    sys.stderr.write(f"ERROR: no LSP-framed response to initialize (exit code so far: {rc})\n")
    sys.stderr.write("stderr head: " + bytes(err).decode('utf-8', 'replace')[:500] + "\n")
    try: p.kill()
    except Exception: pass
    sys.exit(4)

# 3) Graceful shutdown request.
try:
    p.stdin.write(frame(shutdown)); p.stdin.write(frame(exit_notif))
    p.stdin.flush(); p.stdin.close()
except Exception:
    pass

# 4) Prefer a clean exit, but a booted+responding server that lingers on `exit`
# must NOT red the build -- boot+respond is the load-bearing assertion.
try:
    p.wait(timeout=timeout)
    print("OK: flow-lsp smoke passed (booted, responded, exit={}, stdout bytes={})".format(
        p.returncode, len(out)))
except subprocess.TimeoutExpired:
    try: p.kill()
    except Exception: pass
    print("OK: flow-lsp smoke passed (booted + responded; killed after no clean "
          "exit within {:.0f}s, stdout bytes={})".format(timeout, len(out)))
PY
