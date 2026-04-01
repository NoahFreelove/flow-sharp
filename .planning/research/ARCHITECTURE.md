# Architecture Patterns

**Domain:** Music programming language - feature expansion
**Researched:** 2026-03-29

## Current Architecture

Flow follows a clean pipeline: `Source -> Lexer -> Tokens -> Parser -> AST -> Interpreter -> Value`. The architecture is well-suited for the planned features because it cleanly separates concerns:

- **Lexer/Parser** changes for new syntax (loops, string interpolation, chord DSL)
- **AST** additions for new node types (ForStatement, WhileStatement, InterpolatedStringExpression)
- **Interpreter** dispatch for new statements/expressions
- **StandardLibrary** extensions for new built-in functions (loadWav, sidechain, pan, writeMidi)
- **Audio pipeline** extensions (voice allocation, custom oscillators)

No architectural rewrites are needed. All features extend the existing structure.

## Component Boundaries

| Component | Responsibility | Extends For |
|-----------|---------------|-------------|
| `Lexing/SimpleLexer.cs` | Token recognition | String interpolation syntax, `for`/`while` keywords, chord progression delimiters |
| `Parsing/Parser.cs` | AST construction | ForStatement, WhileStatement, InterpolatedStringExpression, ChordProgressionExpression |
| `Ast/Statements/` | Statement node types | ForStatement, WhileStatement |
| `Ast/Expressions/` | Expression node types | InterpolatedStringExpression |
| `Interpreter/Interpreter.cs` | Statement execution | Loop execution, break/continue handling |
| `Interpreter/ExpressionEvaluator.cs` | Expression evaluation | Interpolated strings, chord progression evaluation |
| `Runtime/MusicalContext.cs` | Musical state (tempo, key, timesig) | Per-voice context for polyrhythm; beat position tracking for live reload |
| `StandardLibrary/Audio/FileIO.cs` | WAV export | WAV import (loadWav) |
| `StandardLibrary/Audio/DSP/Compressor.cs` | Dynamic range compression | Sidechain compression variant |
| `StandardLibrary/Audio/SongRenderer.cs` | Song-to-buffer rendering | Voice allocation integration, per-voice panning |
| `StandardLibrary/Audio/Synthesizers/` | Instrument rendering | Custom oscillator adapter (wraps user procs) |
| `StandardLibrary/Harmony/` | Chord/scale operations | Chord progression DSL, voice leading algorithm |
| `Audio/AudioPlaybackManager.cs` | Playback lifecycle | Beat-synced reload scheduling |

## Patterns to Follow

### Pattern 1: Built-in Function Registration

All new features exposed to Flow code follow the same registration pattern.

**When:** Adding any new function callable from Flow code.
**Example:**
```csharp
// In a new or existing registration file
public static void Register(InternalFunctionRegistry registry)
{
    var sig = new FunctionSignature("loadWav", [StringType.Instance]);
    registry.Register("loadWav", sig, args => {
        string path = args[0].As<string>();
        var buffer = WavReader.Import(path);
        return Value.Buffer(buffer);
    });
}
```

### Pattern 2: AST Node as Immutable Record

All new AST nodes must be C# records (project convention).

**When:** Adding new syntax (loops, interpolated strings, chord progressions).
**Example:**
```csharp
// In Ast/Statements/
public record ForStatement(
    string IteratorName,
    FlowType IteratorType,
    Expression Collection,
    List<Statement> Body
) : Statement;
```

### Pattern 3: DSP as Static Methods Returning New Buffers

All audio processing returns new buffers; inputs are never modified. This is a strict convention throughout the DSP code.

**When:** Adding sidechain compression, panning, sample manipulation.
**Example:**
```csharp
public static AudioBuffer ApplyPan(AudioBuffer input, float pan)
{
    var result = new AudioBuffer(input.Frames, 2, input.SampleRate);
    float angle = pan * MathF.PI / 2f;
    float leftGain = MathF.Cos(angle);
    float rightGain = MathF.Sin(angle);
    for (int f = 0; f < input.Frames; f++)
    {
        float sample = input.Channels == 1
            ? input.Data[f]
            : (input.Data[f * 2] + input.Data[f * 2 + 1]) * 0.5f;
        result.Data[f * 2] = sample * leftGain;
        result.Data[f * 2 + 1] = sample * rightGain;
    }
    return result;
}
```

### Pattern 4: Synthesizer Interface

New instruments (including custom oscillators) follow the `INoteSynthesizer` pattern.

