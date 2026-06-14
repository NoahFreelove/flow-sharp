# Flow Codebase Audit — 2026-06-14

**Date:** 2026-06-14
**Method:** Comprehensive parallel-reviewer sweep across the whole repo (lang core, type system, audio DSP, synthesis/render, transforms, generative, live/REPL/OSC, MIDI/clock/JACK, CLI/install, WASM/Web, docs) → adversarial verifiers re-ran every claim against current source with live repros → dogfood pass authoring & running 8 example pieces. Only adversarially-CONFIRMED findings are recorded here.
**Counts:** 88 confirmed bugs (19 high · 45 medium · 24 low). 87 fixable here; 1 hardware-gated (owner UAT). Plus ~50 ergonomics findings and 8 dogfood pieces (all keepable).

Machine-readable bug array: `.planning/sweep-2026-06-14-findings.json` (consumed by the fix wave).

---

## 1. Top-priority synthesis (read this if nothing else)

1. **`pitchShift` inverts its stretch direction** — every upward shift (`+Nst`/`+Nc`, all modes, plus drum pitch-shift) renders near-silent DC at the wrong pitch (octave-up `+1200c` comes out ~42 Hz). `PitchShiftEngine.cs:59-60`, one-line fix (`factor: ratio` not `1/ratio`). HIGH.
2. **Non-4/4 time signatures play at the wrong wall-clock speed** — 6/8 is 2× too slow, 2/2 is 2× too fast, in both WAV and MIDI, because `NoteType.GetBeats` returns denominator-unit beats while every time/tick conversion assumes quarter-note beats. Breaks `examples/beat/*`. `NoteType.cs:392-394`. HIGH.
3. **`(if …)` evaluates BOTH branches** when branches are ordinary expressions — side-effects fire on the untaken arm and `if` cannot guard against errors/empty-list/div-by-zero. The lazy overload is never selected because the parser doesn't auto-wrap branches. Pervasive (each/map callbacks). `BuiltInFunctions.cs:424-427` + `ExpressionEvaluator.cs:306-313`. HIGH.
4. **JI/Pythagorean tuning is wrong for any non-C key** — the tonic itself renders at the wrong frequency (G in JI Gmajor sounds a fifth high) because a C-relative ratio is multiplied by the tonic's 12-TET anchor without normalizing. `PitchConversion.cs:98-112`. HIGH.
5. **`swing N { }` context block is a complete no-op** — a documented headline feature; `MusicalContext.Swing` is stored but read by no render path, so output is byte-identical with/without it. Real swing only comes from the `quantize` transform. HIGH-impact (filed medium).
6. **WASM playground breaks on the second Run and corrupts MIDI downloads** — the shared `FlowEngine` is reused so any top-level declaration throws "already declared" on re-run; and `RunResult.midi` crosses to JS as a base64 string (not `Uint8Array`), so the Blob download is a corrupt `.mid`. Both HIGH; both fixable in `WasmEntry.cs` / the SvelteKit wrapper.
7. **Silent music loss across several render/convert paths**: `midiOut` drops ALL voice-block notes; SFZ velocity-crossfade produces a silent hole; SFZ round-robin only loads one alternate sample (odd triggers silent); bundled sampled instruments render pure silence with no advisory when WAVs are missing (the documented Web "fall back to synthesis" is actually "fall back to silence"); `midi2flow` discards all velocity and drops whole melodic tracks on a single channel-9 hit. Mostly HIGH.
8. **Pattern matching silently routes to the wrong arm for two literal kinds** — a Note literal pattern (`match note | C4 => …`) can never match a Note, and a Symbol literal pattern (`| #kick => …`) always falls through to wildcard. Both produce wrong control flow with no error. `PatternMatcher.cs` / `Parser.cs`.
9. **Locale bug corrupts music literals 10×** — Cent/Decibel/Millisecond/Second parse with locale-dependent `double.TryParse`, so in comma-decimal locales `2.5s`→25s, `-12.5dB`→-125dB, silently. Desktop (the default target) does not pin InvariantCulture. `ExpressionEvaluator.cs` + `SimpleLexer.cs`. HIGH.
10. **`humanize` (uniform) breaks two-run determinism** — a static unseeded `Random` is never reset at the render boundary (the PRNG CI gate doesn't scan `Transforms/`), so the same source `writeWav`s to different bytes. HIGH. Also: chunk's static rotation counter has the same per-render leak (medium).

Recurring themes: (a) **charity hiding real defects** — silent rests/silence/wrong-arm selection where an advisory or correct behavior was intended; (b) **documented-but-unreachable surface** — `inspect`, `length`, `zip`, `oscBundle`-of-messages, `exportWav`, named args on user procs, `xs@-1`, `_q` rests, chord-bracket articulation; (c) **doc drift** — stale counts, removed builtins, wrong GM routing, contradicting saw/square anti-aliasing claims.

---

## 2. Confirmed bugs

### 2.1 Language core (lexer / parser / interpreter)

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 1 | high | PatternMatcher.cs:92-93 (root Parser.cs:2033) | Note-literal patterns in `match` never match a Note scrutinee — `Value.From` makes a String, LooseEquals(Note,String) is always false → wrong arm / silent Void. | Build the comparison as `Value.Note((string)lit.Value)` when scrutinee is Note-typed (or emit a Note-discriminated pattern routed through a MatchNote helper). | no |
| 2 | medium | SimpleLexer.cs:506-532 | Negative literal after a value-end token fails to lex (`(add 5 -3)` errors; `(mul n -1)` works) — value-end tokens missing from the signed-number expr-start set. | Add IntLiteral/FloatLiteral/music-literal-end/RParen/At to the TryLexSignedNumber expr-start set. | no |
| 3 | medium | Parser.cs:304 | A tuple literal can't start a statement — `<<a,b>> ~> f` and bare `<<1,2>>` parse-error (always treated as destructure target). | Lookahead from `<<` to matching `>>`; treat as destructure only when the next token is `=`, else fall through to expression-statement. | no |
| 4 | medium | Parser.cs:1739 | Multi-statement lambda body only parses when the first statement is a type decl; `fn x => ((print "side") x)` errors. | Decide body shape structurally (top-level `;` or >1 expression → multi-statement) instead of the type-keyword-only lookahead. | no |
| 5 | medium | Interpreter.cs:137-140, 681, 714 | Top-level `return` (incl. inside top-level for/while) silently truncates the rest of the program; asymmetric with the fenced-block ClearLeakedReturn discipline. | Call ClearLeakedReturn (gated `!InsideProcCall`) on the for/while paths and after the top-level statement loop. | no |
| 6 | medium | SimpleLexer.cs:506-532 | Documented negative array index `xs@-1` is unreachable (At not in signed-number expr-start set); only `xs@(neg 1)` works. | Same one-line lexer fix as #2 (add `TokenType.At`). | no |

### 2.2 Type system / overloads

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 7 | medium | OverloadResolver.cs:296-321 | Named-arg resolution picks the FIRST name-compatible candidate before type-matching, then type-checks only that survivor — `(transpose s amount=+50c)` and `(db x=12.0)` fail though positional works. | Accumulate every name/arity-valid candidate with its own reordered type vector; filter by Matches and rank by specificity per-candidate. | no |
| 8 | low | TupleType.cs:45-68 | `TupleType.AnyArity` violates Equals/GetHashCode contract (Equals true vs differing hash). Latent — no FlowType-keyed dict today. | Remove the AnyArity short-circuit from Equals (wildcard matching already lives in IsCompatibleWith/CanConvertTo). | no |
| 9 | low | SemitoneType.cs:22-25 | A `Long` arg fits neither Semitone nor Cent overload of transpose despite Long being in the widening chain. | Add `LongType` to SemitoneType.IsCompatibleWith. | no |

### 2.3 Audio DSP

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 10 | high | PitchShiftEngine.cs:59-60 | pitchShift inverts the stretch direction → all upward shifts produce wrong pitch + near-silent/DC output (all 3 modes, all overloads, drum path). | Change stretch `factor: 1.0/ratio` → `factor: ratio`; resample loop unchanged. | no |
| 11 | medium | FileIO.cs:116-159 | WAV writer omits the RIFF even-boundary pad byte after an odd-size 24-bit data chunk; reader assumes it exists (writer/reader asymmetry). | Pad with one 0x00 when data size is odd; bump the RIFF fileSize field by the pad, keep data chunkSize true. | no |
| 12 | low | GranularEngine.cs:82 | Granular with grain longer than the source buffer collapses to near-silence (Hann peak lands outside the buffer); no clamp/advisory. | Clamp grain to buffer length + one-shot advisory. | no |

### 2.4 Synthesis & rendering (sampled / SFZ)

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 13 | high | SfzRenderer.cs:211-212, 350-364 | SFZ velocity crossfade renders ONE region but applies the xfade gain (and a spurious 0.7071) → total silence at band edges, a gaping volume hole mid-velocity. | Render and additively sum the overlapping layers (cos²+sin²=1); delete the 0.7071 sibling factor. | no |
| 14 | high | SfzSampleCache.cs:186 | SFZ round-robin alternate sample files never eager-loaded (only grid-winner) → every non-grid trigger renders silent. | CollectRegionsFromBar must add every region matching (midi,vel), not just the grid winner. | no |
| 15 | high | SampledInstrumentRenderer.cs:161,182 | Bundled sampled instruments render pure silence with NO advisory when WAVs are missing (Web/partial install) — "fall back to synthesis" is actually "fall back to silence". | WarnOnce at both silent-return sites; add HasLoadedSamples gate; ideally route to real synthesis fallback. | no |
| 16 | medium | SampledInstrumentRenderer.cs:214-215 | SAMP-03 articulation multiplier quartiles span the full buffer incl. the 1.5s release tail → staccato "attack" bucket smears across the piano tail; disagrees with the SFZ path. | Bound the multiplier window to authoredFrames (sample against authoredFrames). | no |
| 17 | low | SfzRenderer.cs:588,624 | Dead `firstIteration` variable in loop-crossfade AssembleBody (assigned, never read); comment describes unimplemented behavior. | Delete the variable; loop seam state is correctly carried by srcReadPos. | no |

### 2.5 Music transforms / harmony

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 18 | high | TransformFunctions.cs:1305,1337 | `humanize` (uniform) uses a static unseeded Random never reset at render boundary → breaks two-run cmp-clean for offline writeWav. PRNG CI gate doesn't scan Transforms/. | Route through PrngRegistry keyed by (location,"humanize"); delete the static field; extend the gate. | no |
| 19 | medium | TransformFunctions.cs:1505-1544, 1588-1629 | trill/tremolo on chord brackets collapse upper chord-tones into a single-onset cluster (IsChordTone subdivisions stack on lastLeadOnset). | Group (lead, chord-tones) and re-emit each subdivision as a complete stacked chord. | no |
| 20 | high | TransformFunctions.cs:1505-1544, 1552-1559 | trill on tuplet notes ignores DurationFraction (stale fraction preserved by With) → severe bar overflow (~6.75 beats in a 4/4 bar). | Add a rational-duration branch to TrillBar mirroring TremoloBar's BaseQuarterFraction path. | no |
| 21 | medium | ScaleDatabase.cs:132-140 | Roman-numeral chord quality ignores numeral case — `iv` renders F major not F minor (borrowed chords silently wrong). | When no extension supplied, force maj/min from char.IsUpper; keep dim where the diatonic default is dim + lowercase. | no |
| 22 | medium | ScaleDatabase.cs:118, 154-195, 251 | Roman numerals in church-mode keys (`key Ddorian`) silently resolve to all rests — callers use legacy major/minor-only TryParseKey. | Migrate ResolveRomanNumeral/GetScaleNotes to TryParseKeyWithMode + per-mode interval/quality tables; add an unresolved-numeral advisory. | no |

### 2.6 Generative & patterns

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 23 | medium | PatternFunctions.cs:408,450-453 | chunk's static rotation counter never reset at render boundary (comment falsely claims PrngRegistry resets it) → breaks two-run determinism; process-global, leaks across engines. | Move counter to per-ExecutionContext state and reset at every render boundary. | no |
| 24 | medium | LsystemFunctions.cs:188 | lsystemToSequence silently drops every note when the mapper returns a bare Note literal (its .Data is a string, not MusicalNoteData) — documented surface yields empty output. | Add a NoteType case that parses the note text into MusicalNoteData (mirror MarkovFunctions). | no |
| 25 | medium | JamFunctions.cs:96-132 | jam's documented sparse named-arg surface `(jam over=… style=… seed=42)` is rejected — six fixed-arity positional overloads, defaults only in C#. | Add per-parameter defaults to FunctionSignature + named-arg default-fill, or correct the docs to advertise only resolvable shapes. | no |
| 26 | medium | PatternFunctions.cs:456 | chunk front-loads bars (ceil-divide) leaving trailing empty chunks → 1-in-n silent dead cycle; doc example (2,1,1,1) is wrong (actual 2,2,1,0). | Even distribution (remainder to first chunks); fix the doc comment. | no |
| 27 | low | MarkovFunctions.cs:50-53, 75-78 | Markov bit-layout doc and "quarter-note units" description contradict the implementation (stores NoteValueType enum slot; layout is `(d<<12)|p`). Self-consistent, doc-only. | Correct the xmldoc + rename the param/field to durationEnumSlot. | no |

### 2.7 Live coding / REPL / OSC

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 28 | medium | OscFunctions.cs:288-319 | `oscBundle` can't wrap messages — error references a non-existent `oscSendMessage` builtin; only bundle handles carry a packet, so a bundle of NOTE messages is impossible. | Add `oscMsg(path, …args) → OscHandle`; relax oscSendBundle to accept a bare message handle; fix the error string. | no |
| 29 | medium | LiveStatusPanel.cs:276-278 | `flow watch` plain-line mode swallows distinct advisories/errors after the first (process-lifetime WarnOnce dedup with path-only/constant keys). | Content-sensitive dedup keys for per-render diagnostics, or write the advisory body directly to stderr in plain-line mode. | no |
| 30 | low | OscFunctions.cs:586-594 | Every clean `oscStop` logs a spurious "[osc] receive error … socket has been disconnected" (Rug.Osc throws generic Exception, not ObjectDisposedException). | `if (cts.IsCancellationRequested) break;` first in the generic catch (or a `when (cts.IsCancellationRequested)` filter). | no |
| 31 | medium | ReplLineEditor.cs:629-644 | Multi-line note streams can't be entered in the REPL — completeness check ignores the Pipe token, so an unterminated `\| …` is judged complete and truncated. | Count pipes; treat an odd count as incomplete. | no |
| 32 | low | LiveStatusPanel.cs:336/351/366/389,425 | ANSI status panel repaints absolute rows 1-4 with no scroll-region reservation → collides with scrolling log output (cosmetic corruption). | Reserve a DECSTBM scroll region below the panel (or anchor with cursor save/restore). | no |

### 2.8 MIDI / clock / JACK

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 33 | high | MidiFunctions.cs:516-546 (root BarType.cs:182-220) | `midiOut` silently drops ALL voice-block notes (ToTimeline ignores ParallelVoices) — breaks the documented .mid/audio parity. | In ScheduleOneSequence, walk bar.ParallelVoices like writeMidi does. | no |
| 34 | medium | MidiFunctions.cs:448 vs MidiExport.cs:369-382 | `midiOut` uses per-section tempo but `writeMidi` writes one global first-section tempo → multi-tempo songs diverge (writeMidi is the outlier vs renderer + midiOut). | Emit a SetTempoEvent at each section boundary where tempo changes; dedupe equal tempos. | no |
| 35 | medium | MidiFunctions.cs:94-106, 112-163 (RtMidiMidiBackend.cs:218-300) | `openMidiOutput` handle is never closed — native device + ALSA port leak on the low-level escape-hatch path; no CC123, stuck notes. | Finalizer/SafeHandle backstop + a `closeMidiOutput`/`midiPanic` builtin. | **YES (real MIDI port)** |
| 36 | medium | MidiClockFunctions.cs:57 (MidiClock.cs:222-232) | `clockMaster` reads a frozen MusicalContext snapshot — composer `tempo` blocks after start never change the clock rate. | Document frozen-at-start, or pass a tempo-read delegate `() => ctx.GetMusicalContext().Tempo`. | no |
| 37 | medium | MusicalContext.cs:228-231 (JackFunctions.cs:210, MidiClock.cs:380) | JACK/clock tempo validation has no upper bound — pathological transport BPM is accepted and busy-spins the master at 100% CPU. | Add a sane upper cap (e.g. ≤1000 BPM); WarnOnce on the high end; clamp PulseIntervalMs. | no |
| 38 | low | MidiFunctions.cs:379 (consumed 499-501) | `midiOut overrides=` Dict silently ignores non-Int channel values (e.g. Long) → remap silently no-ops to GM default channel. | Charitable numeric coercion (int/long/double/float → int) before clamping to 0..15. | no |

### 2.9 CLI / install / midi2flow

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 39 | high | FlowGenerator.cs:280-337 | midi2flow silently discards all MIDI note velocity (data loss) — Flow streams support dynamics but none are emitted; every note collapses to 0.63. | Bucket velocity onto the ppp..fff ladder; emit a sticky dynamic token when the bucket changes. | no |
| 40 | high | Quantizer.cs:198 | A Format-1 track with ANY channel-9 note is flagged all-drum and dropped — one stray snare loses the whole melody. | Make Format-1/2 channel-aware (SplitByChannel) like Format 0; route ch9 to a drum track, others to melodic. | no |
| 41 | medium | Midi2FlowCommand.cs:45-57 (flow-midi/Program.cs:92-105) | midi2flow reports success (exit 0) when it produced a comment-only file with all notes dropped. | Signal playable-track count to callers; warn + return non-zero exit when zero notes survived. | no |
| 42 | medium | flow-interpreter/Program.cs:9-10 | Interpreter prints a stale `v0.1` banner to STDOUT on every invocation incl. `-e`/pipe → pollutes captured output. | Delete the banner (or move to stderr in the interactive-REPL branch, read the real version). | no |
| 43 | low | scripts/uninstall.sh:10 | `FLOW_VERSION` default is stale 0.1.0 and never read (dead/misleading). | Delete the unused line (install.sh defaults 1.5.0). | no |
| 44 | low | flow-cli/Commands/VersionCommand.cs:7-8 | Doc comment claims version `0.1.0-phase30`; code/csproj are 1.5.0 (runtime correct). | Update the comment to 1.5.0. | no |

### 2.10 WASM / Web runtime

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 45 | high | WasmEntry.cs:48,337 (flow-runtime.js:247-265) | `RunResult.midi` crosses to JS as a base64 string, not a Uint8Array → `new Blob([midi])` is a corrupt `.mid` download. | Decode base64→Uint8Array in the SvelteKit runtime wrapper (frozen runtime), or expose midi via a typed-array [JSExport]. | no |
| 46 | high | WasmEntry.cs:185-193, 279/289 | RunFromJs reuses one FlowEngine across runs → second run of any script with a top-level declaration throws "already declared". | Build a fresh FlowEngine per run (dispose previous), re-bootstrapping stdlib. | no |
| 47 | medium | WasmEntry.cs:258-348 (RenderingDiagnostics.cs:21,33) | WarnOnce dedup is process-static and never reset per run → RunResult.stderr diverges on two runs of the same source (D-48-16 violation). | Call RenderingDiagnostics.ResetForTesting() at the top of RunFromJs; add an advisory-emitting determinism test. | no |
| 48 | medium | PlaybackFunctions.cs:104,116,163,211 | `loop()`/`stream()` silently produce no audio on Web (Task.Run never executes on the single WASM thread); no advisory. | On OperatingSystem.IsBrowser() route to synchronous fire-and-forget (native source.loop) + advisory; never Task.Run on Web. | no |
| 49 | low | WasmEntry.cs:227-239 | All FlowErrors labeled kind='eval' and SourceSnippet always null despite a registered SourceMap → playground's Rust-style box degrades. | Thread engine.SourceMap into MapFlowErrors and populate SourceSnippet from the source line. | no |
| 50 | low | WebAudioBackend.cs:331,187 | Buffers with >2 channels are marshalled to WebAudio as stereo → garbled playback (PromoteToStereo passes ≥2ch through; JS de-interleaves as 2). | Add a >2ch downmix branch in PromoteToStereo + advisory. | no |

### 2.11 Documentation accuracy

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 51 | medium | wiki/Playback-and-Export.md:103,122-128,300-301 | Removed builtin `exportWav` still documented as current across 7 wiki pages; examples use old buffer-first arg order and error. | Replace with path-first `writeWav` across all 7 pages; delete the exportWav section/rows. | no |
| 52 | medium | wiki/Home.md:76-77 | TOC links to two non-existent wiki pages (Live-Coding.md, OSC-and-MIDI.md) → 404 on the live docs site. | Author the pages or remove the TOC entries (also fix Quick-Start.md:73 + Audio-and-Synthesis.md:163 dangling links). | no |
| 53 | medium | FEATURES.md:169 | Claims raw oscillators saw/square are PolyBLEP band-limited; the raw builtins are naive (PolyBLEP is only on the renderSong synth path). | Drop the PolyBLEP parenthetical (or split raw vs synth rows); also fix BuiltInDocs.cs:105,110. | no |
| 54 | low | wiki/Audio-and-Synthesis.md:172-173 | Synth-instrument table labels "saw"/"square" as naive; Phase 46 made them PolyBLEP band-limited (triangle row correct). | Update rows 172-173 to "PolyBLEP band-limited (Phase 46)". | no |
| 55 | low | CLAUDE.md:305,345 | Mis-states horn GM routing (says 56; code routes horn→60) and drum channel is internally inconsistent (ch 9 vs ch 10). | `brass*`→56, `horn*`→60; align drum-channel wording (ch 9 0-indexed). | no |
| 56 | low | FEATURES.md:442 | BuiltInDocs entry count stale (claims 104, actual 107). | Update to 107 or use an approximate phrasing. | no |
| 57 | low | FEATURES.md:434 | Test-file count stale (claims 123, actual 133 top-level test_*.flow). | Update to 133 / "130+". | no |
| 58 | low | FEATURES.md:11 (wiki/Home.md:9) | Special-types count stale (says 22, codebase has 26 — missing the Phase 38/40 handles); Home.md also mislabels Buffer as music-aware. | Update to 26; fix the Buffer mislabel. | no |
| 59 | low | CLAUDE.md:79-83 | Guard-location line numbers drifted (Parser.cs:220→251, FlowEngine.cs:185,202→233,254, BuiltInFunctions.cs:1027→1035). | Refresh the numbers or key the references on symbol names. | no |

### 2.12 Diagnostics quality (errors / charity gaps)

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 60 | high | Parser.NoteStream.cs:401 | Uppercase note-name typo in a note stream (`\| C4 D4 Z9 E4 \|`) silently drops surrounding notes and reports a location-less "Empty note stream" — asymmetric with the charitable lowercase path. | Recover at the break: emit a charitable rest + located advisory, Advance/continue (mirror the lowercase var-ref path). | no |
| 61 | medium | StdLib.cs:390,400 (ExpressionEvaluator.cs:1035 dead) | Integer/all division by zero throws to the catch-all → location-less "Unexpected error"; the charitable ReportDivisionByZero handler is dead code. | Catch the zero-divisor InvalidOperationException in the internal-call dispatch and call ReportDivisionByZero(location). | no |
| 62 | medium | Interpreter.cs:1049-1051 (Value.cs:382) | A failed RHS feeding a typed declaration produces a SECOND, location-less cascade error leaking C# ("underlying CLR type 'null'"). | Skip the declaration when the RHS is Void (already errored); hide CLR-type detail behind a verbose flag. | no |
| 63 | medium | BuiltInFunctions.cs:1651-1654,1725-1728,1748-1751 | euclidean throws InvalidOperationException on degenerate input instead of the charitable WarnOnce-clamp the rest of the generative family uses; loses source location (0:0). | Clamp + WarnOnce (strict-mode → located ReportError); keep the >1024 DoS clamp. | no |
| 64 | low | NoteStreamCompiler.cs:972 | Random-choice weight-sum warnings use Console.Error.WriteLine per-element (spam) instead of WarnOnce; also bypass the WASM stderr redirect. | Route through RenderingDiagnostics.WarnOnce keyed by location; reword to not imply weights must sum to 100. | no |

### 2.13 Ergonomic surface / consistency (confirmed defects)

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 65 | medium | Interpreter.cs:938-941 | User procs & all Flow-defined stdlib procs silently lack named-arg support (param names dropped at declaration); error wrongly says "names not yet declared". | Pass `proc.Parameters.Select(p=>p.Name)` to the FunctionSignature ctor. | no |
| 66 | high | ExpressionEvaluator.cs:108,118,126,136 (SimpleLexer.cs:692…862) | Cent/Decibel/Millisecond/Second parse with locale-dependent double.TryParse → silent 10× corruption in comma-decimal locales. | Pass NumberStyles.Float + InvariantCulture at both lex + eval sites; pin InvariantCulture at startup. | no |
| 67 | medium | BuiltInFunctions.cs:236-244 | `(str 440Hz)` fails with ambiguous-overload (Float vs Double) — no str(Hertz) overload, while every other music literal stringifies. | Add a str(Hertz) overload returning "<n>Hz" (InvariantCulture). | no |
| 68 | medium | std.flow:330 (VisualizationFunctions.cs:36-38) | Documented `(inspect seq)` alias is unreachable — registered in C# but has no `.flow` surface decl; `(visualize)` works. | Add `internal proc inspect (Sequence: seq)` to std.flow. | no |
| 69 | low | Standard Library / BuiltInDocs.cs:36 | `length` is documented (BuiltInDocs + CLAUDE.md) but not registered — only `len` exists; `(length …)` errors. | Register `length` as an alias of `len` (or fix the docs to `len`). | no |
| 70 | medium | BuiltInDocs.cs:65 (Collections) | `(zip a b)` documented with a BuiltInDocs entry but never registered → "Function not found". | Implement & register `zip` (two wildcard arrays → array of 2-tuples). | no |

### 2.14 Note-stream / voice-block / tuplet / beat (parser + render)

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 71 | high | NoteType.cs:392-394 | Non-4/4 time signatures render at wrong wall-clock speed (6/8 2× slow, 2/2 2× fast) in WAV + MIDI — GetBeats returns denominator-unit beats; converters assume quarter beats. | Make GetBeats return quarter-note units (`fraction*4.0`) and convert the Numerator-derived bar totals likewise. | no |
| 72 | high | Parser.NoteStream.cs:256-259, 792-795 | Articulation on a chord bracket silently corrupts: `[C4 E4 G4]q.stacc` → dotted chord + phantom rest + no articulation; `[…]q>` is a hard parse error. | Add an Articulation field to ChordElement and call TryParseArticulation in both chord-bracket parse branches; thread into CompileChordElement. | no |
| 73 | medium | SimpleLexer.cs:197-203 (transforms-voiceblocks) | Rest-with-suffix `_q`/`_h`/`_e` lexes as an identifier → bar rejected as "Empty note stream"; only bare `_` works. | Emit a standalone Underscore when `_` is followed by exactly one duration letter not starting a longer word (leaving the suffix as its own token). | no |
| 74 | medium | SimpleLexer.cs:201 (lexer / note-stream) | Same `_q`/`_w` rest-suffix lexing defect surfaced from the jazz dogfood (`\| _w \|` → "Empty note stream"); in-repo flagged as a known parser bug. | Same lexer fix as #73 (single root cause). | no |
| 75 | medium | TransformFunctions.cs:866-882 | `(invert seq)` is a silent no-op when all notes are in voice blocks (axis search ignores ParallelVoices → identity clone). | Recurse the axis search into ParallelVoices (parent-then-voices order). | no |
| 76 | low | VisualizationFunctions.cs:72-95 | `(visualize)`/`(inspect)` reports "(no notes in sequence)" for voice-block sequences (display-only; iterates only MusicalNotes). | Add a ParallelVoices pass with a per-voice cursor from the bar onset. | no |

### 2.15 Tuning / config / SFZ surface / formatting

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 77 | high | PitchConversion.cs:98-112 | JI/Pythagorean pragma produces wrong frequencies for any non-C key (tonic itself wrong) — C-relative ratio × tonic 12-TET anchor, not normalized. | Divide each looked-up ratio by the tonic's own C-relative ratio (tonic-relative); add non-C-tonic regression tests. | no |
| 78 | high | flow-interpreter/Program.cs:7 | flow-interpreter never loads ~/.config/flow/config.toml → sfz_root (and tempo/timesig/device/stdlib paths) silently ignored; SFZ scripts hard-fail (only flow-cli loads config). | Move FlowConfigLoader to flow-lang/Runtime + Tomlyn to flow-lang.csproj; call LoadFromXdg() at interpreter startup. | no |
| 79 | low | ScalaBuiltins.cs:77 | `(loadScala "x.scl")` resolves CWD-relative, unlike script-relative `use` → fails when run from another directory; error doesn't mention the asymmetry. | Resolve relative .scl/.kbm against the current script directory (add CurrentScriptFile to ExecutionContext). | no |
| 80 | low | SfzParser.cs:84-108 | SViolinVib.sfz uses `tune`/`ampeg_dynamic` opcodes outside the 20-opcode whitelist — charitably ignored; `tune=-20` makes one region render ~20¢ sharp. | Add `tune` (parse as signed cents → TuneCents on SfzRegion, fold into varispeed); whitelist ampeg_dynamic as an ignored flag. | no |
| 81 | low | Value.cs:425 | `(str 1.0)` drops the decimal point ("1" not "1.0"); also via StrBeat. Cosmetic. | Append ".0" when a finite G10-formatted double has no '.'/'E'; or fix the sweep's expectation comments. | no |

### 2.16 Filters / time-stretch (charity + length)

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 82 | medium | PhaseVocoder.cs:99 | `stretch(#vocoder)` returns +frameSize (2048) extra frames (~46ms) vs PSOLA; the extra tail is real OLA residue. (#auto works around via Math.Min.) | Size the returned buffer as round(inFrames*factor); the internal OLA buffer keeps its own +frameSize headroom. | no |
| 83 | medium | Filter.cs:101 | lowpass/highpass/bandpass throw on a Nyquist-boundary cutoff (≥22050Hz at 44.1k) → crashes the interpreter session; violates charitable policy. | Clamp to nyquist-1 (and ≤0 → 20Hz) with WarnOnce, matching the bandpass Q-clamp house pattern. | no |

### 2.17 Musical context / MIDI routing

| # | sev | file:line | finding | suggested fix | hw-gated? |
|---|-----|-----------|---------|---------------|-----------|
| 84 | medium | MusicalContext.cs:92 / Interpreter.cs:267-278 / ExecutionContext.cs:918-960 | `swing N { }` context block is a render no-op — Swing is stored but read by no render/MIDI path; only `quantize` swings. | Wire Swing into note-onset placement (reuse quantize math, map [0,1]→[-1,1], identity-guard at 0.5); at minimum emit an advisory. | no |
| 85 | medium | InstrumentRouting.cs:82 | No `bass` GM prefix — a `bass*` sequence exports as piano (program 0) across MIDI/MusicXML/LilyPond; no advisory. | Add synthbass→38, electricbass→33, fretless→35, bass→32 after the `bassoon` check (ordering matters). | no |

> Note: the JSON file enumerates all 88 findings with full root-cause/repro/fix text. The 85 rows above are the deduplicated table view; a few singleton-subsystem findings share a root cause with a listed row (e.g. #73 and #74 are the same `_q` lexer defect surfaced from two dogfood pieces, and the MarkovFunctions naming nit appears once). All 88 objects are present and load-bearing in the JSON.

---

## 3. Ergonomics

Grouped by theme. Each: **title** — friction — suggestion.

### Language surface & namespace
- **Chord/note symbols steal identifiers** — `Int Am = 5`, `proc Am()` fail ("Expected variable name. Got ChordLiteral") because ChordParser claims `Am`/`Dm`/`Cs` at lex time — suggestion: relax the lexer in declaration/binding position, or document the reserved shapes.
- **Musical-context blocks reject a variable value** — `gain x { }`/`pan p { }` error "Unexpected token {" when x/p are variables — allow identifiers/parenthesized expressions or give a targeted diagnostic.
- **Single Notes are second-class** — `(transpose C4 +5st)`, `(up C4)`, `(play C4)` all error; only Sequences work — add Note overloads that lift to single-element ops.
- **No `Note[] → Sequence` constructor** — generative arrays (markov/lsystem/chordNotes) can't reach the audio path without heavy createSequence ceremony — add `(sequence Note[])` honoring the active timesig.
- **scaleNotes/chordNotes return inconsistent Strings, no string→Note** — scaleNotes drops octaves, chordNotes keeps them, and `(note "C4")` doesn't exist — return Note[] (or add `(note String)`).

### Overloads & named args
- **User procs cannot accept named args** — the same defect as bug #65, surfaced as friction — thread proc param names into the signature.
- **OverloadResolver named-arg comment asserts a contract the builtins violate** — the "first survivor wins" comment claims distinct names per overload but transpose/db/hz/ms/sec/cents share names — update the comment after the resolver fix.
- **Two same-signature nested procs → ambiguity instead of innermost-wins shadowing** — `GetFunctionOverloads` merges both frames — prefer the local overload over an inherited identical signature.
- **createSineTone family error message misleads** ("does not yet support named arguments" though names ARE declared) and the three overloads disagree on param-name order — fix the message + canonicalize ordering.
- **transpose Int-vs-Double footgun** — `12` is an octave, `12.0` is 12 cents (inaudible), silently — consider an advisory when an integer-valued Double hits the Cent overload.
- **`length`/`zip`/`inspect` documented-but-unreachable** — see bugs #68-70; ergonomically these read as "the docs lie".

### Diagnostics
- **"No matching overload" never shows candidate signatures** (the ambiguous path does) — append `sig.ToString()` candidates, capped ~3-5.
- **Unknown `@module` leaks an absolute bin path** with no stdlib hint — detect the `@`-prefix branch and did-you-mean over known stdlib modules.
- **did-you-mean suggests punctuation builtins** (`?` for `x`) — filter the candidate pool to identifier-shaped names.
- **euclidean is the only generative primitive that throws** (bug #63) — align with the charitable family.
- **Missing-sample silence indistinguishable from a rest** (bug #15 surface) — emit a one-shot per-instrument advisory.

### Audio / DSP charity
- **lowpass/highpass accept unbounded Q with no clamp/advisory** (only bandpass is guarded) — Q=100000 silently pushes >1.0 — mirror the bandpass MaxQ clamp.
- **compress/delay throw on out-of-range params** instead of charitable clamp — coerce + advisory to match house style.
- **stretch #vocoder length surprise** (bug #82) — fix or advise on the +46ms.
- **gain() vs volume() naming needs a doc lookup** — `(gain buf 0.5)` is read as dB (≈no attenuation) — advise when a bare Double magnitude looks linear.

### Live / OSC / MIDI workflow
- **OSC handlers never fire without an explicit `(oscPump)` drain**, and there's no sleep/yield — document the pump-in-a-loop pattern; consider `(oscPumpFor …)`.
- **200 Hz per-path OSC rate limit silently drops rapid triggers** — key by path+arg-signature or expose the window; debug advisory on drops.
- **No way to close/panic a low-level MIDI device handle** (bug #35 surface) — add closeMidiOutput/midiPanic.
- **midiPorts returns Void and prints to stdout** — can't pick a port programmatically — return Array[String].
- **jackSync is one-shot** — no continuous tempo follow — document poll-in-a-loop or add jackFollow/jackStop.

### CLI / install
- **`flow midi2flow` lacks the standalone flow-midi flags** (--no-sustain/--sfz/--dump/--playable), and flow-midi isn't installed — add the flags to the unified verb.
- **`flow render -o` is a no-op for the output path** (only warns) — honor -o by copying the emitted WAV, or exit non-zero when -o is unmatched.
- **install.sh doesn't validate stdlib .flow files in the payload** — add a post-extract check for std.flow/audio.flow.

### WASM / Web
- **RunResult.wav is permanently null** with no in-memory WAV sink despite the field+typedef — add a FileIO in-memory WAV sink, or drop the field + advise.
- **Comment falsely claims Roslyn const-folds SupportsLiveBlocks/IsWebTarget** (they're runtime static props) — soften the const-fold claim in three files.

### Docs consistency
- **FEATURES.md vs wiki disagree on saw/square band-limiting** (bugs #53-54) — adopt one raw-vs-synth split everywhere.
- **Hard-coded counts drift every phase** (bugs #56-58) — generate in CI or use ranges.

### Note-stream / transforms / generative / tuning workflow
- **`(str seq)` doesn't report voice-block contents** — add a "N parallel voices in bar M" note.
- **Tuplet syntax requires an explicit duration suffix** with non-obvious meaning — add a friendly parse-time advisory explaining `{3:2 …}q`.
- **lsystemToSequence dumps one monolithic bar** with no bar-splitting — add `bars=` or auto-split by timesig.
- **Weight-sum warning implies weights must total 100** (bug #64 surface) — reword to "relative weights".
- **Two interpreter entry points behave differently for config** (bug #78 surface) — startup advisory when no config loaded.
- **enable pragma must precede even `use`** — relax or add a hint to the error.
- **No builtin to query sequence duration** in quarter-beats/seconds — add durationBeats/durationSeconds.
- **Tuplet shorthand `{3}q` ratio ambiguity** — document `{N:M}` requirement after the Denominator fix.
- **Carlos Alpha `[tuning]` advisory never fires with the default linear KBM** — the comment misleads — clarify.
- **`(loadScala …)` must run from repo root** (bug #79 surface) — script-relative resolution or doc the CWD dependency.

### Collections / lambdas / DSP-chain ergonomics
- **`(str tuple)` has no overload** though `(print tuple)` works — add str(Tuple).
- **`(concat)` is binary-only** — nesting for ≥3 parts — add a varargs concat or strJoin.
- **No guidance when a `#symbol` pattern arm misses** (bug surface of #Symbol-pattern) — advisory naming the unrecognized articulation symbol.
- **Named-arg granular differs from positional** (PRNG key is call-site) — document; consider a seed= arg or content-based seeding.

### Jazz-combo showcase ergonomics
- **NoteValue constants (EIGHTH) need `use "@notation"`** and bare `e` can't be passed to quantize — re-export the constants / accept a duration letter.
- **A 4-instrument WAV mix needs 4 duplicate per-stem Songs** because renderSong takes one synth name — let renderSong honor per-sequence-name routing like the MIDI path.

---

## 4. Dogfooding

| piece | ran? | rendered? | keepable? | path | what broke |
|-------|------|-----------|-----------|------|------------|
| transforms-voiceblocks | yes | yes (WAV+MIDI) | **yes** | examples/sweep/transforms_voices.flow | 3 bugs found while authoring: chord brackets don't re-arpeggiate after transforms (expected); `_q` rest-suffix is a lexer bug (workaround: bare `_`); voice blocks survive transforms. Two-run cmp-clean confirmed. |
| generative | yes | yes (2 WAVs, A-C byte-identical) | **yes** | examples/sweep/generative.flow | 5 bugs: `?`/`??` non-determinism (section D demonstrates it); lsystemToSequence single-bar overflow; euclidean throws on degenerate input; weight-sum warning spam; Markov param naming nit. |
| sfz-articulation | yes (flow-cli only) | yes (7.2 MB WAV) | **yes** | examples/sweep/sfz_articulation.flow | Primary bug: interpreter never loads config.toml → "SFZ root not configured"; only `flow-cli run` works. Two unrecognized-opcode advisories (tune/ampeg_dynamic). MD5-stable. |
| tuplets-beat | yes | yes (2 WAVs, audible) | **yes** | examples/sweep/tuplets_beat.flow | 2 high bugs: tuplet Denominator ignored ({3:2}q wrong); non-4/4 wall-clock speed (6/8 2× slow, 2/2 2× fast). beat-true-to-sig literals + dotted/fractional durations correct. |
| microtonal | yes | yes (4 distinct WAVs) | **yes** | examples/sweep/microtonal.flow | Each tuning system byte-distinct; two-run clean. Uncovered the JI/Pythagorean non-C-key frequency bug (piece avoids it by using key Cmajor). Carlos Alpha [tuning] advisory comment is misleading (harmless). |
| collections-lambdas | yes | no (compute-only, exit 0) | **yes** | examples/sweep/collections.flow | 3 bugs: `(if …)` evaluates both branches (most impactful — broke each+if callbacks; piece reworked to dict-filter); symbol literal pattern arms always miss; `zip` documented but unregistered. |
| dsp-chain | yes | yes (1.2 MB WAV, no clipping) | **yes** | examples/sweep/dsp_chain.flow | Identity fast-paths byte-identical; two-run clean; expected #auto advisory. 1 bug: PhaseVocoder +frameSize length; 1 ergonomics: filter Nyquist crash. |
| jazz-combo-showcase | yes | yes (51.7s WAV + multi-track MIDI, two-run identical) | **yes** | examples/jazz/combo.flow | 4 bugs: swing block is a render no-op (used quantize instead); chord-bracket articulation corruption; `_q`/`_w` rest lexer bug; no `bass` GM prefix (workaround: stringBass→GM48). Complete intentional jazz piece. |

All 8 pieces ran to exit 0 and are keepable as committed showcase/sweep content. None require hardware.

---

## 5. Out of scope / cancelled

- **Ableton Link — CANCELLED by the owner.** Already documented in CLAUDE.md as DEFERRED (D-40-06, GPL contamination hazard). No Link work in this sweep; no findings filed against it. A clean-room/re-licensed community binding remains welcome.
- **Hardware-gated finding (owner UAT, not auto-fixed):** 1 finding has `requiresHardware=true`:
  - **#35 — `openMidiOutput` handle leak / stuck notes** (`MidiFunctions.cs:94-106`, `RtMidiMidiBackend.cs:218-300`). The native device + ALSA port leak only manifests with a real matchable MIDI output port (hardware/software synth/DAW virtual port). The *fix* (finalizer/SafeHandle backstop + `closeMidiOutput`/`midiPanic` builtins) is implementable without hardware, but verifying the leak/stuck-note behavior end-to-end needs a real port — flagged for owner UAT.
- **Other MIDI/JACK behaviors that are in-process reproducible** (clockMaster frozen snapshot #36, tempo upper-bound busy-spin #37, midiOut voice-block drop #33, multi-tempo divergence #34, overrides coercion #38) are NOT hardware-gated — they reproduce with the in-process test seams (CaptureMidiBackend / TransportQueryOverride / TimestampingHandle) and are fixable here.
