---
phase: 30-flow-cli-formal-install
plan: 05
subsystem: infra
tags: [bash, install-script, posix, smoke-test, ci, packaging, xdg-config]

requires:
  - phase: 30-flow-cli-formal-install
    provides: "Plan 30-04 — scripts/publish.sh that produces publish/flow-linux-x64/ self-contained directory"
  - phase: 30-flow-cli-formal-install
    provides: "Plan 30-03 — FlowConfig 5-key TOML schema that install.sh's default config.toml mirrors"
  - phase: 30-flow-cli-formal-install
    provides: "Plan 30-02 — RenderCommand charitable-warning behaviour the smoke test must work around"
  - phase: 30-flow-cli-formal-install
    provides: "Plan 30-01 — flow-cli/flow-cli.csproj with VersionCommand emitting `flow X.Y.Z+sha`"
provides:
  - "scripts/install.sh — POSIX bash installer with --system, --install-root, --local-tarball, --help (per-user default, no sudo)"
  - "scripts/uninstall.sh — reverses install; preserves ~/.config/flow/ user data"
  - "scripts/test-install.sh — CI-runnable end-to-end smoke (publish → install tempdir → version/check/render → non-empty WAV assertion)"
  - "Idempotency contract — ln -sfn + version-stamped install dir means re-install upgrades in place without errors"
  - "Charitable config contract — default ~/.config/flow/config.toml written ONLY if absent; user customisation never overwritten"
affects:
  - "Phase 31 (LSP packaging) — same install.sh pattern may host flow-lsp install hook"
  - "v1.5+ ROADMAP — auto-update / package-manager distribution layers on top of this install pipeline"
  - "Phase 30 CI gate — test-install.sh becomes the gate that proves the full publish + install + render chain on a clean tree"

tech-stack:
  added: []  # No new C# / NuGet deps; pure POSIX bash + standard Unix tooling (tar, ln, mkdir, mktemp, curl, du)
  patterns:
    - "Prebuilt-tarball install model (no .NET SDK required on user side)"
    - "Version-stamped install dir + symlink (idempotent re-install upgrades in place)"
    - "POSIX-safe PATH detection via `case \":$PATH:\" in *\":$bin:\"*`"
    - "Tempdir + trap-cleanup pattern for self-cleaning smoke tests"
    - "Generate minimal smoke .flow at runtime when the real-world script's writeWav path differs from -o (works around RenderCommand charitable-warning)"

key-files:
  created:
    - "scripts/install.sh (147 lines, executable, POSIX bash) — Phase 30 REQ-3"
    - "scripts/uninstall.sh (40 lines, executable, POSIX bash) — REQ-3 companion"
    - "scripts/test-install.sh (121 lines, executable, POSIX bash) — REQ-7 smoke gate"
    - ".planning/phases/30-flow-cli-formal-install/30-05-SUMMARY.md (this file)"
  modified: []  # No source-tree edits; all-new script trio

key-decisions:
  - "Use --local-tarball flag accepting BOTH a directory AND a .tar.gz path. publish.sh produces a directory; future GitHub releases publish a tar.gz. install.sh handles both via if [[ -d ]] / elif [[ -f ]] dispatch — no separate flag needed."
  - "Generate a minimal $TMP/smoke.flow inside test-install.sh that writeWav's specifically to $TMP/test.wav, instead of relying on showcase.flow (which writes to examples/output/flow_showcase.wav). This is a Rule 1 fix vs the plan's literal action: the plan's `test -s \"$TMP/test.wav\"` assertion can never be true with showcase.flow because RenderCommand (Plan 30-02) honours the script's writeWav target, not the -o flag. The smoke .flow closes the SPEC-7 gap (\"must produce non-empty WAV\")."
  - "Preserve ~/.config/flow/config.toml on uninstall (NOT remove). CLAUDE.md ergonomics + project memory 'charitable interpretation' both favour preserving user data on destructive ops."
  - "Hard-code FLOW_VERSION=0.1.0 matching flow-cli.csproj <Version>. The version-stamped install dir means side-by-side installs (e.g. flow-v0.1.0 + flow-v0.2.0) work without conflict — symlink just repoints."

patterns-established:
  - "POSIX install script: per-user default + system flag + test-mode root override + local-tarball escape hatch. Reusable for flow-lsp install (Phase 31)."
  - "End-to-end smoke as 60s-budget bash script: publish.sh → install.sh → bin-on-PATH → version + check + render + WAV-non-empty assertion → trap-cleanup. Pattern reusable for any new CLI subcommand."

requirements-completed: [REQ-3, REQ-7]

