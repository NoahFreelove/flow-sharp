# Phase 41: Reach + v1.5 Closer - Context

**Gathered:** 2026-06-07
**Status:** Ready for planning
**Mode:** `--auto` (Claude selected recommended defaults; every decision logged below for composer review/override before execution)

<domain>
## Phase Boundary

Phase 41 is the **v1.5 closer** — the "reach" surface that makes Flow learnable and runnable beyond the author's Linux box, plus the third-genre showcase that validates the genre-agnostic claim. Last by construction: it consumes Phases 35-40's surface.

**Genuine remaining scope (6 requirement clusters):**
1. **DOC-01 / DOC-02** — `flow doc` documentation generator (`///` doc-comments → browsable HTML reference; examples execute as regression tests).
2. **BIN-01** — cross-platform self-contained binaries (linux-x64/arm64, osx-x64/arm64, win-x64).
3. **WASAPI-01** — Windows audio backend (`IAudioBackend` via NAudio.Wasapi).
4. **COREAUDIO-01** — macOS audio backend (existing hand-rolled `CoreAudioBackend.cs` is the shipping path; OwnAudioSharp swap conditional on a failed Mac smoke-test).
5. **JET-01** — JetBrains plugin Marketplace publish (from Phase 31 scaffolding).
6. **SHOWCASE-01** — third-genre showcase piece (jazz / EDM / death metal — composer's choice).

**EXPLICITLY OUT OF SCOPE — already shipped, do NOT re-build:**
- **WASM-01 / WASM-02 / WASM-03** were **carved out of Phase 41 on 2026-05-25** (struck through in ROADMAP) and built by **Phases 47 (compile-target) + 48 (Mono-WASM runtime + WebAudioBackend) + 49 (flowlang.dev SvelteKit playground)** — via a *different architecture* than the original WASM-01 plan (Mono-WASM `flow-lang` Web target + SvelteKit site, NOT a Blazor `flow-wasm/` project). Phase 41 does NOT implement a WASM playground. **Bookkeeping reconciliation owed** (see D-19): REQUIREMENTS.md traceability still lists WASM-01/02/03 as "Pending / Phase 41" — the planner/verifier must flip them to "Shipped (carved to Phase 47-49)", not generate work for them.

</domain>

<decisions>
## Implementation Decisions

### Execution split — autonomous vs human-gated (D-01..D-05)
Phase 41 mixes Linux-completable work with cross-platform/external-account work that this Linux box cannot honestly verify. Per the autonomous-execution agreement (`feedback_autonomous_phase_execution`), we run everything we can and **flag — never fake** the human gates.

- **D-01 (autonomous, fully completable on Linux):** `flow doc` generator (DOC-01/02), third-genre showcase (SHOWCASE-01), `linux-x64` + `linux-arm64` binaries with runtime smoke-test (BIN-01 subset).
- **D-02 (autonomous code-write, human-verify):** Write `WasapiBackend.cs` (WASAPI-01) + confirm/keep `CoreAudioBackend.cs` (COREAUDIO-01) compile-clean and `IAudioBackend.IsAvailable()`-probe-gated; **cross-compile** `osx-x64`/`osx-arm64`/`win-x64` binaries (`dotnet publish` produces them from Linux without executing them) and checksum them. The backend code lands; *real-hardware audible verification is a human gate*.
- **D-03 (human gate — external account):** JetBrains Marketplace publish (JET-01) needs the composer's JetBrains account + signing cert. Prepare ALL artifacts (plugin.xml metadata, `build.gradle.kts` signing config, CHANGELOG.md, plugin-verifier CI, `docs/jetbrains/install.html` direct-download fallback); the actual Marketplace upload is the human's action.
- **D-04 (human gate — outward-facing publish):** Do NOT cut the v1.5.0 GitHub Release autonomously. Prepare release notes + all binary artifacts + SHA-256 checksums staged; the composer pushes the Release.
- **D-05 (human gate — hardware UAT):** Windows WASAPI + macOS CoreAudio audible playback verification, and execution-smoke of the osx/win binaries, are HUMAN-UAT rows written to `41-HUMAN-UAT.md`, mirroring `40-HUMAN-UAT.md` / `49-HUMAN-UAT.md`.

### `flow doc` generator — output & `///` grammar (D-06..D-10)
- **D-06:** `flow doc` ships as a **`flow-cli` verb** (lands in `flow-cli/Commands/`, sibling to `run`/`test`/`repl` from Phase 30). Not a new project.
- **D-07:** `///` doc-comment grammar is **additive to `//`** at the lexer (`SimpleLexer.SkipWhitespaceAndComments`): a `///` line emits a doc-comment token bound to the *following* proc declaration; `//` line comments and `/* */` blocks are unchanged. Charitable: a proc with no `///` still gets a signature-only doc entry (never an error) — consistent with `feedback_charitable_interpretation`.
- **D-08:** Content sources = `///` doc-comments + parsed proc signatures + **`flow-lang/StandardLibrary/BuiltInDocs.cs`** builtin metadata (the existing ~104 Phase 17/31 entries). No duplication — generator reads BuiltInDocs directly.
- **D-09:** Output = **browsable static HTML** at `docs/reference/index.html` (default) **plus a Markdown sibling** (so the reference is greppable/diffable in-repo). Content-hash incremental cache so re-gen only touches changed entries. Static HTML only — no search/interactive JS for v1.5 (deferred).
- **D-10 (DOC-02):** Code examples inside `///` doc-comments **execute via the Phase 35 TEST-01 hermetic test framework** (the `flow test` runner). Failures surface as `[example failed]` annotations in the generated docs AND double as regression tests. Examples that produce audio/MIDI are checked for successful render, not byte output, to stay platform-portable.

### Third-genre showcase (SHOWCASE-01) — D-11..D-13
- **D-11 (RECOMMENDED DEFAULT — composer confirms/overrides at plan time):** **EDM.** Rationale: maximally contrasting with v1.4's classical *symphony* + *ragtime*, and the strongest fit for the required feature checklist — four-on-the-floor **pattern matching**, a Phase 36 **generative primitive** (e.g. Euclidean/`markov`), Phase 37 **granular DSP / time-stretch** (the signature EDM texture), sidechain compression (already in Flow), a **live block**, and **real-time MIDI** out. This is a genuine *composer* choice — it is one `.flow` file under `examples/<genre>/`, trivially swappable. **The composer should confirm before execution.**
- **D-12 (alternatives, equally valid):** **Death metal** — boldest genre-agnostic proof (tremolo-picked patterns, blast-beat drums, aggressive synthesis); **Jazz** — leans on `@improv jam` + swing + Markov, but *overlaps the existing `examples/generative/markov_jazz.flow`* so it showcases less new ground.
- **D-13:** ~60s curated piece at `examples/<genre>/<piece>.flow`; README.md `## Showcase` v1.5 section embeds inline-audio; the piece's WAV + MIDI offline render must hold two-run cmp-clean + RMS-windowed regression (±0.5 dB / 100 ms per SPEC-8).

### Cross-platform binaries (BIN-01) — D-14..D-16
- **D-14:** `dotnet publish -p:PublishSingleFile=true --self-contained -r <rid>` for all 5 RIDs, extending **`scripts/publish.sh`**. All 5 build autonomously (cross-compiled from Linux); only linux-x64/arm64 are runtime-smoke-tested here (D-02).
- **D-15:** **No trimming for v1.5.** The reflection-heavy `InternalFunctionRegistry` makes `PublishTrimmed` a runtime-breakage hazard (same caution as the WASM `trim-roots.xml`); desktop binary size is not budget-critical. Trimming + source-gen registry is a v1.6 item (also unblocks NativeAOT-LLVM per D-v1.5-02).
- **D-16:** Artifact naming: `flow-<rid>-v1.5.0.tar.gz` (Linux/macOS) + `flow-win-x64-v1.5.0.zip` (Windows), alongside the existing `flow-linux-x64.tar.gz`. Each ships with a `.sha256`. Framework-dependent = false (self-contained).

### Windows / macOS audio backends (WASAPI-01 / COREAUDIO-01) — D-17..D-18
- **D-17 (Windows):** Single `flow-lang/Audio/WasapiBackend.cs` implementing `IAudioBackend` via **NAudio.Wasapi 2.3.0**, structured after `PulseAudioSimpleBackend.cs`. Shared-mode default; exclusive-mode opt-in via a config flag (Phase 30 `config.toml`). Desktop-only → Web-stripped + `IsAvailable()`-probe-gated like CoreAudio already is.
- **D-18 (macOS):** The **existing hand-rolled `flow-lang/Audio/CoreAudioBackend.cs`** (AudioToolbox/AudioQueue P/Invoke) **is the shipping path** — it already exists but has never been verified on real Mac hardware. OwnAudioSharp 1.0.68 evaluation is **deferred unless** the human Mac smoke-test (D-05) shows >20 ms round-trip latency unacceptable for live coding, at which point swap to OwnAudioSharp (RESEARCH should still scope the swap so it's ready). NAudio.Wasapi PackageReference must be Desktop-only (Web `AssemblyReferenceScanTests` forbidden-prefix gate).

### Bookkeeping reconciliation — D-19
- **D-19:** During execution/verification, reconcile the WASM-01/02/03 traceability (mark Shipped/carved to 47-49, not Pending/41) and the stale ROADMAP progress-table rows (Phase 39 shows "Not started", Phase 48 shows "In Progress 5/7" — both actually shipped). STATE frontmatter + phase-detail sections are authoritative.

### Claude's Discretion
- Exact HTML template/styling for `flow doc` output (functional, readable; no design contract needed — this is a reference site, not the flowlang.dev marketing site).
- Internal structure of the doc-comment AST attachment and the content-hash cache key.
- Specific Euclidean/generative/DSP knobs in the showcase piece (composer owns the *musical* content; Claude owns the *scaffolding* that proves the feature checklist).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner) MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` — Phase 41 entry: goal, sub-order (`flow doc` → [WASM carved out] → binaries → JetBrains → showcase last), 6 success criteria, dependency notes (OwnAudioSharp smoke-test, miniaudio fallback).
- `.planning/REQUIREMENTS.md` §"Phase 41" — DOC-01/02 (l.162-163), BIN-01 (l.158), WASAPI-01 (l.153), COREAUDIO-01 (l.154), JET-01 (l.167), SHOWCASE-01 (l.171), and the carved WASM-01/02/03 (l.147-149, struck through in ROADMAP).
- `.planning/PROJECT.md` — core value, constraints (net10.0, minimal deps, two-run determinism).

### `flow doc` (DOC-01/02)
- `flow-lang/StandardLibrary/BuiltInDocs.cs` — builtin metadata source (~104 entries); the generator reads this directly.
- `flow-lang/Lexing/SimpleLexer.cs` (`SkipWhitespaceAndComments`) — the `///` additive-grammar insertion site.
- `flow-cli/Commands/` + `flow-cli/Program.cs` — where the `flow doc` verb registers (Phase 30 CLI).
- Phase 35 TEST-01 framework (the `flow test` runner) — `.planning/phases/35-language-foundation/35-04-SUMMARY.md` — DOC-02 example execution backbone.

### Cross-platform binaries & audio backends (BIN-01 / WASAPI-01 / COREAUDIO-01)
- `scripts/publish.sh` — existing publish script to extend for 5-RID self-contained output.
- `flow-lang/Audio/IAudioBackend.cs` — the interface WasapiBackend implements.
- `flow-lang/Audio/PulseAudioSimpleBackend.cs` — structural template for WasapiBackend.
- `flow-lang/Audio/CoreAudioBackend.cs` — existing macOS path (COREAUDIO-01 shipping path).
- `flow-lang.Tests/Integration/Phase47/AssemblyReferenceScanTests.cs` — add NAudio.Wasapi to forbidden-prefix gate so Web build drift is caught.

### JetBrains publish (JET-01)
- `flow-jetbrains/build.gradle.kts` + `flow-jetbrains/src/` — Phase 31 scaffolding to extend with signing config + verifier CI.

### Showcase & determinism (SHOWCASE-01)
- `examples/generative/markov_jazz.flow` — existing generative example (informs the jazz-overlap note in D-12).
- `flow-lang.Tests/Helpers/RmsRegressionTests.cs` (`AssertRmsWithinTolerance`) — SPEC-8 ±0.5 dB/100 ms gate for the showcase render.

### Sibling milestone-close human gates (context, not Phase 41 work)
- `.planning/phases/40-studio-sync/40-HUMAN-UAT.md` — Phase 40 hardware UAT still open.
- `.planning/phases/49-flowlang-dev-site/49-HUMAN-UAT.md` — Phase 49 deploy/OAuth/cross-browser gates still open.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`BuiltInDocs.cs`** — ~104 documented builtins; `flow doc` consumes it directly rather than re-deriving builtin metadata.
- **`CoreAudioBackend.cs`** — macOS `IAudioBackend` already exists (hand-rolled AudioToolbox P/Invoke); COREAUDIO-01 is mostly *verify*, not *build*.
- **`PulseAudioSimpleBackend.cs`** — the proven `IAudioBackend` shape (`IsAvailable()` probe + push path) to mirror for `WasapiBackend.cs`.
- **`scripts/publish.sh`** — existing single-RID publish to generalize to 5 RIDs.
- **`flow-jetbrains/`** — full Gradle plugin scaffold (build.gradle.kts, gradlew, src) from Phase 31; JET-01 adds signing/metadata/verifier on top.
- **`flow-cli/` (Commands/ + Scaffold/ + Program.cs)** — the formal CLI from Phase 30; `flow doc` is one more verb.

### Established Patterns
- **`#if !FLOW_WEB` + Compile-Remove + forbidden-prefix scan** (Phase 47) — the discipline for any new desktop-only native dep (NAudio.Wasapi must follow it).
- **Charitable interpretation** (`feedback_charitable_interpretation`) — missing `///` → signature-only doc entry, never an error.
- **HUMAN-UAT artifact pattern** (`40-HUMAN-UAT.md`, `49-HUMAN-UAT.md`) — the template for `41-HUMAN-UAT.md`.
- **Two-run cmp-clean + RMS regression** — the showcase render must preserve both.

### Integration Points
- Lexer `///` token → Parser binds it to `ProcDeclaration` → `flow doc` reads it (touches Lexing + Parsing + a new generator under flow-cli).
- `WasapiBackend` registers in `AudioPlaybackManager.DetectBackend` (Windows branch), mirroring the PulseAudio/CoreAudio selection.
- `flow doc` example execution reuses the Phase 35 hermetic test runner — no new isolation machinery.

</code_context>

<specifics>
## Specific Ideas

- **Genre = EDM is a recommended default, NOT a locked decision.** The composer owns this creative call; surface it for confirmation at plan time (alternatives death metal / jazz, each one `.flow` file). See `feedback_ergonomics_priority` + `project_genre_agnostic`.
- **"Flag, don't fake."** Every cross-platform/external gate writes an honest `41-HUMAN-UAT.md` row rather than a fabricated pass (`feedback_autonomous_phase_execution`).
- The macOS path is *already written* — keep the existing hand-rolled CoreAudio unless a real Mac proves it too laggy for live coding; don't add OwnAudioSharp speculatively.

</specifics>

<deferred>
## Deferred Ideas

- **OwnAudioSharp 1.0.68 swap for macOS** — only if the human Mac smoke-test shows >20 ms round-trip latency (D-18). Conditional, not v1.5 default work.
- **Binary trimming + source-generated `InternalFunctionRegistry`** — v1.6; unblocks `PublishTrimmed` desktop size wins *and* NativeAOT-LLVM WASM (D-v1.5-02).
- **`flow doc` search / interactive site features** — v1.6; v1.5 ships static HTML + Markdown only (D-09).
- **Custom flowlang.dev domain + Phase 49 live deploy / OAuth / cross-browser audio** — Phase 49's own human gates; tracked there, not Phase 41.
- **Phase 40 hardware UAT (real synth / DAW master+slave / JACK)** — Phase 40's human gate; required for milestone close but not Phase 41 work.

None of the above are scope creep — they are explicitly out of Phase 41's boundary and recorded so they aren't lost.

</deferred>

---

*Phase: 41-Reach + v1.5 Closer*
*Context gathered: 2026-06-07*
