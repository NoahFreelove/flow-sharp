# Flow Language Milestones

Historical record of shipped versions. Full details archived in `.planning/milestones/`.

---

## v1.6 Backlog (forward-deferred, not yet scheduled)

Items deferred during v1.5 phases that are out of scope for the current milestone but should not be lost. Scheduled into a v1.6 milestone when one is opened.

**WASM runtime + browser (Phase 48 follow-ups):**
- Chrome (+ other Chromium) WASM audio re-smoke — the original 2026-05-30 boot blocker (`dotnet.boot.js` 404 / no AppBundle) was root-caused + FIXED + HTTP-verified in Phase 48 (curl `/_framework/dotnet.boot.js` → HTTP 200); the human audio ear-check was deferred because Firefox already passed on the same engine path. Re-smoke audio in Chrome/Chromium early in Phase 49.
- Safari WASM smoke — no macOS available during Phase 48 (Linux-only dev machine); verify under Phase 49 / v1.6. Safari historically has the strictest autoplay policy; the D-48-09 gesture chain should satisfy it but confirm.
- AudioWorklet + SharedArrayBuffer ring-buffer streaming (D-48-02) for live-coding-in-browser — requires COOP/COEP headers (Cloudflare Pages supports natively; Phase 49 may wire preemptively) + un-stripping `live { }` blocks for Web. Multi-week stretch; also unlocks a real preemptive 30s cap via a worker thread (D-48-10).
- NativeAOT-LLVM via a source-generator pass on `InternalFunctionRegistry` (D-v1.5-02) — would let `flow-lang.dll` AOT-link, dropping bundle size 50%+. Source-gen authoring is non-trivial.
- `runtime.exportWav()` helper in `flow-runtime.js` (D-48-18 WAV-download parallel) — hand-rolled WAVE-header wrap for browser download; v1 plays audio live via WebAudioBackend instead.
- v1.6 WebRTC DataChannel OSC-shaped surface; WebMIDI for live MIDI hardware; IndexedDB persistence for saved scripts; service-worker offline-PWA playground.

**flowlang.dev site (Phase 49 follow-ups):**
- Custom domain (D-49-37) — composer grabs e.g. `flowmusic.dev` / `flow-music.dev` / `composeflow.dev` (`flowlang.dev` is taken by an unrelated language) and CNAMEs it to CF Pages. v1.5 ships on `<project>.pages.dev`. Dashboard + DNS only, no code change; steps in `49-DEPLOYMENT-RUNBOOK.md` §6.
- Wiki auto-rebuild webhook — a GitHub Action on the wiki repo that triggers a CF Pages deploy hook so wiki pushes auto-rebuild without a flow-sharp default-branch push (D-49-25; v1 rebuilds on flow-sharp push).
- Monaco full-LSP bridge (D-49-14) — currently syntax highlighting + builtin Tab-completion via the hand-written Monarch tokenizer; wire `monaco-languageclient` to the Phase 17 `flow-lsp` for hover/diagnostics/go-to-def.
- COOP/COEP un-scoping → AudioWorklet + SharedArrayBuffer in-browser live coding (shared with the Phase 48 D-48-02 stretch) — `_headers` already lays the scoped-to-/playground foundation.
- AnalyserNode audio-waveform visualization in the playground (D-49-CONTEXT deferred) — a live oscilloscope/spectrum over the rendered buffer.
- Anonymous "Save" fallback (no GitHub) + community-submitted showcase pieces (gallery curation + moderation pipeline); PWA install / IndexedDB script persistence; mobile-editing affordances (Monaco zoom + large-target buttons); inline runnable code in docs pages; i18n.

