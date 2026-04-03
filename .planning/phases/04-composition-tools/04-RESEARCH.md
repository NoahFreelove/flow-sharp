# Phase 4: Composition Tools - Research

**Researched:** 2026-04-02
**Domain:** Music composition DSL features (chord progressions, voice leading, polyrhythm, pattern variation)
**Confidence:** HIGH

## Summary

Phase 4 adds four composition features to Flow: a chord progression DSL with voice leading, polyrhythmic layering, and probabilistic pattern variation. All four features build on existing infrastructure -- the harmony module (ChordParser, HarmonyFunctions, ScaleDatabase), the note stream pipeline (NoteStreamCompiler, SequenceData, BarData), the transform system (TransformFunctions), and the audio rendering pipeline (SongRenderer, SequenceRenderer).

The progression DSL requires changes at every pipeline stage: new `Progression` token in the lexer, `ProgressionExpression` AST node, parser rule, evaluator dispatch, and a `ProgressionCompiler` that resolves roman numerals via existing `ScaleDatabase.ResolveRomanNumeral()` and applies voice leading. Polyrhythm and variation are pure built-in functions registered via `FunctionSignature` + lambda, requiring no syntax changes.

**Primary recommendation:** Implement progression DSL + voice leading as one unit (they are inseparable -- D-05 through D-07), polyrhythm as a standalone built-in, and variation as a standalone built-in. The progression DSL is the most complex piece (lexer + parser + AST + evaluator + compiler); polyrhythm and variation are straightforward function registrations.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** New `progression` keyword with pipe syntax: `progression | I IV vi V |` -- parsed by lexer/parser into a `ProgressionExpression` AST node. Matches existing note stream `| ... |` style.
- **D-02:** Each chord fills one bar by default. Optional `:N` suffix for multi-bar chords: `progression | I:2 IV vi V:2 |` (I and V get 2 bars each).
- **D-03:** Progressions resolve roman numerals using the active `key` context (same as existing note stream roman numeral resolution via `HarmonyFunctions`).
- **D-04:** Output is a `Sequence` value -- directly usable with `renderSequence`, `renderSong`, transforms, etc.
- **D-05:** Configurable voice count: user can specify number of voices. Default: match chord note count (3 for triads, 4 for 7th chords).
- **D-06:** Voice leading minimizes movement in upper voices -- each voice moves to the nearest available chord tone. Bass voice follows chord root.
- **D-07:** Voice leading algorithm applies between adjacent chords in the progression. First chord uses root position.
- **D-08:** Optional voice count parameter in syntax or via a configuration function. Exact syntax is Claude's discretion.
- **D-09:** Users create sections with different `timesig` blocks, then combine with `polyrhythm(section1, section2)` built-in function.
- **D-10:** Auto-calculates LCM of the two time signatures for cycle alignment (e.g., 3/4 + 4/4 = 12 beats). Both patterns loop until they meet.
- **D-11:** Optional `beats` parameter overrides auto-calculation: `polyrhythm(waltz, groove, 8)` cuts at 8 beats.
- **D-12:** Each section renders with its own time grid independently, then both are mixed into a single stereo buffer.
- **D-13:** Four mutation types: pitch shift (by scale degrees, stay in key), rhythm variation (split/merge notes, preserve bar length), rest insertion (replace notes with rests), velocity variation (alter dynamics).
- **D-14:** Two overload styles: simple `vary(sequence, 0.3)` for 30% overall mutation, and specific `vary(sequence, 0.3, "pitch")` for controlling which mutation type.
- **D-15:** Simple overload randomly selects mutation type per note. Specific overload applies only the named type.
- **D-16:** Optional seed parameter for reproducible variations: `vary(sequence, 0.3, 42)` or `vary(sequence, 0.3, "pitch", 42)`.
- **D-17:** Pitch mutations stay within the active key context (diatonic movement). If no key context, use chromatic movement.
- **D-18:** All functions return new Sequence values -- never mutate input. Composable via `->`: `melody -> vary(0.3) -> transpose(2)`.

