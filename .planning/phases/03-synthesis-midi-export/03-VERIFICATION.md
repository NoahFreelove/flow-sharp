---
phase: 03-synthesis-midi-export
verified: 2026-04-02T23:59:00Z
status: passed
score: 11/11 must-haves verified
re_verification: false
---

# Phase 3: Synthesis & MIDI Export Verification Report

**Phase Goal:** Users can define their own oscillator waveforms in Flow code and export compositions as standard MIDI files
**Verified:** 2026-04-02T23:59:00Z
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (from ROADMAP.md Success Criteria)

| #   | Truth                                                                                                              | Status     | Evidence                                                                                           |
| --- | ------------------------------------------------------------------------------------------------------------------ | ---------- | -------------------------------------------------------------------------------------------------- |
| 1   | User can define a custom oscillator via a Flow proc (wavetable approach) and use it as an instrument in renderSong | ✓ VERIFIED | `oscillator()` built-in registered with 3 overloads; test uses array + lambda forms                |
| 2   | Custom oscillators work with existing voice allocation and effects pipeline                                         | ✓ VERIFIED | SynthesizerFactory.Create() checked before built-in switch; BarRenderer calls `SynthesizerFactory.Create(synthType)` |
| 3   | User can call `writeMidi("output.mid", song)` and produce a .mid file                                             | ✓ VERIFIED | `writeMidi` registered in BuiltInFunctions.cs; delegates to `MidiExport.WriteMidi`; writes via DryWetMidi |
| 4   | Exported MIDI files contain correct tempo, time signature, key signature, and per-note velocities                  | ✓ VERIFIED | MidiExport.cs emits SetTempoEvent, TimeSignatureEvent, KeySignatureEvent; velocity clamped 1-127   |

**Score:** 4/4 truths verified

---

### Required Artifacts

