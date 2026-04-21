# Phase 15: Composer DX Part 2 — Pattern Map

**Mapped:** 2026-04-20
**Files analyzed:** 22 (12 modified, 10 new)
**Analogs found:** 22 / 22 (all files have an existing in-repo analog; zero "no analog" entries)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/Ast/Statements/MusicalContextStatement.cs` | AST (enum extension) | request-response | same file — existing `MusicalContextType` enum entries `Pan`, `Gain` | exact |
| `flow-lang/Runtime/MusicalContext.cs` | runtime (nullable field + Clone + ToString) | request-response | same file — existing `Pan`, `Gain` nullable-double fields (lines 40-41) | exact |
| `flow-lang/Runtime/ExecutionContext.cs` | runtime (stack walk + early-break predicate) | request-response | same file — `GetMusicalContext` walk at lines 186-212 | exact |
| `flow-lang/Parsing/Parser.cs` (top-level dispatch) | parser (token dispatch) | request-response | same file — `Gain` lookahead gate at lines 126-132 | exact |
| `flow-lang/Parsing/Parser.cs` (numeric-body case) | parser (case body) | request-response | same file — `Gain` case at lines 526-539 **with divergence for negative-sign rejection** | role-match |
| `flow-lang/Lexing/TokenType.cs` | lexing (enum) | request-response | same file — existing `Gain`, `Pan` enum entries (lines 25-26) | exact |
| `flow-lang/Lexing/SimpleLexer.cs` | lexing (keyword table) | request-response | same file — `"gain" => TokenType.Gain` at line 598 | exact |
| `flow-lang/Interpreter/Interpreter.cs` | interpreter (context-switch case) | request-response | same file — `Gain` case at lines 231-245 **with divergence for silent clamp vs error** | role-match |
| `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` | stdlib/DSP (new overload) | streaming (buffer transform) | same file — existing `Apply(buffer, roomSize, damping, mix)` at lines 26-53 | exact |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` | stdlib/audio (voice-loop edit) | streaming (voice pipeline) | same file — `RenderSection` pan/gain application at lines 117-133 | exact |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | stdlib (overload registration) | transform | same file — existing `euclidean` 3-arg registration at lines 1033-1074 | exact |
| `flow-lang/std.flow` | stdlib Flow-side declaration | declarative | same file — existing `internal proc euclidean (Int: hits, Int: steps, Note: pitch)` at line 133 | exact |
| `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` | test (unit, direct API) | request-response | `flow-lang.Tests/Unit/Phase14/SliceTests.cs` — direct-registry `[Fact]` pattern | role-match |
| `flow-lang.Tests/Unit/Phase15/ReverbApplyRt60Tests.cs` | test (unit, direct DSP) | streaming | `flow-lang.Tests/Unit/Phase08/MixTests.cs` — AudioBuffer-level direct Facts | role-match |
| `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` | test (unit, registry-level) | transform | `flow-lang.Tests/Unit/Phase14/SliceTests.cs` | role-match |
| `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` | test (unit, registry-level + determinism) | transform | `flow-lang.Tests/Unit/Phase14/SliceTests.cs` **+ `VariationFunctions.VarySeeded` PRNG pattern** | role-match |
| `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` | test (integration, end-to-end render) | streaming | `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` | exact (test-shape) |
| `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` | test (integration, byte-identical MIDI) | batch compare | `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` | exact (test-shape) |
| `flow-lang.Tests/Shared/MidiReadHelpers.cs` | test helper (promotion) | file-I/O | inline code in `DynamicsMidiVelocityTests.cs:56-61` (being promoted per DEFER-05) | exact (source of promotion) |
| `flow-lang.Tests/FlowScriptData.cs` | test data (Theory seed) | request-response | same file — existing `RequiredSentinels` entries (lines 60-206) | exact |
| `tests/test_reverb_time.flow` | flow test script | request-response | `tests/test_dynamics_midi_velocity.flow` | exact |
| `tests/test_euclidean_swing.flow` / `tests/test_euclidean_humanize.flow` | flow test script | request-response | `tests/test_dynamics_midi_velocity.flow` | exact |

---

## Pattern Assignments

### `flow-lang/Ast/Statements/MusicalContextStatement.cs` (AST enum extension)

**Analog:** same file, `MusicalContextType` enum at line 8.

**Current shape (verbatim, `MusicalContextStatement.cs:8`):**
```csharp
public enum MusicalContextType { Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel, Pan, Gain }
```

**Mirror action:** append `ReverbTime` as the 10th member. One-line change; no new file, no constructor change to the record at lines 14-20 (the record already parameterizes `ContextType`).

---

### `flow-lang/Runtime/MusicalContext.cs` (nullable field + Clone + ToString)

**Analog:** same file — `Gain` / `Pan` nullable-double field treatment.

**Field pattern to mirror (verbatim, `MusicalContext.cs:40-41`):**
```csharp
public double? Pan { get; set; }  // -1.0 (left) to 1.0 (right), null = inherit
public double? Gain { get; set; }  // 0.0 to 2.0 (null = inherit, default 1.0 at usage site)
```

**Clone pattern to mirror (verbatim, `MusicalContext.cs:51-60`):**
```csharp
public MusicalContext Clone() => new()
{
    TimeSignature = TimeSignature,
    Tempo = Tempo,
    Swing = Swing,
    Key = Key,
    Velocity = Velocity,
    Pan = Pan,
    Gain = Gain
};
```

**ToString pattern to mirror (verbatim, `MusicalContext.cs:95-106`):**
```csharp
if (Gain != null) parts.Add($"gain={Gain}");
```

**Mirror action:**
- Add `public double? ReverbTime { get; set; }  // 0.0 (dry) to 30.0 (clamped ceiling), null = inherit; seconds` beneath `Gain`.
- Add `ReverbTime = ReverbTime` as 8th line of the `Clone()` initializer.
- Add `if (ReverbTime != null) parts.Add($"reverbTime={ReverbTime}");` inside `ToString()` before the final `return`.
- Do NOT add a static `IsValidReverbTime` helper — validation lives in the parser (negative reject) and interpreter (silent clamp), not on the DTO.

