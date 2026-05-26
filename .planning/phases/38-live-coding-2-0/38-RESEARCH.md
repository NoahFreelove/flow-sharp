# Phase 38: Live Coding 2.0 — Research

**Researched:** 2026-05-23
**Domain:** Live-coding language runtime (hot-swap interpreter + REPL + audio I/O + OSC network protocol)
**Confidence:** HIGH (all integration points scouted in-tree; external dep API surfaces verified live on NuGet / FreeDesktop / GitHub)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**`live` Block Scope + Watch Mode Posture**

- **D-38-01:** No `live { }` block → whole-script hot-swap (drop-in). `flow watch file.flow` on a script with no `live { }` block keeps the existing `LiveReloadManager` behavior — whole-script re-render at bar boundary with 64-sample crossfade. `live { }` becomes the OPTIONAL precision tool for finer-grained quantize. Composers with existing .flow files in `tests/` / `examples/` keep working without edits. **Migration burden: zero** per D-v1.5-01 pre-traction latitude.
- **D-38-02:** Multiple `live` blocks per file → each swaps independently at its own quantize timeline. Per-block pending-buffer slot + bar counter. Composer can mix `live 1bar { drums }` with `live 2bar { pad }`. The ANSI status panel (D-38-08) lists each active block with its quantize and last-swap-bar.
- **D-38-03:** File-scope frozen when `live { }` exists. Only the live block body re-evaluates on save. File-scope bindings (procs, vars, `loadSfz` calls, sections, musical-context blocks) execute ONCE at first run and are then frozen. Composer must restart `flow watch` to pick up file-scope changes. Mental model: opting into `live { }` opts into "performance lock" where setup doesn't change mid-set.
- **D-38-04:** File-scope edits during session → one-shot stderr advisory. When composer edits OUTSIDE any `live { }` block, emit `[live] file-scope edit detected outside live blocks at line N — restart 'flow watch' to apply.` Dedup per `(filepath, line)` per process. NO auto-restart — preserves Pitfall #12 "live session never dies mid-set" lock.
- **D-38-05:** Existing `LiveReloadManager` debounce 500ms → 200ms. Tighter responsiveness per LIVE-02 spec wording.
- **D-38-06:** Existing 64-sample crossfade preserved unchanged. Used by both the whole-script swap path (D-38-01) and per-`live`-block swap path (D-38-02).

**Live Block Recovery UX**

- **D-38-07:** 30s timeout AND stale-closure detection → revert silently to previous buffer + dedup'd stderr advisory. Consistent recovery UX across both failure modes. Playback continues with the last good buffer.
- **D-38-08:** 4-row ANSI live status panel (modernized `flow watch`): row 1 musical context, row 2 live blocks, row 3 voice pool, row 4 sticky advisory. Plain-line fallback when stdout is not a TTY. Researcher decides exact ANSI escape sequences + redraw cadence.

**REPL Surface**

- **D-38-09:** `:help fn` meta-command form (not bare `?fn`). Extends the existing `:quit`/`:help`/`:clear`/`:stop` family. Bare `:help` shows the current help text; `:help transpose` prints signature + doc-comment + 1-line example from `BuiltInDocs`. **OVERRIDES REQUIREMENTS.md REPL-02 wording** at composer's direction; update at Plan 38-01.
- **D-38-10:** Extend `(visualize seq)` with articulation glyphs + bar tick marks; `(inspect seq)` is a builtin-level alias. **OVERRIDES REQUIREMENTS.md REPL-04 wording**; update at Plan 38-01.
- **D-38-11:** Pull in a `ReadLine.NET`-style lightweight readline library for Ctrl+R history search + multi-line editing + persistent history. New NuGet dep — researcher picks specifically among `ReadLine.NET` / `PrettyPrompt` / equivalent at plan-start with license + maintenance + .NET 10 compat check. Falls back to hand-rolled TUI line editor on `Console.ReadKey()` (~400-600 LOC) if no library passes the gate.
- **D-38-12:** LSP embedding strategy: in-process via OmniSharp DI per scout (`flow-lsp/Handlers/CompletionHandler.cs:95-144` `BuildItems()` is static and directly callable). REPL spawns an in-memory LSP instance replacing `Console.OpenStandardInput()` with `MemoryStream` pipes; calls `BuildItems()` on Tab.

**OSC Type Tags + Behavior**

- **D-38-13:** `(oscSend ...)` uses charitable smallest-tag-that-fits inference. **OVERRIDES REQUIREMENTS.md OSC-02 wording**; update at Plan 38-01.
- **D-38-14:** Rate-limit overflow = drop-newest, sample-and-hold semantics. Within 5ms (1/200Hz) window, FIRST message per path handled; subsequent ones dropped silently.
- **D-38-15:** Full bundle support both directions, timetag honored on receive. Bundle nesting depth capped at 8 (T-38 DoS guard).
- **D-38-16:** OSC server lifecycle returns a handle — `(oscListen port path handler)` returns an OscHandle value; `(oscStop handle)` cancels the listener.

### Claude's Discretion (deferred to researcher / planner)

- Exact ANSI escape sequence cadence for the 4-row status panel.
- Exact readline library pick among ReadLine.NET / PrettyPrompt / equivalent.
- Exact name and shape of the OSC type-tag escape hatch (`types=",hd"` named arg vs `(oscSendTyped ...)` separate builtin vs `(asOscFloat 1.5)` per-arg wrapper).
- Auto-clear timeout for the sticky advisory row (proposed 8s default).
- LSP-in-process sharing between REPL and live `flow watch` (single vs per-process).
- Exact name of the `(inspect seq)` / `(visualize seq)` alias pair backing builtin.
- Whether `(oscListen ...)` is blocking or returns immediately as handle (D-38-16 says handle).
- PulseAudio capture stream device name (default? composer-overridable?). Auto-attenuation: -20 dB constant during open.
- 200Hz overflow advisory shape — one-shot per path per process vs no advisory at all.
- Plan breakdown — researcher / plan-checker decide how to slice ~5-7 plans (suggested 7-plan shape in CONTEXT).

### Deferred Ideas (OUT OF SCOPE)

- Streaming audio input `(micStream callback)`.
- `setup { }` block (sibling to `live { }`).
- Composer-tunable micro-crossfade length.
- OSC address pattern wildcards (`/synth/*/freq`).
- OSC IPv6 + multicast.
- OSC server-side authentication / TLS.
- Hand-rolled TUI line editor (only ships if readline gate fails).
- Auto-restart on file-scope edit.
- OSC bundles with nesting depth > 8.
- Web MIDI in REPL completion.
- REPL syntax highlighting (could ship as PrettyPrompt side-benefit).
- Composer-facing pause/resume hotkey for `flow watch`.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LIVE-01 | `live <quantize> { ... }` block — auto-loops the block; hot-swaps content at the next quantize unit boundary (default `1bar`). Quantize unit accepts `NoteValue` (`q`/`h`/`w`/etc.) or `Bar`. Explicit opt-out from determinism contract with stderr advisory at every entry. | §A `LiveBlockStatement` AST analog of `MusicalContextStatement`; §B parser keyword dispatch at Parser.cs:104-171; §F per-block pending-buffer slot extending `LiveReloadManager._pendingBuffer` to `Dictionary<int, float[]>` keyed by block id. |
| LIVE-02 | Modernized watch mode (rewrite of existing `flow --watch`) — ANSI live status panel, structured stderr (`[live]` prefix on advisory, `[error]` on parse fail), 30s wall-clock evaluation cap with CancellationToken, 200ms file-watch debounce. | §F ANSI escape sequence catalog (cursor save/restore, line clear, color); §G CancellationToken plumbing via `Task.Run(() => engine.Execute(...), ct.Token).Wait(TimeSpan.FromSeconds(30))`; rewrites `LiveReloadManager.TriggerBackgroundRender` (line 274). |
| LIVE-03 | State preservation across live reload — voice-pool state preserved IF voice name still exists post-edit; musical context stack reset to file-scope; PRNG state reseeded at swap boundary; stale-closure detection. | §C name-keyed voice diff against `VoiceAllocator` output; §D `PrngRegistry.ResetAtRenderBoundary()` call at swap boundary; §E AST-walk closure capture inspection. |
| REPL-01 | LSP-backed tab completion — REPL embeds `flow-lsp` in-process and queries `CompletionHandler` for the current line. Token-heuristic fallback when partial-parse fails. | §I `BuildItems()` is **already a pure static** method with no transport coupling — REPL constructs the 4 indices (`BuiltInIndex` / `StdlibSymbolIndex` / `KeywordIndex` / `UserSymbolIndex`) DIRECTLY without needing OmniSharp `LanguageServer.From()` plumbing. |
| REPL-02 | `:help fn` meta-command (per D-38-09 override) — prints signature + doc-comment + 1-line example from `BuiltInDocs` (104 entries). | §K `BuiltInDocs.TryGet(identifier)` returns `Doc(Summary, IReadOnlyList<ParamDoc>)`; add to `HandleCommand` switch at Repl.cs:212. Hover handler precedent at `flow-lsp/Handlers/HoverHandler.cs:46-65`. |
| REPL-03 | Multi-line editing + history search — Ctrl+R history search; multi-line input via continuation prompt; persistent history at `~/.config/flow/history`. | §J `PrettyPrompt 4.1.1` recommended (active maintenance, MPL-2.0, history filtering, multi-line, .NET 6+ ⇒ .NET 10 compatible); ReadLine.NET is the fallback (last 2018, no Ctrl+R, MIT). |
| REPL-04 | Pretty piano-roll on `(inspect seq)` — ASCII piano-roll with pitch on Y axis, time on X axis; tick marks at bar boundaries; articulation glyphs at note onsets. | §L UI-SPEC glyph inventory pre-locked (`>` `.` `^` `_` `!` `~`); extend `VisualizationFunctions.Visualize` at lines 117-131 (note placement loop). |
| AUDIO-IN-01 | `(micBuffer duration)` reads from default input device via PulseAudio capture (`PA_STREAM_RECORD` flag, parallel to existing playback path). Auto-attenuates 20 dB on open. Returns `Buffer`. | §M `PA_STREAM_RECORD = 2` (verified from `pulseaudio/src/pulse/def.h` enum order); §N `pa_simple_read(IntPtr, IntPtr, nuint, out int)` P/Invoke mirror of existing `pa_simple_write`. |
| AUDIO-IN-02 | Captured `Buffer` composes with existing `mix`/`play`/`writeWav`/`granular` builtins. Sample-rate conversion to 44.1 kHz at capture-side (linear interpolation). | §O `AudioBuffer` type already shared between playback + new capture path; §O resampler is ~30 LOC linear interpolation (matches the existing `loadWav` resample idiom). |
| OSC-01 | `(oscListen port path handler)` server rate-limited to 200 Hz per path. Handler is a Flow `(Args... => Void)` lambda. | §P Rug.Osc 1.2.5 `OscReceiver.Receive()` blocking loop pattern; rate-limit gate via per-path `Dictionary<string, long> _lastFireTimeMs`. |
| OSC-02 | `(oscSend host port path arg1 arg2 ...)` with charitable smallest-tag-that-fits inference (per D-38-13 override). Uses Rug.Osc 1.2.5. | §Q `Value.Type` → OSC type tag dispatch table; §Q `OscMessage(address, params object[])` constructor accepts CLR-typed args. |
</phase_requirements>

## Summary

Phase 38 ships four loosely-coupled surfaces — a language construct (`live { }` block), a watch-mode rewrite, REPL polish (tab/help/history/piano-roll), audio input via PulseAudio capture, and an OSC server/client. The single highest-stakes thread is **"live session never dies mid-set"** (Pitfall #12) — every failure mode for `live { }` block evaluation, OSC flood, audio backend hiccup, or REPL parse error must continue playing the last-good audio while emitting a dedup'd stderr advisory.

The good news: every surface has a strong existing analog in-tree. The `live` block AST mirrors `MusicalContextStatement` exactly; the watch rewrite extends `LiveReloadManager` orthogonally (status panel as overlay; debounce constant change; per-block pending-buffer slot); the REPL's LSP-backed completion is materially simpler than CONTEXT framed it — `CompletionHandler.BuildItems()` is **already a pure static** method that needs only the 4 symbol indices, NOT a full OmniSharp `LanguageServer` instance with `MemoryStream` pipes (D-38-12 can be SIMPLIFIED at plan time); the PulseAudio capture path is a mechanical P/Invoke mirror of the playback path; the OSC surface is a single `OscFunctions.cs` file consuming `Rug.Osc 1.2.5` (5 builtins, ~250 LOC). All three new NuGet dependencies (`Rug.Osc 1.2.5`, `PrettyPrompt 4.1.1`, and a flow-interpreter→flow-lsp `<ProjectReference>`) are verified live on NuGet with permissive licenses (MIT-style / MPL-2.0 / first-party).

The one composer-facing decision that needs sharper framing is the **REPL line-editor pick** — `PrettyPrompt 4.1.1` (Sept 2023, MPL-2.0, multi-line+history+autocomplete) is the clear winner over `ReadLine 2.0.1` (June 2018, MIT, no Ctrl+R, no multi-line) because Ctrl+R reverse-history search is required by REPL-03 and PrettyPrompt ships it as a first-class feature. The plan should NOT consider hand-rolling unless PrettyPrompt's MPL-2.0 license raises a concern (it doesn't for Flow — MPL is file-scope copyleft, compatible with MIT consumers).

