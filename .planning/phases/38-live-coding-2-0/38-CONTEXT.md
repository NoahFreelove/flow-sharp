# Phase 38: Live Coding 2.0 - Context

**Gathered:** 2026-05-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 38 ships the v1.5 live-coding pillar across four surfaces:

1. **`live <quantize> { ... }` block + modernized watch mode** (LIVE-01..03) — Composer wraps a block in `live <quantize> { ... }` (default `1bar`). On file save the block re-evaluates and swaps at the next quantize-unit boundary with a 64-sample equal-power crossfade. Quantize accepts `NoteValue` (`q`/`h`/`w`/etc.) or `Bar`. Stderr advisory at every entry explicitly opting OUT of the two-run cmp-clean determinism contract (D-v1.5-07). 30s wall-clock evaluation cap (CancellationToken); 200ms file-watch debounce. Voice-pool state preserved IF voice name still exists post-edit; musical context stack resets to file-scope; PRNG state reseeded at swap boundary; stale-closure detection raises a clear advisory rather than silently misbehaving. Modernized watch rewrites the existing `flow --watch` (`flow-interpreter/LiveReloadManager.cs` 389 lines, currently 500ms debounce + bar-boundary swap + 64-sample crossfade) — ANSI live status panel + structured stderr.

2. **REPL polish** (REPL-01..04) — LSP-backed tab completion (REPL embeds `flow-lsp` in-process and queries `CompletionHandler`; token-heuristic fallback on partial-parse failure); inline `?fn` help (signature + doc-comment + 1-line example from `BuiltInDocs` 104 entries from Phase 31); multi-line editing with paren-balanced continuation prompt; Ctrl+R history search backed by `~/.config/flow/history`; ASCII piano-roll on `(inspect seq)` — pitch on Y, time on X, tick marks at bar boundaries, articulation glyphs (`>`/`.`/`^`/etc.) at note onsets.

3. **Audio input** (AUDIO-IN-01..02) — `(micBuffer duration)` reads from default input device via PulseAudio capture (`PA_STREAM_RECORD` flag, parallel to existing playback path); auto-attenuates 20 dB on open to prevent feedback; returns `Buffer`. Captured `Buffer` composes with existing `mix`/`play`/`writeWav`/`granular` builtins. Sample-rate conversion to 44.1 kHz at capture-side (linear interpolation). Existing `flow-lang/Audio/PulseAudioSimpleBackend.cs` is PLAYBACK-only today — adds `pa_simple_new()` with `PA_STREAM_RECORD = 2` + `pa_simple_read()` P/Invoke binding.

4. **OSC server/client** (OSC-01..02) — `(oscListen port path handler)` server rate-limited to 200 Hz per path; handler is a Flow `(Args... => Void)` lambda. `(oscSend host port path arg1 arg2 ...)` client; OSC 1.0 type tags (`,f`/`,d`/`,i`/`,h`/`,s`/`,T`/`,F`). Uses `Rug.Osc 1.2.5` (zero deps, .NET Standard 2.0, OSC 1.0 complete). New `flow-lang/StandardLibrary/Network/OscFunctions.cs` builtin file (no existing network surface).

**In scope:** 11 requirements (LIVE-01..03, REPL-01..04, AUDIO-IN-01..02, OSC-01..02).