---

### `flow-lang/Runtime/ExecutionContext.cs` (stack walk + early-break)

**Analog:** same file — `GetMusicalContext` at `ExecutionContext.cs:186-212`.

**Current walk + early-break (verbatim):**
```csharp
public MusicalContext GetMusicalContext()
{
    var resolved = new MusicalContext();
    foreach (var frame in _callStack)
    {
        if (frame.MusicalContext != null)
        {
            resolved.TimeSignature ??= frame.MusicalContext.TimeSignature;
            resolved.Tempo ??= frame.MusicalContext.Tempo;
            resolved.Swing ??= frame.MusicalContext.Swing;
            resolved.Key ??= frame.MusicalContext.Key;
            resolved.Velocity ??= frame.MusicalContext.Velocity;
            resolved.Pan ??= frame.MusicalContext.Pan;
            resolved.Gain ??= frame.MusicalContext.Gain;
        }
        if (resolved.TimeSignature != null && resolved.Tempo != null
            && resolved.Swing != null && resolved.Key != null
            && resolved.Velocity != null && resolved.Pan != null
            && resolved.Gain != null)
            break;
    }
    // Defaults
    resolved.TimeSignature ??= new TypeSystem.SpecialTypes.TimeSignatureData(4, 4);
    resolved.Tempo ??= 120.0;
    resolved.Swing ??= 0.5;
    return resolved;
}
```

**Mirror action (two edits, both required — RESEARCH Pitfall 1):**
1. Add `resolved.ReverbTime ??= frame.MusicalContext.ReverbTime;` immediately after the `Gain` line inside the `if (frame.MusicalContext != null)` block.
2. Extend the 8-clause early-break predicate to require `resolved.ReverbTime != null` as the 8th clause (insert before the closing `)` at line 204).
3. Do NOT add a default for `ReverbTime` at the bottom of the method — null is the sentinel for "no reverbTime context active; SongRenderer treats it as no-op". This matches how `Pan`/`Gain` get their "defaults" (`?? 0.0` / `?? 1.0`) at the consumer site in `SongRenderer.cs:118-119`, not here.

---

### `flow-lang/Parsing/Parser.cs` — top-level dispatch (line ~128)

**Analog:** same file — `Gain` lookahead gate at `Parser.cs:126-132`.

**Pattern to mirror (verbatim):**
```csharp
// Only parse `gain` as a context block when followed by a numeric literal or sign
// (e.g., `gain 0.5 { ... }`), not when used as a function name.
if (Check(TokenType.Gain) && _current + 1 < _tokens.Count
    && (_tokens[_current + 1].Type is TokenType.IntLiteral or TokenType.FloatLiteral
        or TokenType.Minus or TokenType.Plus))
{
    Advance(); // consume `gain`
    return ParseMusicalContextStatement(MusicalContextType.Gain);
}
```

**Mirror action:** insert an analogous block for `ReverbTime` immediately after the `Gain` block. Rationale for keeping the same 4-token lookahead (including `Minus`/`Plus`): the negative sign MUST be consumed so the error points at the `-` — refusing to enter the parse path would leave the `-` unconsumed and produce a misleading error at the `{`. See RESEARCH Pitfall 4.

---

### `flow-lang/Parsing/Parser.cs` — numeric-body case (line ~540)

**Analog:** same file — `Gain` case body at `Parser.cs:526-539`.

**Pattern to mirror (verbatim):**
```csharp
case MusicalContextType.Gain:
{
    int gainSign = 1;
    var gainLoc = CurrentToken.Location;
    if (Match(TokenType.Minus)) gainSign = -1;
    else if (Match(TokenType.Plus)) gainSign = 1;
    if (Check(TokenType.IntLiteral))
        value = new LiteralExpression(gainLoc, gainSign * (double)(int)Advance().Value!);
    else if (Check(TokenType.FloatLiteral))
        value = new LiteralExpression(gainLoc, gainSign * (double)Advance().Value!);
    else
        throw new ParseException($"Expected numeric gain value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    break;
}
```

**Divergence from analog (CONTEXT D-03):** replace the `gainSign = -1;` branch with an immediate `ParseException` so negative RT60 is rejected at parse time with the error anchored at the `-` location. The `Plus` branch stays (silent accept). Exact shape per RESEARCH Pattern 1 step 7:

```csharp
case MusicalContextType.ReverbTime:
{
    var rtLoc = CurrentToken.Location;
    if (Match(TokenType.Minus))
        throw new ParseException(
            $"reverbTime cannot be negative (RT60 is a time in seconds); got '-' at {rtLoc}");
    if (Match(TokenType.Plus)) { /* silent sign noise, accept */ }
    if (Check(TokenType.IntLiteral))
        value = new LiteralExpression(rtLoc, (double)(int)Advance().Value!);
    else if (Check(TokenType.FloatLiteral))
        value = new LiteralExpression(rtLoc, (double)Advance().Value!);
    else
        throw new ParseException(
            $"Expected numeric reverbTime value, got {CurrentToken.Type} '{CurrentToken.Text}' at {CurrentToken.Location}");
    break;
}
```

---

### `flow-lang/Lexing/TokenType.cs`

**Analog:** same file — `Gain`, `Pan` enum members at lines 25-26.

**Pattern to mirror (verbatim):**
```csharp
Pan,
Gain,
Pickup,
```

**Mirror action:** add `ReverbTime,` between `Gain,` and `Pickup,`. Keep alphabetical-within-cluster grouping (the existing enum groups musical-context tokens together).

---

### `flow-lang/Lexing/SimpleLexer.cs` — keyword table (line ~598)

**Analog:** same file — keyword switch at `SimpleLexer.cs:580-608`.