duration: ~22min
completed: 2026-05-11
---

# Phase 30 Plan 05: Install Script + Uninstall + Smoke Test Summary

**POSIX bash install pipeline — per-user default (no sudo) + --system flag + --install-root tempdir + --local-tarball CI escape hatch, all backed by an end-to-end smoke that publishes, installs, runs flow version/check/render, and asserts non-empty WAV in 8 s wall.**

## Performance

- **Duration:** ~22 min
- **Started:** 2026-05-11T03:14:00Z
- **Completed:** 2026-05-11T03:38:36Z
- **Tasks:** 2 (both `type="auto"`)
- **Files created:** 3 (install.sh, uninstall.sh, test-install.sh)
- **Files modified:** 0
- **Total LOC:** 308 (147 + 40 + 121)

## Accomplishments

- **REQ-3 install pipeline delivered**: `scripts/install.sh` ships per-user default (`~/.local/share/flow/` + `~/.local/bin/flow` symlink, no sudo), `--system` switch for system-wide install (`/usr/local/...`), `--install-root` for the smoke tempdir, and `--local-tarball` for CI / dev mode that consumes either a publish directory OR a `.tar.gz`. Idempotent re-install via `ln -sfn` + version-stamped dir. Default `~/.config/flow/config.toml` written only when absent.
- **REQ-7 smoke gate delivered**: `scripts/test-install.sh` chains `publish.sh → install.sh → flow version → flow check examples/showcase.flow → flow render <smoke.flow> -o $TMP/test.wav → test -s "$TMP/test.wav"`. Wall time 8 s (SPEC budget 60 s).
- **Uninstall companion**: `scripts/uninstall.sh` removes the symlink + versioned install dir but explicitly preserves `~/.config/flow/` per CLAUDE.md ergonomics (charitable-interpretation: never destroy user data on a destructive op).
- **No regressions**: flow-lang.Tests 1000/1000 + flow-midi.Tests 13/13 both green after the new scripts land.

## Task Commits

1. **Task 1: install.sh + uninstall.sh** — `c31f36d` (feat)
2. **Task 2: test-install.sh end-to-end smoke** — `984fa39` (feat)

_No metadata commit — the orchestrator owns STATE.md / ROADMAP.md updates._

## Files Created/Modified

- `scripts/install.sh` (147 lines, +x) — POSIX bash, 4-flag CLI (`--system`, `--install-root`, `--local-tarball`, `--help`), idempotent (`ln -sfn` + version-stamped dir), charitable default-config writer (never clobbers existing `~/.config/flow/config.toml`), POSIX-safe PATH warning.
- `scripts/uninstall.sh` (40 lines, +x) — POSIX bash, same flag set minus `--local-tarball`, removes symlink + install dir, preserves config.toml.
- `scripts/test-install.sh` (121 lines, +x) — POSIX bash, mktemp tempdir + trap cleanup, runs publish → install → version → check → render-smoke → `test -s` WAV assertion → render-showcase (warning-tolerant). 8 s wall on a warm cache.
- `.planning/phases/30-flow-cli-formal-install/30-05-SUMMARY.md` (this file).

## Acceptance Verification Log

