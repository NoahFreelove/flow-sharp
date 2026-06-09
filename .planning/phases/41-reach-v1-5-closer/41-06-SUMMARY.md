---
phase: 41-reach-v1-5-closer
plan: 06
subsystem: infra
tags: [jetbrains, intellij-platform-gradle-plugin, lsp4ij, plugin-signing, marketplace, gradle, kotlin-dsl, packaging]

# Dependency graph
requires:
  - phase: 31-lsp-enhancements-jetbrains-stretch
    provides: "flow-jetbrains/ Gradle scaffold (IntelliJ Platform plugin 2.2.0, LSP4IJ 0.19.3, JDK 21 toolchain), plugin.xml LSP4IJ server extension, FlowLanguageServerFactory"
  - phase: 41-reach-v1-5-closer
    provides: "41-01 threat register (T-41-06-IDISCLOSE env-var-only secret rule), 41-HUMAN-UAT.md row 6 (D-03 publish gate)"
provides:
  - "build.gradle.kts signing/publishing/pluginVerification DSL (env-var-only secrets, no committed literals)"
  - "plugin.xml v1.5.0 metadata + <change-notes> CDATA"
  - "flow-jetbrains/CHANGELOG.md (Keep-a-Changelog [1.5.0])"
  - "docs/jetbrains/install.html direct-download fallback page"
  - "verifier-valid plugin (until-build=253.* fix) — buildPlugin green, verifyPlugin Compatible vs IC-2024.2"
affects: [jetbrains-marketplace-publish, v1.5-release, JET-01]

# Tech tracking
tech-stack:
  added: []  # no new packages — IntelliJ Platform plugin 2.2.0 + LSP4IJ 0.19.3 already pinned by Phase 31
  patterns:
    - "Marketplace secrets via providers.environmentVariable(...) ONLY — never committed (T-41-06-IDISCLOSE)"
    - "Autonomous build+verify / human sign+publish split (D-03): buildPlugin + verifyPlugin run here; signPlugin + publishPlugin are the composer's env-var-authenticated action"
    - "Verifier-valid until-build wildcard (253.*) instead of an empty attribute"

key-files:
  created:
    - "flow-jetbrains/CHANGELOG.md"
    - "docs/jetbrains/install.html"
  modified:
    - "flow-jetbrains/build.gradle.kts"
    - "flow-jetbrains/src/main/resources/META-INF/plugin.xml"
    - ".gitignore"
    - ".planning/phases/41-reach-v1-5-closer/41-HUMAN-UAT.md"

key-decisions:
  - "until-build fixed from provider { \"\" } (empty, verifier-rejected) to wildcard \"253.*\" — plugin 2.2.0 has no untilBuild.unset()"
  - "Committed verifier list stays recommended() for the composer's build env; verified locally against the explicit downloadable IC-2024.2 baseline"
  - "Provisioned a portable Temurin JDK 21 in $HOME (no root) so buildPlugin/verifyPlugin ran genuinely — not charitably deferred"

patterns-established:
  - "Pattern 1: JetBrains publish secrets are env-var-only; zero secret literals in any committed file"
  - "Pattern 2: buildPlugin is the hard autonomous gate; verifyPlugin compatibility output is captured (recommended()-vs-baseline) and informs the human publish decision"

requirements-completed: [JET-01]

# Metrics
duration: 10min
completed: 2026-06-08
---

# Phase 41 Plan 06: JetBrains Marketplace Publish Staging Summary

**Staged every JetBrains Marketplace publish artifact from the Phase 31 scaffolding — env-var-only signing/publishing/pluginVerification DSL, v1.5.0 plugin metadata + change-notes, CHANGELOG.md, and a direct-download install.html — with `buildPlugin` BUILD SUCCESSFUL and the plugin verified Compatible against IC-2024.2 (fixing a Phase-31 empty-`until-build` defect); sign + publish left as the D-03 human gate, never faked.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-06-08T00:11:26Z
- **Completed:** 2026-06-08T00:21:10Z
- **Tasks:** 2
- **Files modified:** 6 (2 created, 4 modified)

## Accomplishments