**Pattern to mirror (verbatim):**
```csharp
"pan" => TokenType.Pan,
"gain" => TokenType.Gain,
"pickup" => TokenType.Pickup,
```

**Mirror action:** add `"reverbTime" => TokenType.ReverbTime,` between `"gain"` and `"pickup"` entries. Preserves camelCase (matches identifier-style rather than all-lowercase; consistent with REQUIREMENTS.md §DX-07 exact spelling `reverbTime`).

---

### `flow-lang/Interpreter/Interpreter.cs` — context switch case (line ~246)

**Analog:** same file — `Gain` case at `Interpreter.cs:231-245`.

**Pattern to mirror (verbatim):**
```csharp
case MusicalContextType.Gain:
{
    var gainVal = _evaluator.Evaluate(ctx.Value);
    double gain = gainVal.Type is IntType
        ? (double)gainVal.As<int>()
        : gainVal.As<double>();
    if (gain < 0.0 || gain > 2.0)
    {
        _errorReporter.ReportError(
            $"Gain must be between 0.0 and 2.0, got {gain}", ctx.Location);
        break;
    }
    musicalCtx.Gain = gain;
    break;
}
```

**Divergence from analog (CONTEXT D-02, D-03):**
- NO error on out-of-range — replace the `if (gain < 0.0 || gain > 2.0)` block with `rt60 = Math.Min(rt60, 30.0);` (silent clamp at upper bound per D-03).
- NO lower-bound check — negatives already rejected at parse time; `0.0` is a sentinel (D-02 "dry").

Target shape (from RESEARCH Pattern 1 step 8):
```csharp
case MusicalContextType.ReverbTime:
{
    var rtVal = _evaluator.Evaluate(ctx.Value);
    double rt60 = rtVal.Type is IntType ? (double)rtVal.As<int>() : rtVal.As<double>();
    // D-03: silent clamp to 30s (negative already rejected at parse time)
    rt60 = Math.Min(rt60, 30.0);
    // D-02: 0.0 preserved as sentinel for "dry" — no error, no clamp-up
    musicalCtx.ReverbTime = rt60;
    break;
}
```

Insert as new case after the `Gain` case (at approximately line 246, immediately before `case MusicalContextType.Key:` at line 247).

---

### `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` — NEW overload (line ~54)

**Analog:** same file — existing `Apply(AudioBuffer input, float roomSize, float damping, float mix)` at `Reverb.cs:26-53`.

**Existing overload pattern to mirror (verbatim):**
```csharp
public static AudioBuffer Apply(AudioBuffer input, float roomSize, float damping, float mix)
{
    // Clamp parameters to valid ranges
    roomSize = Math.Clamp(roomSize, 0f, 1f);
    damping = Math.Clamp(damping, 0f, 1f);
    mix = Math.Clamp(mix, 0f, 1f);

    var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);

    // Scale delay times for the actual sample rate
    double rateScale = input.SampleRate / 44100.0;

    // Process each channel independently
    for (int ch = 0; ch < input.Channels; ch++)
    {
        var dry = ExtractChannel(input, ch);
        var wet = ProcessChannel(dry, roomSize, damping, rateScale);

        // Mix wet/dry into result
        for (int frame = 0; frame < input.Frames; frame++)
        {
            float mixed = dry[frame] * (1f - mix) + wet[frame] * mix;
            result.SetSample(frame, ch, mixed);
        }
    }

    return result;
}
```

**Comb-delay constant to reuse (verbatim, `Reverb.cs:10`):**
```csharp
private static readonly int[] CombDelays = [1116, 1188, 1277, 1356];
```

**Existing `ProcessChannel` signature to call (verbatim, `Reverb.cs:58`):**
```csharp
private static float[] ProcessChannel(float[] input, float roomSize, float damping, double rateScale)
{
    int length = input.Length;
    float feedback = 0.7f + roomSize * 0.28f; // Map room size to feedback range [0.7, 0.98]
    // ...
}
```

**Divergence from analog (CONTEXT D-13):** The new overload must NOT call `ProcessChannel` with a `roomSize` argument — it needs a `feedback` value derived from RT60. Per RESEARCH Pattern 3, the cleanest strict refactor is:
1. Change `ProcessChannel` signature from `(input, roomSize, damping, rateScale)` to `(input, feedback, damping, rateScale)`.
2. Remove the internal `float feedback = 0.7f + roomSize * 0.28f;` line from `ProcessChannel`.
3. In the existing `Apply(roomSize,…)` overload, compute `float feedback = 0.7f + roomSize * 0.28f;` before calling `ProcessChannel(dry, feedback, damping, rateScale)`.
4. In the NEW overload, compute feedback via Schroeder: `Math.Pow(10.0, -3.0 * avgDelaySeconds / rt60Seconds)`.

This is a strict refactor — no behavior change for existing callers.

**New overload to add (sketch from RESEARCH Pattern 3, lines 421-465):**
```csharp
public static AudioBuffer Apply(AudioBuffer input, double rt60Seconds, float damping, float mix)
{
    if (rt60Seconds <= 0.0) rt60Seconds = 0.001; // avoid div-by-zero; dry short-circuit lives in SongRenderer, not here

    double rateScale = input.SampleRate / 44100.0;
    double avgDelaySamples = (CombDelays[0] + CombDelays[1] + CombDelays[2] + CombDelays[3]) / 4.0 * rateScale;
    double avgDelaySeconds = avgDelaySamples / input.SampleRate;
    float feedback = (float)Math.Clamp(Math.Pow(10.0, -3.0 * avgDelaySeconds / rt60Seconds), 0.0, 0.99);

    damping = Math.Clamp(damping, 0f, 1f);
    mix = Math.Clamp(mix, 0f, 1f);

    var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);
    for (int ch = 0; ch < input.Channels; ch++)
    {
        var dry = ExtractChannel(input, ch);
        var wet = ProcessChannel(dry, feedback, damping, rateScale);
        for (int frame = 0; frame < input.Frames; frame++)
        {
            float mixed = dry[frame] * (1f - mix) + wet[frame] * mix;
            result.SetSample(frame, ch, mixed);
        }
    }
    return result;
}
```