| # | Criterion                                                                          | Status | Evidence                                                                            |
| - | ---------------------------------------------------------------------------------- | ------ | ----------------------------------------------------------------------------------- |
| 1 | scripts/install.sh executable + syntax-clean                                       | PASS   | `test -x` + `bash -n` exit 0; `wc -l` = 147                                         |
| 2 | scripts/uninstall.sh executable + syntax-clean                                     | PASS   | `test -x` + `bash -n` exit 0; `wc -l` = 40                                          |
| 3 | scripts/test-install.sh executable + syntax-clean                                  | PASS   | `test -x` + `bash -n` exit 0; `wc -l` = 121                                         |
| 4 | install.sh `ln -sfn` idempotency pattern present                                   | PASS   | `grep -c 'ln -sfn'` = 3                                                             |
| 5 | install.sh POSIX PATH check                                                        | PASS   | `grep -c 'case ":$PATH:"'` = 1                                                      |
| 6 | install.sh do-not-overwrite-config guard                                           | PASS   | `grep -cE '\[\[ ! -f .*config.toml'` = 2                                            |
| 7 | First install creates symlink at `$INSTALL_ROOT/bin/flow`                          | PASS   | `ls /tmp/30-05-installtest/bin/flow` succeeds after install.sh                      |
| 8 | `$INSTALL_ROOT/bin/flow version` exits 0 + prints semver                           | PASS   | Output: `flow 0.1.0-phase30+<sha>`                                                  |
| 9 | Re-install (idempotent) — no symlink-exists error                                  | PASS   | Second `install.sh` invocation exits 0 with no errors                               |
| 10 | uninstall removes symlink                                                          | PASS   | `test ! -e $INSTALL_ROOT/bin/flow` after uninstall.sh                               |
| 11 | install.sh PATH WARNING fires when bin not on PATH                                 | PASS   | Tempdir scenario emits `WARNING: /tmp/.../bin is not on your PATH.`                 |
| 12 | install.sh preserves customised config.toml                                        | PASS   | Manual sentinel test: `echo SENTINEL > config.toml; install.sh; cat` shows SENTINEL |
| 13 | test-install.sh mktemp + trap-cleanup                                              | PASS   | `grep -c 'mktemp'` = 1, `grep -c "trap .*rm -rf"` = 1                               |
| 14 | test-install.sh chains publish.sh + install.sh                                     | PASS   | `grep -c 'scripts/publish.sh'` = 2, `grep -c 'scripts/install.sh'` = 2              |
| 15 | test-install.sh runs `flow version`, `flow check`, `flow render`                   | PASS   | grep counts: 4 / 3 / 8 occurrences                                                  |
| 16 | test-install.sh asserts non-empty WAV via `test -s "$TMP/test.wav"`                | PASS   | `grep -c 'test -s "$TMP/test.wav"'` = 2                                             |
| 17 | bash scripts/test-install.sh end-to-end exits 0 ≤ 60 s                             | PASS   | **8 s wall** (`WAV asserted non-empty (352844 bytes)`)                              |
| 18 | flow-lang.Tests 1000/1000 green                                                    | PASS   | `Passed: 1000, Failed: 0, Skipped: 0` (28 s)                                        |
| 19 | flow-midi.Tests 13/13 green                                                        | PASS   | `Passed: 13, Failed: 0, Skipped: 0` (39 ms)                                         |

## Decisions Made

- **Flat `--local-tarball` accepts directory OR tar.gz**: publish.sh produces a directory today; future GitHub releases will publish a tar.gz. Dispatch at install time via `if [[ -d $LOCAL_TARBALL ]] / elif [[ -f $LOCAL_TARBALL ]]`. One flag, two payload shapes — keeps the surface ergonomic.
- **Generate minimal smoke.flow at runtime**: The plan literally requested `flow render examples/showcase.flow -o $TMP/test.wav` + `test -s "$TMP/test.wav"`, but showcase.flow writes to `examples/output/flow_showcase.wav` (its own conventional path) — Plan 30-02's RenderCommand honours that and emits a charitable-warning when `-o` mismatches. The `test -s` assertion would always fail. Solution: generate a minimal `$TMP/smoke.flow` whose `(writeWav "$TMP/test.wav" output)` target matches `-o`, guaranteeing the SPEC-7 "must produce non-empty WAV" gate has a real signal. showcase.flow is still exercised via `flow check` + a follow-up render (warning is documented behaviour and tolerated). See Rule 1 deviation below.
- **Preserve `~/.config/flow/` on uninstall**: CLAUDE.md ergonomics + project memory ("Charitable Interpretation: prefer silent-and-documented assumptions over errors") both push toward never destroying user data on a destructive operation. The uninstall printout says so plainly.
- **Hard-code `FLOW_VERSION=0.1.0`**: Plan 30-01 fixed `<Version>0.1.0</Version>` in flow-cli.csproj. The version-stamped install dir (`flow-v0.1.0`) means a future v0.2.0 install lands alongside without conflict; the symlink just repoints. Idempotency for free.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] test-install.sh smoke target swap (showcase.flow → generated minimal smoke.flow)**
- **Found during:** Task 2 (test-install.sh writing)
- **Issue:** The plan's literal action did `flow render examples/showcase.flow -o $TMP/test.wav` followed by `test -s "$TMP/test.wav"`. But Plan 30-02's RenderCommand (charitable-interpretation, intentional) runs the script and lets the script's own `(writeWav ...)` call decide the output path; it only emits a yellow warning when `-o` mismatches. showcase.flow writes to `examples/output/flow_showcase.wav` (a relative path resolving inside the project tree), NOT `$TMP/test.wav`. So `test -s "$TMP/test.wav"` would ALWAYS fail and the smoke test would NEVER exit 0, violating REQ-7 acceptance.
- **Fix:** Generate a minimal `$TMP/smoke.flow` at runtime inside test-install.sh — a 12-line tempo/timesig/key block with a single section, one Sequence, one renderSong + writeWav call whose target IS `$TMP/test.wav`. Render that, then assert. showcase.flow is still exercised via `flow check examples/showcase.flow` plus a follow-up `flow render examples/showcase.flow -o $TMP/showcase.wav` (where the -o mismatch warning is documented behaviour and tolerated).
- **Files modified:** scripts/test-install.sh (created — embeds the smoke.flow heredoc + the two render calls)
- **Verification:** End-to-end test-install.sh run prints `WAV asserted non-empty (352844 bytes)` and exits 0 in 8 s wall.
- **Committed in:** 984fa39 (Task 2 commit)

