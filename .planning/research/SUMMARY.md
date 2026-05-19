# Project Research Summary — Flow v1.5 Stage, Studio, Web

**Domain:** Music-production interpreter (.NET 10 C#) — citizenship + reach milestone over an already-shipped v1.4 base
**Researched:** 2026-05-18
**Confidence:** HIGH on language-foundation, sound-design, notation, and OSC surfaces; MEDIUM on Phase 38 live-coding UX details; MEDIUM-LOW on Phase 41 WASM (rapidly evolving) and Ableton Link license posture

## Executive Summary

Flow v1.5 is a citizenship + reach milestone, not a feature-from-scratch one. Across 7 phases (35–41) it adds 23 picked features + 4 v1.4 carryovers + housekeeping to a language that already ships ~83K LOC C# + 312 .flow files, a full DSP rack, 9 synths + sampled instruments + opt-in SFZ orchestral sampler, Phase 28 articulations, multi-track MIDI export, MIDI import, LSP-backed editor, microtonal Scala tunings, and a public symphony+ragtime showcase. The job is to take Flow from "credible single-author public language" to "real citizen of the music-software world" alongside TidalCycles, Sonic Pi, Strudel, and SuperCollider — extending creative reach (live coding revamp, generative algebra, improv API), ecosystem interop (notation export, real-time MIDI, transport sync), and distribution (WASM playground, cross-platform binaries, docs generator).

The recommended approach is **hand-roll-first, dep-add-only-when-the-surface-justifies-it.** Phases 35–37 ship zero new external dependencies. Phase 38 adds one (Rug.Osc); Phase 39 vendors two source bundles (ABCSharp, musicxml-schemas); Phase 40 adds RtMidi.Core + optional JackSharp (with Ableton Link held behind a license check); Phase 41 adds NAudio.Wasapi + OwnAudioSharp for cross-platform audio backends and a Blazor WebAssembly host for the playground. The honest WASM recommendation is **Mono-WASM jiterpreter, not NativeAOT-LLVM** — Flow's reflection-heavy InternalFunctionRegistry would require a source-generator pass to survive AOT trimming; that pass is a v1.6 candidate, not a v1.5 lock.

The silent constraint across all 7 phases is the **two-run cmp-clean determinism contract** — 7 of 25 pitfalls touch it directly (granular jitter, sampler round-robin, generative primitives Markov/L-system/cellular/Lorenz, Link tempo, OSC, watch-mode wall-clock, MIDI clock discovery). Every PRNG-driven feature must thread through the existing renderSong/writeWav boundary reseed pattern (Phase 15 dither precedent); the `live { ... }` block is the single explicit opt-out, documented at every entry. The second cross-cutting concern is that **Phase 38's modernized watch mode + `live` block is the linchpin for Phase 41's WASM playground** — without it, the playground is "run a script in the browser" instead of "Strudel-tier live coding in the browser." The third is two license hazards (Ableton Link's GPLv2/commercial dual-license and RubberBand's identical posture) — Link gates a Phase 40 stretch feature; RubberBand is rejected in favor of a hand-rolled phase vocoder.

## Key Findings

### Recommended Stack

Flow's v1.4 baseline (.NET 10, C# 13, PulseAudio P/Invoke, DryWetMidi 8.0.3 for offline MIDI files) carries forward unchanged. v1.5 introduces 4–6 new NuGet packages, all scoped behind interface seams (`IAudioBackend`, new `IMidiBackend`, new OSC module). The majority of new code is hand-rolled into existing folders; the dep adds cluster at the back of the milestone (Phases 38, 40, 41). See STACK.md.

**Core technologies (new for v1.5):**
- **Rug.Osc 1.2.5** (Phase 38): OSC 1.0 protocol complete; MIT-style; zero deps
- **RtMidi.Core 1.0.53** (Phase 40): cross-platform real-time MIDI device I/O — required because DryWetMidi 8.0.3 explicitly has no Linux/ALSA support for device I/O (file I/O only)
- **NAudio.Wasapi 2.3.0** (Phase 41): Windows audio backend, scoped to a single `WasapiBackend.cs`
- **OwnAudioSharp 1.0.68** (Phase 41, preferred for macOS): miniaudio C# binding; fall back to hand-rolled CoreAudio P/Invoke if latency unacceptable
- **Blazor WebAssembly (.NET 10 SDK)** + **KristofferStrube.Blazor.WebAudio** (Phase 41): WASM playground host
- **Vendored sources, not NuGets:** `matthewcpp/ABCSharp` (Phase 39 ABC import), `sightreader/musicxml-schemas` POCOs (Phase 39 MusicXML export scaffolding)
- **Hand-rolled, no library:** pattern matching + Rust diagnostics + test framework (Phase 35); Tidal pattern algebra + Markov/L-system/CA/Lorenz + chord-aware Markov improv (Phase 36); granular synthesis + hand-rolled phase vocoder + stereo pan + sampler polish (Phase 37); `live { ... }` + modernized watch + REPL polish + audio input via PA_STREAM_RECORD (Phase 38); LilyPond export + MML import (Phase 39); MIDI clock (Phase 40); `flow doc` (Phase 41)

**Explicitly rejected:** NAudio-full / CSCore (Windows-centric duplication), NWaves (abandoned), managed-midi ("past project"), DryWetMidi for real-time device I/O (no Linux), Magenta (Python, archived 2026-01-06), DocFX (documents C# not Flow), MusicXml.NET (parser only), RubberBand (GPL hazard), CC-BY-SA/CC-BY-NC native libs (Phase 29 SPEC-2 precedent).

**License gating:** Ableton Link is dual-license GPLv2+/commercial — flag for legal review at Phase 40 start.

### Expected Features

Scope is locked by PROJECT.md / .planning/MILESTONES.md — 23 picked features + 4 carryovers across 7 phases. v1.5 reads as table stakes for the category of language Flow is joining. See FEATURES.md.

**Must have (table stakes, P1):**
- **Phase 35:** pattern matching with literal+constructor+wildcard+guard + music-aware extractors; Rust-style multi-line diagnostics; pure-Flow test framework + `(test "name" body)` + 4 assert primitives; `-> as name` chain naming
- **Phase 36:** 12 Tidal-style combinators on Sequence; generative primitives (Markov, L-system, CA rules 30/90/110/184 + 2D Life, Lorenz); parameterized sections; chord-aware Markov improvisation
- **Phase 37:** granular synthesis (Hann + Gaussian + Tukey); independent time-stretch + pitch-shift via hand-rolled phase vocoder with `#vocoder`/`#psola`/`#auto` modes; stereo pan + SFZ-renderer stereo retrofit; sampler polish bundle (round-robin, velocity-layer crossfade, per-articulation envelope multipliers, more flute samples, sampled drums, ragtime piano warmth)
- **Phase 38:** `live { ... }` block + cue-quantized hot-swap at bar boundary; modernized watch with ANSI status; REPL polish (LSP-backed completion + `?fn` help + multiline + history search + piano-roll preview); audio input; OSC server + client
- **Phase 39:** MusicXML export (partwise 3.1 subset); LilyPond export; ABC import (2.1 + abc2midi); MML import (PC-98-era common core)
- **Phase 40:** real-time MIDI output via new IMidiBackend; MIDI clock master + slave (24 PPQN); Ableton Link (license-gated); JACK transport (Linux opt-in)
- **Phase 41:** WASM playground (Mono-WASM jiterpreter, share-via-URL); cross-platform binaries; `flow doc` generator; JetBrains Marketplace publish; third-genre showcase (jazz / EDM / death metal)

**Differentiators (where v1.5 beats peers):** Tidal algebra on a statically-typed language; music-aware match extractors; LilyPond export (no peer ships first-class); first-class generative primitives in core stdlib; chord-aware Markov improv as zero-dep built-in.

**Should have (P2, defer cleanly):** `-> as name`, audio input, JACK transport, ABC + MML import (P2/P3), JetBrains Marketplace publish.

**Anti-features (declared rejected):** transformer/VAE improv ML; full MusicXML round-trip; real-time multi-channel audio routing via JACK/ASIO; browser-based collaborative editing; hot-reload by destroy-and-reconstruct; VST/AU hosting; type inference for `var`; user-defined types/structs; global per-articulation envelope-multiplier knob.

### Architecture Approach

v1.5 is additive integration into Flow's existing pipeline (Source → Lexer → Parser → AST → Interpreter → Value) with no rewrites. Existing IAudioBackend is extended (capture + Web/Wasapi/CoreAudio impls), parallel IMidiBackend introduced, new stdlib folders cluster by Phase. Pattern AST nodes live in a new `Ast/Patterns/` folder distinct from Expressions/Statements. MusicalContext push/pop stack is the canonical scoping mechanism — Phase 36 parameterized sections push synthetic frames; Phase 40 Link reads via a delegating accessor (no write-back from network). See ARCHITECTURE.md.

**Major components:**
1. **`Ast/Patterns/` + `MatchExpression`** (Phase 35) — separate AST tree for patterns; decision-tree compile (Jules Jacobs / Yorick Peterse reference)
2. **`Diagnostics/SnippetRenderer`** (Phase 35) — Rust-style renderer; defaulted-Span retrofit (v1.3 Phase 22 precedent)
3. **`StandardLibrary/Test/` + `flow-cli/Commands/TestCommand`** (Phase 35) — test framework + FlowEngine snapshot/restore for hermetic isolation
4. **`StandardLibrary/Patterns/` + `Generative/` + `Runtime/PrngRegistry`** (Phase 36) — Tidal combinators + Markov/L-system/CA/Lorenz routed through a seeded RNG registry keyed by (SourceLocation, generator-name)
5. **`Audio/DSP/GranularSynth` + `TimeStretchPitchShift`** + SFZ/Sampled-renderer extensions (Phase 37) — voice gets a `Pan` attribute applied at constant-power before additive mix
6. **`Ast/Statements/LiveBlockStatement` + `flow-interpreter/LiveReloadManager` extension + `flow-cli/Commands/LiveCommand`** (Phase 38) — embed flow-lsp in-process for REPL completion; new AudioInputManager; OSC server/client
7. **`StandardLibrary/Notation/`** (Phase 39) — MusicXmlExport, LilyPondExport, AbcImport, MmlImport sharing a small score IR
8. **`Audio/IMidiBackend` + `Audio/Backends/` (Alsa/CoreMidi/WinMm) + `Audio/Transport/` (MidiClock/Link/Jack)** (Phase 40) — MusicalContext.Tempo becomes Link-pollable via delegating accessor (no mutation)
9. **`Audio/Backends/Wasapi/CoreAudio/WebAudioBackend` + `flow-wasm/` project + `flow-cli/Commands/DocCommand`** (Phase 41) — new `flow-wasm/` Blazor project (NOT a target-framework flip on flow-cli); `flow doc` lives in flow-cli as subcommand

**Anti-patterns documented:** lifting platform-specific code out of IAudioBackend; hardcoding tempo at render-time; adding patterns inside Expressions; spawning per-feature threads (use one event pump); making WASM a target profile of flow-cli; per-test subprocesses.

### Critical Pitfalls (top 7 of 25)

1. **WASM .NET 10 NativeAOT-LLVM kills FlowEngine's reflection-heavy registry (Phase 41)** — Ship the playground on Mono-WASM jiterpreter; take the ~10 MB hit; revisit NativeAOT in v1.6 once a source-generated registrations table exists. Reject any new reflection in v1.5 unless gated behind `[DynamicallyAccessedMembers]`.

2. **Ableton Link tempo overwrites MusicalContext and breaks offline-render determinism (Phase 40)** — Lock at Phase 40 start: Link is a render-time input for playback only (play/loop/preview), NEVER for writeWav/writeMidi. Latch last-seen tempo on peer-disappear; do NOT fall back mid-piece. CI test: byte-identical writeWav with Link peer vs without.

3. **Real-time MIDI output: hot-plug + sysex + audio-MIDI latency misalignment (Phase 40)** — IMidiBackend per platform with own hot-plug thread; emit MIDI at `audioBuffer.PlaybackStartTime + bufferOffset` (NOT at queue time); sysex on separate queue marked best-effort; hot-plug = log + retry + quiet-drop (NEVER throw — would break long `live` sessions).

4. **MusicXML cross-consumer divergence (Phase 39)** — Target MuseScore as reference consumer; subset to 3.1 partwise; lock articulation decision table (Accent→`<accent/>`, Marcato→`<strong-accent/>`, Sforzando→`<dynamics><sfz/></dynamics>`, Legato as slur spans NOT per-note); flatten nested tuplets; CI round-trip via mscore; document Dorico tuplet limitations.

5. **`live { ... }` state preservation + infinite-loop bailout (Phase 38)** — Document that live blocks opt out of determinism contract (stderr advisory on every entry); reload at bar boundary; voice-pool state preserved IF voice name still exists; **30-second wall-clock cap with CancellationToken**; 200ms file-watch debounce; stale-closure detection.

6. **Generative-primitive determinism break (Phase 36)** — Every Markov/L-system/CA/Lorenz call takes optional `#seed N`; unseeded → reseeded at renderSong boundary via new PrngRegistry keyed by (SourceLocation, generator-name). Lorenz cross-platform FP divergence documented as platform-specific limitation.

7. **Pattern-matching exhaustiveness vs charitable interpretation (Phase 35)** — Non-exhaustive match warns to stderr + falls through to Void (charitable interpretation rule). Composer opts INTO strict via `enable matchExhaustive;` pragma. NO C-style fall-through; arms independent; type-narrowing within arms on.

**Plus 18 secondary pitfalls** covering: LilyPond engraver edge cases, ABC dialect divergence, MML scope creep, phase-vocoder transient smearing, granular clicks + jitter determinism, OSC type-tag drift + flood rate, REPL partial-parse completion, Rust-diagnostic span migration, test-framework state pollution, cross-platform audio buffer-size, JetBrains Marketplace publish, stereo-pan mono-fold, sampler round-robin determinism, doc-generator example-as-test, watch-mode stale closures, improv-API distinguishability, MIDI clock master/slave, audio input feedback + sample-rate, generative primitive seeding.

## Implications for Roadmap

Phase structure is **already locked** by PROJECT.md / MILESTONES.md — 7 phases (35–41). Research surfaces sub-order within each phase and which phases warrant deeper research-spawn during `/gsd:plan-phase`.

### Phase 35: Language Foundation
**Rationale:** Bottom of the dependency tree. Pattern matching unblocks Phase 36 parameterized-section destructuring AND Phase 40 MIDI event dispatch. Rust diagnostics improve every later parser change. Test framework lets every later phase land regression coverage.
**Delivers:** `match`/`case`/`when`/`as` + decision-tree compile + music-aware extractors; defaulted-Span AST retrofit + SnippetRenderer; pure-Flow test framework + `flow test` CLI + FlowEngine snapshot/restore; `-> as name` parser sugar; housekeeping (humanizeGaussian voice-block bug, Phase 17 HUMAN-UAT rows 1-3, Phase 04 verification gaps, CLAUDE.md "Public as of v1.4" footnote revision).
**Sub-order:** Diagnostics renderer / Span migration first → test framework → match → `-> as name`.

### Phase 36: Sequence Algebra & Generative
**Rationale:** Builds on Phase 35 pattern matching. Three of v1.5's "beats peers" features land here.
**Delivers:** 12 Tidal combinators on Sequence; Markov/L-system/CA/Lorenz; parameterized sections; chord-aware Markov improvise.
**Sub-order:** Pattern algebra → Markov + L-system + CA + Lorenz → improvise → parameterized sections.

### Phase 37: Sound Design + Sampler Polish (largest phase)
**Rationale:** Closes 4 v1.4 carryovers. Roadmapper may subdivide plans per PROJECT.md note.
**Delivers:** granular(...); stretch(..., #vocoder|#psola|#auto); per-voice Pan attribute; SFZ round-robin + velocity-layer crossfade + per-articulation envelope multipliers stacking multiplicatively on Phase 28 rules; more flute samples; sampled drums; ragtime piano warmth.
**Sub-order:** Sampler polish first (low-risk Phase 33 extensions) → granular + time-stretch + stereo pan independently.
**Audit before plan starts:** Stereo-pan scope — PROJECT.md says shipped at v1.0 Phase 2; v1.4 forward-deferred says open. Per-voice synth-path likely shipped + SFZ sampler-path mono-only. Confirm during Phase 37 CONTEXT spawn.

### Phase 38: Live Coding 2.0
**Rationale:** Linchpin for Phase 41 WASM playground.
**Delivers:** `live <quantize> { ... }` block + cue-quantized hot-swap; modernized watch (ANSI + structured stderr + 30s bailout + 200ms debounce); REPL polish (LSP-in-process completion + token-heuristic for partial-parse + `?fn` help + multiline + history + piano-roll preview); audio input via PA_STREAM_RECORD; OSC server + client with `,d`-only discipline + 200 Hz rate limit + no zeroconf in v1.5.
**Uses:** Rug.Osc 1.2.5 (only new NuGet); in-process flow-lsp reference.
**Sub-order:** Modernized watch + live block FIRST → REPL polish → audio input → OSC.

### Phase 39: Notation Citizenship
**Rationale:** Standalone (any time after Phase 35). MusicXML export is citizenship hook; LilyPond export is engraving-quality differentiator vs all peers.
**Delivers:** MusicXML 3.1 partwise export; LilyPond text emit; ABC 2.1 + abc2midi import; MML PC-98 common-core import.
**Uses:** Vendored sightreader/musicxml-schemas POCOs + vendored matthewcpp/ABCSharp source — no new NuGets.
**Sub-order:** MusicXML → LilyPond → ABC → MML (defer to v1.6 first if cuts needed).

### Phase 40: Studio Sync
**Rationale:** IMidiBackend is the linchpin (clock + Link + JACK all sit on top). Ableton Link license review must happen at phase start.
**Delivers:** IMidiBackend mirroring IAudioBackend; ALSA-seq + CoreMIDI + WinMM impls via RtMidi.Core 1.0.53; MIDI clock master + slave (24 PPQN, mode-switch at bar-boundary only, 8-pulse settle in slave); Ableton Link via P/Invoke (license-gated); JACK transport via JackSharp 0.4.0 (Linux opt-in).
**Uses:** RtMidi.Core 1.0.53, JackSharp 0.4.0, Ableton Link C++ via P/Invoke (gated).
**Sub-order:** IMidiBackend Linux first → MIDI clock → Link → JACK.

### Phase 41: Reach + Closer
**Rationale:** Last by construction. Closes the milestone with third-genre showcase consuming features from Phases 35-40.
**Delivers:** WASM playground (Mono-WASM jiterpreter, <15 MB compressed target); cross-platform binaries (linux-x64/arm64, osx-x64/arm64, win-x64 self-contained); `flow doc` (`///` doc-comment grammar + extracted-and-executed examples + content-hash incremental cache); JetBrains Marketplace publish (signing + verifier CI + direct-download fallback); third-genre showcase.
**Uses:** NAudio.Wasapi 2.3.0 (single file); OwnAudioSharp 1.0.68 (smoke-test in Plan 01, fall back to hand-rolled CoreAudio P/Invoke); KristofferStrube.Blazor.WebAudio in new `flow-wasm/` project.
**Sub-order:** flow doc first → WASM playground → cross-platform binaries → JetBrains publish → third-genre showcase last.

### Phase Ordering Rationale

- Phase 35 absolutely first — match used in Phase 40 MIDI dispatch, Phase 36 destructuring, Phase 39 articulation emit
- Phase 36 ↔ Phase 37 commutative but milestone orders 36 first
- Phase 38 must precede Phase 41 (WASM playground IS watch-mode-in-browser)
- Phase 39 standalone
- Phase 40 must precede Phase 41's MIDI piece (Web MIDI is just another IMidiBackend)
- Phase 41 last — third-genre showcase consumes everything

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | DryWetMidi Linux limitation confirmed at official docs; all packages .NET 10 compatible; WASM HIGH for Mono-jiterpreter, MEDIUM for NativeAOT (deferred). License HIGH except Ableton Link (gating pending). |
| Features | HIGH | Peer-tool calibration well-documented; FEATURES.md surfaces table-stakes/differentiator split. MEDIUM-LOW only on improv style-rule content (empirical). |
| Architecture | HIGH | Grounded in actual file inspection. Existing IAudioBackend + Phase 33 SFZ + Phase 32 Tuning patterns are templates. MEDIUM only on WASM and Ableton Link. |
| Pitfalls | HIGH | 7 of 25 pitfalls map directly to documented determinism contract; cross-cutting constraints are internal-grounded. |

**Overall confidence:** HIGH

### Gaps to Address

- **Ableton Link license posture (Phase 40 gating)** — GPLv2+/commercial needs legal review against MIT distribution. Action: Phase 40 CONTEXT must confirm license check before plan land. If conflict, defer Link to community contribution.
- **Phase 37 stereo-pan audit** — PROJECT.md says shipped v1.0 Phase 2; v1.4 forward-deferred says open. Likely per-voice synth-path shipped + SFZ sampler-path mono-only. Action: Phase 37 CONTEXT inspect Audio/SongRenderer.cs + Audio/Sfz/SfzRenderer.cs.
- **OwnAudioSharp macOS latency unknown (Phase 41)** — miniaudio quirks on macOS could yield unacceptable live-coding latency. Action: Phase 41 Plan 01 smoke-test on real hardware; fall back to hand-rolled CoreAudio P/Invoke.
- **WASM bundle-size unknown (Phase 41)** — Mono-jiterpreter + FlowEngine + curated stdlib target <15 MB compressed. Action: Phase 41 Plan 01 dry-run; prune stdlib subset or lazy-load samples if >15 MB.
- **RtMidi.Core Linux ALSA untested upstream (Phase 40)** — Officially advertises Windows+macOS; Linux needs Flow-side integration test. Fallback: thin ALSA-direct backend behind IMidiBackend.
- **Pidgin dependency removal (housekeeping)** — Referenced but unused. Action: include as Phase 35 housekeeping subtask or Phase 41 final cleanup.

## Sources

Per-file sources are in STACK.md (28 entries), FEATURES.md (54 entries), ARCHITECTURE.md (codebase inspection), PITFALLS.md (12 external + 4 internal).