---

### `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — voice-loop (line ~133)

**Analog:** same file — pan/gain per-voice application at `SongRenderer.cs:115-144`.

**Pattern to mirror (verbatim):**
```csharp
private static AudioBuffer RenderSection(SectionData section, INoteSynthesizer synthesizer)
{
    double bpm = section.Context?.Tempo ?? DefaultBpm;
    double pan = section.Context?.Pan ?? 0.0;
    double gain = section.Context?.Gain ?? 1.0;
    var allVoices = new List<Voice>();
    double maxBeats = 0;

    foreach (var (name, sequence) in section.Sequences)
    {
        var voices = SequenceRenderer.RenderSequenceToVoices(
            sequence, synthesizer, DefaultSampleRate, bpm);
        // Apply pan and gain from musical context to all voices in this section
        foreach (var voice in voices)
        {
            if (pan != 0.0)
                voice.Pan = pan;
            voice.Gain *= gain;
        }
        allVoices.AddRange(voices);
        // ...
    }
    // ...
    return MixVoicesToStereoBuffer(allVoices, bpm, DefaultSampleRate, maxBeats);
}
```

**Mirror action (CONTEXT D-02, D-14, D-15):** insert the RT60 read beside the `pan`/`gain` reads (line 117-119 range) and apply `Reverb.Apply` per voice. RT60 is NOT assigned to the `Voice` DTO (per D-14 anti-pattern) — it's consumed inline in the voice-loop pass.

Sketch:
```csharp
double? rt60 = section.Context?.ReverbTime;
// ...existing pan/gain loop unchanged...
// After the pan/gain loop but before MixVoicesToStereoBuffer:
if (rt60.HasValue && rt60.Value != 0.0)  // D-02: exact == 0 comparison (no epsilon; see Pitfall 3)
{
    for (int i = 0; i < allVoices.Count; i++)
    {
        var v = allVoices[i];
        v.Buffer = Reverb.Apply(v.Buffer, rt60.Value, damping: 0.5f, mix: 0.3f);  // D-15 defaults
    }
}
```

(Planner: confirm `Voice.Buffer` is mutable; if not, construct new Voice with substituted buffer preserving Pan/Gain/OffsetBeats. Use the same replacement pattern already used elsewhere in this file — scout for `new Voice(` if needed.)

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs` — euclidean overloads (line ~1074)

**Analog (registration shape):** same file — existing `euclidean` registration at `BuiltInFunctions.cs:1033-1074`.

**Pattern to mirror (verbatim):**
```csharp
var euclideanSignature = new FunctionSignature(
    "euclidean",
    [IntType.Instance, IntType.Instance, NoteType.Instance]);
registry.Register("euclidean", euclideanSignature, args =>
{
    int hits = (int)args[0].Data!;
    int steps = (int)args[1].Data!;
    string noteStr = (string)args[2].Data!;

    if (hits <= 0) throw new InvalidOperationException("euclidean: hits must be > 0");
    if (steps <= 0) throw new InvalidOperationException("euclidean: steps must be > 0");
    if (hits > steps) throw new InvalidOperationException("euclidean: hits must be <= steps");

    var (noteName, octave, alteration) = NoteType.Parse(noteStr);
    var pattern = Bjorklund(hits, steps);

    var duration = steps switch
    {
        <= 4 => NoteValueType.Value.QUARTER,
        <= 8 => NoteValueType.Value.EIGHTH,
        <= 16 => NoteValueType.Value.SIXTEENTH,
        _ => NoteValueType.Value.THIRTYSECOND
    };

    var notes = new List<MusicalNoteData>();
    foreach (bool isHit in pattern)
    {
        if (isHit)
            notes.Add(new MusicalNoteData(noteName, octave, alteration, (int)duration, isRest: false));
        else
            notes.Add(new MusicalNoteData(' ', 0, 0, (int)duration, isRest: true));
    }

    var timeSig = new TimeSignatureData(4, 4);
    var bar = new BarData(notes, timeSig);
    var sequence = new SequenceData();
    sequence.AddBar(bar);
    return Value.Sequence(sequence);
});
```

**Analog (local-seeded-PRNG shape):** `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:71-77`.

**Pattern to mirror (verbatim, `VariationFunctions.cs:71-77`):**
```csharp
private static Value VarySeeded(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double probability = args[1].As<double>();
    int seed = args[2].As<int>();
    return Value.Sequence(ApplyVariation(seq, probability, null, new Random(seed), null));
    //                                                         ^^^^^^^^^^^^^^^^^^
    // Local instance; scoped to this call; byte-identical across calls with same seed.
}
```

**Anti-pattern to avoid (verbatim, `TransformFunctions.cs:667-687`):**
```csharp
// DO NOT MIRROR — static shared RNG defeats determinism
private static readonly Random HumanizeRng = new();
// ...
double velJitter = (HumanizeRng.NextDouble() * 2.0 - 1.0) * amount * 0.2;
// ^^ Shared state across calls — two calls with "same inputs" produce different
// output on the second run. This is the existing `humanize` built-in (distinct
// from DX-09's euclidean humanize PARAMETER). See RESEARCH Pitfall 8.
```

**Mirror action (CONTEXT D-05 through D-12, D-17):**

Two new `FunctionSignature` registrations after the existing one at line 1074:

```csharp
// 4-arg: swing only (D-05..D-08)
var euclideanSwingSig = new FunctionSignature(
    "euclidean",
    [IntType.Instance, IntType.Instance, NoteType.Instance, DoubleType.Instance]);
registry.Register("euclidean", euclideanSwingSig, args => { /* swing body */ });

// 6-arg: swing + humanize + seed (D-09..D-12, D-17)
var euclideanHumanSig = new FunctionSignature(
    "euclidean",
    [IntType.Instance, IntType.Instance, NoteType.Instance,
     DoubleType.Instance, DoubleType.Instance, IntType.Instance]);
