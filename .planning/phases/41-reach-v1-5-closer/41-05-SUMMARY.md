---
phase: 41-reach-v1-5-closer
plan: 05
subsystem: infra
tags: [dotnet-publish, single-file, self-contained, cross-compile, sha256, bin-01]

# Dependency graph
requires:
  - phase: 30-flow-cli-formal-install
    provides: single-RID linux-x64 self-contained publish.sh + flow-cli AssemblyName=flow
  - phase: 41-reach-v1-5-closer (41-03/41-04)
    provides: flow doc verb + showcase consumed by the same flow-cli entrypoint the binaries ship
provides:
  - "scripts/publish.sh produces self-contained single-file archives for all 5 RIDs (linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64)"
  - "flow-<rid>-v1.5.0.tar.gz (linux/osx) + flow-win-x64-v1.5.0.zip (windows) each with a .sha256 sidecar"
  - "linux-x64 runtime smoke (flow version); linux-arm64 best-effort qemu smoke; osx/win exec staged for 41-HUMAN-UAT.md"
affects: [v1.5.0 GitHub Release (D-04 human gate), 41-HUMAN-UAT.md rows 3-5/7]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "5-RID per-RID publish loop: managed-only binaries cross-compile from one Linux host (audio backends P/Invoke SYSTEM libs that are never bundled)"
    - "No-trim discipline (PublishTrimmed=false on every RID) for the reflection-heavy InternalFunctionRegistry"
    - ".sha256 sidecar per archive (tampered-binary mitigation; bare-filename form for `sha256sum -c`)"
    - "Best-effort cross-arch smoke: attempt qemu, downgrade to skip-with-reason, never fatal"

key-files:
  created: []
  modified:
    - scripts/publish.sh

key-decisions:
  - "Kept the exact Phase 30 flag set verbatim and only generalized the RID dimension — no flag drift (D-14)"
  - "PublishTrimmed=false on every RID (D-15); the only -p:PublishTrimmed flag invocation is =false"
  - "Per-RID 120 MB SPEC-2 size budget applied per-RID, not summed (each RID lands 38-39 MB)"
  - "linux-arm64 qemu smoke is best-effort: this x64 host lacks the aarch64 glibc loader, so it skips-with-reason rather than failing the script"
  - "osx-x64/osx-arm64/win-x64 binaries are cross-compiled + checksummed but NEVER executed here — exec smoke is the 41-HUMAN-UAT.md gate (Pitfall 4, D-05), not faked"

patterns-established:
  - "Per-RID publish loop with per-RID binary-name (flow vs flow.exe), stdlib-copy check, size budget, archive (tar.gz vs zip), and .sha256"
  - "Honest cross-OS boundary: build+checksum autonomously; execute only what this host can run; flag the rest"

requirements-completed: [BIN-01]

# Metrics
duration: 12min
completed: 2026-06-08
---

# Phase 41 Plan 05: Cross-Platform Binaries Summary

**`scripts/publish.sh` now produces self-contained single-file archives for all 5 RIDs (linux-x64/arm64, osx-x64/arm64, win-x64) as `flow-<rid>-v1.5.0.tar.gz`/`.zip` with a `.sha256` sidecar each, no trimming — linux-x64 runtime-smoked, osx/win execution honestly deferred to the human gate.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-06-08T00:55:00Z (approx)
- **Completed:** 2026-06-08
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Generalized the single-RID linux-x64 publish script to a 5-RID loop that cross-compiles all targets from this Linux host (managed-only binaries; audio backends P/Invoke system libs that are never bundled).
- Each RID produces a self-contained single-file binary (`flow` / `flow.exe`), packaged as `flow-<rid>-v1.5.0.tar.gz` (linux/osx) or `flow-win-x64-v1.5.0.zip` (windows), with a `.sha256` sidecar — tampered-binary mitigation for the v1.5.0 Release (D-16, T-41-05-TAMPER).
- Kept the exact Phase 30 flag set with `-p:PublishTrimmed=false` on every RID (D-15 — the reflection-heavy `InternalFunctionRegistry` would silently break under trimming).
- Preserved per-RID stdlib-copy verification + per-RID 120 MB SPEC-2 size budget (each RID lands 38-39 MB published / 33-35 MB archived).
- linux-x64 `flow version` smoke passes natively; linux-arm64 attempts qemu-aarch64 best-effort and skips-with-reason (non-fatal) when the host lacks the aarch64 sysroot; osx/win binaries are built + checksummed but never executed here.

## Task Commits

1. **Task 1: Generalize publish.sh to a 5-RID self-contained loop with tar/zip + .sha256** — `c1cff6f` (feat)

**Plan metadata:** (this commit — docs: complete plan)

## Files Created/Modified
- `scripts/publish.sh` — Rewritten from single-RID linux-x64 to a 5-RID loop: per-RID clean → `dotnet publish` single-file/self-contained/no-trim → binary-existence + stdlib-copy + per-RID size-budget checks → tar.gz/zip package → `.sha256` sidecar → linux smoke (native x64 + best-effort qemu arm64), osx/win exec deferred to HUMAN-UAT.

## Verification Results
- `bash scripts/publish.sh` exits **0**.
- 5 archives produced: `flow-linux-x64-v1.5.0.tar.gz`, `flow-linux-arm64-v1.5.0.tar.gz`, `flow-osx-x64-v1.5.0.tar.gz`, `flow-osx-arm64-v1.5.0.tar.gz`, `flow-win-x64-v1.5.0.zip`.
- 5 `.sha256` sidecars produced; all 5 verify `OK` via `sha256sum -c`.
- linux-x64 `flow version` smoke → `flow 0.1.0-phase30+0dcc24d…` (pass).
- No `-p:PublishTrimmed=true` flag anywhere; the single `-p:PublishTrimmed` flag invocation is `=false`.
- Publish output is gitignored (`/publish/` in `.gitignore`) and not tracked — only `scripts/publish.sh` committed.

