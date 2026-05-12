# Phase 28: MIDI + Audio Polyphony & Articulation Rewrite — Research

**Conducted:** 2026-05-10
**Sources:** SPEC, CONTEXT, source files in flow-lang/StandardLibrary/Audio + Parser + Lexer + Interpreter

## Executive Summary

Phase 28 has nine SPEC requirements spanning four cross-cutting concerns: parser surface (`{voice ...}`, `leg`, `voicePool`), articulation rendering (per-articulation rules at BarRenderer + per-articulation envelopes at all 9 synths), MIDI export topology (one track per Sequence), and test infrastructure (RMS-windowed regression + ragtime fixtures + manual UAT). The data flow is `Source → Parser → NoteStreamCompiler → BarRenderer → INoteSynthesizer.RenderNote → Voice → SongRenderer mix → AudioBuffer` for WAV and `Source → Parser → NoteStreamCompiler → SongData → MidiExport.ExportMidiInternal → MidiFile.Write` for MIDI. The two pipelines diverge at SongData; both consume `MusicalNoteData.Articulation`.

The architecture supports the rewrite cleanly:
- Voice mixing in `SongRenderer.MixVoicesToStereoBuffer` already sums voices additively — single-sequence polyphony only requires the parser/compiler to produce multiple parallel `Voice` objects sharing the same bar onset.
- `MusicalNoteData.Articulation` already plumbs from compiler to BarRenderer to MIDI; only the per-articulation rules and envelope shapes need to be expanded.
- `MidiExport.ExportMidiInternal` already iterates `sectionData.Sequences`, so refactoring single Track 1 into N per-sequence tracks is structural, not algorithmic.
- `MusicalContext` already has push/pop semantics via `_context.PushFrame()` / `_context.PopFrame()`, providing a direct template for `voicePool N { ... }`.

## Key Findings

### Finding 1: Voice-block parsing is a localized parser change, not a new IR layer

`Parser.NoteStream.cs:38-372` has a single `ParseNoteStream` method that walks elements until pipe-or-EOF. The brace dispatch at line 153 already handles `{N:M ...}q` tuplets. A `{voice ...}` arm parallel to that — recognizing `voice` keyword inside the brace and then recursively parsing children with the existing element loop — produces a new `VoiceBlockElement` AST node. The compiler in `NoteStreamCompiler.cs` then needs to:

1. Recognize `VoiceBlockElement` in `CompileBar`'s switch
2. Compile each block's children to a `BarData` using `ToTimeline()` semantics already present
3. Emit them as parallel `BarData` lists OR — cleaner — keep one `BarData` but tag each note with a `VoiceIndex` so all notes inside `{voice C5q D5q E5q F5q}` share an onset of 0 and step independently of `{voice C4w}` which holds for 4 beats

The simpler approach: emit one `BarData` per voice block, then `BarRenderer.RenderBarToVoices` is called once per block with the same beat offset, and `SongRenderer` already mixes them. This is the **"emit N parallel BarData per voice block, share lead bar onset"** strategy.

### Finding 2: BarRenderer's articulation switch (lines 67-77) is the locked-rules drop-in site

The current code applies articulation as a duration multiplier:
```csharp
case Articulation.Staccato: durationBeats *= 0.5; break;
case Articulation.Marcato:  durationBeats *= 0.8; break;
```

Phase 28 replaces these constants and adds Legato:
- Staccato: `× 0.25` (was 0.5)
- Marcato: `× 0.25` (was 0.8 — Marcato is now Staccato + Accent)
- Legato: `× 1.10` (new, plus crossfade)
- Tenuto: `× 1.00` (unchanged)
- Accent / Sforzando: `× 1.00` (unchanged duration, but velocity changes)

The duration multiplier is the **generic-layer** rule. Per-instrument envelope shaping happens AT THE SYNTHESIZER. The two layers compose: BarRenderer chooses how long the buffer is; the synthesizer shapes the envelope inside that buffer.

NoteStreamCompiler.cs:670-674 already applies velocity boosts for Accent/Marcato/Sforzando. Phase 28 extends:
- Accent: `+0.30` (was `+0.20` — locked at +30% per SPEC)
- Marcato: `+0.30` (already +0.30 — confirmed)
- Sforzando: spike +0.50 at attack, decay over first 15% of duration (currently `velocity = 0.95` is a static "loud" — needs envelope-shape rather than scalar velocity)

