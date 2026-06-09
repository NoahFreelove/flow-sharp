# Flow Language — Codebase Audit

**Date:** 2026-06-09
**Scope:** Full repo — `flow-lang/`, `flow-interpreter/`, `flow-cli/`, `flow-lsp/`, `flow-midi/`, `flow-site/`, `scripts/`, docs (README / FEATURES / wiki / CLAUDE.md), tests.
**Method:** 10 parallel review agents (5 bug hunters by subsystem, 2 code-quality, 1 docs-accuracy, 1 composer-gaps, 1 feature ideation), then **one independent adversarial verifier per bug claim** (46 claims → 42 confirmed, 4 refuted). 56 agents total. Every confirmed finding below was re-read in today's code by a second agent instructed to refute it; verifier corrections are folded in. Prior-audit (2026-04-18) items were re-checked — survivors are tagged **[still open]**.
**Confidence note:** file:line cites were verified by the skeptic pass, but spot-check before large refactors. The four refuted claims are listed in §9 for the record.

---

## 1. Top-priority synthesis (read this if nothing else)

1. **Phase 38 is partially fiction.** The REPL modernization (PrettyPrompt tab-completion, Ctrl+R, persistent history) and the entire LIVE-03 live-coding pipeline (per-block quantized swaps, stale-closure gate, voice preservation, per-swap PRNG reseed, file-scope-edit detection) are **dead code** — implemented, tested directly by unit tests, and never wired into the production `flow repl` / `flow watch` paths. CLAUDE.md documents all of it as shipped. (§5.1, §5.2)
2. **Every core melodic transform silently destroys voice-block polyphony** (`transpose`/`invert`/`retrograde`/`repeat`/`concat`/`legato`/... drop `BarData.ParallelVoices` → silence) and rebuilds notes with a 12-of-17-arg ctor that strips `IsChordTone` (chords re-arpeggiate + overflow bars), tuplet fractions, quantize/legato/portamento fields. Found independently by two agents. (§4.1, §4.2)
3. **`compress`/`sidechain` mute the first ~0.5 s of every call** (gain envelope initialized to −96 dB then release-curves up to unity). (§3.1)
4. **PhaseVocoder has no COLA normalization** — `(stretch buf 1.001)` is **+6 dB** louder than factor 1.0 (verifier measured; worse than originally claimed), with severe AM ripple at factor > 2. PSOLA stretching produces pitch-rate tremolo for factor > 1. These feed `stretch`, `pitchShift`, and the DRUM-01 drum path. (§3.2, §3.4)
5. **The flowlang.dev playground will fail its pending HUMAN-UAT as committed:** OAuth gist save destroys the composer's code; all five "press Run to hear it" showcase deep-links render zero audio (sources end in `writeWav`, never `play`); the Stop button is a complete no-op for script audio; the MIDI download is dead end-to-end (`RunResult.midi` hardcoded null); the committed WASM AppBundle is stale (predates the PolyBLEP fix); debug spew pollutes the frozen stderr contract. (§6, §5 realtime)
6. **OSC handlers execute on a thread-pool thread against the shared, non-thread-safe Interpreter/ExecutionContext** — concurrent frame push/pop with the foreground evaluator. (§5.3)
7. **The install front door is broken for v1.5:** `install.sh` builds artifact URLs that don't match `publish.sh` naming (and defaults to 0.1.0); `flow version` inside a v1.5 archive reports `0.1.0-phase30`. (§7.1)
8. **Docs drift is systemic:** CLAUDE.md still documents the removed RtMidi.Core dependency, "two projects" (of nine), "only DryWetMidi" (of four packages); FEATURES.md denies shipped macOS/Windows playback, REPL completion, and PolyBLEP anti-aliasing; Phase 38/40 surfaces (`live`, `@osc`, `@midi`, `@jack`, `micBuffer`) have **zero** composer-facing documentation. (§7)
9. **Prerequisite-gated tests silently PASS instead of SKIP** across the 55k-LOC suite — green CI with zero assertions exercised on any box without the Linux MIDI stack. (§8.1)
10. **Quoted strings are silently re-typed as music literals** (`"10s"` → Second, `"a"` → Note A4) because the evaluator can't distinguish quoted strings from music-literal tokens. (§2.1)

---

## 2. Confirmed bugs — language core