**Published checksums (for the staged v1.5.0 Release, D-04 human gate):**
```
798b1245abca832b31ab7e20b98b76947fceabbea11dbd68bdb74270f5be9f58  flow-linux-arm64-v1.5.0.tar.gz
a1c468e593f2d1e801f48f75a5f3a160f3ecbf34c9f3fc6803e3941224f92604  flow-linux-x64-v1.5.0.tar.gz
04d1710e39e1834b4f78dd82a1b72f7309dc3ae7fdbc9dcd1c75df25035700db  flow-osx-arm64-v1.5.0.tar.gz
1e123f67733d6d7b70819a7c3ecd4295a630a46be79f5174068365593918511d  flow-osx-x64-v1.5.0.tar.gz
894cd1f06203d621fa61e343ae0dc637f6e3ccd73054ad413f70092209b9c32e  flow-win-x64-v1.5.0.zip
```
_Note: these hashes are reproducible per run but not pinned in VCS — the publish dir is gitignored. Re-run `bash scripts/publish.sh` to regenerate before cutting the Release._

## Decisions Made
- **Generalize only the RID dimension; keep the flag set verbatim (D-14).** The Phase 30 flag set (`--self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:DebugType=embedded -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`) is copied unchanged into the loop — no flag drift.
- **Best-effort, never-fatal linux-arm64 smoke.** qemu-aarch64 is binfmt-registered on this host, but qemu-user needs the aarch64 glibc loader (`/lib/ld-linux-aarch64.so.1`) which an x64 box does not ship — so the bare invocation fails. The script invokes the emulator explicitly and downgrades any failure to skip-with-reason (the artifact is still built + checksummed). The plan mandates "missing qemu must not fail the script"; a non-functional qemu is the same case.
- **Honest cross-OS boundary (Pitfall 4 / D-05).** osx/win binaries cross-compile cleanly but cannot be exercised on Linux; the script echoes that their exec smoke is the 41-HUMAN-UAT.md gate (rows 3-5) and never runs them.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] linux-arm64 smoke aborted the whole script when qemu could not emulate the binary**
- **Found during:** Task 1 (first full run of the rewritten script)
- **Issue:** My first arm64-smoke branch assumed a binfmt-registered qemu-aarch64 means the binary will run, so it used bare transparent dispatch (`"$ARM64_BIN" version`). On this x64 host qemu-user is registered but the aarch64 glibc loader is absent (`Could not open '/lib/ld-linux-aarch64.so.1'`), so the invocation exited non-zero and `set -euo pipefail` aborted the entire script — failing the "missing/unusable qemu must not fail the script" plan requirement. All 5 archives + checksums had already been produced before the abort.
- **Fix:** Rewrote the arm64-smoke block to invoke `qemu-aarch64` explicitly, wrap it in an `if … then` so failure is tolerated, and downgrade any failure (no qemu, missing loader, emulation crash) to a clear skip-with-reason message. The artifact stays built + checksummed; real arm64 exec is the 41-HUMAN-UAT.md gate / real arm64 hardware.
- **Files modified:** scripts/publish.sh
- **Verification:** Re-ran `bash scripts/publish.sh` → exit 0; arm64 row prints "exec smoke SKIPPED … Non-fatal"; all other criteria still green.
- **Committed in:** `c1cff6f` (Task 1 commit — the fix was applied before the first commit, so the committed script is the corrected version).

---

**Total deviations:** 1 auto-fixed (1 bug).
**Impact on plan:** The fix was necessary to satisfy the plan's explicit "do NOT fail the script for missing qemu" requirement and the broader honest-boundary contract. No scope creep — only the linux-arm64 smoke block's failure-handling changed; the 5-RID publish/package/checksum body is exactly as planned.

## Issues Encountered
- The `NU1701` warning ("Rug.Osc 1.2.5 restored using .NETFramework") appears on every publish. It is pre-existing (the OSC dependency is .NET Standard 2.0-targeted and the message is the standard cross-TFM-restore advisory) and does not affect the published binaries — out of scope for this plan, not fixed.

## Known Stubs
None. The script produces real, runnable, checksummed artifacts; linux-x64 is runtime-proven. The osx/win execution rows are honest pending human-gate items (already present in `41-HUMAN-UAT.md` rows 3-5), not stubs.

## User Setup Required
None for this plan's autonomous scope. Downstream human gates (already staged in `41-HUMAN-UAT.md`):
- **Rows 3-5:** execute the osx-x64 / osx-arm64 / win-x64 binaries on real hardware (verify `.sha256` → unpack → `flow version` + a render).
- **Row 7:** cut the v1.5.0 GitHub Release (verify every `.sha256`, attach the 5 archives). Do NOT cut it autonomously (D-04).

## Next Phase Readiness
- All 5 binary archives + checksums are produced on demand by `scripts/publish.sh`, ready to stage for the v1.5.0 Release.
- The honest osx/win exec + Release rows remain in `41-HUMAN-UAT.md` (pending) — the milestone close depends on the composer signing those off, not on further autonomous work here.

## Self-Check: PASSED

- `scripts/publish.sh` — FOUND
- `.planning/phases/41-reach-v1-5-closer/41-05-SUMMARY.md` — FOUND
- Commit `c1cff6f` — FOUND
- 5 archives + 5 `.sha256` sidecars produced; `bash scripts/publish.sh` exits 0; linux-x64 smoke green; no `-p:PublishTrimmed=true`.

---
*Phase: 41-reach-v1-5-closer*
*Completed: 2026-06-08*