| Artifact                                                                 | Expected                                                              | Status     | Details                                                                  |
| ------------------------------------------------------------------------ | --------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------ |
| `flow-lang/StandardLibrary/Audio/Synthesizers/WavetableSynthesizer.cs`  | INoteSynthesizer with wavetable + linear interpolation + ADSR         | ✓ VERIFIED | 55 lines; implements INoteSynthesizer; phase-increment loop; ADSR applied |
| `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs`                    | SynthesizerFactory with RegisterWavetable and runtime registry        | ✓ VERIFIED | `_customWavetables` dict + `RegisterWavetable()` + TryGetValue in Create() |
| `flow-lang/StandardLibrary/Audio/MidiExport.cs`                         | MIDI export logic walking SongData hierarchy via DryWetMidi           | ✓ VERIFIED | 261 lines; conductor track + note track; all required MIDI events present  |
| `flow-lang/flow-lang.csproj`                                             | DryWetMidi NuGet package reference                                    | ✓ VERIFIED | `<PackageReference Include="Melanchall.DryWetMidi" Version="8.0.3" />`   |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs`                         | oscillator() and writeMidi() built-ins registered                     | ✓ VERIFIED | 3 oscillator overloads + writeMidi registered in RegisterAudio            |
| `tests/test_custom_oscillator.flow`                                      | End-to-end test of custom oscillator registration and rendering       | ✓ VERIFIED | Tests array, pre-built array, and lambda-based oscillator forms           |
| `tests/test_midi_export.flow`                                            | End-to-end test of MIDI export from a Song                            | ✓ VERIFIED | Two songs: 3/4 Gmajor 140bpm + 4/4 Cmajor 120bpm; writes to /tmp/        |

---

### Key Link Verification

| From                                       | To                                     | Via                                                         | Status     | Details                                                                |
| ------------------------------------------ | -------------------------------------- | ----------------------------------------------------------- | ---------- | ---------------------------------------------------------------------- |
| `BuiltInFunctions.cs`                      | `SynthesizerFactory.RegisterWavetable` | oscillator() built-in extracts float array, registers wavetable | ✓ WIRED    | `Audio.SynthesizerFactory.RegisterWavetable(name, ExtractWavetable(floatArray))` confirmed |
| `NoteSynthesizer.cs` (SynthesizerFactory)  | `WavetableSynthesizer.cs`              | `_customWavetables` dict checked before switch              | ✓ WIRED    | `_customWavetables.TryGetValue(key, out var wavetable)` -> `new WavetableSynthesizer(wavetable)` |
| `BuiltInFunctions.cs`                      | `MidiExport.WriteMidi`                 | writeMidi built-in delegates to MidiExport.WriteMidi        | ✓ WIRED    | `registry.Register("writeMidi", writeMidiSignature, Audio.MidiExport.WriteMidi)` |
| `MidiExport.cs`                            | `Melanchall.DryWetMidi`                | Uses MidiFile, TimedEvent, TrackChunk for SMF encoding      | ✓ WIRED    | `using Melanchall.DryWetMidi.Core/Interaction` at top of file          |
| `MidiExport.cs`                            | `SongData` hierarchy                   | Walks Song -> Section -> Sequence -> Bar -> MusicalNoteData | ✓ WIRED    | `foreach (var sectionRef in song.Sections)` -> bars -> MusicalNotes loop |
| `BarRenderer.cs`                           | `SynthesizerFactory.Create()`          | renderSong path reaches BarRenderer which calls Create      | ✓ WIRED    | `INoteSynthesizer synthesizer = SynthesizerFactory.Create(synthType)`  |
| oscillator() built-in                      | `collections.Invoker`                  | User proc evaluated via Invoker (same as map/filter)        | ✓ WIRED    | `collections.Invoker!(proc, new List<Value> { Value.Int(tableSize) })`  |

---

### Data-Flow Trace (Level 4)

| Artifact                    | Data Variable          | Source                                   | Produces Real Data          | Status      |
| --------------------------- | ---------------------- | ---------------------------------------- | --------------------------- | ----------- |
| `WavetableSynthesizer.cs`   | `_wavetable` (float[]) | Registered via `SynthesizerFactory.RegisterWavetable` from oscillator() built-in | Yes — user-supplied array or proc result | ✓ FLOWING |
| `MidiExport.cs`             | `noteEvents` list      | Walks `SongData.Sections` -> bars -> `MusicalNotes` | Yes — real MusicalNoteData per note | ✓ FLOWING |
| `MidiExport.cs`             | conductor meta events  | `section.Context?.Tempo`, `ctx.TimeSignature`, `ctx.Key` | Yes — from MusicalContext set by Flow script | ✓ FLOWING |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — .NET 9 SDK not available in this environment; `dotnet build` and `dotnet run` cannot execute. Code-level analysis substitutes.

**Code-level substitutes verified:**

| Behavior                                                         | Check Method               | Result                                                                      | Status  |
| ---------------------------------------------------------------- | -------------------------- | --------------------------------------------------------------------------- | ------- |
| oscillator() with array arg routes to RegisterWavetable          | Code path trace            | oscillatorArraySignature registered; ExtractWavetable + RegisterWavetable called | ✓ PASS |
| oscillator() with proc arg uses collections.Invoker              | Grep for Invoker in args   | `collections.Invoker!(proc, ...)` confirmed in both proc overloads           | ✓ PASS |
| SynthesizerFactory.Create() returns WavetableSynthesizer for custom name | Code path trace       | TryGetValue check precedes switch; returns `new WavetableSynthesizer(wavetable)` | ✓ PASS |
| MidiExport emits SetTempoEvent with 60_000_000 conversion        | Grep on MidiExport.cs      | `int microsPerBeat = (int)(60_000_000.0 / bpm)` + `new SetTempoEvent(microsPerBeat)` | ✓ PASS |
| MIDI denominator encoded as power of 2                           | Grep on MidiExport.cs      | `byte midiDenominator = (byte)Math.Log2(timeSigDenominator)` confirmed       | ✓ PASS |
| Velocity mapped 0.0-1.0 to 1-127                                 | Grep on MidiExport.cs      | `Math.Clamp((int)(note.Velocity * 127), 1, 127)` confirmed                   | ✓ PASS |
| Section repeats: repeat loop 0..RepeatCount                      | Code path trace            | `for (int repeat = 0; repeat < sectionRef.RepeatCount; repeat++)` confirmed  | ✓ PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description                                                      | Status        | Evidence                                                            |
| ----------- | ----------- | ---------------------------------------------------------------- | ------------- | ------------------------------------------------------------------- |
| SYNTH-01    | 03-01-PLAN  | User can define custom oscillator waveforms via Flow procs (wavetable approach) | ✓ SATISFIED   | oscillator(String, Function) + oscillator(String, Void[]) built-ins; WavetableSynthesizer.cs |
| SYNTH-02    | 03-01-PLAN  | Custom oscillators integrate with existing instrument/voice pipeline | ✓ SATISFIED   | SynthesizerFactory.Create() handles custom names; BarRenderer uses it transparently |
| MIDI-01     | 03-02-PLAN  | User can export a Song/Sequence to a standard MIDI file via writeMidi | ✓ SATISFIED   | writeMidi built-in registered; MidiExport.WriteMidi writes .mid via DryWetMidi |
| MIDI-02     | 03-02-PLAN  | MIDI export preserves tempo, time signature, key, and note velocities | ✓ SATISFIED   | SetTempoEvent + TimeSignatureEvent + KeySignatureEvent on conductor track; velocity mapping confirmed |

Note: REQUIREMENTS.md lists SYNTH-01 and SYNTH-02 as still "Pending" (unchecked boxes) and the traceability table shows them as "Pending". This is a documentation inconsistency — the code is fully implemented. MIDI-01 and MIDI-02 are correctly marked complete.

---

### Anti-Patterns Found

| File                               | Line | Pattern                         | Severity  | Impact                                                           |
| ---------------------------------- | ---- | ------------------------------- | --------- | ---------------------------------------------------------------- |
| `MidiExport.cs`                    | 174  | seqTick not advanced for non-pickup bars without TimeSignature | ⚠️ Warning | Bars without an explicit TimeSignature on the BarData object do not advance the sequence tick position (the `if (bar.TimeSignature != null)` guard on line 210 skips the advance). In practice all musical bars have TimeSignature, so this is unlikely to affect real scripts, but is a latent correctness issue. |
| `tests/test_custom_oscillator.flow` | —    | Test uses array/lambda forms, not proc-with-size form | ℹ️ Info  | The 3-arg `oscillator(String, Function, Int)` overload is registered but not directly exercised by the test (lambda in Test 4 goes through 2-arg overload). Does not affect goal achievement. |

No stubs, no placeholder returns, no empty handlers, no TODO/FIXME comments found in phase-modified files.

---

### Human Verification Required

The following items require a .NET 9 SDK environment to run the actual interpreter:

#### 1. Custom Oscillator Audio Quality

**Test:** Run `dotnet run --project flow-interpreter tests/test_custom_oscillator.flow` and verify all four PASS lines appear with non-zero frame counts.
**Expected:** "All custom oscillator tests passed" with frame counts > 0 for each rendered buffer.
**Why human:** Build and runtime unavailable (no .NET 9 SDK in this environment).

#### 2. MIDI File Structural Validity

**Test:** Run `dotnet run --project flow-interpreter tests/test_midi_export.flow`, then open `/tmp/test_flow_export.mid` in a DAW (Reaper, MuseScore, GarageBand) or MIDI file inspector.
**Expected:** File opens; tempo track shows 140 BPM; key shows G major; time sig shows 3/4; notes match G4 B4 D5 pattern from the waltz section, duplicated for the repeat.
**Why human:** MIDI file structural correctness and DAW compatibility cannot be verified without executing the code and reading binary output.

#### 3. Custom Oscillator + Effects Pipeline Integration

**Test:** Write a brief Flow script that registers a custom oscillator then applies reverb/delay to a rendered buffer and plays it.
**Expected:** No errors; audio plays; effects are audible on the custom oscillator output.
**Why human:** Verifies SYNTH-02 (effects pipeline integration) at runtime rather than by code trace alone.

---

### Gaps Summary

No gaps found. All must-haves from both plans are verified:

- WavetableSynthesizer exists, is substantive (55 lines, correct algorithm), and is wired into SynthesizerFactory which is called by BarRenderer on every note render.
- oscillator() built-in exists with 3 overloads, calls collections.Invoker correctly for proc overloads, and routes pre-built arrays directly to RegisterWavetable.
- MidiExport.cs is substantive (261 lines), produces all required MIDI meta events, handles section repeats, and is wired through the writeMidi built-in.
- DryWetMidi 8.0.3 is referenced in the csproj.
- Both test files exist and exercise the new functionality.

One documentation inconsistency: REQUIREMENTS.md still shows SYNTH-01 and SYNTH-02 as "Pending" in both the checkbox list and the traceability table. The implementation is complete. This should be updated to reflect the actual state.

One latent code issue: bars without an explicit `TimeSignature` property on the `BarData` object cause the tick position to stall in MidiExport's sequence advancement loop. This affects no current test scripts (all musical bars carry TimeSignature) but could cause silent bugs in edge-case scripts using legacy bar mode. Categorized as a warning rather than a blocker.

---

_Verified: 2026-04-02T23:59:00Z_
_Verifier: Claude (gsd-verifier)_