| # | Sev | Where | Finding |
|---|-----|-------|---------|
| 2.1 | high | `Interpreter/ExpressionEvaluator.cs:71` | **Quoted strings re-typed as music literals.** Every string-payload literal runs through `TryParseSpecialLiteral`; the parser emits the same undiscriminated `LiteralExpression` for quoted strings and music-literal tokens. `String s = "10s"` errors "Cannot assign Second to String"; `"a"` becomes Note A4; non-erroring positions silently change equality/dict-key/overload dispatch. Fix: add a `LiteralKind` discriminator set by the parser. |
| 2.2 | high | `Runtime/ExecutionContext.cs:682` | **Overload cache never invalidated on `PopFrame`.** A nested proc that non-ambiguously shadows an outer proc (e.g. local `f(Int)` over global `f(Double)`) and is called in-scope leaves its overload cached; after the frame pops, calls execute the popped local body. Verifier-corrected repro: requires a non-ambiguous shadow (identical signatures hit the ambiguity path, which isn't cached). Fix: invalidate on pop when the frame declared functions, or add a scope-generation counter to the cache key. |
| 2.3 | med | `Interpreter/Interpreter.cs:734` (+350, 437, 512; `ExpressionEvaluator.cs:1236-1252`) | **`return` inside section/context/live blocks leaks `_returnValue`** — rest of the program silently skipped with no diagnostic; a `return` inside a *called* parameterized section becomes the enclosing proc's return value. Verifier adds: the flag also persists across REPL evals (`Execute()` never resets it). |
| 2.4 | med | `Interpreter/ExpressionEvaluator.cs:670-724` | **Bare function name on `~>` RHS bypasses overload resolution** — zero-arg overloads get auto-invoked during RHS evaluation; otherwise `overloads[0]` is picked blindly and invoked with no coercion (wrong overload → internal `InvalidCastException`). Verifier correction: `->` is NOT affected (parser rewrites bare-identifier RHS at parse time); only `~>` has the defect. |
| 2.5 | med | `Runtime/ExecutionContext.cs:674` + `StackFrame.cs:68-83` | **User procs are dynamically scoped.** Call frames parent to `CurrentFrame`, so a proc body reads the *caller's* locals and an assignment to an undeclared name (e.g. typo'd param) silently mutates the caller's variable. Contradicts `wiki/Language-Basics.md:442` and the lambda capture model. Fix: mark call frames as lookup boundaries. |
| 2.6 | med | `Runtime/ModuleLoader.cs:284` | **Healthy imports fail after any earlier soft error.** Module-execute checks global `_errorReporter.HasErrors` (already true from accumulated pre-import errors) instead of a before/after delta → false "Failed to import '@audio'". Fix: snapshot error count around module execution. |
| 2.7 | low | `Interpreter/ExpressionEvaluator.cs:1080` | **Interpolated strings bypass `Value.ToString`** — `"{myArray}"` prints `System.Collections.Generic.List\`1[...]`, bools print `True`, Symbols lose `#`. One-line fix: `val.ToString()` instead of `val.Data?.ToString()`. |
| 2.8 | low | `Runtime/ExecutionContext.cs:670` | **`PushFrame` increments `_callDepth` before throwing on overflow** — each depth-overflow permanently lowers the effective max depth for a long-lived REPL session (watch mode unaffected — fresh engine per reload). Validate before incrementing. |
| 2.9 | low | `TypeSystem/FunctionSignature.cs:133-141` | **User-proc varargs never type-checked** — `Matches` only validates when the vararg slot is an `ArrayType`, but user procs register the base element type, so `proc sum(Int...: nums)` accepts any trailing types → internal `InvalidCastException` downstream instead of a no-matching-overload diagnostic. |

---

## 3. Confirmed bugs — audio engine / DSP

