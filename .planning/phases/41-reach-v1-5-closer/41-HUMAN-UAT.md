# Phase 41 — HUMAN-UAT: Cross-Platform Audio + Binaries + Marketplace + Release

**Status:** Phase 41 closed **Complete (HUMAN-UAT deferred)** 2026-06-08.
- **Rows 1–5 CANCELLED** (composer 2026-06-08): the cross-platform **hardware** gates (Windows/macOS audible playback, osx-x64/osx-arm64/win-x64 binary execution) are cancelled — this is a Linux-only box with no Mac/Windows hardware. The code shipped and the Linux-side machine half is automated (probe-gated `IsAvailable()==false`, Web build green, 5 RID binaries cross-compiled + `.sha256`-checksummed); the audible/executable confirmation simply won't be performed. Not faked, not pending — **cancelled by decision**.
- **Rows 6–7 DEFERRED** (not cancelled): JetBrains Marketplace publish + v1.5.0 GitHub Release are external-account/publish actions, not hardware. They stay as standing debt for whenever the composer chooses to publish; re-run via `/gsd:verify-work 41`.
Original status: **execution-complete-pending-HUMAN-UAT**.

> **"Flag, don't fake" — `feedback_autonomous_phase_execution` + D-02.** The autonomous
> Phase 41 run lands every line of Linux-completable code: the `flow doc` generator, the
> third-genre showcase, the `WasapiBackend.cs` / `CoreAudioBackend.cs` IAudioBackend code,
> the 5-RID `dotnet publish` cross-compile, and the SHA-256 checksums. What it **cannot
> honestly verify on this Linux box** — real Windows/macOS audible playback, execution of
> the osx/win binaries, the JetBrains Marketplace upload (external account + signing cert),
> and the outward-facing GitHub Release cut — is written here as an HONEST pending row, never
> a fabricated pass. Mirrors `40-HUMAN-UAT.md` / `49-HUMAN-UAT.md`.

> **Honest machine-vs-human split (D-02).** The automated suite proves the *Linux-side
> machine half*: `WasapiBackend.IsAvailable()` / `CoreAudioBackend.IsAvailable()` return
> `false` here without crashing (probe-gated), the Web build stays green (NAudio/AudioToolbox
> never reach the WASM closure), `flow doc` emits HTML+Markdown, the showcase render holds
> two-run cmp-clean + RMS regression, and all 5 RID archives + `.sha256` files are produced.
> It does NOT prove that a Windows machine makes sound through WASAPI, that a Mac makes sound
> through CoreAudio within 20 ms, that the cross-compiled osx/win binaries actually run, that
> the plugin appears on the JetBrains Marketplace, or that the v1.5.0 Release is live. Those
> are the rows here. **Code lands here (D-02); audible / executable / published verify there.**

## Prerequisites (composer's machines + accounts)

This dev box is Linux-only and holds no JetBrains signing cert. To clear these rows the
composer needs:

```bash
# 1. A Windows machine (for WASAPI-01 + win-x64 exec smoke)
#    - download flow-win-x64-v1.5.0.zip from the staged release artifacts
#    - verify its .sha256, unzip, run `flow version` and a render

# 2. A macOS machine — Intel AND/OR Apple Silicon (for COREAUDIO-01 + osx exec smoke)
#    - download flow-osx-x64-v1.5.0.tar.gz / flow-osx-arm64-v1.5.0.tar.gz
#    - verify .sha256, untar, run `flow version` + a live-coding session

# 3. A JetBrains account + plugin signing certificate (for JET-01)
#    - env vars ONLY (never committed): CERTIFICATE_CHAIN / PRIVATE_KEY /
#      PRIVATE_KEY_PASSWORD / PUBLISH_TOKEN  (T-41-01-IDISCLOSE mitigation)

# 4. GitHub repo push access (for the v1.5.0 Release cut, D-04)
```

The autonomous run stages every artifact (binaries, checksums, release notes, plugin
metadata + signing config) so each row below is a *verify + publish* step, not a *build* step.

## Per-Gate Rows