- `build.gradle.kts` gained the IntelliJ Platform 2.x `signing` / `publishing` / `pluginVerification` blocks. All four secrets — `CERTIFICATE_CHAIN`, `PRIVATE_KEY`, `PRIVATE_KEY_PASSWORD`, `PUBLISH_TOKEN` — are read via `providers.environmentVariable(...)` ONLY; a literal-scan confirms zero secret values committed (T-41-06-IDISCLOSE / V14 / V6).
- Plugin version bumped `0.1.0 → 1.5.0` in both `build.gradle.kts` and `plugin.xml`; added a `<change-notes>` CDATA block. `since-build="242"` and the LSP4IJ `<server id="flow">` extension are unchanged.
- `flow-jetbrains/CHANGELOG.md` (Keep-a-Changelog, `## [1.5.0]`) and `docs/jetbrains/install.html` (self-contained static HTML direct-download fallback, no framework) created.
- **`./gradlew buildPlugin` → BUILD SUCCESSFUL** under a provisioned Temurin JDK 21 — `flow-jetbrains-1.5.0.zip` (1.61 MB) produced under `build/distributions/`, carrying the v1.5.0 `plugin.xml` + change-notes.
- **`./gradlew verifyPlugin` → Compatible** against the IntelliJ IDEA Community 2024.2 baseline (`IC-242.20224.300 against dev.flowlang.jetbrains:1.5.0: Compatible`). This caught and fixed a real Phase-31 defect (empty `until-build`).
- `41-HUMAN-UAT.md` row 6 annotated: artifacts staged + build/verify green; the Marketplace upload (`signPlugin`/`publishPlugin`) remains the composer's pending action.

## Task Commits

1. **Task 1: Signing/publishing/verification DSL + v1.5.0 metadata + CHANGELOG** — `4910f8f` (feat)
2. **Task 2: install.html fallback + buildPlugin/verifyPlugin + until-build Rule-1 fix** — `7678554` (feat)

**Plan metadata:** (final docs commit — this SUMMARY + STATE + ROADMAP + HUMAN-UAT)

## Files Created/Modified

- `flow-jetbrains/build.gradle.kts` — version `1.5.0`; added `signing`/`publishing`/`pluginVerification` (env-var-only); `until-build` `provider { "" }` → `"253.*"`.
- `flow-jetbrains/src/main/resources/META-INF/plugin.xml` — version `1.5.0`; added `<change-notes>` CDATA; `since-build="242"` + LSP4IJ server untouched.
- `flow-jetbrains/CHANGELOG.md` — **created.** Keep-a-Changelog `## [1.5.0]` initial Marketplace release entry.
- `docs/jetbrains/install.html` — **created.** Static direct-download fallback (GitHub Release link, Install-from-Disk steps, `flow`/`FLOW_LSP_PATH` prerequisite, troubleshooting).
- `.gitignore` — allow-listed `flow-jetbrains/CHANGELOG.md` past the global `*.md` ignore (mirrors the `README.md` / `docs/**/*.md` precedents).
- `.planning/phases/41-reach-v1-5-closer/41-HUMAN-UAT.md` — row 6 annotated with the autonomous build/verify results; status stays **pending** (publish = composer).

## Decisions Made

- **`until-build` = `"253.*"` (not empty, not unset).** The IntelliJ Plugin Verifier rejects `until-build=""`. Plugin 2.2.0 exposes no `untilBuild.unset()` (it is an `Unresolved reference` — verified), so a concrete wildcard is the verifier-valid form. `253.*` matches the Phase-31 note that LSP4IJ 0.19.3 is API-stable across IntelliJ Platform 242..253.
- **Committed verifier list kept as `recommended()`.** That is the canonical, future-proof selector for the composer's build env. Local verification used a transient (uncommitted) `ide("IC", "2024.2")` override purely to obtain a real compatibility signal against a currently-downloadable IDE — then restored `recommended()`.
- **Provisioned a portable JDK 21 instead of deferring.** The plan allowed a charitable toolchain-deferral, but a genuine build was achievable: a non-root Temurin JDK 21 was downloaded to `$HOME/.local/jdks/jdk21`, so `buildPlugin` + `verifyPlugin` ran for real.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `until-build=""` (empty) is rejected by the IntelliJ Plugin Verifier**
- **Found during:** Task 2 (running `./gradlew verifyPlugin`)
- **Issue:** The Phase-31 `untilBuild = provider { "" }` emits an *empty* `until-build=""` attribute. The verifier rejects it: *"The `<until-build>` attribute with only a branch number () is not valid… include a wildcard, for example '.\*'."* A plugin with this defect would be rejected at Marketplace upload — directly defeating JET-01.
- **Fix:** Replaced with the verifier-valid wildcard `untilBuild = "253.*"` (`untilBuild.unset()` is unavailable in plugin 2.2.0). Rebuilt; the packaged `plugin.xml` now emits `until-build="253.*"`; `verifyPlugin` against IC-2024.2 reports **Compatible**.
- **Files modified:** `flow-jetbrains/build.gradle.kts`
- **Verification:** `./gradlew verifyPlugin` (transient IC-2024.2 pin) → `Compatible`, BUILD SUCCESSFUL.
- **Committed in:** `7678554` (Task 2 commit)