| # | Sev | Where | Finding |
|---|-----|-------|---------|
| 3.1 | high | `StandardLibrary/Audio/DSP/Compressor.cs:43` + `SidechainCompressor.cs:48` | **Gain envelope initialized to −96 dB** — output starts at ~1.6e-5 amplitude and release-curves to unity over ~450 ms; the first half-second of every `compress`/`sidechain` call is effectively muted. Fix: initialize to 0 dB. |
| 3.2 | high | `StandardLibrary/Audio/DSP/PhaseVocoder.cs:272` | **No COLA normalization in overlap-add.** sqrt-Hann analysis × sqrt-Hann synthesis with no window-energy division → gain ≈ 2/factor at defaults: **+6.0 dB at factor→1** (verifier-measured), 0 dB only at factor 2 (why tests missed it), +12 dB at 0.5, heavy AM ripple past factor 2. Propagates into `pitchShift` `#vocoder`/`#auto`. Fix: accumulate and divide by window-energy array. |
| 3.3 | high | `Audio/CoreAudioBackend.cs:201-206` | **macOS `play` never drains.** `AudioQueueStop(immediate: false)` returns immediately (the "blocks until dry" comment is wrong) → play() returns ~280 ms early; script exit then issues immediate stop (`:275`, `:418`) and truncates the tail. Violates `play`'s documented blocking contract; PulseAudio sibling genuinely drains. Fix: wait for all buffers to return to the free list (or `kAudioQueueProperty_IsRunning`). |
| 3.4 | med | `StandardLibrary/Audio/DSP/Psola.cs:199` | **PSOLA maps each input epoch to exactly one output epoch** (no grain duplication/decimation) → Hann grains abut at factor 2 with near-zero joins: amplitude nulls at the pitch rate (buzzy tremolo) on stretched percussive material (`#auto` routes transients here; also the drum pitch-shift path). Fix: uniform output-epoch grid, nearest input epoch per grain. |
| 3.5 | low | `StandardLibrary/Audio/FileIO.cs:49,109,400-405` | **[still open]** WAV writer `int` size overflow for long renders (negative RIFF fields, corrupt file, no error); loader's `data`-chunk branch doesn't consume odd-byte pad / 24-bit remainder → chunks after `data` parse misaligned; `bitsPerSample=0` → `DivideByZeroException` before the friendly error. |
| 3.6 | low | `StandardLibrary/Audio/DSP/Reverb.cs:162-167` | **[still open]** Comb-filter feedback still has no denormal flush (Delay and Filter both gained one since April) — reverb tails burn 10-100× CPU in subnormal range. Allpass (`:181-202`) shares the gap. |
| 3.7 | low | `StandardLibrary/Audio/DSP/Filter.cs:67` | **[still open]** Bandpass `Q = center/bandwidth` unbounded — ulp-narrow bands round `a2` to 1.0f (pole on the unit circle, rings ~indefinitely; ~8.8-min decay half-life even at near-misses). No clamp or advisory, contra the charitable-clamp convention. Composer-reachable via the 3-arg bandpass only (lowpass/highpass q is internal-only — verifier correction). |
| 3.8 | low | `StandardLibrary/Audio/DSP/Reverb.cs:33,96` + `SongRenderer.cs:350` | **Reverb tail hard-cut at input length** (Delay extends; Reverb doesn't, even in the RT60 overload). Per-voice `reverbTime` path compounds it: each note's "room" dies when its voice buffer ends — per-note reverb stubs on staccato material. The repo's own Phase 15 notes + a test comment acknowledge the crop. |
| 3.9 | low | `Audio/WebAudioBackend.cs:177` | **Debug stderr spew on every browser `(play ...)`** — `[flow-audio-cs] samples=...` leaks into the frozen D-48-15 `RunResult.stderr` advisory contract (rendered in the playground diagnostics pane). Verifier correction: the second half of the original claim (unity-gain PromoteToStereo) is **intentional** — D-48-07's test-pinned contract is identical-samples dual-mono; fix is a one-word CLAUDE.md edit, not code. |

---

## 4. Confirmed bugs — standard library / music theory

| # | Sev | Where | Finding |
|---|-----|-------|---------|
| 4.1 | high | `StandardLibrary/Transforms/TransformFunctions.cs:595` (+516, 907, 940, 965-971, 1192, 1236) | **All bar-rebuilding transforms drop `BarData.ParallelVoices`** → `(transpose voicedSeq +2st)`, `(concat a b)`, retrograde, repeat, swell, crescendo... on `| {voice ...} {voice ...} |` material render **silence**. The repo fixed this exact failure for `humanizeGaussian` only (`:1298-1322` comment: "dropped ParallelVoices entirely → silent 44-byte WAVs") and Phase 36's `CloneBar` copies it for all 13 @patterns combinators — the core transform family was never retrofitted. Fix: lift the HumanizeBar recursion (or reuse `PatternFunctions.CloneBar`) into `TransformNotes` + the hand-rolled loops. |
| 4.2 | high | `TransformFunctions.cs:653,718,938,1024,1068,1124,1162` | **Transforms rebuild notes with 12 of 17 ctor args** — `IsChordTone` reset to false (chord brackets `[C4 E4 G4]q` re-arpeggiate into sequential notes and overflow the bar after `transpose`), tuplet `DurationFraction` nulled, quantize `OnsetOffset` / legato `DurationOverlap` / `PortamentoMs` stripped (`seq -> quantize -> transpose` undoes the quantize). Fix: extend `MusicalNoteData.With(...)` with pitch slots and route all transforms through it. |
| 4.3 | med | `StandardLibrary/Notation/MusicXmlExport.cs:382` | **MusicXML `<duration>` emitted in timesig-denominator beats, not divisions-per-quarter** — wrong by denominator/4 in any non-/4 meter (6/8 eighths get 2× duration; 2/2 halves get ½×); `<backup>` math inherits it. Only /4 meters round-trip, which is why the charitable-skip mscore gate never caught it. Fix: `beats * (4.0/denom) * divisions`; add a 6/8 fixture. |
| 4.4 | med | `StandardLibrary/Harmony/ScaleDatabase.cs:132-140` | **[still open]** Roman-numeral extension *replaces* diatonic quality — `ii7` in C major → **D7** (dominant) instead of Dm7; same for iii7/vi7/vii7 and minor-key i7/iv7/v7. The project's own design doc specifies the correct behavior. (`iim7` happens to work — that's the workaround.) |
| 4.5 | low | `TransformFunctions.cs:1399,1405-1406,1437` | **[still open]** `trill` drops the dotted flag (dotted notes lose ⅓ duration) and drops CentOffset/Articulation on the upper neighbor; `tremolo` subdivides at a fixed ¼ regardless of `reps` — only `tremolo 4` preserves length (`tremolo 8` doubles it, `tremolo 2` halves it). |
| 4.6 | low | `StandardLibrary/BuiltInFunctions.cs:1638` | **3-arg `euclidean` lacks the 1024-step DoS cap** its own 4-/6-arg overloads enforce — `(euclidean 1e8 1e8 C4)` OOMs the REPL and hangs a browser tab (WASM cap is non-preemptive). |

