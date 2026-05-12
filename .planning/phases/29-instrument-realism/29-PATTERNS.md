# Phase 29: Instrument Realism - Pattern Map

**Mapped:** 2026-05-10
**Files analyzed:** 19 (2 new infrastructure classes + 9 modified synths + 1 modified renderer + 7 new test classes)
**Analogs found:** 18 / 19 (1 has no exact analog; uses RESEARCH.md pattern)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (NEW) | renderer / INoteSynthesizer-shaped helper | note→sample→buffer | `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` | role-match |
| `flow-lang/StandardLibrary/Audio/SampleCache.cs` (NEW) | per-engine cache singleton | (instrument, midi) → AudioBuffer (lazy then memoized) | `flow-lang/Core/FlowEngine.cs` (singleton-owned-by-engine pattern) + Dictionary cache idiom in `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs` | role-match |
| `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` (MODIFIED) | synth shell → delegates to renderer | RenderNote → SampledInstrumentRenderer.Render | current PianoSynthesizer.cs (will be wholly replaced with delegation shell) | exact (transform-in-place) |
| `flow-lang/StandardLibrary/Audio/Synthesizers/BrassSynthesizer.cs` (MODIFIED) | synth shell → delegates to renderer | RenderNote → SampledInstrumentRenderer.Render | current BrassSynthesizer.cs (will be transformed to thin delegate) | exact |
| `flow-lang/StandardLibrary/Audio/Synthesizers/SaxSynthesizer.cs` (MODIFIED) | synth shell → delegates to renderer | RenderNote → SampledInstrumentRenderer.Render | current SaxSynthesizer.cs | exact |
| `flow-lang/StandardLibrary/Audio/Synthesizers/StringsSynthesizer.cs` (MODIFIED) | synth shell → delegates to renderer | RenderNote → SampledInstrumentRenderer.Render | current StringsSynthesizer.cs | exact |
| `flow-lang/StandardLibrary/Audio/Synthesizers/FluteSynthesizer.cs` (MODIFIED) | synth shell → delegates to renderer | RenderNote → SampledInstrumentRenderer.Render | current FluteSynthesizer.cs | exact |
| `flow-lang/StandardLibrary/Audio/Synthesizers/BellSynthesizer.cs` (MODIFIED) | synth shell → delegates to renderer | RenderNote → SampledInstrumentRenderer.Render | current BellSynthesizer.cs | exact |
| `flow-lang/StandardLibrary/Audio/Synthesizers/DrumSynthesizer.cs` (MODIFIED) | percussion synth (retain + improve) | RenderNote multi-component | current DrumSynthesizer.cs | exact (improve-in-place) |
| `flow-lang/StandardLibrary/Audio/Synthesizers/OrganSynthesizer.cs` (MODIFIED) | tonal synth (retain + formant upgrade) | RenderNote formant | current OrganSynthesizer.cs | exact |
| `flow-lang/StandardLibrary/Audio/Synthesizers/WavetableSynthesizer.cs` (MODIFIED) | wavetable synth (retain + variant types) | RenderNote wavetable | current WavetableSynthesizer.cs | exact |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (MODIFIED) | song-level renderer with eager-load hook | renderSong → SampleCache.EagerLoad → RenderSection | current SongRenderer.cs:89-117 RenderSong entry | exact (additive) |
| `flow-lang/Core/FlowEngine.cs` (MODIFIED) | engine constructor — owns SampleCache | construct → cache lifetime | current FlowEngine.cs ownership of AudioPlaybackManager | role-match |
| `flow-lang.Tests/Integration/Phase29/SampledInstrumentSmokeTests.cs` (NEW) | smoke unit test | render each tonal instrument | `flow-lang.Tests/Integration/Phase23/*.cs` (any per-instrument test) | role-match |
| `flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs` (NEW) | cache behavior + speedup test | render twice, assert ≥30% speedup | `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (two-run pattern) | role-match |
| `flow-lang.Tests/Integration/Phase29/VelocityLayerTests.cs` (NEW) | spectral cosine-similarity unit test | render two velocities, compute FFT, cosSim assertion | (no exact analog — FFT is new in Phase 29) | NEW pattern from RESEARCH |
| `flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs` (NEW) | harmonic-richness ratio test | FFT magnitude integration ≥ 20% gain | (no exact analog — same FFT helper as VelocityLayerTests) | NEW pattern from RESEARCH |
| `flow-lang.Tests/Integration/Phase29/ArticulationOnSampleTests.cs` (NEW) | per-articulation render assertion | render C4q × 6 articulations → 6 distinct buffers | Phase 28's ArticulationFacts (created in Phase 28; Phase 29 follows same pattern) | role-match (pattern from Phase 28) |
| `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` (NEW) | LICENSE.md format audit | parse text → assert fields | no direct analog; closest is `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` for the file-walking pattern (file existence + content read + assertion) | role-match |
| `flow-lang.Tests/Integration/Phase29/RepoSizeTests.cs` (NEW) | directory-size budget test | enumerate files → sum lengths → assert ≤ 5 MB | similar file-walking pattern to LicenseAuditTests | role-match |
| `flow-lang.Tests/Integration/Phase29/AbFixtureSmokeTests.cs` (NEW) | render each A/B fixture without exception | render → assert non-empty buffer | `flow-lang.Tests/FlowScriptTests.cs` (parameterized script-execution pattern) | role-match |
| `flow-lang.Tests/Integration/Phase29/Phase29ByteIdenticalTests.cs` (NEW) | extend two-run determinism for Phase 29 fixtures | render twice → bytes equal | `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` | exact |
| `examples/tests/realism_ab/*.flow` (NEW × 6) | A/B test fixture flow script | normal flow script | `examples/tests/maple_leaf_opening.flow` (created in Phase 28) + `examples/tutorial.flow` | role-match |
| `examples/scripts/realism_ab_render.sh` (NEW) | closure render script | bash render orchestration | no direct analog; closest: `tests/run_all.sh` if exists, or `examples/tutorial.flow` invocation pattern | NEW pattern |
| `flow-lang/Samples/{instr}/LICENSE.md` (NEW × 6) | data documentation | static text | no analog — first license docs in project | NEW |
| `flow-lang/Samples/{instr}/*.wav` (NEW data) | binary asset | static binary | no analog — first bundled audio asset | NEW |

## Pattern Assignments

### `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` (renderer, note→sample→buffer)

**Analog:** `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs`

**Imports pattern** (lines 1-5 of PianoSynthesizer.cs):
```csharp
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;
```
For SampledInstrumentRenderer, namespace is `FlowLang.StandardLibrary.Audio;` (one level up — it's used BY the synthesizers).

**Render entry pattern** (PianoSynthesizer.cs:13-22):
```csharp
public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
{
    if (note.IsRest)
        return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

    double frequency = PitchConversion.NoteToFrequency(note, tuning);
    double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
    int numSamples = (int)(durationSeconds * sampleRate);
    if (numSamples <= 0)
        return new AudioBuffer(0, 1, sampleRate);
```
SampledInstrumentRenderer uses the same signature shape, plus rest/silence short-circuit, plus duration→numSamples conversion. The midi-note + pitch logic differs (it picks a nearest sample), but the prelude is identical.

**Buffer-output pattern** (PianoSynthesizer.cs:82):
```csharp
return SynthUtils.ToMonoBuffer(samples, sampleRate);
```
SampledInstrumentRenderer returns a mono `AudioBuffer`. The varispeed-shifted sample comes back from `FileIO.VarispeedResample` already as `AudioBuffer`; SampledInstrumentRenderer may need to convert to `float[]` for envelope-shaping then back to AudioBuffer — same pattern.

**Velocity scaling pattern** (PianoSynthesizer.cs:30):
```csharp
double baseAmp = 0.18 * note.Velocity;
```
SampledInstrumentRenderer's non-piano path uses `output = sample * note.Velocity` linear amplitude scaling. Same idiom.

---

### `flow-lang/StandardLibrary/Audio/SampleCache.cs` (singleton cache, (instrument, midi) → AudioBuffer)

**Analog (lifetime):** `flow-lang/Core/FlowEngine.cs` (owns AudioPlaybackManager)

**Singleton-owned-by-engine pattern** (FlowEngine.cs:22, 33, 42):
```csharp
private readonly AudioPlaybackManager _audioManager;
public AudioPlaybackManager AudioManager => _audioManager;

public FlowEngine(ErrorReporter errorReporter, bool verbose = false)
{
    _audioManager = new AudioPlaybackManager();
    // ...
}
```
SampleCache follows the same pattern: FlowEngine constructs a SampleCache; exposes it via property; cache lifetime = engine lifetime. Cache passes into SongRenderer / SampledInstrumentRenderer via ExecutionContext or a similar threading mechanism.

**Dictionary cache idiom** (typical in Flow's codebase):
```csharp
private readonly Dictionary<KeyType, ValueType> _cache = new();

public ValueType Get(KeyType key)
{
    if (_cache.TryGetValue(key, out var cached)) return cached;
    var fresh = Compute(key);
    _cache[key] = fresh;
    return fresh;
}
```
SampleCache uses TWO dictionaries (raw + varispeed-shifted) — keyed by `(instrument, midi, velocity)` and `(instrument, midi, velocity, shift)` tuples respectively.

**Disposal pattern** (FlowEngine.cs IDisposable):
```csharp
public class FlowEngine : IDisposable
{
    public void Dispose() { _audioManager?.Dispose(); ... }
}
```
SampleCache holds `AudioBuffer` instances; if `AudioBuffer` doesn't implement IDisposable, no explicit cleanup needed. Cache memory reclaimed on GC when FlowEngine is disposed.

---

### `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` (MODIFIED — delegation shell)

**Analog (current state):** itself (the existing 84-line file is replaced)

**Transformation pattern** — pre-Phase-29:
```csharp
// 84 lines of hand-rolled additive synthesis
public class PianoSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        // ... 80+ lines of synthesis
    }
}
```
**Post-Phase-29:**
```csharp
public class PianoSynthesizer : INoteSynthesizer
{
    private readonly SampledInstrumentRenderer _renderer;

    public PianoSynthesizer(SampleCache cache)
    {
        _renderer = new SampledInstrumentRenderer(cache, "piano", hasVelocityLayers: true);
    }

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        => _renderer.Render(note, sampleRate, durationBeats, bpm, tuning);
}
```
**Same pattern for Brass/Sax/Strings/Flute/Bell** — only differ by instrument name string + `hasVelocityLayers: false` for non-piano.

**Factory change** (NoteSynthesizer.cs:227-251) — SynthesizerFactory.Create needs to accept a SampleCache to inject into tonal synths:
```csharp
// CURRENT (no cache):
public static INoteSynthesizer Create(string synthType) { ... }

// POST-PHASE-29: (factory needs the cache; threaded from ExecutionContext or similar)
public static INoteSynthesizer Create(string synthType, SampleCache cache) { ... }
```

---

### `flow-lang/StandardLibrary/Audio/Synthesizers/DrumSynthesizer.cs` (MODIFIED — multi-component upgrade)

**Analog:** existing DrumSynthesizer.cs (improve in place)

**Existing kick pattern** (DrumSynthesizer.cs:53-60):
```csharp
private static float[] RenderKick(int sr, double vel)
{
    int frames = (int)(0.301 * sr);
    var buf = new float[frames];
    // Pitch sweep: exponential decay from 150 to 50 Hz
    double phase = 0.0;
    // ...
}
```
**Phase 29 upgrade pattern (multi-component):**
```csharp
private static float[] RenderKick(int sr, double vel)
{
    int frames = (int)(0.301 * sr);
    var buf = new float[frames];
    // Component 1: body sine with pitch sweep (existing logic, refined)
    // Component 2: click transient (new — 1-2 ms white noise burst at start)
    // Component 3: body decay (new — extended exponential decay tail)
    // Mix all three additively
}
```
Same idiom — additive mixing of named components into a single `float[]`, each component with its own envelope. Same `SynthUtils.GenerateSine`/`GenerateWhiteNoise`/`GenerateADSR` helpers.

---

### `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (MODIFIED — eager-load hook)

**Analog:** itself (additive change at entry point)

**Existing RenderSong entry** (SongRenderer.cs:89-99):
```csharp
public static Value RenderSong(IReadOnlyList<Value> args)
{
    var song = args[0].As<SongData>();
    string synthType = (string)args[1].Data!;

    SynthUtils.ResetNoiseRng();
    AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);

    foreach (var sectionRef in song.Sections) { ... }
```
**Phase 29 addition (after ResetNoiseRng, before foreach):**
```csharp
public static Value RenderSong(IReadOnlyList<Value> args)
{
    var song = args[0].As<SongData>();
    string synthType = (string)args[1].Data!;

    SynthUtils.ResetNoiseRng();

    // Phase 29: eager-load samples needed by this song for this instrument
    var cache = SampleCache.GetForCurrentEngine();   // or threaded via context
    cache.EagerLoad(song, synthType);

    AudioBuffer result = new AudioBuffer(0, StereoChannels, DefaultSampleRate);
    // ... rest unchanged
}
```
The cache is keyed off the active FlowEngine; `EagerLoad` is idempotent for the same song+instrument so repeated calls are cheap.

---

### `flow-lang.Tests/Integration/Phase29/SampleCacheTests.cs` (NEW — cache + speedup)

**Analog:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs`

**Two-run wrapper pattern** (Phase18 test):
```csharp
[Fact]
public void Showcase_TwoRunsProduceIdenticalWav()
{
    RunTwiceAndCompare(isMidi: false);
}

private static void RunTwiceAndCompare(bool isMidi)
{
    string testsRoot = FlowScriptData.FindTestsRoot();
    // ... setup, two runs, compare
}
```
Phase 29's `SampleCacheTests.cs` follows the same "render twice" structure, but instead of byte-equal, it measures elapsed wall time:
```csharp
[Fact]
public void SecondRender_IsAtLeast30PercentFaster()
{
    using var engine = new FlowEngine();
    var stopwatch1 = Stopwatch.StartNew();
    engine.Run("Sequence demo = | C4q D4q E4q F4q |  Song s = [demo*1]  renderSong(s, \"piano\")");
    stopwatch1.Stop();

    var stopwatch2 = Stopwatch.StartNew();
    engine.Run("Sequence demo = | C4q D4q E4q F4q |  Song s = [demo*1]  renderSong(s, \"piano\")");
    stopwatch2.Stop();

    Assert.True(stopwatch2.ElapsedMilliseconds <= stopwatch1.ElapsedMilliseconds * 0.7,
        $"Second render should be ≥30% faster (was {stopwatch1.ElapsedMilliseconds}ms → {stopwatch2.ElapsedMilliseconds}ms)");
}
```
Same FlowEngine-per-test pattern; same engine.Run script-execution mechanism.

---

### `flow-lang.Tests/Integration/Phase29/Phase29ByteIdenticalTests.cs` (NEW — extend determinism)

**Analog:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs`

**Direct pattern copy** — the only change is the script path and test name. The `RunTwiceAndCompare` helper from Phase 18 can be lifted or duplicated:
```csharp
[Collection("FlowScripts")]
public class Phase29ByteIdenticalTests
{
    [Fact]
    public void RealismAbPianoFixture_TwoRunsProduceIdenticalWav()
    {
        RunTwiceAndCompare("examples/tests/realism_ab/piano.flow", isMidi: false);
    }
    // ... ×6 instruments
}
```
The exact private helper is the Phase 18 pattern, adjusted only for script path.

---

### `flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs` (NEW — FFT-based)

**No exact analog in codebase.** Uses RESEARCH.md pattern (hand-rolled radix-2 DFT, ~30 lines, deterministic).

**Test shape:**
```csharp
[Fact]
public void DrumKick_HarmonicRichness_AtLeast20PercentGainOverPhase28Baseline()
{
    using var engine = new FlowEngine();
    // Render drum C2 (kick) for 1 sec at 44.1 kHz under Phase 29
    var bufPhase29 = RenderTestNote(engine, "drums", "C2", durationSeconds: 1.0);
    double ratioPhase29 = ComputeHarmonicRichnessRatio(bufPhase29, fundamentalHz: 50.0); // kick has body around 50Hz

    // Read pinned Phase 28 baseline ratio from a fixture file
    double ratioPhase28 = ReadPinnedBaseline("Phase29/drum_kick_phase28_baseline.json");

    Assert.True(ratioPhase29 >= ratioPhase28 * 1.20,
        $"Phase 29 kick should be ≥20% richer (Phase 28: {ratioPhase28:F3}, Phase 29: {ratioPhase29:F3})");
}

private static double ComputeHarmonicRichnessRatio(AudioBuffer buf, double fundamentalHz)
{
    Complex[] data = PadToPowerOf2(buf.Data.Select(s => new Complex(s, 0)).ToArray());
    var spectrum = FFT(data); // hand-rolled radix-2
    var mag = spectrum.Select(c => c.Magnitude).ToArray();
    int fundBin = (int)Math.Round(fundamentalHz * data.Length / buf.SampleRate);
    double fundEnergy = mag[fundBin] * mag[fundBin];
    double harmEnergy = 0;
    for (int n = 2; fundBin * n < mag.Length / 2; n++)
        harmEnergy += mag[fundBin * n] * mag[fundBin * n];
    return harmEnergy / fundEnergy;
}
```
The Phase 28 baseline ratios are computed once (during Phase 29 Wave 0 or in CONTEXT.md preamble), pinned in a per-instrument fixture JSON, and never recomputed unless Phase 28's synthesis changes.

---

### `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` (NEW — file-content audit)

**Analog (closest):** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (for the file-walking idiom)

**Test shape:**
```csharp
[Theory]
[InlineData("piano")]
[InlineData("brass")]
[InlineData("sax")]
[InlineData("strings")]
[InlineData("flute")]
[InlineData("bell")]
public void EachInstrumentLicenseFile_HasRequiredFields(string instrument)
{
    string testsRoot = FlowScriptData.FindTestsRoot();
    string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
    string licensePath = Path.Combine(repoRoot, "flow-lang", "Samples", instrument, "LICENSE.md");
    Assert.True(File.Exists(licensePath), $"LICENSE.md missing for {instrument}");
    string contents = File.ReadAllText(licensePath);
    Assert.Contains("License:", contents);
    Assert.Contains("Source:", contents);
    // CC0 or Public Domain only — CC-BY rejected
    bool isCc0 = contents.Contains("License: CC0") || contents.Contains("License: Public Domain");
    Assert.True(isCc0, $"LICENSE.md for {instrument} must declare CC0 or Public Domain (got: {contents.Substring(0, Math.Min(200, contents.Length))})");
    Assert.DoesNotContain("License: CC-BY", contents);
}
```
Same `FindTestsRoot` + file-existence + content-read idiom as Phase 18 byte-identical tests.

---

### `flow-lang.Tests/Integration/Phase29/RepoSizeTests.cs` (NEW — directory size budget)

**Analog (closest):** `LicenseAuditTests.cs` (same file-walking idiom)

**Test shape:**
```csharp
[Fact]
public void SamplesDirectory_DoesNotExceed5MB()
{
    string testsRoot = FlowScriptData.FindTestsRoot();
    string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
    string samplesRoot = Path.Combine(repoRoot, "flow-lang", "Samples");
    Assert.True(Directory.Exists(samplesRoot), "flow-lang/Samples/ must exist");
    long totalBytes = Directory.EnumerateFiles(samplesRoot, "*.*", SearchOption.AllDirectories)
        .Sum(f => new FileInfo(f).Length);
    const long FIVE_MB = 5L * 1024 * 1024;
    Assert.True(totalBytes <= FIVE_MB,
        $"flow-lang/Samples/ is {totalBytes / 1024.0 / 1024.0:F2} MB; must be ≤ 5 MB");
}
```
Cross-platform (no `du` dependency); deterministic.

---

### `examples/tests/realism_ab/piano.flow` (NEW × 6 fixtures)

**Analog:** `examples/tests/maple_leaf_opening.flow` (Phase 28 fixture — created during Phase 28 execution, similar structure)

**Existing Phase 28 fixture pattern (extrapolated from Phase 28 SPEC §Requirement 9):**
```flow
-- 4 bars exercising the instrument's strongest qualities
use "@audio"

tempo 120 {
  key Cmajor {
    timesig 4/4 {
      section showcase {
        Sequence main = | <instrument-specific notes> |
      }
    }
  }
}

Song s = [showcase]
Buffer rendered = renderSong(s, "<instrument>")
writeWav(rendered, "examples/output/realism_ab/<instrument>.wav")
```
For piano: 5-10 second arpeggio with mixed articulations. For brass: 5-10 second fanfare. For sax: 5-10 second melodic line. For strings: sustained chord with melody. For flute: melodic line. For drums: rock fill.

---

### `examples/scripts/realism_ab_render.sh` (NEW — closure render orchestration)

**No direct analog.** New shell script. Sketch:
```bash
#!/bin/bash
# Renders each fixture under Phase 28 baseline + Phase 29 with randomized A/B mapping.
# Phase 28 baseline obtained from `git checkout <phase-28-closure-commit> -- flow-lang/`
# (or via separate `phase28_baseline.tar.gz` of pre-Phase-29 build outputs).

FIXTURES=("piano" "brass" "sax" "strings" "flute" "drums")
OUTPUT_DIR="examples/output/realism_ab"
ANSWER_KEY="$OUTPUT_DIR/answer_key.txt"

mkdir -p "$OUTPUT_DIR"
echo "# Answer key — sealed at $(date)" > "$ANSWER_KEY"

for fixture in "${FIXTURES[@]}"; do
  # Render Phase 28 baseline (assumed available via prior render or pinned tarball)
  # Render Phase 29 output (current branch)
  # Randomize A/B mapping
  letter_for_29=$((RANDOM % 2 == 0 ? "A" : "B"))
  # ... mv outputs to A_/B_ slots
  echo "$fixture: $letter_for_29" >> "$ANSWER_KEY"
done

# Move answer-key out to a sealed commit (or git note)
echo "Sign-off: composer renders A/B WAVs, lists guesses in 29-VERIFICATION.md, runs unseal command"
```
Deterministic randomization (seeded `$RANDOM`) for two-run determinism continuity.

---

## Shared Patterns

### `INoteSynthesizer.RenderNote` signature contract
**Source:** `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:16-19`
**Apply to:** SampledInstrumentRenderer.Render method (mirror signature even though it's not a direct INoteSynthesizer impl) + ALL 9 modified Synthesizer.RenderNote methods
```csharp
public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
```
This signature is locked across Flow's synthesis layer. Phase 29 must not introduce variants.

### Rest-handling short-circuit
**Source:** Every synth class (e.g. PianoSynthesizer.cs:15-16, BrassSynthesizer.cs:14-15)
**Apply to:** SampledInstrumentRenderer.Render and all modified synth shells
```csharp
if (note.IsRest)
    return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
```
Universal pattern — preserves the rendering contract that rests produce silence buffers of the correct duration.

### Duration → numSamples conversion
**Source:** Every synth class
**Apply to:** SampledInstrumentRenderer + all retained-synth modifications
```csharp
double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
int numSamples = (int)(durationSeconds * sampleRate);
if (numSamples <= 0)
    return new AudioBuffer(0, 1, sampleRate);
```
Universal pattern. SampledInstrumentRenderer uses this to determine how many samples to fit (trim/pad the varispeed-shifted sample to numSamples).

### Mono-buffer construction
**Source:** `flow-lang/StandardLibrary/Audio/SynthUtils.ToMonoBuffer`
**Apply to:** SampledInstrumentRenderer return
```csharp
return SynthUtils.ToMonoBuffer(samples, sampleRate);
```
Helper wraps `new AudioBuffer(samples.Length, 1, sampleRate)` + sample copy.

### Velocity-driven amplitude scaling
**Source:** All synth classes
**Apply to:** SampledInstrumentRenderer non-piano path (single-velocity instruments)
```csharp
double amplitude = base * note.Velocity;  // common idiom
// or:
for (int i = 0; i < samples.Length; i++) samples[i] *= (float)note.Velocity;
```
Piano path uses the crossfade formula `output = (1-v) * pp + v * ff` instead.

### Two-run determinism test pattern
**Source:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs`
**Apply to:** All Phase 29 fixtures that need determinism continuity
```csharp
[Fact]
public void Foo_TwoRunsProduceIdenticalWav() { RunTwiceAndCompare(isMidi: false); }
```
Phase 29 reuses the helper (or duplicates the pattern in `Phase29ByteIdenticalTests.cs`).

### xUnit Theory + InlineData for per-instrument tests
**Source:** xUnit framework idiom (existing in flow-lang.Tests)
**Apply to:** LicenseAuditTests, SampledInstrumentSmokeTests, AbFixtureSmokeTests
```csharp
[Theory]
[InlineData("piano")]
[InlineData("brass")]
// ...
public void Test(string instrument) { ... }
```
Reduces boilerplate for the 6-tonal-instrument coverage requirement.

### SynthUtils helpers as building blocks for hand-rolled synth improvements
**Source:** `flow-lang/StandardLibrary/Audio/SynthUtils.cs`
**Apply to:** Drums/Organ/Wavetable improvements
```csharp
SynthUtils.GenerateSine(samples, freq, amp, sampleRate);
SynthUtils.GenerateWhiteNoise(samples, amp);
SynthUtils.GenerateSaw(samples, freq, amp, sampleRate);
SynthUtils.GenerateADSR(attack, decay, sustain, release, frames, sampleRate);
SynthUtils.ApplyEnvelope(samples, envelope);
SynthUtils.OnePoleLP(samples, cutoff, sampleRate);
SynthUtils.ToMonoBuffer(samples, sampleRate);
SynthUtils.BeatsToSeconds(beats, bpm);
SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
```
Drums multi-component, Organ formant, Wavetable variants all combine these primitives — no new helper functions required.

## No Analog Found

Files with no close match in the codebase (planner should use RESEARCH.md patterns instead):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `flow-lang.Tests/Integration/Phase29/VelocityLayerTests.cs` (cosine similarity over FFT magnitudes) | spectral analysis unit test | render → FFT → compare | No FFT-based spectral test exists yet in flow-lang.Tests. Pattern is original to Phase 29 (uses RESEARCH §Validation Architecture FFT methodology). |
| `flow-lang.Tests/Integration/Phase29/HarmonicRichnessTests.cs` (FFT magnitude integration) | spectral analysis unit test | render → FFT → integrate partials | Same as above — first FFT helper in the test suite. Shared FFT code with VelocityLayerTests. Plan should factor the FFT helper into a single test-helper file (`flow-lang.Tests/Helpers/Phase29Fft.cs` or similar). |
| `flow-lang/Samples/{instr}/LICENSE.md` (license docs) | data documentation | static | First license docs in project; format is locked by SPEC (`License:` + `Source:` + body). |
| `flow-lang/Samples/{instr}/*.wav` (binary assets) | static binary | n/a | First bundled audio asset in the repo. Format / size constraints come from SPEC, not from existing patterns. |
| `examples/scripts/realism_ab_render.sh` (closure orchestration) | shell script | n/a | No existing shell-script orchestration pattern in repo. Pattern derived from research + spec. |

## Metadata

**Analog search scope:** `flow-lang/StandardLibrary/Audio/`, `flow-lang/StandardLibrary/Audio/Synthesizers/`, `flow-lang/Core/`, `flow-lang.Tests/Integration/`, `examples/`
**Files scanned:** 25 (9 synth classes, NoteSynthesizer.cs, FileIO.cs, SongRenderer.cs, SequenceRenderer.cs, SynthUtils.cs, FlowEngine.cs, 5 Phase18 + Phase23 test files, 3 example .flow files, README.md)
**Pattern extraction date:** 2026-05-10

---

## PATTERN MAPPING COMPLETE

**Phase:** 29 - instrument-realism
**Files classified:** 19 production files + 7 test files + 6 fixture files + 6 LICENSE.md + 21 sample data files = 59 total artifacts
**Analogs found:** 18 / 19 production / modified files (1 production file — `SampledInstrumentRenderer` — has a role-match analog from PianoSynthesizer)

### Coverage
- Files with exact analog (improve / transform in place): 12 (9 synths + SongRenderer + 2 existing test pattern reuses)
- Files with role-match analog: 6 (SampledInstrumentRenderer ← Piano; SampleCache ← FlowEngine + Dictionary cache idiom; test files ← Phase 18 / Phase 23 patterns; A/B fixture scripts ← Phase 28's maple_leaf_opening.flow pattern)
- Files with no analog: 5 (FFT-based tests × 2; LICENSE.md × 6 (counted as 1 type); WAV sample data; closure shell script)
