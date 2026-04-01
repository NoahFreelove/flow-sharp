# Domain Pitfalls

**Domain:** Audio programming language extension (Flow language, C#/.NET 9 interpreter)
**Researched:** 2026-03-29

## Critical Pitfalls

Mistakes that cause rewrites or major issues.

### Pitfall 1: Custom Oscillator Performance Death Spiral
**What goes wrong:** Calling a Flow `proc` per-sample for custom oscillators. At 44100 Hz sample rate, a 4-second note requires 176,400 interpreter evaluations. With the overhead of Flow's interpreter (scope creation, type checking, overload resolution), this will be 100-1000x slower than native C# synthesis.
**Why it happens:** Natural first implementation: "just call the user's function for each sample."
**Consequences:** Custom oscillator notes take seconds to render instead of milliseconds. Users will think the feature is broken.
**Prevention:** Block-based evaluation. Call the user proc once with a buffer/array parameter, or provide a wavetable approach: user proc generates one cycle of the waveform (e.g., 1024 samples), then the engine resamples that wavetable at the desired frequency. The wavetable approach is how most real synthesizers work.
**Detection:** Profile custom oscillator rendering early. If a single note takes > 50ms to render, the approach needs rethinking.

### Pitfall 2: WAV Import Format Assumptions
**What goes wrong:** Assuming all WAV files are 16-bit PCM stereo at 44100 Hz. Real-world WAV files come in many formats: 8/16/24/32-bit PCM, 32/64-bit IEEE float, mono/stereo/multichannel, various sample rates (22050, 44100, 48000, 96000).
**Why it happens:** The existing WAV writer only writes specific formats, so the reader mirrors those assumptions.
**Consequences:** Users try to load a 24-bit WAV from their DAW and get garbage audio or a crash.
**Prevention:** Parse the `fmt` chunk properly: read format code (1=PCM, 3=IEEE float), bits per sample, channel count, sample rate. Convert all formats to the internal float32 mono/stereo at 44100 Hz. Handle format mismatches gracefully with resampling (linear interpolation is adequate for v1; sinc resampling later).
**Detection:** Test with WAV files from different DAWs and sources. Audacity exports in various formats and is good for generating test files.

### Pitfall 3: Loop Constructs Without Break/Continue
**What goes wrong:** Implementing `for`/`while` without `break` and `continue` support. Users immediately hit cases where they need early exit ("play notes until we hit a rest") or skip iterations ("skip every other note").
**Why it happens:** Break/continue require special control flow that doesn't fit the normal expression-evaluation model. They need to unwind the call stack to the enclosing loop, which is awkward in a tree-walking interpreter.
**Consequences:** Users work around it with flags and `if` statements, leading to ugly code and complaints.
**Prevention:** Implement break/continue as special exceptions (C# exceptions, caught by the loop handler). This is the standard approach for tree-walking interpreters:
```csharp
class BreakException : Exception { }
class ContinueException : Exception { }
// In loop handler:
try { EvaluateBody(body); }
catch (BreakException) { break; }
catch (ContinueException) { continue; }
```
**Detection:** Write test cases with `break` and `continue` from day one.

### Pitfall 4: Beat-Synced Reload Threading Issues
**What goes wrong:** File watcher fires on a thread pool thread, which tries to re-parse and re-evaluate Flow code while the audio playback thread is reading from the same data structures. Race conditions cause crashes, garbled audio, or deadlocks.
**Why it happens:** `FileSystemWatcher` callbacks run on thread pool threads. Audio playback runs on its own thread. The interpreter's `ExecutionContext`, `StackFrame`, and section data are not thread-safe.
**Consequences:** Random crashes during live reload. Intermittent garbled audio. Deadlocks that freeze the application.
**Prevention:** Double-buffering approach: file watcher triggers re-parse on a background thread into a NEW execution context. When the new context is ready AND the current bar/section boundary is reached, atomically swap the reference. The audio thread only ever reads from one consistent context. Use `Interlocked.Exchange` or a lock-free swap for the handoff.
**Detection:** Stress test by saving files rapidly while audio plays. If it crashes within 30 seconds of rapid saves, the threading model is wrong.

## Moderate Pitfalls

### Pitfall 5: Sidechain Buffer Length Mismatch
**What goes wrong:** The target buffer (bass) and sidechain source buffer (kick) have different lengths or sample rates. Processing assumes they're the same length.
**Prevention:** Validate that both buffers have the same sample rate. For length mismatches, either pad the shorter buffer with silence or process only up to the length of the shorter one. Document the behavior clearly.

### Pitfall 6: Voice Stealing Audible Artifacts
**What goes wrong:** When the voice pool is full and a new note arrives, stealing an active voice produces an audible click or pop because the stolen voice's audio cuts off abruptly.
**Prevention:** Apply a short fade-out (5-10ms) to the stolen voice before reassigning. This is how hardware synths handle it. The fade is imperceptible but eliminates the click.

### Pitfall 7: String Interpolation Recursive Nesting
**What goes wrong:** Users write `$"outer {$"inner {x}"}"` -- nested interpolated strings. The lexer needs to track nesting depth of braces within interpolated strings, which complicates the tokenizer.
**Prevention:** Either disallow nesting (simpler, adequate for v1) or implement a brace-depth counter in the lexer. Most languages start with non-nested interpolation.

### Pitfall 8: MIDI Export Timing Precision
**What goes wrong:** Converting from Flow's beat-based timing (floating point) to MIDI's tick-based timing (integer) introduces rounding errors. A sequence of notes that should total exactly 4 beats drifts by a tick, causing the MIDI file to go out of sync.
**Prevention:** Use MIDI's standard resolution (480 ticks per quarter note). Round to nearest tick. After converting all notes in a bar, adjust the last note's duration to ensure the bar total is exact. This "error distribution" approach is standard in MIDI sequencers.

### Pitfall 9: Polyrhythm LCM Explosion
**What goes wrong:** Polyrhythm support requires finding the least common multiple (LCM) of two time signatures to determine the shared cycle length. 7/8 against 11/8 has an LCM cycle of 77 eighth notes. Users could request absurd polyrhythms (13/8 against 17/8 = 221 eighth notes per cycle).
**Prevention:** Cap the maximum cycle length (e.g., 64 bars). Warn when polyrhythmic cycles are very long. Provide a `cycle` parameter that lets users explicitly set the alignment period.

### Pitfall 10: Chord Progression Voice Leading Complexity
**What goes wrong:** Implementing a "correct" voice leading algorithm that handles all cases (parallel fifths avoidance, common tone retention, minimal motion) is a deep music theory problem. A naive implementation produces awkward voicings.
**Prevention:** Start with simple rules: (1) keep common tones, (2) move remaining voices by the smallest interval, (3) avoid parallel fifths/octaves. This covers 90% of cases. Do not attempt four-part chorale-style voice leading in v1 -- that's a graduate-level music theory problem.

## Minor Pitfalls

### Pitfall 11: WAV Reader Endianness
**What goes wrong:** WAV files are little-endian. If someone runs Flow on a big-endian system (rare but possible), `BinaryReader` defaults to little-endian on .NET, so this is actually fine. But custom byte manipulation code (like the existing 24-bit writer) must be aware.
**Prevention:** Use `BinaryReader`/`BinaryWriter` consistently. They handle endianness correctly for WAV.

### Pitfall 12: Sequence Visualization Terminal Width
**What goes wrong:** ASCII piano roll exceeds terminal width for long sequences, wrapping awkwardly and becoming unreadable.
**Prevention:** Detect terminal width (`Console.WindowWidth`), paginate or compress the time axis to fit. Show one or two bars at a time with scrolling.

### Pitfall 13: loadWav Path Resolution
**What goes wrong:** Relative paths in `loadWav("drums/kick.wav")` resolve relative to the working directory, not the .flow file's directory. Users expect the path to be relative to their script.
**Prevention:** Resolve paths relative to the directory of the currently executing .flow file (same behavior as `use` imports). The `ModuleLoader` already handles this for imports; reuse the same path resolution logic.

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Loop constructs | Missing break/continue (Pitfall 3) | Implement break/continue from the start using exception-based control flow |
| String interpolation | Nested interpolation complexity (Pitfall 7) | Disallow nesting in v1; document the limitation |
| Sample import | Format assumption (Pitfall 2) | Support PCM 8/16/24/32 and IEEE float; resample to 44100 Hz |
| Panning | No pitfall -- straightforward math | Just implement constant-power pan law |
| Sidechain compression | Buffer length mismatch (Pitfall 5) | Validate sample rates match; handle length differences |
| Voice allocation | Audible clicks on steal (Pitfall 6) | Short fade-out on stolen voices |
| Custom oscillators | Performance death spiral (Pitfall 1) | Wavetable approach, not per-sample proc evaluation |
| MIDI export | Timing precision (Pitfall 8) | 480 ticks/quarter, error distribution on bar boundaries |
| Pattern variation | No major pitfall | Extend existing `(? ...)` infrastructure |
| Chord progression DSL | Voice leading complexity (Pitfall 10) | Start with simple rules, not full chorale-style voice leading |
| Polyrhythm | LCM explosion (Pitfall 9) | Cap cycle length, provide explicit cycle parameter |
| Beat-synced reload | Threading issues (Pitfall 4) | Double-buffering with atomic swap at bar boundaries |
| Sequence visualization | Terminal width (Pitfall 12) | Detect width, paginate output |

## Sources

- Existing codebase analysis: `flow-lang/StandardLibrary/Audio/DSP/Compressor.cs`, `flow-lang/StandardLibrary/Audio/FileIO.cs`
- Standard MIDI specification: 480 ticks/quarter note convention
- Synthesizer voice allocation: standard hardware synth design (Roland, Yamaha documentation)
- Tree-walking interpreter patterns: Crafting Interpreters (Bob Nystrom) -- break/continue as exceptions
- Music theory: voice leading rules from Walter Piston's "Harmony"