---

## 5. Confirmed bugs — tooling, live coding, real-time, WASM

| # | Sev | Where | Finding |
|---|-----|-------|---------|
| 5.1 | high | `flow-interpreter/Repl.cs:23` | **Phase 38 REPL modernization is dead code.** `ReplLineEditor` (PrettyPrompt + LSP tab-completion + Ctrl+R + persistent history) is declared but never instantiated; both REPL entry points use `Console.ReadLine()`. Only unit tests construct the class. PrettyPrompt PackageReference + flow-lsp ProjectReference are carried solely for it. (`:help <name>` and multi-line continuation DID ship — verifier correction.) Also fix `ReplLineEditor.cs:228` line-0 Position bug when wiring. |
| 5.2 | high | `flow-interpreter/LiveReloadManager.cs:520-530, 595, 676, 724, 844` | **Phase 38 LIVE-03 pipeline unreachable in production `flow watch`.** `RenderScript` hard-codes `perBlockBuffers = null` ("Plan 38-02 will fill" — never done); `StartRenderTask` bypasses `StagePendingBuffers` (the only site of the stale-closure gate AND the per-swap `PrngRegistry.ResetAtRenderBoundary`); `PreserveVoiceState`/`DetectFileScopeEdit` have zero production callers; `_lastVoices` is never assigned; every save is a whole-script 1-bar swap regardless of `live <quantize>`; the status panel always receives an empty blocks list. Either wire it or amend CLAUDE.md/Phase 38 closure docs. |
| 5.3 | high | `StandardLibrary/Network/OscFunctions.cs:624` | **OSC handlers invoke Flow lambdas on a thread-pool thread against shared interpreter state** — `ExecuteUserFunctionWithCaptures` mutates `_recursionDepth`, `StrictMode`, the plain `Stack<StackFrame>`, `_returnValue` with zero synchronization while the foreground evaluator may be running. Fix: marshal to the interpreter thread, or a cloned context per listener. |
| 5.4 | high | `Runtime/WasmEntry.cs:308-316` | **`RunResult.wav`/`midi` hardcoded null** — `writeMidi` on Web writes to the unreachable Emscripten VFS; the playground's MIDI-download feature (D-48-17/18) is dead end-to-end. The XML doc describes an "in-memory hook" that was never built. |
| 5.5 | med | `flow-lang/Audio/MidiClock.cs:566-583` | **Clock-slave poll loop mishandles `rtmidi_in_get_message` returns** — the reachable defect (verifier-corrected): a sysex > 512 bytes on the clock-in port (sysex explicitly un-ignored at `:548`) throws `ArgumentException` outside the inner try → poll thread silently dies, slave stops following the master. The −1/stale-512-bytes busy-loop is latent/defensive. Fix: honor `delta < 0`, clamp copy to `Math.Min(n, buf.Length)`. |
| 5.6 | med | `StandardLibrary/Midi/MidiFunctions.cs:401, 241` | **`midiOut` leaks a native librtmidi device + open ALSA port on every call** — handle never closed, no finalizer; repeated live-jam calls accumulate kernel sequencer ports. Fix: try/finally Close (+ CC123 all-notes-off), or cache one handle per port. |
| 5.7 | med | `flow-interpreter/LiveReloadManager.cs:533-534, 358, 388-390` | **Watch mode applies new sample-rate/channels immediately while the old buffer streams until the bar swap** — wrong pitch/speed or broken interleave for up to a bar; bar-boundary math computed with new values against the old buffer. Tempo/timesig already defer correctly; rate/channels should ride the same pending-context path. Fields also non-volatile cross-thread. |
| 5.8 | med | `flow-interpreter/LiveReloadManager.cs:860-882` | **Watch mode swallows parse/eval diagnostics** — `RenderScript` populates `errors` only for IOException; a typo'd save shows "[live] no audio output detected — keeping previous version" with no line number, in the feature whose purpose is save-listen iteration. Every other front-end formats `ErrorReporter`. |
| 5.9 | med | `scripts/test_two_run_determinism.sh:106-113` | **Determinism harness resolves writeWav output against the script dir based on a false premise** (`flow render` never chdirs — verified: no `SetCurrentDirectory` anywhere) → exit-2 setup error when invoked from repo root. Secondary: unquoted `eval` on interpolated command strings (`:127,134,141`). |
| 5.10 | low | `StandardLibrary/Network/OscFunctions.cs:537-543` | **Future-timetag OSC bundles fire handlers after `oscStop`** — `Task.Delay(...).ContinueWith(...)` carries no CancellationToken; a stopped handle still invokes composer lambdas minutes later (compounding 5.3). |
| 5.11 | low | `Audio/WebAudioBackend.cs` + `WasmEntry.cs:382-412` + playground | **Playground Stop button is a complete no-op for script audio** (verifier-corrected, broader than claimed): `runtime.stop()` stops `_sharedBackend`, which is only created by `runtime.play()` — but `RunResult.wav` is always null so the playground never calls `play()`; script `(play ...)` audio routes through the engine's own separate `WebAudioBackend` instance. Also: `Stop` only revokes the most recent source; `_activeSource` written outside the lock; replaced JSObject handles leak. |
| 5.12 | low | `Audio/WebAudioBackend.cs:177` + `wasm/flow-runtime.js:106,141-146,168,319-321` | **Leftover debug logging in the frozen WASM runtime** (both the source copy and the committed `flow-site/static/wasm/` copy) — console spew on every context creation/play/resume + a per-sample maxAbs scan. Strip, re-publish, re-run `sync-runtime.sh`. |
| 5.13 | low | `StandardLibrary/Network/OscFunctions.cs:657` | **OSC Buffer blob round-trip loses channels + sample rate** — receive side reconstructs mono@44100 from interleaved stereo floats (renderSong output is always stereo → easily hit). Add a tiny header or a WarnOnce advisory. |
| 5.14 | low | `flow-interpreter/LiveStatusPanel.cs:271-284, 347-373` | **Status-panel heartbeat never repaints** — the 8s advisory auto-clear and "Xs ago" refresh are state-only (cleared text persists on screen); advisory row hardcodes `\x1b[4;1H` (one row too low in the common case — which is *every* case since blocks list is always empty, per 5.2); advisories duplicated to stdout + stderr. |
| 5.15 | low | `flow-cli/Doc/DocExampleRunner.cs:63-69` + emitters | **`flow doc` misattributes `[example failed]`** — failures collected densely, rendered by example index: when a strict subset fails, a *passing* example gets publicly flagged and the failing one shows clean. |
| 5.16 | low | `flow-lsp/Symbols/StdlibSymbolIndex.cs:26` | **LSP stdlib index frozen at the Phase 17 six modules** — @patterns/@generative/@improv/@sfz/@osc/@notation-io/@midi/@jack invisible to completion/hover/`use "` suggestions; the VSIX workflow copies only the same six .flow files; verifier adds: Phase 37 DSP builtins (granular/stretch/pitchShift) are also missing from `RegisterSignaturesOnly`. |

