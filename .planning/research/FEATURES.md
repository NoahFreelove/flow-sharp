# Feature Research — Flow v1.5 "Stage, Studio, Web"

**Domain:** Music-production language (interpreted DSL, real-time + render + notation export)
**Researched:** 2026-05-18
**Confidence:** HIGH on language-ecosystem comparisons (Sonic Pi / SuperCollider / TidalCycles / Strudel are well-documented), MEDIUM on workflow nuances (live-coding UX), MEDIUM-LOW on ML-continuation since the "good-enough baseline" depends on Flow's footprint commitment.

---

## Reading-The-Room Note

Flow already ships a huge surface — 9 synths, sampled instruments + SFZ, full DSP rack, 5 articulations + sforzando + portamento, tuplets, microtonal tuning, multi-track MIDI export, MIDI import, LSP, WAV export, REPL+watch, lambdas, symbols, tuples, generic dicts, custom oscillators, vocal synthesis. v1.5 is a **citizenship + reach** milestone, not a feature-from-scratch milestone. The feature framing below reflects that: most v1.5 items are table stakes *for the category of language Flow is now joining* (Tidal / Sonic Pi / SuperCollider tier), not for music software generally. The split below treats "table stakes" as "expected by a composer who comes to Flow from one of those peer tools."

---

## Feature Landscape

### Table Stakes (Users Expect These at v1.5)

Features a peer-tool composer (Tidal / Sonic Pi / Strudel / SuperCollider) will assume Flow has if Flow is presenting itself as a serious music language.

| Feature (v1.5 phase) | Why Expected | Complexity | Notes |
|----------------------|--------------|------------|-------|
| **Pattern matching** (Phase 35) | Modern statically-typed language baseline — Rust, Swift, Haskell, OCaml, Scala all have it; F# inherits from ML; even Python 3.10+ has structural match. With Flow's Symbol + Tuple types already shipping, no pattern destructuring is the conspicuous gap. | M | Fits S-expression style: `(match x ((Cmaj7) ...) ((Dm7) ...))`. Music-aware extractors are the differentiator (see below); raw constructor + literal + wildcard match is the table-stakes core. |
| **Rust-style multi-line diagnostics** (Phase 35) | Composers writing dense note-stream syntax need precise error spans. Flow already has `--verbose` (v1.1) but error format is single-line. rustc, swiftc, elm all set this bar; ScalaC moved to multi-line in 2020+. | M | Span data already in lexer (column/line tracked). Need: source-line echo with caret + label, suggestion lines, secondary spans. Annotate-snippets-style renderer is ~600 LOC. |
| **Pure-Flow test framework** (Phase 35) | Carryover from v1.2 deferred quick task (`260420-0c0`). Every language ecosystem has a test idiom — SRFI-64 for Scheme, `#[test]` for Rust, pytest for Python. Flow tests currently are `.flow` scripts checked by no-error exit code; that's brittle. | S | `(test "name" body)` macro + `(assertEq a b)` / `(assertNotesMatch seqA seqB)` / `(assertWithinDb buf1 buf2 0.5dB)` builtins. The dB one is novel and load-bearing — see Phase 28 RMS-windowed regression precedent. |
| **MusicXML export** (Phase 39) | Universal score interchange — MuseScore, Finale, Sibelius, Dorico, LilyPond all consume it. Without MusicXML, Flow is a render+playback tool; with it, Flow is a composer's tool that talks to notation software. | L | Min viable subset: `<part>` / `<measure>` / `<note>` (with `<pitch>` step+octave+alter for cents/microtones rounded), `<duration>` / `<divisions>` (Flow's Fraction maps cleanly), `<key>`, `<time>`, `<direction>` for tempo, `<articulations>` for staccato/accent/tenuto/marcato. Round-trip with MuseScore is the canonical bar. Phase 28's 5 articulations all have direct MusicXML mappings; sforzando maps to `<accent>` + dynamics. |
| **LilyPond export** (Phase 39) | Engraving-quality printed scores are the academic / classical composer's expected output. LilyPond is the open-source standard. | M | Shorthand articulations are dead simple: `-.` staccato, `->` accent, `--` tenuto, `-^` marcato. Slurs `( )`, ties `~`. Microtones: LilyPond has `\quartersharp` / `\quartertone` but no general cents — for arbitrary Scala tuning, emit cents in a comment + closest 12-TET pitch; flag as known limitation. |
| **ABC import** (Phase 39) | Folk / Irish trad / English / Scottish session music lives in ABC. Flow's existing chord literals + roman numerals + key context map almost 1:1. Common-subset is sufficient — full ABC 2.1 is 100+ pages but the working subset (header K:/M:/L:/Q:, notes a-g/A-G, accidentals `^_=`, durations `2 /2`, bar lines `|`, repeats `:|:`, chord symbols `"Cm"`) is ~30 productions. | M | Use abc2midi conventions (Maj/Min/Dor/Mix/etc.) for mode keys — these are the de facto standard outside the strict spec. |
| **Real-time MIDI output** (Phase 40) | Long-running v1.0 deferral. Composers want to drive hardware synths + DAW VST tracks live from Flow. MIDI output is the universal "talk to the outside world" hook for music software. Csound, SuperCollider, Sonic Pi, ChucK, Pd all have it. | M | New `IMidiBackend` mirroring v1.0 `IAudioBackend`. Linux: ALSA seq + JACK MIDI; Mac: CoreMIDI; Win: WinMM. Csound uses PortMIDI as cross-platform fallback — viable for Flow too. API surface: `(midiOut device note vel dur)`, `(midiCc device cc value)`, `(midiOutDevices)`, `(setMidiOutDevice name)`. |
| **MIDI clock sync** (Phase 40) | Bare-minimum sync to drum machines, hardware sequencers, modular Eurorack with MIDI input. Master mode (Flow generates 24 PPQN ticks) is required; slave mode (Flow follows incoming clock) is the "real composer setup" tier — both should ship. | M | Tied to MIDI output backend. Clock = 24 PPQN; tempo discovery from clock tick interval; Start / Stop / Continue + Song Position Pointer messages. Tempo jitter smoothing is the gotcha (rolling-average tick interval). |
| **Cross-platform binaries** (Phase 41) | Carryover from v1.4's deferred items. v1.4 shipped Linux x64 only — Mac + Windows users currently can't run Flow. With WASM playground (Phase 41) bringing curious users, "how do I install locally" becomes the immediate next click. | M-L | .NET 10 self-contained publish for `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64`. Audio backend hooks: WASAPI on Windows, CoreAudio on Mac — write thin `IAudioBackend` impls each ~200 LOC. PulseAudio backend keeps working on Linux. Don't try to ship a single fat binary — per-platform tarballs/zips with install scripts. |
| **`flow doc` documentation generator** (Phase 41) | Every modern language has one: rustdoc, cargo doc, godoc, JuliaDoc, even Sonic Pi has its built-in help browser. Flow has ~200 builtins, 9 synths, multiple stdlib modules — without browsable docs the surface is unlearnable. | M | Reuse `BuiltInDocs` already populated to 104 entries for the LSP (v1.2 Phase 17). Emit static HTML site: function index + per-module pages + searchable JS index (same shape as rustdoc). Runnable examples are the differentiator (see below). |
| **WASM playground** (Phase 41) | Strudel.cc set this bar for live-coding languages — zero install, share-via-URL, classroom-friendly. With v1.5's `live { ... }` block shipping in Phase 38, the playground IS the live-coding demo. | L | Two paths: (a) compile .NET 10 → WASM via AOT (`PublishWasm`), audio out via Web Audio API bridge; (b) re-implement the interpreter loop in JS/TS targeting a Strudel-style scheduler. Path (a) is cleaner architecturally but heavier payload (~10-15MB); path (b) is lighter but a fork. Recommendation: (a), accept the payload cost since this is a one-time download. |