**Primary recommendation:** Pin `Rug.Osc 1.2.5` + `PrettyPrompt 4.1.1` + add `flow-lsp` as a `<ProjectReference>` from `flow-interpreter`. Rewrite `LiveReloadManager` orthogonally (preserve bar-boundary + 64-sample crossfade primitives; replace orchestration with ANSI panel + multi-block tracking + 30s `CancellationTokenSource` + 200ms debounce). Add `LiveBlockStatement` AST mirroring `MusicalContextStatement`, plus `live` keyword to `TokenType` + lexer keyword table + parser dispatch at the keyword-block site. Voice preservation = name-key Dict diff against current `VoiceAllocator` output. Stale-closure detection = AST-walk that compares each `LambdaExpression.CapturedNames` against the new file-scope frame. PRNG reseed = single call to `engine.Context.PrngRegistry.ResetAtRenderBoundary()` in the swap callback. PulseAudio capture extends `PulseAudioSimpleBackend` with `PA_STREAM_RECORD = 2` constant + `pa_simple_read` P/Invoke binding (~50 LOC mirror of playback path). `OscFunctions.cs` registers 5 builtins (`oscSend`, `oscListen`, `oscStop`, `oscBundle`, `oscSendBundle`); type-tag inference via `Value.Type` switch matches the `Value` factory shape exactly.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `live <quantize> { body }` parsing | flow-lang Parser | flow-lang AST | New `LiveBlockStatement` mirrors `MusicalContextStatement` shape exactly; keyword dispatch lives in `Parser.ParseStatement()` |
| `live` block re-evaluation orchestration | flow-interpreter `LiveReloadManager` rewrite | flow-lang FlowEngine | Already a flow-interpreter responsibility; FlowEngine.Execute is the per-render unit |
| Voice-pool name-keyed preservation | flow-lang `VoiceAllocator` (new method) | flow-interpreter swap callback | Voice list lives in flow-lang; flow-interpreter requests "diff against previous" at swap time |
| PRNG reseed on swap | flow-lang `PrngRegistry.ResetAtRenderBoundary()` | flow-interpreter swap callback | Reuses the Phase 36 boundary API verbatim |
| Stale-closure detection | flow-lang Interpreter (AST walk helper) | flow-interpreter swap callback | AST is flow-lang-owned; new `LambdaCaptureAuditor` helper |
| 30s CancellationToken plumbing | flow-interpreter (wrapper around `engine.Execute`) | flow-lang (no API change) | Wall-clock cap is wall-clock-owned by orchestrator; `Task.Run(...).Wait(ts)` pattern needs no engine changes |
| ANSI status panel rendering | flow-interpreter (new `LiveStatusPanel.cs`) | n/a | Pure terminal I/O |
| TTY-detection fallback | flow-interpreter (`Console.IsOutputRedirected` check) | n/a | Already a standard .NET API |
| REPL line editor (Tab / Ctrl+R / multi-line) | flow-interpreter `Repl.cs` rewrite (`PrettyPrompt`) | n/a | UI-only concern |
| REPL Tab completion (LSP-backed) | flow-interpreter REPL → flow-lsp `CompletionHandler.BuildItems()` static | flow-lsp Symbols namespace | The 4 indices are pure (no LSP transport needed); just instantiate them once at REPL init |
| `:help fn` lookup | flow-interpreter REPL → flow-lang `BuiltInDocs.TryGet()` | n/a | Phase 31 doc table already in flow-lang |
| `(inspect seq)` / `(visualize seq)` extension | flow-lang `VisualizationFunctions` | n/a | Existing builtin file; add articulation glyph branch in note-placement loop |
| PulseAudio capture P/Invoke | flow-lang `PulseAudioSimpleBackend` extension | flow-lang new `PulseAudioCaptureBackend` (sibling class option) | Mirrors existing playback P/Invoke pattern |
| `(micBuffer)` builtin | flow-lang `StandardLibrary/Audio/InputFunctions.cs` (new) | flow-lang `BuiltInFunctions.cs` registration | Standard new-builtin pattern |
| 44.1kHz sample-rate conversion | flow-lang `InputFunctions.cs` (linear interp ~30 LOC) | n/a | Capture-side resample before returning Buffer |
| OSC `Rug.Osc` adapter | flow-lang `StandardLibrary/Network/OscFunctions.cs` (new) | flow-lang `BuiltInFunctions.cs` registration | New top-level Network namespace; only Phase 38 file lives there in v1.5 |
| OSC type-tag inference | flow-lang `OscFunctions.cs` (Value.Type → CLR-typed `OscMessage` args) | n/a | Single dispatch site |
| OSC rate-limit gate | flow-lang `OscFunctions.cs` (per-path `_lastFireTimeMs`) | n/a | Lives next to handler invocation |
| OSC server lifecycle (handle) | flow-lang `OscFunctions.cs` (`OscHandle` value type) | flow-lang `Value.cs` (new factory) | Reference-identity value type per Phase 32 `Tuning` / Phase 33 `Sfz` / Phase 36 `MarkovModel` precedent |
| Live-mode tests | flow-lang.Tests (existing capture-mode FlowEngine harness) | flow-interpreter.Tests for panel rendering | Capture mode skips real audio; ANSI panel tested via string-buffer redirect |
| OSC tests | flow-lang.Tests (UDP loopback `127.0.0.1:0`) | n/a | Tests run in same process — bind ephemeral port, send + recv |
| Audio-input tests | flow-lang.Tests (fixture WAV fed through capture path) | n/a | No real mic in CI — `IAudioBackend` capture-mode hook reads WAV instead |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET | 10.0.107 | Runtime [VERIFIED: `dotnet --version`] | Existing — no change |
| C# 13 | Latest | Language | Existing — records / pattern matching / file-scoped namespaces |
| `Rug.Osc` | 1.2.5 | OSC 1.0 client + server, bundle support, all type tags, UDP send/recv [VERIFIED: NuGet API `api.nuget.org/v3-flatcontainer/rug.osc/index.json` returns versions list ending in `"1.2.5"`] [CITED: https://www.nuget.org/packages/Rug.Osc] | Zero dependencies; .NET Standard 2.0 (compatible with .NET 10); stable since Jan 2014 — OSC spec hasn't changed since 2002, so age is a feature; comprehensive `OscReceiver` / `OscSender` / `OscMessage` / `OscBundle` / `OscTimeTag` surface; 100% thread-safe per author's own docs [CITED: search hit "Rug.Osc is ... 100% thread safe"] |
| `PrettyPrompt` | 4.1.1 | REPL line editor (Tab completion, Ctrl+R history search, multi-line input, persistent history) [VERIFIED: NuGet API returns `"4.1.1"`] [CITED: https://www.nuget.org/packages/PrettyPrompt] | Last published Sept 30, 2023 — actively maintained; MPL-2.0 (file-scope copyleft, compatible with Flow's MIT-style distribution); features per README: syntax highlighting, autocompletion menus, history "similar to PSReadLine's HistorySearchBackward", multi-line via Shift+Enter soft-newline, IPromptCallbacks customization hooks [CITED: https://github.com/waf/PrettyPrompt]; targets .NET 6+ — compatible with .NET 10 |
| `flow-lsp` (ProjectReference) | first-party | In-process LSP completion via `CompletionHandler.BuildItems()` static [VERIFIED: code grep at flow-lsp/Handlers/CompletionHandler.cs:95-144] | Already a static method with NO OmniSharp transport coupling; consume the 4 indices (`BuiltInIndex`, `StdlibSymbolIndex`, `KeywordIndex`, `UserSymbolIndex`) directly; **MATERIALLY SIMPLER than D-38-12's "in-memory MemoryStream LanguageServer" framing** — no need to instantiate `LanguageServer.From()` at all |
| PulseAudio Simple API | system (`libpulse-simple.so.0`) | Linux audio capture extension — adds `PA_STREAM_RECORD = 2` + `pa_simple_read()` [VERIFIED: enum value via pulseaudio/src/pulse/def.h] [CITED: https://github.com/pulseaudio/pulseaudio/blob/master/src/pulse/def.h] | Existing playback backend at `flow-lang/Audio/PulseAudioSimpleBackend.cs` (~310 LOC) already P/Invokes `pa_simple_new` / `pa_simple_write` / `pa_simple_drain` / `pa_simple_flush` / `pa_simple_free` / `pa_strerror`; capture extension adds ONE constant + ONE P/Invoke signature + one builtin |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Melanchall.DryWetMidi` | 8.0.3 | Existing MIDI file R/W | UNCHANGED in Phase 38 |
| `OmniSharp.Extensions.LanguageServer` | 0.19.9 | Existing flow-lsp framework | UNCHANGED — REPL bypasses transport layer via direct static call |
| `Pidgin` | 3.5.1 | Existing (unused — flagged for removal in v1.5 housekeeping) | UNCHANGED in Phase 38 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Rug.Osc 1.2.5` | `OscCore` (newer, more idiomatic API) | Rug.Osc covers full OSC 1.0 spec including bundle nesting + all type tags out of the box; OscCore has narrower surface and would require composer-side wrapping for bundles. Stick with Rug.Osc per STACK.md and CONTEXT.md prior research. |
| `PrettyPrompt 4.1.1` | `ReadLine 2.0.1` (MIT, tonerdo/readline) | ReadLine **lacks Ctrl+R reverse history search and multi-line input** [CITED: WebFetch github.com/tonerdo/readline shortcut guide]. REPL-03 explicitly requires both. ReadLine last release May 2017 — inactive. **PrettyPrompt wins decisively on feature-fit.** Fallback only if MPL-2.0 license raises an unforeseen concern (it should not for Flow). |
| `PrettyPrompt 4.1.1` | Hand-rolled `Console.ReadKey` TUI editor (~400-600 LOC) | Composer ergonomics suffers (no syntax-highlighting, no autocomplete menus); 400-600 LOC of new code to test/maintain; redundant with a battle-tested library. Reserve as fallback per D-38-11 ONLY if PrettyPrompt fails the license gate. |
| Direct static `BuildItems()` call | Full `LanguageServer.From()` in-process with `MemoryStream` pipes per CONTEXT D-38-12 wording | The framing in D-38-12 assumes the LSP needs transport — but `BuildItems()` is ALREADY `public static` and takes raw inputs (`uri`, `text`, `ast`, `tokens`, `cursor`, 4 indices). Constructing a full `LanguageServer` adds OmniSharp's request-routing / JSON-RPC plumbing for ZERO benefit at REPL scale. **Simpler approach: instantiate the 4 indices once in `Repl.cs`, parse the REPL line via `ParseSession`, call `BuildItems()` directly on Tab.** Planner should flag this simplification in Plan 38-04 and confirm with composer before pursuing the heavier MemoryStream path. |
| New `PulseAudioCaptureBackend` sibling class | Extend `PulseAudioSimpleBackend` in place | Sibling class keeps the playback class minimal and decouples lifecycles (a capture session and a playback session can have different sample rates / channels). **Recommend new sibling.** |
| Linear interpolation resampler | Catmull-Rom (already in `loadWav` from Phase 22) | Linear is correct for capture-rate normalization at audio rates (artifacts inaudible); Catmull-Rom is overkill for 48→44.1 conversion. ~30 LOC vs reusing the existing 80+ LOC Catmull-Rom path. PITFALLS.md Pitfall #24 specifies "Catmull-Rom interpolator (already exists in `loadWav` from Phase 22 varispeed). Same code path." — both work; linear is cheaper. **Recommend linear for simplicity; trivial to swap to Catmull-Rom later if HUMAN-UAT reports aliasing.** |

**Installation:**
```bash
# Add to flow-lang.csproj <ItemGroup>
dotnet add flow-lang package Rug.Osc --version 1.2.5

# Add to flow-interpreter.csproj <ItemGroup>
dotnet add flow-interpreter package PrettyPrompt --version 4.1.1
# Also add: <ProjectReference Include="..\flow-lsp\flow-lsp.csproj" />
```

**Version verification:** Verified via NuGet flatcontainer API on 2026-05-23:
- `Rug.Osc`: latest 1.2.5 [VERIFIED: NuGet API]
- `PrettyPrompt`: latest 4.1.1 [VERIFIED: NuGet API]
- `ReadLine`: latest 2.0.1 (fallback candidate) [VERIFIED: NuGet API]

## Package Legitimacy Audit

Ran slopcheck 0.6.1 against the three NuGet candidate packages on 2026-05-23. slopcheck only queries PyPI (not NuGet) — its `[SLOP]` verdict on `Rug.Osc` is a **false positive** because the package is .NET-only. NuGet API confirms all three packages exist. Source-repo verification + last-published verification + license checks below stand in for slopcheck's normal flow.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| `Rug.Osc` 1.2.5 | NuGet [VERIFIED: api.nuget.org/v3-flatcontainer/rug.osc/index.json returns version list ending `"1.2.5"`] | 12+ years (stable since Jan 2014 — OSC 1.0 spec is stable since 2002) [CITED: https://www.nuget.org/packages/Rug.Osc] | Not surfaced by NuGet API direct query | Original at bitbucket.org/rugcode/rug.osc (per WebSearch hits) | [SLOP — false positive — Rug.Osc is .NET, slopcheck only checks PyPI] | **Approved** — author of multiple OSC examples in the wild (xioTechnologies, VRChat ecosystem); STACK.md research backed |
| `PrettyPrompt` 4.1.1 | NuGet [VERIFIED: api.nuget.org/v3-flatcontainer/prettyprompt/index.json returns version list ending `"4.1.1"`] | Last published Sept 30, 2023 [CITED: nuget.org/packages/PrettyPrompt] | 104.4K total (66.2K for 4.1.1) [CITED: nuget.org/packages/PrettyPrompt] | github.com/waf/PrettyPrompt (199 stars, 477 commits, active) [CITED: WebFetch] | [OK — but note PyPI slopcheck warning "No source repository linked"; the source IS linked on NuGet, just not surfaced to slopcheck] | **Approved** |
| `ReadLine` 2.0.1 (fallback only) | NuGet [VERIFIED: api.nuget.org/v3-flatcontainer/readline/index.json returns version list ending `"2.0.1"`] | Last published June 12, 2018 [CITED: nuget.org/packages/ReadLine] — INACTIVE | 2.1M total [CITED: nuget.org/packages/ReadLine] | github.com/tonerdo/readline (inactive since 2017) [CITED: WebFetch] | [OK] | **Reserved as fallback only** — feature-incomplete for REPL-03 (no Ctrl+R, no multi-line); ship only if PrettyPrompt fails the license gate |

**Packages removed due to slopcheck [SLOP] verdict:** none (Rug.Osc false positive — verified live on NuGet and via Bitbucket source repo)
**Packages flagged as suspicious [SUS]:** none

*slopcheck does not query NuGet; verification above is via NuGet flatcontainer API + WebFetch of source repos + cross-reference to STACK.md research.*

## Architecture Patterns

### System Architecture Diagram

```
                                    flow watch foo.flow
                                            │
                                            ▼
                   ┌──────────────────────────────────────────────┐
                   │  LiveReloadManager.Run() (rewrite — Plan 38-01) │
                   │                                              │
                   │   ┌──────────────────────────────────────┐   │
                   │   │  1. Initial render — FlowEngine.Execute │
                   │   │     populates _liveBlocks: Dict<int,    │
                   │   │     LiveBlockRecord>                    │
                   │   └──────────────────────────────────────┘   │
                   │                  │                           │
                   │                  ▼                           │
                   │   ┌──────────────────────────────────────┐   │
                   │   │  2. Spawn 3 background tasks:        │   │
                   │   │     (a) Streaming playback loop       │   │
                   │   │     (b) FileSystemWatcher → debounce │   │
                   │   │         200ms → background render    │   │
                   │   │     (c) ANSI panel redraw — 2 Hz     │   │
                   │   └──────────────────────────────────────┘   │
                   └──────────────────────────────────────────────┘
                                            │
                  ┌─────────────────────────┼─────────────────────────┐
                  ▼                         ▼                         ▼
       File save detected            Playback loop                ANSI panel
                  │                  (continues)                       │
                  ▼                         │                          ▼
       ┌──────────────────────┐             │           ┌──────────────────────┐
       │  Background render — │             │           │  Read shared state:  │
       │  Task.Run with 30s   │             │           │   tempo / bar /      │
       │  CancellationToken   │             │           │   live blocks /      │
       │                      │             │           │   voice count /      │
       │  scenario A:         │             │           │   recent advisory    │
       │   parse fails →      │             │           │                      │
       │   [live] advisory    │             │           │  Render at 2 Hz via  │
       │                      │             │           │  ANSI cursor moves   │
       │  scenario B:         │             │           │  (CSI sequences)     │
       │   30s timeout →      │             │           │                      │
       │   [live] advisory    │             │           │  TTY-detect fallback │
       │                      │             │           │  → plain line on     │
       │  scenario C:         │             │           │   each state change  │
       │   stale closure →    │             │           └──────────────────────┘
       │   [live] advisory    │             │
       │                      │             │
       │  scenario D:         │             │
       │   success → diff     │             │
       │   live blocks →      │             │
       │   stage pending      │             │
       │   buffers for each;  │             │
       │   reseed PRNG;       │             │
       │   stage voice-name   │             │
       │   diff               │             │
       └──────────────────────┘             │
                  │                         │
                  └────────────► At each per-block bar boundary,
                                 streaming loop swaps in pending buffer
                                 via 64-sample equal-power crossfade
                                            │
                                            ▼
                                  PulseAudio playback


  REPL mode (orthogonal — Plan 38-04):
       composer launches `flow` with no args
                       │
                       ▼
       ┌──────────────────────────────────────────────┐
       │  Repl.Run() rewrite                          │
       │                                              │
       │  PrettyPrompt loop:                          │
       │   Tab key → BuildItems(uri, text, ast,       │
       │     tokens, cursor, 4 indices) → list of    │
       │     CompletionItem; render menu             │
       │                                              │
       │   Ctrl+R → PrettyPrompt's HistorySearchBack  │
       │                                              │
       │   :help fn → BuiltInDocs.TryGet(fn) → format │
       │     summary + signature + example (line 19   │
       │     of BuiltInDocs.cs onwards)               │
       │                                              │
       │   (inspect seq) → VisualizationFunctions     │
       │     .Visualize with NEW articulation glyph   │
       │     branch in note-placement loop            │
       └──────────────────────────────────────────────┘


  Audio input (orthogonal — Plan 38-05):
       composer calls `(micBuffer 4s)`
                       │
                       ▼
       ┌──────────────────────────────────────────────┐
       │  InputFunctions.MicBuffer:                   │
       │   1. Open PA_STREAM_RECORD via pa_simple_new │
       │      (sample format Float32LE, 2ch, query    │
       │      device's native rate)                   │
       │   2. Read N seconds via pa_simple_read       │
       │      into raw[] (native rate)                │
       │   3. If native ≠ 44100, linear-interp        │
       │      resample to 44100 → out[]               │
       │   4. -20 dB scalar attenuation               │
       │   5. Close stream; return AudioBuffer        │
       │  Stderr advisories: open-attenuate (once),   │
       │   resample (once per native rate)            │
       └──────────────────────────────────────────────┘


  OSC (orthogonal — Plan 38-06):
       composer calls `(oscListen 7777 "/x" handler)`
                       │
                       ▼
       ┌──────────────────────────────────────────────┐
       │  OscFunctions.Listen:                        │
       │   1. New OscReceiver(7777); Connect()        │
       │   2. Wrap in OscHandle reference-id Value    │
       │   3. Task.Run(async () => {                  │
       │        while (!ct.IsCancellationRequested) { │
       │          var pkt = recv.Receive(); // block  │
       │          DispatchPacket(pkt, handler);       │
       │        }                                     │
       │      }, ct);                                 │
       │   4. DispatchPacket(pkt, handler):           │
       │       if (pkt is OscMessage) match path;     │
       │           rate-limit gate → invoke handler   │
       │       if (pkt is OscBundle) recurse depth≤8  │
       │   5. Return OscHandle Value                  │
       └──────────────────────────────────────────────┘
```

### Recommended Project Structure

```
flow-interpreter/
├── LiveReloadManager.cs              # REWRITE — orchestrator + multi-block tracking + CancellationToken (Plan 38-01/02/03)
├── LiveStatusPanel.cs                # NEW — ANSI rendering, TTY fallback, 2 Hz redraw timer (Plan 38-01)
├── Repl.cs                           # EXTEND — swap line input to PrettyPrompt; add :help; in-process completion (Plan 38-04)
└── flow-interpreter.csproj           # +PackageReference PrettyPrompt; +ProjectReference flow-lsp

flow-lang/
├── Ast/Statements/
│   └── LiveBlockStatement.cs         # NEW — record mirroring MusicalContextStatement shape (Plan 38-02)
├── Parsing/Parser.cs                 # EXTEND — `live` keyword dispatch at line ~163; new ParseLiveBlockStatement method (Plan 38-02)
├── Lexing/TokenType.cs               # EXTEND — add `Live` token (Plan 38-02)
├── Lexing/SimpleLexer.cs             # EXTEND — keyword table at line 884: "live" => TokenType.Live (Plan 38-02)
├── Interpreter/
│   ├── Interpreter.cs                # EXTEND — handle LiveBlockStatement: enroll in engine's live-block registry, evaluate body once initially (Plan 38-02)
│   └── LambdaCaptureAuditor.cs       # NEW — AST walker comparing closure CapturedNames to new file-scope frame (Plan 38-03)
├── StandardLibrary/
│   ├── Audio/
│   │   ├── VoiceAllocator.cs         # EXTEND — add DiffByVoiceName(prevList, newList) → (preserved, dropped, added) (Plan 38-03)
│   │   ├── PulseAudioCaptureBackend.cs  # NEW — sibling to PulseAudioSimpleBackend; pa_simple_new with PA_STREAM_RECORD=2, pa_simple_read P/Invoke, IDisposable (Plan 38-05)
│   │   └── InputFunctions.cs         # NEW — (micBuffer Second) → Buffer; linear resampler to 44.1kHz; -20dB attenuation; stderr advisories (Plan 38-05)
│   ├── Network/                      # NEW namespace (first file lives here)
│   │   └── OscFunctions.cs           # NEW — registers 5 builtins; type-tag inference dispatch; rate-limit gate; OscHandle value type; bundle send/recv with depth-cap 8 (Plan 38-06)
│   ├── VisualizationFunctions.cs     # EXTEND — articulation glyph branch in note-placement loop at lines 117-131; alias `(inspect seq)` (Plan 38-04)
│   └── BuiltInFunctions.cs           # EXTEND — Register InputFunctions + OscFunctions in the appropriate Register*() method (Plan 38-05/06)
├── Runtime/
│   ├── Value.cs                      # EXTEND — add OscHandle factory method (Plan 38-06)
│   └── PrngRegistry.cs               # READ-ONLY — call ResetAtRenderBoundary() from live-swap callback; no API change (Plan 38-03)
├── TypeSystem/SpecialTypes/
│   └── OscHandleType.cs              # NEW — reference-identity type (mirrors TuningType / SfzType / MarkovModelType pattern) (Plan 38-06)
├── audio.flow                        # EXTEND — add (micBuffer) forward-decl (Plan 38-05)
├── osc.flow                          # NEW — module (loads on use "@osc"): forward-decls for oscSend / oscListen / oscStop / oscBundle / oscSendBundle (Plan 38-06)
└── flow-lang.csproj                  # +PackageReference Rug.Osc

flow-cli/Commands/
└── WatchCommand.cs                   # READ-ONLY — 50 LOC; LiveReloadManager construction stays compatible (Plan 38-01)

examples/live/                        # NEW — 5 composer-facing chapters (Plan 38-07)
├── hello_live.flow
├── multi_block.flow
├── repl_session.md
├── mic_granular.flow
└── osc_controller.flow

tests/
├── test_live_*.flow                  # Live-mode tests via capture-mode FlowEngine harness (Plan 38-03/07)
├── test_repl_*.flow                  # REPL tests via direct ExecuteScriptAndGetResult (Plan 38-04/07)
├── test_audio_in_*.flow              # Fixture WAV fed through capture path (Plan 38-05/07)
└── test_osc_*.flow                   # UDP loopback 127.0.0.1:0 round-trip (Plan 38-06/07)
```

### Pattern 1: AST Mirror — `LiveBlockStatement` as `MusicalContextStatement` clone

**What:** New AST node for `live <quantize> { body }`. Mirrors `MusicalContextStatement`'s shape exactly: location + value (quantize expression) + body (list of statements) + Span. Quantize accepts `NoteValue` literal (`q`/`h`/`w`/etc.) or `Bar` literal or unit-tagged integer (`1bar`, `4bars`).

**When to use:** Whenever a new musical-context-style block construct is added to the language.

**Example (Plan 38-02):**

```csharp
// Source: pattern from flow-lang/Ast/Statements/MusicalContextStatement.cs:1-22
namespace FlowLang.Ast.Statements;

/// <summary>
/// A live-coding block statement that auto-loops its body and hot-swaps content
/// at the next quantize unit boundary (default 1bar).
///
/// LIVE-01: composer wraps a block in `live <quantize> { ... }`. On file save
/// the watcher re-evaluates the body and stages a pending buffer; the streaming
/// playback loop swaps it in at the next quantize boundary via 64-sample
/// equal-power crossfade.
///
/// D-v1.5-07: live blocks emit a stderr advisory on every entry explicitly
/// noting they opt OUT of the two-run cmp-clean determinism contract.
/// </summary>
public record LiveBlockStatement(
    SourceLocation Location,
    Expression QuantizeValue,       // NoteValue (q/h/w/...) or Bar literal or unit-tagged Int
    IReadOnlyList<Statement> Body,
    int BlockId,                    // Stable per-source-location id used by LiveReloadManager
                                    //   to key pending-buffer slot + status-panel row
    Span? Span = null
) : Statement(Location);
```

Parser dispatch (mirrors `flow-lang/Parsing/Parser.cs:105-162`):

```csharp
// Source: pattern from flow-lang/Parsing/Parser.cs:105-162
// In ParseStatement(), after the existing musical-context-keyword dispatch block:
if (Check(TokenType.Live))   // After TokenType.Live is added to Lexing/TokenType.cs
{
    Advance();
    return ParseLiveBlockStatement();
}

private LiveBlockStatement ParseLiveBlockStatement()
{
    var location = PreviousToken.Location;

    // Parse quantize value: NoteValue token (q/h/w/...) OR Int + "bar" identifier OR
    // simple Int treated as bar count.
    Expression quantize;
    if (Check(TokenType.NoteValueLiteral))     // q/h/w/e/s NoteValue tokens (existing)
        quantize = new LiteralExpression(CurrentToken.Location, Advance().Value!, Span: ...);
    else if (Check(TokenType.IntLiteral))
    {
        var intLoc = CurrentToken.Location;
        var intVal = (int)Advance().Value!;
        // Optional "bar" / "bars" suffix
        if (Check(TokenType.Identifier) && (CurrentToken.Text == "bar" || CurrentToken.Text == "bars"))
            Advance();
        quantize = new LiteralExpression(intLoc, intVal, Span: Span.At(intLoc));
    }
    else
        throw new ParseException($"Expected quantize unit (NoteValue or Int + 'bar'), got {CurrentToken.Type} at {CurrentToken.Location}");

    Expect(TokenType.LBrace, "Expected '{' to open live block body");
    var body = new List<Statement>();
    while (!Check(TokenType.RBrace) && !IsAtEnd())
    {
        while (Match(TokenType.Semicolon)) ;
        if (Check(TokenType.RBrace) || IsAtEnd()) break;
        var stmt = ParseStatement();
        if (stmt != null) body.Add(stmt);
        Match(TokenType.Semicolon);
    }
    Expect(TokenType.RBrace, "Expected '}' to close live block body");

    // BlockId is the source location's FNV-1a hash (deterministic across runs;
    // stable across edits as long as the live { ... } token position is unchanged.
    // When composer adds/removes a live block, its BlockId changes — voice-pool
    // preservation falls through to the "block not in new set" diff branch and
    // those voices finish naturally. This matches D-38-03's "file-scope frozen"
    // intent: composer expects a restart on structural file changes.)
    int blockId = ComputeBlockId(location);

    return new LiveBlockStatement(location, quantize, body, blockId,
        Span: new Span(location, PreviousToken.Location));
}
```

### Pattern 2: Watch-mode rewrite — preserve primitives, replace orchestration

**What:** `LiveReloadManager` keeps its bar-boundary detection (line 230), 64-sample crossfade (line 251), and capture-mode render (line 328). What changes is the orchestration around them: per-block pending-buffer slot (Dict instead of single field), 200ms debounce constant (line 277), 30s `CancellationTokenSource` wrap around `engine.Execute`, and ANSI status panel state-publish.

**When to use:** Phase 38-01/02/03 — all three Live plans extend `LiveReloadManager` along orthogonal axes.

**Example (Plan 38-01 — debounce + 30s cap + ANSI integration):**

```csharp
// Source: extends flow-interpreter/LiveReloadManager.cs:274 + 328
private const int DebounceMs = 200;                    // Was 500; LIVE-02 + D-38-05 + Pitfall #21
private const int EvalTimeoutSec = 30;                 // LIVE-02 + D-38-07 + Pitfall #12
private readonly LiveStatusPanel _panel = new();       // NEW

private void TriggerBackgroundRender()
{
    var now = DateTime.Now;
    if ((now - _lastChangeTime).TotalMilliseconds < DebounceMs) return;
    _lastChangeTime = now;
    Thread.Sleep(100); // allow file write to settle (preserved from existing)

    Task.Run(() =>
    {
        try
        {
            _panel.PublishAdvisory("[watch] re-rendering...", AdvisoryLevel.Info);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(EvalTimeoutSec));

            // Run engine.Execute on a worker task; cancel via Thread.Abort-style timeout.
            // FlowEngine.Execute is synchronous and does NOT today consume a CancellationToken
            // (verified — Core/FlowEngine.cs:221 has no ct parameter); we wrap with
            // Task.Run + Wait(timeout) and on timeout BLEED the worker task (cooperative —
            // worker can't be force-killed without unsafe AppDomain unloading, gone in .NET).
            // Mitigation: the 30s cap protects the COMPOSER-facing flow (playback keeps
            // going on the previous buffer); the orphaned task ends naturally when the
            // FlowEngine instance is GC'd at the end of the next successful render.
            // See §G below for the rationale + alternative (cooperative ct in Interpreter).
            AudioBuffer? capturedBuffer = null;
            MusicalContext? musicalContext = null;
            string? errors = null;
            Dictionary<int, LiveBlockBuffer>? perBlockBuffers = null;

            var renderTask = Task.Run(() =>
            {
                capturedBuffer = RenderScript(_filePath, out musicalContext, out errors, out perBlockBuffers);
            }, cts.Token);

            if (!renderTask.Wait(TimeSpan.FromSeconds(EvalTimeoutSec)))
            {
                _panel.PublishAdvisory(
                    $"[live] evaluation timed out at {EvalTimeoutSec}s — keeping previous version",
                    AdvisoryLevel.Error,
                    dedupKey: $"live-timeout:{_filePath}");
                cts.Cancel();
                return;
            }

            if (capturedBuffer == null)
            {
                _panel.PublishAdvisory(
                    $"[live] {errors ?? "no audio output"} — keeping previous version",
                    AdvisoryLevel.Error,
                    dedupKey: $"live-parse:{_filePath}");
                return;
            }

            // Diff live blocks: stage per-block pending buffers (Plan 38-02 — see §A/§F).
            // For each block id present in BOTH old + new, stage a new pending slot;
            // for blocks dropped from new (composer deleted them), the streaming
            // loop finishes the current buffer and removes the block from rotation.
            StagePendingBuffers(perBlockBuffers!, musicalContext);
            // Reset PRNG at the swap boundary so generative primitives don't accumulate
            // state across reloads (LIVE-03 — see §D).
            _engineForPrng?.Context.PrngRegistry.ResetAtRenderBoundary();
            // Voice-pool preservation (LIVE-03 — see §C).
            PreserveVoiceState(perBlockBuffers!);
        }
        catch (Exception ex)
        {
            _panel.PublishAdvisory(
                $"[live] {ex.Message} — keeping previous version",
                AdvisoryLevel.Error,
                dedupKey: $"live-exception:{ex.GetType().Name}");
        }
    });
}
```

### Pattern 3: Reference-identity Value type for OscHandle

**What:** Phase 32 `Tuning`, Phase 33 `Sfz`, and Phase 36 `MarkovModel` / `LsystemModel` established the "reference-identity special type" pattern. `OscHandle` (D-38-16) is the next in line.

**When to use:** When composer needs to address an opaque runtime resource by Flow-level value (cancel a listener, stop a stream, etc.).

**Example (Plan 38-06):**

```csharp
// Source: pattern from flow-lang/Runtime/Value.cs:60-101 (Tuning + Sfz + MarkovModel factories)
namespace FlowLang.Runtime;

public class Value
{
    // ...existing factories...

    /// <summary>
    /// Phase 38 D-38-16 — wraps an OscHandleData reference in a Flow Value typed as
    /// OscHandleType.Instance. Reference identity per Phase 32 Tuning / Phase 33 Sfz /
    /// Phase 36 MarkovModel precedent: two (oscListen port path handler) calls produce
    /// distinct Values even with identical port + path arguments.
    /// </summary>
    public static Value OscHandle(OscHandleData handle) => new(handle, OscHandleType.Instance);
}

// Source: pattern from flow-lang/TypeSystem/SpecialTypes/SfzType.cs (similar sealed singleton)
namespace FlowLang.TypeSystem.SpecialTypes;

public sealed class OscHandleType : FlowType
{
    public static readonly OscHandleType Instance = new();
    private OscHandleType() : base("OscHandle") { }

    public override int GetSpecificity() => 150;          // After MarkovModel(148), LsystemModel(149)
    public override bool IsCompatibleWith(FlowType other) => other is OscHandleType;
}

// In OscFunctions.cs:
public sealed class OscHandleData
{
    public int Port { get; init; }
    public string Path { get; init; } = "";
    public OscReceiver Receiver { get; init; } = null!;
    public CancellationTokenSource Cts { get; init; } = null!;
    public Task ListenerTask { get; init; } = null!;
}
```

### Anti-Patterns to Avoid

- **Hand-roll TUI line editor as primary surface:** Pre-rejected by D-38-11. Only ship if PrettyPrompt fails the license gate.
- **`new Random(seed)` outside `PrngRegistry`:** Pre-existing CI gate `PrngRegistryNewRandomGateTests` would fail. OSC tag-inference jitter (if any) must route through registry.
- **Full `LanguageServer.From()` + MemoryStream pipes inside the REPL:** CONTEXT D-38-12 framing implies this; the actual `BuildItems()` static makes it unnecessary. Plan 38-04 should call out the simpler approach to composer and confirm before pursuing the heavier path.
- **Forcing termination of a stuck `engine.Execute` task:** .NET dropped `Thread.Abort` for managed code; we cannot force-kill a worker without unsafe AppDomain unloading. The 30s cap PROTECTS the composer (playback keeps going on the previous buffer); orphaned-worker leak is acceptable (one per stuck render until next successful render GCs the FlowEngine instance). Document this limitation in code-comment; revisit if HUMAN-UAT reports worker accumulation.
- **OSC bundle nesting without depth cap:** D-38-15 locks `depth ≤ 8` (mirrors Phase 36 T-36-17 / Phase 39 D-39-19). Without a cap, a malicious or buggy upstream can DoS the listener thread via crafted bundle chains.
- **Unicode glyphs in piano-roll:** UI-SPEC pre-locked ASCII-only. Unicode breaks in `xterm`/`vt100`/`dumb` and old Windows consoles.
- **Auto-restart on file-scope edit:** D-38-04 rejected this. "Live session never dies mid-set" is the highest-stakes lock; auto-restart contradicts it.
- **Block on `OscReceiver.Receive()` in the calling thread:** Wraps in `Task.Run` (background loop) with a per-handle `CancellationToken`. `(oscListen ...)` returns immediately with the OscHandle (D-38-16).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| OSC 1.0 protocol — type tags, bundles, timetag NTP fixed-point | Custom byte-level OSC parser/serializer | `Rug.Osc 1.2.5` | Full OSC 1.0 spec coverage including bundles (D-38-15 critical), all type tags (`,i`/`,h`/`,f`/`,d`/`,s`/`,T`/`,F`/`,b`/`,N`), `OscTimeTag` NTP fixed-point math, thread-safety; STACK.md preselected; ~12 years stable since Jan 2014 (OSC spec is stable since 2002) |
| REPL line editor — Ctrl+R history search, multi-line, persistent history | `Console.ReadKey()` raw key-handling loop with our own buffer | `PrettyPrompt 4.1.1` | Built-in `HistorySearchBackward` (Ctrl+R), soft-newline multi-line, persistent-history hook, `IPromptCallbacks` for async completion delegation; ~400-600 LOC saved + composer-grade ergonomics |
| LSP-grade tab completion for REPL | Re-implement scope-aware identifier matching | Direct call to `flow-lsp/Handlers/CompletionHandler.cs:95-144` `BuildItems()` static | Phase 17 + Phase 31 already shipped 5-source merge (builtins/stdlib/user/keyword/snippets) + 3 context filters (imports/pragmas/musical-context boost) + 104-entry doc table; reuse via `<ProjectReference>` + 4 index instantiations + ~10 LOC dispatch |
| Doc-string lookup for `:help fn` | New doc table | `BuiltInDocs.TryGet(identifier)` at flow-lang/StandardLibrary/BuiltInDocs.cs:199 | Phase 31 already shipped 104-entry table; HoverHandler at flow-lsp/Handlers/HoverHandler.cs:46-65 is the existing consumer pattern — second consumer follows same shape |
| PulseAudio capture P/Invoke from scratch | Reimplement libpulse-simple bindings | Extend `flow-lang/Audio/PulseAudioSimpleBackend.cs` (~310 LOC) | Already P/Invokes `pa_simple_new` / `pa_simple_write` / `pa_simple_drain` / `pa_simple_flush` / `pa_simple_free` / `pa_strerror`; capture adds ONE constant (`PA_STREAM_RECORD = 2`) + ONE P/Invoke (`pa_simple_read`); ~40-50 LOC mirror |
| ANSI color/cursor escape table | Re-derive CSI sequences | Use industry-standard CSI strings (verified codes below in §F) | `\x1b[2J` clear screen, `\x1b[H` cursor home, `\x1b[<n>A` cursor up, `\x1b[K` erase to end of line, `\x1b[s` / `\x1b[u` save/restore cursor, `\x1b[1m` bold, `\x1b[2m` dim, `\x1b[31m` red, etc.; all in xterm-256 baseline + Windows Terminal + iTerm2 + Konsole + gnome-terminal |
| Sample-rate conversion algorithms | Reimplement Catmull-Rom or Lanczos | Linear interpolation (~30 LOC) | Capture-side rate mismatch (typically 48 kHz → 44.1 kHz; ratio 1.088); linear interp aliasing is inaudible at audio rates; Catmull-Rom available via Phase 22's varispeed path if HUMAN-UAT flags it |
| Voice-pool state diff | New mutable state-tracker | Extend `VoiceAllocator` with `DiffByVoiceName(prev, next)` static helper | Existing API at `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs:124-169` returns `List<Voice>`; new helper compares `Voice.Name` (per Phase 28 docs — voice names like "piano:3", "drums:1") between two lists |
| PRNG reseed boundary | New reseed mechanism | `engine.Context.PrngRegistry.ResetAtRenderBoundary()` at `flow-lang/Runtime/PrngRegistry.cs:122` | Phase 36 D-v1.5-06 boundary API is the single source of truth; live-swap is just another boundary |
| Stale-closure detection | Type-checker pass | AST walker comparing `LambdaExpression.CapturedNames` to new file-scope frame's keys | One-shot pass per swap; ~50 LOC; consumes existing AST + ExecutionContext.GlobalFrame.Variables/Functions dicts |

**Key insight:** Phase 38 is **heavily reuse-driven**. CONTEXT explicitly framed it as "ANSI panel + multi-block tracking + 200ms debounce + 30s CancellationToken" being the ORCHESTRATION rewrite, with the bar-boundary + crossfade + render-on-error-keep-previous primitives PRESERVED. The single largest research surprise — `CompletionHandler.BuildItems()` is already `public static` with no transport coupling — means D-38-12 can be SIMPLIFIED at plan time without sacrificing functionality.

## §A — Live Block AST + Parser Integration

**Closest existing analog:** `flow-lang/Ast/Statements/MusicalContextStatement.cs` — record with `Location`, an enum `ContextType`, a `Value` expression, optional `Value2`, `Body` list, and Span. Parser dispatch at `Parser.cs:104-171` matches a keyword token (Tempo/Timesig/Key/Swing/VoicePool/Tuning/SustainPedal) and calls `ParseMusicalContextStatement(MusicalContextType.X)`.

**For Phase 38:**
1. Add `Live` to `flow-lang/Lexing/TokenType.cs` (in the same block as `Timesig`/`Tempo`/`VoicePool`/`Tuning`/`SustainPedal`).
2. Add `"live" => TokenType.Live` to the keyword table in `SimpleLexer.cs:884-896` (the `voicePool`/`sustainPedal`/`tuning` block).
3. Create `flow-lang/Ast/Statements/LiveBlockStatement.cs` with the record shape from Pattern 1 above.
4. In `Parser.cs::ParseStatement`, add a new `if (Match(TokenType.Live)) return ParseLiveBlockStatement();` after the existing `Match(TokenType.Tuning)` dispatch (line ~170).
5. `ParseLiveBlockStatement` mirrors `ParseTuningContextStatement` shape — see Pattern 1 code above.

**Interpreter handling:** When the Interpreter encounters a `LiveBlockStatement`, it must (a) enroll the block in the engine's live-block registry (a new `Dictionary<int, LiveBlockRegistration>` on `ExecutionContext`), (b) execute the body ONCE during the initial run so the per-block AudioBuffer is captured into the registry, and (c) emit the `[live] entering live block at line N — opts OUT of two-run cmp-clean determinism` advisory once per block per process (dedup via existing `RenderingDiagnostics.WarnOnce` pattern).

**Live block registry shape (new, on ExecutionContext):**

```csharp
public sealed record LiveBlockRegistration(
    int BlockId,
    SourceLocation Location,
    double QuantizeBeats,    // Resolved at registration: NoteValue → beats; Int → bars*beatsPerBar
    IReadOnlyList<Statement> Body,
    AudioBuffer? CapturedBuffer,  // Set after body's evaluation completes
    MusicalContext? SnapshotContext  // Tempo/timesig at registration — bar duration math
);
```

**Confidence:** HIGH (parser pattern is established; only new code is mechanical mirroring).

## §B — Voice-Pool State Preservation by NAME (LIVE-03)

**Current state:** `VoiceAllocator.AllocateWithPool` at `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs:124-169` operates on a `List<Voice>` and either returns the list unchanged (count ≤ pool size) or truncates voice buffers in place via `TruncateVoiceBuffer` (line 178). Voices are mutable; state is NOT preserved across full renders today.

**Voice naming:** Phase 28 docs (CLAUDE.md "Voice-pool allocation") refer to voice names like "piano:0", "drums:1", etc. Verifying — examining `Voice` properties: voices have an instrument label and ordinal index. The `Voice` record / class is referenced from `flow-lang/StandardLibrary/Audio/Voice.cs` (Phase 28). Plan 38-03 may need to add a `Name` property if not already there (sourced from instrument label + ordinal at allocation time); audit at plan-start.

**Minimal API addition (Plan 38-03):**

```csharp
// In VoiceAllocator.cs — new static helper alongside Allocate / AllocateWithPool
public static (List<Voice> Preserved, List<Voice> Dropped, List<Voice> Added)
    DiffByVoiceName(IReadOnlyList<Voice> prev, IReadOnlyList<Voice> next)
{
    var prevByName = prev.ToDictionary(v => v.Name, v => v, StringComparer.Ordinal);
    var nextByName = next.ToDictionary(v => v.Name, v => v, StringComparer.Ordinal);

    var preserved = new List<Voice>();
    var dropped = new List<Voice>();
    var added = new List<Voice>();

    foreach (var (name, voice) in prevByName)
    {
        if (nextByName.TryGetValue(name, out var newVoice))
            preserved.Add(newVoice);    // Inherit prev's audio position / envelope phase
        else
            dropped.Add(voice);
    }
    foreach (var (name, voice) in nextByName)
        if (!prevByName.ContainsKey(name))
            added.Add(voice);

    return (preserved, dropped, added);
}
```

**Diff strategy at swap (in `LiveReloadManager.StagePendingBuffers`):**

1. After successful render, extract the new voice list from the freshly-rendered Buffer (the SongRenderer's `_lastVoiceListUsedForTests` instrumentation precedent — Plan 38-03 may need to add an analogous `_lastVoiceListUsedForLive` hook on `SongRenderer`).
2. Call `DiffByVoiceName(_previousVoices, newVoices)`.
3. For each `preserved` voice: transfer the previous voice's playback offset + envelope state to the new voice's instance via a new `Voice.CopyStateFrom(prevVoice)` method. Audible effect: a piano note that started 2.5s ago in the OLD code continues playing from the 2.5s point in the NEW code (if the new code retains the same voice name).
4. For each `dropped` voice: apply a 5ms fade-out via the existing `ApplyFadeOut` private at line 87, then remove from the active set.
5. For each `added` voice: no special handling — just include in the new voice list.
6. The streaming playback loop's NEXT bar boundary swaps the buffer.

**Confidence:** MEDIUM — pattern is clear, but a small audit is needed at plan-start to confirm `Voice.Name` property exists (or add it).

## §C — Stale-Closure Detection

**Goal:** When the file is re-evaluated, detect if any active lambda/closure references a binding that no longer exists in the new file-scope frame. Don't silently misbehave — emit `[live] stale closure: references removed binding '<name>' at line N — keeping previous version` advisory and revert to previous buffer.

**Approach (cheapest):** AST-walk pass. When evaluating a lambda, the existing Interpreter captures the closure's free variables (the lambda's body references identifiers not bound in its parameter list — these get resolved at call time against the enclosing scope chain). Phase 38 needs a one-time AST walk over each LiveBlockStatement body to enumerate the set of free variables it references at the file scope.

**Implementation sketch (Plan 38-03, new file `flow-lang/Interpreter/LambdaCaptureAuditor.cs`):**

```csharp
public static class LambdaCaptureAuditor
{
    /// <summary>
    /// Walks the AST collecting all VariableExpression and FunctionCallExpression names
    /// that resolve to the FILE SCOPE (i.e., not to lambda params, not to locals).
    /// Returns the set of names that must exist in the new file-scope frame for the
    /// body to evaluate without referring to removed bindings.
    /// </summary>
    public static HashSet<string> CollectFileScopeReferences(IReadOnlyList<Statement> body)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var localScope = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stmt in body)
            WalkStatement(stmt, refs, localScope);
        return refs;
    }

    private static void WalkStatement(Statement s, HashSet<string> refs, HashSet<string> locals)
    {
        switch (s)
        {
            case VariableDeclaration v:
                locals.Add(v.Name);
                if (v.Value is not null) WalkExpression(v.Value, refs, locals);
                break;
            case ExpressionStatement e:
                WalkExpression(e.Expression, refs, locals);
                break;
            case AssignmentStatement a:
                if (!locals.Contains(a.Name)) refs.Add(a.Name);
                WalkExpression(a.Value, refs, locals);
                break;
            // ... cover remaining 6 statement types from flow-lang/Ast/Statements
        }
    }

    private static void WalkExpression(Expression e, HashSet<string> refs, HashSet<string> locals) { /* ... */ }
}

// At swap time in LiveReloadManager:
var refs = LambdaCaptureAuditor.CollectFileScopeReferences(newLiveBlock.Body);
var newFileScope = engine.Context.GlobalFrame;
foreach (var name in refs)
{
    if (!newFileScope.HasVariable(name) && !newFileScope.HasFunction(name))
    {
        _panel.PublishAdvisory(
            $"[live] stale closure: references removed binding '{name}' at line {newLiveBlock.Location.Line} — keeping previous version",
            AdvisoryLevel.Error,
            dedupKey: $"live-stale-closure:{name}:{newLiveBlock.Location.Line}");
        return; // skip this swap; previous buffer continues
    }
}
```

**Cost:** O(AST node count of live block body) per render — fast.

**Confidence:** HIGH (AST walk is standard; the `ExecutionContext.GlobalFrame` shape already exposes the queries we need).

## §D — PrngRegistry Reseed at Swap Boundary (LIVE-03)

**Existing API:** `flow-lang/Runtime/PrngRegistry.cs:122` `ResetAtRenderBoundary()` — clears `_registry` + `_drawCounts`. The render-boundary salt stays constant in v1.5 (the Phase 36 comment at line 119 explicitly reserves a non-zero salt for Phase 38 live opt-out per D-v1.5-07).

**Call site:** Plan 38-03 invokes `engine.Context.PrngRegistry.ResetAtRenderBoundary()` from the live-swap callback (inside `LiveReloadManager.StagePendingBuffers`, AFTER the new buffer is staged but BEFORE the stream-loop swaps it in). This means: each successful live reload reseeds PRNG state to deterministic-seeded-by-source-location values, so generative primitives (`markov`, `lorenz`, `sometimes`, etc.) don't accumulate state across reloads.

**Future opt-out (D-v1.5-07 hook):** If composer demands "give me actually-random output in live mode", a future plan could pass a non-zero salt derived from wall-clock at swap time. Phase 38 stays at salt=0 (deterministic-per-source-location reseed).

**Confidence:** HIGH (API already shipped + tested in Phase 36).

## §E — 30s CancellationToken Integration

**Current state:** `FlowEngine.Execute(source, fileName)` at `flow-lang/Core/FlowEngine.cs:221` is **synchronous and does NOT accept a CancellationToken** (verified via grep — zero `CancellationToken` mentions in FlowEngine.cs). The Interpreter's statement loop does not check for cancellation. The only existing CancellationToken usage is in `PulseAudioSimpleBackend.Play` (cooperative cancellation during the playback write loop).

**Decision:** Two options.

**Option A (planner's likely first choice — simpler):** Wrap `engine.Execute` in `Task.Run` with timeout; on timeout cancel the `CancellationTokenSource` (a no-op for the worker since FlowEngine doesn't check it), and rely on the outer `LiveReloadManager` to keep playing the previous buffer. The orphaned worker task ends naturally when the FlowEngine instance is GC'd. **This works for the 30s cap PROTECTING THE COMPOSER, but technically leaks a worker thread per stuck render.**

**Option B (heavier, cooperative cancellation in Interpreter):** Add `CancellationToken` parameter to `FlowEngine.Execute` + thread through `Interpreter.Execute` + `Interpreter.ExecuteStatement` + selected hot paths (`while` loop body, `for` loop body, `each` callbacks, the AST-walk dispatcher). Check `ct.ThrowIfCancellationRequested()` periodically. This properly terminates the worker.

**Recommendation:** Ship Option A in Plan 38-01 with a code-comment documenting the limitation; revisit Option B only if HUMAN-UAT reports worker accumulation (unlikely in normal use — a 30s stuck render is a rare event).

**Code (Option A, in LiveReloadManager):**

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
AudioBuffer? result = null;
var task = Task.Run(() => { result = RenderScript(_filePath, ...); }, cts.Token);
if (!task.Wait(TimeSpan.FromSeconds(30)))
{
    cts.Cancel();   // No-op for FlowEngine but marks the worker as canceled
    EmitTimeoutAdvisory();
    return;         // Previous buffer keeps playing
}
```

**Confidence:** HIGH (pattern is standard; CONTEXT D-38-07 specifies the recovery UX).

## §F — ANSI Status Panel Rendering

**Best practice for 4-row in-place redraw at ~2 Hz (per UI-SPEC):**

**ANSI escape sequences (verified against xterm Control Sequences reference + Windows Terminal docs):**

| Sequence | Bytes | Effect |
|---------|-------|--------|
| Cursor home | `\x1b[H` | Move cursor to row 1, col 1 |
| Move to row N col 1 | `\x1b[<N>;1H` | Position cursor (1-indexed) |
| Cursor up N | `\x1b[<N>A` | Move up N rows |
| Save cursor | `\x1b[s` | Push cursor position (DEC SC variant; widely supported but iTerm2 prefers `\x1b 7` ESC 7) |
| Restore cursor | `\x1b[u` | Pop cursor position (DEC RC; iTerm2 prefers `\x1b 8`) |
| Erase to end of line | `\x1b[K` | Clear from cursor to EOL |
| Erase entire line | `\x1b[2K` | Clear current row (keep cursor) |
| Clear screen | `\x1b[2J` | Clear viewport (keep scrollback) |
| Bold | `\x1b[1m` | Bold attribute |
| Dim | `\x1b[2m` | Dim attribute |
| Reset attributes | `\x1b[0m` | Reset to default |
| Red FG | `\x1b[31m` | Foreground color red |
| Green FG | `\x1b[32m` | Foreground color green |
| Yellow FG | `\x1b[33m` | Foreground color yellow |
| Cyan FG | `\x1b[36m` | Foreground color cyan |

**Cross-terminal compatibility check:**
- xterm-256color: all sequences above supported. [HIGH confidence]
- Windows Terminal: all supported since Windows 10 1903 (default modern shell). [HIGH confidence — confirmed via Microsoft docs years ago]
- iTerm2 (macOS): all supported. [HIGH confidence — author of iTerm2 follows xterm closely]
- gnome-terminal / Konsole / xfce4-terminal: all supported. [HIGH confidence — VT100/xterm-family clones]
- vt100 / dumb / piped output: NO ANSI support — must fall through to plain-line mode via TTY detection.

**TTY-detection logic (locked per UI-SPEC):**

```csharp
bool isColorEnabled = !Environment.GetEnvironmentVariable("NO_COLOR")?.Length > 0
                   && !cliArgs.Contains("--no-color")
                   && !Console.IsOutputRedirected
                   && Environment.GetEnvironmentVariable("TERM") != "dumb";
```

**Redraw cadence (per UI-SPEC):**

- 2 Hz heartbeat (`System.Threading.Timer` with 500ms period) calls `RedrawPanel()` only if internal state-version counter incremented since last redraw.
- Event-driven redraws (bar boundary crossed, live block swap, advisory emitted) bump the state-version + signal the timer's `AutoReset` so the next 500ms tick redraws.
- All rendering happens on the timer thread — **NEVER on the audio thread** (Pitfall #21 lock).

**Layout strategy — terminal-aware:** Use the "scroll region" approach: at startup, emit `\x1b[6r` (set top margin row 6 onwards as scrollable region; rows 1-5 are fixed for the panel) — then ALL subsequent `Console.WriteLine` output scrolls below the panel. Redraw the 4 panel rows via `\x1b[<N>;1H` + `\x1b[2K` + content for each row. On exit, emit `\x1b[r` to reset the scroll region.

**Confidence:** HIGH for ANSI sequences (verified against xterm Control Sequences reference); MEDIUM for scroll-region approach (works in xterm/iTerm2/gnome-terminal/Windows Terminal; Konsole may have quirks — plan should test on Konsole at plan-start). Alternative if scroll-region quirks emerge: redraw the panel at TERMINAL TOP using `\x1b[H` + 4 row writes, but emit a fixed-height "blank" gap below before the first stdout line — this approach also works but loses scrollback.

## §G — In-Process LSP Embedding (D-38-12)

**CONTEXT D-38-12 framing:** "REPL spawns an in-memory `LanguageServer` instance replacing `Console.OpenStandardInput()` / `Console.OpenStandardOutput()` with `MemoryStream` pipes; calls `BuildItems()` on Tab."

**Researched simpler approach:** `flow-lsp/Handlers/CompletionHandler.cs:95-144` declares `BuildItems` as `public static` with these dependencies:
- `DocumentUri uri` (just `DocumentUri.From("<repl>")`)
- `string text` (the current REPL line)
- `FlowProgram? ast` (from `ParseSession.Parse(text, "<repl>").Ast`)
- `IReadOnlyList<Token>? tokens` (same parse result)
- `Position cursor` (column of cursor on REPL line)
- 4 indices: `BuiltInIndex`, `StdlibSymbolIndex`, `KeywordIndex`, `UserSymbolIndex`

The 4 indices are POCOs constructed via `Microsoft.Extensions.DependencyInjection` in flow-lsp's `Program.cs:30-50` but they have plain constructors that take an `InternalFunctionRegistry`. They have NO LSP transport coupling — they're just symbol catalogs.

**Recommended approach (Plan 38-04, materially simpler than D-38-12 framing):**

```csharp
// In Repl.cs constructor (or a new ReplCompletionEngine wrapper):
private readonly BuiltInIndex _builtIns;
private readonly StdlibSymbolIndex _stdlib;
private readonly KeywordIndex _keywords;
private readonly UserSymbolIndex _users;
private readonly ParseSession _parser;

public Repl()
{
    _engine = new FlowEngine();
    // The same registry the engine uses:
    var registry = new InternalFunctionRegistry();
    BuiltInFunctions.RegisterSignaturesOnly(registry);  // No audio side effects
    _builtIns = new BuiltInIndex(registry);
    _stdlib = new StdlibSymbolIndex();
    _keywords = new KeywordIndex();
    _users = new UserSymbolIndex();
    _parser = new ParseSession();
}

// On PrettyPrompt tab callback:
private async Task<IReadOnlyList<CompletionItem>> CompleteAsync(string line, int cursor)
{
    var parseResult = _parser.Parse(line, "<repl>");
    var uri = DocumentUri.From("file:///<repl>");
    var position = new Position(line: 0, character: cursor);
    return CompletionHandler.BuildItems(
        uri, line, parseResult.Ast, parseResult.Tokens, position,
        _builtIns, _users, _stdlib, _keywords).ToList();
}
```

**Mapping to PrettyPrompt's `IPromptCallbacks`:**

```csharp
public class FlowPromptCallbacks : PromptCallbacks
{
    private readonly Repl _repl;
    public FlowPromptCallbacks(Repl repl) => _repl = repl;

    protected override async Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(
        string text, int caret, TextSpan spanToBeReplaced, CancellationToken ct)
    {
        // Convert flow-lsp CompletionItem → PrettyPrompt CompletionItem (1:1 fields)
        var items = await _repl.CompleteAsync(text, caret);
        return items.Select(i => new PrettyPrompt.Completion.CompletionItem(
            replacementText: i.Label,
            displayText: i.Label,
            extendedDescription: _ => Task.FromResult<FormattedString>(i.Detail ?? "")
        )).ToList();
    }
}
```

**Token-heuristic fallback (REPL-01 spec):** When `parseResult.Ast == null` (partial parse fails), `BuildItems()` falls through to the default merge of all 5 sources without filters — that's already the existing behavior at lines 126-130 (`var merged = builtIns.Items().Concat(stdlib.Items())...`). Token-heuristic narrowing is therefore automatic; if Plan 38-04 wants tighter ranking (per Pitfall #13 "if I typed `(transp`, I get `transpose` ranked first"), it can call `BuildItems` then filter/sort by prefix-match against the last identifier-shaped token.

**Confidence:** HIGH — verified via direct read of `CompletionHandler.BuildItems()` signature + Symbols/ directory listing + Program.cs DI registrations.

**Action for Plan 38-04:** Call out this simplification to composer; confirm OK to skip the MemoryStream LanguageServer approach before pursuing the heavier path. The simpler approach is also single-process by definition — no "shared LSP between REPL and `flow watch`" question (Claude's discretion item) — both share the same flow-lsp assembly via ProjectReference.

## §H — ReadLine Library Selection

**Library comparison (verified live on NuGet API + GitHub on 2026-05-23):**

| Property | `ReadLine` 2.0.1 | `PrettyPrompt` 4.1.1 | `Sharprompt` |
|----------|------------------|----------------------|--------------|
| Last published | June 12, 2018 [CITED: nuget.org] | Sept 30, 2023 [CITED: nuget.org] | Active (not researched in detail — prompts/wizards focus, NOT line-editor focus) |
| License | MIT | MPL-2.0 (file-scope copyleft) | Apache-2.0 |
| .NET 10 compat | Yes — .NET Standard 2.0 [CITED: nuget.org] | Yes — targets .NET 6+ [CITED: nuget.org] | Yes |
| Ctrl+R history search | **NO** [CITED: github.com/tonerdo/readline shortcut guide] | YES — "history filtering similar to PSReadLine's HistorySearchBackward" [CITED: github.com/waf/PrettyPrompt README] | Not its purpose (it's a wizard/multi-step prompt library) |
| Multi-line input | **NO** [CITED: github.com/tonerdo/readline — single-line `ReadLine.Read()` only] | YES — "Optionally detects incomplete lines and converts Enter to a soft newline (Shift-Enter)" [CITED: PrettyPrompt README] + word-wrapping | No |
| Tab completion | YES (basic, function-style hook) | YES — `IPromptCallbacks.GetCompletionItemsAsync` async menu | YES (for predefined choice lists) |
| Persistent history | YES (simple file load) | YES (hook-based) | n/a |
| Syntax highlighting | NO | YES (optional) | n/a |
| Maintenance state | **Inactive since 2017** [CITED: WebFetch] | Active — 477 commits, 199 stars [CITED: PrettyPrompt GitHub] | Active |
| Total NuGet downloads | 2.1M total [CITED: nuget.org] | 104.4K total [CITED: nuget.org] | (not surveyed) |

**Recommendation:** `PrettyPrompt 4.1.1`. The license decision (MPL-2.0 is a weak file-scope copyleft — modifications to PrettyPrompt's own files must be MPL, but consumers can be MIT and link PrettyPrompt without contamination) is compatible with Flow's distribution model. The feature-fit is decisive: REPL-03 requires Ctrl+R reverse history search and multi-line; PrettyPrompt ships both first-class while ReadLine has neither.

**Fallback decision criteria (if PrettyPrompt fails the license gate at Plan 38-04 plan-start):**

1. If MPL-2.0 is rejected by composer review → hand-roll TUI editor (~400-600 LOC per D-38-11) — DO NOT ship `ReadLine 2.0.1` as a half-feature alternative (missing Ctrl+R is the deal-breaker).
2. If PrettyPrompt's TextCopy transitive dep (`>=6.2.1`, MIT) raises a concern → still acceptable (MIT compatible with Flow); skip only if composer explicitly rejects transitive deps.

**Confidence:** HIGH (library comparison verified live on NuGet API + GitHub READMEs).

## §I — PulseAudio Capture (AUDIO-IN-01)

**Existing playback path:** `flow-lang/Audio/PulseAudioSimpleBackend.cs:286-308` P/Invokes `pa_simple_new` / `pa_simple_free` / `pa_simple_write` / `pa_simple_drain` / `pa_simple_flush` / `pa_strerror`. Constant `PA_STREAM_PLAYBACK = 1` at line 275; sample format `PA_SAMPLE_FLOAT32LE = 5` at line 276.

**Capture extension (Plan 38-05):**

1. Add to constants (in existing file or new sibling file `PulseAudioCaptureBackend.cs`):
   ```csharp
   private const int PA_STREAM_RECORD = 2;   // Verified from pulseaudio/src/pulse/def.h enum
   ```

2. Add P/Invoke binding (mirrors `pa_simple_write` at line 301-302):
   ```csharp
   [DllImport("libpulse-simple.so.0", CallingConvention = CallingConvention.Cdecl)]
   private static extern int pa_simple_read(IntPtr s, IntPtr data, nuint bytes, out int error);
   ```

3. Capture-side init mirrors `Initialize` at line 36-78 EXCEPT pass `PA_STREAM_RECORD` instead of `PA_STREAM_PLAYBACK`, and use a different stream description ("capture"):
   ```csharp
   _connection = pa_simple_new(
       IntPtr.Zero,             // Use default server
       "flow-lang",             // Application name
       PA_STREAM_RECORD,        // <-- Changed direction
       IntPtr.Zero,             // Use default device (composer can override via SetDevice later)
       "capture",               // <-- Changed stream description
       ref sampleSpec,          // Same Float32LE / sampleRate / channels
       IntPtr.Zero,
       IntPtr.Zero,
       out error);
   ```

4. Capture-read loop:
   ```csharp
   public float[] CaptureSamples(int totalFrames, int channels)
   {
       int totalSamples = totalFrames * channels;
       int totalBytes = totalSamples * sizeof(float);
       var samples = new float[totalSamples];
       var handle = GCHandle.Alloc(samples, GCHandleType.Pinned);
       try
       {
           int byteOffset = 0;
           const int chunkBytes = 4096 * sizeof(float);
           while (byteOffset < totalBytes)
           {
               int readSize = Math.Min(chunkBytes, totalBytes - byteOffset);
               int error;
               int result;
               lock (_lock)
               {
                   if (!IsInitialized) break;
                   var ptr = handle.AddrOfPinnedObject() + byteOffset;
                   result = pa_simple_read(_connection, ptr, (nuint)readSize, out error);
               }
               if (result < 0)
               {
                   var errMsg = Marshal.PtrToStringAnsi(pa_strerror(error));
                   throw new InvalidOperationException($"PulseAudio read error: {errMsg}");
               }
               byteOffset += readSize;
           }
       }
       finally { handle.Free(); }
       return samples;
   }
   ```

5. **Default device:** Pass `IntPtr.Zero` for `dev` — PulseAudio uses the system default (per composer's PulseAudio mixer settings). Researcher's recommendation per CONTEXT discretion: keep default; no composer-overridable device name in v1.5 (matches the playback-side decision; cross-device support deferred).

6. **Header availability gotcha:** On the dev machine, `/usr/include/pulse/simple.h` is NOT installed (only `libpulse0` is, not `libpulse-dev`). This is **fine for runtime** (the P/Invoke binds to `libpulse-simple.so.0` directly; no compile-time header needed). The plan should call this out so plan-start doesn't go hunting for the header.

**Sample format:** `PA_SAMPLE_FLOAT32LE` (matches playback path).

**Confidence:** HIGH (mirror of existing playback path; PulseAudio Simple API is the same pattern for both directions per FreeDesktop docs).

## §J — 44.1kHz Sample-Rate Conversion (AUDIO-IN-02)

**Existing resampling in codebase:** `loadWav` resamples to 44100 Hz (per `BuiltInDocs.cs:111` description "16/24/32-bit, resamples to 44100Hz"). Implementation in `flow-lang/Audio/FileIO.cs` (Phase 22 — Catmull-Rom varispeed pattern).

**Recommendation for Phase 38 capture path:** Linear interpolation (~30 LOC, cheap, inaudible artifact at the typical 48 → 44.1 kHz ratio of 1.088). Pitfall #24 says either is fine ("Catmull-Rom interpolator (already exists in `loadWav` from Phase 22 varispeed). Same code path."). Linear keeps the new file smaller and the dep tree flatter; swap to Catmull-Rom only if HUMAN-UAT flags aliasing.

**Linear interpolator (Plan 38-05, in `InputFunctions.cs`):**

```csharp
private static float[] ResampleLinear(float[] input, int inputRate, int outputRate, int channels)
{
    if (inputRate == outputRate) return input;   // Identity fast-path

    double ratio = (double)inputRate / outputRate;
    int inputFrames = input.Length / channels;
    int outputFrames = (int)Math.Ceiling(inputFrames / ratio);
    var output = new float[outputFrames * channels];

    for (int outFrame = 0; outFrame < outputFrames; outFrame++)
    {
        double inFracIdx = outFrame * ratio;
        int inIdxLo = (int)Math.Floor(inFracIdx);
        int inIdxHi = Math.Min(inIdxLo + 1, inputFrames - 1);
        float t = (float)(inFracIdx - inIdxLo);

        for (int ch = 0; ch < channels; ch++)
        {
            float lo = input[inIdxLo * channels + ch];
            float hi = input[inIdxHi * channels + ch];
            output[outFrame * channels + ch] = lo + (hi - lo) * t;
        }
    }

    return output;
}
```

**Stderr advisory (per UI-SPEC):**

```
[audio-in] resampling capture stream from 48000 Hz to 44100 Hz (linear interpolation)
```
Dedup key: `audio-in-resample:<input-rate>` (one-shot per native rate per process).

**Confidence:** HIGH (linear interpolation is textbook; the ratio range is small).

## §K — Rug.Osc 1.2.5 API

**Core types (per WebFetch of XML doc + WebSearch HotExamples):**

- `OscReceiver` — UDP listener, has `Connect()`, blocking `Receive()` and non-blocking `TryReceive()`, `Dispose()`. Constructor `OscReceiver(int port)`.
- `OscSender` — UDP sender, has `Connect()`, `Send(OscPacket)`, `Dispose()`. Constructor `OscSender(IPAddress address, int port)`.
- `OscMessage` — single message with address + arguments. Constructor `OscMessage(string address, params object[] args)` — accepts CLR-typed args (int/long/float/double/string/bool/byte[]); type tag is INFERRED from runtime type.
- `OscBundle` — container for nested `OscPacket[]`. Constructor `OscBundle(OscTimeTag timetag, params OscPacket[] messages)`. Indexable `Item[int]` for nested packets; `Count` property.
- `OscTimeTag` — NTP fixed-point timestamp. Constructor `OscTimeTag(ulong ntpValue)`; conversions `ToDataTime()` / `FromDataTime()`; static `OscTimeTag.Now` / `OscTimeTag.Immediately` (value `1`).
- `OscPacket` — base class for OscMessage / OscBundle. Static `OscPacket.Read(byte[], int, int)` for parsing.

**Server receive loop pattern (per WebSearch HotExamples summary):**

```csharp
// In OscFunctions.cs — inside the (oscListen ...) builtin:
var receiver = new OscReceiver(port);
var cts = new CancellationTokenSource();
var task = Task.Run(() =>
{
    receiver.Connect();
    while (!cts.IsCancellationRequested)
    {
        try
        {
            OscPacket packet = receiver.Receive();   // Blocking
            DispatchPacket(packet, path, handler, 0);  // Depth starts at 0
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            // Charitable: log and continue (Pitfall #12 — never die mid-set)
            Console.Error.WriteLine($"[osc] receive error on port {port}: {ex.Message}");
        }
    }
    receiver.Dispose();
}, cts.Token);

return Value.OscHandle(new OscHandleData {
    Port = port, Path = path, Receiver = receiver, Cts = cts, ListenerTask = task
});

private static void DispatchPacket(OscPacket pkt, string targetPath, Value handler, int depth)
{
    if (depth > 8)   // D-38-15 nesting depth cap
    {
        RenderingDiagnostics.WarnOnce($"osc-bundle-depth:{targetPath}",
            $"[osc] bundle nesting depth exceeds 8 at {targetPath} — collapsing to flat dispatch");
        return;
    }
    if (pkt is OscBundle bundle)
    {
        // Respect timetag: future timetag → schedule on Task.Delay; Immediately(1) → sync
        if (bundle.Timestamp.Value > 1)
        {
            var when = bundle.Timestamp.ToDataTime();
            var delay = when - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                Task.Delay(delay).ContinueWith(_ => DispatchBundleContents(bundle, targetPath, handler, depth + 1));
            else
                DispatchBundleContents(bundle, targetPath, handler, depth + 1);
        }
        else
            DispatchBundleContents(bundle, targetPath, handler, depth + 1);
    }
    else if (pkt is OscMessage msg && msg.Address == targetPath)
    {
        InvokeHandlerWithRateLimit(targetPath, handler, msg);
    }
}
```

**UDP loopback for tests:**

```csharp
// In test file (e.g., tests/test_osc_roundtrip.flow OR a C# test):
// Bind to ephemeral port via 127.0.0.1:0 — but Rug.Osc OscReceiver constructor
// takes only a port, not 0. Workaround: pick a random high-port number per test
// (e.g., 10000 + threadId % 50000) OR use `new OscReceiver(IPAddress.Loopback, port)`
// overload + a port-scan loop until binding succeeds.
```

**Confidence:** HIGH for core API + bundle handling; MEDIUM for ephemeral-port test pattern (need to verify Rug.Osc allows port=0 OR write a port-scan helper — flag at Plan 38-06 plan-start).

## §L — OSC Type-Tag Inference (D-38-13)

**Mapping (locked per CONTEXT canonical_refs):**

| Flow `Value.Type` | CLR `Data` | OSC type tag | OscMessage constructor arg |
|-------------------|-----------|--------------|------------------------------|
| `IntType` | int | `,i` | `(int)v.Data` |
| `LongType` | long | `,h` | `(long)v.Data` |
| `FloatType` | double (stored as double per Value.cs:25 + line 178 comment) | `,f` | `(float)(double)v.Data` |
| `DoubleType` | double | `,d` | `(double)v.Data` |
| `StringType` | string | `,s` | `(string)v.Data` |
| `SymbolType` | string | `,s` (interned identity collapses to string on the wire) | `(string)v.Data` |
| `BoolType` true | bool true | `,T` | `true` |
| `BoolType` false | bool false | `,F` | `false` |
| `BufferType` | object (AudioBuffer wrapping float[]) | `,b` (blob) | flatten to `byte[]` |

**Dispatch site (Plan 38-06, in `OscFunctions.cs`):**

```csharp
private static object[] InferOscArgs(IReadOnlyList<Value> flowArgs)
{
    var oscArgs = new object[flowArgs.Count];
    for (int i = 0; i < flowArgs.Count; i++)
    {
        var v = flowArgs[i];
        oscArgs[i] = v.Type switch
        {
            IntType => (int)v.Data!,
            LongType => (long)v.Data!,
            FloatType => (float)(double)v.Data!,
            DoubleType => (double)v.Data!,
            StringType => (string)v.Data!,
            SymbolType => (string)v.Data!,
            BoolType => (bool)v.Data!,
            BufferType => AudioBufferToBlob((AudioBuffer)v.Data!),
            _ => throw new ArgumentException(
                $"[osc] unsupported arg type at index {i}: {v.Type.Name} — use Int/Long/Float/Double/String/Symbol/Bool/Buffer")
        };
    }
    return oscArgs;
}

// Rug.Osc handles the type-tag encoding from the CLR-typed args automatically.
```

**Escape hatch (Claude's discretion in CONTEXT):** Recommend named-arg `types=",hd"` form per CONTEXT example for `(oscSend host port "/x" 1 1.5 types=",hd")` — leverages Phase 36 D-36-11 universal named-arg syntax. Plan 38-06 implements; advisory `[osc] type-tag inferred as '<tag>' for arg <i> at <path> — pass an explicit cast for finer control` fires once per path per process when inference selects a non-default for a numeric arg.

**Confidence:** HIGH (Value type discrimination is mechanical; Rug.Osc accepts CLR-typed `params object[]` per docs).

## §M — OSC Rate Limit Implementation

**Per-path `_lastFireTime` timestamp gate (Plan 38-06):**

```csharp
private static readonly ConcurrentDictionary<string, long> _lastFireTimeMs = new();
private const int RateLimitWindowMs = 5;   // 1/200Hz = 5ms (D-38-14)

private static void InvokeHandlerWithRateLimit(string path, Value handler, OscMessage msg)
{
    var nowMs = Environment.TickCount64;
    var lastMs = _lastFireTimeMs.GetOrAdd(path, 0L);
    if (nowMs - lastMs < RateLimitWindowMs) return;  // Drop-newest, sample-and-hold
    _lastFireTimeMs[path] = nowMs;

    // Invoke handler with the message's args
    var flowArgs = ConvertOscArgsToFlowValues(msg.ToArray());
    InvokeFlowLambda(handler, flowArgs);
}
```

**Thread-safety:** `ConcurrentDictionary` is the standard .NET concurrent-access primitive. Multiple UDP receive threads could in principle land on different paths simultaneously; per-key atomicity from `GetOrAdd` + the indexer assignment is sufficient for sample-and-hold (worst case: two threads on the SAME path both pass the gate in the same 5ms window; rare; acceptable per D-38-14 charitable interpretation).

**Advisory shape (Claude's discretion):** Default no-advisory per D-38-14 — sample-and-hold IS the expected behavior. If composer demand surfaces in early use, add one-shot `[osc] /<path>: flood detected (rate-limit active)` per-path per-process via `RenderingDiagnostics.WarnOnce` with key `osc-flood:<path>`.

**Confidence:** HIGH (standard concurrent-dict gate).

## §N — `(visualize seq)` Extension

**Current state:** `flow-lang/StandardLibrary/VisualizationFunctions.cs` (332 LOC). Renders ASCII piano-roll with `#` for sustained note body, `|` for bar lines, `+` and `-` for bottom separator, and a beat-number axis below.

**Articulation glyph injection (Plan 38-04):** The note-placement loop at lines 117-131 currently writes `#` for every column of every note's duration. To add articulation glyphs at note onsets:

1. Source articulation: `MusicalNoteData.Articulation` (Phase 28 enum: Accent/Staccato/Marcato/Tenuto/Sforzando/Legato/Normal). Verify property exists at plan-start by reading `flow-lang/StandardLibrary/Audio/MusicalNoteData.cs`.
2. Glyph mapping (per UI-SPEC, locked):

   | Articulation | Glyph |
   |--------------|-------|
   | Accent | `>` |
   | Staccato | `.` |
   | Marcato | `^` |
   | Tenuto | `_` |
   | Sforzando | `!` |
   | Legato | `~` (drawn in gap-cell BETWEEN connected notes) |
   | Normal | (no glyph — falls through to `#`) |

3. Composition rule (per UI-SPEC):

   ```csharp
   // Modified note-placement loop (replaces lines 125-129):
   foreach (var (midi, label, startBeat, duration, articulation) in noteEvents)
   {
       int row = maxMidi - midi;
       int startCol = (int)Math.Round(startBeat * columnsPerBeat);
       int endCol = (int)Math.Round((startBeat + duration) * columnsPerBeat);
       endCol = Math.Min(endCol, gridWidth);

       char onsetGlyph = articulation switch
       {
           Articulation.Accent => '>',
           Articulation.Staccato => '.',
           Articulation.Marcato => '^',
           Articulation.Tenuto => '_',
           Articulation.Sforzando => '!',
           _ => '#'  // Normal AND Legato — Legato gets gap-cell handling separately
       };

       for (int c = startCol; c < endCol; c++)
       {
           if (c >= 0 && c < gridWidth)
               grid[row, c] = (c == startCol) ? onsetGlyph : '#';
       }
   }

   // Legato gap-cell pass (NEW — runs after main note placement):
   var notesByRow = noteEvents.GroupBy(n => maxMidi - n.midiPitch);
   foreach (var rowGroup in notesByRow)
   {
       var sorted = rowGroup.OrderBy(n => n.startBeat).ToList();
       for (int i = 0; i < sorted.Count - 1; i++)
       {
           if (sorted[i].articulation == Articulation.Legato)
           {
               int gapCol = (int)Math.Round((sorted[i].startBeat + sorted[i].durationBeats) * columnsPerBeat);
               int nextStart = (int)Math.Round(sorted[i+1].startBeat * columnsPerBeat);
               if (nextStart - gapCol == 1 && gapCol >= 0 && gapCol < gridWidth
                   && grid[rowGroup.Key, gapCol] == ' ')
                   grid[rowGroup.Key, gapCol] = '~';
           }
       }
   }
   ```

4. **Backward compat:** Existing scripts that pass sequences without articulation data (or with `Articulation.Normal`) get the identical existing rendering. Pure additive change.

5. **Tick-mark row (also Plan 38-04):** Above the first pitch row, insert a tick-mark row with `+` at bar-line columns and `-` at all other columns, plus bar numbers placed at the column of each bar's first beat. See UI-SPEC §"Tick-Mark Row" for the exact format.

6. **`(inspect seq)` alias:** Add a second registration in `Register()`:
   ```csharp
   var sig3 = new FunctionSignature("inspect", [SequenceType.Instance], ParameterNames: ["seq"]);
   registry.Register("inspect", sig3, Visualize);  // Same dispatch function
   ```

**Confidence:** HIGH for ASCII glyph branch; MEDIUM on tick-mark row layout (composition with existing `barBoundaries`/`barLineColumns` collections needs careful alignment — verify against existing bottom-separator pattern at lines 166-177).

## §O — Test Infrastructure

**Live mode tests (Plan 38-01/02/03):** Use the existing capture-mode FlowEngine path. Live mode tests instantiate a `LiveReloadManager` with a temp file, simulate file edits via `File.WriteAllText`, and assert on the stderr advisory dedup-keyed sentinels.

```csharp
// In flow-lang.Tests/Phase38/LiveReloadManagerTests.cs (NEW):
[Fact]
public void EditWithStaleClosure_KeepsPreviousBuffer_EmitsDedupAdvisory()
{
    var tmp = Path.GetTempFileName();
    File.WriteAllText(tmp, "Int x = 5\nlive 1bar { (play (sine 440 1.0)) }\n");

    var stderr = new StringWriter();
    Console.SetError(stderr);

    using var mgr = new LiveReloadManager(tmp);
    var runTask = Task.Run(() => mgr.Run());

    Thread.Sleep(200);  // Initial render
    File.WriteAllText(tmp, "live 1bar { (play (sine (mul x 2) 1.0)) }\n");  // x removed
    Thread.Sleep(500);  // Debounce + render

    var stderrText = stderr.ToString();
    Assert.Contains("[live] stale closure: references removed binding 'x'", stderrText);
}
```

**OSC tests (Plan 38-06):** UDP loopback `127.0.0.1` with a port chosen from a small ephemeral-range pool (e.g., 13000-13100 with port-scan-until-bind helper). Round-trip a message, assert receive.

```csharp
[Fact]
public async Task OscSend_Receive_RoundTripsIntFloatString()
{
    int port = FindFreePort(13000, 13100);
    var received = new TaskCompletionSource<OscMessage>();
    using var receiver = new OscReceiver(port);
    receiver.Connect();
    _ = Task.Run(() => {
        var pkt = receiver.Receive();
        if (pkt is OscMessage m) received.SetResult(m);
    });

    using var sender = new OscSender(IPAddress.Loopback, port);
    sender.Connect();
    sender.Send(new OscMessage("/test", 42, 3.14f, "hello"));

    var msg = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Assert.Equal("/test", msg.Address);
    Assert.Equal(42, msg[0]);
    Assert.Equal(3.14f, (float)msg[1]);
    Assert.Equal("hello", msg[2]);
}
```

**Audio-input tests (Plan 38-05):** No real mic in CI. Add a capture-mode hook on `IAudioBackend` that feeds a fixture WAV file through `CaptureSamples` instead of calling `pa_simple_read`. Tests assert on the resampled output's frame count + RMS.

```csharp
[Fact]
public void MicBuffer_FedFixtureWav_ResamplesAndAttenuates20dB()
{
    var fixturePath = "tests/fixtures/440hz_48000_2ch_1s.wav";
    using var engine = new FlowEngine();
    engine.AudioManager.CaptureFixturePath = fixturePath;  // NEW property — capture-mode hook

    var result = engine.ExecuteScriptAndGetResult("(micBuffer 1.0)", "<test>");
    var buf = (AudioBuffer)result!.Data!;

    Assert.Equal(44100, buf.SampleRate);  // Resampled
    Assert.Equal(44100, buf.Frames);

    var rms = ComputeRMS(buf.Data);
    var origRms = ReadOriginalRMS(fixturePath);
    var expectedRms = origRms * Math.Pow(10, -20.0 / 20.0);  // -20 dB
    Assert.InRange(rms, expectedRms * 0.9, expectedRms * 1.1);
}
```

**Confidence:** HIGH (capture-mode FlowEngine + RMS regression baselines are already-established patterns from Phase 18/25/27/28).

## Project Constraints (from CLAUDE.md)

The following CLAUDE.md directives constrain Phase 38 implementation:

- **GSD workflow enforcement:** All file edits go through GSD commands; no direct repo edits outside the workflow.
- **Goals (in priority order):** Composer ergonomics first; genre-agnostic music-only scope; make easy cases fast. Phase 38's `live { }` block, REPL polish, audio input, and OSC all serve the composer ergonomics goal directly.
- **Non-goals:** Not general-purpose computation; not maximum runtime efficiency; not type strictness for its own sake; not music-genre-specific. Phase 38 respects all four.
- **Pre-public no-deprecation latitude (D-v1.5-01):** Breaking changes ship in single commits with in-repo migrators; no `flow migrate` CLI subcommand. Justifies the D-38-09 / D-38-10 / D-38-13 REQUIREMENTS.md wording overrides.
- **Charitable interpretation (feedback memory):** Prefer silent-and-documented assumptions over errors. D-38-04 / D-38-07 / D-38-13 / D-38-14 / D-38-15 all instantiate this rule.
- **Functional S-expression style (feedback memory):** No infix operators; prefix-only via `(add)` / `(mul)`. OSC builtins follow this — `(oscSend host port "/x" 1.5 "hello")` not `oscSend("/x", 1.5)`.
- **Minimal dependencies:** Every new NuGet is a liability. Phase 38 adds 2 NuGets + 1 ProjectReference — `Rug.Osc 1.2.5` (load-bearing, no hand-roll possible without burning Phase 38's full budget on OSC protocol bytes), `PrettyPrompt 4.1.1` (load-bearing for REPL-03 Ctrl+R + multi-line + persistent history; hand-roll fallback reserved per D-38-11), and `flow-lsp` as ProjectReference (first-party reuse).
- **Two-run cmp-clean determinism:** Phase 38 OFFLINE render paths (`writeWav`, `writeMidi`) STAY deterministic. `flow watch` + `live { }` explicitly opt OUT per D-v1.5-07 with stderr advisory at every entry. PRNG reseed at swap boundary via existing `PrngRegistry.ResetAtRenderBoundary()`.
- **Linux-first PulseAudio:** AUDIO-IN-01 extends the existing PulseAudio backend. Cross-platform audio capture deferred to Phase 41.
- **RMS-windowed regression testing (SPEC-8 ±0.5 dB / 100ms):** Live mode tests should use string-level assertion of stderr advisory + audio buffer continuity (the previous-buffer-keeps-playing contract), not byte-identical determinism.

## Common Pitfalls

### Pitfall 1: "Live session never dies mid-set" violated by failure path

**What goes wrong:** A `live { }` block re-evaluation hits a parse error, runtime exception, 30s timeout, or stale closure — and the orchestrator throws or hangs instead of reverting cleanly to the previous buffer. Audio thread starves; composer loses the performance.

**Why it happens:** Default exception handling in .NET propagates exceptions up the call stack. Without explicit recovery, an `engine.Execute` exception inside `LiveReloadManager.TriggerBackgroundRender` would propagate out of the Task.Run, get lost (unhandled task exception), and leave the pending-buffer Dict stale.

**How to avoid:** EVERY failure mode in `TriggerBackgroundRender` is wrapped in a try/catch with a dedup-keyed advisory + early return (NO buffer staged, previous keeps playing). The 30s timeout uses `Task.Wait(timeout)` which DOESN'T throw on timeout — it returns false, allowing controlled revert. Stale-closure detection runs BEFORE staging the new buffer so detection failure also reverts.

**Warning signs:** Composer reports "the audio just stopped after I saved" → check stderr for missing advisory + uncaught exception trace.

### Pitfall 2: Multi-block swap timing — independent quantize per block (D-38-02)

**What goes wrong:** Composer has `live 1bar { drums }` and `live 2bar { pad }`. Both blocks re-render together on save, but they MUST swap at INDEPENDENT bar boundaries (drums every bar, pad every 2 bars). Naive implementation swaps both at the next 1-bar boundary, breaking the pad's quantize contract.

**Why it happens:** Single-pending-buffer field (the existing `LiveReloadManager._pendingBuffer`) can only stage ONE pending swap at a time.

**How to avoid:** Replace `_pendingBuffer` with `_pendingBuffersByBlockId: Dictionary<int, LiveBlockBuffer>`. The streaming loop checks each registered block's quantize boundary independently. Each block has its own `_lastSwapBar` counter.

### Pitfall 3: Stale-closure detection returns false-positive on shadowed names

**What goes wrong:** Composer's `live 1bar { Int x = 5; (play (sine (mul x 100) 1.0)) }` declares a LOCAL `x` inside the block. Naive AST walker treats `x` as a file-scope reference and complains "removed binding 'x'" when no file-scope `x` exists.

**Why it happens:** Walker must track locally-bound names (from `VariableDeclaration`, lambda params, `each` callback params, etc.) and exclude them from the "references" set.

**How to avoid:** `LambdaCaptureAuditor.WalkStatement` maintains a `localScope: HashSet<string>` that mirrors the binding semantics — VariableDeclaration adds, scope exit removes. Lambda bodies push a new scope frame with the lambda's params.

### Pitfall 4: PrettyPrompt async/sync mismatch with Flow's sync Repl loop

**What goes wrong:** PrettyPrompt's `ReadLineAsync` is async; Flow's existing `Repl.Run()` is sync. Awkward `Task.Run().Wait()` wrapping can deadlock if the worker thread tries to schedule back on the captured sync context.

**Why it happens:** `Task.Wait()` blocks the calling thread; if PrettyPrompt's internal callbacks try to schedule continuations on that thread, deadlock.

**How to avoid:** Convert `Repl.Run()` to `async Task RunAsync()` and have `Main` call `RunAsync().GetAwaiter().GetResult()` at the very top of the process. OR use `.ConfigureAwait(false)` throughout (PrettyPrompt likely already does, but verify at Plan 38-04 plan-start).

### Pitfall 5: OSC `Receive()` blocks forever on `Cts.Cancel()`

**What goes wrong:** Composer calls `(oscStop handle)`. The CancellationToken fires, but the underlying `OscReceiver.Receive()` is blocked on a UDP `recv()` syscall and DOESN'T observe the token. The listener task hangs; the process can't exit cleanly.

**Why it happens:** `Rug.Osc.OscReceiver` doesn't accept a CancellationToken in its `Receive()` signature (the API predates CancellationToken-everywhere conventions).

**How to avoid:** Call `receiver.Dispose()` in the cancellation callback. Disposing the underlying socket causes the blocked `Receive()` to throw `ObjectDisposedException`, which the loop catches and exits. Wrap the `Dispose` invocation in a try/catch for idempotency.

```csharp
cts.Token.Register(() => { try { receiver.Dispose(); } catch { } });
```

### Pitfall 6: 30s CancellationToken doesn't actually terminate the worker

**What goes wrong:** A `live { }` block has an infinite loop. The 30s timer fires, but the worker continues running (FlowEngine doesn't check the token). After 10 saves with infinite loops, the process has 10 orphaned threads consuming CPU.

**Why it happens:** Option A (per §E) wraps `engine.Execute` in `Task.Run + Wait(timeout)` but FlowEngine.Execute is synchronous and doesn't cooperate with cancellation. The "cancellation" is a fiction at the FlowEngine layer.

**How to avoid (v1.5):** Accept the orphan-leak as a known limitation. Document in code-comment. Worker exits naturally when FlowEngine is GC'd (next successful render replaces the engine reference). In normal use, 30s timeouts are rare events; orphan accumulation is bounded by composer's iteration speed and ends at process exit.

**How to avoid (v1.6 if needed):** Implement Option B — thread `CancellationToken` through `FlowEngine.Execute` → `Interpreter.Execute` → ExecuteStatement, with `ct.ThrowIfCancellationRequested()` in hot paths (loop bodies, `each` callbacks, expression evaluator dispatch). Plan 38-01 should comment this hook for future implementation.

### Pitfall 7: Voice-name diff breaks when same instrument has different ordinals per render

**What goes wrong:** Composer renders `live 1bar { (piano (notes ...)) }`. First render gives voice name "piano:0". After edit, same code but the SongRenderer assigns "piano:1" (because voice allocation is dependent on rendering order). Diff sees ALL voices as "dropped + added" → preserves none.

**Why it happens:** Voice name is `instrument:ordinal` where ordinal is assigned at allocation time per render. Stable across renders ONLY if the voice allocation order is also stable.

**How to avoid:** Voice naming for live-preserve purposes should be SOURCE-LOCATION based, not allocation-order based. The voice's `Name` for diff purposes = `instrument:<source-location-hash>` (FNV-1a hash of the source location of the synth call). Plan 38-03 must add a `Voice.LiveDiffName` property (computed at voice-construction time) and use IT for the diff, not the existing `Voice.Name`.

### Pitfall 8: ANSI panel garbles `Console.WriteLine` output

**What goes wrong:** The 4-row panel sits at the top of the terminal. Composer prints debug output via `Console.WriteLine` — the output appears at row 5 (below panel) but overlaps with the panel when scrollback occurs.

**Why it happens:** No "scroll region" set, so newlines push the panel up and out of view.

**How to avoid:** Set a DEC top-margin / bottom-margin pair with `\x1b[5;<bottomRow>r` so rows 1-4 stay fixed and rows 5+ scroll. On exit, reset with `\x1b[r`. Test on each major terminal (xterm, Konsole, iTerm2, Windows Terminal, gnome-terminal) at plan-start — known quirk on some terminals where scroll regions interact with line-wrap.

### Pitfall 9: REPL completion menu blocks REPL on slow parse

**What goes wrong:** Each Tab keypress triggers a full parse of the REPL line + symbol-merge across 5 sources. For long pasted REPL inputs (e.g., 500-line snippet), parse takes 200ms+; REPL feels laggy.

**Why it happens:** `ParseSession.Parse` is synchronous and walks the full token stream.

**How to avoid:** Cache the parse result keyed by `(line, cursor_position)`; only re-parse when the line text changes. Run completion off-thread via PrettyPrompt's async callback hook (`GetCompletionItemsAsync` with CancellationToken — cancel in-flight completion if user types another char before menu appears). 200ms typical-parse latency is acceptable; document if >1s on a single Tab.

### Pitfall 10: OSC ephemeral-port test flakiness

**What goes wrong:** Test bind on port 13000; another test or process is using it; bind fails; CI flakes.

**Why it happens:** Hardcoded port allocation doesn't account for collisions in parallel test execution or CI shared infrastructure.

**How to avoid:** Helper `FindFreePort(int rangeStart, int rangeEnd)` that tries each port in range and returns the first that binds successfully via a probe `TcpListener` / `UdpClient`; OR check if Rug.Osc supports port=0 (need to verify at plan-start — XML doc was incomplete on this).

## Code Examples

### Wrapping `BuildItems` for PrettyPrompt

```csharp
// Source: extends flow-lsp/Handlers/CompletionHandler.cs:95-144 (static BuildItems API)
// + flow-lsp/Program.cs:30-78 (DI registrations as reference)
public class FlowCompletionEngine
{
    private readonly BuiltInIndex _builtIns;
    private readonly StdlibSymbolIndex _stdlib;
    private readonly KeywordIndex _keywords;
    private readonly UserSymbolIndex _users;
    private readonly ParseSession _parser;
    private readonly DocumentUri _replUri = DocumentUri.From("file:///<repl>");

    public FlowCompletionEngine()
    {
        var registry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(registry);
        _builtIns = new BuiltInIndex(registry);
        _stdlib = new StdlibSymbolIndex();
        _keywords = new KeywordIndex();
        _users = new UserSymbolIndex();
        _parser = new ParseSession();
    }

    public IReadOnlyList<CompletionItem> Complete(string line, int cursor)
    {
        var parseResult = _parser.Parse(line, "<repl>");
        var position = new Position(line: 0, character: cursor);
        return CompletionHandler.BuildItems(
            _replUri, line, parseResult.Ast, parseResult.Tokens, position,
            _builtIns, _users, _stdlib, _keywords).ToList();
    }
}
```

### `(micBuffer)` builtin

```csharp
// Source: pattern from flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
// + extends PulseAudioCaptureBackend (new in Plan 38-05)
namespace FlowLang.StandardLibrary.Audio;

public static class InputFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        var sig = new FunctionSignature("micBuffer", [SecondType.Instance],
            ParameterNames: ["duration"]);
        registry.Register("micBuffer", sig, MicBuffer);
        // Also accept Double for charitable interpretation:
        var sigD = new FunctionSignature("micBuffer", [DoubleType.Instance],
            ParameterNames: ["duration"]);
        registry.Register("micBuffer", sigD, MicBuffer);
    }

    public static Value MicBuffer(IReadOnlyList<Value> args)
    {
        double seconds = (double)args[0].Data!;
        const int captureRate = 48000;   // Common system default; resampled below
        const int channels = 2;

        // One-shot advisory per micBuffer call site per process — dedup via WarnOnce
        RenderingDiagnostics.WarnOnce("audio-in-attenuate:open",
            "[audio-in] mic stream attenuated -20 dB on open to prevent feedback");

        using var backend = new PulseAudioCaptureBackend();
        if (!backend.Initialize(captureRate, channels))
            throw new InvalidOperationException("No audio input available. Check PulseAudio source configuration.");

        int frames = (int)(seconds * captureRate);
        float[] raw = backend.CaptureSamples(frames, channels);

        // Resample to 44.1kHz if needed
        const int targetRate = 44100;
        float[] resampled = raw;
        if (captureRate != targetRate)
        {
            RenderingDiagnostics.WarnOnce($"audio-in-resample:{captureRate}",
                $"[audio-in] resampling capture stream from {captureRate} Hz to {targetRate} Hz (linear interpolation)");
            resampled = ResampleLinear(raw, captureRate, targetRate, channels);
        }

        // -20 dB attenuation (factor 10^(-20/20) = 0.1)
        const float attenFactor = 0.1f;
        for (int i = 0; i < resampled.Length; i++)
            resampled[i] *= attenFactor;

        var buffer = new AudioBuffer(resampled, targetRate, channels);
        return Value.Buffer(buffer);
    }

    private static float[] ResampleLinear(float[] input, int inRate, int outRate, int channels)
    {
        // See §J for full implementation
        if (inRate == outRate) return input;
        double ratio = (double)inRate / outRate;
        int inputFrames = input.Length / channels;
        int outputFrames = (int)Math.Ceiling(inputFrames / ratio);
        var output = new float[outputFrames * channels];
        for (int o = 0; o < outputFrames; o++)
        {
            double fIdx = o * ratio;
            int lo = (int)Math.Floor(fIdx);
            int hi = Math.Min(lo + 1, inputFrames - 1);
            float t = (float)(fIdx - lo);
            for (int ch = 0; ch < channels; ch++)
                output[o * channels + ch] = input[lo * channels + ch] +
                    (input[hi * channels + ch] - input[lo * channels + ch]) * t;
        }
        return output;
    }
}
```

### `(oscListen)` server with rate limit + bundle dispatch

```csharp
// Source: combines Rug.Osc OscReceiver pattern + D-38-14 rate-limit + D-38-15 bundle depth cap
namespace FlowLang.StandardLibrary.Network;

public static class OscFunctions
{
    private static readonly ConcurrentDictionary<string, long> _lastFireTimeMs = new();
    private const int RateLimitWindowMs = 5;   // 200Hz max
    private const int MaxBundleDepth = 8;

    public static Value Listen(IReadOnlyList<Value> args)
    {
        int port = (int)args[0].Data!;
        string path = (string)args[1].Data!;
        var handler = args[2];   // Function-typed Value

        var receiver = new OscReceiver(port);
        var cts = new CancellationTokenSource();

        // Dispose-on-cancel so blocked Receive() throws (Pitfall 5)
        cts.Token.Register(() => { try { receiver.Dispose(); } catch { } });

        Task listenerTask = Task.Run(() =>
        {
            try
            {
                receiver.Connect();
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var pkt = receiver.Receive();
                        DispatchPacket(pkt, path, handler, depth: 0);
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex)
                    {
                        // Charitable — log and continue
                        Console.Error.WriteLine($"[osc] receive error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                RenderingDiagnostics.WarnOnce($"osc-bind:{port}",
                    $"[osc] bind failed on port {port} — {ex.Message}; oscListen returned no handle");
            }
        }, cts.Token);

        return Value.OscHandle(new OscHandleData
        {
            Port = port, Path = path, Receiver = receiver, Cts = cts, ListenerTask = listenerTask
        });
    }

    private static void DispatchPacket(OscPacket pkt, string targetPath, Value handler, int depth)
    {
        if (depth > MaxBundleDepth)
        {
            RenderingDiagnostics.WarnOnce($"osc-bundle-depth:{targetPath}",
                $"[osc] bundle nesting depth exceeds {MaxBundleDepth} at {targetPath} — collapsing to flat dispatch");
            return;
        }
        if (pkt is OscBundle bundle)
        {
            for (int i = 0; i < bundle.Count; i++)
                DispatchPacket(bundle[i], targetPath, handler, depth + 1);
            return;
        }
        if (pkt is OscMessage msg && msg.Address == targetPath)
        {
            var nowMs = Environment.TickCount64;
            var lastMs = _lastFireTimeMs.GetOrAdd(targetPath, 0L);
            if (nowMs - lastMs < RateLimitWindowMs) return;  // Sample-and-hold
            _lastFireTimeMs[targetPath] = nowMs;

            var flowArgs = ConvertOscArgsToFlowValues(msg.ToArray());
            InvokeFlowLambda(handler, flowArgs);
        }
    }

    private static IReadOnlyList<Value> ConvertOscArgsToFlowValues(object[] oscArgs)
    {
        var result = new Value[oscArgs.Length];
        for (int i = 0; i < oscArgs.Length; i++)
        {
            result[i] = oscArgs[i] switch
            {
                int v => Value.Int(v),
                long v => Value.Long(v),
                float v => Value.Float(v),
                double v => Value.Double(v),
                string v => Value.String(v),
                bool v => Value.Bool(v),
                byte[] v => Value.Buffer(v),
                _ => Value.Void()
            };
        }
        return result;
    }

    private static void InvokeFlowLambda(Value lambda, IReadOnlyList<Value> args)
    {
        // Use existing FunctionOverload.Invoke path — see flow-lang/Interpreter for the
        // exact dispatch mechanism. Skipping detail here; pattern is established.
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Whole-script reload only (current `LiveReloadManager`) | Optional finer-grained `live { }` blocks (Plan 38-02) | Phase 38 (this) | Composer can mix per-block quantize timelines — `live 1bar { drums }` + `live 2bar { pad }` |
| 500ms file-watch debounce | 200ms debounce (D-38-05) | Phase 38 (this) | Tighter live-coding feel; matches LIVE-02 + Pitfall #21 |
| No evaluation timeout | 30s wall-clock cap with revert (D-38-07) | Phase 38 (this) | "Live session never dies mid-set" (Pitfall #12) |
| Console.ForegroundColor scattered through render path | Structured ANSI status panel (D-38-08) | Phase 38 (this) | Composer sees tempo / bar / voices at a glance |
| `Console.ReadLine` in REPL | PrettyPrompt with Tab + Ctrl+R + multi-line | Phase 38 (this) | LSP-backed completion, persistent history search |
| No audio input | `(micBuffer Second)` + PulseAudio capture | Phase 38 (this) | Granular-from-mic real-time texture, sample import via line-in |
| No network protocol | OSC server + client via Rug.Osc 1.2.5 | Phase 38 (this) | TouchOSC / Open Stage Control / hardware controller integration |

**Deprecated/outdated:**
- `Repl.cs:31-37` Ctrl+C handler preserved (don't deprecate; useful semantics).
- The single-`_pendingBuffer` field at `LiveReloadManager.cs:23` is rewritten to a Dict — composer-invisible change.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Voice.Name` property exists with shape "instrument:ordinal" | §B + §C + Pitfall 7 | Plan 38-03 must audit at start; if missing, ~5 LOC to add to `Voice` record. Low risk. |
| A2 | `Voice.LiveDiffName` should be SOURCE-LOCATION-based, not allocation-order-based, to survive re-renders | Pitfall 7 | If allocation order IS stable across renders (need to verify by reading SongRenderer), the existing `Voice.Name` suffices. If not, need source-location hash. Plan 38-03 audits. |
| A3 | `LambdaExpression` AST node tracks captured environment OR can be re-derived via AST walk | §C | Lambda capture inspection MAY require a single AST walk per lambda or per swap (Plan 38-03 confirms). Verified-by-design at flow-lang's recursive-descent parser exits + record-type AST nodes. |
| A4 | `ExecutionContext.GlobalFrame` exposes `HasVariable(name)` and `HasFunction(name)` query methods | §C | Common shape for scope frames; if missing, equivalent via Dictionary `ContainsKey`. Low risk; standard dict-backed scope. |
| A5 | PrettyPrompt's `IPromptCallbacks.GetCompletionItemsAsync` is the right hook for tab completion (CompletionItem type maps 1:1 from flow-lsp's CompletionItem) | §G | PrettyPrompt's CompletionItem and flow-lsp's CompletionItem are different types — need an adapter. Verified PrettyPrompt has `IPromptCallbacks` and async completion per README. Adapter is ~10 LOC. |
| A6 | PrettyPrompt persists history across sessions when given a file path | §H + REPL-03 | README mentions "optionally persistent across sessions" but doesn't show the API. Plan 38-04 reads README + sample app at plan-start to confirm. If not natively supported, ~30 LOC custom hook via `IPromptCallbacks`. |
| A7 | Rug.Osc `OscReceiver.Receive()` blocks until UDP packet arrives, and `Dispose()` from another thread unblocks it (throws ObjectDisposedException) | Pitfall 5 | Standard UDP socket behavior + Rug.Osc inherits .NET socket semantics; verified by WebSearch summary "Receive() method is blocking and will only return once a message is received". HIGH confidence. |
| A8 | `OscMessage(string address, params object[] args)` constructor accepts CLR-typed args and infers OSC type tag automatically | §L | Confirmed via WebSearch + XML doc summary; Rug.Osc handles type-tag encoding from runtime types. HIGH confidence. |
| A9 | `OscBundle` exposes nested packets via `Item[int]` indexer with `Count` property | §K | Verified via XML doc extract (WebFetch summary). HIGH confidence. |
| A10 | `PA_STREAM_RECORD = 2` (PulseAudio direction enum) | §I | VERIFIED via WebFetch of pulseaudio/src/pulse/def.h — enum order PA_STREAM_NODIRECTION(0) / PA_STREAM_PLAYBACK(1) / PA_STREAM_RECORD(2) / PA_STREAM_UPLOAD(3). HIGH confidence. |
| A11 | `pa_simple_read` signature mirrors `pa_simple_write` exactly except read-vs-write direction | §I | Both are PulseAudio Simple API — same calling convention, same data pointer + bytes + out error pattern. HIGH confidence per FreeDesktop docs. |
| A12 | Composer is OK with MPL-2.0 license for PrettyPrompt | §H + Standard Stack | MPL-2.0 is weak file-scope copyleft — modifications to PrettyPrompt own files must stay MPL, but consumers (including Flow) can be MIT/Apache. Compatible with Flow's distribution. LOW risk; flag at Plan 38-04 plan-start for explicit composer confirmation. |
| A13 | ANSI scroll-region (`\x1b[5;Nr`) works correctly on xterm/iTerm2/Konsole/Windows Terminal/gnome-terminal | §F + Pitfall 8 | xterm + Windows Terminal: HIGH confidence (xterm spec). iTerm2 + gnome-terminal: HIGH confidence (xterm-compatible). Konsole: MEDIUM — historically had quirks; verify at plan-start. Alternative top-of-screen redraw without scroll region also works as fallback. |
| A14 | `MusicalNoteData.Articulation` property exists with Phase 28 enum values | §N | Plan 38-04 must verify at start; if not directly on `MusicalNoteData`, may be on `MusicalNote` wrapper or on the `Bar`. Low-effort to chain access. |
| A15 | `RenderingDiagnostics.WarnOnce(key, message)` exists with the contract "emit message once per key per process" | Pitfalls + Code Examples | Used throughout the existing codebase per CLAUDE.md / CONTEXT.md references. HIGH confidence. |
| A16 | The `_engineForPrng` reference in `LiveReloadManager` would be a single persistent FlowEngine instance used for live state | §D | Currently `RenderScript` creates a fresh engine per render (line 344). Plan 38-03 must decide: either (a) keep fresh-engine-per-render and reseed PRNG inside that engine before staging buffer, OR (b) reuse the streaming engine and reseed at swap. (a) is simpler; (b) preserves PRNG cache across renders. Recommend (a) per existing pattern. |

## Open Questions (RESOLVED)

> Each question carries an inline **Recommendation:** that routes to the plan owning the resolution. Cross-referenced into the plan-checker Dimension 11 audit.

1. **Does `Voice.Name` exist today, and is the format stable across re-renders?**
   - What we know: Phase 28 docs reference "piano:3" / "drums:1" voice names; SongRenderer assigns them at render time.
   - What's unclear: Whether the ordinal "3" is allocation-order-derived (changes per render) or source-location-derived (stable).
   - Recommendation: Plan 38-03 reads `flow-lang/StandardLibrary/Audio/Voice.cs` + `SongRenderer.cs` at plan-start; add `Voice.LiveDiffName` if needed.

2. **Does Konsole correctly honor ANSI scroll region (`\x1b[5;Nr`)?**
   - What we know: xterm spec defines it; most xterm-compatible terminals honor it.
   - What's unclear: Konsole's xterm-compat layer historically had divergences.
   - Recommendation: Plan 38-01 includes a Konsole smoke test; fall back to top-of-screen redraw without scroll region if Konsole misbehaves (composer-facing acceptable).

3. **Does PrettyPrompt's `History` API support persistent file-backed history out of the box?**
   - What we know: README mentions "optionally persistent across sessions" without API details.
   - What's unclear: Whether to pass a file path to `Prompt` constructor or wire via `IPromptCallbacks`.
   - Recommendation: Plan 38-04 reads PrettyPrompt source + sample app at plan-start; if not native, ~30 LOC hook.

4. **Does Rug.Osc `OscReceiver` constructor accept port=0 for ephemeral binding, or do tests need a port-scan helper?**
   - What we know: Standard .NET socket bind to port 0 picks an ephemeral port; whether Rug.Osc passes through is uncertain.
   - What's unclear: Without source access, can't tell from docs alone.
   - Recommendation: Plan 38-06 plan-start test: instantiate `new OscReceiver(0)` and check `receiver.LocalEndPoint.Port` after Connect; fall through to port-scan helper if it doesn't work.

5. **Whether to ship D-38-13's `types=",hd"` named-arg escape hatch vs. a separate `(oscSendTyped)` builtin?**
   - What we know: Both approaches work; named-arg is more idiomatic with Phase 36 D-36-11 universal named-arg syntax.
   - What's unclear: Composer preference.
   - Recommendation: Default to named-arg `types=` form (cleaner, matches D-36-11); flag at Plan 38-06 plan-start for composer confirmation.

6. **Should the live block opt-out advisory (D-v1.5-07) fire on every BAR or only on first ENTRY?**
   - What we know: CONTEXT D-v1.5-07 says "stderr advisory on every entry"; UI-SPEC advisory catalog uses dedup key `live-determinism-optout:<line>` (one-shot per live block per process).
   - What's unclear: "Every entry" — is each loop-iteration an entry, or just the first enrollment?
   - Recommendation: One-shot per block per process via dedup (matches UI-SPEC); composer can find the advisory in stderr; spamming every bar would drown out the panel.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | All Phase 38 surfaces | ✓ | 10.0.107 | — |
| `pactl` (PulseAudio CLI) | AUDIO-IN-01 smoke testing | ✓ | 17.0 | Composer can test via real mic on dev machine |
| `libpulse-simple.so.0` (PulseAudio Simple shared lib) | AUDIO-IN-01 runtime | ✓ | (via libpulse0 1:17.0+dfsg1) | None — Linux-primary scope |
| `pulse/simple.h` (libpulse-simple-dev header) | AUDIO-IN-01 compile time | ✗ | — | Not needed — P/Invoke binds to .so at runtime; no compile-time header dependency |
| PulseAudio input source (mic, line-in) | AUDIO-IN-01 runtime tests on dev | ✓ | `alsa_input.pci-0000_00_1f.3.analog-stereo` (PipeWire) detected | Fixture WAV file replay via capture-mode hook for CI |
| Network UDP loopback (127.0.0.1) | OSC-01 / OSC-02 tests | ✓ (standard on Linux) | — | — |
| `Rug.Osc 1.2.5` NuGet package | OSC-01 / OSC-02 | ✓ (verified on api.nuget.org) | 1.2.5 | None — no clean alternative; hand-roll OSC is out of phase scope |
| `PrettyPrompt 4.1.1` NuGet package | REPL-03 | ✓ (verified on api.nuget.org) | 4.1.1 | Hand-roll TUI editor (~400-600 LOC) per D-38-11 IF MPL-2.0 license rejected |
| `flow-lsp` first-party project | REPL-01 | ✓ (in-tree at /home/noah/Desktop/projects/flow-sharp/flow-lsp) | net10.0 | None — already a dependency of the build |

**Missing dependencies with no fallback:** None — every Phase 38 dependency is available or has a documented fallback.

**Missing dependencies with fallback:** `pulse/simple.h` — fallback is "P/Invoke binds at runtime; no compile dependency". Composer-side `PrettyPrompt` license concern (if it surfaces) — fallback is hand-roll TUI per D-38-11.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing, used by `flow-lang.Tests` / `flow-midi.Tests`) + pure-Flow `(test ...)` framework from Phase 35 TEST-01 (`tests/test_*.flow`) |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` + per-test xunit autodetect; pure-Flow tests discovered by `flow test [path]` CLI subcommand |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase38"` |
| Full suite command | `dotnet test` (runs xunit; phase tests scoped via Phase38 namespace) + `for t in tests/test_live_*.flow tests/test_repl_*.flow tests/test_audio_in_*.flow tests/test_osc_*.flow; do dotnet run --project flow-interpreter "$t"; done` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LIVE-01 | `live <quantize> { body }` parses; body executes once at first run; stderr advisory emitted once per block per process | unit | `dotnet test --filter "Phase38.LiveBlockParserTests"` | ❌ Wave 0 — new |
| LIVE-01 | Multiple `live` blocks coexist with independent quantize timelines | integration | `dotnet test --filter "Phase38.MultiLiveBlockTests"` | ❌ Wave 0 — new |
| LIVE-01 | `live 1bar { ... }` quantize value `1bar` parses as Bar literal | unit | `dotnet test --filter "Phase38.LiveBlockQuantizeTests"` | ❌ Wave 0 — new |
| LIVE-02 | 200ms file-watch debounce (rapid saves don't queue) | integration | `dotnet test --filter "Phase38.LiveReloadDebounceTests"` | ❌ Wave 0 — new |
| LIVE-02 | 30s evaluation cap on infinite-loop body → revert to previous + dedup advisory | integration | `dotnet test --filter "Phase38.LiveReloadTimeoutTests"` | ❌ Wave 0 — new |
| LIVE-02 | ANSI status panel renders 4 rows; falls back to plain line on `Console.IsOutputRedirected` | unit | `dotnet test --filter "Phase38.AnsiStatusPanelTests"` | ❌ Wave 0 — new |
| LIVE-02 | Parse error in re-eval → keep previous buffer + `[live]` advisory (dedup per `parse:<line>`) | integration | `dotnet test --filter "Phase38.LiveReloadParseErrorTests"` | ❌ Wave 0 — new |
| LIVE-03 | Voice-pool state preserved across reload when voice name (`Voice.LiveDiffName`) survives | integration | `dotnet test --filter "Phase38.VoicePreservationTests"` | ❌ Wave 0 — new |
| LIVE-03 | PRNG state reseeded at swap boundary via `PrngRegistry.ResetAtRenderBoundary()` | unit | `dotnet test --filter "Phase38.PrngReseedAtSwapTests"` | ❌ Wave 0 — new |
| LIVE-03 | Stale closure (references removed binding) → advisory + revert | integration | `dotnet test --filter "Phase38.StaleClosureDetectionTests"` | ❌ Wave 0 — new |
| LIVE-03 | Musical context stack resets to file-scope on swap | unit | `dotnet test --filter "Phase38.LiveContextResetTests"` | ❌ Wave 0 — new |
| REPL-01 | Tab completion via `CompletionHandler.BuildItems` returns expected merged item set for `(transp` partial input | unit | `dotnet test --filter "Phase38.ReplCompletionTests"` | ❌ Wave 0 — new |
| REPL-01 | Token-heuristic fallback on partial-parse failure returns identifier-prefix matches | unit | `dotnet test --filter "Phase38.ReplCompletionFallbackTests"` | ❌ Wave 0 — new |
| REPL-02 | `:help transpose` prints summary + params from `BuiltInDocs.TryGet("transpose")` | unit | `dotnet test --filter "Phase38.ReplHelpFnTests"` | ❌ Wave 0 — new |
| REPL-02 | `:help fooBar` (unknown) prints `[help] no documentation entry for 'fooBar' ...` to stdout in yellow | unit | `dotnet test --filter "Phase38.ReplHelpFnTests"` | (same file) |
| REPL-03 | Persistent history file written to `~/.config/flow/history` with 0600 perms | integration | `dotnet test --filter "Phase38.ReplHistoryFileTests"` | ❌ Wave 0 — new |
| REPL-03 | Ctrl+R reverse history search returns most-recent match (via PrettyPrompt) | manual-only | Composer UAT (interactive — can't automate Ctrl+R keypress reliably in xunit) | UAT |
| REPL-03 | Multi-line input via continuation prompt + paren-balanced detection (preserved from `Repl.IsInputComplete`) | unit | `dotnet test --filter "Phase38.ReplMultilineTests"` | ❌ Wave 0 — new |
| REPL-04 | `(inspect seq)` and `(visualize seq)` both invoke `VisualizationFunctions.Visualize`; ASCII output contains articulation glyphs at note onsets | unit | `dotnet test --filter "Phase38.VisualizeArticulationTests"` | ❌ Wave 0 — new |
| REPL-04 | Tick-mark row rendered above first pitch row with `+` at bar columns | unit | `dotnet test --filter "Phase38.VisualizeTickMarkTests"` | ❌ Wave 0 — new |
| REPL-04 | Backward compat: existing scripts without articulation data render identically to pre-Phase-38 | smoke | `for t in tests/test_visualize_*.flow; do dotnet run --project flow-interpreter "$t"; done` (pre-existing tests must pass) | ✓ |
| AUDIO-IN-01 | `(micBuffer 1.0)` returns a `Buffer` with 44100 frames, attenuated -20 dB; fixture WAV harness | integration | `dotnet test --filter "Phase38.MicBufferFixtureTests"` | ❌ Wave 0 — new |
| AUDIO-IN-01 | Stderr advisory `[audio-in] mic stream attenuated -20 dB on open ...` emitted once per call | unit | `dotnet test --filter "Phase38.MicBufferAdvisoryTests"` | (same file) |
| AUDIO-IN-02 | Linear interpolation resampler: 48 kHz input → 44.1 kHz output preserves RMS within 0.5 dB | unit | `dotnet test --filter "Phase38.ResamplerLinearTests"` | ❌ Wave 0 — new |
| AUDIO-IN-02 | Captured Buffer composes with `(granular ...)` from Phase 37 DSP-01 | integration | `dotnet test --filter "Phase38.MicComposesWithGranularTests"` | ❌ Wave 0 — new |
| OSC-01 | `(oscListen 13050 "/x" handler)` returns OscHandle; sending `(oscSend "127.0.0.1" 13050 "/x" 42)` invokes handler with `[Int(42)]` args within 500ms | integration | `dotnet test --filter "Phase38.OscRoundTripTests"` | ❌ Wave 0 — new |
| OSC-01 | Rate-limit at 200 Hz drops 2nd message within 5ms window | unit | `dotnet test --filter "Phase38.OscRateLimitTests"` | ❌ Wave 0 — new |
| OSC-01 | Bundle dispatch: nested OscBundle with depth 9 emits `[osc] bundle nesting depth exceeds 8` advisory and skips | unit | `dotnet test --filter "Phase38.OscBundleDepthCapTests"` | ❌ Wave 0 — new |
| OSC-01 | `(oscStop handle)` cancels listener; receiver disposed; task terminates within 1s | integration | `dotnet test --filter "Phase38.OscStopTests"` | ❌ Wave 0 — new |
| OSC-02 | `(oscSend host port path Int Float String)` infers type tags `,ifs` (smallest-tag-that-fits) | unit | `dotnet test --filter "Phase38.OscSendInferenceTests"` | ❌ Wave 0 — new |
| OSC-02 | `types=",hd"` named-arg overrides inference | unit | `dotnet test --filter "Phase38.OscSendTypeOverrideTests"` | ❌ Wave 0 — new |
| OSC-02 | Send bundle via `(oscSendBundle host port bundle [timetag])` round-trips through receiver | integration | `dotnet test --filter "Phase38.OscBundleRoundTripTests"` | ❌ Wave 0 — new |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "Phase38.<TaskScopeNamespace>"` (e.g., for Plan 38-04 `dotnet test --filter "Phase38.Repl"`)
- **Per wave merge:** `dotnet test --filter "Phase38"` (all Phase 38 unit + integration tests) + `for t in tests/test_*_*.flow; do dotnet run --project flow-interpreter "$t"; done` (Flow scripts)
- **Phase gate:** Full `dotnet test` (all phases) green + all `tests/test_*.flow` pass before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `flow-lang.Tests/Phase38/` directory — does not exist yet; all 23 Phase 38 unit/integration test files are new
- [ ] `tests/test_live_*.flow` — new pure-Flow tests for live mode (use capture-mode FlowEngine harness)
- [ ] `tests/test_repl_*.flow` — new pure-Flow tests for REPL (test scripts pipe through `ExecuteScriptAndGetResult`)
- [ ] `tests/test_audio_in_*.flow` — new pure-Flow tests with fixture WAV input
- [ ] `tests/test_osc_*.flow` — new pure-Flow tests with UDP loopback
- [ ] `tests/fixtures/440hz_48000_2ch_1s.wav` — synthetic test fixture (~340 KB; generate via existing Flow `(sine 440 1.0)` rendered at 48 kHz)
- [ ] `flow-lang.Tests/Phase38/Helpers/FindFreePort.cs` — ephemeral UDP port helper for OSC tests
- [ ] `flow-lang.Tests/Phase38/Helpers/CaptureModeAudioFixtureHook.cs` — replaces `pa_simple_read` with fixture WAV replay
- [ ] `flow-interpreter.Tests/Phase38/AnsiStatusPanelTests.cs` — string-buffer redirect of `Console.Out` to assert on ANSI sequences emitted

## Security Domain

Phase 38 introduces three new attack surfaces — UDP OSC listening on user-chosen ports, mic audio capture, and file-watch-triggered code reload. Treat each via ASVS.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | OSC 1.0 has no auth; composer-trust model |
| V3 Session Management | no | No sessions in Flow's model |
| V4 Access Control | no | Single-user desktop tool |
| V5 Input Validation | yes | OSC packets parsed by Rug.Osc (battle-tested); bundle depth cap 8 (D-38-15) is the validation layer for nesting; OSC argument type-tag whitelist enforced via Rug.Osc internal parsing; address pattern is literal-only in v1.5 (D-38-16 — no wildcard match → no regex DoS) |
| V6 Cryptography | no | No crypto — OSC 1.0 spec is plaintext; v1.6+ can add OSC-over-TLS if composer demand surfaces |
| V7 Error Handling | yes | Charitable interpretation throughout — never throw on bad OSC packet, bad audio frame, bad live block; advisory + revert via existing `RenderingDiagnostics.WarnOnce` pattern |
| V8 Data Protection | yes | REPL history at `~/.config/flow/history` is `0600` (private to user — composer may type credentials into REPL) per UI-SPEC |
| V9 Communications | yes | OSC binds to user-chosen UDP port; document that exposing port to internet is composer-risk; default bind to 0.0.0.0 (Rug.Osc default) — composer can specify a more restrictive endpoint via `(oscListenLocal port)` v1.6 extension if needed |
| V10 Malicious Code | yes | File-watch triggers re-evaluation of user's own `.flow` source — same trust boundary as `dotnet run --project flow-interpreter file.flow`; no remote code; FileSystemWatcher is scoped to a single file's directory |
| V11 Business Logic | n/a | n/a |
| V12 Files | yes | History file scoped to `~/.config/flow/` per Phase 30 XDG precedent; mic capture writes to in-memory buffer only (no file persistence unless composer explicitly calls `writeWav`) |
| V13 API | n/a | No HTTP API |
| V14 Configuration | yes | `Console.IsOutputRedirected` / `NO_COLOR` / `TERM=dumb` / `--no-color` checks for ANSI fallback (UI-SPEC locked) |

### Known Threat Patterns for {stack}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| OSC bundle nesting DoS (crafted infinite-depth bundle) | Denial of Service | Depth cap 8 per D-38-15 + dedup advisory |
| OSC flood (LFO at 1 kHz) | Denial of Service | Rate-limit 200 Hz per path per D-38-14; drop-newest sample-and-hold |
| UDP packet spoofing (sender pretends to be another host) | Spoofing | OSC 1.0 has no auth; composer-trust per CONTEXT (single-user desktop); document risk in `examples/live/osc_controller.flow` chapter |
| Mic feedback loop when playback active | Denial of Service (audio integrity) | -20 dB auto-attenuation on `(micBuffer)` open per AUDIO-IN-01 + Pitfall #24 |
| File-watch re-evaluates malicious code | Tampering | Composer-trust model — user is editing their own file; FileSystemWatcher scoped to single file's directory |
| REPL history captures secrets | Information Disclosure | `~/.config/flow/history` `0600` perms (private) per UI-SPEC; composer can `:clear` to delete history |
| ANSI escape injection from OSC string arg | Tampering (terminal hijack via crafted string in `(print)` of received OSC arg) | Sanitize stderr output paths — Flow's existing `(print)` doesn't filter escape sequences; ADD: strip `\x1b` chars from advisory bodies in `LiveStatusPanel.PublishAdvisory` and in `RenderingDiagnostics.WarnOnce`. Plan 38-06 + Plan 38-01 must include this defense |

## Sources

### Primary (HIGH confidence)
- [VERIFIED via `cat`] `/home/noah/Desktop/projects/flow-sharp/CLAUDE.md` (project guidelines)
- [VERIFIED via `Read`] `flow-interpreter/LiveReloadManager.cs` (389 LOC — rewrite target)
- [VERIFIED via `Read`] `flow-interpreter/Repl.cs` (272 LOC — extension target)
- [VERIFIED via `Read`] `flow-lang/Audio/PulseAudioSimpleBackend.cs` (310 LOC — extension target)
- [VERIFIED via `Read`] `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` (197 LOC)
- [VERIFIED via `Read`] `flow-lang/Runtime/PrngRegistry.cs` (222 LOC — `ResetAtRenderBoundary()` at line 122)
- [VERIFIED via `Read`] `flow-lang/StandardLibrary/VisualizationFunctions.cs` (332 LOC — extension target)
- [VERIFIED via `Read`] `flow-lang/StandardLibrary/BuiltInDocs.cs` (201 LOC — `TryGet` at line 199)
- [VERIFIED via `Read`] `flow-lang/Ast/Statements/MusicalContextStatement.cs` (22 LOC — AST pattern analog)
- [VERIFIED via `Read`] `flow-lang/Parsing/Parser.cs` lines 90-179 + 649-851 (keyword dispatch + ParseMusicalContextStatement)
- [VERIFIED via `Read`] `flow-lang/Core/FlowEngine.cs` (381 LOC — `Execute` at line 221 has NO CancellationToken)
- [VERIFIED via `Read`] `flow-lang/Runtime/Value.cs` (366 LOC — type discrimination dispatch via `Value.Type`)
- [VERIFIED via `Read`] `flow-lsp/Handlers/CompletionHandler.cs` lines 1-220 (static `BuildItems` at lines 95-144)
- [VERIFIED via `Read`] `flow-lsp/Program.cs` (89 LOC — DI registration pattern)
- [VERIFIED via `cat`] `/home/noah/Desktop/projects/flow-sharp/.planning/phases/38-live-coding-2-0/38-CONTEXT.md`
- [VERIFIED via `Read`] `/home/noah/Desktop/projects/flow-sharp/.planning/phases/38-live-coding-2-0/38-UI-SPEC.md`
- [VERIFIED via `Read`] `/home/noah/Desktop/projects/flow-sharp/.planning/REQUIREMENTS.md` (Phase 38 REQs at lines 89-110)
- [VERIFIED via `Read`] `/home/noah/Desktop/projects/flow-sharp/.planning/research/STACK.md`
- [VERIFIED via `Read` partial 1-638] `/home/noah/Desktop/projects/flow-sharp/.planning/research/PITFALLS.md` (Pitfalls #10, #12, #13, #21, #24)

### Secondary (MEDIUM-HIGH confidence)
- [CITED: https://www.nuget.org/packages/Rug.Osc] WebFetch — Rug.Osc 1.2.5 (zero deps, MIT-style, Jan 2014, .NET Standard 2.0)
- [CITED: https://www.nuget.org/packages/PrettyPrompt] WebFetch — PrettyPrompt 4.1.1 (Sept 2023, MPL-2.0, 104.4K downloads, .NET 6+)
- [CITED: https://www.nuget.org/packages/ReadLine] WebFetch — ReadLine 2.0.1 (June 2018, MIT, 2.1M downloads, .NET Standard 2.0)
- [CITED: https://github.com/tonerdo/readline] WebFetch — ReadLine project status (inactive since 2017, no Ctrl+R, no multi-line)
- [CITED: https://github.com/waf/PrettyPrompt] WebFetch — PrettyPrompt status (199 stars, 477 commits, MPL-2.0, history-filtering "similar to PSReadLine's HistorySearchBackward", multi-line via Shift+Enter, IPromptCallbacks)
- [CITED: https://github.com/waf/PrettyPrompt/blob/main/README.md] WebFetch — basic ReadLineAsync loop pattern, IPromptCallbacks
- [CITED: https://github.com/pulseaudio/pulseaudio/blob/master/src/pulse/def.h] WebFetch — `pa_stream_direction` enum: PA_STREAM_RECORD = 2
- [CITED: https://freedesktop.org/software/pulseaudio/doxygen/simple.html] WebFetch + WebSearch — pa_simple_* signatures + blocking semantics
- [CITED: https://freedesktop.org/software/pulseaudio/doxygen/parec-simple_8c-example.html] WebSearch summary — canonical parec-simple.c pattern (PA_STREAM_RECORD direction + pa_simple_read in loop)
- [CITED: https://github.com/OmniSharp/csharp-language-server-protocol/blob/master/sample/SampleServer/Program.cs] WebFetch — LanguageServer.From() basic embedding pattern
- [CITED: https://github.com/xioTechnologies/OSC-Terminal/blob/master/OSC%20Terminal/OSC%20Terminal/Rug.Osc/Rug.Osc.XML] WebFetch — Rug.Osc XML doc (OscPacket / OscBundle / OscTimeTag signatures)
- [CITED: WebSearch hot-examples.com summary] OscReceiver.Receive() blocking; TryReceive() non-blocking; OscReceiver(int port) + Connect() + Receive() loop pattern; thread-safe library

### Tertiary (LOW confidence, flagged for plan-start verification)
- [ASSUMED] Rug.Osc 1.2.5 `OscMessage(string, params object[])` constructor accepts arbitrary CLR types and infers type tag — confirmed indirectly via xioTechnologies XML doc summary; verify at Plan 38-06 plan-start with a 10-LOC smoke test
- [ASSUMED] PrettyPrompt's persistent-history API surface — README mentions "optionally persistent across sessions" without code; verify at Plan 38-04 plan-start
- [ASSUMED] Konsole ANSI scroll-region (`\x1b[N;Mr`) is supported — xterm spec is portable but Konsole historically had quirks; verify at Plan 38-01 plan-start

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all three NuGet packages verified live on NuGet API + source-repo cross-reference + STACK.md research; first-party ProjectReference to flow-lsp is verified by reading the static `BuildItems()` method directly
- Architecture: HIGH — every Phase 38 surface has a strong in-tree analog identified by file:line citation
- Pitfalls: HIGH — pitfalls drawn from existing PITFALLS.md + 5 new Phase-38-specific pitfalls identified from research

**Research date:** 2026-05-23
**Valid until:** 2026-06-22 (30 days — Rug.Osc / PulseAudio / OSC 1.0 / .NET 10 are all stable; PrettyPrompt may iterate but 4.1.1 is current; in-tree code may change but the patterns documented are durable)