**Disputed (design-locked vs. defect):** the `flow watch` 200 ms **leading-edge** change gate (`LiveReloadManager.cs:434-445`) drops trailing events in a save burst, so an editor's final write (atomic temp+rename, format-on-save) can be silently ignored until the next save. One verifier confirmed it as a bug; another found D-38-05 LOCKs leading-edge-with-settle as the pinned design. Both agree on the mechanics (and that `_lastChangeTime` is unsynchronized). Recommendation: revisit D-38-05 — a trailing-edge debounce is strictly safer for the stated "save-listen" purpose; at minimum add the missing synchronization.

---

## 6. flow-site (playground / website) — pre-HUMAN-UAT blockers first

| # | Sev | Where | Finding |
|---|-----|-------|---------|
| 6.1 | high | `src/lib/playground/share-controls.svelte.ts:74` | **OAuth gist save destroys the composer's code.** `beginGistAuth()` navigates away without stashing editor contents; on `#token=` return the playground mounts with the default snippet and never auto-resumes the save (the comment promises resume; `+page.svelte` onMount only captures the token). First-time save: code gone, second save saves the wrong snippet. Directly fails HUMAN-UAT gate 2. |
| 6.2 | high | `src/lib/showcase/pieces.ts:246` + `sources.ts` | **All five `runnableOnWeb` showcase deep-links produce zero audio** — every embedded source ends in `(writeWav ...)`, zero `(play ...)` calls; browser audio happens only via `play`. The detail page says "press Run to hear it"; auto-run spins seconds of synchronous WASM with no sound and the LED flips to "playing". Directly fails HUMAN-UAT gate 3. |
| 6.3 | med | `static/wasm/` (last touched 479ba12) | **Committed AppBundle is stale** — predates 5953cc4 (PolyBLEP oscillator fix, which edited `NoteSynthesizer.cs`, part of the Web build — and even touched `flow-site/src/lib/runtime.ts` without re-syncing). Playground plays the pre-fix aliasing saw/square. Also: 5.7 MB of wasm binaries with no `.gitattributes` binary/-diff marking or LFS. Re-run `sync-runtime.sh`; add a bundle-staleness CI check. |
| 6.4 | med | `src/routes/+page.svelte:272` | **VU meter `{#each vu as h (h)}` keyed by height value** — init array is 14 identical values → guaranteed duplicate keys on first Play click (`each_key_duplicate` throw in dev; undefined keyed reconciliation in prod). Key by index. (New in the iOS-6 redesign, 94df2ed.) |
| 6.5 | med | `src/routes/+page.svelte:376` | **Home `:root` block leaks `--font-mono` globally** — collides with `tokens.css:71`; after visiting `/`, every route's code/console/error typography silently switches from JetBrains Mono to Menlo (navigation-order-dependent styling; undermines visual baselines). Scope tokens to the page wrapper. |
| 6.6 | med | `src/routes/+page.svelte:173` | **iOS-6 redesign dropped the hero snippets' `#code=` playground deep-links** (D-49-08 contract — docs + showcase still implement it); the three "Open in playground →" anchors are bare `/playground`. Also orphaned `CodeCard.svelte` + `examples.ts` as dead code. |
| 6.7 | low | `src/routes/playground/+page.svelte:113` | **`&run=1` honored on cold loads with no user activation** — suspended AudioContext: silent run now, buffered audio blasts out on a later gesture, overlapping the new run. Firefox/Safari strictest. Gate on `navigator.userActivation?.hasBeenActive`. |
| 6.8 | low | `_headers:29` | **COOP/COEP rule `/playground/*` never matches the bare `/playground` document** (trailingSlash 'never') — the isolation headers apply to nothing that matters; will bite the v1.6 AudioWorklet/SAB work. Add a sibling `/playground` rule. |
| 6.9 | low | `src/routes/+page.svelte:124` | **iOS-6 home regressed a11y landmarks** — no `<main>`, two unlabeled `<nav>`s, decorative VU/LED markup exposed to AT, dark-theme users get a hard-locked light page. axe's landmark rules are "moderate" so the 0-critical gate stays green while the HUMAN-UAT screen-reader smoke gets worse. |
| 6.10 | low | `src/routes/playground/+page.svelte:55` | **Prod bundle monkey-patches `window.AudioContext` with a test-observation Proxy** (never restored; wraps the home page's audio too) + `__flowEditorValue`/`__flowRuntimeReady` globals + a voided `hasGistToken` lint-silencer. Gate behind a test-mode flag. |

---

## 7. Documentation accuracy

| # | Sev | Finding |
|---|-----|---------|
| 7.1 | high | **Install path cannot deliver v1.5** — `scripts/install.sh:21` defaults `FLOW_VERSION=0.1.0` and builds `flow-v${VERSION}-linux-x64.tar.gz` (version-first), while `publish.sh` (D-16) names `flow-<rid>-v1.5.0.tar.gz` (rid-first) → guaranteed 404 even with the right version. No OS/arch detection despite 5 RIDs. And `flow-cli.csproj:10-11` still stamps `0.1.0-phase30`, so a "v1.5.0" binary reports 0.1.0 from `flow version`. |
| 7.2 | high | **CLAUDE.md describes a dependency that no longer exists** — four passages (lines 66, 343, 347, 348) document RtMidi.Core 1.0.53 + the reflection bridge; it was removed 2026-06-07 for direct librtmidi P/Invoke (`Audio/LibRtMidi.cs`). Also: "Two projects" (`:177`) vs. 9 in the .sln — `flow-midi`/`flow-midi.Tests` (the midi2flow converter, ~995-line Quantizer) appear **nowhere**; "External deps: only Pidgin + DryWetMidi" (`:412`, `:431`) vs. actual Rug.Osc + NAudio.Wasapi (+ PrettyPrompt, OmniSharp.LSP in sibling projects), with NAudio listed under "NOT recommended" while shipped. |
| 7.3 | high | **Phase 38/40 surfaces have zero composer-facing docs** — `live <quantize> { }`, `micBuffer`, all of `@osc`, `@midi` (midiPorts/midiOut/clock*), `@jack` are absent from FEATURES.md and all 26 wiki pages (which feed flowlang.dev). Grep matches nothing but "oscillator". |
| 7.4 | high | **FEATURES.md + wiki actively deny shipped capabilities** — "macOS / Windows playback: Not yet" (`:253`), "only PulseAudio implemented" (`:250`), "Cross-platform official builds: Not yet" (`:428`), `wiki/Playback-and-Export.md:3,277` — while CoreAudioBackend + WasapiBackend ship and are probe-wired, and publish.sh builds 5 RIDs. Also stale: "REPL tab completion: Not yet" (`:331` — partially true given §5.1!), "oscillators aliased by design" (`:169` vs. PolyBLEP commit 5953cc4), "BuiltInDocs not yet exposed" (`:396` vs. `:help` + `flow doc`). This costs adoption at first contact. |
| 7.5 | med | **README CLI table lists 11 of 14 verbs** — `flow lsp`, `flow test`, `flow doc` missing (`README.md:93-107` vs. `CommandRegistry.BuildAllCommands()`). |
| 7.6 | low | **wiki/Home.md banner stuck at v1.4** ("two showcase pieces"); tooling bullet lists 7 of 14 verbs. May be deliberate until the release gate — but it contradicts README in the same repo and ships to the website. |
| 7.7 | low | **writeMidi microtone advisory text still says "deferred to v1.4"** (`MidiExport.cs:256`) — two versions stale, and the advisory only fires for tuning blocks, not note-stream cent offsets (see gap 8.4). |

---

## 8. Code quality (maintenance risk)

| # | Sev | Finding |
|---|-----|---------|
| 8.1 | high | **Prerequisite-gated tests silently PASS instead of SKIP** — ~15+ `return; // charitable skip` sites (RealMidiLoopback, ClockMaster, VirtualMidi, Phase44 StrictFlowScriptSuite, Phase45 CrossFileSmoke, Phase42 ClampGrep ×6): green CI with zero assertions on any box without the Linux MIDI stack. The repo already owns the right mechanism (`FlowTargetFactAttribute` sets `Skip`; xunit.v3 supports `Assert.Skip()`). Regressions in gated areas are invisible until the one provisioned bench box runs. |
| 8.2 | high | **ExecutionContext is an accreting god-object** (1340 lines, 72 members) — five copy-pasted module-enable booleans + SectionRegistry/TestRegistry/StyleRegistry/Sfz* registries/strict flags/overload cache. Every module phase edits the core runtime scoping class. Introduce a generic module-state side-table. |
| 8.3 | med | **Eight public mutable static test seams on the shipped API** (MidiFunctions.BackendOverride, OscFunctions.HandlerInvokeOverride, InputFunctions.CaptureOverride, MidiClock.SlaveSourceOverride, JackFunctions.TransportQueryOverride, etc.) — accidental public API + process-global state; flow-midi already models the right pattern (`internal` + `InternalsVisibleTo`). |
| 8.4 | med | **[still open] Pidgin 3.5.1 dead dependency** — zero `using Pidgin` anywhere; referenced unconditionally (unlike the Web-gated packages), sitting in the budgeted WASM closure. One-line removal; optionally add to `ForbiddenTypeRefPrefixes`. |
| 8.5 | med | **31 wall-clock `Thread.Sleep` timing assertions** in Phase 38/40 suites (e.g. ClockMaster sleeps 1.5 s + 2 s then asserts ~41.67 ms pulse deltas) — flake generators under CI load; ~10+ s pure sleep per run. Thread an injectable tick source through MidiClock (the seam pattern already exists). |
| 8.6 | med | **Per-chunk `new float[4096]` in the live-playback streaming loop** (`CoreAudioBackend.cs:232`, `PulseAudioSimpleBackend.cs:180`) — ~350 KB/s Gen0 garbage on the audio-feed thread for the duration of a live set; the one hot path where the project's own "no GC pressure" constraint applies. Hoist a scratch buffer. |
| 8.7 | low | **Duplicated libpulse P/Invoke surface** between playback + capture backends (struct + 3 externs, self-annotated as a mirror) — the April `PtrToStringAnsi`-on-UTF-8 issue now has two call-site families. Extract `internal static class LibPulse`. |
| 8.8 | low | **9 files still block-scoped namespaces** (TimeSignatureType, SequenceType, NoteValueType, BarRenderer, MusicalConversions, SequenceRenderer, PitchConversion, NoteSynthesizer, ClassicalComposition) vs. the documented convention; add the .editorconfig rule. |
| 8.9 | low | **40-line self-contradicting deliberation comment** in the reverb registration (`EffectsFunctions.cs:46`) — replace with the 3-line conclusion + decision pointer. |

---

## 9. Refuted claims (for the record)

Four bug claims were killed by the adversarial pass — kept here so they don't get re-reported:

1. **break/continue escaping proc bodies** — the parser already rejects `break` outside a loop (`Parser.cs:285-298`); the only residual nit is that `_inLoop` isn't cleared across nested proc-body parses (a proc declared lexically inside a loop can parse a `break` that dynamically breaks the calling loop — hardening optional).
2. **`play(Sequence)` mono-only mix** — the code is as described, but unreachable: the sole caller hard-codes the mono SineSynthesizer and Pan is always 0 on that path; there is no stereo content to lose.
3. **Unseeded `vary`/`humanize` violating the PRNG contract** — real wall-clock `new Random()` sites, but explicitly out of D-v1.5-06's scope per PrngRegistry's own doc ("FROM PHASE 36 ONWARD"), the Phase 36 plan ("DOES NOT MIGRATE"), and `wiki/Generative.md:86` ("frozen by design").
4. **Watch debounce drops trailing saves** — refuted as D-38-05-locked design by one verifier, confirmed by another (see "Disputed" note in §5). Listed both places honestly.

---

## 10. Feature gaps (composer's-chair; verified in code)

1. **renderSong is mono-timbral** (`SongRenderer.cs:213-216`) — one instrument string for the whole Song while writeMidi routes per-track GM programs. The flagship EDM showcase renders kick, clap, bass, pad AND lead through `"saw"` and fakes sidechain with whole-mix compress because no per-sequence stems exist. The single biggest demo→production gap. (See idea I1/I7.)
2. **midi2flow discards velocity, drums, and mid-song tempo/key changes** (`flow-midi/Conversion/FlowGenerator.cs:288-313`; velocity is carried through the whole quantizer pipeline then never read; every track hardcoded to piano) — while writeMidi exports velocity, so Flow→Flow round-trips flatten performances.
3. **Cent-precision transpose doesn't exist** — `(transpose seq +50c)` rounds to 0 semitones (banker's rounding!) and no-ops with a warning, while per-note `C4+50c` is core syntax, the synth path honors arbitrary cents, and CLAUDE.md + FEATURES.md both advertise it. Only the wiki admits the truth.
4. **writeMidi flattens note-stream microtones silently** — `CentOffset` never consulted, no pitch-bend emission, and the advisory only fires for tuning blocks.
5. **No effect automation surface** — every DSP param is frozen per call; a filter sweep (the most common EDM gesture) is impossible; `tempoRamp` proves the start→end pattern exists. Start with two-value overloads on the filter family. (Idea I2.)
6. **Named args can't skip ladder knobs** — touching `transientThreshold` on `stretch` forces restating mode/frameSize/hopSize/overlap (the v1.6 "OverloadResolver relaxation" backlog item; disproportionately hurts the Phase 37 surface).
7. **jux ≡ superimpose** — the Tidal-defining L/R split is still mono, but per-voice Pan shipped in Phase 37, so the fix is now wiring Pan=∓1 onto the two voice branches.
8. **LSP/editor experience lags the language by 7 modules** (§5.16) — the newest, most-marketed features are the ones VSCode doesn't know about.