### Differentiators (Where v1.5 Goes Beyond the Peer Tools)

Features that, if executed well, make Flow distinct from Tidal/Strudel/Sonic Pi/SuperCollider — not just "another music language" but worth choosing.

| Feature (v1.5 phase) | Value Proposition | Complexity | Notes |
|----------------------|-------------------|------------|-------|
| **Tidal-style pattern algebra grafted onto a STATICALLY-TYPED language** (Phase 36) | Tidal/Strudel are Haskell/JS — dynamically typed. Sonic Pi is Ruby-flavored. SuperCollider is Smalltalk-flavored. No statically-typed language has this. Flow gets type-checked `(every 4 (fast 2) seq)` — composer benefits: hover types in LSP, completion of combinator chains, fewer crashes mid-set. | L | Core 12: `fast` `slow` `rev` `every` `sometimes` `chunk` `jux` `iter` `palindrome` `striate` `degrade` `cat`. The full Tidal vocab is ~80 functions — start with the dozen that show up in 90% of `.tidal` files (per the Tidal workshop corpus). Defer `linger`, `swingBy`, `stutter`, etc. to v1.6. |
| **Music-aware pattern-matching extractors** (Phase 35) | Rust/Haskell pattern matching on language ADTs is generic — Flow can match on **musical structure**: `(match c ((Cmaj7) ...) ((Dm7) ...))`, `(match deg ((I) ...) ((V7) ...))`, `(match n ((Note p) when (= (pitchClass p) 0) ...))`. The music types are first-class, so the match arms can be too. | M | Chord-quality extraction is easy (Flow already parses Cmaj7 → ChordData with quality field — a match arm just compares ChordData). Roman numeral match needs the active key context — be charitable: `((I) ...)` matches whatever I resolves to in the current key block. |
| **`-> as name` flow chain naming** (Phase 35) | Flow's `->` operator is already a parse-time transform. `seq -> transpose(2) as t1 -> reverb(decay)` lets the composer name intermediate values mid-chain without breaking the chain. Single feature that no peer tool has. | S | Pure parser sugar — `-> as <name>` parses as both "bind the LHS-piped result to `<name>` in scope" AND "continue passing the same value down the chain." No runtime change. |
| **Granular synthesis as a NATIVE-CITIZEN type** (Phase 37) | SuperCollider has `GrainBuf`, Csound has `partikkel`, Max has `munger~` — all are oscillator-tier opcodes that the composer manually wires. Flow can expose `granular(buffer, grainDur=50ms, density=20Hz, jitter=0.2, position=0.5, pitchJitter=+50c, window=#hann)` as a one-liner that returns a Buffer composable with reverb/gain/pan. | M-L | Parameter surface (locked in plan): `grainSize: Ms` (1-1000), `density: Hertz` (1-1000), `position: Double` (0.0-1.0 source-buffer phase), `positionJitter: Double` (0.0-1.0), `pitchJitter: Cent` or `Double`, `window: Symbol` (`#hann`, `#gaussian`, `#tukey`, default `#hann`). Hann is the table-stakes window; Gaussian matches Csound's partikkel; Tukey is the differentiator (variable-tapered, good for percussion). |
| **Time-stretch + pitch-shift as Hybrid HPS (Harmonic/Percussive Separation)** (Phase 37) | Most music languages ship one algorithm (phase vocoder, period). Material-aware switching — phase vocoder for harmonic content, OLA/SOLA for percussive content — is the modern bar (Surina 2008, PVSOLA papers). Flow can ship `stretch(buf, factor, mode=#auto\|#harmonic\|#percussive)` with `#auto` doing HPS internally. | L | Parameter surface: `stretchFactor: Double` (0.25-4.0 reasonable), `pitchRatio: Double` or `Cent`/`Semitone` (independent of stretch — that's the point), `formantPreserve: Bool` (vocoder mode only). Default `#auto` picks based on spectral flux / transient density. Hand-rolled phase vocoder is ~400 LOC; OLA is ~80 LOC; HPS detector is ~150 LOC. Total feasible. |
| **Stereo pan as a per-instrument default** (Phase 37) | Sonic Pi has `pan: -1..1` per `play`; SuperCollider has `Pan2.ar`. Flow currently has voice-block + spatial-audio in v1.0 Phase 2 (per CLAUDE.md note: "spatial audio / per-voice panning — v1.0 Phase 2") but per the v1.4 forward-deferred list it appears the SFZ stereo retrofit is the missing piece. | S-M | Constant-power law (`L = cos(π/4 · (1+pan)), R = sin(π/4 · (1+pan))`) is the textbook default — Sonic Pi uses this, SuperCollider's `Pan2.ar` uses this. Linear is the wrong default (-3dB center dip). Confirm Phase 2 implementation; if constant-power already, the deferred work is SFZ-renderer stereo (currently mono per SfzRenderer). Expose `(pan voice -0.5)`, `pan` as section-level keyword, OR as a per-track parameter. |
| **Sampler-polish bundle as a curated set** (Phase 37) | Not one feature — six small ones. SFZ round-robin (`seq_position`/`seq_length`), velocity layers with crossfade (`xfin_*` opcodes), per-articulation envelope multipliers for the sampled path (Phase 29 follow-up), more flute samples to close the D5 timbre crossover, sampled drums with transient-preserving pitch shift, warmer piano timbre + VSCO velocity-layer expansion (ragtime UAT iteration #2 follow-up). | M total | Each item is S or low-M individually. SFZ round-robin: add `seq_position` / `seq_length` to the existing 13-opcode whitelist, hold a counter in the SfzRenderer voice state. Velocity crossfade: `xfin_lovel` / `xfin_hivel` / `xfout_*` opcodes, equal-power crossfade between adjacent velocity regions instead of hard-switching. |
| **Parameterized sections** (Phase 36) | SuperCollider has `Pdef`/`Pbind` for this; Sonic Pi has `define`. Flow's `section name { ... }` is currently parameterless. Adding `section verse(pitchOffset, intensity) { ... }` + `[verse(0, 0.8) verse(2, 1.0) chorus]` syntax lets composers write phrase templates once and reuse them across keys / dynamics / variations. | M | Scope rule: section args + closure over outer musical context (tempo / key / timesig / voicePool / tuning visible inside). This is the natural fit with Flow's ExecutionContext stack. Pitfall: `[verse(0) verse(2)]` already parses as `[verse 0 verse 2]` — Song expression syntax conflict. Resolution: parens-required-when-args form, allowing existing `[verse chorus]` to keep working. |
| **Improvisation API (jam over chord progression)** (Phase 36) | Magenta MusicVAE / MusicTransformer / MuseNet need 100MB+ models + Python/TF stack. ChucK / Sonic Pi / Tidal don't have one at all — composers roll their own Markov chains. Flow can ship a "good enough baseline" — chord-aware Markov over a corpus, second-order n-gram, scale-degree-keyed transition table. Zero-dependency, runs in the interpreter, deterministic-seedable. | M-L | API: `(improvise overChord melodyCorpus length=8 seed=42 order=2)`. Corpus is a `Sequence` (or array of them). Output respects chord tones on strong beats + scale tones on weak beats. NOT trying to compete with Transformer-quality; trying to be the "Sonic Pi has Markov chain examples in the forum" tier — but built-in. Anti-feature: shipping a transformer model — see anti-features below. |
| **Generative primitives as first-class** (Phase 36) | Markov / L-system / cellular / Lorenz are scattered across forums in every music language — none ship them as core. Flow ships `(markov order corpus seed)`, `(lsystem axiom rules iterations)`, `(cellular rule initialState steps)`, `(lorenz sigma rho beta steps)` as standard library. Composer writes 1 line, gets a Sequence. | M | Cellular rules to expose: rule 30 (chaotic), rule 90 (Sierpinski self-similar), rule 110 (class-4 complex), rule 184 (traffic-flow), Conway's 2D Life as the special case. Lorenz default params: σ=10, ρ=28, β=8/3 (canonical butterfly); useful ranges σ∈[6,35] ρ∈[20,50] β∈[2,6] per Cherry Audio's musical implementation. L-system needs alphabet→note mapping convention — recommend `'F'→note, '+'→up step, '-'→down step, '['→push state, ']'→pop` (turtle-graphics convention). |
| **Multi-line diagnostics with "did you mean" for note streams** (Phase 35) | Generic Rust-style diagnostics are table stakes. Music-aware diagnostics are the differentiator: "key Cmaj does not contain `F#4`; did you mean `F4` (in Cmaj scale) or did you mean `key Gmaj`?" Scale lint already exists (v1.3 Phase 24) — this is its multi-line cousin. | M | Build on v1.3 scale-lint infrastructure. Threading note-stream parser spans through to the multi-line renderer is the work. |
| **OSC server + client** (Phase 38) | TidalCycles speaks to SuperDirt over OSC; Sonic Pi has built-in OSC server (`/osc-send`); Strudel has OSC bridge. Flow joins the network by speaking OSC — gets it instant interop with TouchOSC controllers, hardware running OSC, Pure Data, Max, MaxMSP. | S-M | OSC 1.0 spec is small (~30 pages). Address pattern + type tag string + arguments. Existing C# library: `Rug.Osc` or hand-roll (~300 LOC for spec-1.0 messages + bundles). API: `(oscListen "/path" handler)`, `(oscSend host port "/path" args...)`, `(oscBundle msgs)`. |
| **`live { ... }` block + modernized watch mode** (Phase 38) | Sonic Pi's `live_loop` is the prior art — define a loop, redefine it on save, the loop swaps at the next cycle boundary. Flow's existing watch mode (v1.0) re-runs the whole script — not cue-quantized. `live { ... }` block + cue-quantized hot-swap is the modern bar (also prereq for Phase 41 WASM live coding). | M | Hot-swap at the next bar boundary (using active timesig). State preservation across swaps: anything inside `live { }` is treated as code-swappable; anything outside (e.g. instrument loads, sample buffer reads) is preserved. ANSI status panel in the terminal (per `flok`-style live coding UIs) — table-stakes for the modern bar. |
| **REPL polish: LSP-backed completion + `?fn` help + piano-roll preview** (Phase 38) | Julia REPL, IPython, Lisp SLIME set the bar. Flow REPL currently has watch mode but no completion. Reuse the v1.2 LSP — REPL calls into `flow-lsp` for completion + hover + signature help. `?functionName` prints a doc card from `BuiltInDocs`. Piano-roll preview is the music-specific differentiator — `(preview seq)` prints ASCII piano roll to terminal (v1.0 Phase 1 had this — extend to also show on REPL value-display for Sequence values). | M | Multiline editing via `System.Console` API or `ReadLine.NET` library. History search (Ctrl-R) is table-stakes. Tab completion calls LSP. |
| **Ableton Link sync** (Phase 40) | Modern composer DAW workflows: Live + Bitwig + Maschine all support Link for tempo+phase sync over LAN. Tidal supports Link; Sonic Pi supports Link. Flow joining is the "you can play with friends" hook — non-trivial protocol but the Ableton SDK is open-source (`ableton/link` on GitHub, C++/header-only). | M-L | C++ interop from .NET via P/Invoke (the Link library is C++, not C). Need a thin C-API shim. UDP multicast on port 20808 (Link's default). Tempo discovery + beat phase + start/stop sync. |
| **JACK transport sync** (Phase 40) | Linux pro-audio composer workflow. JACK is the de facto pro Linux audio. Ardour, Hydrogen, Carla, Rosegarden all speak JACK transport. With Linux being Flow's primary platform, JACK transport sync is the "I'm a serious Linux musician" tier. | M | `libjack` P/Invoke. Different model from Link — sample-frame-accurate, master/slave. Strictly opt-in (don't break PulseAudio-only users). |

### Anti-Features (Commonly Requested or Tempting, but Wrong for Flow)

| Anti-Feature | Why Requested | Why Problematic | Alternative |
|--------------|---------------|-----------------|-------------|
| **Ship a Transformer / VAE model for improvisation** | "Modern music languages should use ML" / "Magenta exists, just bundle it" | (1) Model size: MusicTransformer is 100s of MB, blows Flow's ~40MB binary; (2) Python+TF runtime dependency contradicts CLAUDE.md "minimal dependencies"; (3) Determinism contract breaks (v1.2 Phase 17 byte-identical-two-runs); (4) Non-musical use of GPU/CUDA stack outside Flow's scope. | Markov + L-system + chord-aware Markov over user-supplied corpus. "Good enough baseline" tier. Document a recipe for piping out to external Magenta if composer wants — but Flow stays zero-dep. |
| **Full MusicXML round-trip (import + export with 100% fidelity)** | "We export, we should also import" | Import requires parsing every MusicXML feature MuseScore emits — including engraving directions, beam groups, slur positioning, page layout, lyrics, fingering. ~10× the work of export. Not the v1.5 trade-off. | Export only, leave import to v1.6+ if demand emerges. Alternative: import only via the existing MIDI import path (MusicXML → MIDI via external tool → Flow). |
| **Real-time AUDIO output to external software via JACK + USB + ASIO** | "If we have MIDI out, we should have audio out" | Audio routing is a different beast — JACK clients, virtual cables, ASIO drivers. Each platform is its own integration story. Flow already has WAV export + PulseAudio playback. Real-time MULTI-CHANNEL routing to a DAW for further processing is a v2.0 conversation. | WAV export → drag into DAW. MIDI out → DAW renders MIDI through its own VSTs. These two cover 95% of "use Flow with my DAW" workflows. |
| **Browser-based collaborative editing (Strudel / Flok style)** | "Strudel has it, the WASM playground should too" | Multi-user CRDT collaboration over WebRTC is a 6-month project alone. Flow's WASM playground (Phase 41) needs to ship as one composer + share-via-URL. Multi-user real-time editing is a v2.0+ conversation. | Single-composer playground with share-via-URL (the Strudel.cc baseline, not the Flok extension). |
| **Hot-reload by destroying and reconstructing the interpreter** | "Just re-run the script on file save" | Current watch mode does exactly this and it's audibly bad mid-set — playback stops, voices re-initialize. The `live { ... }` block exists specifically to NOT do this. | Cue-quantized swap at bar boundary, preserve state outside `live { }` block. (This IS Phase 38's design.) |
| **VST/AU plugin hosting** | "Real music software hosts plugins" | Already declared Out of Scope in PROJECT.md. Plugin SDKs are platform-specific, license-encumbered (Steinberg VST3 SDK), and the host-side state machine is a year-long project. | MIDI out + audio recording from Flow's WAV export — let the DAW be the plugin host. |
| **Full ABC 2.1 import (every feature)** | "Be standards-compliant" | ABC 2.1 has decoration shortcuts, lyric alignment, voice multiplexing within one stave, custom user macros — features that don't map cleanly to Flow's note-stream + chord-literal model. Trying to be 100% compliant means special-casing a long tail of folk-music edge cases. | Common-subset import (90% of `thesession.org` files parseable) + clear "unsupported feature: X at line N" diagnostics. |
| **Generic multi-tongue MML parser** | "Support every chiptune dialect" | PMD vs NRTDRV vs MUCOM88 vs MML.NET vs PPMCK — each is a different dialect. Generic parser is a parser-combinator framework. | Target one dialect for the MVP: PPMCK/MCK (NES-targeted, modern chiptune community, most common per VGMPF wiki). Document as "PPMCK-compatible MML"; defer other dialects. |
| **MIDI clock slave mode that auto-adjusts every project tempo** | "Auto-follow the master clock everywhere" | Surprising behavior — composer hits Play in Flow, tempo jumps because some random MIDI device is sending clock. Better: explicit opt-in per script. | `enable midiClockSlave;` pragma or `(midiClockFollow inputDevice)` builtin to opt in. |
| **`watch` mode that auto-renders WAV on every save** | "Show me the audio output continuously" | I/O thrashing. Renders are slow for long songs (multiple seconds → file write → invalidate page cache). Plus disk churn. | Existing watch mode plays via PulseAudio; for WAV export use explicit `(writeWav ...)`. Phase 38's `live { ... }` block is the streaming-render path. |
| **Type inference for all `var` declarations** | "Modern languages have it" | Long-deferred per PROJECT.md. Conflicts with Flow's explicit-types philosophy + overload resolution (specificity scoring depends on annotated types). Probably a v2.0 conversation with a new language flag, not a v1.5 phase. | Keep explicit. Pattern matching reduces the surface where verbose types hurt — that's the v1.5 ergonomic improvement. |
| **User-defined types / structs** | "Modern languages have it" | Same answer as type inference — long-deferred. Music types are first-class — Note, Chord, Sequence — and the v1.3 Symbol + Tuple + Dict bundle covers most "I need a record" cases via tuples + dicts. User-defined records is a v2.0 conversation. | Tuples + dicts cover 90%. If a composer truly needs `record Chord {root, quality, ...}`, the dict path is `(dict #root "C" #quality "maj7" #extensions <<#9, #13>>)`. |
| **Per-articulation envelope multipliers controlled by GLOBAL CONFIG** | "Let the composer tune all articulations site-wide" | Surprising action-at-a-distance. A composer who tunes their staccato 30% sharper globally breaks every example script and showcase piece. | Per-phase-37 plan: per-instrument articulation envelope multipliers for the SAMPLED path are calibrated against the synth path's locked Phase 28 rules. No global knob exposed to composer — just sensible defaults the SAMPLED renderer respects. |

---

## Feature Dependencies

```
Phase 35 (Language Foundation)
├── Pattern matching ──> required by Phase 36 (parameterized sections destructure args)
├── Rust-style diagnostics ──> required by Phase 35 test framework (assertion failures need good output)
├── -> as name ──> independent
└── Pure-Flow test framework ──> required by every other phase (regression coverage of new features)
                                          │
                                          ▼
Phase 36 (Sequence Algebra & Generative)
├── Tidal-style pattern algebra ──> independent (just new functions over Sequence)
├── Generative primitives ──> independent
├── Parameterized sections ──> requires pattern matching for destructuring section args
└── Improvisation API ──> requires (a) chord progression context — already exists, (b) Markov primitive from same phase
                                          │
                                          ▼
Phase 37 (Sound Design + Sampler Polish)
├── Granular synthesis ──> independent (new builtin returning Buffer)
├── Time-stretch + pitch-shift ──> independent (new builtin over Buffer)
├── Stereo pan across instruments ──> verify Phase 2 status; SFZ retrofit blocked on Phase 33's SfzRenderer (already shipped)
└── Sampler polish bundle (6 sub-items) ──> independent of v1.5 phases, depends on shipped Phase 29 + Phase 33
                                          │
                                          ▼
Phase 38 (Live Coding 2.0)
├── live { ... } block ──> required by Phase 41 WASM playground (the user-facing live-coding entry point)
├── Modernized watch mode ──> required by Phase 41 WASM (browser equivalent)
├── REPL polish ──> requires LSP from v1.2 Phase 17 (shipped), depends on BuiltInDocs (shipped)
├── Audio input ──> independent (new IAudioBackend input method)
└── OSC server/client ──> independent (new builtin family)
                                          │
                                          ▼
Phase 39 (Notation Citizenship)
├── MusicXML export ──> independent
├── LilyPond export ──> independent (could share an internal "musical notation IR" with MusicXML — see ARCHITECTURE note)
├── ABC import ──> independent (new parser, outputs Flow source like midi2flow does)
└── MML import ──> independent (new parser)
                                          │
                                          ▼
Phase 40 (Studio Sync)
├── Real-time MIDI output ──> required by MIDI clock (clock is a MIDI message)
├── MIDI clock ──> requires real-time MIDI output
├── Ableton Link ──> independent of MIDI but co-located thematically
└── JACK transport ──> independent of MIDI but Linux-specific
                                          │
                                          ▼
Phase 41 (Reach + Closer)
├── WASM playground ──> requires (a) live { } block from Phase 38, (b) modernized watch mode from Phase 38
├── Cross-platform binaries ──> requires WASAPI + CoreAudio backends — new IAudioBackend implementations
├── flow doc generator ──> requires BuiltInDocs (shipped) + Pure-Flow test framework (runnable examples)
├── JetBrains Marketplace publish ──> requires v1.4 Phase 31 scaffolding (shipped)
└── Third-genre showcase ──> requires features from Phases 35-40 (the showcase IS the v1.5 closer that uses them)
```

### Key Dependency Notes

- **Phase 35 is the bottom of the dependency tree.** Pattern matching unblocks parameterized sections (Phase 36) by giving the destructuring syntax; test framework unblocks regression coverage of every subsequent phase; diagnostics improve developer experience everywhere.
- **Phase 38's `live { ... }` block is the linchpin for Phase 41.** WASM playground without `live { ... }` is just "run a script in the browser" — uninteresting. With `live { ... }`, it's a Strudel-tier live-coding-in-browser experience.
- **Phase 40's MIDI output is the linchpin within its own phase.** Clock + Link + JACK transport are sync protocols; MIDI output is the message transport. Build MIDI output first, then layer clock, Link, JACK on top.
- **Phase 41 cross-platform binaries depend on new audio backends** (WASAPI on Windows, CoreAudio on Mac). These are new C# code under the `IAudioBackend` abstraction (already proven via PulseAudio + v1.4 setting precedent).
- **MusicXML and LilyPond export can share a "score IR"** — both consume the same Section/Song/Sequence tree and emit different textual formats. Resist the temptation to write each from scratch; a small intermediate representation (notes + tied durations + accidentals + bar lines + articulation attachments) makes both emitters ~200 LOC each.
- **Phase 36's improvisation API depends on Markov from the same phase.** Sequence the plans within Phase 36: Markov primitive first, improvisation API second.
- **Phase 37's stereo pan needs a Phase 2 status audit before plan starts.** PROJECT.md says "Spatial audio / per-voice panning — v1.0 Phase 2" is shipped; v1.4 forward-deferred list says "Stereo panning across instruments" is open. Audit which case is true (probably: per-voice panning exists but the SfzRenderer is mono-only).

---

## MVP Definition for v1.5

The milestone is **already scoped** by PROJECT.md / .planning/MILESTONES.md as 7 phases (35-41). The MVP question for v1.5 is "within each phase, what's MUST vs NICE?"

### Phase 35 — MUST ship

- Pattern matching: literal + constructor + wildcard + guard. Music-aware extractors for Chord-quality and Note-pitch are differentiator MUST.
- Rust-style multi-line diagnostics: source-line echo, caret, label, secondary spans. "Did you mean" can defer to a follow-up.
- Pure-Flow test framework: `(test "name" body)` + 4 core assert primitives (`assertEq`, `assertNotesMatch`, `assertBytesEqual`, `assertWithinDb`). Fixture model can defer.
- `-> as name`: pure parser sugar, S-complexity. Ship it.

### Phase 35 — NICE-to-have (defer to v1.5.x or v1.6)

- Compile-time exhaustiveness checking for pattern matching (Rust does this; Flow can ship runtime-only and add static checking later).
- "Did you mean" lint suggestions for misspelled function names (separate effort from multi-line span renderer).
- Fixtures / setup-teardown in the test framework (each test can be self-contained; fixtures are convenience).

### Phase 36 — MUST ship

- Tidal-style pattern algebra: the 12 core combinators (fast / slow / rev / every / sometimes / chunk / jux / iter / palindrome / striate / degrade / cat). These cover 90% of `.tidal` files.
- Generative primitives: Markov (order 1-3), L-system (turtle-graphics convention), cellular (rules 30, 90, 110, 184 + 2D Life), Lorenz (sigma/rho/beta).
- Parameterized sections with paren-required-when-args syntax.
- Improvisation API: chord-aware Markov over user corpus, scale-degree transition table, deterministic-seedable.

### Phase 36 — NICE

- The other ~60 Tidal combinators (linger / swingBy / stutter / range / segment / etc.) — pick another dozen for v1.6 based on real composer feedback.
- 2D L-systems with branching (`[`/`]` push/pop), parametric L-systems with arguments.
- Higher-order Markov chains (variable-order, smoothed). Probably overkill for v1.5.

### Phase 37 — MUST ship

- Granular synthesis with Hann + Gaussian + Tukey windows.
- Time-stretch + pitch-shift with `#auto` (HPS-based) + `#harmonic` (phase vocoder) + `#percussive` (OLA) modes.
- Stereo pan audit + SFZ-renderer stereo retrofit.
- Sampler polish bundle: SFZ round-robin, velocity layer crossfade, per-articulation envelope multipliers (sampled path), warmer piano timbre, more flute samples, sampled drums with transient-preserving pitch shift.

### Phase 37 — NICE

- Formant-preserving pitch shift (vocal-friendly). Already shipped in vocal synthesis (v1.1 Phase 10); confirm if applies to general buffer pitch shift.
- Sample-and-hold / oscillator-rate granular position modulation.

### Phase 38 — MUST ship

- `live { ... }` block with cue-quantized hot-swap at bar boundary.
- Modernized watch mode with ANSI status panel.
- REPL polish: LSP-backed completion, `?fn` help, multiline editing, history search.
- Piano-roll preview for Sequence values in REPL.
- OSC server + client (1.0 spec, common-subset address-pattern + types + bundles).
- Audio input: `(liveAudio device)` → Buffer-streaming source compatible with DSP chain.

### Phase 38 — NICE

- OSC 1.1 wildcard address patterns, time-tagged bundle scheduling. Defer.
- Multi-line REPL paste with auto-detection of statement boundaries. Defer.

### Phase 39 — MUST ship

- MusicXML export: notes + durations + key + timesig + tempo + Phase 28 articulations + multi-part.
- LilyPond export: same scope, LilyPond shorthand articulations, microtonal as cents-comment + nearest 12-TET.
- ABC import: common-subset (header, notes, accidentals, durations, bars, repeats, chord symbols, abc2midi mode keys).
- MML import: PPMCK/MCK dialect (NES chiptune).

### Phase 39 — NICE

- MusicXML lyrics export.
- LilyPond engraving directives (manual stem direction, beam grouping).
- ABC voice multiplexing within a stave. Defer.

### Phase 40 — MUST ship

- Real-time MIDI output via IMidiBackend (Linux: ALSA seq + JACK MIDI fallback; Mac: CoreMIDI; Win: WinMM). Cross-platform via PortMIDI is acceptable fallback.
- MIDI clock output (master mode, 24 PPQN, Start/Stop/Continue, Song Position Pointer).
- MIDI clock input (slave mode, opt-in via pragma or builtin).
- Ableton Link master + slave (UDP multicast, tempo + beat phase).
- JACK transport master + slave (Linux only).

### Phase 40 — NICE

- MIDI sysex output (specific composer use cases — likely defer to v1.6).
- MTC (MIDI Time Code) sync. Different protocol, niche use cases.

### Phase 41 — MUST ship

- WASM playground at flow-lang.example.dev (or similar) with editor + audio + share-via-URL.
- Cross-platform binaries: linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64.
- `flow doc` generator: static HTML with function index + per-module pages + searchable JS index + runnable examples (via the test framework's harness).
- JetBrains Marketplace publish (Phase 31 scaffolding → published plugin).
- Third-genre showcase: jazz / EDM / death metal (composer choice; per CLAUDE.md genre-agnostic memory).

### Phase 41 — NICE

- WASM playground examples library (curated bookmarks from the docs site).
- Cross-platform install scripts for each OS — convenience, not strictly required.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Notes |
|---------|------------|---------------------|----------|-------|
| Pattern matching (35) | HIGH | MEDIUM | P1 | Unblocks Phase 36 parameterized sections |
| Rust-style diagnostics (35) | HIGH | MEDIUM | P1 | Quality-of-life floor for the milestone |
| Pure-Flow test framework (35) | HIGH | LOW | P1 | Regression coverage for every subsequent phase |
| `-> as name` (35) | MEDIUM | LOW | P2 | Pleasant but not load-bearing |
| Tidal pattern algebra (36) | HIGH | MEDIUM-HIGH | P1 | Core Phase 36 deliverable |
| Generative primitives (36) | HIGH | MEDIUM | P1 | Core Phase 36 deliverable |
| Parameterized sections (36) | HIGH | MEDIUM | P1 | Composer-facing power tool |
| Improvisation API (36) | MEDIUM | MEDIUM-HIGH | P2 | Useful, but Markov is composable from primitives |
| Granular synthesis (37) | HIGH | MEDIUM-HIGH | P1 | Class of sound not currently reachable in Flow |
| Time-stretch + pitch-shift (37) | HIGH | HIGH | P1 | Load-bearing for sample-based composition |
| Stereo pan polish (37) | MEDIUM | LOW-MEDIUM | P1 | v1.4 forward-deferred, must close |
| Sampler polish bundle (37) | MEDIUM | MEDIUM total | P1 | v1.4 forward-deferred list, must close |
| live { } block (38) | HIGH | MEDIUM | P1 | Prereq for Phase 41 WASM |
| Modernized watch mode (38) | HIGH | MEDIUM | P1 | Prereq for Phase 41 WASM |
| REPL polish (38) | MEDIUM | MEDIUM | P1 | Daily-use composer ergonomics |
| Audio input (38) | MEDIUM | MEDIUM | P2 | Niche but cheap given existing IAudioBackend |
| OSC server/client (38) | MEDIUM | MEDIUM | P1 | Interop hook — unlocks third-party tooling |
| MusicXML export (39) | HIGH | MEDIUM-HIGH | P1 | Notation-citizenship hook |
| LilyPond export (39) | MEDIUM | MEDIUM | P1 | Score engraving for classical composers |
| ABC import (39) | MEDIUM | MEDIUM | P2 | Folk-music corpus accessibility |
| MML import (39) | LOW-MEDIUM | MEDIUM | P3 | Niche chiptune community; nice but defer to v1.6 IF scope cuts needed |
| Real-time MIDI output (40) | HIGH | MEDIUM | P1 | Long-deferred from v1.0 |
| MIDI clock sync (40) | MEDIUM | MEDIUM | P1 | Bundled with MIDI output |
| Ableton Link (40) | HIGH | MEDIUM-HIGH | P1 | Modern composer expectation |
| JACK transport (40) | MEDIUM | MEDIUM | P2 | Linux-specific; useful but niche |
| WASM playground (41) | HIGH | HIGH | P1 | Milestone reach goal |
| Cross-platform binaries (41) | HIGH | MEDIUM | P1 | Long-deferred from v1.0 |
| flow doc generator (41) | HIGH | MEDIUM | P1 | Surface is unlearnable without it |
| JetBrains publish (41) | MEDIUM | LOW | P2 | Scaffolding already done, publishing is mechanical |
| Third-genre showcase (41) | HIGH | MEDIUM | P1 | Milestone closer + genre-agnostic claim validation |

**Priority key:**
- P1: MUST ship in v1.5 (defines the milestone)
- P2: SHOULD ship if phase has bandwidth (defers cleanly to v1.5.x or v1.6)
- P3: NICE; defer if scope cuts needed

---

## Competitor Feature Analysis

| Feature | TidalCycles / Strudel | Sonic Pi | SuperCollider | Flow v1.5 Approach |
|---------|-----------------------|----------|---------------|--------------------|
| Pattern algebra | First-class (the whole point) | Limited (`tick`/`look`/`ring`) | Patterns library (Pbind/Pseq/etc.) | Tidal-style core 12 combinators on top of static types |
| Generative (Markov etc.) | Community libs / per-set code | Forum recipes | Pwrand / Pmarkov classes | First-class builtins for Markov / L-system / cellular / Lorenz |
| Live coding | `d1 $ ...`, hot-swap on eval | `live_loop`, cue-quantized swap | Pdef proxy swap | `live { ... }` block with cue-quantized swap (Sonic Pi model) |
| MIDI out | SuperDirt over OSC mostly | First-class via MIDI library | First-class via MIDIClient | First-class via new `IMidiBackend` (Phase 40) |
| MIDI clock + Link | Link via SuperDirt | Built-in Link + MIDI clock | Built-in Link + MIDI clock | Both, plus JACK transport for Linux composers |
| WASM/browser | Strudel.cc — gold standard | None (Sonic Pi Web is a Pi-only thing) | None (Overtone client-side is limited) | WASM playground via .NET AOT (Phase 41) |
| MusicXML export | Not really | Not really | Limited (community) | First-class with Phase 28 articulation mapping |
| LilyPond export | Not really | Not really | Limited (community) | First-class via score IR |
| ABC import | Not really | Not really | Limited | Common-subset parser (Phase 39) |
| Type safety | Dynamic (JS) / Inferred (Haskell) | Dynamic (Ruby) | Dynamic (Smalltalk) | **Static** — differentiator (already Flow's pitch) |
| Granular synthesis | Via SuperDirt orbits | `synth :prophet, slide:, attack:`-style only | First-class (GrainBuf etc.) | First-class `granular()` builtin (Phase 37) |
| OSC | Native (SuperDirt) | First-class | First-class | First-class server + client (Phase 38) |
| Cross-platform binaries | Haskell-installable | Per-OS installer | Per-OS installer | Per-OS self-contained binary (Phase 41) |
| Improvisation API | Per-set code | Forum recipes (Markov) | Pmarkov + manual chord knowledge | Built-in `improvise()` with chord-aware Markov (Phase 36) |
| Sample-based playback | Per-OS Dirt samples | `:sample` keyword + supplied bank | Buffer.read / SynthDef | Already exists (Phase 29) + sampler-polish bundle (Phase 37) |
| Notation engraving | None | None | None | LilyPond export (Phase 39) — differentiator vs all peers |

---

## Sources

### Pattern algebra / live coding
- [Tidal Cycles — Time docs](https://tidalcycles.org/docs/reference/time/) (HIGH — official docs)
- [Tidal Cycles — Conditions docs](https://tidalcycles.org/docs/reference/conditions/) (HIGH — official docs)
- [Tidal Cycles — Alteration docs](https://tidalcycles.org/docs/reference/alteration/) (HIGH — official docs)
- [Tidal Cycles — jux userbase](https://userbase.tidalcycles.org/jux/en.html) (HIGH — official wiki)
- [Strudel — Functions API](https://strudel.cc/functions/intro/) (HIGH — official docs)
- [Strudel — Technical Manual](https://github.com/tidalcycles/strudel/wiki/Technical-Manual) (HIGH — official)
- [Strudel REPL homepage](https://strudel.cc/) (HIGH — official)

### Pattern + parameterized sections
- [SuperCollider — Pdef class](https://doc.sccode.org/Classes/Pdef.html) (HIGH — official docs)
- [SuperCollider — Pattern Guide 06c Composition](https://depts.washington.edu/dxscdoc/Help/Tutorials/A-Practical-Guide/PG_06c_Composition_of_Patterns.html) (HIGH — official)
- [SuperCollider — Pbindef](https://doc.sccode.org/Classes/Pbindef.html) (HIGH — official)

### Generative
- [Sonic Pi — Markov-chain piano gist](https://gist.github.com/omardelarosa/168114215e9c182b9a4cc7b44300ac94) (MEDIUM — community)
- [Sonic Pi forum — Markov chains for beginners pt 3](https://in-thread.sonic-pi.net/t/markov-chains-for-beginners-part-3/5353) (MEDIUM — community)
- [Music Generation through Cellular Automata (ResearchGate)](https://www.researchgate.net/publication/2324938_Music_Generation_through_Cellular_Automata_How_to_Give_Life_to_Strange_Creatures) (MEDIUM — academic)
- [Listening to Elementary Cellular Automata (Medium)](https://medium.com/code-music-noise/listening-to-elementary-cellular-automata-661018229362) (MEDIUM — community)
- [Rule 30 — Wikipedia](https://en.wikipedia.org/wiki/Rule_30) (HIGH — encyclopedic)
- [Growing Music: Musical Interpretations of L-Systems (CCRMA)](https://ccrma.stanford.edu/~elisse/256A/final/growing%20music%20-%20musical%20interpretations%20of%20l-systems.pdf) (HIGH — academic, Stanford CCRMA)
- [Cherry Audio — Lorenz Attractor module ranges](https://store.cherryaudio.com/modules/lorenz-attractor) (MEDIUM — vendor docs with locked param ranges)
- [Lorenz system — Wikipedia](https://en.wikipedia.org/wiki/Lorenz_system) (HIGH — encyclopedic)

### Magenta / ML continuation
- [Magenta — MusicTransformer](https://magenta.withgoogle.com/music-transformer) (HIGH — official)
- [Magenta — MusicVAE multitrack](https://magenta.withgoogle.com/multitrack) (HIGH — official)

### Granular synthesis
- [Thor Magnusson — Granular Synthesis chapter](https://thormagnusson.gitbooks.io/scoring/content/PartII/chapter10.html) (HIGH — academic textbook)
- [UCSB MAT — Granular Synthesis notes](https://w2.mat.ucsb.edu/240/B/notes/Granular_Synthesis.html) (HIGH — academic)
- [SuperCollider GrainBuf — composerprogrammer.com](https://composerprogrammer.com/teaching/supercollider/sctutorial/5.2%20Granular%20Synthesis.html) (HIGH — tutorial)
- [Csound — Granular Synthesis (FLOSS Manual)](http://floss.booktype.pro/csound/g-granular-synthesis/) (HIGH — official Csound docs)
- [Wikipedia — Granular synthesis](https://en.wikipedia.org/wiki/Granular_synthesis) (HIGH)

### Time-stretch + pitch shift
- [Stephan Bernsee — Time/Pitch Overview blog](http://blogs.zynaptiq.com/bernsee/time-pitch-overview/) (HIGH — industry-standard reference)
- [Surina — Time and pitch scaling in audio processing](https://www.surina.net/article/time-and-pitch-scaling.html) (HIGH — industry-standard reference)
- [Wikipedia — Audio time stretching and pitch scaling](https://en.wikipedia.org/wiki/Audio_time_stretching_and_pitch_scaling) (HIGH — encyclopedic)
- [Improved PVSOLA Time-Stretching Polyphonic Audio (ResearchGate)](https://www.researchgate.net/publication/242019210_Improved_PVSOLA_Time-Stretching_and_Pitch-Shifting_for_Polyphonic_Audio) (MEDIUM — academic)

### Notation export
- [MusicXML 3.0 Tutorial (Recordare)](https://www.musicxml.com/wp-content/uploads/2012/12/musicxml-tutorial.pdf) (HIGH — official)
- [Working with MusicXML files (MuseScore handbook)](https://handbook.musescore.org/file-management/working-with-musicxml-files) (HIGH — MuseScore official)
- [Unofficial MusicXML Test Suite (music21)](https://music21.org/music21docs/developerReference/musicxmlTest.html) (HIGH — interop test reference)
- [LilyPond Notation Reference — articulations list](https://lilypond.org/doc/v2.23/Documentation/notation/list-of-articulations) (HIGH — official)
- [LilyPond Learning Manual — Articulations](https://lilypond.org/doc/v2.25/Documentation/learning/articulations.html) (HIGH — official)

### ABC + MML
- [ABC notation — Wikipedia](https://en.wikipedia.org/wiki/ABC_notation) (HIGH — encyclopedic)
- [ABC standard v2.1](https://abcnotation.com/wiki/abc:standard:v2.1) (HIGH — official standard)
- [abc2midi specification](https://abc.sourceforge.net/standard/abc2midi.txt) (HIGH — official extension)
- [Music Macro Language — VGMPF wiki](https://www.vgmpf.com/Wiki/index.php/Music_Macro_Language) (MEDIUM — community wiki)
- [pedipanol's guide to MML — drivers list](https://mml-guide.readthedocs.io/other/) (MEDIUM — community)
- [mmlx GitHub — NES chiptune MML for modern toolchains](https://github.com/ccampbell/mmlx) (MEDIUM — community)

### Live coding / Sonic Pi
- [Sonic Pi — Multiple Live Loops tutorial](https://github.com/sonic-pi-net/sonic-pi/blob/dev/etc/doc/tutorial/09.3-Multiple-Live-Loops.md) (HIGH — official)
- [Sonic Pi — Live Audio tutorial](https://github.com/samaaron/sonic-pi/blob/master/etc/doc/tutorial/13.2-Live-Audio.md) (HIGH — official)
- [Sonic Pi — Live Coding tutorial intro](https://github.com/sonic-pi-net/sonic-pi/blob/dev/etc/doc/tutorial/01.1-Live-Coding.md) (HIGH — official)
- [SuperCollider — SoundIn class](https://doc.sccode.org/Classes/SoundIn.html) (HIGH — official)

### Diagnostics
- [Rust Compiler Dev Guide — Diagnostics](https://rustc-dev-guide.rust-lang.org/diagnostics.html) (HIGH — official)
- [Rustc Dev Guide — Emitting Errors and Diagnostics](https://github.com/rust-lang/rustc-dev-guide/blob/main/src/diagnostics.md) (HIGH — official)

### Pattern matching
- [Rust Book — Enums and Pattern Matching](https://doc.rust-lang.org/book/second-edition/ch06-00-enums.html) (HIGH — official)
- [FP Block — Pattern matching across languages](https://academy.fpblock.com/blog/pattern-matching/) (MEDIUM — community)

### SFZ + sampler
- [SFZ Format — seq_position opcode](https://sfzformat.com/opcodes/seq_position/) (HIGH — official)
- [SFZ Format — seq_length opcode](https://sfzformat.com/opcodes/seq_length/) (HIGH — official)
- [SFZ Format — Drum basics tutorial (round robins)](https://sfzformat.com/tutorials/drum_basics/) (HIGH — official)
- [SFZ Format home](https://sfzformat.com/) (HIGH — official)

### MIDI / sync
- [Csound — Realtime MIDI manual](https://csound.com/docs/manual/MidiTop.html) (HIGH — official)
- [Ableton — Synchronizing with Link, Tempo Follower, and MIDI](https://www.ableton.com/en/manual/synchronizing-with-link-tempo-follower-and-midi/) (HIGH — official)
- [Ableton — Synchronizing Live via MIDI (help center)](https://help.ableton.com/hc/en-us/articles/209071149-Synchronizing-Live-via-MIDI) (HIGH — official)

### OSC
- [Open Sound Control 1.0 Specification](https://hangar.org/wp-content/uploads/2012/01/The-Open-Sound-Control-1.0-Specification-opensoundcontrol.org_.pdf) (HIGH — official spec)
- [Wikipedia — Open Sound Control](https://en.wikipedia.org/wiki/Open_Sound_Control) (HIGH — encyclopedic)
- [python-osc PyPI](https://pypi.org/project/python-osc/) (HIGH — reference implementation)

### Distribution + docs
- [The second half of shipping a CLI: Homebrew tap, Scoop bucket](https://dev.to/vineethnkrishnan/the-second-half-of-shipping-a-cli-homebrew-tap-scoop-bucket-and-the-sha-dance-bmi) (MEDIUM — community blog, current practice)
- [Rustdoc book — What is rustdoc?](https://doc.rust-lang.org/rustdoc/what-is-rustdoc.html) (HIGH — official)
- [Rust By Example — Documentation](https://doc.rust-lang.org/rust-by-example/meta/doc.html) (HIGH — official)

### Test framework
- [SRFI-64: A Scheme API for test suites](https://srfi.schemers.org/srfi-64/srfi-64.html) (HIGH — official spec; the closest S-expression-shaped precedent for Flow)

### WASM playground
- [Strudel.cc REPL](https://strudel.cc/) (HIGH — the canonical bar)
- [CDM — Strudel live coding in browser](https://cdm.link/musical-powerful-live-coding-in-the-browser-is-near-with-strudel-usable-now/) (MEDIUM — industry coverage)

---

*Feature research for: Flow v1.5 Stage, Studio, Web — 23 features + carryover polish across 7 phases*
*Researched: 2026-05-18*
