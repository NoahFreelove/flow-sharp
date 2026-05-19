# Pitfalls Research — v1.5 Stage, Studio, Web

**Domain:** Music-production interpreter feature expansion (live coding, generative algebra, notation interop, transport sync, distribution)
**Researched:** 2026-05-18
**Confidence:** HIGH (existing-system constraints derived from CLAUDE.md + MILESTONES.md, external claims grounded in 2026 sources)
**Scope:** Integration pitfalls when adding 25+ features in Phases 35–41 to Flow's existing interpreter. Generic project-management risks excluded.

---

## Cross-Cutting Constraints (Read First)

Every pitfall below is scored against these existing-system invariants. When prevention is offered, the prevention must not break any of these:

| Invariant | Source | Why It's Load-Bearing |
|-----------|--------|------------------------|
| **Two-run cmp-clean determinism** (same git SHA, two consecutive runs → byte-identical WAV+MIDI) | CLAUDE.md "RMS-windowed regression testing" + Phase 18/25/27/33 inheritance | Composer + CI gate; PRNGs reseed at `renderSong`/`writeWav` boundaries |
| **RMS-windowed regression** (±0.5 dB / 100ms) | SPEC-8 (Phase 28), baselines at `flow-lang.Tests/baselines/Phase28/` | For changes that legitimately move bytes but preserve perceptual fidelity |
| **Charitable interpretation** | Memory: `feedback_charitable_interpretation`, CLAUDE.md "reverbTime 0 is dry sentinel" | Prefer silent-and-documented assumption over error |
| **Genre-agnostic, music-only scope** | Memory: `project_genre_agnostic` | Reject features whose only justification is non-musical |
| **Post-public deprecation cycle** | Memory: `project_pre_public_no_legacy_burden` (rewritten 2026-05-17, public since v1.4) | Breaking changes require deprecation, NOT single-commit swaps |
| **Linux-first PulseAudio playback** | `PulseAudioSimpleBackend`, `IAudioBackend` abstraction | Cross-platform is Phase 41 territory; do not regress Linux |
| **Hand-rolled DSP + minimal deps** | RESEARCH STACK.md, only Pidgin (unused) + DryWetMidi | New deps require justification on par with DryWetMidi |