**2. [Rule 3 - Blocking] `flow-jetbrains/CHANGELOG.md` blocked by the global `*.md` .gitignore**
- **Found during:** Task 1 (staging the new CHANGELOG)
- **Issue:** The repo `.gitignore` line 11 globally ignores `*.md` with a per-path allow-list; the new CHANGELOG was silently un-stageable (`git add` warned "ignored"). The committed artifact would have been missing.
- **Fix:** Added `!flow-jetbrains/CHANGELOG.md` to the allow-list (same idiom as the existing `README.md` / `docs/**/*.md` / `examples/**/*.md` negations). Amended the Task-1 commit to include CHANGELOG + the `.gitignore` change atomically.
- **Files modified:** `.gitignore`
- **Verification:** `git cat-file -e HEAD:flow-jetbrains/CHANGELOG.md` → tracked; `git show --stat` shows `create mode 100644 flow-jetbrains/CHANGELOG.md`.
- **Committed in:** `4910f8f` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 Rule-1 bug, 1 Rule-3 blocking).
**Impact on plan:** Both essential — the Rule-1 fix is required for the plugin to verify (and thus publish) at all; the Rule-3 fix ensures the deliverable CHANGELOG is actually tracked. No scope creep; no architectural change.

## Issues Encountered

- **Host JDK incompatible with Gradle 8.6.** The box had only JDK 8 (too old for the IntelliJ Platform plugin — class file 61.0 = JDK 17+) and JDK 25 (Gradle 8.6's bundled Kotlin DSL throws `IllegalArgumentException: 25.0.3` parsing the version, before any toolchain resolution). `sudo apt install openjdk-21-jdk` was unavailable (non-interactive, no sudo TTY). Resolved by downloading a non-root portable Temurin JDK 21 to `$HOME/.local/jdks/jdk21` and pointing `JAVA_HOME` / `-Dorg.gradle.java.home` at it — `buildPlugin` + `verifyPlugin` then ran genuinely.
- **`verifyPlugin` with committed `recommended()` cannot resolve `ideaIC:2025.3`.** `recommended()` selects an IDE list including IntelliJ IDEA Community 2025.3, which is not published to the JetBrains download/repository endpoints in this environment (`Could not find idea:ideaIC:2025.3`). This is an upstream-availability issue, NOT a plugin defect — it resolves on the composer's machine once 2025.3 ships, or by pinning an explicit IDE. The plugin's actual compatibility was proven against the downloadable IC-2024.2 baseline (`Compatible`).

## Honest Human-Gate Boundary (D-03)

- **Done autonomously here:** the full signing/publishing/verification DSL (env-var-only), v1.5.0 metadata + change-notes, CHANGELOG.md, install.html, a green `buildPlugin`, and a `Compatible` `verifyPlugin` against IC-2024.2.
- **NOT done (the human gate, `41-HUMAN-UAT.md` row 6):** `./gradlew signPlugin publishPlugin` — the Marketplace upload. It needs the composer's JetBrains account, signing certificate, and publish token, supplied as env vars only. **The plugin was not published.** No secret was ever present on this box.

## Security Verification (T-41-06-IDISCLOSE)

- No secret literal exists in any committed file. `build.gradle.kts` references `environmentVariable("CERTIFICATE_CHAIN")`, `environmentVariable("PRIVATE_KEY")`, `environmentVariable("PRIVATE_KEY_PASSWORD")`, `environmentVariable("PUBLISH_TOKEN")` only; a regex scan for `(certificateChain|privateKey|password|token) = "<value>"` finds nothing.

## Next Phase Readiness

- JET-01 build/verify half is complete and proven. Everything for a one-command composer upload is staged. The remaining work is the human Marketplace publish (D-03 gate) and the v1.5.0 GitHub Release that attaches `flow-jetbrains-1.5.0.zip` (referenced by `docs/jetbrains/install.html`).
- Phase 41 remains execution-complete-pending-HUMAN-UAT; this plan does not flip the phase status.

## Self-Check: PASSED

- `flow-jetbrains/build.gradle.kts` — FOUND; contains all four `environmentVariable(...)` references; no secret literals.
- `flow-jetbrains/CHANGELOG.md` — FOUND; tracked in HEAD; has `## [1.5.0]`.
- `docs/jetbrains/install.html` — FOUND; self-contained static HTML.
- `flow-jetbrains/src/main/resources/META-INF/plugin.xml` — FOUND; `<version>1.5.0</version>` + `<change-notes>`; `since-build="242"` + LSP4IJ server intact.
- Commits `4910f8f` and `7678554` — present in `git log`.
- `flow-jetbrains-1.5.0.zip` produced by `buildPlugin`; `verifyPlugin` vs IC-2024.2 → Compatible.

---
*Phase: 41-reach-v1-5-closer*
*Completed: 2026-06-08*