### Claude's Discretion
- Exact voice leading algorithm implementation (nearest-neighbor vs. more sophisticated)
- Whether voice count for progressions is a syntax parameter or a separate function
- Internal data structures for voice state tracking across chord changes
- How `vary` interacts with existing `(? ...)` random choice in note streams (they're separate features)
- polyrhythm function signature details (Section vs Sequence arguments)

### Deferred Ideas (OUT OF SCOPE)
- **Vocaloid support** -- Audio synthesis from text/phonemes. Entirely new capability requiring text-to-speech synthesis pipeline. Belongs in its own phase or future milestone.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| COMP-01 | User can write chord progressions with a DSL that auto-generates voicings | Progression DSL (D-01 through D-04): new token, AST node, parser, evaluator, ProgressionCompiler using existing ScaleDatabase.ResolveRomanNumeral() and ChordParser |
| COMP-02 | Chord DSL resolves voice leading (minimal movement between chords) | Voice leading (D-05 through D-08): nearest-neighbor algorithm operating on MIDI pitch values, bass follows root, upper voices minimize movement |
| COMP-03 | User can write polyrhythmic patterns with overlapping time signatures | Polyrhythm built-in (D-09 through D-12): LCM calculation, independent rendering via SequenceRenderer, mix via MixVoicesToStereoBuffer pattern from SongRenderer |
| COMP-04 | User can generate probabilistic pattern variations from a source sequence | Variation built-in (D-13 through D-17): TransformNotes pattern from TransformFunctions, scale-aware pitch mutation via ScaleDatabase, seeded randomness via Utils.FRand/SetSeed |
</phase_requirements>

## Standard Stack

### Core (No New Dependencies)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 9 | net9.0 | Runtime | Already in use |
| C# 13 | Latest | Language | Record types for AST, pattern matching for dispatch |

No external dependencies needed. All four features are hand-rolled using existing infrastructure.

## Architecture Patterns

### Recommended New Files

```
flow-lang/
  Ast/Expressions/
    ProgressionExpression.cs          # New AST node (record type)
  Runtime/
    ProgressionCompiler.cs            # Compiles progression -> Sequence with voice leading
  StandardLibrary/
    Composition/
      PolyrhythmFunctions.cs          # polyrhythm() built-in
      VariationFunctions.cs           # vary() built-in overloads
```

### Modified Files

```
flow-lang/
  Lexing/TokenType.cs                 # Add Progression token
  Lexing/SimpleLexer.cs               # Recognize "progression" keyword
  Parsing/Parser.cs                   # ParseProgressionExpression
  Interpreter/ExpressionEvaluator.cs  # Evaluate ProgressionExpression
  StandardLibrary/BuiltInFunctions.cs # Register polyrhythm, vary
  Core/FlowEngine.cs                  # Call registration methods
```

### Pattern 1: Progression DSL (Lexer -> Parser -> AST -> Evaluator -> Compiler)

**What:** Full pipeline addition following the note stream pattern.

**Lexer:** Add `Progression` to `TokenType` enum. In `SimpleLexer`, recognize `"progression"` as a keyword identifier (same pattern as `tempo`, `key`, `section`, etc.).

**Parser:** When `Progression` token is matched, parse `| element element ... |` content similarly to `ParseNoteStream()`, but elements are roman numerals (with optional `:N` suffix for bar count) rather than notes.

**AST Node:**
```csharp
// New record in Ast/Expressions/
public record ProgressionElement(
    SourceLocation Location,
    string Numeral,           // "I", "IV", "vi", "V7"
    int BarCount              // Default 1, overridden by :N suffix
);

public record ProgressionExpression(
    SourceLocation Location,
    IReadOnlyList<ProgressionElement> Chords,
    int? VoiceCount           // Optional voice count parameter (null = auto)
) : Expression(Location);
```

**Evaluator dispatch:**
```csharp
// In ExpressionEvaluator switch
ProgressionExpression progression => EvaluateProgression(progression),
```

**Compiler:**
```csharp
// ProgressionCompiler.cs
public class ProgressionCompiler
{
    public SequenceData Compile(ProgressionExpression expr, MusicalContext context)
    {
        // 1. Resolve each roman numeral to ChordData via ScaleDatabase.ResolveRomanNumeral()
        // 2. Apply voice leading between adjacent chords
        // 3. Build bars (one per chord, or N per chord with :N suffix)
        // 4. Return SequenceData
    }
}
```

### Pattern 2: Voice Leading Algorithm (Nearest-Neighbor)

**What:** Minimize pitch movement between adjacent chords in a progression.

**Algorithm (recommended: nearest-neighbor with bass constraint):**
1. First chord: root position at octave 3 (bass) and octave 4 (upper voices)
2. For each subsequent chord:
   - Bass voice: always takes chord root (at octave 3)
   - Upper voices: each voice moves to the nearest available chord tone (by MIDI pitch distance)
   - Use greedy matching: sort upper voices by their current pitch, assign each to the nearest unassigned chord tone

**Implementation uses existing MIDI helpers:**
```csharp
// Reuse TransformFunctions.ToMidi() and FromMidi() pattern
// ChordParser already produces note names with octaves
// ScaleDatabase.ResolveRomanNumeral() returns ChordData with NoteNames
```

**Voice state tracking:**
```csharp
// Track MIDI pitch per voice across chord changes
int[] currentVoicePitches;  // e.g., [48, 60, 64, 67] for C3, C4, E4, G4
```

**Edge cases:**
- Variable chord sizes (triad followed by 7th chord): add/drop voice as needed
- User-specified voice count: double notes (double the root or fifth) or drop extensions
- First chord root position seeding: place bass at octave 3, spread upper voices within octave 4

### Pattern 3: Polyrhythm (Built-in Function)

**What:** Mix two sequences/sections with different time signatures, looping until LCM alignment.

**Signature options (recommendation: accept both Section and Sequence):**
```csharp
// polyrhythm(Section, Section) -> Buffer
// polyrhythm(Section, Section, Int) -> Buffer  (with beats override)
// polyrhythm(Sequence, Sequence) -> Buffer
// polyrhythm(Sequence, Sequence, Int) -> Buffer
```

**LCM calculation:**
```csharp
// 3/4 + 4/4: LCM(3, 4) = 12 beats
// 5/4 + 4/4: LCM(5, 4) = 20 beats
// 7/8 + 4/4: convert to common denominator first, then LCM of numerators
static int Lcm(int a, int b) => a / Gcd(a, b) * b;
static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
```

**Rendering approach (follows SongRenderer.RenderSection pattern):**
1. Determine total duration in beats (LCM or user override)
2. Render each sequence independently via `SequenceRenderer.RenderSequenceToVoices()`, looping as needed
3. Mix using `MixVoicesToStereoBuffer()` pattern (already exists in SongRenderer)

**Time signature extraction:**
- From Section: `section.Context?.TimeSignature`
- From Sequence: inspect `sequence.Bars[0].TimeSignature`

### Pattern 4: Pattern Variation (Built-in Transform)

**What:** Probabilistically mutate notes in a sequence.

**Registration pattern (follows TransformFunctions exactly):**
```csharp
// vary(Sequence, Double) -> Sequence              (random mutation type)
// vary(Sequence, Double, String) -> Sequence       (specific mutation type)
// vary(Sequence, Double, Int) -> Sequence          (random + seed)
// vary(Sequence, Double, String, Int) -> Sequence  (specific + seed)
```

**Four mutation types:**
1. **Pitch shift:** Move note up/down by 1-2 scale degrees. Use `ScaleDatabase.GetScaleNotes()` with active key, find current note's position in scale, shift by random offset. Fall back to chromatic (+/-1 or 2 semitones) if no key.
2. **Rhythm variation:** Split a note into two shorter notes (e.g., quarter -> two eighths) or merge two adjacent notes into one longer note. Must preserve bar total duration.
3. **Rest insertion:** Replace a note with a rest of the same duration.
4. **Velocity variation:** Adjust velocity by +/- 0.1-0.3, clamped to [0.05, 1.0].

**Implementation uses `TransformNotes()` pattern** from TransformFunctions -- iterate all bars, all notes, apply transform, return new SequenceData.

**Seeded randomness:** Use `Utils.SetSeed()` / `Utils.FRand()` pattern already established by `RandomChoiceElement` and `(?? ...)` syntax. For seed parameter, create a local `Random` instance rather than mutating global state.

### Anti-Patterns to Avoid
- **Mutating input sequences:** All operations MUST return new SequenceData/BarData instances (D-18 is locked).
- **Modifying global Random state for seeded vary:** Create a local `Random(seed)` instead of calling `Utils.SetSeed()` which affects all randomness globally.
- **Putting voice leading in the parser:** Voice leading is a semantic operation requiring key context resolution. It belongs in the compiler/evaluator, not the parser.
- **Rendering polyrhythm at parse time:** Polyrhythm produces an AudioBuffer, not a Sequence. It must be a runtime function, not syntax.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Roman numeral resolution | Custom resolver | `ScaleDatabase.ResolveRomanNumeral()` | Already handles all qualities (7, maj7, dim, etc.) and both major/minor keys |
| Chord note expansion | Custom interval math | `ChordParser.TryParse()` + `ChordData.NoteNames` | Already handles 18 chord qualities with correct interval sets |
| Scale degree lookup | Custom scale tables | `ScaleDatabase.GetScaleNotes()` | Already has major and minor scales for all keys |
| MIDI pitch conversion | Custom note math | `TransformFunctions.ToMidi()` / `FromMidi()` pattern | Already handles all notes, octaves, alterations |
| Voice mixing | Custom buffer mixing | `SongRenderer.MixVoicesToStereoBuffer()` pattern | Already handles stereo panning, gain, frame positioning |
| Sequence rendering | Custom render loop | `SequenceRenderer.RenderSequenceToVoices()` | Already handles bar timelines, voice allocation |

**Key insight:** Nearly all building blocks exist. The new code is mostly orchestration -- connecting existing pieces in new configurations.

## Common Pitfalls

### Pitfall 1: Voice Leading Octave Drift
**What goes wrong:** Without anchoring, voice leading can drift all voices into the same octave or diverge to extreme ranges over many chord changes.
**Why it happens:** Nearest-neighbor without constraints will collapse voices toward a cluster.
**How to avoid:** Constrain bass to octave 2-3 range, upper voices to octave 3-5 range. Re-spread if voices get within 2 semitones of each other.
**Warning signs:** All chord tones playing in the same octave; voice crossing (alto above soprano).

### Pitfall 2: Polyrhythm Duration Mismatch
**What goes wrong:** Sequences don't loop cleanly because their actual rendered duration doesn't match the calculated LCM.
**Why it happens:** SequenceData.TotalBeats might not exactly match the expected beat count due to pickup bars or rounding.
**How to avoid:** When looping, calculate exact sample count from LCM beats and tempo, then repeat-render each sequence to fill that exact sample count. Don't rely on TotalBeats for the loop boundary.
**Warning signs:** Audible click or gap at loop points.

### Pitfall 3: Rhythm Variation Breaking Bar Duration
**What goes wrong:** After splitting/merging notes, the bar's total duration no longer matches the time signature.
**Why it happens:** Splitting a note creates two notes whose durations may not sum to the original (due to NoteValue being discrete: whole, half, quarter, etc.).
**How to avoid:** Only split into exact halves (whole->half, half->quarter, quarter->eighth, eighth->sixteenth). Only merge adjacent notes of the same duration. Validate bar duration after mutation.
**Warning signs:** BarData.ValidateDuration() returning false after variation.

### Pitfall 4: Scale-Aware Pitch Mutation Without Key Context
**What goes wrong:** `vary(sequence, 0.3, "pitch")` fails or produces chromatic results when no `key` context is active.
**Why it happens:** ScaleDatabase.GetScaleNotes() requires a key name. Without key context, there are no scale degrees to shift by.
**How to avoid:** D-17 explicitly states: "If no key context, use chromatic movement." Detect null key and fall back to +/- 1-2 semitones using MIDI arithmetic.
**Warning signs:** Exception from ScaleDatabase when key is null.

### Pitfall 5: Progression Without Key Context
**What goes wrong:** `progression | I IV V |` outside a `key` block produces silence or errors.
**Why it happens:** Roman numerals require a key to resolve. `ScaleDatabase.ResolveRomanNumeral()` needs a key name.
**How to avoid:** Check for key context at evaluation time. If null, report a clear error: "progression requires an active key context (use `key Cmajor { ... }`)".
**Warning signs:** ResolveRomanNumeral returning null for every chord.

### Pitfall 6: Voice Count Mismatch Between Chords
**What goes wrong:** Progression has triads (3 notes) and 7th chords (4 notes) mixed. Voice count changes mid-progression.
**Why it happens:** D-05 says default voice count matches chord note count, which varies.
**How to avoid:** When voice count is not explicitly set, use the maximum chord size in the progression as the voice count. For chords with fewer notes, double the root or fifth to fill voices.
**Warning signs:** Array index out of bounds in voice tracking array.

## Code Examples

### Progression Compiler Core Logic
```csharp
// Source: Verified from existing ScaleDatabase.ResolveRomanNumeral() and ChordParser patterns

public SequenceData Compile(ProgressionExpression expr, MusicalContext context)
{
    if (context.Key == null)
        throw new InvalidOperationException("progression requires an active key context");

    var timeSig = context.TimeSignature ?? new TimeSignatureData(4, 4);
    var sequence = new SequenceData();
    
    // Resolve all chords first
    var chords = new List<(ChordData chord, int barCount)>();
    foreach (var elem in expr.Chords)
    {
        var chordData = ScaleDatabase.ResolveRomanNumeral(elem.Numeral, context.Key);
        if (chordData == null)
            throw new InvalidOperationException($"Cannot resolve '{elem.Numeral}' in key {context.Key}");
        chords.Add((chordData, elem.BarCount));
    }
    
    // Determine voice count
    int voiceCount = expr.VoiceCount ?? chords.Max(c => c.chord.NoteNames.Length);
    
    // Apply voice leading and build bars
    int[] currentPitches = InitializeVoices(chords[0].chord, voiceCount);
    foreach (var (chord, barCount) in chords)
    {
        currentPitches = ApplyVoiceLeading(currentPitches, chord, voiceCount);
        for (int b = 0; b < barCount; b++)
        {
            var bar = BuildBar(currentPitches, timeSig);
            sequence.AddBar(bar);
        }
    }
    
    return sequence;
}
```

### Voice Leading (Nearest-Neighbor)
```csharp
// Each voice moves to nearest available chord tone; bass follows root

private int[] ApplyVoiceLeading(int[] prevPitches, ChordData chord, int voiceCount)
{
    // Get target pitches from chord (all octave instances within range)
    var chordTones = GetChordTonesInRange(chord, octaveLow: 2, octaveHigh: 5);
    
    var newPitches = new int[voiceCount];
    var usedTones = new HashSet<int>();
    
    // Bass voice: nearest chord root
    int rootPitch = FindNearestPitch(prevPitches[0], GetRootPitches(chord, 2, 3));
    newPitches[0] = rootPitch;
    
    // Upper voices: greedy nearest-neighbor
    for (int v = 1; v < voiceCount; v++)
    {
        int nearest = FindNearestUnused(prevPitches[v], chordTones, usedTones);
        newPitches[v] = nearest;
        usedTones.Add(nearest);
    }
    
    return newPitches;
}
```

### Polyrhythm LCM Mixing
```csharp
// Source: Derived from SongRenderer.MixVoicesToStereoBuffer() pattern

private static Value Polyrhythm(IReadOnlyList<Value> args)
{
    var seq1 = args[0].As<SequenceData>();
    var seq2 = args[1].As<SequenceData>();
    
    var ts1 = seq1.Bars[0].TimeSignature!;
    var ts2 = seq2.Bars[0].TimeSignature!;
    
    int lcmBeats = Lcm(ts1.Numerator, ts2.Numerator);
    // Override if 3rd arg provided
    if (args.Count > 2) lcmBeats = args[2].As<int>();
    
    double bpm = 120.0; // From context
    int sampleRate = 44100;
    
    // Render each sequence looped to fill LCM duration
    var voices1 = RenderLooped(seq1, lcmBeats, "piano", sampleRate, bpm);
    var voices2 = RenderLooped(seq2, lcmBeats, "piano", sampleRate, bpm);
    
    var allVoices = new List<Voice>();
    allVoices.AddRange(voices1);
    allVoices.AddRange(voices2);
    
    // Mix using existing pattern
    return Value.Buffer(MixVoicesToStereoBuffer(allVoices, bpm, sampleRate, lcmBeats));
}
```

### Variation Engine
```csharp
// Source: Follows TransformFunctions.TransformNotes() pattern

private static SequenceData ApplyVariation(SequenceData seq, double probability, 
    string? mutationType, Random rng, string? keyContext)
{
    var result = new SequenceData();
    foreach (var bar in seq.Bars)
    {
        var newNotes = new List<MusicalNoteData>();
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest || rng.NextDouble() >= probability)
            {
                newNotes.Add(note);
                continue;
            }
            
            string type = mutationType ?? PickRandomMutationType(rng);
            var mutated = type switch
            {
                "pitch" => MutatePitch(note, rng, keyContext),
                "rhythm" => MutateRhythm(note, rng),
                "rest" => new MusicalNoteData(' ', 0, 0, note.DurationValue, isRest: true),
                "velocity" => MutateVelocity(note, rng),
                _ => note
            };
            newNotes.Add(mutated);
        }
        result.AddBar(new BarData(newNotes, bar.TimeSignature!));
    }
    return result;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Roman numerals only in note streams | Progression DSL as first-class syntax | This phase | Chord progressions become declarative rather than manual chord-by-chord |
| Manual chord voicing via note streams | Auto voice leading from progression DSL | This phase | Users don't need to specify inversions |
| Single time signature per piece | Polyrhythm overlays | This phase | Complex rhythmic textures become possible |
| Manual pattern creation | Probabilistic variation | This phase | Generative composition becomes trivial |

## Open Questions

1. **Voice count syntax vs function**
   - What we know: D-08 says optional voice count, syntax or function is Claude's discretion
   - Recommendation: Add optional `voices N` modifier after the progression keyword: `progression voices 4 | I IV V |`. This keeps it declarative and avoids a separate configuration function that could get out of sync.

2. **Polyrhythm function returns Buffer vs Sequence**
   - What we know: D-12 says "mixed into a single stereo buffer" -- so it returns Buffer, not Sequence
   - What's unclear: Users may want to apply further transforms after polyrhythm, which work on Sequences
   - Recommendation: Return Buffer as specified. Users who need Sequence-level operations should compose sequences before calling polyrhythm.

3. **Key context threading for vary**
   - What we know: D-17 says pitch mutations stay in key. Key comes from MusicalContext.
   - What's unclear: How to pass ExecutionContext to a built-in function (built-ins get `IReadOnlyList<Value>`, not the context)
   - Recommendation: Add an overload that accepts a key string explicitly: `vary(sequence, 0.3, "pitch", "Cmajor")`. Also check if the registry supports context-aware functions (some built-ins like `print` access context implicitly). Investigate `InternalFunctionRegistry` for context injection patterns.

4. **Polyrhythm synthesizer selection**
   - What we know: D-12 says render independently then mix
   - What's unclear: How to specify instrument per sequence (SongRenderer uses a single synthType string)
   - Recommendation: Default to "piano". If sections have instrument metadata, use it. This can be enhanced later.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Flow .flow test scripts (no unit test framework) |
| Config file | none -- tests are .flow scripts run directly |
| Quick run command | `dotnet run --project flow-interpreter tests/test_composition.flow` |
| Full suite command | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| COMP-01 | Progression DSL parses and produces Sequence | integration | `dotnet run --project flow-interpreter tests/test_progression.flow` | Wave 0 |
| COMP-02 | Voice leading minimizes movement | integration | `dotnet run --project flow-interpreter tests/test_voice_leading.flow` | Wave 0 |
| COMP-03 | Polyrhythm overlays different time signatures | integration | `dotnet run --project flow-interpreter tests/test_polyrhythm.flow` | Wave 0 |
| COMP-04 | Pattern variation produces mutated sequences | integration | `dotnet run --project flow-interpreter tests/test_variation.flow` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet run --project flow-interpreter tests/test_composition.flow`
- **Per wave merge:** Full test suite
- **Phase gate:** Full suite green before verify

### Wave 0 Gaps
- [ ] `tests/test_progression.flow` -- covers COMP-01: basic progression parsing, key context requirement, bar count suffix, output is Sequence
- [ ] `tests/test_voice_leading.flow` -- covers COMP-02: voice leading produces smooth transitions (verify via str output of note pitches)
- [ ] `tests/test_polyrhythm.flow` -- covers COMP-03: polyrhythm with different time signatures, LCM calculation, beats override
- [ ] `tests/test_variation.flow` -- covers COMP-04: vary with probability, specific mutation types, seeded reproducibility

## Sources

### Primary (HIGH confidence)
- Codebase: `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs` -- Roman numeral resolution, scale notes, key parsing
- Codebase: `flow-lang/StandardLibrary/Harmony/ChordParser.cs` -- Chord symbol parsing, 18 quality types, interval expansion
- Codebase: `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` -- Existing chord/harmony built-in registrations
- Codebase: `flow-lang/Runtime/NoteStreamCompiler.cs` -- Note stream compilation pattern (630 lines, fully analyzed)
- Codebase: `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` -- Transform registration pattern, MIDI helpers, TransformNotes utility
- Codebase: `flow-lang/StandardLibrary/Audio/SongRenderer.cs` -- Section rendering, voice mixing, MixVoicesToStereoBuffer
- Codebase: `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` -- Sequence-to-voices rendering pipeline
- Codebase: `flow-lang/Ast/Expressions/NoteStreamExpression.cs` -- AST record patterns for note stream elements
- Codebase: `flow-lang/Lexing/TokenType.cs` -- 90 token types, enum structure
- Codebase: `flow-lang/StandardLibrary/Utils.cs` -- Random number generation, FRand, seeded randomness

### Secondary (MEDIUM confidence)
- Music theory: Nearest-neighbor voice leading is the standard algorithmic approach used in computational musicology. Bass-follows-root is standard practice for 4-part writing.
- Music theory: LCM-based polyrhythm alignment is the mathematically correct approach (3:4 polyrhythm = 12 subdivision units).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all existing infrastructure verified in codebase
- Architecture: HIGH -- all integration points verified, patterns established by existing features (note streams, transforms, rendering)
- Pitfalls: HIGH -- identified from direct analysis of data types and existing code behavior
- Voice leading algorithm: MEDIUM -- nearest-neighbor is standard but edge cases (voice crossing, octave drift) need careful testing

**Research date:** 2026-04-02
**Valid until:** 2026-05-02 (stable -- no external dependency version concerns)