**A pitfall is "critical" if it threatens any invariant above OR causes silent musical damage (worse than a crash because composers won't notice until performance).**

---

## Critical Pitfalls

### Pitfall 1: WASM .NET 10 NativeAOT-LLVM kills FlowEngine's reflection-heavy registry

**What goes wrong:**
NativeAOT trims `InternalFunctionRegistry`'s lambda-dispatch tables, `OverloadResolver`'s `GetSpecificity()`-via-type-reflection scoring, `ExpressionEvaluator`'s `switch` over runtime `Value.Type` (often type-checked via `is FlowType`/`GetType()`), and `ModuleLoader`'s dynamic `Assembly.Load` paths. First symptom in the browser: every second built-in throws `MissingMethodException` or returns `Void` silently because the trimmer pruned the lambda the registry's `Dictionary<FunctionSignature, Func<Value[],Value>>` was about to invoke.

Microsoft's own docs are explicit: "With AOT… we cannot dynamically load assemblies using Assembly.LoadFile, use Reflection, such as .GetType().GetProperty('Name'), or emit code using System.Reflection.Emit." NativeAOT-LLVM for WASM is real in .NET 10 (50+ engineer team, sub-second startup, 3.4–6.7 MB sample sizes), but the trimming pressure is harder than on desktop because every kilobyte of metadata costs page-load time.

**Why it happens:**
FlowEngine was designed in a JIT world. The registration pattern `registry.Register(signature, args => { ... })` works *because* lambdas live as closed-over delegates the linker can't statically reach. Same problem for `BuiltInFunctions.cs`'s ~100 lambdas — they're invoked via dictionary lookup, not method call.

**How to avoid:**
- Phase 41 spike: build a tracer that dumps every `FunctionSignature` invoked by the v1.4 + v1.5 test suite. Generate a `DynamicDependencyAttribute` table or — better — a source-generated `BuiltInFunctions.Registrations.g.cs` that emits direct method calls. This is the "trim-correct" pattern the Blazor team uses for their AOT path.
- Use the Mono interpreter (jiterpreter) as the WASM target, NOT NativeAOT-LLVM, for v1.5. Sample-application JIT-on-WASM weighs ~10 MB but preserves reflection. Take the size hit; revisit NativeAOT-LLVM in v1.6+ when the source-generator pass exists. **Honest recommendation: the WASM playground in Phase 41 should be Mono-WASM jiterpreter, not NativeAOT.**
- Reject any reflection added between v1.5 start and Phase 41 unless it's gated behind `[DynamicallyAccessedMembers]`.

**Warning signs:**
- A built-in works in `dotnet run` but returns `Void` in the WASM build with no error.
- WASM build size > 15 MB even with trimming aggressive.
- Linker warning `IL2026`/`IL2070`/`IL2075` during publish — these are pre-runtime breakage notices.

**Phase to address:** Phase 41 (WASM playground). Spike in week 1 of Phase 41; if NativeAOT-LLVM proves intractable, fall back to Mono-WASM jiterpreter same day. Do NOT block on this.

---

### Pitfall 2: Ableton Link tempo becomes authoritative, breaks Flow's `MusicalContext` stack

**What goes wrong:**
Flow's `MusicalContext` is a push/pop stack where `tempo 120 { ... }` is composer-authored truth. Link is a peer-equal protocol where *any* participant can change the tempo and everyone follows the last writer. Naive integration: Link's tempo callback overwrites `MusicalContext.Tempo` → composer's `tempo 140 { ... }` block silently changes meaning depending on what's on the LAN → determinism contract breaks on every run because the Link host was different.

Worse: when the Link peer disappears mid-piece (cable yanked, app crashes), there's no "session ended" event — Link channels are persistent IDs across reconnect. The behavior question "what tempo do we use now?" has no obvious answer.

**Why it happens:**
The instinct is "Link is sync, just wire it to tempo." But Link's contract is "any participant can adjust, last write wins" — that's the antithesis of a deterministic interpreter's contract.

**How to avoid:**
- **Decision rule (lock at Phase 40):** Link is a render-time *input* for the playback path, NOT for `writeWav`/`writeMidi`. Composer-authored `tempo` inside the source file is canonical for offline rendering (preserves determinism contract). Link-driven tempo only modulates the playback clock when `play`/`loop`/`preview` is active AND `(linkEnable)` has been called.
- When Link is enabled, expose `(linkTempo)` as a read-only function and `(linkOffset)` to let composers express "play at Link's tempo, scaled by 1.5". The composer chooses to read from Link; Link does NOT silently override.
- Peer-disappear handling: latch the last-seen Link tempo into a fallback. If Link signals zero peers, hold the current tempo. Do NOT fall back to MusicalContext's authored tempo mid-piece — that's a jolt at an unpredictable moment.
- Real-time priority on Linux (PREEMPT_RT or RT cgroup) is NOT required for Link itself — Link's UDP discovery + beat-time math is fine at SCHED_OTHER. RT priority is needed for the audio callback that consumes Link's clock, which is already a problem Flow's PulseAudio path has (and currently lives without). Phase 40 should NOT add an RT requirement.

**Warning signs:**
- Two consecutive `writeWav` runs at the same git SHA produce different byte counts because Link sneaked into the offline render path.
- Playback "drifts" against the authored sequence — symptom of Link's clock disagreeing with `MusicalContext.Tempo` on a path that should have been Link-immune.
- Test suite passes on the dev machine but fails on CI because CI has no Link peer.

**Phase to address:** Phase 40 (transport sync). Add a "Link is render-time-input-only" decision row to PROJECT.md Key Decisions immediately on Phase 40 start.

---

### Pitfall 3: Real-time MIDI output hot-plug + sysex timing destroys event determinism

**What goes wrong:**
USB MIDI controllers plug/unplug during a session. The naive `IMidiBackend` opens a handle at session start; the device dies mid-piece; the next `noteOn` throws or — worse — succeeds silently to a dead handle and the human hears nothing.

Worse compositional pitfall: audio buffer is 256–1024 samples (~5–20ms at 44.1kHz); MIDI is sub-ms. If Flow's playback loop emits MIDI events when the audio *buffer* is queued (not when it *plays*), MIDI fires up to 20ms before audio → noticeable for percussive parts, wrong for sample-accurate sync.

Sysex (large dumps) blocks the MIDI thread; if it's the same thread as the audio scheduler, the audio buffer underruns and you get a click.

**Why it happens:**
ALSA's seq API, JACK MIDI, and CoreMIDI all have different hot-plug semantics. .NET has no first-class MIDI primitive — DryWetMidi 8.0.3 covers SMF *files* very well but its `OutputDevice` real-time path is platform-conditional and the Linux story is thin.

**How to avoid:**
- Adopt the `IAudioBackend` pattern verbatim: `IMidiBackend` with one impl per platform (ALSA seq on Linux, WinMM/UWP on Windows, CoreMIDI on macOS). Each impl owns its hot-plug detection thread.
- **Latency alignment rule (lock at Phase 40):** MIDI events emit at `audioBuffer.PlaybackStartTime + bufferOffset` (not at queue time). This requires the audio backend to expose "when will this buffer's first sample reach the DAC?" — PulseAudio's `pa_stream_get_latency` provides this; WASAPI and CoreAudio have equivalents.
- Sysex on a separate thread/queue. Mark sysex messages as "best-effort, no sample-accuracy guarantee" in the composer-facing docs.
- Hot-plug: when a device disappears, log to stderr (charitable-interpretation rule), retry on every emit for ~1s, then quietly drop until the device returns. Do NOT throw — that breaks long-running `live { ... }` sessions.
- Default-device selection: deterministic choice (alphabetical by name, ties broken by USB topology). The composer-facing rule is "if you care about WHICH device, name it explicitly with `(setMidiDevice "name")`."

**Warning signs:**
- Test suite passes but a real keyboard sounds "ahead of" the audio in playback.
- Sysex dump from a connected synth causes an audio glitch.
- `(midiSend ...)` succeeds in unit tests but the human reports "I'm getting nothing" — symptom of a closed device handle returning success-no-op.

**Phase to address:** Phase 40 (real-time MIDI output). Latency-alignment rule is the highest-stakes lock-in.

---

### Pitfall 4: MusicXML export consumer-dependent — different DAWs read different sub-dialects

**What goes wrong:**
Composer exports Flow → MusicXML, opens in MuseScore: looks great. Same file in Dorico: triplets are wrong starting at bar 14; tuplet errors corrupt everything after. Same file in Finale: imports as "highly disjointed" (per Finale's own help center community posts).

Worse: Flow's Phase 28 articulations have no clean MusicXML mapping. Legato → slur (one note? whole phrase?); sforzando → `sfz` dynamic OR `accent` notation OR both; marcato → `>` accent vs `^` strong-accent. Choose wrong, downstream renders wrong articulation, composer never sees the bug until performance.

**Why it happens:**
"Because of how loose the MusicXML standard is, it is a complete dice roll how good the conversion will be from any one program to any other program." (vi-control 2024). Dorico struggles with tuplets specifically; Sibelius and MuseScore handle them better. MusicXML 4.0 exists but compliance is "rather sketchy" across the big three.

**How to avoid:**
- **Target MuseScore as the reference consumer.** MuseScore has the most permissive importer and is free, so v1.5 users can validate output. Document this explicitly in the `flow doc` output.
- Subset to MusicXML 3.1 + the partwise (NOT timewise) flavor. Avoid layout-positioning attributes entirely — let the consumer engrave. Encode only: pitches, durations, tuplets via `<time-modification>`, key/time/tempo, slurs (for legato), accents (`>` for accent, `^` for marcato — both standard), dynamics including `sfz` (for sforzando), staccato/tenuto/legato as `<articulations>` children.
- Articulation decision table (lock in Phase 39):
  - Accent → `<articulations><accent/></articulations>` + `<notations>`
  - Marcato → `<articulations><strong-accent/></articulations>`
  - Staccato → `<articulations><staccato/></articulations>`
  - Tenuto → `<articulations><tenuto/></articulations>`
  - Legato → `<slur type="start"/>` ... `<slur type="stop"/>` over the legato region (NOT per-note)
  - Sforzando → `<dynamics><sfz/></dynamics>` on the affected note
- Round-trip test in CI: export `examples/symphony/symphony.flow` to MusicXML, import into MuseScore via `mscore --convert-to ...`, re-export to MusicXML, diff. Any drift is a regression.
- Tuplets: simplify before emit — convert nested tuplets to a single tuplet ratio where possible. If un-flattenable, emit with explicit `<time-modification>` and accept that Dorico will mangle it.

**Warning signs:**
- A composer pastes the exported XML into MuseScore and notes are "wrong" — usually tuplets or ties.
- The round-trip CI test diff exceeds 5 lines.
- The first user bug report is "Dorico can't open this."

**Phase to address:** Phase 39 (notation export). Test with all three major consumers in HUMAN-UAT; document Dorico tuplet limitations as a known divergence.

---

### Pitfall 5: LilyPond microtonal + nested tuplet + multi-voice notation produces engraver errors

**What goes wrong:**
Flow's Phase 32 supports Scala tunings with cent-precision (`C4+50c`). LilyPond has *some* microtonal support via `-iesih`/`-eseh` (quarter-tone accidentals) but it's quarter-tone-only out of the box. A 17-cent offset has no first-class glyph; you need to invoke `\override` for arbitrary cent markings. Nested tuplets work in LilyPond (`\times 2/3 { \times 3/4 { ... } }`) but the engraver complains visually about beam grouping.

Multi-voice collisions: Flow's voice blocks (`{voice C4w} {voice C5q D5q E5q F5q}`) translate to LilyPond `<<` voice contexts. Two voices with overlapping pitches in the same staff produce engraver collision warnings.

**Why it happens:**
LilyPond's text format is forgiving (the parser rarely errors); the engraver is strict (visual output complains). The two are decoupled, so a file can "compile" yet produce unreadable PDFs.

**How to avoid:**
- Emit cent offsets as a comment on the note + a `\once \override` for the cent value in text form. LilyPond won't render an exact glyph but the engraver won't error.
- Wrap nested tuplets in `\override Beam.breakable = ##t` and accept that LilyPond's beam grouping will be approximate.
- Multi-voice: emit each voice into its own `\new Voice` inside a `<< { ... } \\ { ... } >>` context. Use `\voiceOne` / `\voiceTwo` directives for stem-direction discipline.
- Run `lilypond -dno-print-pages` on the emit in CI to catch engraver errors without rendering PDFs.

**Warning signs:**
- LilyPond emits an `unterminated tuplet` warning.
- The PDF has overlapping noteheads.
- The score "compiles" but the composer says "it looks wrong."

**Phase to address:** Phase 39 (LilyPond export). Pair-program with a LilyPond-literate composer in HUMAN-UAT.

---

### Pitfall 6: ABC parser — dialect divergence (1.6/2.0/2.1 + abc2midi extensions)

**What goes wrong:**
"ABC notation" is at least three incompatible languages. ABC 1.6 (the de facto pre-2003), ABC 2.0 (2003), ABC 2.1 (current 2011). On top: abc2midi extensions (`%%MIDI ...` directives), Folk Information Exchange custom keys, and dozens of ad-hoc tunebook dialects. Flow imports a `.abc` file from The Session — it parses; result sounds wrong because the modal key `Edor` (E dorian) was treated as `E major`.

**Why it happens:**
ABC's specification is community-maintained, the lints are weak, and most real-world `.abc` files in the wild use 2.0 with abc2midi sprinkles. If Flow targets 2.1 strict, half the corpus fails.

**How to avoid:**
- **Target ABC 2.1 + the abc2midi subset that appears in The Session corpus** (>50K tunes, the de facto test bed). Modal keys (`Edor`, `Dmix`, `Aphr`, etc.) MUST be parsed — they're not optional for folk music.
- Multi-tune files: parse all tunes; first tune is the entry point; expose `(abcTune index)` for later access. Composers can `use` the file and pull tune #5 by index.
- Ornaments: support `~` (general ornament → trill), `T` (trill), `S` (segno — purely structural, no audio impact). Drop `M`, `P`, `H`, etc. with a stderr warning (charitable-interpretation).
- Build a corpus regression test: import 100 tunes from The Session, render to MIDI, ensure no parse errors and no zero-byte outputs.

**Warning signs:**
- A user file imports without error but plays in the wrong key.
- A common ornament token isn't recognized — silent drop.
- Two consecutive imports of the same `.abc` file produce different Flow code (non-determinism in parsing).

**Phase to address:** Phase 39 (ABC import). The Session corpus regression is the single most valuable test.

---

### Pitfall 7: MML parser — multiple incompatible dialects, where to draw the line

**What goes wrong:**
MML (Music Macro Language) is a family, not a language. MML for PC-88, MSX, PC-98, MUCOM88, PMD, MML2VGM, and modern chiptune trackers all differ. The "common core" (`cdefgab`, octave `o`, length `l`, tempo `t`, loop `[ ... ]`) is ~70% of any dialect. The remaining 30% is FM operator routing (4-op vs 2-op), drum maps, ADPCM envelopes, panning byte values — chiptune-specific and not interoperable.

**Why it happens:**
There's no canonical MML. Importers either pick one dialect (lock out the others) or try to be lenient (silently misinterpret advanced syntax).

**How to avoid:**
- **Lock the import surface at PC-98-era MML common core.** Document the supported subset: notes, durations, octaves, tempo, loops, rest, tie, dot. Reject (with stderr) FM operator routing, drum maps, ADPCM envelopes — these have no Flow analog without a 6-month chiptune mode.
- Charitable interpretation: unrecognized tokens become rests, NOT errors. Document this so chiptune composers can pre-process their files.
- If demand emerges for a specific dialect (PMD, MUCOM), gate it behind `enable mmlPmd;` pragma — additive opt-in, doesn't break the common core.

**Warning signs:**
- Imported MML plays but skips notes silently — symptom of token drops without stderr.
- A chiptune composer reports "this used to work in $tracker" — likely a dialect-specific feature outside the common core.

**Phase to address:** Phase 39 (MML import). Common-core lock is the v1.5 scope decision.

---

### Pitfall 8: Phase vocoder smears transients on percussive material; PSOLA wrong for harmonic

**What goes wrong:**
Flow's existing material is mixed (orchestral piano + saxophone + drums). A naive phase-vocoder time-stretch on the drum buss smears the kick attack into a "phasey" wash. PSOLA preserves the attack but introduces formant artifacts on the saxophone's harmonic content. Either choice produces clearly-wrong output on half the material.

The known classical fix is harmonic-percussive separation (HPSS) + selective application — phase vocoder on harmonic content, overlap-add on percussive. Implementing HPSS in v1.5 is its own project; gluing in a library would violate the minimal-dependency rule.

**Why it happens:**
Time-stretch has no single right algorithm. Every choice trades artifact-type for artifact-amplitude.

**How to avoid:**
- **Default to phase vocoder with phase-locking at transients** (median-filter-based transient detection). This is the "improving the phase vocoder" 2023 Danish Sound paper's prescription. Adds modest complexity, preserves the existing minimal-dep posture.
- Expose a composer-facing parameter: `(timestretch buf factor #vocoder)` vs `(timestretch buf factor #psola)`. Document which to choose:
  - Drums, percussion, transient-heavy → `#psola`
  - Tonal melodies, sustained chords → `#vocoder` (default)
  - Mixed → split with HPSS … OR render the parts separately and stretch independently before mixing (the Flow-idiomatic answer because composers already structure pieces by voice).
- **The Flow-idiomatic answer** is the prevention: encourage composers to time-stretch individual voices BEFORE mixing, not the full mix. This sidesteps the algorithm-choice problem entirely.

**Warning signs:**
- Stretched drums sound "phasey" or "loss of attack."
- Stretched sax sounds "chipmunked" or "robotic."
- Round-trip stretch (2× then 0.5×) doesn't recover the original — symptom of non-invertible artifacts compounding.

**Phase to address:** Phase 37 (time-stretch). Composer-facing two-choice API is the gate.

---

### Pitfall 9: Granular synthesis — grain boundary clicks + determinism vs jitter tension

**What goes wrong:**
Grain boundaries click when not windowed (Hann/Gaussian/Tukey choice matters; Tukey with α=0.5 is the conventional default). High grain density at high pitches aliases. *Composers want grain jitter* — random pitch, position, and length perturbation — and that introduces randomness that fights Flow's two-run cmp-clean determinism contract.

**Why it happens:**
Granular synthesis is musically alive *because* of randomness. Determinism asks for the opposite. Naive jitter uses `Random()` without seed control → every run sounds different → contract breaks.

**How to avoid:**
- Window: Tukey α=0.5 default; expose `(grainWindow #hann | #gaussian | #tukey)` for advanced use. Always windowed, no exceptions.
- Jitter: every granular synth call gets a *seedable* PRNG, reseeded at `renderSong`/`writeWav` boundary just like the existing white-noise + TPDF dither RNGs (CLAUDE.md "Pattern: synth-RNG-seeded-at-render-boundary"). Composer-facing: `(grain buf #jitter 0.3)` is deterministic at a fixed git SHA; `(grain buf #jitter 0.3 #seed 42)` is deterministic across SHA changes.
- Aliasing: pre-low-pass the grain source at Nyquist/2 when pitch-shifting up; this is cheap.
- Tests: granular smoke test must pass two-run cmp-clean. Add to the SPEC-8 RMS-windowed regression baseline set.

**Warning signs:**
- Audible clicks at grain density >50/sec.
- Two `renderSong` calls produce different bytes — granular RNG is unseeded.
- Pitched grains above 8kHz have a "swarm of bees" texture — aliasing.

**Phase to address:** Phase 37 (granular). RNG seed discipline is identical to existing patterns; reuse don't reinvent.

---

### Pitfall 10: OSC — type tag drift + UDP packet loss + flood rate

**What goes wrong:**
OSC's type tags (`,f` float32 vs `,d` float64 vs `,i` int32) are easy to mismatch between client and server. Sender uses `,f`, receiver expects `,d` → silent corruption (the bytes interpret as garbage). UDP is lossy; LFO at 100Hz produces 6000 messages/min → some drop on congested LANs.

OSC bundles wrap multiple messages with a time tag; mishandled time tags fire messages all-at-once-now instead of staggered.

Discovery via zeroconf/Bonjour is conventional but optional; many implementations skip it and force manual IP/port config.

**Why it happens:**
OSC 1.0 is simple but its discipline ISN'T enforced by libraries. Network byte order in spec, but some implementations send host byte order (especially on little-endian platforms). Discovery via Bonjour requires mDNS — Linux has Avahi, Windows requires Apple's Bonjour Print Services, neither is universal.

**How to avoid:**
- Pick a discipline and document it: Flow uses `,d` (float64) for ALL numeric arguments. Document that interop with `,f`-only consumers requires a converter on the receiver side. This is opinionated but clear.
- Always network byte order (big-endian) on the wire — the spec says so. Test with a wireshark capture before declaring Phase 38 done.
- Rate-limit OSC emit to 200Hz max per channel by default. Composers can override but the default is sane.
- Discovery: do NOT ship zeroconf in v1.5. Manual `(oscClient "192.168.1.42" 9000)` only. Zeroconf is a v1.6 candidate behind `use "@osc-zeroconf"`.
- Bundles: only emit bundles when explicitly composer-requested; default is single messages.

**Warning signs:**
- OSC receiver shows numeric garbage that becomes correct on byte-swap.
- LFO at high rate causes consumer-side stuttering.
- Random "missing" parameter updates on a busy LAN — symptom of UDP loss without retry policy.

**Phase to address:** Phase 38 (OSC server/client). Single-message default + rate limit are the v1.5 locks.

---

### Pitfall 11: Pattern matching exhaustiveness — Flow's flexible-type tradition vs strict checking

**What goes wrong:**
Naive `match` requires exhaustiveness checking. But Flow's existing overload resolution is forgiving (Int→Long→Float→Double widening, Void wildcards, charitable interpretation). Strict exhaustiveness on a music type like `Note` is hostile to the composer ("but I just want the C4 case, fall through to default"). Strict refutability on `let` destructuring is hostile ergonomic.

Charitable interpretation says: accept non-exhaustive, with stderr warning, fall through to a charitable default (Void or the input value). This is the right Flow answer; getting it wrong means importing Rust-style strictness into a music-first language.

**Why it happens:**
Pattern-matching cargo cult — language designers import Rust's exhaustiveness checking as "the right way." It is, for systems languages; it isn't, for music languages where the composer's "I'll handle this later" is sacred.

**How to avoid:**
- Non-exhaustive `match` warns to stderr (charitable-interpretation rule) AND returns Void for the unmatched arm. Composer can opt into strict via `enable matchExhaustive;` pragma per the v1.3 pragma pattern.
- `let <<a, b>> = expr` destructuring already exists (Phase 26.1) and is irrefutable — keep it that way.
- Type-narrowing within match arms: do it. `match x { case Note n => ... }` makes `n` typed as `Note` inside the arm.
- Guard clauses `| when (...)`: support, but composers will use these heavily so make sure they don't break the `Lazy<Value>` thunk pattern (Phase 11 `Lazy<Value>` with `ExecutionAndPublication` is the existing precedent).
- Fall-through semantics: NO fall-through (Rust-style, not C-style). Composer's match arms are independent.

**Warning signs:**
- Composer reports "match silently did nothing" — happens if the warning is suppressed in their context.
- Pattern arm executes against the wrong type — type narrowing broken.
- Guard clause re-evaluates side effects on each test — Thunk caching broken.

**Phase to address:** Phase 35 (language foundation). Pattern-match decision rules go into a PATTERNS.md entry on Phase 35 start.

---

### Pitfall 12: `live { ... }` block — state preservation across reload + infinite loop bailout

**What goes wrong:**
Composer is jamming in a `live` block; saves a file; the new code has a typo causing an infinite loop. The watcher hot-swaps and now the audio thread is stuck in a `while(true)` — playback hangs, the file system event for the *next* save is queued behind the stuck callback, the composer panics.

Worse: the watcher hot-swaps mid-bar. The voice pool has 12 active notes. Their envelopes reference the OLD `MusicalContext.Tempo`; the new code has `tempo 140` instead of `tempo 120`. Notes' release times become inconsistent — audible glitch.

Worse-still: the determinism contract assumes a single deterministic execution order. `live` mode by definition has wall-clock-dependent reload events; two re-recordings of "the same session" can't be byte-identical.

**Why it happens:**
Hot reload is hard. Live coding adds wall-clock dependencies. State preservation across reloads is an unbounded design problem (which state survives? closures? variables? PRNG cursors?).

**How to avoid:**
- **`live` blocks opt out of the determinism contract.** Document this explicitly. Two `live` sessions are NOT expected to produce byte-identical output — that's the whole point.
- Reload happens at the next bar boundary (already a v1.0 Phase 5 commitment for "beat-synced live reload" — extend this). Voice-pool state preserved across reload IF the voice was authored by a name that exists in the new code; dropped if the name disappears.
- **Infinite-loop bailout:** every `live` block evaluation runs in a `CancellationToken`-aware execution context with a 30-second wall-clock cap. If a reload doesn't yield within 30 seconds, kill the evaluation, revert to the previous code, log to stderr. Composer keeps jamming.
- File-watch debounce: 200ms between successive saves. Rapid Ctrl-S spams don't queue 50 reloads.
- Closures referencing now-stale bindings: detect at reload time — if a closed-over name no longer exists, log warning, drop the closure. Voices using the dropped closure stop at next bar boundary.

**Warning signs:**
- Audio hangs on save → infinite-loop reload, no bailout.
- Notes glitch at bar boundaries → state preservation incomplete.
- Composer can't reproduce a "good take" → determinism not opted out; composer surprised it's not byte-identical.

**Phase to address:** Phase 38 (`live` block + watch mode modernization). 30-second bailout is the highest-stakes lock.

---

### Pitfall 13: REPL completion across the parse boundary — partial-line completion

**What goes wrong:**
User types `(transp` in the REPL. The parser sees an unbalanced paren and bails. LSP completion expects a stable file — there's no LSP query that handles "complete this partial S-expression." Result: REPL completion always returns the global symbol set, no context-sensitive narrowing.

**Why it happens:**
LSP was designed for stable files. REPL parse states are inherently partial. Bridging the two requires either a *separate* completion path for REPL (likely) or a parser that emits "partial AST + cursor location" (hard).

**How to avoid:**
- **Two completion paths.** File context uses LSP (already shipped in Phase 31). REPL context uses a token-level heuristic: look at the last 3 tokens, guess the call site, narrow to functions whose first argument type matches the inferred context.
- Token-level heuristic is good-enough for v1.5. Don't aim for type-narrowed perfection; aim for "if I typed `(transp`, I get `transpose` ranked first."
- Test cases: 20 partial REPL lines, manually annotated with "ideal first suggestion." CI runs the heuristic and asserts >80% rank-1 accuracy.

**Warning signs:**
- Composer types a common stdlib function and the right answer is rank-15 or worse.
- Completion shows internal-only types/functions (Void wildcards, internal Thunk type) — user-facing filter missing.

**Phase to address:** Phase 38 (REPL polish). Heuristic-not-LSP is the v1.5 lock.

---

### Pitfall 14: Rust-style diagnostics — span tracking through the existing pipeline

**What goes wrong:**
Existing `ErrorReporter` has line:column positions but no byte ranges. Rust-style diagnostics (the "this is the bad span | here is what you wrote | here is what was expected") need:
1. Lexer emits tokens with byte ranges (start, end).
2. AST nodes carry composite spans (sum of children's ranges).
3. Source text preserved through the pipeline (currently isn't — the lexer consumes the source string).
4. Error rendering can re-quote source slices with arrows pointing at offending columns.

Retrofit risk: every AST `record` type needs a `Span` field added; every parser production needs to populate it; every error site needs to use the right span. Pure mechanical work, but error-prone in pure quantity.

**Why it happens:**
The original ErrorReporter was good-enough for v1.0. v1.5 raises the bar; the retrofit is overdue.

**How to avoid:**
- **Phase 35 Plan 01**: add `Span` to the base AST node type as a defaulted parameter. Every existing AST `record` becomes `record FooExpr(... , Span Span = default)`. Defaulted parameter = no breaking change to v1.4 .flow files. Migrate parser productions incrementally; old call sites get `default` spans (no Rust-style rendering, fall back to line:col).
- Source text: keep the original string on the `ExecutionContext` per-file. Spans are byte offsets into it. Memory cost is trivial.
- Render: implement once in `DiagnosticRenderer`; use Unicode box-drawing for the underline (`──┴──`).
- Test: a single canonical bad-syntax example with hand-checked rendering — committed as a regression baseline.

**Warning signs:**
- Old AST node missing a `Span` — rendering falls back to bare "error at line N."
- Span points at wrong column — almost certainly off-by-one between lexer/parser conventions.
- Multi-line errors render with broken underlines — usually source-text mismatch.

**Phase to address:** Phase 35 (Rust-style diagnostics). Defaulted-parameter retrofit pattern from v1.3 Phase 22 is the precedent.

---

### Pitfall 15: Test framework — hermetic test runs require state reset that isn't in current FlowEngine

**What goes wrong:**
Composer writes `(test "transpose-preserves-length" {...})`. Test 1 mutates the voice pool. Test 2 runs in the same FlowEngine, inherits the polluted voice pool, gets wrong answers. CI fails non-deterministically depending on test execution order. Worse: musical context stack pollution (a `tempo 200 { ... }` block that crashes mid-body leaves 200 BPM on the stack for the next test).

The MusicalContext stack, voice pool, PRNG state, and SymbolInternTable are all FlowEngine-instance scoped — they DON'T reset between top-level statements.

**Why it happens:**
FlowEngine was designed for one script per process. Test frameworks invert this assumption.

**How to avoid:**
- **Per-test reset**: every `(test ...)` invocation snapshots the FlowEngine's state (context stack, voice pool, PRNG seeds, symbol table) before run, restores after. New utility `FlowEngine.SnapshotState()` / `RestoreState()`.
- Audio output capture for assertion: `(renderToBuffer expr)` evaluates `expr` against a *separate* `FlowEngine` instance and returns the rendered Buffer. This is the test framework's `assertWithinDb` enabler.
- Default to per-test FlowEngine spin-up. Tests that need shared state opt in with `(test "name" #shared {...})`.
- Charitable interpretation: a test that crashes mid-body doesn't crash the framework; it reports "failed" and continues.

**Warning signs:**
- Test passes alone, fails when run with the suite — state pollution.
- `(assertWithinDb a b)` claims 0 dB difference when ears say different — buffer rendered against polluted context.
- Symbol interning table grows unbounded across runs — interning not snapshotted.

**Phase to address:** Phase 35 (pure-Flow test framework). Snapshot/restore utility is the gate.

---

### Pitfall 16: Cross-platform audio backends — WASAPI exclusive vs shared, CoreAudio buffer drift

**What goes wrong:**
WASAPI exclusive mode locks the audio device (no system mixer; 3ms latency); shared mode goes through Windows's audio engine (30–50ms latency, system sounds work). Flow's existing PulseAudio code assumes ~10ms — exclusive is too tight (more underruns), shared is too loose (live coding feels laggy).

CoreAudio buffer sizes vary by device (some devices are 64 samples, others 1024) — fixed-assumption code breaks on unknown hardware.

**Why it happens:**
The PulseAudio model "assume one buffer size everywhere" doesn't survive contact with WASAPI/CoreAudio.

**How to avoid:**
- WASAPI: default to *shared* mode for v1.5. Exclusive mode is opt-in via `(audioMode #exclusive)` after profiling. Shared mode's 30-50ms is the "always works" baseline.
- CoreAudio: query the device's preferred buffer size at startup; allocate Flow buffers as multiples of that. Don't hard-code 256/512/1024.
- Latency expectation: document that v1.5 Windows playback is "DAW-grade for production, not for live performance." Live performance on Windows is v1.6 with exclusive-mode profiling.
- `IAudioBackend` already abstracts the right surface; the impls just need to be honest about their preferred buffer sizes via a new `PreferredBufferSize` property.

**Warning signs:**
- Windows users report audio dropouts — WASAPI exclusive on hardware that can't sustain.
- macOS users on USB-C interfaces report glitches — buffer size mismatch.
- "It works on Linux but Windows sounds different" — sample rate negotiation (44.1 vs 48 kHz default differs).

**Phase to address:** Phase 41 (cross-platform binaries). Shared-mode default + per-device buffer query are the v1.5 locks.

---

### Pitfall 17: JetBrains Marketplace publish — signing + plugin verifier compatibility matrix

**What goes wrong:**
Plugin Marketplace requires a signing certificate (paid) OR a JetBrains-issued marketplace key (free but requires account approval). Plugin Verifier checks compatibility against a matrix of IntelliJ Platform versions; if your `since-build`/`until-build` is too narrow, you miss users; if too broad, the verifier flags incompatible APIs.

Marketplace review is manual and can take days. Plugin rejection over a minor issue (license file format, screenshot dimensions) is common.

**Why it happens:**
Marketplace is gated for spam/security reasons; the process is well-documented but rarely smooth on first attempt.

**How to avoid:**
- Pre-submission checklist: signing key resolved, `plugin.xml` fields complete (name, vendor, description, change-notes), screenshots at correct dimensions (1280x800 PNG), license file present (MIT or Apache 2.0 — match Flow's license).
- Run `gradle runPluginVerifier` against IntelliJ Platform 2024.3 + 2025.x BEFORE submitting. The verifier output is the source of truth on compatibility.
- Submit early in the milestone (Phase 41 week 1), iterate on rejection over 1-2 weeks. Treat publish as a process, not a one-shot.
- Have a fallback: if Marketplace publish gets stuck, ship the plugin as a direct download (.zip from GitHub Releases) — users can "Install Plugin from Disk."

**Warning signs:**
- Verifier reports "missing API element since 2025.1" — fix `since-build`.
- Marketplace review email asks for "more detail in description" — common, polish the description.

**Phase to address:** Phase 41 (JetBrains publish). Direct-download fallback ensures milestone closure isn't blocked by review process.

---

### Pitfall 18: Stereo pan across instruments — mono-collapse breakage + scope confusion

**What goes wrong:**
Composer pans piano hard left, sax hard right. Sounds great on stereo speakers. The mix gets folded to mono (radio, podcast, phone speaker) and one instrument disappears or is heavily attenuated. M/S decoders amplify the side-content at extreme pan positions, producing flange-like artifacts.

Naming confusion: is `pan` per-voice (one note), per-instrument (all piano notes), or per-section (all instruments in `intro`)? Compose-by-mistake when scopes disagree.

**Why it happens:**
Constant-power pan (`left = cos(angle) × sample`, `right = sin(angle) × sample`) at extreme positions concentrates all energy in one channel. Mono-collapse `mono = (left + right) / 2` halves the panned content.

**How to avoid:**
- Default pan is `0.0` (center). Extreme pan ±1.0 documented as "stereo-only, will lose half-energy in mono."
- Pan scope: per-instrument (most idiomatic — "saxophone is in the right speaker"). Per-voice opt-in via `(pan voice 0.5)`. Per-section is a sum: section default applies to instruments without their own pan.
- Naming lock (Phase 37): `(pan instrument value)` and `(pan voice value)` use overload resolution. `(panSection section value)` is explicit per-section because it has different semantics.
- Mono-collapse test: render piece in stereo and mono, check that mono RMS is within ±2 dB of stereo RMS. Flag extreme pans.

**Warning signs:**
- Mono mix sounds "thin" — instruments at hard pan lost.
- Phasing at boundaries — pan changed mid-note without crossfade.
- Composer reports "pan didn't work" — scope confusion (set per-section but expected per-voice).

**Phase to address:** Phase 37 (sound design). Naming lock at Phase 37 start.

---

### Pitfall 19: Sampler polish — round-robin determinism + velocity layer crossfade artifacts

**What goes wrong:**
SFZ round-robin (`seq_position`/`seq_length` opcodes) selects a different sample on each playback. Composer wants this for realism; CI wants two-run determinism. Round-robin index needs to be deterministic — same problem as PRNG seeding (CLAUDE.md "seedable PRNG, reseed at renderSong boundary").

Velocity layer crossfade: linear vs equal-power vs hand-tuned. Linear at the crossfade midpoint sounds attenuated (the +3 dB equal-power rule applies). Wrong crossfade = audible "dip" or "bump" at velocity boundaries.

Per-articulation envelope multipliers (Phase 28's locked rules) stacking with new Phase 37 multipliers: the multipliers must STACK MULTIPLICATIVELY, not REPLACE. Easy to get wrong.

**Why it happens:**
Determinism + alive-sounding samples are in tension. Crossfade math is famously tricky.

**How to avoid:**
- Round-robin index: deterministic counter per `(sequenceName, instrument, pitch)` tuple, reseeded at renderSong boundary. Reuse the existing reseed pattern.
- Velocity layer crossfade: equal-power (cos/sin), NOT linear. Test fixture renders a velocity sweep from 0-127, checks that RMS is smooth (no >1 dB step at layer boundaries).
- Articulation envelope multipliers stack multiplicatively. Phase 28's locked rules emit a *base envelope*; Phase 37's per-articulation multipliers apply ON TOP. Document the math: `final_envelope[t] = base_envelope[t] × articulation_multiplier[t]`. Lock in Phase 37 spec.
- Sampler regression: SPEC-8 RMS-windowed regression for the sampler-using showcase pieces (symphony.flow, ragtime.flow) must pass at ±0.5 dB / 100ms.

**Warning signs:**
- Two-run determinism check fails after enabling round-robin.
- Velocity sweep has audible step at layer boundary.
- Phase 28 articulations sound different after Phase 37 land — regression in envelope-multiplier stacking.

**Phase to address:** Phase 37 (sound design). Round-robin determinism + crossfade math are the locks.

---

### Pitfall 20: Documentation generator — incremental + doc-comment grammar conflicts

**What goes wrong:**
`flow doc` generates HTML/markdown from source comments. Current Flow has `//` single-line only — no `/** ... */` multi-line. Two options: (a) extend the lexer to support `/** ... */` (breaking change to the comment landscape), (b) use special leading-`///` syntax (single-line, multi-line repeated) — Rust-style.

Option (b) is the post-public-deprecation-friendly answer.

Runnable examples in docs: if examples execute as tests (and they should — broken example code is a documentation crime), the hermetic-test issues from Pitfall 15 apply.

Incremental regeneration: regenerate only what changed since the last invocation. Requires content hashing per source file + a manifest.

**Why it happens:**
Doc generators always look easy ("just parse the comments") and turn out to require half a compiler.

**How to avoid:**
- Adopt `///` doc-comment syntax (Rust-style). Three slashes for doc-eligible comment; two slashes is regular. Lexer change is small.
- Doc comments support a structured grammar: first line is summary, blank line, body markdown, optional `# Example` heading triggers example-extraction.
- Examples run through the test framework (Pitfall 15's hermetic guarantees). `flow doc` extracts examples, writes them to temp files, runs them in headless mode, fails if any example errors.
- Incremental: content-hash each `.flow` file, store in `.flow-doc-cache/`. Regenerate only stale entries. Cache invalidates on `flow` binary version change.

**Warning signs:**
- Examples in docs break silently — extraction-to-test path broken.
- Full regeneration takes >5 seconds on a small project — no incremental.
- `///` and `//` confuse the lexer — grammar conflict.

**Phase to address:** Phase 41 (`flow doc`). `///` syntax is the v1.5 lock; runnable examples gate quality.

---

### Pitfall 21: Modernized watch mode — stale closures + non-deterministic playback

**What goes wrong:**
Watch mode hot-swaps the source. A voice's envelope is closed-over the OLD `MusicalContext.Tempo`. New code redefines tempo. The voice's release envelope completes at the WRONG time. Audible glitch.

Filesystem watch race: editor saves twice in 50ms (some editors do this for atomic writes). Two reload events queue. The second reload starts before the first finishes. Race on the FlowEngine state.

The determinism contract: two recordings of the "same" live session won't be byte-identical because wall-clock dependencies are unavoidable. Composer expectation: live sessions are NOT under the determinism contract, but the boundary needs explicit documentation.

**Why it happens:**
Hot-reload is hard; live coding makes the wall clock load-bearing.

**How to avoid:**
- **Live mode opts out of determinism contract** (same call as Pitfall 12, lock in one place).
- File-watch debounce 200ms (same as Pitfall 12). Editors that save atomically need this anyway.
- Stale closures: at reload time, walk the voice pool. Voices whose authoring closure references a now-stale binding are *finished gracefully* (envelope completes against the OLD tempo) and not retriggered. New notes use the new tempo.
- Watch-mode reload happens at bar boundary (existing v1.0 Phase 5 commitment). Bar boundary respects the OLD tempo's bar duration — once the bar ends, new tempo applies.

**Warning signs:**
- Glitch on save → mid-bar reload.
- Multiple reloads queue → debounce broken.
- Composer surprised by non-determinism → documentation gap.

**Phase to address:** Phase 38 (watch mode modernization). Bar-boundary reload + 200ms debounce + opt-out documentation in one phase.

---

### Pitfall 22: Improvisation API distinguishability + style targeting without a corpus

**What goes wrong:**
Composer calls `(improvise sequence #jazz)`. Result sounds like `(? ...)` random choice with a different name. Composer asks "what's the difference between this and the random-choice operator?" Engineer mumbles "vibes."

Style targeting without a corpus or model: hand-crafted style libraries (a swing-rule table, a blues-scale chord-tone preference) work for narrow cases. Anything outside the hand-crafted scope sounds generic. Corpus-trained ML is out of scope for v1.5 (deps + size + license).

**Why it happens:**
"Improv" is a vague claim. Without a corpus, the only handle is "hand-coded musical heuristics." Most users will guess we used ML.

**How to avoid:**
- **Explicit distinguishability rule** (lock at Phase 36): `(? ...)` is uniform random choice (or weighted). `improvise` applies *style heuristics*: rhythmic displacement, target-pitch selection from current chord-tone set, voice-leading constraints, register matching. The output is non-random in the sense that "good" notes are preferred — random in the sense that within the constraints, choices are sampled.
- Hand-crafted style libraries: ship `#jazz`, `#blues`, `#classical` as v1.5 styles. Each style is a rule pack (e.g., `#jazz` = "prefer 3rd/7th chord tones, blue-note offset 50¢ down, swing rhythm displacement 67%"). Document the rules in the user manual so composers can predict output.
- Seeding: `improvise` takes a `#seed N` arg for reproducibility. Without seed, reseeded at renderSong boundary like all PRNGs.
- Defer corpus-trained ML to v1.6+.

**Warning signs:**
- Composer says "this just sounds random" — style rules too weak or too generic.
- Same input produces audibly identical output across styles — style rules not actually distinguishing.
- Determinism contract fails — seed handling broken.

**Phase to address:** Phase 36 (improv API). Style rule packs are the v1.5 scope.

---

### Pitfall 23: MIDI clock master/slave mode switching + tempo discovery

**What goes wrong:**
Flow is set as MIDI clock master, emits clock to hardware sequencer. Composer changes mode to slave (follow external clock from a drum machine) mid-piece. The previously-emitted clock and the about-to-be-received clock disagree on phase. Hardware sequencer drifts.

Tempo discovery vs imposing: as master, Flow imposes its `MusicalContext.Tempo`. As slave, Flow's `MusicalContext.Tempo` must follow incoming clock pulses (24 PPQN). What happens when the composer authored `tempo 120 { ... }` but the external clock is 130 BPM? Charitable interpretation: external clock wins, Flow's authored tempo becomes a no-op WHILE slave mode is active.

**Why it happens:**
MIDI clock is decades-old, well-specified, and still routinely misused.

**How to avoid:**
- Mode switching: only at bar boundary. Mid-bar switch is rejected (stderr warning, deferred to next bar).
- Discovery: as slave, sample 8 clock pulses to estimate tempo before declaring "synced." Below that, audio output is silent (don't emit garbage during sync).
- Tempo override in slave mode: authored `MusicalContext.Tempo` is ignored; stderr advisory on entry to a `tempo N { ... }` block.
- Both modes simultaneously: NO. Lock at "master XOR slave per session" at Phase 40 spec.

**Warning signs:**
- Audio silence at session start → sync delay.
- Drift between Flow and external sequencer → clock-pulse-counting off-by-one.
- Mode-switch glitch → mid-bar switch not gated.

**Phase to address:** Phase 40 (transport sync). Master/slave-exclusive lock at start of phase.

---

### Pitfall 24: Audio input — sample-rate mismatch + echo/feedback

**What goes wrong:**
Mic at 48 kHz, Flow internal at 44.1 kHz. Naive: feed 48 kHz samples to a 44.1 kHz pipeline. Result: pitched 8.84% sharp (factor 48/44.1).

Echo/feedback: speakers playing audio + mic open simultaneously. Mic captures playback. Mic feeds into Flow's effects chain. Flow renders the captured playback BACK to speakers. Audio loop, screaming feedback within seconds.

Latency: mic → ADC → Flow → DAC → speakers. Round-trip is buffer × 2 + Flow's processing. At 256-sample buffer at 44.1 kHz that's 11.6ms one-way, 23ms round-trip. Live-duet ergonomic threshold is ~20ms; above that, performers can't sync.

**Why it happens:**
Audio input is a system-level concern most languages handle poorly. PulseAudio + JACK + WASAPI + CoreAudio all have different capture conventions.

**How to avoid:**
- Sample-rate negotiation: query the input device's native rate; resample to Flow's internal rate via a Catmull-Rom interpolator (already exists in `loadWav` from Phase 22 varispeed). Same code path.
- Feedback prevention: when audio input is enabled, automatically apply a -20 dB safety attenuation on the playback path UNLESS the composer explicitly calls `(disableInputSafety)`. Stderr advisory on every session start.
- Latency expectation: document round-trip as "10-30ms typical, varies by buffer size and OS." Below 20ms is opt-in via small-buffer mode (256 or 128 samples) — same composer-facing pattern as WASAPI exclusive (Pitfall 16).

**Warning signs:**
- Captured audio plays back pitched — resampling missing.
- Feedback within seconds of enabling input — safety attenuation off.
- Composer says "I can't play in time" → latency too high.

**Phase to address:** Phase 38 (audio input). Safety attenuation + resampling reuse are the v1.5 locks.

---

### Pitfall 25: Generative primitives + determinism (Markov, L-system, cellular, Lorenz)

**What goes wrong:**
Markov chains, L-systems, cellular automata, and Lorenz attractors all involve randomness or chaotic sensitivity to initial conditions. Lorenz especially: a tiny floating-point difference in the initial condition compounds into wildly different output. Two-run determinism fails immediately.

Worse: a seeded Markov chain over a fixed alphabet still feels random for the first 100 steps, then settles into the stationary distribution — composer says "after 30 seconds it sounds like the same texture." Not actually random in the long run.

**Why it happens:**
Generative primitives are designed to *feel* alive. Determinism asks for byte-identical output. The reconciliation is: seedable but deterministic.

**How to avoid:**
- All generative primitives take an OPTIONAL `#seed N` arg. Unseeded → reseeded at renderSong boundary (same pattern as existing PRNG discipline). Two-run cmp-clean holds.
- Lorenz: use FIXED-precision arithmetic OR document that bit-identical reproducibility requires identical-platform FP semantics. Honest documentation: "Lorenz output is reproducible on the same platform; cross-platform reproducibility is best-effort." This is the lesser-evil because making Lorenz cross-platform-bit-identical is a multi-month project.
- Long-run staleness: expose generation length as a parameter; document that long runs converge to stationary behavior; suggest pattern algebra (Phase 36 Tidal-style) as the composition mechanism for variety across long timespans.
- Markov chain construction: composer-supplied transition tables; charitable interpretation for missing entries (uniform fallback) with stderr warning.

**Warning signs:**
- Two-run cmp-clean fails on generative-heavy showcase → seed pipeline broken.
- Lorenz output differs on different OS → cross-platform FP divergence (documented limitation).
- Composer says "it's boring after 30 seconds" → stationary-distribution UX issue.

**Phase to address:** Phase 36 (generative primitives). Seed discipline reuse + platform-FP-honest documentation.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Use NativeAOT-LLVM for WASM in Phase 41 without source-gen registry | Smaller WASM payload (~6 MB vs ~10 MB) | Months of trim-correction debugging; every new builtin breaks the build | Never in v1.5 — fall back to Mono-WASM jiterpreter |
| Use a 3rd-party MusicXML library (Music21, mxml.NET) | Faster Phase 39 export | New external dep; library disagreements with target consumers; loss of control over articulation mapping | Never — hand-roll the subset like the existing MIDI export |
| Skip ABC corpus regression (parse without ground-truth) | Faster Phase 39 ABC import shipping | Silent dialect-mismatch bugs surface after release | Never — corpus test is the only safety net |
| Naive `Random.NextDouble()` for granular jitter | Faster Phase 37 granular shipping | Determinism contract breaks on every shipped piece; CI flakes | Never — reuse the renderSong-boundary RNG reseed pattern |
| Treat `live` mode under determinism contract | Single mental model | Impossible to satisfy; composer support burden | Never — opt `live` mode out explicitly in docs |
| Single completion path for REPL + file context | Less code | REPL completion useless from day 1 | Never in v1.5 — two paths confirmed |
| `///` doc comments without lexer change | Faster `flow doc` ship | Doc comments mix with regular comments; can't extract reliably | Acceptable only if Phase 41 ships docs as Phase 35 follow-up, not v1.5 lock |
| Ship Phase 39 LilyPond export without engraver-warning CI | Faster ship | Output "looks wrong" in 30% of cases | Acceptable IF accompanied by HUMAN-UAT with a LilyPond-literate composer; otherwise never |
| Skip M/S compatibility test for stereo pan | Less CI time | Mono mix sounds broken in user reports | Never — mono compatibility is table stakes |
| Use external corpus-trained ML for `improvise` | "Sounds better" in demos | Multi-GB model, GPL or non-permissive license, deferred ship date | Never in v1.5 — handcrafted style libraries are the v1.5 scope |
| Per-test FlowEngine spin-up cost ignored | Simpler implementation | Test suite duration grows linearly with test count | Acceptable IF snapshot/restore added in v1.6 as optimization |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Ableton Link | Wire Link tempo into `MusicalContext.Tempo` directly | Link is playback-time input only; `writeWav`/`writeMidi` use authored tempo |
| Real-time MIDI output | Emit MIDI when audio buffer is queued | Emit at `bufferPlaybackStartTime + offset` using backend's reported latency |
| MusicXML consumers | Test only against MuseScore | Test against all three (MuseScore, Finale, Dorico); document Dorico tuplet limitations |
| LilyPond | Trust the parser ("it compiled") | Run `lilypond -dno-print-pages` in CI; engraver errors are the real signal |
| ABC import | Target ABC 2.1 strict | Target ABC 2.1 + abc2midi subset present in The Session corpus |
| MML import | Try to support all dialects | Lock at PC-98-era common core; gate dialects behind pragmas |
| OSC | Mix `,f` float32 and `,d` float64 | Single discipline: always `,d`. Document for receiver-side conversion |
| Pattern matching | Import Rust-style exhaustiveness strictness | Non-exhaustive warns + falls through (charitable); `enable matchExhaustive;` opts in |
| `live { ... }` block | Assume determinism contract holds | Document opt-out; bar-boundary reload; 30s evaluation cap |
| Cross-platform audio | Hard-code 256/512/1024 sample buffers | Query device preferred size; allocate as multiples |
| JetBrains Marketplace | One-shot submit-and-wait | Iterate over 1-2 weeks; ship direct-download fallback |
| Stereo pan | Hard-pan without mono-fold-down testing | Mono RMS within ±2 dB of stereo RMS is the CI gate |
| SFZ round-robin | `Random.Next()` for sample selection | Deterministic counter per `(seqName, instrument, pitch)`, reseeded at renderSong |
| Doc generator | Examples-as-strings (not executed) | Examples extracted → temp files → executed → fail-on-error |
| Watch mode | Closure capture of `MusicalContext` | Stale-closure detection at reload; graceful-finish for affected voices |
| Improv API | "It's like random but better" | Explicit rule packs with documented heuristics per style |
| MIDI clock | Allow master+slave simultaneously | Master XOR slave per session; mode switch at bar boundary only |
| Audio input | Open mic without playback safety | -20 dB attenuation default; opt-out for advanced users |
| Generative primitives | Unseeded `Random()` in Markov/L-system/cellular/Lorenz | All take optional `#seed N`; reseeded at renderSong boundary |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| WASM Mono-WASM jiterpreter startup cost | Browser page takes 3-5s to "boot" before first sound | Lazy-load stdlib; warm-up on user gesture; show progress | Page-load >5s on slow LANs (Phase 41) |
| Sysex over realtime MIDI thread | Audio underrun during patch dump | Sysex on separate thread/queue, non-realtime priority | First user dumps a 64KB patch at session start (Phase 40) |
| Phase vocoder FFT size too large | Pre-echo on every transient | Use 1024-sample FFT for general material; HPSS for mixed | Stretched drum buss has audible smearing (Phase 37) |
| Granular density >100 grains/sec at high overlap | CPU pegged; underruns | Document grain-count budget; default overlap = 4; warn at 8+ | Composer goes >100 grains/sec on a 4-voice patch (Phase 37) |
| OSC LFO at audio rate | LAN flooded; receivers drop messages | 200 Hz rate limit default; explicit override required | Composer wires `(sin t)` directly to an OSC param (Phase 38) |
| Pattern match guard re-evaluation | Slow `match` on expensive guards | Cache guard results within a single match expression via Thunk pattern | Composer uses `(loadWav)` in a guard (Phase 35) |
| Test suite no per-test FlowEngine | Tests >100 → suite duration linear in count | Reuse engine with snapshot/restore; default to shared, opt-out for isolated | Test suite reaches 500 tests (Phase 35) |
| MusicXML/LilyPond emit not streaming | High memory on long pieces | Stream emit to `TextWriter`, not String concat | Symphony >10 min (Phase 39) |
| Markov chain construction per-call | Repeated cost on long sequences | Memoize transition tables by content hash | Composer applies Markov to a 1000-bar sequence (Phase 36) |
| Watch mode rebuilds AST on every file save | Save-Save-Save spam pegs CPU | 200ms debounce + incremental AST rebuild | Rapid editor saves during live coding (Phase 38) |
| Cross-platform binary self-contained .NET 10 size | 80 MB Linux x64 binary | Trim unused stdlib; document size as expected | Composer surprised at install size (Phase 41) |
| Sampler cache load on first render per process | First-note latency on first render | Eager-load on engine startup (already done Phase 29); confirm same for SFZ (already done Phase 33) | Confirmed; regression risk in Phase 37 polish |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| MIDI input from untrusted USB device parsed without bounds checking | Buffer overflow in MIDI message handler | Validate message length per status byte; reject malformed |
| OSC server bound to 0.0.0.0 without auth | Anyone on LAN can drive Flow's params | Default-bind to 127.0.0.1; explicit `(oscBindAll)` to expose |
| Audio input recorded without user consent dialog | Privacy violation on shared systems | Document that audio input is opt-in; no auto-enable |
| WASM build allows arbitrary file loadWav | Browser sandbox bypass attempt via path traversal | Sanitize all path inputs to file APIs in WASM build; relative-only |
| JetBrains plugin downloads untrusted .flow files | Hostile .flow exploits interpreter bugs | Document that opening untrusted .flow files runs them; same as any IDE |
| `live` mode hot-swap from network filesystem | Race conditions, partial-file reads cause crashes | Watch only local paths; reject NFS/SMB mounts with stderr advisory |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| `match` exhaustiveness errors block composition | Composer can't write incomplete code | Warn + charitable fall-through; opt-in strict via pragma |
| Hot-reload glitches break flow state | Composer loses creative thread | Bar-boundary reload + state preservation + 200ms debounce |
| `improvise` without style docs feels magical/opaque | Composer can't predict output | Document each style's rule pack in the manual; sample outputs |
| MIDI clock sync without audio feedback during sync | Composer thinks audio is broken | Visual or audio cue ("waiting for sync...") during the 8-pulse settle |
| Cross-platform binary size 80 MB | "Why is this so big?" reactions | Document trimming attempts and the .NET 10 baseline; offer compressed download |
| WASM playground requires modern browser | Older-browser users see blank page | Detect via JS, show "Use Chrome/Firefox 120+" message |
| `live { ... }` opt-out from determinism is undocumented | Composer surprised at non-reproducibility | Stderr advisory on every `live` block entry: "Live mode: determinism contract suspended" |
| OSC requires manual IP/port config | No discovery → friction | Document `(oscClient ...)` syntax prominently; v1.6 zeroconf is acceptable defer |
| Pattern algebra (Tidal-style) shares operators with arithmetic | Confusing `+` precedence | Use distinct operators (e.g., `<|>` for choice, `++` for sequence) |
| Per-instrument pan defaults to center | Boring stereo image out of the box | Document "stereo by default" pan presets for orchestral pieces |

---

## "Looks Done But Isn't" Checklist

- [ ] **WASM playground (Phase 41):** Often missing trimming-correctness verification — verify every built-in is callable via in-browser smoke test exercising the full stdlib
- [ ] **Real-time MIDI output (Phase 40):** Often missing audio-MIDI latency alignment — verify by measuring trigger-to-sound latency against an oscilloscope (or visual check via DAW timeline)
- [ ] **MusicXML export (Phase 39):** Often missing cross-consumer testing — verify MuseScore + Finale + Dorico round-trip on at least 3 fixtures
- [ ] **LilyPond export (Phase 39):** Often missing engraver-warning check — verify `lilypond -dno-print-pages` exits clean in CI
- [ ] **ABC import (Phase 39):** Often missing corpus regression — verify 100 tunes from The Session import without parse errors
- [ ] **MML import (Phase 39):** Often missing dialect documentation — verify the supported common-core subset is enumerated in the user manual
- [ ] **Phase vocoder time-stretch (Phase 37):** Often missing transient-preservation test — verify drum-buss round-trip stretch has no audible smearing
- [ ] **Granular synthesis (Phase 37):** Often missing two-run cmp-clean — verify granular smoke test in SPEC-8 regression suite
- [ ] **Pattern matching (Phase 35):** Often missing type narrowing within arms — verify match arm's bound name has the narrowed type
- [ ] **Test framework (Phase 35):** Often missing per-test state reset — verify test 1 mutates the voice pool, test 2 sees a clean voice pool
- [ ] **`live { ... }` (Phase 38):** Often missing 30-second bailout — verify an infinite-loop reload kills the evaluation
- [ ] **Watch mode (Phase 38):** Often missing 200ms debounce — verify rapid saves queue only one reload
- [ ] **REPL completion (Phase 38):** Often missing partial-parse handling — verify completing `(transp` returns `transpose` ranked-1
- [ ] **Cross-platform audio (Phase 41):** Often missing device-buffer-size query — verify on a non-default-buffer USB interface
- [ ] **JetBrains plugin publish (Phase 41):** Often missing plugin verifier CI — verify `gradle runPluginVerifier` passes against multiple platform versions
- [ ] **Stereo pan (Phase 37):** Often missing mono-fold-down test — verify mono RMS within ±2 dB of stereo RMS
- [ ] **Sampler round-robin (Phase 37):** Often missing determinism check — verify two-run cmp-clean with round-robin enabled
- [ ] **Doc generator (Phase 41):** Often missing example-as-test execution — verify all examples in stdlib docs execute clean
- [ ] **Improvisation API (Phase 36):** Often missing style-rule documentation — verify each style has a documented rule pack
- [ ] **MIDI clock (Phase 40):** Often missing mode-switch boundary — verify mid-bar mode switch is rejected with stderr advisory
- [ ] **Audio input (Phase 38):** Often missing feedback safety attenuation — verify default -20 dB on playback when input is enabled
- [ ] **Generative primitives (Phase 36):** Often missing seed parameter — verify every primitive accepts optional `#seed N`
- [ ] **Ableton Link (Phase 40):** Often missing offline-render isolation — verify `writeWav` ignores Link state
- [ ] **OSC (Phase 38):** Often missing rate limit — verify default 200 Hz cap rejects flood
- [ ] **Rust-style diagnostics (Phase 35):** Often missing span migration for all AST nodes — verify a known bad-syntax example renders with arrow underline

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| WASM NativeAOT-LLVM breaks reflection | MEDIUM | Pivot to Mono-WASM jiterpreter; accept ~10 MB size; document v1.6 NativeAOT-LLVM as goal |
| Link tempo polluted offline render | LOW | Add isolation guard; existing PRNG reseed pattern is the precedent |
| MIDI latency mismatch | MEDIUM | Add backend `PreferredLatency` property; align emit to playback-start-time |
| MusicXML wrong in Dorico | LOW | Document the divergence; ship MuseScore-validated; defer Dorico-correctness to v1.6 |
| LilyPond engraver complaints | LOW | Run `lilypond -dno-print-pages` in CI; iterate on patterns; add a follow-up phase |
| ABC dialect rejection | MEDIUM | Expand corpus regression; add dialect-specific pragmas as additive opt-in |
| MML dialect explosion | LOW | Lock common core; defer dialect support behind pragmas |
| Phase vocoder smearing | LOW | Document `#psola` opt-in for percussion; encourage per-voice stretching |
| Granular determinism break | LOW | Reseed at renderSong boundary (existing pattern) |
| OSC type tag confusion | LOW | Document `,d`-only discipline; provide a converter sample |
| Pattern matching strict-mode user surprise | LOW | Default warns + falls through; pragma opts in |
| `live` mode non-determinism complaint | LOW | Document opt-out; stderr advisory on every entry |
| REPL completion ranks poorly | MEDIUM | Iterate the token heuristic on the 20-line regression test |
| Span migration incomplete | LOW | Defaulted-parameter pattern allows incremental migration |
| Test framework state pollution | MEDIUM | Add snapshot/restore; default to per-test isolation |
| Cross-platform audio dropouts | MEDIUM | Query preferred buffer size; document Windows shared-mode default |
| JetBrains Marketplace rejection | LOW | Iterate over 1-2 weeks; direct-download fallback ensures milestone closes |
| Stereo mono-fold breakage | LOW | Mono RMS test; if fail, soften extreme pan defaults |
| SFZ round-robin non-determinism | LOW | Deterministic counter pattern (existing PRNG seed reuse) |
| Doc generator example failures | LOW | Failing example fails the build; composer fixes the example |
| Improv API "sounds random" | MEDIUM | Add more rule packs; document each pack's rules |
| Watch-mode glitch | MEDIUM | Bar-boundary reload + stale-closure detection + graceful-finish |
| MIDI clock drift | MEDIUM | Implement 8-pulse settle; reject mid-bar mode switch |
| Audio input feedback | LOW | -20 dB safety attenuation default |
| Generative primitive long-run boredom | MEDIUM | Document the limitation; recommend pattern algebra for long pieces |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1. WASM .NET 10 trim breakage | Phase 41 | In-browser stdlib smoke test exercises every registered builtin |
| 2. Ableton Link offline-render pollution | Phase 40 | `writeWav` byte-identical run with Link peer vs without |
| 3. Real-time MIDI hot-plug + sysex timing | Phase 40 | Audio-MIDI latency measurement against external timestamp |
| 4. MusicXML cross-consumer divergence | Phase 39 | Three-DAW round-trip CI fixtures |
| 5. LilyPond engraver edge cases | Phase 39 | `lilypond -dno-print-pages` CI gate |
| 6. ABC dialect divergence | Phase 39 | 100-tune The Session corpus regression |
| 7. MML dialect scope creep | Phase 39 | Common-core lock documented in user manual |
| 8. Phase vocoder transient smearing | Phase 37 | Drum-buss round-trip stretch RMS test |
| 9. Granular determinism vs jitter | Phase 37 | Two-run cmp-clean in SPEC-8 regression suite |
| 10. OSC type tags + flood rate | Phase 38 | Wireshark byte-order verification + rate-limit CI |
| 11. Pattern match exhaustiveness | Phase 35 | Charitable default + `enable matchExhaustive;` pragma |
| 12. `live` block state preservation + bailout | Phase 38 | 30-second wall-clock cap test + infinite-loop bailout test |
| 13. REPL completion partial parse | Phase 38 | 20-line regression test, >80% rank-1 accuracy |
| 14. Rust-style diagnostic span migration | Phase 35 | Canonical bad-syntax baseline with hand-checked arrow underline |
| 15. Test framework hermetic isolation | Phase 35 | Snapshot/restore test (test 1 mutates pool, test 2 sees clean) |
| 16. Cross-platform audio backend | Phase 41 | USB-interface test on Windows + macOS |
| 17. JetBrains Marketplace publish | Phase 41 | `gradle runPluginVerifier` CI on multiple platform versions |
| 18. Stereo pan mono-fold | Phase 37 | Mono RMS within ±2 dB of stereo RMS CI gate |
| 19. Sampler round-robin determinism | Phase 37 | Two-run cmp-clean with round-robin enabled |
| 20. Doc generator example execution | Phase 41 | All stdlib examples extracted-and-executed in CI |
| 21. Watch mode stale closure + race | Phase 38 | 200ms debounce + stale-closure-detection test |
| 22. Improv API distinguishability | Phase 36 | Documented rule packs per style + composer A/B against random |
| 23. MIDI clock master/slave | Phase 40 | Mode-switch-at-bar-boundary test + 8-pulse settle |
| 24. Audio input feedback + sample rate | Phase 38 | Default -20 dB attenuation + resampling fixture |
| 25. Generative primitive seeding | Phase 36 | Every primitive accepts `#seed N`; two-run cmp-clean |

---

## Sources

- [.NET 10 WebAssembly Native AOT (Medium, 2026)](https://medium.com/@jacobscottmellor/webassembly-net-ea29c65c11a5)
- [Blazor WebAssembly AOT Compilation (Microsoft Learn 2026)](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0)
- [Improve Blazor WebAssembly AOT: trimming, reflection (dotnet/aspnetcore#64802)](https://github.com/dotnet/aspnetcore/issues/64802)
- [Ableton Link Documentation](https://ableton.github.io/link/)
- [Link features and functions FAQ (Ableton)](https://help.ableton.com/hc/en-us/articles/209776125-Link-features-and-functions-FAQ)
- [Real-Time Linux PREEMPT_RT (ProteanOS 2026)](https://proteanos.com/doc/real-time-linux-preempt-rt-latency-2026/)
- [HOWTO build a simple RT application (Linux Foundation)](https://wiki.linuxfoundation.org/realtime/documentation/howto/applications/application_base)
- [MusicXML state of play (vi-control 2024)](https://vi-control.net/community/threads/musicxml-the-state-of-play-in-2023-4.146417/)
- [Dorico MusicXML Export and Import](https://blog.dorico.com/musicxml-export-and-import/)
- [MusicXML import errors: MuseScore vs Dorico](https://musescore.org/en/node/367868)
- [Translating Finale projects to Dorico using MusicXML](https://blog.dorico.com/2024/09/finale-dorico-musicxml/)
- [Phase Vocoder Done Right (Pruusa + Holighaus, arXiv 2022)](https://arxiv.org/pdf/2202.07382)
- [Audio time stretching and pitch scaling (Wikipedia)](https://en.wikipedia.org/wiki/Audio_time_stretching)
- [A Review of Time-Scale Modification of Music Signals (Applied Sciences)](https://www.cs.bu.edu/fac/snyder/cs583/Literature%20and%20Resources/AReviewOfTimeScaleModification.pdf)
- [Improving the Phase Vocoder (Danish Sound Cluster 2023)](https://danishsoundcluster.dk/wp-content/uploads/2023/03/Danish_Sound_Vocoder_Report.pdf)
- [OSC 1.0 Specification (Stanford CCRMA)](https://opensoundcontrol.stanford.edu/spec-1_0.html)
- [Discovering OSC services with ZeroConf (CCRMA 2004)](https://ccrma.stanford.edu/groups/osc/publications/2004-Discovering-OSC-services-with-ZeroConf.html)
- Internal: `CLAUDE.md` (determinism contract, RMS-windowed regression, Phase 28 articulation rules, voice-pool, Phase 32 Tuning, Phase 33 SFZ)
- Internal: `.planning/MILESTONES.md` (v1.4 patterns established — two-run cmp-clean inheritance from Phase 18/25/27/33)
- Internal: `.planning/PROJECT.md` (constraints, Linux-first, minimal-dependency posture)
- Internal: Memory files `project_pre_public_no_legacy_burden` (post-public deprecation cycle), `feedback_charitable_interpretation` (load-bearing charitable interpretation), `project_v15_backlog` (carryover items)

---

*Pitfalls research for: v1.5 Stage, Studio, Web milestone*
*Researched: 2026-05-18*