registry.Register("euclidean", euclideanHumanSig, args => { /* swing+humanize body */ });
```

Inside each body, the Bjorklund call + note-construction loop from the existing 3-arg body is reused verbatim. The velocity computation is the new part, cribbed directly from RESEARCH Pattern 4:

```csharp
bool[] pattern = Bjorklund(hits, steps);
int gridStep = Math.Max(1, steps / hits);        // D-06: floor div; guard against 0
double swingClamped = Math.Clamp(swing, -1.0, 1.0);
double accentAmount = Math.Abs(swingClamped);
bool accentOnBeats = swingClamped >= 0;          // D-08: positive → on-beats; negative → off-beats

// 6-arg overload only:
double humanizeClamped = Math.Clamp(humanize, 0.0, 1.0);
var rng = new Random(seed);                      // D-17: LOCAL, not ExecutionContext.GetRand

// Per hit, inside the existing `foreach (bool isHit in pattern)` loop:
//   double v = baseVelocity;  // read from MusicalContext.Velocity — see RESEARCH Pitfall 6
//   bool onBeat = (stepIndex % gridStep) == 0;
//   bool accented = accentOnBeats == onBeat;
//   if (accented) v += accentAmount;
//   if (humanizeOverload) v += (rng.NextDouble() * 2.0 - 1.0) * humanizeClamped;
//   // MusicalNoteData constructor clamps [0,1] at NoteType.cs:244 — D-12 belt-and-braces
```

**Critical: base-velocity source (RESEARCH Pitfall 6):** Reading `MusicalContext.Velocity` requires the `RegisterContextDependent` pattern (see `HarmonyFunctions.cs` `resolveNumeral`). The `IReadOnlyList<Value> args` lambda does NOT receive context. Planner chooses:
- Option A: use `RegisterContextDependent` so the overload reads `ctx.GetMusicalContext().Velocity ?? 0.63` (consistent with `NoteStreamCompiler.cs:341` behavior).
- Option B: hardcode `0.63` (simpler, ignores dynamics-block interaction).
- Recommended: Option A for internal consistency with `NoteStreamCompiler.cs:341`.

**std.flow companion edit required (RESEARCH Pitfall 5):** Both new overloads MUST also be declared in `flow-lang/std.flow` alongside the existing line 133 declaration:

```flow
internal proc euclidean (Int: hits, Int: steps, Note: pitch)
internal proc euclidean (Int: hits, Int: steps, Note: pitch, Double: swing)
internal proc euclidean (Int: hits, Int: steps, Note: pitch, Double: swing, Double: humanize, Int: seed)
```

Without the `.flow` declaration, user scripts cannot call the new overloads even when C# registration is present (Phase 14 Plan 02 finding).

---

### `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` (NEW)

**Analog:** `flow-lang.Tests/Unit/Phase14/SliceTests.cs` — Unit Fact style using direct library calls.

**Imports pattern to mirror (verbatim, `SliceTests.cs:1-8`):**
```csharp
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;
```

**Fact shape pattern to mirror (verbatim, `SliceTests.cs:26-36`):**
```csharp
[Fact]
public void Array_NormalRange()
{
    var arr = MakeIntArray(1, 2, 3, 4, 5);
    var result = Collections.SliceArray(new[] { arr, Value.Int(1), Value.Int(4) });
    var elems = result.As<IReadOnlyList<Value>>();
    Assert.Equal(3, elems.Count);
    Assert.Equal(2, elems[0].As<int>());
    // ...
}
```

**Mirror action:** write this class under namespace `FlowLang.Tests.Unit.Phase15`. Facts to include (observable pins per 15-RESEARCH §Phase Requirements → Test Map):

| Fact name | Asserts |
|-----------|---------|
| `Parse_Positive_StoresInContext` | `reverbTime 2.5 { ... }` parses; after execution, `GetMusicalContext().ReverbTime == 2.5`. |
| `Parse_Negative_ParseError` | `reverbTime -2.5 { ... }` → `errorCount >= 1` AND error text contains `"reverbTime cannot be negative"`. |
| `Parse_AboveMax_ClampsTo30` | `reverbTime 45 { ... }` → `GetMusicalContext().ReverbTime == 30.0`. |
| `Parse_Zero_ProducesDry` | `reverbTime 0 { ... }` → `GetMusicalContext().ReverbTime == 0.0` (the dry sentinel; SongRenderer is responsible for the short-circuit). |
| `Nested_WithGain_Independent` | `gain 0.5 { reverbTime 2.0 { ... } }` → both `Gain == 0.5` AND `ReverbTime == 2.0` at innermost frame. |
| `Nested_InsideTempoAndKey_Resolves` | `tempo 120 { key Cmajor { reverbTime 3.0 { ... } } }` → `ReverbTime == 3.0`. |
| `GetMusicalContext_AllFieldsResolvedSearchesReverbTime` | ReverbTime at outermost frame, all other 7 fields at inner frames → resolved still sees ReverbTime at innermost. Covers Pitfall 1 (early-break predicate stale). |

For parse-error assertion, use `FlowEngineRunner.RunSource(source)` from `flow-lang.Tests/Fixtures/FlowEngineRunner.cs`. For direct context probes, use `FlowEngine` + execute source + inspect runtime context via a test hook (follow existing `Phase06/SectionGainBareExpressionTests.cs` pattern — see that file to confirm the exact API if direct context access is needed).

---

### `flow-lang.Tests/Unit/Phase15/ReverbApplyRt60Tests.cs` (NEW)

**Analog:** `flow-lang.Tests/Unit/Phase08/MixTests.cs` — direct AudioBuffer Fact.

**Mirror action:** direct-call Facts against `Reverb.Apply(buffer, rt60Seconds, damping, mix)`:

| Fact name | Asserts |
|-----------|---------|
| `Rt60_ProducesExpectedDecay` | Feed a 3-second impulse (single 1.0 sample followed by zeros) at 44100Hz to `Reverb.Apply(buf, 2.0, 0.5f, 0.3f)`. Assert the sample near frame index `2.0 * 44100` is within ±3 dB of −60 dB relative to peak. Tolerance per RESEARCH Assumption A3. |
| `Rt60_Zero_DoesNotThrow` | `Reverb.Apply(buf, 0.0, 0.5f, 0.3f)` returns a buffer (does not divide-by-zero). Per the new overload's internal `rt60Seconds <= 0.0` guard (RESEARCH Pattern 3). |
| `Rt60_ExistingOverloadUnchanged` | After the `ProcessChannel` signature refactor, `Reverb.Apply(buf, 0.5f, 0.5f, 0.3f)` (roomSize overload) still produces byte-equivalent output to the pre-refactor result. Pin via hash of first 1000 output samples captured empirically from a pre-refactor reference run (per Phase 13 D-11 observable-value-pin discipline). |

---

### `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` (NEW)

**Analog:** `flow-lang.Tests/Unit/Phase14/SliceTests.cs` — direct-registry Fact style.

**Mirror action:** call the new 4-arg registration directly via `InternalFunctionRegistry.Invoke` (or through `FlowEngineRunner` if context access is needed for base velocity), and inspect resulting `SequenceData` notes:

| Fact name | Asserts |
|-----------|---------|
| `Swing_AboveMax_ClampsTo1` | `euclidean(3, 8, "C4", 1.5)` — max velocity in result == `baseVelocity + 1.0` (clamped by MusicalNoteData ctor to 1.0). |
| `NegativeSwing_AccentsOffBeats` | `euclidean(3, 8, "C4", -0.3)` — off-beat hit velocities > on-beat hit velocities. |
| `OnBeat_DetectionMatchesGrid` | `euclidean(3, 8, "C4", 0.3)` with hits at [0,3,6], gridStep = floor(8/3) = 2, on-beats at step indices ≡ 0 (mod 2) → hits at 0, 6 are on-beat; hit at 3 is off-beat. |
| `AccentAmount_IsRawDelta` | `euclidean(3, 8, "C4", 0.25)` — accented hit velocity == base + 0.25 (± 1e-9). |
| `Asymmetric_UnaccentedStaysAtBase` | `swing = 0.3` — unaccented hits velocity == base (not base − 0.3). |
| `Swing_ChangesVelocity_NotTiming` | note `DurationValue` identical across `swing = 0` and `swing = 0.5`; only `Velocity` differs. ROADMAP #1 pin. |

---

### `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` (NEW)

**Analog:** `flow-lang.Tests/Unit/Phase14/SliceTests.cs` (test shape) + `VariationFunctions.cs:71-77` (determinism semantic).

**Mirror action:** Facts targeting the 6-arg euclidean:

| Fact name | Asserts |
|-----------|---------|
| `Humanize_JitterInRange` | `euclidean(8, 16, "C4", 0.0, 0.1, 42)` — all perturbed velocities ∈ `[base − 0.1, base + 0.1]`. |
| `Humanize_AboveMax_ClampsTo1` | `humanize = 2.0` — jitter still ∈ `[-1.0, +1.0]` (clamped). |
| `Humanize_Uniform_NotGaussian` | 1000 seeds, histogram perturbations into 10 buckets over `[-0.1, +0.1]`; each bucket within ±30% of uniform expected count (100). Loose tolerance per RESEARCH to avoid statistical flake. |
| `Humanize_Overflow_Clamps` | Base velocity forced to 0.98 via `dynamics ff { euclidean(..., humanize=0.5, seed=...) }` — velocity saturates at 1.0, does not wrap. |
| `LocalPrng_IsolatedAcrossCalls` | Sequence: `euclidean(…, seed=42)` → `vary(seq, 0.3, seed=99)` (consumes global seeded RNG) → `euclidean(…, seed=42)` again. Both euclidean results byte-identical. Covers D-17. |
| `SameSeed_ProducesIdenticalVelocities` | Two calls `euclidean(3, 8, "C4", 0.0, 0.15, 42)` in sequence → identical `Velocity` arrays. |

---

### `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` (NEW)

**Analog:** `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` — integration via `FlowEngineRunner.RunFile` + output-file byte comparison.

**Imports pattern to mirror (verbatim, `DynamicsMidiVelocityTests.cs:1-9`):**
```csharp
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Xunit;