**Out of scope:** WASM live-coding-in-browser (Phase 41 consumes Phase 38's `live` block surface), real-time MIDI output (Phase 40), notation export (Phase 39 — already complete), MusicXML import (anti-feature lock, v1.6), strict OSC type-tag-by-arg surface (composer chose charitable inference per D-38-15 below), streaming audio input `(micStream callback)` (defer to v1.6 if composer demand surfaces; AUDIO-IN-01 is one-shot blocking `micBuffer duration` only), Ableton Link / JACK / MIDI clock (Phase 40), `setup { }` block (rejected as scope creep — single `live { }` block model is sufficient).

</domain>

<decisions>
## Implementation Decisions

### `live` Block Scope + Watch Mode Posture

- **D-38-01:** **No `live { }` block → whole-script hot-swap (drop-in).** `flow watch file.flow` on a script with no `live { }` block keeps the existing `LiveReloadManager` behavior — whole-script re-render at bar boundary with 64-sample crossfade. `live { }` becomes the OPTIONAL precision tool for finer-grained quantize. Composers with existing .flow files in `tests/` / `examples/` keep working without edits. **Migration burden: zero** per D-v1.5-01 pre-traction latitude.
- **D-38-02:** **Multiple `live` blocks per file → each swaps independently at its own quantize timeline.** Per-block pending-buffer slot + bar counter. Composer can mix `live 1bar { drums }` with `live 2bar { pad }`. The ANSI status panel (D-38-08) lists each active block with its quantize and last-swap-bar.
- **D-38-03:** **File-scope frozen when `live { }` exists.** Only the live block body re-evaluates on save. File-scope bindings (procs, vars, `loadSfz` calls, sections, musical-context blocks) execute ONCE at first run and are then frozen. Composer must restart `flow watch` to pick up file-scope changes. Mental model: opting into `live { }` opts into "performance lock" where setup doesn't change mid-set.
- **D-38-04:** **File-scope edits during session → one-shot stderr advisory.** When composer edits OUTSIDE any `live { }` block, emit `[live] file-scope edit detected outside live blocks at line N — restart `flow watch` to apply.` Dedup per `(filepath, line)` per process. NO auto-restart — preserves Pitfall #12 "live session never dies mid-set" lock. Matches Flow's stderr-advisory pattern (`[tuning]`/`[abc]`/`[mml]` precedents).
- **D-38-05:** **Existing `LiveReloadManager` debounce 500ms → 200ms.** Tighter responsiveness per LIVE-02 spec wording. PITFALLS.md Pitfall #21 specifically locks "200ms debounce" — already in REQUIREMENTS.
- **D-38-06:** **Existing 64-sample crossfade preserved unchanged.** Used by both the whole-script swap path (D-38-01) and per-`live`-block swap path (D-38-02). Composer-tunable defer to v1.6 if click-artifact reports surface.

### Live Block Recovery UX

- **D-38-07:** **30s timeout AND stale-closure detection → revert silently to previous buffer + dedup'd stderr advisory.** Consistent recovery UX across both failure modes. Playback continues with the last good buffer (no audible glitch). Advisory forms:
  - Timeout: `[live] evaluation timed out at 30s — keeping previous version`
  - Stale closure: `[live] stale closure: references removed binding '{name}' at line N — keeping previous version`
  - Parse / runtime error: `[live] {error message} at line N — keeping previous version` (already-shipped behavior in `LiveReloadManager.RenderScript`)
  Dedup per `(error_kind, line)` per process so a flurry of saves doesn't spam.
- **D-38-08:** **4-row ANSI live status panel** (modernized `flow watch`):
  1. `Tempo: 120 BPM | TimeSig: 4/4 | Bar: 47`
  2. `Live blocks: live 1bar @ L12 (last swap bar 47, 32s ago) | live 2bar @ L34 (...)` (one per active live block; collapse to single line if 1 block + no live blocks → omit row)
  3. `Voices: 8/32 | piano:3 brass:2 strings:3` (active voice count + per-instrument breakdown; reads from `VoiceAllocator`)
  4. `[live] last advisory line — auto-cleared after 8s` (sticky single-line for recent advisories; Claude's discretion picks 8s default)
  Panel rendered in place via ANSI cursor moves at TOP of terminal; redrawn at ~10 Hz. Plain-line fallback when stdout is not a TTY (piped/CI/etc.). Researcher decides exact ANSI escape sequences + redraw cadence.

### REPL Surface

- **D-38-09:** **`:help fn` meta-command form** (not bare `?fn`). Extends the existing `:quit`/`:help`/`:clear`/`:stop` family in `flow-interpreter/Repl.cs:210-220`. Bare `:help` shows the current help text; `:help transpose` prints signature + doc-comment + 1-line example from `BuiltInDocs`. Consistent with the existing meta-command grammar — composer doesn't learn two different ways to talk to the REPL. **NOTE:** REQUIREMENTS.md REPL-02 says `?transpose` literal form — this CONTEXT decision OVERRIDES the REQUIREMENTS wording at composer's direction (consistency with existing meta-commands wins over wording-literal). Update REQUIREMENTS.md REPL-02 wording at Plan 38-07 closer (per D-v1.5-01 single-commit migration).
- **D-38-10:** **Extend `(visualize seq)` with articulation glyphs + bar tick marks; `(inspect seq)` is a builtin-level alias.** The existing `flow-lang/StandardLibrary/VisualizationFunctions.cs` (~150 lines, grid + `#` chars + bar lines) gains: articulation glyphs (`>` Accent, `.` Staccato, `^` Marcato, `!` Sforzando, `_` Tenuto, `~` Legato per Phase 28 articulation enum) at note onsets, sharper bar tick marks. `(inspect seq)` calls into the same underlying renderer. Charitable to existing scripts that call `visualize`. **NOTE:** REQUIREMENTS.md REPL-04 says `(inspect seq)` is the surface — this CONTEXT decision ships BOTH names backed by one implementation. Update REQUIREMENTS.md REPL-04 wording at Plan 38-07 closer (per D-v1.5-01 single-commit migration) to "`(inspect seq)` / `(visualize seq)` alias pair".
- **D-38-11:** **Pull in a `ReadLine.NET`-style lightweight readline library** for Ctrl+R history search + multi-line editing + persistent history. New NuGet dep — researcher picks specifically among `ReadLine.NET` / `PrettyPrompt` / equivalent at Plan 38-XX with license + maintenance + .NET 10 compat check. License + maintenance check MANDATORY at plan-start (Flow currently has only Pidgin + DryWetMidi; Phase 38 adds 3 deps total — Rug.Osc + readline + Phase 40 RtMidi.Core — each scoped behind interface/module seam per STACK.md). Falls back to hand-rolled TUI line editor on `Console.ReadKey()` (~400-600 LOC) if no library passes the gate.
- **D-38-12:** **LSP embedding strategy: in-process via OmniSharp DI** per scout (`flow-lsp/Handlers/CompletionHandler.cs:95-144` `BuildItems()` is static and directly callable; no transport coupling). REPL spawns an in-memory `LanguageServer` instance replacing `Console.OpenStandardInput()` / `Console.OpenStandardOutput()` with `MemoryStream` pipes; calls `BuildItems()` on Tab. **Claude's discretion** on whether to share a single in-process LSP between REPL and active `flow watch` (probably yes, share via FlowEngine context) — researcher decides at plan time.

### OSC Type Tags + Behavior

- **D-38-13:** **`(oscSend ...)` uses charitable smallest-tag-that-fits inference.** Map by Flow type: Int → `,i` (32-bit signed), Long → `,h` (64-bit signed), Float → `,f`, Double → `,d`, String → `,s`, Bool → `,T`/`,F`, Buffer/Byte[] → `,b` (blob). Composer writes `(oscSend host port "/x" 1.5 "hello")` and Flow infers `,fs`. **NOTE:** REQUIREMENTS.md OSC-02 says "args explicitly typed... no implicit conversion" — this CONTEXT decision OVERRIDES the REQUIREMENTS wording in favor of Flow's D-v1.5-05 charitable interpretation default. Composer escape hatch: explicit cast at call site (e.g. `(oscSend host port "/x" (toLong 1) 1.5d)`) or named-arg `types=",hd"` override (researcher picks the exact escape-hatch syntax). Update REQUIREMENTS.md OSC-02 wording at Plan 38-07 closer (per D-v1.5-01 single-commit migration).
- **D-38-14:** **Rate-limit overflow = drop-newest, sample-and-hold semantics.** Within a 5ms window (1/200Hz), the FIRST message per path handled; subsequent ones dropped silently. Simplest implementation: per-path `_lastFireTime` timestamp gate before handler invocation. Composer-side smoothing recommended for jitter-sensitive use cases. No advisory on individual drops (would spam at flood); per-path "flood detected" one-shot advisory deferred to Claude's discretion at plan time.
- **D-38-15:** **Full bundle support both directions, timetag honored on receive.** Server auto-unpacks incoming bundles into their contained messages, dispatched in bundle order. Timetag in the future → schedule on a `TaskScheduler` queue (composer's clock); timetag `1` (immediately) → dispatch synchronously. Client: `(oscBundle msg1 msg2 ...)` returns a Bundle value; `(oscSendBundle host port bundle [timetag])` sends. Rug.Osc covers both directions natively — leverage its `OscBundle` type. Bundle nesting depth capped at 8 (T-38 DoS guard, mirroring Phase 36 T-36-17 / Phase 39 D-39-19 patterns).
- **D-38-16:** **OSC server lifecycle returns a handle** — `(oscListen port path handler)` returns an OscHandle value; `(oscStop handle)` cancels the listener. Background tasks: each `(oscListen ...)` spawns a `Task.Run` UDP receive loop. On `flow watch` exit / `flow` process termination, all handles are released. Multiple handlers per same path → list-all-and-broadcast (each handler fires in registration order). Address pattern wildcards (`/synth/*/freq`) deferred to v1.6 (literal-path match only in v1.5). IPv6 + multicast deferred to v1.6.

### Claude's Discretion (deferred to researcher / planner)

- Exact ANSI escape sequence cadence for the 4-row status panel (10 Hz redraw vs event-triggered? color palette per OS? graceful TTY-detection fallback details).
- Exact readline library pick among `ReadLine.NET` / `PrettyPrompt` / equivalent — license + maintenance + .NET 10 compat decides at plan-start. Hand-rolled fallback if no library passes the gate.
- Exact name and shape of the OSC type-tag escape hatch (`types=",hd"` named arg vs `(oscSendTyped host port "/x" ",hd" 1 1.5)` separate builtin vs `(asOscFloat 1.5)` per-arg wrapper). Researcher picks based on whether composer demand surfaces in early use.
- Auto-clear timeout for the sticky advisory row in the ANSI panel (proposed 8s default; tune at researcher's discretion based on terminal-flow ergonomics).
- LSP-in-process sharing between REPL and live `flow watch` (single LanguageServer instance vs per-process) — researcher decides based on cold-load timing.
- Exact name of the `(inspect seq)` / `(visualize seq)` alias pair backing builtin (`renderPianoRoll`? `pianoRollAscii`? Single source of truth).
- Whether `(oscListen ...)` is composer-blocking (loops forever in foreground) or returns immediately as handle (D-38-16 says returns handle). The roadmap success criteria says `(oscListen port path handler)` — implicit non-blocking handle return is the right reading; confirm at plan-start.
- PulseAudio capture stream device name — researcher picks default (`pulse_default`? composer-overridable?). Auto-attenuation: -20 dB constant during open vs ducking only when playback is also active (REQUIREMENTS says "on open" = unconditional; revisit only if composer reports headroom loss for offline `(writeWav (micBuffer 5s))` use).
- 200Hz overflow advisory shape — one-shot per path per process (`[osc] /fader/1: flood detected (rate-limit active)`) vs no advisory at all. Default leans no-advisory (sample-and-hold IS the expected behavior); researcher revisits if composer reports confusion.
- Plan breakdown — researcher / plan-checker decide how to slice ~5-7 plans. Suggested shape per sub-order in roadmap:
  1. Plan 38-01: modernized watch (LIVE-02) + ANSI status panel + 200ms debounce (rewrite `LiveReloadManager`)
  2. Plan 38-02: `live { }` block (LIVE-01) + parser/AST + multiple-block independent swap (consumes Plan 38-01 infrastructure)
  3. Plan 38-03: state preservation across reload (LIVE-03) + voice-pool name-key + PRNG reseed + stale-closure detection + 30s CancellationToken
  4. Plan 38-04: REPL polish (REPL-01..04) — in-process LSP + `:help` + `visualize` extension + readline lib
  5. Plan 38-05: audio input (AUDIO-IN-01..02) — PulseAudio `PA_STREAM_RECORD` P/Invoke + `micBuffer` builtin + 44.1kHz resample
  6. Plan 38-06: OSC (OSC-01..02) — Rug.Osc + `(oscSend)` charitable inference + `(oscListen)` handle + bundle support
  7. Plan 38-07: Closer (examples + 38-VERIFICATION.md + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep incl. REPL-02/REPL-04/OSC-02 wording updates per D-38-09/D-38-10/D-38-13)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 38 ROADMAP + Requirements
- `.planning/ROADMAP.md` §"Phase 38: Live Coding 2.0" — phase goal, success criteria, REQ list (LIVE-01..03, REPL-01..04, AUDIO-IN-01..02, OSC-01..02), Phase 41 dependency note
- `.planning/REQUIREMENTS.md` §"Live Coding 2.0", §"REPL Polish", §"Audio Input", §"OSC" (lines 89-110) — REQUIREMENT wording (treat as floor; D-38-* decisions in THIS file REFINE and in three cases OVERRIDE: D-38-09 REPL-02 form, D-38-10 REPL-04 surface naming, D-38-13 OSC-02 type-tag inference)
- `.planning/PROJECT.md` §"Constraints" — .NET 10, Linux primary, minimal dependencies, real-time audio constraints

### Locked v1.5 Milestone Decisions (apply to Phase 38)
- **D-v1.5-01:** Pre-traction no-deprecation latitude ACTIVE — breaking changes ship in single commits; in-repo migrators only. Justifies D-38-09 / D-38-10 / D-38-13 wording overrides on REQUIREMENTS.md.
- **D-v1.5-05:** Charitable interpretation default (warn + fall through to Void; opt-in strict via pragma). Foundation for D-38-13 OSC charitable inference, D-38-04 file-scope-edit advisory, D-38-07 timeout/stale-closure revert.
- **D-v1.5-06:** All PRNG routed through `flow-lang/Runtime/PrngRegistry.cs` (Phase 36 Plan 36-01). Phase 38 LIVE-03 PRNG reseed at swap boundary consumes the existing `ResetAtRenderBoundary()` API at line 122. Live block determinism opt-out reserved per D-v1.5-07 (currently 0 salt; future live-mode reseed key).
- **D-v1.5-07:** **Live block determinism opt-out** — `live { ... }` blocks emit a stderr advisory on every entry explicitly noting they opt OUT of the two-run cmp-clean determinism contract. 30s wall-clock evaluation cap + 200ms file-watch debounce + bar-boundary swap. CORE Phase 38 lock — D-38-05 / D-38-07 / D-38-08 / D-38-13 all derive from this.

### Phase 35 dependency-root surface (USED by Phase 38 indirectly)
- `.planning/phases/35-language-foundation/35-VERIFICATION.md` — LANG-01..04 + TEST-01..02 shipped state
- `.planning/phases/35-language-foundation/35-03-SUMMARY.md` — Rust-style DiagnosticRenderer (used for `[live]` advisory rendering with source-quoted span pointers when stale-closure includes the source line)
- `.planning/phases/35-language-foundation/35-05-SUMMARY.md` + `35-06-SUMMARY.md` — Pattern AST + MatchExpression (NOT directly consumed by Phase 38 but available)

### Phase 36 PrngRegistry (USED by D-38-07 LIVE-03 reseed)
- `flow-lang/Runtime/PrngRegistry.cs` — `(SourceLocation, generator-name)` keying + `ResetAtRenderBoundary()` API. Phase 38 calls `ResetAtRenderBoundary()` at each `live` block swap boundary per LIVE-03.

### Phase 31 BuiltInDocs (USED by D-38-09 REPL `:help`)
- `flow-lang/StandardLibrary/BuiltInDocs.cs` — 104 entries; static `_docs` dictionary; `TryGet(identifier)` API
- `flow-lsp/Handlers/HoverHandler.cs:46-65` — existing consumer pattern (REPL `:help` follows the same shape)

### v1.5 research (composer's source-of-truth picks)
- `.planning/research/STACK.md` Phase 38 row (line 19) + dedicated sections — `Rug.Osc 1.2.5` recommendation (MIT, zero deps, .NET Standard 2.0); PulseAudio P/Invoke extension for `PA_STREAM_RECORD`; OmniSharp Extensions LanguageServer in-process embedding pattern; readline library candidates
- `.planning/research/FEATURES.md` (lines 33, 52-54) — Phase 38 framing (Sonic Pi `live_loop` prior art + cue-quantized hot-swap as the modern bar); WASM playground dependency on `live { }` block
- `.planning/research/PITFALLS.md` — **Pitfall #10** (OSC type tags + flood rate; 200Hz rate limit), **Pitfall #12** (`live` block state preservation + 30s bailout — "live session never dies mid-set"), **Pitfall #13** (REPL completion partial parse — >80% rank-1 accuracy target), **Pitfall #21** (watch mode stale closure + race — 200ms debounce + stale-closure-detection test), **Pitfall #24** (audio input feedback + sample rate — default -20 dB attenuation + resampling fixture)
- `.planning/research/SUMMARY.md` — Phase 38 dependency-tree position (downstream of Phase 35; precedes Phase 41 WASM)

### Existing code (researcher MUST scout — Phase 38 is heavily reuse-driven)
- `flow-interpreter/LiveReloadManager.cs` (389 lines) — existing whole-script bar-boundary swap, 500ms debounce, 64-sample crossfade, FileSystemWatcher, capture-mode FlowEngine render. **REWRITE TARGET** for Plan 38-01 (LIVE-02 modernized watch). D-38-01 preserves the whole-script swap as the no-`live { }`-block default path; D-38-05 tightens debounce to 200ms.
- `flow-cli/Commands/WatchCommand.cs` (50 lines) — CLI entry point for `flow watch`; orchestrates LiveReloadManager
- `flow-interpreter/Repl.cs` (272 lines) — existing REPL with single-line / explicit-backslash continuation / paren-balanced multi-line / `:quit`/`:help`/`:clear`/`:stop` meta-commands. **EXTENSION TARGET** for Plan 38-04 (REPL-01..04). D-38-09 adds `:help fn` to the meta-command family; D-38-11 swaps the underlying line-input.
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` — existing context-block AST pattern (`Timesig`/`Tempo`/`Key`/`Swing`/`VoicePool`/`Tuning`/etc.). **PATTERN ANALOG** for Plan 38-02 (LIVE-01 `live` block AST). Per Phase 28/32 precedents, the new `LiveBlockStatement` mirrors this shape with body re-evaluation semantics.
- `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` (lines 124-169) — Phase 28 voice-pool with steal-oldest policy, `LastPoolSizeUsedForTests` instrumentation. **READ TARGET** for Plan 38-03 (LIVE-03 voice-pool state preservation across live reload; voice name keys the preservation).
- `flow-lang/Runtime/PrngRegistry.cs` (line 122 `ResetAtRenderBoundary()`) — Phase 36 single PRNG source-of-truth. Plan 38-03 calls reset at each live-block swap boundary per LIVE-03.
- `flow-lang/StandardLibrary/VisualizationFunctions.cs` (~150 lines) — existing `(visualize seq)` builtin (grid + `#` chars + bar lines). **EXTENSION TARGET** for Plan 38-04 (REPL-04 per D-38-10 — adds articulation glyphs + bar tick marks; ships `(inspect seq)` as alias).
- `flow-lang/StandardLibrary/BuiltInDocs.cs` — Phase 31 LSP doc table (104 entries). `:help fn` reads this directly via `TryGet(identifier)`.
- `flow-lsp/Handlers/CompletionHandler.cs` (lines 38-456, `BuildItems()` at 95-144) — static + transport-decoupled; **EMBEDDABLE IN-PROCESS** for Plan 38-04 (REPL-01).
- `flow-lang/Audio/PulseAudioSimpleBackend.cs` (~310 lines) — existing PLAYBACK-only P/Invoke surface. **EXTENSION TARGET** for Plan 38-05 (AUDIO-IN-01 — add `PA_STREAM_RECORD = 2` constant + `pa_simple_read()` binding + capture surface).
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` (and sibling Audio/*.cs files) — builtin registration pattern; AUDIO-IN-01 follows. Suggested new file: `flow-lang/StandardLibrary/Audio/InputFunctions.cs`.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — registry hub; new files call `static Register(InternalFunctionRegistry registry)` and are wired here.
- `flow-lang/flow-lang.csproj` — Phase 38 adds 2 new `<PackageReference>`: `Rug.Osc` 1.2.5 (OSC) + readline library (TBD per D-38-11). Adds `flow-lsp` ProjectReference for in-process embedding per D-38-12.

### Articulation glyph mapping (for D-38-10 visualize extension)
- Phase 28 `Articulation` enum: Accent → `>`, Staccato → `.`, Marcato → `^`, Tenuto → `_`, Sforzando → `!`, Legato → `~` (rendered as overline between connected notes), Normal → no glyph

### OSC type-tag mapping (for D-38-13 charitable inference)
- Int → `,i` (32-bit signed)
- Long → `,h` (64-bit signed)
- Float → `,f`
- Double → `,d`
- String → `,s`
- Symbol → `,s` (interned identity collapses to string on the wire)
- Bool true → `,T` ; Bool false → `,F`
- Buffer/Byte[] → `,b` (blob)
- Nil → `,N` (no-arg OSC marker, primarily for receive-side; outgoing requires explicit composer intent)

### Examples to ship (composer-facing tutorial chapters; mirrors Phase 36/39 chapter pattern)
- `examples/live/hello_live.flow` (new) — minimal `live 1bar { ... }` block with one synth voice; save-edit demonstrates hot-swap
- `examples/live/multi_block.flow` (new) — `live 1bar { drums }` + `live 2bar { pad }` showcasing independent swap timelines
- `examples/live/repl_session.md` (new) — narrated REPL transcript demonstrating `:help transpose`, `(inspect seq)`, Tab completion
- `examples/live/mic_granular.flow` (new) — `(micBuffer 4s) -> (granular ...) -> play` demonstrating AUDIO-IN composing with DSP-01
- `examples/live/osc_controller.flow` (new) — `(oscListen 7777 "/fader/1" handler)` + `(oscSend "localhost" 7777 "/fader/1" 0.5)` round-trip demo

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`flow-interpreter/LiveReloadManager.cs`** (389 lines, existing watch infrastructure) — Bar-boundary detection (`CheckBarBoundary` at line 230), micro-crossfade (`ApplyCrossfade` at line 251, 64-sample equal-power), debounced render trigger (`TriggerBackgroundRender` at line 274, currently 500ms → 200ms), capture-mode FlowEngine render (`RenderScript` at line 328). Plan 38-01 rewrites the ORCHESTRATION (status panel + multi-block tracking + 200ms debounce + 30s CancellationToken) while keeping the bar-boundary + crossfade + render-on-error-keep-previous primitives.
- **`flow-lang/Runtime/PrngRegistry.cs`** (Phase 36, single source-of-truth for PRNG) — `(SourceLocation, generator-name)` keyed; `ResetAtRenderBoundary()` API at line 122. Plan 38-03 invokes at each `live` block swap boundary per LIVE-03 (PRNG state reseeded → deterministic stream per swap given source location unchanged).
- **`flow-lang/StandardLibrary/Audio/VoiceAllocator.cs`** (Phase 28, ~170 lines) — Voice pool with steal-oldest policy + `LastPoolSizeUsedForTests` instrumentation. Plan 38-03 adds voice-name-keyed preservation: on swap, voices whose name (e.g., "piano:0", "drums:1") survives the new buffer's voice list inherit the previous voice state; voices not in the new list are released.
- **`flow-lang/StandardLibrary/VisualizationFunctions.cs`** (~150 lines) — Existing `(visualize seq)` + `(visualize buf)` builtins. Plan 38-04 extends with articulation glyphs (Phase 28 enum mapping per `<canonical_refs>` above) + sharper bar tick marks + makes `(inspect seq)` an alias.
- **`flow-lang/StandardLibrary/BuiltInDocs.cs`** + **`flow-lsp/Handlers/HoverHandler.cs:46-65`** — Phase 31 LSP doc table (104 entries) + existing `TryGet(identifier)` consumer. Plan 38-04 `:help fn` is the SECOND consumer of the same table.
- **`flow-lsp/Handlers/CompletionHandler.cs`** (lines 38-456) — Static `BuildItems()` method (lines 95-144), no transport coupling. Plan 38-04 embeds an in-process LSP via OmniSharp DI with `MemoryStream` pipes replacing console I/O, and calls `BuildItems()` directly on Tab keypress.
- **`flow-lang/Audio/PulseAudioSimpleBackend.cs`** (~310 lines) — PLAYBACK-only P/Invoke surface; `pa_simple_new` at line 275 hardcodes `PA_STREAM_PLAYBACK = 1`. Plan 38-05 adds `PA_STREAM_RECORD = 2` constant + `pa_simple_read()` P/Invoke binding + new capture surface in a sibling class (or extends in place — researcher's call).
- **`flow-lang/Ast/Statements/MusicalContextStatement.cs`** — Established pattern for new context-block AST nodes (Timesig/Tempo/Key/Swing/VoicePool/Tuning). Plan 38-02 `LiveBlockStatement` mirrors this shape (body + quantize + source span).

### Established Patterns
- **Charitable interpretation default (D-v1.5-05)** — All Phase 38 advisory surfaces follow this. Stale closure, timeout, file-scope edit, OSC overflow, parse error inside `live` block: revert + dedup'd stderr advisory, never throw. The `live { }` block opt-out from determinism (D-v1.5-07) is explicit but the recovery model stays charitable.
- **One-shot stderr advisories with dedup** — `RenderingDiagnostics.WarnOnce` keyed on a sentinel. Phase 38 advisories use sentinels like `f"live-fscope-edit:{filepath}:{line}"` / `f"live-timeout:{line}"` / `f"live-stale-closure:{name}:{line}"` so identical failure points dedup per process.
- **Reference-identity value types backed by an established type registry** — Phase 32 `Tuning`, Phase 33 `Sfz`, Phase 36 `MarkovModel`/`LsystemModel`. Phase 38 `OscHandle` (from D-38-16) follows the same shape; `Buffer` (from `(micBuffer)`) reuses the existing `AudioBuffer` value type.
- **Stdlib module activation via `use "@name"`** — Phase 33 `@sfz`, Phase 36 `@patterns`/`@generative`/`@improv`, Phase 39 `@notation-io`. Phase 38: `@osc` (or `@network`) for OSC; mic/REPL/live builtins likely register globally (no `use` gate) since they're core composer infrastructure. Researcher decides activation pattern at plan time.
- **Two-run cmp-clean determinism, EXPLICITLY opted-out by `live { }`** — Phase 18/25/27/28/29/33/36/37 inheritance. Phase 38: offline render paths (`writeWav` / `writeMidi`) STAY deterministic. `flow watch` + `live { }` blocks ARE explicitly NOT deterministic (D-v1.5-07 stderr advisory at every entry). The composer-facing examples under `examples/live/` therefore use offline render assertions where applicable; live-mode hot-swap demos are documentation-only (no cmp-clean assertion).
- **Phase 28 voice-pool state mutability** — Voices are mutable in place (truncated via `TruncateVoiceBuffer`); state is NOT preserved across full renders today. Plan 38-03 adds NAME-keyed preservation specifically for the `live` block swap path; offline render path unchanged.

### Integration Points
- **`FlowEngine.Execute(source, filePath)`** — Composer-facing API; Plan 38-01 wraps in CancellationToken with 30s deadline for live re-renders. Plan 38-03 hooks `engine.Context.GetMusicalContext()` and `VoiceAllocator.LastPoolSizeUsedForTests` introspection to drive the status panel + voice-preservation.
- **`AudioPlaybackManager.WriteChunk(buffer, position, chunkSize, sampleRate, channels)`** — Streaming output path; Plan 38-01 keeps this for hot-swap playback. Plan 38-05 adds a parallel `ReadChunk(...)` path for `PA_STREAM_RECORD` capture.
- **Parser entry — `flow-lang/Parsing/Parser.cs`** — Plan 38-02 adds `LiveBlockStatement` parse rule (`live <quantize-expr> { body }` analogous to `tempo <bpm> { body }`). Quantize accepts `NoteValue` literal (`q`/`h`/`w`/etc.) or `Bar` literal — existing types per Music Types Quick Reference in CLAUDE.md.
- **CLI entry — `flow-cli/Commands/WatchCommand.cs`** — Plan 38-01 invokes the modernized watch manager; signature stays composer-compatible.
- **`flow test` (Phase 35 TEST-01)** — Phase 38 ships tests under `tests/test_live_*.flow`, `tests/test_repl_*.flow`, `tests/test_audio_in_*.flow`, `tests/test_osc_*.flow`. Live-mode tests use the existing capture-mode FlowEngine path (no real wall-clock dependency); OSC tests use loopback (`127.0.0.1:0` for ephemeral port + assert message round-trip); audio-input tests use a fixture WAV file fed through the capture path (no real mic dependency in CI).
- **`@osc` module init** — FlowEngine registers `(oscSend)` / `(oscListen)` / `(oscStop)` / `(oscBundle)` / `(oscSendBundle)` conditionally on `use "@osc"`. Rug.Osc lazily loads on first use to keep `flow-lang.dll` cold-load time unchanged for composers who don't use OSC.

</code_context>

<specifics>
## Specific Ideas

- **REQUIREMENTS.md wording overrides (D-v1.5-01 single-commit migration justifies)** — Plan 38-07 closer MUST sweep REQUIREMENTS.md to update REPL-02 (`:help fn` not `?fn`), REPL-04 (`(inspect seq)` AND `(visualize seq)` alias pair), OSC-02 (charitable inference, not strict-tag-by-arg). These overrides are recorded in D-38-09 / D-38-10 / D-38-13 for traceability.
- **"Live session never dies mid-set" (PITFALLS Pitfall #12)** is the highest-stakes Phase 38 lock. Every recovery path — timeout, stale-closure, parse error, OSC flood, audio backend failure — biases toward CONTINUE PLAYING + EMIT ADVISORY rather than throw/halt.
- **ANSI status panel content commits (D-38-08)** all four rows: tempo+sig+bar, active live blocks, voices+instruments, sticky advisory. Plain-line fallback when stdout is not a TTY — important for CI / piped use where ANSI escape sequences would garble logs.
- **Charitable OSC type-tag inference (D-38-13)** overrides REQUIREMENTS-as-written. The composer's reasoning: Flow's overall posture (D-v1.5-05) wins over OSC-spec literal-mindedness for the common case; explicit-cast escape hatch handles the fussy-receiver case.
- **Single readline library decision (D-38-11)** — researcher picks ONE specifically among `ReadLine.NET` / `PrettyPrompt` / equivalent at plan-start with license + maintenance + .NET 10 compat gate. No hand-roll-AND-library hybrid; library OR hand-roll fallback, not both.
- **In-process LSP embed via OmniSharp DI (D-38-12)** — the scout report (`flow-lsp/Handlers/CompletionHandler.cs:95-144` `BuildItems()` static + transport-decoupled) confirms feasibility. No new dep — `flow-interpreter` adds a ProjectReference to `flow-lsp` and consumes the existing assembly.
- **`examples/live/` directory mirroring `examples/notation/` chapter pattern** (Phase 39 precedent) — 5 chapters covering each surface (live block, REPL session as narrated MD, mic+granular, OSC round-trip, multi-block). All `*.flow` chapters pass `flow test` smoke checks where applicable.

</specifics>

<deferred>
## Deferred Ideas

- **Streaming audio input `(micStream callback)`** — out of scope for Phase 38 v1.5. AUDIO-IN-01/02 lock to one-shot blocking `(micBuffer duration)`. Revisit in v1.6 if composer demand surfaces (likely for granular-from-mic real-time use).
- **`setup { }` block** (sibling to `live { }` for expensive one-time setup like SFZ loads) — rejected as scope creep. Composer's mental model (D-38-03 file-scope-frozen-when-live-exists) covers the use case: put `loadSfz` at file scope, it runs once.
- **Composer-tunable micro-crossfade length** (currently 64 samples) — defer to v1.6 if click-artifact reports surface.
- **OSC address pattern wildcards** (`/synth/*/freq`) — v1.5 ships literal-path match only; wildcards defer to v1.6.
- **OSC IPv6 + multicast support** — defer to v1.6.
- **OSC server-side authentication / TLS** — not in OSC 1.0 spec; defer to v1.6+ if composer demand surfaces (likely never for music-production use).
- **Hand-rolled TUI line editor on `Console.ReadKey()`** — only ships if D-38-11 readline library gate fails. Reserved as fallback, not parallel surface.
- **Auto-restart on file-scope edit** — explicitly rejected (D-38-04) per Pitfall #12 "live session never dies mid-set" lock. Could ship as `--auto-restart` flag in v1.6 if composer demand surfaces.
- **OSC bundles with nesting depth > 8** — capped by D-38-15 DoS guard. Deeper nests collapse with stderr advisory.
- **Web MIDI in REPL completion** — out of scope (Phase 40 / Phase 41 territory).
- **REPL syntax highlighting** — not part of REPL-01..04. Could ship as `PrettyPrompt` side-benefit if D-38-11 picks that library, but not load-bearing.
- **Composer-facing pause/resume hotkey for `flow watch`** — not in LIVE-* scope. Composer can SIGINT (single Ctrl+C silences playback, second exits, per existing `LiveReloadManager:104-116`).

</deferred>

---

*Phase: 38-live-coding-2-0*
*Context gathered: 2026-05-23*