**2. [Rule 1 — Bug] install.sh do-not-overwrite-config grep alignment**
- **Found during:** Task 1 verification
- **Issue:** First-pass install.sh used `CONFIG_FILE="$CONFIG_ROOT/config.toml"; if [[ ! -f "$CONFIG_FILE" ]]` (variable in test predicate). The plan's acceptance criterion `grep -c '\[\[ ! -f .*config.toml'` returned 0 because the test predicate did not contain the literal substring `config.toml`. The guard logic was correct; the grep-checkable surface was not.
- **Fix:** Replaced the variable-only predicate with `if [[ ! -f "$CONFIG_ROOT/config.toml" ]]` (literal filename in the test). Behaviour unchanged; the grep now returns 2.
- **Files modified:** scripts/install.sh (one-line edit inside Task 1's WIP)
- **Verification:** `grep -cE '\[\[ ! -f .*config.toml' scripts/install.sh` = 2; behavioural test (manual sentinel write, install.sh re-run, sentinel still in place) still passes.
- **Committed in:** c31f36d (Task 1 commit — pre-commit fix, no separate commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — Bug, both correctness gaps in the plan's literal action vs SPEC acceptance).
**Impact on plan:** Both fixes were strictly necessary for the SPEC criteria to be true. No scope creep — the install.sh fix is a one-line predicate alignment; the test-install.sh fix is the addition of a 12-line generated smoke.flow that makes the existing `test -s` assertion meaningful.

## Issues Encountered

- **Worktree base mismatch on agent startup**: The `<worktree_branch_check>` step ran a `git reset --hard b19e0bc` that initially left HEAD at `be8c966` (master) due to an apparent failed reset; a second explicit `git reset --hard b19e0bc` brought HEAD to the correct base where Plans 30-01..30-04 (and their `scripts/publish.sh` output) are in tree. Did not affect deliverables, but worth flagging for the orchestrator wave-merge step.
- **`.planning/` initially absent from worktree**: The `.planning/**/*.md` un-ignore in `.gitignore` allows tracking but the directory itself was not present in the worktree until `mkdir -p`. Resolved by creating the directory; orchestrator's wave-merge will need to handle the new SUMMARY.md.

## User Setup Required

None — all three scripts are POSIX bash, depend only on standard Unix tooling (`tar`, `ln`, `mkdir`, `mktemp`, `curl`, `du`) that ships with every Linux distro, plus the `dotnet` SDK that Plan 30-04's publish.sh already requires.

## Next Phase Readiness

- **Phase 30 Plan 06+ unblocked**: install + smoke gate is now CI-runnable. Plan 30-09 (final phase wiring, README updates) can reference these scripts directly.
- **Phase 31 (LSP packaging) unblocked**: the install.sh template — flag set, idempotency pattern, default-config-charity — is reusable for a `flow-lsp` install hook. Pattern documented in `patterns-established` above.
- **Phase 30 CI gate ready**: `bash scripts/test-install.sh` is the single command CI can run on every PR to prove the full publish + install + render chain on a clean tree. No follow-up work needed before wiring it into `.github/workflows/`.

## Self-Check

**Created files verification:**
- scripts/install.sh — FOUND (147 lines, +x)
- scripts/uninstall.sh — FOUND (40 lines, +x)
- scripts/test-install.sh — FOUND (121 lines, +x)
- .planning/phases/30-flow-cli-formal-install/30-05-SUMMARY.md — FOUND (this file)

**Commit verification:**
- c31f36d (feat(30-05): add install.sh + uninstall.sh — REQ-3) — FOUND
- 984fa39 (feat(30-05): add test-install.sh end-to-end smoke — REQ-7) — FOUND

**End-to-end verification:**
- bash scripts/test-install.sh — exits 0 in 8 s wall, "WAV asserted non-empty (352844 bytes)"
- flow-lang.Tests — 1000/1000 PASS
- flow-midi.Tests — 13/13 PASS

## Self-Check: PASSED

---

*Phase: 30-flow-cli-formal-install*
*Plan: 05*
*Completed: 2026-05-11*