Sforzando is the only articulation that needs *time-varying velocity within a note* — which means it must be handled at the synthesizer envelope level, not just velocity scaling. The pragmatic split: NoteStreamCompiler stores `Articulation.Sforzando` on the note; the per-synth envelope shaper detects it and applies the spike-then-decay envelope shape.

### Finding 3: Per-instrument articulation envelopes via shared helper + per-synth tweak point

All 9 synthesizers share the same `SynthUtils.GenerateADSR(attack, decay, sustain, release, frames, sampleRate)` helper from `SynthUtils.cs:131-136`, which delegates to `EnvelopeProcessor.GenerateEnvelopeCurve`. The simplest plan:

1. Add a new helper `SynthUtils.GenerateArticulationADSR(MusicalNoteData note, double baseAttack, double baseDecay, double baseSustain, double baseRelease, int frames, int sampleRate)` that returns the articulation-shaped envelope.
2. Each synth replaces its `GenerateADSR(0.003, 0.6, 0.12, 0.3, ...)` call with `GenerateArticulationADSR(note, 0.003, 0.6, 0.12, 0.3, ...)`.
3. Inside the helper, the articulation rules apply:
   - Staccato → attack × 0.66 (1.5× faster), sustain = 0, release = baseRelease × 0.5
   - Tenuto → release × 1.2
   - Legato → sustain held, plus the *next-note crossfade* — which has to happen at the BarRenderer level since one synth call doesn't see the next note. So Legato envelope at synth = synth-default; legato crossfade = additive overlap from `BarRenderer.cs:81-86`'s existing tied-note 100ms-overlap mitigation generalized.
   - Accent → synth-default envelope (velocity boost is at velocity, not envelope)
   - Marcato → Staccato envelope (= Staccato applies at envelope; +30% velocity applies at velocity)
   - Sforzando → first 15% of frames get a 1.5× amplitude multiplier that decays linearly back to 1.0; rest of envelope is synth-default

This gives a SINGLE shared helper that all 9 synths call, with each synth keeping its own `attack/decay/sustain/release` baseline. Drum synth's articulation rules become no-ops because percussion envelope is already short — the helper detects `synthType == DrumSynth` (or via a flag passed by the synth) and returns the pristine ADSR.

The per-synth differentiation that the SPEC requires (the "FFT cosine similarity < 0.95 between Normal/Staccato pair" test) emerges naturally because each synth has different baseline attack/decay/sustain/release values — applying the same Staccato shaping rule yields measurably different waveforms.

### Finding 4: Multi-track MIDI export — refactor MidiExport.ExportMidiInternal

`MidiExport.cs:178-391` currently has a single `noteTrackChunk` accumulating `noteEvents` from all sequences. The refactor:

1. Replace the single `noteTrackChunk` and `noteEvents` with `Dictionary<string, (TrackChunk chunk, List<TimedEvent> events, int gmProgram)>` keyed by sequence name.
2. Per-sequence GM program lookup table:
   - `piano` → 0
   - `brass` / `horn` → 56
   - `sax` / `saxophone` → 65
   - `flute` → 73
   - `strings` / `string` → 48
   - `organ` → 19
   - `bell` → 14 (tubular bells)
   - `drums` / `drum` → channel 9 (special — channel 9 = GM percussion)
   - `wavetable` → 0 (default to piano-like; user-configurable in future)
   - `sine`/`saw`/`square`/`triangle` → 80, 81, 80, 80 (synth lead family)
3. The instrument name comes from the synthType passed to `renderSong` — but `writeMidi` doesn't take a synthType! 