> **Phase 49 status (2026-06-05): EXECUTION COMPLETE — PENDING HUMAN-UAT + LIVE DEPLOY (NOT shipped).** The greenfield `flow-site/` SvelteKit 2 / Svelte 5 / TS / Tailwind v4 site + skeuomorphic playground is built + green in CI (vitest 70/70, playwright 275/275, lhci ≥0.9 ×4, axe 0-critical), but THREE human-action gates remain OPEN (live CF deploy, GitHub OAuth App + live gist, cross-browser AUDIBLE/visual/SR UAT — all in `49-HUMAN-UAT.md`). Phase 49 flips to SHIPPED only after that sign-off, and does NOT yet increment the v1.5 shipped-phase count. **v1.5 milestone status: 11/15 phases SHIPPED** (35 + 36 + 37 + 38 + 39 + 42 + 43 + 44 + 45 + 47 + 48); Phase 40 (Studio Sync) + Phase 41 (Reach + v1.5 Closer) + Phase 46 + Phase 49 still pending close.

---

## v1.4 Audio Fidelity, Distribution & Public Showcase — Shipped 2026-05-16

**Goal:** Ship the v1.4 audio-fidelity rewrite (per-voice polyphony + articulation envelopes + sampled tonal instruments), the distribution wedge (self-contained `flow` CLI + install + XDG config + MIDI↔Flow round-trip), LSP polish + JetBrains plugin scaffolding, full Scala (`.scl`) microtonal tuning loader, SFZ orchestral sampler (blessed library: VSCO Community CE 1.1.0), and the curated symphony showcase + ragtime companion as the milestone closer — flipping Flow from pre-public to public.

**Delivered:** A rewritten per-voice polyphony + articulation system (5 articulation tokens: staccato, legato, accent, marcato, tenuto with locked envelope rules across all 9 shipping synthesizers); sampled tonal instruments (piano, brass, sax, strings, flute, bell) via a 3.05 MB CC-BY 4.0 University-of-Iowa MIS sample bundle and a SampledInstrumentRenderer that layers Phase 28 articulation envelopes on top; a self-contained 40 MB `flow` Linux x64 binary with 11 subcommands (run/eval/repl/watch/play/render/flow2midi/midi2flow/check/version/new), per-user + system install script, XDG config (~/.config/flow/config.toml) with 5 functional keys, and ±1-tick MIDI↔Flow round-trip on 3 CC0 fixtures; LSP polish (4 closed gaps: completion filter, varargs rendering, comment-form handling, scale-lint diagnostics) plus a JetBrains plugin scaffold (stretch goal met); a full Scala (`.scl`) tuning loader with `(loadScala "path.scl")` builtin, `tuning t { ... }` musical-context block, optional `.kbm` keyboard mapping, ±0.1¢ Carlos Alpha / Bohlen-Pierce acceptance, last-wins integration with the Phase 23 pragma system; an opt-in SFZ orchestral sampler (`use "@sfz"`) with a 19-entry GM symbol dict, common-subset SFZ parser (13 opcodes + `<region>`/`<group>`/`<global>`/`<control>`), per-region sustain looping with 441-frame equal-power crossfade, blessed external library (VSCO Community CE 1.1.0); and two public showcase pieces — "In Five Voices" (orchestral, D minor, ~60s, 5 VSCO-CE instruments) plus "Stride & Stomp" (ragtime, F major, ~58s, solo VSCO-CE UprightPiano) — published in the v1.4.0 GitHub Release with 5 labeled assets (symphony + ragtime as MP3 + WAV pairs + Linux self-contained tarball), accompanied by a top-level README.md `## Showcase` section with inline user-attachments audio embeds and a `docs/announcements/v1.4.0.md` public announcement.

**Stats:**
- Phases: 7 (Phase 28 – Phase 34)
- Plans: 52 across all v1.4 phases (28=7, 29=7, 30=9, 31=9, 32=7, 33=7, 34=6)
- Requirements: all v1.4 SPEC-* + REQ-* + SYM-01..05 marked Complete in REQUIREMENTS.md
- Git range: post-`v1.3.0` tag (2026-05-10) → `v1.4.0` tag (2026-05-16) — ~6 days, dense burn-down by the parallel-executor orchestration model
- Source files at close: 1321 tracked files (`git ls-files | wc -l`)
- Release: https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0 (5 assets, ~60 MB total)