| # | What to verify | Requirement | Decision | How to verify | Status |
|---|----------------|-------------|----------|---------------|--------|
| 1 | Windows WASAPI **audible** stereo playback | WASAPI-01 | D-05 | On Windows, run `flow run examples/<genre>/<piece>.flow`; confirm audible stereo output through the default WASAPI device (shared-mode). The Linux-side machine half (`WasapiBackend.IsAvailable()==false`, no crash; Web build green; NAudio absent from WASM closure) is already automated — this row is the real-hardware audible confirmation. | **cancelled** (composer 2026-06-08 — no Windows hardware; WASAPI code shipped + machine-half automated) |
| 2 | macOS CoreAudio **audible** playback + **<20 ms** round-trip latency | COREAUDIO-01 | D-05 / D-18 | On macOS, run a live-coding session (`flow watch …`); confirm audible output AND that round-trip latency feels <20 ms for live coding. **If latency >20 ms**, escalate to the deferred OwnAudioSharp 1.0.68 swap (D-18) — the RESEARCH scoped it so it's ready; do NOT swap speculatively. The existing hand-rolled `CoreAudioBackend.cs` is the shipping path. | **cancelled** (composer 2026-06-08 — no macOS hardware; CoreAudio code shipped + machine-half automated) |
| 3 | osx-x64 binary **execution** smoke | BIN-01 | D-05 | On an Intel Mac, verify `flow-osx-x64-v1.5.0.tar.gz` `.sha256`, untar, run `flow version` + a render (`flow run examples/<genre>/<piece>.flow`). Cross-compiled from Linux (not executable here). | **cancelled** (composer 2026-06-08 — no Intel-Mac hardware; binary cross-compiled + checksummed) |
| 4 | osx-arm64 binary **execution** smoke | BIN-01 | D-05 | On Apple Silicon, verify `flow-osx-arm64-v1.5.0.tar.gz` `.sha256`, untar, run `flow version` + a render. Cross-compiled from Linux (not executable here). | **cancelled** (composer 2026-06-08 — no Apple-Silicon hardware; binary cross-compiled + checksummed) |
| 5 | win-x64 binary **execution** smoke | BIN-01 | D-05 | On Windows, verify `flow-win-x64-v1.5.0.zip` `.sha256`, unzip, run `flow version` + a render. Cross-compiled from Linux (not executable here). | **cancelled** (composer 2026-06-08 — no Windows hardware; binary cross-compiled + checksummed) |
| 6 | JetBrains Marketplace **publish** | JET-01 | D-03 | From `flow-jetbrains/`, run `./gradlew signPlugin publishPlugin` with the signing cert + publish token supplied as env vars ONLY — `CERTIFICATE_CHAIN` / `PRIVATE_KEY` / `PRIVATE_KEY_PASSWORD` / `PUBLISH_TOKEN` — **never committed to VCS** (T-41-01-IDISCLOSE). Confirm the plugin appears on the JetBrains Marketplace (or the `docs/jetbrains/install.html` direct-download fallback is used). **Plan 41-06 staged everything autonomously:** `plugin.xml` v1.5.0 metadata + `<change-notes>`, `build.gradle.kts` `signing`/`publishing`/`pluginVerification` DSL (env-var-only secrets), `CHANGELOG.md`, `docs/jetbrains/install.html`. `./gradlew buildPlugin` → **BUILD SUCCESSFUL** (`flow-jetbrains-1.5.0.zip` produced); `./gradlew verifyPlugin` against the downloadable **IC-2024.2** baseline → **Compatible** (caught + fixed a Phase-31 `until-build=""` defect → `253.*`). The committed `recommended()` verifier list could not resolve `ideaIC:2025.3` in the build env (not yet published upstream); it resolves on the composer's machine. The upload (sign + publish) is the composer's action — env-var secrets only. | **pending** (artifacts staged + build/verify green; publish = composer) |
| 7 | v1.5.0 GitHub **Release** cut | BIN-01 / SHOWCASE-01 | D-04 | Composer verifies every staged `.sha256`, attaches the 5 binaries (`flow-{linux-x64,linux-arm64,osx-x64,osx-arm64}-v1.5.0.tar.gz` + `flow-win-x64-v1.5.0.zip`) + the showcase WAV/MIDI, and pushes the Release. Do NOT cut the outward-facing Release autonomously (D-04) — release notes + all artifacts are staged; the human publishes. | **pending** |

*Status legend: pending · passed · gotcha (non-blocking) · failed (blocking → routes to in-phase repair or v1.6 defer).*

## Secret Handling (T-41-01-IDISCLOSE — JET-01)

The JetBrains signing certificate and publish token are passed to `./gradlew
signPlugin publishPlugin` as the environment variables `CERTIFICATE_CHAIN`,
`PRIVATE_KEY`, `PRIVATE_KEY_PASSWORD`, and `PUBLISH_TOKEN`. **No secret enters version
control** — neither this UAT doc, the staged `build.gradle.kts`, nor the CHANGELOG carries
a cert or token. This is the mitigation recorded in the 41-01 plan threat register
(T-41-01-IDISCLOSE: env-var-only, "never committed").

## Closure Conditions

Phase 41 was closed **Complete (HUMAN-UAT deferred)** 2026-06-08. Revised conditions:

- **Rows 1–5** (Windows/macOS audible + osx-x64/osx-arm64/win-x64 exec) — **CANCELLED** 2026-06-08.
  No Mac/Windows hardware on the composer's setup; the code shipped + the Linux-side machine half
  is automated. These will not be performed and no longer gate the phase.
- **Row 6** (JetBrains publish) — DEFERRED; sign off pass OR document the direct-download fallback
  whenever the composer chooses to publish.
- **Row 7** (v1.5.0 GitHub Release) — DEFERRED; cut by the composer with all `.sha256` verified
  whenever the composer chooses to release.

Rows 6–7 are optional publish steps, not blockers. Re-run `/gsd:verify-work 41` if/when taken.

A row that fails with a blocking defect routes through the closer: in-phase repair if it is a
Flow-side bug, or a documented v1.6 deferral. Cross-platform/external-account verification is
the composer's action by definition — the autonomous run cannot and does not fake it.

## Composer Notes

_(composer appends date-stamped observations here after the UAT pass — model the Phase
40/48/49 Composer Notes blocks: honest about what was vs wasn't confirmed, per-platform.)_