This is the **gap the SPEC implicitly assumes is filled**: MIDI export needs to know which synthesizer maps to which sequence. Currently `writeMidi(filepath, song)` only sees the song. For Phase 28, the cleanest path is:
- Store `synthName` on each `SequenceData` (it's nullable, defaults to "piano")
- Composer-facing API: `instrument(seq, "brass") -> Sequence` setter (already exists? — verify)
- Or via section context: a `instrument "brass" { ... }` musical-context block that tags all sequences declared inside

For Wave 2 of Phase 28, **lock the simpler choice**: each `SectionData.Sequences` Dictionary is keyed by a sequence name — the convention is to name the sequence with its instrument (e.g. `Sequence piano = | ... |`). The MIDI export reads the dictionary key as a hint, falling back to GM 0. This avoids touching the Sequence type system and stays inside MIDI export's own surface. **This is planner discretion territory** — the SPEC says "the synthesizer name registered for that sequence's render" but doesn't lock how the registration happens. The plan must propose a concrete mechanism.

Recommended: the MIDI export reads sequence-key→GM-program from a built-in lookup table; the lookup is name-based with case-insensitive prefix matching ("piano" matches "piano_lh" and "piano_rh" → both map to piano GM 0). Composer can override with a special meta-comment in the future, but that's deferred.

Cross-section same-name sequences concatenating (per SPEC) — already supported by the loop structure in `ExportMidiInternal:255-381`. The `for (sectionRef ...)` outer loop with `for (repeat ...)` inner loop, with `seqTick = sectionStartTick` inside, means events for "melody" in section 1 and "melody" in section 2 already accumulate at sequential ticks. Phase 28 just needs the dict-keyed-by-seqName routing to map to the same TrackChunk for both occurrences.

### Finding 5: Voice-pool with steal-oldest is a clean refactor of the existing `VoiceAllocator`

`VoiceAllocator.Allocate` (85 lines, lines 1-85) currently sorts by peak amplitude descending and keeps the loudest. Phase 28 changes the policy from "keep loudest" to "steal oldest" — meaning when the pool is full, the voice with the longest already-elapsed playtime is truncated.

But "elapsed playtime" requires temporal ordering. The current algorithm is offline (all voices presented at once, no time concept). Phase 28's voice-pool semantics live at the **timeline-projection** layer, not the post-render allocation layer:

```
Onset-ordered iteration:
  For each note onset (in time order):
    If active_voice_count < poolSize:
      allocate new voice
    Else:
      find voice with smallest (currentTime - voice.OnsetTime)
      → that's the "oldest"; truncate its remaining samples at currentTime
      reuse its slot for the new voice
    Track active voices: voice exits the pool when its samples end
```

The cleanest implementation is a new method `VoiceAllocator.AllocateWithPool(List<Voice> voices, int sampleRate, int poolSize)` that:
1. Sorts voices by `OffsetBeats` (onset time)
2. Walks the sorted list, maintaining a `priority queue` of active voices keyed by `OnsetBeats + DurationBeats` (when they free up)
3. When current voice would exceed `poolSize`, find the active voice with the EARLIEST onset and truncate it at the new voice's onset
4. Returns the (modified) voices list

The default 1024-voice cap in `RenderSequenceToVoices` (lines 35, 45, 95, 107) becomes the new pool default — but per SPEC the LOCKED default is 32. Update the default; preserve `maxVoices` parameter for backward compat (passing `1024` continues to work; passing nothing now defaults to 32; `voicePool N` block overrides).

The `voicePool N { ... }` musical-context block: add `MusicalContextType.VoicePool`, lex `voicePool` keyword, parse `voicePool` + integer + `{`, push pool size onto context stack. SequenceRenderer reads `MusicalContext.VoicePoolSize` (new field, nullable) and passes to `VoiceAllocator.AllocateWithPool`.

### Finding 6: RMS-windowed test infrastructure is straightforward Math + WAV reader

The test pattern is:
```
AssertRmsWithinTolerance(rendered, baselineWavPath, windowMs: 100, toleranceDb: 0.5)
```

Implementation:
1. Read baseline WAV (existing `FileIO.cs` writes WAV; reverse-engineer the reader OR use `BinaryReader` on the RIFF format — the project already understands WAV).
2. Verify frame counts match exactly (rendered.Frames == baseline.Frames). Different frame count = test FAILS unconditionally.
3. Slice each buffer into 100ms windows: `windowSamples = sampleRate * 0.100 = 4410 samples at 44.1 kHz`
4. For each window, compute RMS = `sqrt(mean(samples²))`
5. Compute dB difference: `dB = 20 × log10(rendered_rms / baseline_rms)` clamped to avoid log(0)
6. Assert `abs(dB) ≤ toleranceDb`. On failure, report `"RMS deviation in window {N} ({startMs}-{endMs}ms): expected {baselineDb}, got {renderedDb}"`

Lives at `flow-lang.Tests/Helpers/RmsRegressionTests.cs` as a static helper class. Per-test override via overload `AssertRmsWithinTolerance(rendered, baseline, windowMs, toleranceDb, reason: "Phase X envelope shaping legitimately exceeds default tolerance")`.

The negative test fixture: a piece of test code that intentionally renders Staccato at 100% duration (e.g. by patching the BarRenderer constant) and asserts the helper's failure message contains the per-window dB values.

### Finding 7: Ragtime fixtures + manual UAT have specific authorial constraints

Synthetic fixture `examples/tests/ragtime_polyphony.flow`: 4 bars exercising
- Bar 1: held + running (`| {voice C2w} {voice C5q E5q G5q E5q} |`)
- Bar 2: staccato + sustained pedal (`| {voice C2w} {voice C5q stacc D5q stacc E5q stacc F5q} |`)
- Bar 3: legato across tuplets (`| {voice C2h F2h} {voice C5q leg D5q leg {3 E5 F5 G5}q} |`)
- Bar 4: mixed-articulation chord (`| [C4 E4 G4]q stacc [C4 E4 G4]q ten [C4 E4 G4]q. >`)

Maple Leaf Rag opening fixture `examples/tests/maple_leaf_opening.flow`: first 8 bars of Joplin's 1899 Maple Leaf Rag (public domain). The piece is in Ab major, 2/4 time, ragtime stride. Hand-transcription: the LH plays a low-bass-then-mid-chord oom-pah pattern (octave-then-triad), the RH plays syncopated melody. SPEC requires both LH stride and RH syncopated melody with audible separation.

Manual UAT in `28-VERIFICATION.md`: a "Manual UAT Sign-off" section with two checkboxes. The phase-closure script (or human reviewer) refuses to mark the phase complete while either checkbox is empty.

## Validation Architecture

This section structures the validation strategy for VALIDATION.md per Nyquist Dimension 8.

**Sampling tier** — at what fidelity is each requirement validated?

| Requirement | Acceptance gate | Sampling fidelity | Test type |
|---|---|---|---|
| 1. Voice-block syntax | Parse + render + MIDI emit | Unit (parser) + Integration (render-to-WAV) | xUnit Fact (parser) + Fact (RMS regression) |
| 2. Held-note non-truncation | RMS first 50ms vs last 50ms ratio ≥ 0.5 | Integration | Fact (RMS regression) |
| 3. Articulation.Legato enum value | grep on NoteType.cs + parser fact | Unit | Fact (parser) |
| 4. Locked articulation envelope rules | ±5% audible-duration, ±2 velocity units | Unit (per-articulation 6 facts) | Fact per articulation |
| 5. Per-instrument articulation envelopes | FFT cosine similarity < 0.95 Normal/Staccato | Unit (54 facts: 6 art × 9 synth) | Fact per (synth, articulation) |
| 6. Multi-track MIDI export | DryWetMidi load + Chunks.Count == 1 + N | Integration | Fact (MIDI structure) |
| 7. Voice-pool allocation | 50-onset stress test → 32 voices, oldest stolen | Integration | Fact (counter-instrumented render) |
| 8. RMS-windowed test infra | Helper exists + positive + negative fact | Unit | Helper class facts |
| 9. Manual UAT + ragtime fixtures | Composer renders + listens + signs off | Manual | Checkbox in 28-VERIFICATION.md |

**Coverage strategy:**
- **Necessary** (must exist): every SPEC requirement has at least one fact.
- **Sufficient** (no gaps): the 19 SPEC acceptance-criteria checkboxes form a complete grid; each maps to an automated test (#1–#15) or manual UAT (#16–#19).

**Falsifiability check:**
- Every fact must FAIL when the implementation is wrong. The SPEC's negative-test requirement (#8 — intentional-regression triggers diagnostic) is the canonical falsifier for the RMS infra itself. Each per-articulation fact is falsifiable by setting the wrong duration multiplier in BarRenderer.

**Validation completeness gate (planning level):**
- After plans are finalized, the plan-checker confirms each Phase 28 SPEC requirement (1–9) is addressed by at least one plan. This is enforced via the `requirements: [SPEC-1, ...]` frontmatter convention.

## Risk Surfaces

### R1: Voice-block + tuplet interaction
A voice-block containing a tuplet (`{voice {3 C4 D4 E4}q}`) requires the parser to dispatch to `ParseTupletChildren` from inside the voice-block parser. Both use `{` … `}` — disambiguation needs a keyword check at the brace open. Plan the parser to peek for `voice` keyword after `{`.

### R2: Voice-pool determinism
Steal-oldest selection must be deterministic across runs. If two voices have identical onset, the tiebreaker (e.g. lower voice index) must be specified. Without a tiebreaker, the existing two-run determinism contract breaks.

### R3: Sforzando spike envelope ≠ velocity-only
Sforzando's "+50% spike at attack, decay back to base over first 15% of duration" is a TIME-VARYING amplitude shaper, not a single velocity number. NoteStreamCompiler's current `velocity = 0.95` static assignment is wrong for Phase 28. The envelope-shaper helper must consume `Articulation.Sforzando` directly.

### R4: MIDI multi-track ProgramChange channel assignment
Drum sequence requires MIDI **channel 9** (zero-indexed) — DryWetMidi assigns channel via `NoteOnEvent.Channel`. The plan must explicitly assign channel-per-track and route drums to channel 9.

### R5: RMS test baseline freshness
Baselines committed to `flow-lang.Tests/baselines/Phase28/` must be regenerated whenever the implementation legitimately changes. The plan must include a "regenerate baselines" command/script and document the "when to regen" rule (only after manual UAT confirms audibly-correct).

### R6: Backward compat for legato(Sequence, Double) transform
The Phase 22 transform must continue to set `DurationOverlap` AND now also set `Articulation.Legato`. The existing test suite (`LegatoFacts`) must stay green.

## Code Excerpts (for plan author reference)

### BarRenderer articulation switch (current, will be rewritten)
```csharp
// flow-lang/StandardLibrary/Audio/BarRenderer.cs:67-77
switch (note.Articulation)
{
    case Articulation.Staccato: durationBeats *= 0.5; break;
    case Articulation.Marcato:  durationBeats *= 0.8; break;
    // Normal, Tenuto, Accent, Sforzando don't shorten duration
}
```

### NoteStreamCompiler articulation velocity (current, will be rewritten)
```csharp
// flow-lang/Runtime/NoteStreamCompiler.cs:670-674
if (articulation == Articulation.Accent)
    velocity = Math.Min(velocity + 0.2, 1.0);
else if (articulation == Articulation.Marcato)
    velocity = Math.Min(velocity + 0.3, 1.0);
else if (articulation == Articulation.Sforzando)
    velocity = 0.95;
```

### Articulation enum (current — needs `Legato` added)
```csharp
// flow-lang/TypeSystem/SpecialTypes/NoteType.cs:200-208
public enum Articulation
{
    Normal, Staccato, Tenuto, Marcato, Accent, Sforzando
    // Phase 28: ADD `Legato` here
}
```

### MidiExport main loop structure (current — single track)
```csharp
// flow-lang/StandardLibrary/Audio/MidiExport.cs:245-251
var noteTrackChunk = new TrackChunk();
var noteEvents = new List<TimedEvent>();
noteEvents.Add(new TimedEvent(new ProgramChangeEvent((SevenBitNumber)0), 0));
// ... single track accumulates all sequences
```

### MusicalContextStatement dispatch (current — pattern for VoicePool)
```csharp
// flow-lang/Interpreter/Interpreter.cs:135-306
// _context.PushFrame() / try { ... } finally { _context.PopFrame() }
// Phase 28 adds `case MusicalContextType.VoicePool:` with int validation 1..256
```

### TryParseArticulation (current — needs `leg` token)
```csharp
// flow-lang/Parsing/Parser.NoteStream.cs:397-421
private Articulation? TryParseArticulation()
{
    if (Check(TokenType.GreaterThan)) { ... return Accent; }
    if (Check(TokenType.Identifier))
    {
        switch (text)
        {
            case "stacc": ... return Staccato;
            case "ten":   ... return Tenuto;
            case "marc":  ... return Marcato;
            // Phase 28: ADD `case "leg": ... return Legato;`
        }
    }
    return null;
}
```

## Existing Test Patterns to Mirror

- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — Two-run determinism gate. Pattern: render twice with `FlowEngineRunner`, `byte[] bytes1.SequenceEqual(bytes2)`. Phase 28 preserves this (per `Constraints` in SPEC); Phase 28's NEW pattern is RMS-windowed comparison, which sits alongside.
- `flow-lang.Tests/Unit/Phase26_2/` — recent unit test directory pattern, organize Phase 28 unit Facts under `flow-lang.Tests/Unit/Phase28/` and Integration Facts under `flow-lang.Tests/Integration/Phase28/`.

## RESEARCH COMPLETE

This research informs the Phase 28 plans, which decompose into 7 plans across 3 waves:

- Wave 1 (parallel): parser+lexer surface, articulation rules at compiler/BarRenderer
- Wave 2 (depends on Wave 1): per-synth envelope shaping, multi-track MIDI export, voice-pool allocator
- Wave 3 (depends on Wave 2): RMS test infrastructure + ragtime fixtures, closure (UAT, ROADMAP/STATE updates)