**When:** Adding custom oscillator support.
**Example:**
```csharp
public class UserDefinedSynthesizer : INoteSynthesizer
{
    private readonly Value _userProc;
    private readonly Interpreter _interpreter;

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
    {
        // Call user-defined proc: myOsc(frequency, duration, sampleRate)
        var result = _interpreter.CallFunction(_userProc, freq, dur, sr);
        return result.As<AudioBuffer>();
    }
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Mutating Audio Buffers In-Place
**What:** Modifying an `AudioBuffer.Data` array directly in DSP functions.
**Why bad:** Breaks referential transparency; a buffer passed to two effects would get corrupted. Every existing DSP function creates a new buffer.
**Instead:** Always return a new `AudioBuffer` from DSP operations.

### Anti-Pattern 2: Adding External Dependencies for Simple DSP
**What:** Pulling in NWaves or NAudio for a single DSP function.
**Why bad:** Creates dependency on external library lifecycle, duplicates existing hand-rolled DSP stack, adds transitive dependencies.
**Instead:** Implement DSP math directly. Panning is 3 lines. Sidechain is a modification of existing compressor. WAV reading is the inverse of existing WAV writing.

### Anti-Pattern 3: Interpreter-Level Recursion for Loops
**What:** Implementing `for` loops by recursively calling the interpreter for each iteration.
**Why bad:** Stack overflow on large iteration counts; C# stack is limited.
**Instead:** Use a C# `while` loop in the interpreter that evaluates the body, checks the condition, and continues.

### Anti-Pattern 4: Global State for Voice Allocation
**What:** Storing voice pool as a static/global.
**Why bad:** Breaks concurrent execution, makes testing hard, couples unrelated features.
**Instead:** Voice allocator should be scoped to a song render or section render context, passed through the rendering pipeline.

## Data Flow for New Features

### WAV Import Data Flow
```
Flow code: Buffer b = loadWav("drums.wav")
  -> BuiltInFunction dispatch
    -> WavReader.Import(path)
      -> BinaryReader reads RIFF/fmt/data chunks
      -> Converts PCM to float32
      -> Returns AudioBuffer
    -> Value.Buffer(audioBuffer)
  -> Variable "b" bound in current StackFrame
```

### Sidechain Compression Data Flow
```
Flow code: sidechain(bass, kick, -20.0, 4.0, 5.0, 50.0)
  -> BuiltInFunction dispatch
    -> Compressor.ApplySidechain(bass, kick, threshold, ratio, attack, release)
      -> For each frame: peak detection on kick buffer
      -> Gain reduction computed from kick's envelope
      -> Applied to bass buffer samples
      -> Returns new AudioBuffer
  -> Value.Buffer(result)
```

### Custom Oscillator Data Flow
```
Flow code: instrument "wobble" wobbleOsc
           section intro { ... }  // uses "wobble" instrument
  -> Interpreter binds "wobble" name to wobbleOsc proc
  -> SongRenderer encounters instrument "wobble"
    -> Creates UserDefinedSynthesizer wrapping the proc
    -> For each note: calls proc(freq, duration, sampleRate)
    -> Proc evaluates in interpreter, returns Buffer
    -> Buffer used as voice audio
```

### Loop Execution Data Flow
```
Flow code: for Int i in (range 0 8) { ... }
  -> Parser produces ForStatement(iterator="i", type=Int, collection=FunctionCallExpr, body=[...])
  -> Interpreter evaluates collection expression -> gets array [0,1,2,...,7]
  -> For each element: push new scope, bind "i", execute body statements, pop scope
  -> Last body evaluation's value is the loop's value (or Void)
```

## Scalability Considerations

| Concern | Current (small pieces) | At 100+ sections | At 1000+ notes/sequence |
|---------|----------------------|-------------------|------------------------|
| Voice allocation | N/A (implicit) | Need voice pool with limits | Voice stealing prevents unbounded memory |
| Custom oscillators | N/A | Per-sample proc evaluation is slow | Block-based evaluation (call proc once per N samples) |
| WAV loading | N/A | Memory from loaded samples | Lazy loading or sample pool with eviction |
| Render time | Seconds | Minutes without optimization | Consider parallel section rendering |
| Visualization | N/A | Console output size | Paginate or summarize long sequences |

## Sources

- Existing codebase: `flow-lang/Core/FlowEngine.cs`, `flow-lang/Interpreter/Interpreter.cs`, `flow-lang/StandardLibrary/Audio/`
- CLAUDE.md architecture documentation
- Existing synthesizer pattern: `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs`
- Existing DSP pattern: `flow-lang/StandardLibrary/Audio/DSP/Compressor.cs`