namespace FlowLang.Tests.Integration.Phase14;
```

**Fact scaffold to mirror (verbatim, `DynamicsMidiVelocityTests.cs:24-72`):**
```csharp
[Collection("FlowScripts")]
public class DynamicsMidiVelocityTests
{
    [Fact]
    public void Crescendo_EmitsExpectedVelocityGradient()
    {
        using var runner = new FlowEngineRunner();
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));

        string testScript = Path.Combine(repoRoot, "tests", "test_dynamics_midi_velocity.flow");
        string outputMidi = Path.Combine(repoRoot, "tests", "output", "dynamics_velocity.mid");

        if (File.Exists(outputMidi)) File.Delete(outputMidi);

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            Directory.CreateDirectory(Path.Combine(repoRoot, "tests", "output"));

            var (success, stdout, stderr, errorCount) = runner.RunFile(testScript);

            Assert.True(success, $"Script failed: stderr={stderr}");
            Assert.Equal(0, errorCount);
            Assert.True(File.Exists(outputMidi), $"MIDI file not written: {outputMidi}");
            // ... observable pins ...
        }
        finally { Environment.CurrentDirectory = originalCwd; }
    }
}
```

**Mirror action:** Facts:

| Fact name | Asserts |
|-----------|---------|
| `Zero_ShortCircuitsReverb` | `reverbTime 0 { ... }` WAV output byte-identical to same render with NO `reverbTime` wrapper. Observable pin = `File.ReadAllBytes(p1).SequenceEqual(File.ReadAllBytes(p2))`. |
| `PerVoice_Applies` | `reverbTime 2.0 { ... }` WAV tail-length (number of non-silent trailing samples) > no-reverb reference. |
| `Explicit_And_Context_Stack` | Script with both `reverb(buf, 0.5)` inline AND `reverbTime 2.0` wrapper → output byte-count > single-reverb baseline (D-16). |

---

### `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` (NEW)

**Analog:** `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` — same scaffold, divergent assertion.

**Mirror action:** Fact scaffold copied verbatim from `DynamicsMidiVelocityTests.cs` above, but assertion uses `Shared/MidiReadHelpers.GetVelocityBytes(path1).SequenceEqual(…path2)` AND asserts the exact byte sequence empirically recorded on first authoring (Phase 14 DX-08 pattern — run once, record observed bytes, re-run to confirm byte-identical). Facts:

| Fact name | Asserts |
|-----------|---------|
| `SameSeed_ByteIdenticalMidi` | Script renders `euclidean(3, 8, "C4", 0.3, 0.1, 42)` twice to two paths; `File.ReadAllBytes(p1) == File.ReadAllBytes(p2)` AND `GetVelocityBytes(p1) == <empirically-pinned sequence>`. |
| `SameSeed_ByteIdenticalWav` | Same script, WAV output; byte-identical between two runs. ROADMAP #2. |

**Empirical byte capture protocol (RESEARCH §Expected velocity bytes):** the planner MUST NOT compute the byte sequence from theory — `System.Random(42).NextDouble()` is .NET-version-dependent. Protocol: author the Fact with a placeholder expected-bytes array, run the test once on net10, record observed bytes from the AssertEqual diff, re-run to confirm green. Document the empirical capture in a Divergence entry in the plan's SUMMARY.

---

### `flow-lang.Tests/Shared/MidiReadHelpers.cs` (NEW — promotion per DEFER-05)

**Analog:** inline code block in `DynamicsMidiVelocityTests.cs:56-61`:

```csharp
var midiFile = MidiFile.Read(outputMidi);
byte[] velocities = midiFile
    .GetNotes()
    .Select(n => (byte)n.Velocity)
    .ToArray();