**Key accomplishments:**
1. Phase 28 — articulation system (5 tokens: staccato / legato / accent / marcato / tenuto, locked envelope rules across 9 synthesizers) + per-voice polyphony (`voicePool 32 { ... }` musical-context block, steal-oldest-onset with deterministic tiebreaker) + multi-track MIDI export (one track per uniqueSequenceName + prefix-match GM program routing)
2. Phase 29 — sampled tonal instruments (piano + brass + sax + strings + flute + bell via `SampledInstrumentRenderer`, 3.05 MB CC-BY 4.0 University-of-Iowa MIS bundle, ≤ 5 MB cap, eager-load + per-FlowEngine `SampleCache`)
3. Phase 30 — `flow` self-contained Linux x64 binary (~40 MB) + 11-subcommand CLI + `install.sh` (per-user default, `--system` flag, idempotent) + XDG config (5 functional keys) + MIDI↔Flow round-trip (±1 tick on 3 CC0 fixtures via `Quantizer` + `FlowGenerator` rewrite; latent Phase 28 `writeMidi` denominator double-encoding bug found and fixed)
4. Phase 31 — LSP polish (4 closed gaps: completion filtering by imports + pragmas + musical-context boost; varargs rendering in Hover/SignatureHelp; new comment-form lexer support; scale-lint analyzer wiring) + JetBrains plugin scaffolding (stretch goal MET)
5. Phase 32 — full Scala (`.scl`) tuning loader + `tuning t { ... }` musical-context block (three composer surface forms: identifier, inline call, string-literal sugar), ±0.1¢ Carlos Alpha / Bohlen-Pierce acceptance, 5 canonical archive fixtures + 3 malformed parser-error fixtures, `Tuning` first-class music type with reference identity, optional `.kbm` keyboard mapping with default-linear-KBM synthesis (D-07)
6. Phase 33 — SFZ orchestral sampler (`use "@sfz"` opt-in gate; `(loadSfz #symbol)` / `(loadSfz "/abs/path.sfz")` builtins; 19-entry GM symbol dict; 13-opcode common-subset parser + `<control>` extension; `SfzRenderer` with grid lookup + nearest-pitch varispeed fallback + 441-frame equal-power crossfade + Phase 28 articulation envelope hook; blessed external library VSCO Community CE 1.1.0)
7. Phase 34 — symphony showcase ("In Five Voices") + ragtime companion ("Stride & Stomp") + v1.4.0 annotated tag + GitHub Release with 5 labeled assets + docs/announcements/v1.4.0.md + top-level README `## Showcase` section + milestone closure docs ([release link](https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0))

