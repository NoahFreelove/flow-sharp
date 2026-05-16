# Phase 33: SFZ Orchestral Sampler — Pattern Map

**Mapped:** 2026-05-15
**Files analyzed:** 21 (15 new, 6 modified)
**Analogs found:** 21 / 21 (100% — every new file has a direct in-repo precedent)

## File Classification

### New Files

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `flow-lang/sfz.flow` | stdlib module | side-effecting load (forward-decl + init marker) | `flow-lang/notation.flow` (forward-decls) + `flow-lang/audio.flow` (proc + constants block) | role-match (stdlib) |
| `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` | type (sealed singleton) | none — used by type checker + factory | `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` | exact |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzData.cs` | data model (record) | parser output → renderer input | `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` `ParsedScala` record (lines 23-28) | exact |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs` | data model (record) | parser output → grid + renderer | `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` `ParsedScala` record | exact |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzLoopMode.cs` | enum | parser output → renderer branch | new — no enum analog in Tuning subdir; trivial |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` | parser (hand-rolled, INI-style) | string → SfzData (file content + path → parsed) | `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` | exact (parser, same strict-numeric posture) |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzParseException.cs` | exception | thrown from parser | `flow-lang/StandardLibrary/Audio/Tuning/ScalaParseException.cs` | exact |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs` | cache (per-engine) | eager-load + memoize varispeed shifts | `flow-lang/StandardLibrary/Audio/SampleCache.cs` | exact |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` | renderer | MusicalNoteData + SfzData → AudioBuffer | `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` | exact (sample-based renderer with Phase 28 envelope hook) |
| `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs` | builtin registration | wires `loadSfz` + `__enableSfzModule` overloads into registry | `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` | exact |
| `flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz` | test fixture | parser + renderer input | `flow-lang.Tests/fixtures/scala/partch_43.scl` (committed fixture pattern) | role-match |
| `flow-lang.Tests/fixtures/sfz-smoke/C4_sine.wav` + `G5_sine.wav` | test fixture (binary) | SFZ `sample=` target | `flow-lang/Samples/<instrument>/*.wav` shape (under repo-size cap) | role-match |
| `flow-lang.Tests/Tools/Phase33FixtureGenerator.cs` | one-shot helper | regenerates fixture WAVs from seed | new — no precedent; design from sine-burst spec in 33-RESEARCH.md Example 4 |
| `flow-lang.Tests/Unit/Phase33/Sfz*Tests.cs` (7 files) | unit tests | parser + renderer + crossfade + region match | `flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs` + `LoadScalaBuiltinFacts.cs` | exact |
| `flow-lang.Tests/Integration/Phase33/Sfz*Tests.cs` (4 files) | integration tests | end-to-end via FlowEngineRunner | `flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs` | exact |
| `examples/symphony/sfz_smoke.flow` + `README.md` | example | composer-facing showcase | existing `examples/` Flow scripts | role-match (docs) |

### Modified Files

| Modified File | Role | Data Flow | Closest Analog (within file) | Match Quality |
|---------------|------|-----------|------------------------------|---------------|
| `flow-lang/Runtime/ExecutionContext.cs` | runtime state | new fields: `SfzEnabled`, `SfzInstruments`, `SfzPatchRegistry`, `SfzDiagnostics` | existing `SymbolInternTable` property (line 85) | exact (auto-init dict field on context) |
| `flow-lang/Runtime/Value.cs` | factory | new `Value.Sfz(SfzData)` factory method | `Value.Tuning(ResolvedTuning)` (line 60) | exact |
| `flow-lang/Runtime/FlowConfig.cs` | config POCO | new `SfzRoot` field on `FlowConfigPoco` | existing `InstallPath`, `DefaultTempo`, etc. (lines 21-25) | exact |
| `flow-lang/Core/FlowEngine.cs` | engine startup | register `SfzBuiltins`; create `SfzSampleCache`; static accessor | existing `ScalaBuiltins.Register(internalRegistry)` (line 74) + `CurrentSampleCache` static (line 54, 67) | exact |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` | dispatch hook | new `sampler:` prefix branch BEFORE existing instrument switch | existing `FlowEngine.CurrentSampleCache?.EagerLoad(song, synthType)` (line 113) | role-match (added branch in same method) |
| `flow-lang/StandardLibrary/Audio/MidiExport.cs` | dispatch hook | strip `sampler:` prefix + 12 new GM entries | existing `ResolveGmProgram(string seqName)` (lines 60-73) | exact (prefix-match extension) |
| `flow-lang/Interpreter/Interpreter.cs` | typed-binding hook | populate `SfzPatchRegistry` when `varDecl.Type is SfzType` | existing `ExecuteVariableDeclaration` (line 588) — single new branch before `_context.DeclareVariable` at line 646 | exact (precedent: same method already handles type-narrowing branches) |
| (flow-cli/Config/FlowConfigLoader.cs — **no edit needed**) | TOML loader | uses `JsonNamingPolicy.SnakeCaseLower` so `sfz_root` auto-maps to `SfzRoot` | already implemented (lines 34-37) | zero-diff |

## Pattern Assignments

### `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` (type, sealed singleton)

**Analog:** `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` (entire file, 27 LOC)

**Full pattern to copy** (lines 14-27):
```csharp
public sealed class TuningType : FlowType
{
    private TuningType() { }
    public static TuningType Instance { get; } = new();
    public override string Name => "Tuning";
    public override int GetSpecificity() => 137;
    public override bool IsCompatibleWith(FlowType target) => target is TuningType;
    public override bool CanConvertTo(FlowType target) => target is TuningType;
}
```

**Divergence for SfzType:**
- `Name => "Sfz"`
- `GetSpecificity() => 150` (above all music types; existing slots: Tuning=137, Section=138, Beat=139, Song=140, Hertz=144 — per 33-RESEARCH Example 1)
- Both compatibility predicates check `target is SfzType`

---

### `flow-lang/Runtime/Value.cs` (modified — new factory)

**Analog:** `Value.Tuning` factory at line 60 of the same file

**Pattern excerpt** (lines 53-61):
```csharp
/// <summary>
/// Phase 32 Plan 32-04 — wraps a <see cref="StandardLibrary.Audio.Tuning.ResolvedTuning"/>
/// reference in a Flow <see cref="Value"/> typed as <see cref="TuningType.Instance"/>.
/// Identity follows reference equality per CONTEXT D-* / Claude's Discretion: two
/// (loadScala "x.scl") calls produce distinct Values even with identical
/// content (Phase 32 doesn't cache per SPEC out-of-scope).
/// </summary>
public static Value Tuning(StandardLibrary.Audio.Tuning.ResolvedTuning resolved)
    => new(resolved, TuningType.Instance);
```

**Divergence for new Value.Sfz factory:**
- Type parameter: `StandardLibrary.Audio.Sfz.SfzData data`
- Returns: `new(data, SfzType.Instance)`
- XML-doc note that two `(loadSfz #violin)` calls produce distinct Values (per CONTEXT D-12 reference identity)
- Insert immediately after `Value.Tuning` (line 61) to keep music-type factories grouped

---

### `flow-lang/Runtime/FlowConfig.cs` (modified — new POCO field)

**Analog:** Existing `FlowConfigPoco` properties (lines 19-26)

**Pattern excerpt** (lines 19-26):
```csharp
public record FlowConfigPoco
{
    public string? InstallPath { get; init; }
    public string? DefaultAudioDevice { get; init; }
    public int? DefaultTempo { get; init; }
    public string? DefaultTimesig { get; init; }
    public List<string>? StdlibSearchPath { get; init; }
    // ... Defaults static below
}
```

**Divergence:** Add one line `public string? SfzRoot { get; init; }` to the POCO. Tomlyn's `JsonNamingPolicy.SnakeCaseLower` (already configured in `FlowConfigLoader.cs:36`) auto-maps the TOML key `sfz_root` to `SfzRoot` — **no edit needed in `flow-cli/Config/FlowConfigLoader.cs`**. `FlowConfigPoco.Defaults` (line 34) requires no edit (record-init defaults to null for the new nullable field).

---

### `flow-lang/Runtime/ExecutionContext.cs` (modified — 4 new fields)

**Analog:** Existing `SymbolInternTable` property at line 85 (in-context dict field with XML-doc shape)

**Pattern excerpt** (lines 79-85):
```csharp
/// <summary>
/// Per-context Symbol intern table — guarantees pointer equality for <c>#foo</c> literals
/// (Phase 26.1 SYM-01). All <c>Value.Symbol(name, ctx)</c> calls with the same name and the
/// same context return the same <see cref="Value"/> instance, so reference-equality of the
/// Value wrappers is the canonical Symbol equality check.
/// </summary>
public Dictionary<string, Value> SymbolInternTable { get; } = new();
```

**Divergence:** Add four sibling auto-init properties (per 33-RESEARCH Example 2):
1. `public bool SfzEnabled { get; set; } = false;` (flipped true by `__enableSfzModule`)
2. `public Dictionary<Value, string> SfzInstruments { get; } = new();` (Symbol → relative path)
3. `public Dictionary<string, FlowLang.StandardLibrary.Audio.Sfz.SfzData> SfzPatchRegistry { get; } = new();` (variable name → patch data)
4. `public HashSet<string> SfzDiagnostics { get; } = new();` (advisory dedup set; consumed by RenderingDiagnostics-style WarnOnce sentinel)

Cluster the four below `SymbolInternTable` with a `// Phase 33 — SFZ surface` section header comment to mirror the existing "Random Number Generation State" block (lines 31-72).

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` (parser, hand-rolled INI-style)

**Analog:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs` (302 LOC)

**Strict-numeric posture** (lines 44-55):
```csharp
private const int MaxStepCount = 10000;

/// <summary>
/// NumberStyles for cents values. AllowExponent and AllowThousands are
/// excluded per D-18 (strict-reject 1.5e2 and 100,5).
/// </summary>
private const NumberStyles CentsStyle =
    NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands;
```

**Single-pass line walker** (lines 57-86):
```csharp
public static ParsedScala Parse(string content, string filePath)
{
    var lines = content.Split('\n');
    int lineCursor = 0;
    string? description = null;
    while (lineCursor < lines.Length)
    {
        var raw = lines[lineCursor];
        var stripped = StripCr(raw);
        lineCursor++;
        if (stripped.TrimStart().StartsWith('!')) continue;
        if (stripped.Trim().Length == 0) continue;
        description = stripped.Trim();
        break;
    }
    // ...
```

**Strict numeric path with CultureInfo.InvariantCulture** (line 103):
```csharp
if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out stepCount))
{
    throw new ScalaParseException(filePath, stepCountLine, 1,
        "step count (positive integer)", token);
}
```

**DoS cap** (lines 119-125):
```csharp
if (stepCount > MaxStepCount)
{
    throw new ScalaParseException(filePath, stepCountLine, 1,
        "step count <= 10000", token);
}
```

**Trailing CR strip** (lines 300-301):
```csharp
private static string StripCr(string line)
    => line.Length > 0 && line[^1] == '\r' ? line[..^1] : line;
```

**Divergence for SfzParser:**
- Comment marker is `//` (not `!`)
- State machine: track current `<global>` / `<group>` / `<region>` accumulator; inheritance flattened at parse time per CONTEXT Claude's-Discretion ("Header inheritance applied AT PARSE TIME")
- Whitelist `HashSet<string>` of 13 opcode names with `StringComparer.Ordinal` (RESEARCH Pattern 2 + Pitfall 12)
- Unknown opcode path: call `RenderingDiagnostics.WarnOnce` keyed on `($"sfz:opcode:{patchDescription}:{opcodeName}")` and continue parsing (charitable interpretation per CLAUDE.md memory)
- After region list assembled, build the 128×128 grid (D-02 last-declared-wins write-order) and `SortedByPitch[]` side index (D-03)
- DoS cap: `MaxRegionCount = 10000` (same constant shape as `MaxStepCount`)
- Cap: clamp `loop_end` happens at render time, NOT parse time (Pitfall 3 — sample length unknown until WAV load)
- Apply per-region normalizations at parse time (Pitfalls 7, 8): `pan ÷ 100.0` to `[-1.0, +1.0]` Flow range; `volume` dB → linear `Math.Pow(10.0, db / 20.0)` BEFORE storing on the region

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzData.cs` + `SfzRegion.cs` (data models)

**Analog:** `ParsedScala` record at `ScalaParser.cs:23-28`

**Pattern excerpt** (lines 22-28):
```csharp
public sealed record ParsedScala(
    string Description,
    double[] StepCents,
    double PeriodCents,
    IReadOnlyDictionary<int, (int Num, int Den)> Ratios,
    string FilePath);
```

**Divergence for SfzData record:**
```csharp
public sealed record SfzData(
    string Description,
    string BasePath,
    IReadOnlyList<SfzRegion> Regions,
    SfzRegion?[,] Grid,             // [128, 128] keyed by (midi, vel)
    int[] SortedByPitch);
```

**Divergence for SfzRegion record:** 13-field flat record (CONTEXT Claude's-Discretion "SfzRegion field set"):
- `SamplePath: string`
- `PitchKeycenter: int`, `LoKey: int`, `HiKey: int`
- `LoVel: int`, `HiVel: int`
- `LoopMode: SfzLoopMode` (enum: NoLoop, OneShot, LoopContinuous, LoopSustain)
- `LoopStart: int`, `LoopEnd: int`
- `AmpegAttack: double`, `AmpegRelease: double`
- `Volume: double` (stored LINEAR after dB→linear conversion at parse time, per Pitfall 8)
- `Pan: double` (stored in Flow's `[-1.0, +1.0]` range after `÷ 100.0` at parse time, per Pitfall 7)

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs` (builtin registration)

**Analog:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaBuiltins.cs` (entire file, 125 LOC)

**Register-method pattern** (lines 36-50):
```csharp
public static void Register(InternalFunctionRegistry registry)
{
    // 1-arg: loadScala(String) → Tuning
    var sigOne = new FunctionSignature("loadScala", [StringType.Instance]);
    registry.Register("loadScala", sigOne, LoadScalaOneArg);

    // 2-arg: loadScala(String, String) → Tuning
    var sigTwo = new FunctionSignature("loadScala",
        [StringType.Instance, StringType.Instance]);
    registry.Register("loadScala", sigTwo, LoadScalaTwoArg);

    // (str Tuning) → String  per CONTEXT D-04 description format
    var sigStrTuning = new FunctionSignature("str", [TuningType.Instance]);
    registry.Register("str", sigStrTuning, StrTuning);
}
```

**Builtin body pattern** (lines 52-61):
```csharp
private static Value LoadScalaOneArg(System.Collections.Generic.IReadOnlyList<Value> args)
{
    string sclPath = args[0].As<string>();
    string sclContent = File.ReadAllText(sclPath);
    var parsedScl = ScalaParser.Parse(sclContent, sclPath);
    var kbm = ScalaKbmParser.Default(parsedScl);
    var resolved = new ResolvedTuning(parsedScl, kbm);
    FireUnmappedAdvisoryIfNeeded(resolved, kbm);
    return Value.Tuning(resolved);
}
```

**One-shot advisory dedup** (lines 102-123):
```csharp
private static void FireUnmappedAdvisoryIfNeeded(ResolvedTuning resolved, ScalaKbm kbm)
{
    bool anyUnmapped = false;
    int lo = kbm.FirstMidi < 0 ? 0 : kbm.FirstMidi;
    int hi = kbm.LastMidi > 127 ? 127 : kbm.LastMidi;
    for (int midi = lo; midi <= hi; midi++)
    {
        if (resolved.MidiToHz[midi] == 0.0) { anyUnmapped = true; break; }
    }
    if (!anyUnmapped) return;

    RenderingDiagnostics.WarnOnce(
        sentinelKey: $"tuning:unmapped:{resolved.Description}",
        message: $"[tuning] unmapped MIDI keys under '{resolved.Description}' — rendered as rest");
}
```

**Divergence for SfzBuiltins:**
- `Register(InternalFunctionRegistry registry, ExecutionContext context)` — pass context so `__enableSfzModule` can flip `SfzEnabled` and copy the dict into `SfzInstruments`
- Three signatures: `loadSfz(Symbol) → Sfz`, `loadSfz(String) → Sfz`, `__enableSfzModule(Dict) → Void` (the marker called from `sfz.flow`)
- Builtin bodies check `ctx.SfzEnabled` first; throw `UndefinedFunctionError("loadSfz requires 'use \"@sfz\"'")` when false (D-10)
- Symbol overload: look up arg[0] (a Symbol Value) in `ctx.SfzInstruments`; join with `ctx.ResolvedSfzRoot` (cached on first call per Pitfall 2); call `SfzParser.Parse`; wrap with `Value.Sfz(data)`
- String overload: treat arg[0] as absolute path; bypass dict; call parser directly
- Missing `sfz_root`: throw `MissingSfzRootError` with message pointing at `~/.config/flow/config.toml` (no silent default per RESEARCH "Anti-patterns")

---

### `flow-lang/sfz.flow` (stdlib module)

**Analog (forward-decl shape):** `flow-lang/notation.flow:1-25`
**Analog (constants + side-effecting init):** `flow-lang/audio.flow:60-79` (global constants) + Phase 26.2 ERG-05 dormant forward-decl pattern

**Forward-decl pattern** (notation.flow:5-12):
```flow
Note: ===== Internal C# Function Declarations =====

internal proc createMusicalNote (Note: pitch, NoteValue: duration)
internal proc createRest (NoteValue: duration)
internal proc createTimeSignature (Int: numerator, Int: denominator)
```

**Divergence for sfz.flow** (per CONTEXT D-09/D-11 and 33-RESEARCH Pattern 1):
- Forward-decls for `loadSfz(Symbol)` + `loadSfz(String)` + `__enableSfzModule(Dict)`
- Body: `Dict __sfzInstruments = (dict #violin "Strings/Violin/violin-Sustain.sfz" ...)` with all 19 GM entries (CONTEXT D-16's GM-program dict references 16; the 19-symbol GM orchestral list locked in SPEC-2)
- Trailing call: `(__enableSfzModule __sfzInstruments)` — this side-effecting builtin (a) flips `ExecutionContext.SfzEnabled = true`, (b) copies the dict's `(Value Symbol, string path)` pairs into `ExecutionContext.SfzInstruments`
- The exact 19 paths are unverified (Assumption A1) — Plan 33-01 task validates against VSCO-CE 1.1.0 release

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs` (per-engine cache)

**Analog:** `flow-lang/StandardLibrary/Audio/SampleCache.cs` (244 LOC)

**Class-doc + storage model** (lines 9-40):
```csharp
/// <summary>
/// Phase 29 — per-FlowEngine cache for bundled instrument samples.
/// Lifetime = engine lifetime (SPEC D-15). Eager-loads samples on renderSong entry.
/// Idempotent: repeated EagerLoad calls for the same (song, instrument) are no-ops.
/// ...
/// </summary>
public class SampleCache
{
    private readonly Dictionary<(string instrument, int sampleMidi, string velocity), AudioBuffer> _rawCache = new();
    private readonly Dictionary<(string instrument, int sampleMidi, string velocity, int shift), AudioBuffer> _shiftedCache = new();
    private readonly Dictionary<string, List<int>> _availablePitches = new();
    private readonly HashSet<string> _eagerLoadedKeys = new();
```

**Deterministic eager-load via sorted iteration** (lines 87-92):
```csharp
foreach (var pitch in manifest.pitches.OrderBy(p => p))
{
    foreach (var velocity in manifest.velocities.OrderBy(v => v, StringComparer.Ordinal))
    {
        // ... load WAV
    }
}
```

**Varispeed memoization** (lines 149-163):
```csharp
public AudioBuffer? GetVarispeed(string instrument, int sampleMidi, string velocity, int semitonesShift)
{
    var shiftedKey = (instrument, sampleMidi, velocity, semitonesShift);
    if (_shiftedCache.TryGetValue(shiftedKey, out var cached)) return cached;

    var rawKey = (instrument, sampleMidi, velocity);
    if (!_rawCache.TryGetValue(rawKey, out var raw)) return null;

    var shifted = semitonesShift == 0
        ? raw
        : FileIO.VarispeedResample(raw, Math.Pow(2.0, semitonesShift / 12.0));
    _shiftedCache[shiftedKey] = shifted;
    return shifted;
}
```

**Divergence for SfzSampleCache:**
- Constructor signature: `SfzSampleCache()` — no `_samplesRoot` field (sample paths are absolute, resolved against `region.SamplePath` relative to `patch.BasePath` at eager-load)
- Cache key: `(SfzData patch, string samplePath)` for the raw cache; `(SfzData patch, string samplePath, int semitonesShift)` for the shifted cache (the patch reference acts as the per-patch namespace)
- `EagerLoad(SongData song, SfzData patch)` — walk song notes, dereference `patch.Grid[midi, vel]`, collect distinct regions into a HashSet, **then iterate in `.OrderBy(r => r.SamplePath, StringComparer.Ordinal).ThenBy(r => r.PitchKeycenter)` order** (Pitfall 5 — preserves two-run determinism)
- Idempotency key: `$"sfz:{patch.GetHashCode()}:{song.GetHashCode()}"` (parallel to SampleCache's `$"{instrument}:{song.GetHashCode()}"` pattern at line 77)
- Calls `FileIO.LoadWavInternal(Path.Combine(patch.BasePath, region.SamplePath))` to load each WAV
- Onset-trimming via `SampleCache.TrimLeadingSilence(...)` (the existing internal helper) — RESEARCH §"Reusable Assets" notes this MAY not be needed for SFZ (engineer-trimmed); planner can omit on first pass, add if smoke fixture RMS lands below threshold

---

### `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` (renderer)

**Analog:** `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (224 LOC)

**Class doc-comment + algorithm steps** (lines 8-49):
```csharp
/// <summary>
/// Phase 29 — sample-based instrument renderer for the 6 tonal instruments
/// (Piano, Brass, Sax, Strings, Flute, Bell).
///
/// Implements the INoteSynthesizer-shaped Render method without (yet) implementing
/// the interface directly — ...
///
/// Rendering algorithm (REQ-1):
///   1. Look up the closest-pitched sample via SampleCache.NearestSamplePitch.
///   2. Varispeed-shift to the exact target pitch via SampleCache.GetVarispeed.
///   3. Apply velocity: ...
///   4. Trim or zero-pad the resulting mono buffer to the authored note duration.
///   5. Apply the Phase 28 articulation envelope on top ... (REQ-5)
///   6. Wrap in an AudioBuffer at the engine's sample rate via SynthUtils.ToMonoBuffer.
/// </summary>
```

**Render-method entry + rest short-circuit + frame calc** (lines 73-85):
```csharp
public AudioBuffer Render(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
{
    if (note.IsRest)
        return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

    double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
    int targetFrames = (int)(durationSeconds * sampleRate);
    if (targetFrames <= 0)
        return new AudioBuffer(0, 1, sampleRate);

    int targetMidi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
    int sampleMidi = _cache.NearestSamplePitch(_instrument, targetMidi);
    int semitonesShift = targetMidi - sampleMidi;
```

**Trim-to-duration + Phase 28 articulation envelope** (lines 116-132):
```csharp
var fitted = new float[targetFrames];
int copyLen = Math.Min(mono.Length, targetFrames);
Array.Copy(mono, fitted, copyLen);

// Phase 29 REQ-5 / D-17 / D-18 / D-19: Phase 28 articulation envelope applies
// ON TOP of the sample. ...
float[] envelope = SynthUtils.GenerateArticulationADSR(
    note.Articulation,
    baseAttack: 0.005, baseDecay: 0.05, baseSustain: 1.0, baseRelease: 0.05,
    frames: targetFrames, sampleRate: sampleRate, isPercussion: false);
SynthUtils.ApplyEnvelope(fitted, envelope);

return SynthUtils.ToMonoBuffer(fitted, sampleRate);
```

**Divergence for SfzRenderer.Render** (per 33-RESEARCH Patterns 4 + 5 + 6):
1. After `targetMidi` calc: clamp velocity to `[1, 127]` per Pitfall 9 (`int vel = Math.Clamp((int)Math.Round(note.Velocity * 127.0), 1, 127);`)
2. Region match: `SfzRegion? region = patch.Grid[targetMidi, vel];` — single array index, no scan
3. On null region: walk `patch.SortedByPitch[]` for nearest pitch; varispeed-shift the matched region's sample by `Math.Pow(2.0, semitonesShift / 12.0)` via `FileIO.VarispeedResample` (RESEARCH §"Reusable Assets" — verbatim reuse, no duplicate code)
4. On still-null after fallback: `RenderingDiagnostics.WarnOnce(sentinelKey: $"sfz:missing:{patch.Description}:{targetMidi}:{vel}", ...)` + return silence (charitable per RESEARCH Pattern 4 step (d))
5. Sustain-loop branch: when `region.LoopMode ∈ {LoopContinuous, LoopSustain}`, run the 441-frame equal-power sin/cos crossfade per RESEARCH Pattern 5 (~30 LOC inline). Defensive `effectiveLoopEnd = Math.Min(region.LoopEnd, sourceBuffer.Length - 1)` per Pitfall 3
6. Apply `region.Volume` (already linear at parse time per Pitfall 8) and `region.Pan` (already in Flow's `[-1.0, +1.0]` per Pitfall 7) to the rendered buffer
7. Phase 28 envelope: use the region-aware overrides per SPEC-8:
   ```csharp
   baseAttack:  region.AmpegAttack > 0  ? region.AmpegAttack  : 0.005,
   baseRelease: region.AmpegRelease > 0 ? region.AmpegRelease : 0.05,
   ```
   (RESEARCH Pattern 6 + SPEC-8 acceptance criterion)
8. `Render` signature: takes `SfzData patch` instead of `RenderTuning tuning` — the patch IS the renderer state. Or pass both (patch + tuning) if `RenderTuning` ever bites for non-12-TET sampler use (out of scope per SPEC).

---

### `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (modified — new sampler:NAME branch)

**Analog (in-file):** Existing `RenderSong(IReadOnlyList<Value> args)` body at lines 97-132 of the same file

**Pattern excerpt — existing instrument dispatch path** (lines 97-113):
```csharp
public static Value RenderSong(IReadOnlyList<Value> args)
{
    var song = args[0].As<SongData>();
    string synthType = (string)args[1].Data!;

    SynthUtils.ResetNoiseRng();

    // Phase 29 REQ-4 — eager-load instrument samples for this song. Idempotent
    // for repeated (song, instrument) within an engine lifetime. ...
    FlowEngine.CurrentSampleCache?.EagerLoad(song, synthType);

    AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);
    foreach (var sectionRef in song.Sections)
    {
        if (!song.SectionRegistry.TryGetValue(sectionRef.Name, out var sectionData))
            throw new InvalidOperationException(...);
        var sectionBuffer = RenderSection(sectionData, synthType);
        ...
```

**Divergence (per CONTEXT D-13 and 33-RESEARCH Example 3):**
- Insert a `sampler:` branch BEFORE the `FlowEngine.CurrentSampleCache?.EagerLoad(song, synthType)` call (line 113)
- Branch body (paraphrased):
  ```csharp
  if (synthType.StartsWith("sampler:", StringComparison.Ordinal))
  {
      string patchName = synthType.Substring("sampler:".Length);
      var ctx = FlowEngine.CurrentExecutionContext;   // new static accessor (see FlowEngine.cs divergence)
      if (ctx is null || !ctx.SfzPatchRegistry.TryGetValue(patchName, out var patch))
          throw new InvalidOperationException(
              $"Unknown sampler patch '{patchName}'. Known: [{string.Join(", ", ctx?.SfzPatchRegistry.Keys ?? Enumerable.Empty<string>())}]. " +
              $"Did you forget `Sfz {patchName} = (loadSfz #...)`?");
      FlowEngine.CurrentSfzSampleCache?.EagerLoad(song, patch);
      // ... per-section render via SfzRenderer.Render, identical mixing pipeline (RenderSection-equivalent for the sampler path)
      // Returns Value.Buffer(result) — same as Phase 29 path.
  }
  ```
- Existing Phase 29 path stays byte-identical (REQ-determinism gate)

---

### `flow-lang/Core/FlowEngine.cs` (modified — SFZ wiring)

**Analog (in-file):** Existing `ScalaBuiltins.Register(internalRegistry)` call at line 74; existing `CurrentSampleCache` static at lines 54 + 67

**Pattern excerpts:**
```csharp
// Line 54 — static accessor pattern
public static SampleCache? CurrentSampleCache { get; private set; }

// Lines 64-67 — constructor sets static
_sampleCache = new SampleCache();
CurrentSampleCache = _sampleCache;

// Line 74 — registration call
ScalaBuiltins.Register(internalRegistry);

// Lines 213-216 — Dispose clears static if still pointing at this engine's cache
if (ReferenceEquals(CurrentSampleCache, _sampleCache))
    CurrentSampleCache = null;
```

**Divergence (per RESEARCH §"Open Questions" Q4):**
1. Add `_sfzSampleCache = new SfzSampleCache();` field + `SfzSampleCache => _sfzSampleCache` public read + `CurrentSfzSampleCache` static accessor mirroring lines 38-54
2. Add `public static ExecutionContext? CurrentExecutionContext { get; private set; }` static (RESEARCH Assumption A4 — cleanest path)
3. Constructor: assign both `CurrentSfzSampleCache = _sfzSampleCache;` and `CurrentExecutionContext = _context;` after the existing `_context = new RuntimeContext(...)` (line 76)
4. Add `SfzBuiltins.Register(internalRegistry, _context);` call right after the Phase 32 `ScalaBuiltins.Register(internalRegistry);` (line 74)
5. Dispose: mirror lines 213-216 for both new statics

---

### `flow-lang/Interpreter/Interpreter.cs` (modified — typed-binding hook)

**Analog (in-file):** `ExecuteVariableDeclaration` (lines 588-647) — single new branch before `_context.DeclareVariable` at line 646

**Pattern excerpt — current end of method** (lines 638-647):
```csharp
// Convert if needed
if (!value.Type.Equals(varDecl.Type) && value.Type.CanConvertTo(varDecl.Type))
{
    value = value.ConvertTo(varDecl.Type);
}

_context.DeclareVariable(varDecl.Name, value);
```

**Divergence (per CONTEXT D-12):**
Insert one branch BEFORE `_context.DeclareVariable`:
```csharp
// Phase 33 D-12: register typed-Sfz bindings in the patch registry so
// `renderSong song "sampler:violin"` can find the bound patch by name.
if (varDecl.Type is FlowLang.TypeSystem.SpecialTypes.SfzType &&
    value.Data is FlowLang.StandardLibrary.Audio.Sfz.SfzData sfzData)
{
    _context.SfzPatchRegistry[varDecl.Name] = sfzData;
}

_context.DeclareVariable(varDecl.Name, value);
```

Note that reassignment to a same-named variable overwrites the registry entry naturally (per Pitfall 10's documented "last-bound-wins" contract).

---

### `flow-lang/StandardLibrary/Audio/MidiExport.cs` (modified — prefix-strip + 12 new GM entries)

**Analog (in-file):** `ResolveGmProgram(string seqName)` at lines 60-73

**Pattern excerpt** (lines 60-73):
```csharp
private static (int gmProgram, int channel) ResolveGmProgram(string seqName)
{
    if (string.IsNullOrEmpty(seqName)) return (0, 0);
    string lower = seqName.ToLowerInvariant();
    if (lower.StartsWith("piano")) return (0, 0);
    if (lower.StartsWith("brass") || lower.StartsWith("horn")) return (56, 0);
    if (lower.StartsWith("sax")) return (65, 0);
    if (lower.StartsWith("flute")) return (73, 0);
    if (lower.StartsWith("string")) return (48, 0);
    if (lower.StartsWith("organ")) return (19, 0);
    if (lower.StartsWith("bell")) return (14, 0);
    if (lower.StartsWith("drum")) return (0, 9);
    return (0, 0);
}
```

**Divergence (per CONTEXT D-15/D-16 and 33-RESEARCH Pitfall 6):**
- Add `sampler:` prefix strip as the very FIRST statement after the empty-check (Pitfall 6 — must come before the existing `StartsWith` chain to prevent `sampler:flute` bleeding through to the GM-0 fallback):
  ```csharp
  if (lower.StartsWith("sampler:")) lower = lower.Substring("sampler:".Length);
  ```
- Add 12 new entries (CONTEXT D-16): `violin → 40`, `viola → 41`, `cello → 42`, `contrabass → 43`, `oboe → 68`, `clarinet → 71`, `bassoon → 70`, `horn → 60` (overrides existing horn→56 alongside brass — re-validate ordering), `trombone → 57`, `tuba → 58`, `timpani → 47` (channel 9 — percussion), `choir → 52`, `harp → 46`, `guitar → 24`, `harpsichord → 6`, `celeste → 8`
- **Pitfall:** `horn` already maps to 56 (alongside `brass`) — D-16 reassigns `horn → 60` (French horn). Verify ordering puts the more-specific `horn` check BEFORE the `brass` check; otherwise `brass*` swallows it (Pitfall 6 again).
- D-17: MIDI track-name meta-event must use the stripped name (e.g. `"violin"`, not `"sampler:violin"`). The `ResolveGmProgram` rename mirrors this — search-and-update the track-name emission site to use `lower` after strip.

---

### `flow-lang.Tests/Unit/Phase33/SfzParserTests.cs` (and 6 siblings)

**Analog:** `flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs` (lines 1-65)

**FindRepoRoot helper + fixture-loader pattern** (lines 27-43):
```csharp
private static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    throw new InvalidOperationException("Could not locate repo root");
}

private static string LoadFixture(string name)
{
    var path = Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", name);
    return File.ReadAllText(path);
}
```

**Fact pattern** (lines 45-62):
```csharp
[Fact]
public void Partch43_Parses_43Steps_Period2to1_RatiosPreserved()
{
    var content = LoadFixture("partch_43.scl");
    var scl = ScalaParser.Parse(content, "partch_43.scl");

    Assert.Equal("Harry Partch's 43-tone pure scale", scl.Description);
    Assert.Equal(42, scl.StepCents.Length);
    Assert.Equal(1200.0, scl.PeriodCents, precision: 9);
    Assert.True(scl.Ratios.ContainsKey(0), "Ratios should contain key 0 (81/80)");
    Assert.Equal((81, 80), scl.Ratios[0]);
}
```

**Divergence:** Switch fixture directory from `scala` to `sfz-smoke`; switch parser API to `SfzParser.Parse(content, filePath, diagnosticsSink, patchDescription)`; assert on `SfzData.Regions.Count`, `SfzData.Grid[midi, vel]?.SamplePath`, `SfzData.SortedByPitch[]`. The same FindRepoRoot/LoadFixture helpers can be lifted unchanged.

---

### `flow-lang.Tests/Integration/Phase33/SfzSmokeTests.cs` (and 3 siblings)

**Analog:** `flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs` (lines 1-80)

**Test-collection serialization + cwd handling pattern** (lines 29-77):
```csharp
[Collection("FlowScripts")]
public class SampledInstrumentSmokeTests
{
    [Theory]
    [InlineData("piano", true)]
    ...
    public void RenderingTonalInstrument_DoesNotThrow(string instrument, bool hasVelocityLayers)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string sampleDir = Path.Combine(repoRoot, "flow-lang", "Samples", instrument);
        if (!Directory.Exists(sampleDir) || ...)
            return;  // Skip if Plan 01 samples not yet committed

        string originalCwd = Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory = repoRoot;
            using (var runner = new FlowEngineRunner())
            {
                string setupScript = $@"
                    use ""@audio""
                    tempo 120 {{
                        section demo_{instrument} {{
                            Sequence main = | C4q |
                        }}
                    }}
                    Song s = [demo_{instrument}]
                    Buffer rendered = (renderSong s ""{instrument}"")
                ";
                var setup = runner.RunSource(setupScript, $"<setup-{instrument}>");
                Assert.True(setup.Success, ...);
                ...
```

**Divergence:** Switch to `use "@sfz"`, `Sfz violin = (loadSfz "..."${"/path/to/smoke.sfz"})`, `renderSong s "sampler:violin"`. Fixture-skip guard checks `Directory.Exists(Path.Combine(repoRoot, "flow-lang.Tests", "fixtures", "sfz-smoke"))` (test fixture path, NOT the Samples bundle dir).

---

## Shared Patterns

### One-shot Stderr Advisory (Phase 23/32 dedup)

**Source:** `flow-lang/Diagnostics/RenderingDiagnostics.cs:29-36`
**Apply to:** `SfzParser` (unknown opcodes), `SfzRenderer` (missing regions), `SfzBuiltins` (missing `sfz_root`)

```csharp
public static void WarnOnce(string sentinelKey, string message)
{
    lock (_lock)
    {
        if (!_emitted.Add(sentinelKey)) return;
    }
    Console.Error.WriteLine(message);
}
```

**Phase 33 sentinel-key conventions** (per CONTEXT Claude's-Discretion + RESEARCH Example 2):
- Unknown opcode: `$"sfz:opcode:{patchDescription}:{opcodeName}"`
- Missing region: `$"sfz:missing:{patchDescription}:{midi}:{vel}"`
- Missing config: `$"sfz:config:sfz_root_missing"`

Test isolation: every Phase 33 test class with WarnOnce dependencies follows the existing precedent in `flow-lang.Tests/Unit/Phase32/LoadScalaBuiltinFacts.cs:23-24`:
```csharp
public LoadScalaBuiltinFacts() { RenderingDiagnostics.ResetForTesting(); }
public void Dispose()         { RenderingDiagnostics.ResetForTesting(); }
```

### Strict Numeric Parsing

**Source:** `flow-lang/StandardLibrary/Audio/Tuning/ScalaParser.cs:54-55` + line 103
**Apply to:** All numeric parsing in `SfzParser` (int opcodes: lokey/hikey/lovel/hivel/loop_start/loop_end/pitch_keycenter; double opcodes: ampeg_attack/ampeg_release/volume/pan)

```csharp
private const NumberStyles FloatStyle =
    NumberStyles.Float & ~NumberStyles.AllowExponent & ~NumberStyles.AllowThousands;

// Integer parsing (no sign, no decimal, no whitespace):
int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value);

// Double parsing (mask above + InvariantCulture):
double.TryParse(token, FloatStyle, CultureInfo.InvariantCulture, out value);
```

### Deterministic Iteration

**Source:** `flow-lang/StandardLibrary/Audio/SampleCache.cs:88-92`
**Apply to:** `SfzSampleCache.EagerLoad`'s region-iteration order (Pitfall 5 — preserve Phase 18/25/27 two-run byte-identical contract)

```csharp
foreach (var pitch in pitches.OrderBy(p => p))
{
    foreach (var velocity in velocities.OrderBy(v => v, StringComparer.Ordinal))
    {
```

**Phase 33 divergence:** Use `.OrderBy(r => r.SamplePath, StringComparer.Ordinal).ThenBy(r => r.PitchKeycenter)` over the collected unique-regions HashSet before iterating (RESEARCH §"Pattern 7" — the unsorted HashSet iteration is the Pitfall 5 hazard).

### Phase 28 Articulation Envelope Hook

**Source:** `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs:126-132`
**Apply to:** `SfzRenderer.Render` (after region match + loop expansion + volume/pan application)

```csharp
float[] envelope = SynthUtils.GenerateArticulationADSR(
    note.Articulation,
    baseAttack: 0.005, baseDecay: 0.05, baseSustain: 1.0, baseRelease: 0.05,
    frames: targetFrames, sampleRate: sampleRate, isPercussion: false);
SynthUtils.ApplyEnvelope(fitted, envelope);
return SynthUtils.ToMonoBuffer(fitted, sampleRate);
```

**Phase 33 divergence:** Per-region attack/release overrides per SPEC-8 acceptance:
```csharp
baseAttack:  region.AmpegAttack  > 0 ? region.AmpegAttack  : 0.005,
baseRelease: region.AmpegRelease > 0 ? region.AmpegRelease : 0.05,
```

### Varispeed Pitch Shift

**Source:** `flow-lang/StandardLibrary/Audio/FileIO.cs:338-355` (`VarispeedResample`)
**Apply to:** `SfzRenderer` (nearest-pitch fallback) + `SfzSampleCache.GetVarispeed` (memoized shifted-cache)

```csharp
public static AudioBuffer VarispeedResample(AudioBuffer source, double ratio)
{
    int newFrames = (int)Math.Round(source.Frames / ratio);
    var result = new AudioBuffer(newFrames, source.Channels, source.SampleRate);
    for (int frame = 0; frame < newFrames; frame++)
    {
        double srcPos = frame * ratio;
        int srcFrame = (int)srcPos;
        float frac = (float)(srcPos - srcFrame);
        for (int ch = 0; ch < source.Channels; ch++)
        {
            float s0 = source.GetSample(Math.Min(srcFrame, source.Frames - 1), ch);
            float s1 = source.GetSample(Math.Min(srcFrame + 1, source.Frames - 1), ch);
            result.SetSample(frame, ch, s0 + frac * (s1 - s0));
        }
    }
    return result;
}
```

**Usage pattern:** `FileIO.VarispeedResample(buffer, Math.Pow(2.0, semitonesShift / 12.0))` (RESEARCH §"Don't Hand-Roll" — verbatim reuse, no new resample code).

### Reference-identity Type Equality

**Source:** `flow-lang/TypeSystem/SpecialTypes/TuningType.cs:24-26`
**Apply to:** `SfzType.IsCompatibleWith` and `CanConvertTo` (CONTEXT Claude's-Discretion — strict; no numeric coercion; two `loadSfz` calls produce distinct Values)

```csharp
public override bool IsCompatibleWith(FlowType target) => target is TuningType;
public override bool CanConvertTo(FlowType target) => target is TuningType;
```

## No Analog Found

All 21 Phase 33 files have a direct in-repo precedent. The two pieces of *new code logic* that have no analog (acknowledged in 33-RESEARCH):

| Logic | Where | Why No Analog | Reference |
|-------|-------|--------------|-----------|
| SFZ parser body (state machine across `<global>`/`<group>`/`<region>` headers) | `SfzParser.Parse` body | No INI-style state-machine parser exists in repo; Scala parser is single-section | 33-RESEARCH §"Pattern 2 quirks" — header inheritance applied at parse time by flattening |
| 441-frame equal-power sin/cos loop crossfade | `SfzRenderer.Render` body (~30 LOC) | No looped-sample renderer exists in repo; Phase 29 buffers always zero-pad past end | 33-RESEARCH §"Pattern 5" — full pseudocode included; SPEC-5 acceptance criterion gates correctness |

Both are well-scoped (≤ 30 + ≤ 250 LOC) and have authoritative external references (sfzformat.com for parser; equal-power constant-power math is standard). Neither needs a codebase analog — RESEARCH.md ships the full pattern in §"Pattern 2" and §"Pattern 5" respectively.

## Metadata

**Analog search scope:**
- `flow-lang/TypeSystem/SpecialTypes/` (verified TuningType.cs as exact SfzType analog)
- `flow-lang/StandardLibrary/Audio/Tuning/` (verified ScalaParser, ScalaBuiltins as parser + builtin analogs)
- `flow-lang/StandardLibrary/Audio/` (verified SampleCache, SampledInstrumentRenderer, SongRenderer, MidiExport, FileIO)
- `flow-lang/Runtime/` (verified Value, FlowConfig, ExecutionContext)
- `flow-lang/Core/FlowEngine.cs` (verified registration + cache lifecycle pattern)
- `flow-lang/Diagnostics/RenderingDiagnostics.cs` (verified WarnOnce dedup)
- `flow-lang/Interpreter/Interpreter.cs:580-647` (verified ExecuteVariableDeclaration hook site)
- `flow-cli/Config/FlowConfigLoader.cs` (verified zero-edit required — JsonNamingPolicy.SnakeCaseLower handles `sfz_root` auto-mapping)
- `flow-lang.Tests/Unit/Phase32/` + `Integration/Phase29/` (verified test analogs)
- `flow-lang/audio.flow` + `notation.flow` (verified stdlib forward-decl pattern)

**Files scanned:** 21 source + 4 test files read in part or full.
**Pattern extraction date:** 2026-05-15