```

**Mirror action:** promote this inline snippet to a shared helper class. Signature (from 15-RESEARCH Wave 0 Gaps):

```csharp
namespace FlowLang.Tests.Shared;

internal static class MidiReadHelpers
{
    public static byte[] GetVelocityBytes(string midiPath)
    {
        var midiFile = MidiFile.Read(midiPath);
        return midiFile.GetNotes().Select(n => (byte)n.Velocity).ToArray();
    }

    public static int[] GetNoteNumbers(string midiPath)
    {
        var midiFile = MidiFile.Read(midiPath);
        return midiFile.GetNotes().Select(n => (int)n.NoteNumber).ToArray();
    }

    public static byte[] ReadAllBytes(string midiPath) => File.ReadAllBytes(midiPath);
}
```

Post-promotion: update `DynamicsMidiVelocityTests.cs:56-61` to call `MidiReadHelpers.GetVelocityBytes(outputMidi)` — one-line delta, preserves green state.

---

### `flow-lang.Tests/FlowScriptData.cs` — Theory rows (line ~205, inside `RequiredSentinels`)

**Analog:** same file — existing `RequiredSentinels` entries at lines 60-206.

**Pattern to mirror (verbatim, `FlowScriptData.cs:75-80`):**
```csharp
// Phase 13-01 (FIX-01): pin transpose-with-int success sentinel.
["test_transpose_int.flow"] = new[]
{
    "transpose with int: ok",
    "test_transpose_int: PASSED",
},
```

**Mirror action:** append three entries (order matching RESEARCH Wave 0 Gaps):

```csharp
// Phase 15 DX-07: reverbTime parses, renders, and short-circuits at 0.
["test_reverb_time.flow"] = new[]
{
    "reverbTime 2.5: PASSED",   // positive-value end-to-end render
    "reverbTime 0 dry: PASSED", // D-02 dry short-circuit observable
},

// Phase 15 DX-09: euclidean 4-arg swing overload.
["test_euclidean_swing.flow"] = new[]
{
    "euclidean swing: PASSED",
},

// Phase 15 DX-09: euclidean 6-arg humanize overload, same-seed byte-identical.
["test_euclidean_humanize.flow"] = new[]
{
    "euclidean humanize seed=42: PASSED",
    "two runs byte-identical: PASSED",
},
```

Exact sentinel strings must match each `.flow` script's `(print ...)` output verbatim — pass-2 empirical capture expected.

---

### `tests/test_reverb_time.flow` (NEW)

**Analog:** `tests/test_dynamics_midi_velocity.flow` (shown below verbatim).

**Pattern to mirror (verbatim, `tests/test_dynamics_midi_velocity.flow`):**
```flow
use "@std"
use "@audio"

Note: DX-08 — deterministic 5-note crescendo for MIDI velocity regression
Note: crescendo(0.25, 0.75) over 5 notes produces a linear gradient;