---

## 11. Feature opportunities for v1.6+ (from the ideation agent; deduped against ROADMAP/backlog)

**Headline tier:**
- **I1. Per-part instrument routing in renderSong** (`instruments=` Dict keyed by sequence name, mirroring writeMidi's GM routing) — closes gap 10.1. *(medium)*
- **I2. Automation lanes + beat-synced LFO rack** — `(automate buf lowpass #cutoff (ramp 200Hz 8kHz 8b))`, wobble/pump/sweeps, fully deterministic; builds on RBJ biquads + EnvelopeProcessor + TempoRampRenderer precedent. *(large)*
- **I3. Embeddable playground widget** — `/embed#code=` route + iframe snippet; the share codec, frozen runtime API, and lazy-load already exist. Turns every blog post into a runnable Flow demo. *(medium)*
- **I4. Lyric-singing wedge** — syllable-annotated note streams (`| C4:shoo D4:bee |`) driving the existing formant engine; CLAUDE.md names vocaloid as a goal with nothing scheduled. *(large)*

**Solid tier:** I5 MIDI input (`midiListen`/`midiRecord` → Sequence; input path exists for clock slave) · I6 buffer chop + resequence (breakbeat slicing; FFT/HPS/transient machinery already built) · I7 stems export (`writeStems` — voices already tagged `{sequenceName}:{ordinal}`) · I8 in-language `(loadMidi)` → Song (flow-midi's parser exists; DryWetMidi survives Web → drag-drop .mid in the playground) · I9 groove templates (extract/apply timing+velocity feel as composer-editable packs, like improv styles) · I10 `flow pkg` git-based package manager (resolves through existing `stdlib_search_path`; no registry needed) · I11 FM synthesis family (the biggest palette hole; zero sample cost) · I12 @improv progression generator + reharmonizer · I13 `flow fmt` AST formatter.

**Quick win:** I14 `flow help <fn>` CLI subcommand (BuiltInDocs + the REPL's renderer; an afternoon).

---

## 12. Suggested sequencing

1. **Before the v1.5 release gate / HUMAN-UAT:** §6.1–6.3 (playground OAuth/audio/bundle), 5.4, 5.11, 5.12 (WASM stop/MIDI/debug spew), 7.1 (install.sh — users literally cannot install v1.5 as documented), 3.3 (macOS tail truncation — it's one of the UAT rows).
2. **Correctness sprint (composer-facing):** 4.1 + 4.2 (transform data loss — arguably the worst pure-language bugs), 3.1 (compressor), 3.2 + 3.4 (stretch/pitchShift levels), 4.4 (ii7), 2.1 (string retyping), 5.8 (watch diagnostics).
3. **Truth-in-docs pass:** 7.2–7.6, plus a "Phase 38 reconciliation" decision: wire §5.1/5.2 or re-document them honestly.
4. **Hygiene:** 8.1 (test skips — cheap and high-value), 8.4 (Pidgin), 8.6 (audio-thread allocs), then the rest of §8.