**Patterns established:**
- Two-run cmp-clean determinism contract (Phase 18/25/27/33 inheritance — replacing pre-Phase-28 pinned-bytes which was dropped when articulation envelopes legitimately changed rendered bytes; the contract is in shape, not in pinned bytes)
- RMS-windowed regression testing for behavior that legitimately changes bytes but preserves perceptual fidelity (±0.5 dB / 100ms tolerance per SPEC-8; baselines under `flow-lang.Tests/baselines/Phase28/`)
- HUMAN-UAT.md for subjective composer sign-off (Phase 17 + Phase 33 + Phase 34 precedent — `closed_with_followup` status preserves UAT close while honestly flagging v1.5+ residual concerns)
- Per-instrument render + sum-mix pattern for orchestral pieces (Phase 33 + Phase 34)
- GitHub user-attachments drag-drop for inline audio player in README (Phase 34 novel — RESEARCH Pitfall 1 manual drag-drop workaround for GitHub's lack of CLI upload support)
- Per-showcase-piece dual-format asset pair (MP3 for streaming + WAV for archival) — repeatable for future v1.X releases shipping multiple curated pieces
- Release-asset filenames lock to the URL pattern the announcement pre-bakes — composer / planner / executor agree on the URL shape BEFORE either the announcement is drafted or the release is created
- Pre-public → public pivot as a first-class milestone-closure step: external memory file `project_pre_public_no_legacy_burden.md` rewritten in lockstep with CLAUDE.md "Public as of v1.4" footnote so all future sessions inherit the post-public framing

**Known deferred items at close:**
- Phase 17: 3 pending HUMAN-UAT scenarios (manual-smoke.md rows 1-3) — orthogonal to v1.4; rolled forward to v1.5 backlog
- Phase 04: VERIFICATION.md gaps_found — orthogonal to v1.4; rolled forward to v1.5 backlog
- Phase 34 ragtime: composer accepted UAT iteration #2 with `closed_with_followup` status (warmer-piano timbre / SFZ velocity layers / humanizeGaussian voice-block bug remain v1.5+ candidates)

**Forward-deferred items (v1.5+ candidates):**
- Stereo panning across instruments (SfzRenderer stereo retrofit OR hand-stereo via dual-render-and-pan)
- A second contrasting genre showcase (jazz / EDM / death metal — to validate the "genre-agnostic" claim further)
- SFZ round-robin opcode parser extension (`seq_position` / `seq_length`)
- Per-articulation envelope multipliers for the sampled instrument path (Phase 29 follow-up — staccato sounds thinner than on the Phase 28 hand-rolled synths)
- Sampled drums with transient-preserving pitch shift (Phase 29 v1.5 follow-up — drums remain synth-only per SPEC D-02)
- More flute samples to close the D5 timbre-crossover gap (Phase 29 v1.5 backlog)
- GitHub-rendered video screen-recording demo (live coding session capture)
- `flow showcase` CLI subcommand (one-command render of both v1.4 showcase pieces from a fresh checkout)
- Per-articulation A/B fixture as a permanent example (e.g. `examples/symphony/symphony_no_articulation.flow`)
- Warmer-piano timbre + VSCO velocity-layer expansion (composer ragtime UAT iteration #2 follow-up)
- humanizeGaussian voice-block bug investigation (Phase 34 ragtime iteration #2 follow-up)
- JetBrains plugin Marketplace publish (Phase 31 stretch — scaffolding ships in v1.4, publish DEFERRED to v1.5)

**Archives:**
- `.planning/phases/{28,29,30,31,32,33,34}/` (per-phase planning artifacts: CONTEXT.md + RESEARCH.md + PATTERNS.md + per-plan PLAN+SUMMARY + VERIFICATION.md + HUMAN-UAT.md where applicable)
- `examples/symphony/symphony.flow` + `examples/symphony/sfz_smoke.flow` + `examples/symphony/README.md` (canonical Flow source + reproduction docs)
- `examples/ragtime/ragtime.flow` + `examples/ragtime/README.md` (canonical Flow source + reproduction docs)
- `docs/announcements/v1.4.0.md` (public announcement, verbatim release body)
- GitHub Release: https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0
- Annotated tag `v1.4.0` (object SHA `66842d6efafd5105c82521c07b977dd1113504d1` pointing at commit `74de69adb47b2a23985633a392f6ddb6f1389f21`)

---

## v1.3 Composer DX Tier B/C — Shipped 2026-05-10

**Goal:** Close every DEFER-01..06 item carried from v1.2 and ship the Tier B/C composer DX bundle, with tuplet + arbitrary-duration note syntax as the lead capability. Land a foundational language consistency pass (prefix-only arithmetic standardization), the symbols + tuples + generic dicts bundle, and the music-type ergonomics gap surfaced after Phase 25.

**Delivered:** Rational duration arithmetic (`Fraction` struct + `MusicalNoteData.DurationFraction`); tuplet brackets `{N:M ...}q` + per-note shorthand `C4/X:Y[suffix]` + arbitrary fractional durations `C4/12` with nested tuplets, bar-fit validation, and auto-elevated MIDI TPQN (cap 9600); DEFER-01..06 closures (`range`, multi-letter enharmonic edges, slice negative-from-end, file-scope `enable <pragma>;` system, H-as-B alias inside note streams, Gaussian humanize via Box-Muller); Tier B/C composer DX bundle (`arpeggio` 4-arg overload, `inversion` + `voicing` chord transforms, `delay` NoteValue-sync overload, `quantize` with strength + swing, `legato` + `portamento` articulations, `loadWav` varispeed overloads); microtonal tuning wedge (JI / Pythagorean / equalTemperament via pragma + Pattern A `RenderTuning` value object); scale linting (`enable scaleLint;` opt-in pragma in flow-lsp); operator standardization (prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(idiv)`/`(concat)` builtins, removal of `BinaryExpression` AST node, migration of all in-repo `.flow` files); symbols + tuples + generic dicts (`#foo` interned `Symbol` primitive, `<<a, b, c>>` `Tuple` literal with `~>` unpack op + destructuring + `@N` indexing, generic `Dict<K, V>` with hashable keys); music-type ergonomics (Ms/Sec/Hertz numeric compatibility, new `Hertz` type with `800Hz`/`1.5kHz` literals, `volume(Buffer, Double)` linear-multiplier function alongside dB-only `gain`, FX music-typed overloads for delay/compress/reverb/lowpass/highpass/bandpass/createXxxTone); tutorial + showcase refresh demonstrating every v1.3 feature with byte-identical determinism preserved.

**Stats:**
- Phases: 12 (Phase 18 – Phase 27, plus inserted Phase 26.1 + Phase 26.2)
- Plans: ~67 across all v1.3 phases
- Requirements: 41 total — all Complete (FRAC-*, TUP-*, DEFER-*, PRAG-*, DX-10..15, MICR-*, LINT-*, STD-*, SYM-*, TUP-09, DICT-*, ERG-*, QOL-04)
- Git range: post-`v1.2` tag (2026-04-26) → `v1.3.0` tag (2026-05-10) — ~2 weeks
- Forward-deferred to v1.4: full Scala (`.scl`) tuning loader, SFZ orchestral sampler, per-voice articulation envelope rewrite, instrument realism (sampled tonals), distribution wedge (CLI binary + install), LSP polish + JetBrains stretch

**Key accomplishments:**
1. Phase 18 — `Fraction` rational-arithmetic primitive (GCD normalize, never uses `double` for tuplet duration math) + `MusicalNoteData.DurationFraction` overrides the existing `DurationValue` enum when set
2. Phase 19 — tuplets `{N:M ...}q` brackets + `{N ...}q` music21-convention shorthand + nested tuplets via accumulating `Fraction outerScale` + `C4/N` arbitrary fractional duration syntax + per-note `C4/X:Y[suffix]` shorthand + bar-fit validator + auto-elevated MIDI TPQN (cap 9600)
3. Phase 20 — `range(Int, Int[, Int])`, slice negative-from-end indexing (Python-style), multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)
4. Phase 21 — file-scope `enable <pragma>;` system (lexer pre-scan, closed-set registry, no `use` propagation) + DEFER-02/03 `H`-as-`B` alias inside note streams
5. Phase 22 — `arpeggio` 4-arg overload (rate + direction + pattern), `inversion` + `voicing` chord transforms (drop2/drop3/open/close/spread), `delay` NoteValue-sync overload, `quantize` with strength + swing, `legato` + `portamento` articulations (MIDI CC65/CC5 emission), `loadWav` varispeed overloads (linear-interpolation pitch-shift)
6. Phase 23 — microtonal tuning wedge (JI / Pythagorean / equalTemperament via pragma; Pattern A `RenderTuning` value object threaded through `PitchConversion.NoteToFrequency` + 13 synthesizers; 7 JI + 7 Pythagorean mode-keyed ratio tables; `ScaleDatabase.TryParseKeyWithMode` 5-church-mode extension; one-shot `RenderingDiagnostics` warnings)
7. Phase 24 — scale linting (`enable scaleLint;` opt-in pragma in flow-lsp, zero flow-lang touch beyond one PragmaRegistry line, respects nested key contexts)
8. Phase 25 — Gaussian humanize via Box-Muller (`humanizeGaussian()` separate from uniform `humanize()` to preserve v1.2 byte-identical determinism contract)
9. Phase 26 — operator standardization (prefix-only arithmetic; deleted `BinaryExpression` + `BinaryOperator`; 5 same-type overloads per `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`; single-token negative literals at expression-start)
10. Phase 26.1 — symbols + tuples + dicts (`#foo` `Symbol` primitive with pointer-equality interning, `<<a, b, c>>` `Tuple` literal with `~>` unpack flow op + destructuring + `@N` indexing, generic `Dict<K, V>` with hashable keys, `(unpack tuple func)` first-class apply)
11. Phase 26.2 — music-type ergonomics (Ms/Sec/Hertz IsCompatibleWith Double|Float; Semitone stays Int-only; new `Hertz` type with `800Hz`/`1.5kHz` literals; new `volume(Buffer, Double)` linear-multiplier function alongside dB-only `gain`; FX music-typed overloads for delay-Ms / compress-Decibel-Ms / reverb-Second / lowpass/highpass/bandpass-Hertz / createXxxTone-Hertz family)
12. Phase 27 — tutorial + showcase refresh (every v1.3 feature exercised end-to-end with byte-identical determinism preserved; CLAUDE.md gained Music Types Quick Reference table; companion pragma files at `examples/pragmas/{h_alias,microtonal_ji}.flow`)

**Patterns established:**
- Two-pass strict authorship inherited from v1.2 — Pass 1 from REQUIREMENTS, Pass 2 reality-check; format / signature drift caught pre-commit
- Charitable interpretation honored throughout — D-07 voicings, Pitfall 7 random arpeggio fallback, Pitfall 9 quantize identity short-circuit, default-linear-KBM synthesis per D-07
- Defaulted-parameter migration pattern — `MusicalNoteData` accepted 3 new defaulted-parameter fields without breaking 30+ existing positional call sites (`OnsetOffset`, `DurationOverlap`, `PortamentoMs`)
- Byte-identical determinism contract maintained through additive-only changes (synth white-noise + TPDF dither RNGs reseeded at renderSong/writeWav boundaries; tutorial.flow + showcase.flow + euclidean.flow regression gates green across all v1.3 phases)
- File-scope pragma system as the natural opt-in mechanism for behavioral changes (PRAG-01 → MICR-01..03 → LINT-01..03 → DEFER-02/03)

**Archives:**
- `.planning/phases/{18,19,20,21,22,23,24,25,26,26.1,26.2,27}/` (per-phase planning artifacts)
- 9 quick-tasks alongside Phase 22-27 (260420-0c0, 260426-v5s, 260502-lhm, 260502-lum, 260502-oib, 260504-v6j, 260504-cks, 260504-w24, 260509-qqe — see STATE.md Quick Tasks Completed)

---

## v1.2 Stability & Composer DX — Shipped 2026-04-26

**Goal:** Verify then fix the critical bugs surfaced by the 2026-04-18 codebase audit, unblock the failing test suite, ship the Tier A + Tier B composer DX bundles, refresh the tutorial so v1.1 + v1.2 capabilities are discoverable, and ship a Flow Language Server with VSCode extension.

**Delivered:** A stable interpreter with cleanly-failing edge cases (init/Thunk/musical-context body), full Tier A + Tier B composer DX (slice, flat literals + enharmonic, MIDI velocity preservation end-to-end, reverbTime context block, euclidean swing/humanize with byte-identical output), a tutorial + showcase pair that exercises every v1.1 + v1.2 feature with audible WAV + MIDI output, a Flow Language Server (LSP) plus VSCode extension with syntax highlighting, live diagnostics, and intelligent completion/hover/signature-help/go-to-def, and retroactive Nyquist validation closing the v1.1 documentation-lag debt.

**Stats:**
- Phases: 7 (Phase 11 – Phase 17)
- Plans: 41 (all complete)
- Requirements: 18 total — all Complete (5 SPIKE verdicts + 13 fix/test/DX/QOL shipments)
- Git range: post-`v1.1` tag → `v1.2` tag (~3 months, 2026-01-24 → 2026-04-26)
- Source files at close: ~83K LOC C# + 312 .flow files

**Key accomplishments:**
1. Audit Spike isolated as Phase 11 — 1 Confirmed (C1 → FIX-07a) + 4 Dismissed (C2/C3/C4/C5) before any production code changed
2. Stability fixes — `init([])` errors cleanly (FIX-05), `Thunk` caches failures via `Lazy<Value>` (FIX-06), `ExecuteMusicalContext` body skip resolved (FIX-07a), `if(Bool, Void, Void)` wildcard overload + auto-mkdir for writeWav (TEST-03)
3. Nyquist Validation Backfill — Phases 6–10 each gained or promoted a `VALIDATION.md` at `nyquist_compliant: true` with observable-value pins (TEST-04)
4. Composer DX Tier A — `slice` (Sequence + Array[T]), extended flat-literal Parse + `enharmonic()` + lexer dispatch reorder, MIDI velocity byte-array regression via DryWetMidi (DX-05/06/08)
5. Composer DX Tier B — `reverbTime <s> { ... }` musical-context block with per-voice RT60 (Schroeder closed-form mapping, dry-render sentinel), `euclidean(hits, steps, note, swing)` + 6-arg humanize/seed overload with byte-identical output across runs (DX-07/09)
6. Tutorial + showcase refresh — `examples/tutorial.flow` and `examples/showcase.flow` exercise every v1.1 + v1.2 feature end-to-end with byte-identical determinism contract holding across two consecutive runs (QOL-03)
7. Flow Language Server + VSCode extension — `flow-lsp` (OmniSharp.Extensions.LanguageServer over stdio, references flow-lang only), per-platform self-contained VSIX (linux-x64, win-x64, osx-x64, osx-arm64) with bundled stdlib, semantic tokens + diagnostics + completion + hover + signature help + go-to-definition + 5 snippet templates + roman-numeral context inside note streams + `BuiltInDocs` lookup table populated to 104 entries

**Patterns established:**
- Two-pass strict authorship — Pass 1 from REQUIREMENTS, Pass 2 reality-check; format/signature drift surfaced before commit
- Charitable interpretation as load-bearing — 4 criterion-moot/reframe events (TEST-03 P12, DX-06 P14, criterion #3 P15, criterion #4 P16)
- HUMAN-UAT for non-blocking checkpoints — Phase 17 `17-HUMAN-UAT.md` (status: partial) instead of fake-passing manual verification
- "Closed (audit false positive)" first-class traceability marker — TEST-01/TEST-02 retained as audit-trail entries
- Determinism contract — synth white-noise + TPDF dither RNGs reseeded at renderSong/writeWav boundaries; byte-identical WAV + MIDI for `tutorial.flow` and `showcase.flow` two consecutive runs

**Known deferred items at close:** 4 (see STATE.md Deferred Items)
- Debug session: `function-overload-resolution-failures` (awaiting human verify)
- Quick task: pure-Flow test library (`260420-0c0...`)
- Phase 17: 3 pending HUMAN-UAT scenarios (manual-smoke rows 1-3)
- Phase 04: VERIFICATION.md gaps_found

**Forward-deferred items (DEFER-01..06):**
- DEFER-01: `range(Int, Int) → Array[Int]` stdlib registration
- DEFER-02/03: `H` note-stream-only `B` alias via pragma system + candidate `enable` keyword
- DEFER-04: Multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)
- DEFER-05: Slice negative-from-end indexing
- DEFER-06: Gaussian humanize distribution

**Archives:**
- `.planning/milestones/v1.2-ROADMAP.md`
- `.planning/milestones/v1.2-REQUIREMENTS.md`

---

## v1.1 Polish & Foundations — Shipped 2026-04-18

**Goal:** Fix critical bugs that break user scripts, improve developer experience with missing language features, then expand music production capabilities.

**Delivered:** A production-grade interpreter with honest error reporting, a usable math stdlib, comment syntax, better audio composition primitives (mix, per-section gain, three new synth timbres, tempo ramps), and a formant-based vocal synthesis pipeline with external TTS integration.

**Stats:**
- Phases: 5 (Phase 6 – Phase 10)
- Plans: 10 (all complete)
- Requirements: 16 total — 15 Complete, 1 Invalid (FIX-04 premise did not hold)
- Git range: `9f4d1cb` (v1.1 execution commit) → `v1.1` tag
- Files changed during milestone: 123
- LOC delta: +12,768 / −1,625

**Key accomplishments:**
1. `--verbose` flag with TextWriter-threaded diagnostics (module loads, failed overload resolutions) on stderr (QOL-01)
2. Overload resolution fixed for music-type widening (`transpose(seq, 2)`, `vary(seq, 0.5)` now work) (FIX-01)
3. Bare expressions captured inside sections — including deeply nested musical-context blocks after audit-driven fix (FIX-02 × AUDIO-06)
4. Error reporter no longer masks function-not-found failures; exit code 1 on errors (FIX-03)
5. `//` line comments + 17-function math stdlib (sin/cos/tan/abs/sqrt/min/max/floor/ceil/round/pow/log + pi/tau) (DX-01, DX-02)
6. `writeWav(path, buf)` path-first convention with `exportWav` alias + REPL auto-imports of @std, @audio, @collections (DX-03, DX-04)
7. `mix(Buffer, Buffer)` with mono-to-stereo promotion + per-section `gain` musical context block (AUDIO-05, AUDIO-06)
8. Three new synthesizer presets — strings (detuned saws), organ (Hammond additive), bell (Risset inharmonic partials) (AUDIO-07)
9. `tempoRamp(sequence, startBPM, endBPM) → Buffer` with bar-midpoint interpolation (AUDIO-08)
10. Formant-based vocal synthesis (`sing(phoneme, note, dur)`) + external TTS hook (`tts(text)`, `setTtsCommand(cmd)`) producing AudioBuffers that compose with mix/writeWav/play (VOC-01, VOC-02)

**Known gaps at close:**
- FIX-04 reclassified as Invalid (architectural precondition does not exist)
- `examples/tutorial.flow` does not yet demonstrate v1.1 features (deferred, tracked in next milestone planning)
- Phases 6–9 lacked individual `VERIFICATION.md` files; verified retroactively via 3-source audit (SUMMARY frontmatter + code inspection + live E2E execution)
- Nyquist validation incomplete: 4/5 phases missing `VALIDATION.md`, phase 10 has draft with `nyquist_compliant: false`

**Archives:**
- `.planning/milestones/v1.1-ROADMAP.md`
- `.planning/milestones/v1.1-REQUIREMENTS.md`
- `.planning/milestones/v1.1-MILESTONE-AUDIT.md`

---

## v1.0 MVP — Shipped 2026-04-03

The initial MVP. Delivered: lexer/parser/interpreter pipeline, static type system with music-specific types, flow operator, musical context blocks, note stream syntax, chord literals + roman numerals, section/song structure, pattern transforms, piano/brass/sax/drums synthesis, DSP effects, WAV export, PulseAudio playback, MIDI import, REPL with watch mode, module imports, standard library.

**Phases:** 1–5 (Language Foundations, Audio Pipeline, Synthesis & MIDI Export, Composition Tools, Live Coding)

*(v1.0 was shipped before milestone-completion tooling existed; details are preserved in `.planning/ROADMAP.md` under the v1.0 MVP collapsed section.)*