tempo 120 {
    timesig 4/4 {
        Sequence base = | C4 D4 E4 F4 G4 |
        Sequence curve = base -> crescendo 0.25 0.75
        section s { curve }
        Song song = [s]
        (writeMidi "tests/output/dynamics_velocity.mid" song)
        (print "dynamics_velocity: PASSED")
    }
}
```

**Mirror action:** write `tests/test_reverb_time.flow` with two sub-tests separated by nested context blocks, ending each sub-test with a `(print "... PASSED")` sentinel matching the FlowScriptData entry above. Use S-expression function-call style per user memory `feedback_language_philosophy.md` — e.g., `(print (concat "frames: " (str (getFrames buf))))`, NOT infix.

---

### `tests/test_euclidean_swing.flow` / `tests/test_euclidean_humanize.flow` (NEW)

**Analog:** same as above (`tests/test_dynamics_midi_velocity.flow`).

**Mirror action:** each script exercises one overload, prints its PASSED sentinel, and — for the humanize script — calls `euclidean` twice with the same seed + `writeMidi` twice, letting the integration Fact compare bytes.

---

## Shared Patterns

### Nullable-field inheritance through `MusicalContext`

**Sources:** `MusicalContext.cs:35-60, 95-107` (field + Clone + ToString) + `ExecutionContext.cs:186-212` (walk + early-break).
**Apply to:** all DX-07-related modifications to `MusicalContext.cs`, `ExecutionContext.cs`.
**Critical:** all 4 touch-points (field, Clone, ToString, walk+predicate) MUST land in the same commit. Missing ToString is cosmetic but diagnostic; missing Clone breaks section snapshots; missing walk breaks inheritance; missing early-break predicate breaks deep-nesting inheritance silently (RESEARCH Pitfall 1).

### Parse-time vs interpret-time validation split

**Sources:** `Parser.cs:526-539` (parse-time numeric coercion) + `Interpreter.cs:231-245` (value-range validation).
**Apply to:** DX-07 parser + interpreter edits.
**Rule:** errors with no defensible musical interpretation → parse-time (negative RT60 per D-03). Silent clamps → interpret-time (upper-bound 30s per D-03). This matches the charitable-interpretation philosophy encoded in user memory `feedback_charitable_interpretation.md`.

### Local seeded PRNG per call

**Source:** `VariationFunctions.cs:71-77` (`VarySeeded`).
**Apply to:** DX-09 6-arg euclidean humanize body.
**Anti-pattern to avoid:** `TransformFunctions.cs:667` static shared `HumanizeRng`. Never reference this for new features.

### Defaults at consumer, not producer

**Source:** `SongRenderer.cs:117-119` (`section.Context?.Tempo ?? DefaultBpm`, `…?.Pan ?? 0.0`, `…?.Gain ?? 1.0`).
**Apply to:** SongRenderer ReverbTime read.
**Rule:** `MusicalContext.ReverbTime` stays nullable end-to-end; the "no reverb" default (null → skip) is expressed at the voice-loop consumer site, not baked into `ExecutionContext.GetMusicalContext`. Consistent with existing `Pan`/`Gain` treatment.

### C# registration + .flow declaration pair

**Source:** `BuiltInFunctions.cs:1033-1074` (C# registration) + `flow-lang/std.flow:133` (Flow-side declaration).
**Apply to:** both new DX-09 euclidean overloads.
**Rule:** both are required; omitting the `.flow` declaration means the overload is invisible to user scripts even when C# registration succeeds (Phase 14 Plan 02 finding).

### Observable-value pin for integration Facts

**Source:** `DynamicsMidiVelocityTests.cs:62-66` — empirical byte array pinned with inline derivation comment.
**Apply to:** `EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi`.
**Rule:** Pass 1 authors the Fact with a placeholder expected-bytes array; Pass 2 runs once, records observed bytes, updates the Fact, re-runs to confirm green. Document as Divergence in plan's SUMMARY.md. Pattern reaffirmed by Phase 14 D-13 two-pass strict authorship discipline.

### FlowEngineRunner + CWD restoration

**Source:** `DynamicsMidiVelocityTests.cs:39-71` (`Environment.CurrentDirectory` swap + try/finally).
**Apply to:** all Phase 15 integration Facts that write files.
**Rule:** `writeWav` / `writeMidi` resolve paths relative to CWD; set CWD to repo root so the `.flow` script's relative `tests/output/...` path lands where the Fact reads.

---

## No Analog Found

None. Every file in this phase has a direct in-repo analog that shipped in an earlier phase (12/13/14). Phase 15 is a composition exercise, not an invention exercise — reaffirming RESEARCH §Don't Hand-Roll.

---

## Metadata

**Analog search scope:**
- `flow-lang/Ast/Statements/`, `flow-lang/Runtime/`, `flow-lang/Parsing/`, `flow-lang/Lexing/`, `flow-lang/Interpreter/`
- `flow-lang/StandardLibrary/Audio/`, `flow-lang/StandardLibrary/Audio/DSP/`, `flow-lang/StandardLibrary/Composition/`, `flow-lang/StandardLibrary/Transforms/`
- `flow-lang.Tests/Unit/Phase14/`, `flow-lang.Tests/Unit/Phase08/`, `flow-lang.Tests/Integration/Phase14/`, `flow-lang.Tests/Integration/Phase06/`, `flow-lang.Tests/Fixtures/`
- `tests/` for `.flow` script analogs

**Files scanned (key reads):**
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` (full, 20 lines)
- `flow-lang/Runtime/MusicalContext.cs` (full, 107 lines)
- `flow-lang/Runtime/ExecutionContext.cs:180-212` (target 33 lines)
- `flow-lang/Parsing/Parser.cs:100-149, 500-559` (two non-overlapping ranges)
- `flow-lang/Lexing/TokenType.cs:10-44`
- `flow-lang/Lexing/SimpleLexer.cs:575-614`
- `flow-lang/Interpreter/Interpreter.cs:210-279`
- `flow-lang/StandardLibrary/Audio/DSP/Reverb.cs` (full, 160 lines)
- `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs:30-79`
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs:110-179`
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1020-1109`
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:60-105`
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:655-709` (anti-pattern ref)
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:235-248`
- `flow-lang/std.flow:125-149`
- `flow-lang.Tests/Unit/Phase14/SliceTests.cs:1-80`
- `flow-lang.Tests/Integration/Phase14/DynamicsMidiVelocityTests.cs` (full, 73 lines)
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs` (full, 57 lines)
- `flow-lang.Tests/FlowScriptData.cs` (full, 207 lines)
- `tests/test_dynamics_midi_velocity.flow` (full, 19 lines)

**Collision check (RESEARCH §Pre-landing collision grep):** 0 hits for `reverbTime` across `flow-lang/*.flow`, `examples/`, `tests/` (re-verified 2026-04-20).

**Pattern extraction date:** 2026-04-20
