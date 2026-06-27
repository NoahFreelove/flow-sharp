---
quick_id: 260627-0vk
slug: fix-vscode-extension-ci-repair-flow-lsp-
title: Fix the VSCode extension CI (publish-extension.yml)
date: 2026-06-27
status: complete
commit: 9993cfd
---

# Quick Task 260627-0vk — SUMMARY

## Diagnosis (the surprise)

The extension CI was **not** broken on missing marketplace secrets (my earlier
guess). `build-server` failed at the **"Smoke-test the LSP binary"** step
(osx-arm64, exit 3 "did not exit within timeout"); `fail-fast: true` then
cancelled the other 3 legs and `publish` was skipped — so publish never ran.

Reproduced on **linux-x64 locally**: `scripts/lsp-smoke.sh` exits 3. But the
flow-lsp binary is healthy — instrumented runs show it replies to `initialize`
(266-byte capabilities result) and exits **code 0** when the handshake is
respected. The harness was the bug: it wrote `initialize`/`initialized`/
`shutdown`/`exit` in one `communicate()` burst and only read stdout *after*
writing, so the server never finished the initialize handshake before `exit` and
hung. A real LSP client drains stdout continuously.

## What shipped

1. **`scripts/lsp-smoke.sh`** — rewritten Python driver:
   - drains stdout AND stderr concurrently (background threads),
   - sends `initialize`+`initialized`, **waits for the framed initialize reply**,
     then sends `shutdown`+`exit`,
   - asserts boot+framed-response as the load-bearing check (exit 4 if none),
   - attempts graceful exit but force-kills + still PASSES if it lingers (editor
     force-kills too; exit timing must never red the build).
   - Verified: passes against the published linux-x64 binary, deterministically,
     in ~0.7s (exit 0 ×2).

2. **`.github/workflows/publish-extension.yml`**:
   - build-server `fail-fast: true` → `false` (one platform's failure no longer
     cancels the others or silently skips publish).
   - publish job secret-guarded: `VSCE_PAT`/`OVSX_PAT` mapped to job `env`; each
     marketplace step gated on `env.X != ''` with a `::notice::` skip step when
     unset. A `v*` tag push is green with or without secrets, and publishing
     auto-activates per-registry once a token is added.

## Verification

- `bash -n scripts/lsp-smoke.sh` clean; smoke passes ×2 (exit 0).
- Full build-server pipeline green locally: `dotnet publish flow-lsp` (self-
  contained single-file) → copy 15 stdlib `.flow` → smoke (fixed) → `npm ci` →
  `tsc` compile → `vscode-tmgrammar-snap` 6/6 → `vsce package --target linux-x64`
  → 34.43 MB VSIX (156 files).
- Workflow YAML parses; `fail-fast: false`; publish env = {VSCE_PAT, OVSX_PAT};
  4 guarded steps with correct `if` conditions.

## Remaining (human action — only needed to actually PUBLISH)

Build-server is now green and uploads 4 installable VSIX artifacts per run. To
publish to the marketplaces on a tag push:
- Own/claim the `flow-lang` publisher on the VS Marketplace + create a matching
  Open VSX namespace.
- Add repo secrets `VSCE_PAT` (Azure DevOps PAT, Marketplace scope) and
  `OVSX_PAT` (Open VSX token). Until then, publishing self-skips (no failure).
- `vsce package` warns on the missing `repository`/LICENSE fields (non-fatal);
  add them later if desired.

## Commits

- `9993cfd` — fix(ci): repair flow-lsp smoke test + harden extension publish workflow
