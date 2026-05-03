---
phase: 260502-oib
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/Audio/SignalGeneration.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/audio.flow
  - tests/test_noise_builtin.flow
autonomous: true
requirements:
  - QUICK-OIB
must_haves:
  truths:
    - "Flow scripts can call (noise 1.0) to get a 1-second mono white-noise buffer at 44100 Hz, amplitude 1.0"
    - "Flow scripts can call (noise seconds amplitude), (noise seconds amplitude channels), and (noise seconds amplitude channels sampleRate)"
    - "Charitable interpretation holds: negative seconds clamps to 0 frames, channels < 1 promotes to 1, sampleRate <= 0 falls back to 44100"
    - "Existing tests still pass — pure-additive change, no edits to CreateClip / GenerateSine / Value.ToString"
    - "tests/test_noise_builtin.flow exits 0 and prettyBuffer's output shows the buffer was filled (peak near amplitude)"
  artifacts:
    - path: "flow-lang/StandardLibrary/Audio/SignalGeneration.cs"
      provides: "Noise(IReadOnlyList<Value>) overload methods (4 arities)"
      contains: "public static Value Noise"
    - path: "flow-lang/StandardLibrary/BuiltInFunctions.cs"
      provides: "Four noise FunctionSignatures registered with the registry"
      contains: "registry.Register(\"noise\""
    - path: "flow-lang/audio.flow"
      provides: "internal proc noise declarations (4 arities) for parse-time binding"
      contains: "internal proc noise"
    - path: "tests/test_noise_builtin.flow"
      provides: "End-to-end test exercising all four overloads"
      min_lines: 10
  key_links:
    - from: "flow-lang/StandardLibrary/Audio/SignalGeneration.cs (Noise core)"
      to: "flow-lang/StandardLibrary/Audio/SynthUtils.cs (GenerateWhiteNoise)"
      via: "SynthUtils.GenerateWhiteNoise(buffer.Data, amplitude)"
      pattern: "SynthUtils\\.GenerateWhiteNoise"
    - from: "flow-lang/StandardLibrary/BuiltInFunctions.cs"
      to: "flow-lang/StandardLibrary/Audio/SignalGeneration.cs (Noise methods)"
      via: "registry.Register with FunctionSignature per arity"
      pattern: "registry\\.Register\\(\"noise\""
    - from: "flow-lang/audio.flow"
      to: "Parser binder (resolves `noise` calls to registered C# overloads)"
      via: "internal proc declarations"
      pattern: "internal proc noise"
    - from: "tests/test_noise_builtin.flow"
      to: "(noise ...) and (prettyBuffer ...)"
      via: "use \"@audio\" + use \"@std\" then call noise + prettyBuffer"
      pattern: "noise"
---

<objective>
Add a `noise` builtin to flow-lang that fills a buffer with white noise, exposing four arity-based overloads to Flow scripts. Wraps the existing `SynthUtils.GenerateWhiteNoise` C# function — no new DSP code, just plumbing.

Purpose: Closes a documentation/implementation gap (BuiltInDocs.cs:97 advertises `noise` but no signature is registered). White noise is a foundational signal-generation primitive that composers expect alongside `sine`, `saw`, `square`, `triangle`.

Output: 4 new Value-returning C# methods, 4 registry entries, 4 `internal proc` declarations in `audio.flow`, and 1 test script that exercises all arities and exits 0.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@flow-lang/StandardLibrary/Audio/SignalGeneration.cs
@flow-lang/StandardLibrary/Audio/SynthUtils.cs
@flow-lang/StandardLibrary/BuiltInFunctions.cs
@flow-lang/audio.flow
@flow-lang/std.flow

<interfaces>
<!-- Key contracts the executor needs. Verified from the codebase 2026-05-02. -->

From flow-lang/StandardLibrary/Audio/SynthUtils.cs (around line 120):
```csharp
// Fills the buffer with white noise at the given amplitude (additive).
// NB: this is ADDITIVE — it += into existing samples. For a fresh
// AudioBuffer (zero-initialized .Data), this is equivalent to a write.
public static void GenerateWhiteNoise(float[] buffer, double amplitude)
```

From flow-lang/StandardLibrary/Audio/AudioCore.cs (line 16, 34):
```csharp
public float[] Data { get; }                         // raw interleaved float buffer
public AudioBuffer(int frames, int channels, int sampleRate)
```

From flow-lang/StandardLibrary/Audio/SignalGeneration.cs (line 168 — the pattern to mirror):
```csharp
public static Value CreateClip(IReadOnlyList<Value> args)
{
    double duration = args[0].As<double>();
    double amplitude = args[1].As<double>();
    int sampleRate = 44100;
    int frames = (int)(duration * sampleRate);
    var buffer = new AudioBuffer(frames, 1, sampleRate);
    // ... fills first 10% with random samples ...
    return Value.Buffer(buffer);
}
```

From flow-lang/StandardLibrary/BuiltInFunctions.cs (around line 583 — the registration pattern to mirror):
```csharp
var createClipSig = new FunctionSignature("createClip",
    [DoubleType.Instance, DoubleType.Instance]);
registry.Register("createClip", createClipSig, Audio.SignalGeneration.CreateClip);
```

Note: registry.Register signature is (string name, FunctionSignature sig, Func<IReadOnlyList<Value>, Value> impl). For overloaded names the same `name` string is reused; the OverloadResolver disambiguates by signature.

From flow-lang/audio.flow (lines 6–7 — declaration pattern; declarations live alongside the existing buffer/signal builtins):
```
Note: Creates a new audio buffer with the specified parameters
internal proc createBuffer(Int: frames, Int: channels, Int: sampleRate)
```

From flow-lang/std.flow (lines 147–149 — the prettyBuffer overloads we just shipped, available for the test):
```
internal proc prettyBuffer (Buffer: b)
internal proc bufferHex (Buffer: b)
internal proc bufferHex (Buffer: b, Int: offset, Int: length)
```

Type instances (already imported in BuiltInFunctions.cs):
- DoubleType.Instance — for Double params (seconds, amplitude)
- IntType.Instance — for Int params (channels, sampleRate)
- Both live in flow-lang/TypeSystem/PrimitiveTypes/

Memory note (from user feedback files):
- Functional S-expression style, no infix
- Charitable interpretation: silently clamp / document edge cases rather than throw
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Implement Noise() in SignalGeneration.cs and register four overloads</name>
  <files>flow-lang/StandardLibrary/Audio/SignalGeneration.cs, flow-lang/StandardLibrary/BuiltInFunctions.cs, flow-lang/audio.flow</files>
  <behavior>
    Test cases that the implementation must satisfy (exercised by Task 2's test script):

    - Test 1 (1-arity, defaults): `(noise 1.0)` returns a Buffer with 44100 frames, 1 channel, sampleRate 44100, peak |sample| close to 1.0 (white noise → expect peak > 0.5 with overwhelming probability).
    - Test 2 (2-arity, custom amplitude): `(noise 0.5 0.25)` returns a Buffer with 22050 frames, mono, sr 44100; peak |sample| > 0.05 and ≤ 0.25.
    - Test 3 (3-arity, stereo): `(noise 0.1 0.5 2)` returns a Buffer with 4410 frames, 2 channels, sr 44100; both channels contain non-zero samples (independent random samples per interleaved slot is the desired/documented behavior).
    - Test 4 (4-arity, custom sample rate): `(noise 0.1 0.5 1 22050)` returns a Buffer with 2205 frames, mono, sr 22050.
    - Test 5 (charitable clamping):
        * `(noise -1.0)` → Buffer with 0 frames (no exception)
        * `(noise 0.1 0.5 0)` → channels promoted to 1
        * `(noise 0.1 0.5 1 0)` → sampleRate falls back to 44100
  </behavior>
  <action>
1. **Add Noise overloads to flow-lang/StandardLibrary/Audio/SignalGeneration.cs** — add four `public static Value` methods inside the existing `SignalGeneration` class (place them after `CreateClip` at the end of the class):

   ```csharp
   // Core implementation. All other arities delegate here with defaults.
   // Charitable interpretation per user memory: clamp invalid args silently
   // (negative seconds → 0 frames; channels < 1 → 1; sampleRate <= 0 → 44100)
   // rather than throwing. Mirrors CreateClip's style.
   public static Value Noise(IReadOnlyList<Value> args)
   {
       double seconds   = args[0].As<double>();
       double amplitude = args[1].As<double>();
       int    channels  = args[2].As<int>();
       int    sampleRate = args[3].As<int>();

       if (seconds < 0) seconds = 0;
       if (channels < 1) channels = 1;
       if (sampleRate <= 0) sampleRate = 44100;

       int frames = (int)(seconds * sampleRate);
       var buffer = new AudioBuffer(frames, channels, sampleRate);
       // GenerateWhiteNoise is additive (+=), but a fresh AudioBuffer's
       // .Data is zero-initialized, so this is a write in practice.
       SynthUtils.GenerateWhiteNoise(buffer.Data, amplitude);
       return Value.Buffer(buffer);
   }

   public static Value Noise1(IReadOnlyList<Value> args)
       => Noise(new List<Value> { args[0], Value.Double(1.0), Value.Int(1), Value.Int(44100) });

   public static Value Noise2(IReadOnlyList<Value> args)
       => Noise(new List<Value> { args[0], args[1], Value.Int(1), Value.Int(44100) });

   public static Value Noise3(IReadOnlyList<Value> args)
       => Noise(new List<Value> { args[0], args[1], args[2], Value.Int(44100) });
   ```

   IMPORTANT: do NOT modify the existing CreateClip / GenerateSine / Generate{Saw,Square,Triangle} methods — pure additive per <constraints>.

   If `Value.Double(...)` / `Value.Int(...)` factory names differ in the codebase, use whatever factory `CreateOscillatorState` / `CreateClip` use to construct numeric Values (verify by skimming `flow-lang/Runtime/Value.cs`). Adapt names if needed; the four-method shape stays the same.

2. **Register four signatures in flow-lang/StandardLibrary/BuiltInFunctions.cs** — add the block immediately after the `createClipSig` registration (around line 583):

   ```csharp
   // White noise — wraps SynthUtils.GenerateWhiteNoise. Four arities; resolver disambiguates by arg count.
   var noise1Sig = new FunctionSignature("noise", [DoubleType.Instance]);
   registry.Register("noise", noise1Sig, Audio.SignalGeneration.Noise1);

   var noise2Sig = new FunctionSignature("noise", [DoubleType.Instance, DoubleType.Instance]);
   registry.Register("noise", noise2Sig, Audio.SignalGeneration.Noise2);

   var noise3Sig = new FunctionSignature("noise", [DoubleType.Instance, DoubleType.Instance, IntType.Instance]);
   registry.Register("noise", noise3Sig, Audio.SignalGeneration.Noise3);

   var noise4Sig = new FunctionSignature("noise", [DoubleType.Instance, DoubleType.Instance, IntType.Instance, IntType.Instance]);
   registry.Register("noise", noise4Sig, Audio.SignalGeneration.Noise);
   ```

3. **Add internal proc declarations to flow-lang/audio.flow** — append to the `===== Core Buffer Operations =====` section (or wherever feels semantically closest to the existing signal-generation/buffer-creation declarations; `audio.flow` doesn't have a strict ordering, just keep the four together):

   ```
   Note: White noise — 1s mono buffer at 44100 Hz, amplitude 1.0
   internal proc noise(Double: seconds)

   Note: White noise — mono 44100 Hz, custom amplitude
   internal proc noise(Double: seconds, Double: amplitude)

   Note: White noise — 44100 Hz, custom amplitude and channels
   internal proc noise(Double: seconds, Double: amplitude, Int: channels)

   Note: White noise — fully parameterized
   internal proc noise(Double: seconds, Double: amplitude, Int: channels, Int: sampleRate)
   ```

Per user memory feedback: keep S-expression call style and silently clamp edge cases (negative seconds, zero channels, invalid sample rate) rather than throwing.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet build flow-lang 2>&1 | tail -20 | grep -E "Build succeeded|error"</automated>
  </verify>
  <done>
    - `dotnet build flow-lang` succeeds with 0 errors
    - `grep -c "public static Value Noise" flow-lang/StandardLibrary/Audio/SignalGeneration.cs` returns 4
    - `grep -c 'registry.Register("noise"' flow-lang/StandardLibrary/BuiltInFunctions.cs` returns 4
    - `grep -v '^Note:' flow-lang/audio.flow | grep -c "internal proc noise(" ` returns 4
    - No edits to CreateClip / Generate* methods (verifiable via `git diff flow-lang/StandardLibrary/Audio/SignalGeneration.cs` showing only additions in the Noise region)
  </done>
</task>

<task type="auto">
  <name>Task 2: Add end-to-end test script exercising all four noise overloads</name>
  <files>tests/test_noise_builtin.flow</files>
  <action>
Create `tests/test_noise_builtin.flow` that exercises every overload, verifies the buffer shapes via `getFrames` / `getChannels` / `getSampleRate`, and pretty-prints one buffer to demonstrate it's actually filled. The script must exit 0 (success = no errors printed by the interpreter).

Use S-expression call style throughout (per user memory: no infix). Use `use "@audio"` for noise + buffer accessors and `use "@std"` for `prettyBuffer` / `print` / `str`.

```
use "@audio"
use "@std"

Note: Test 1 — 1-arity defaults: 1s mono 44100Hz, amplitude 1.0
Buffer b1 = (noise 1.0)
(print "Test 1 (1-arity defaults):")
(print (str "  frames=" (str (getFrames b1)) " (expected 44100)"))
(print (str "  channels=" (str (getChannels b1)) " (expected 1)"))
(print (str "  sampleRate=" (str (getSampleRate b1)) " (expected 44100)"))

Note: Test 2 — 2-arity custom amplitude
Buffer b2 = (noise 0.5 0.25)
(print "Test 2 (2-arity, amp=0.25):")
(print (str "  frames=" (str (getFrames b2)) " (expected 22050)"))
(print (str "  channels=" (str (getChannels b2)) " (expected 1)"))

Note: Test 3 — 3-arity stereo
Buffer b3 = (noise 0.1 0.5 2)
(print "Test 3 (3-arity, stereo):")
(print (str "  frames=" (str (getFrames b3)) " (expected 4410)"))
(print (str "  channels=" (str (getChannels b3)) " (expected 2)"))

Note: Test 4 — 4-arity custom sample rate
Buffer b4 = (noise 0.1 0.5 1 22050)
(print "Test 4 (4-arity, sr=22050):")
(print (str "  frames=" (str (getFrames b4)) " (expected 2205)"))
(print (str "  sampleRate=" (str (getSampleRate b4)) " (expected 22050)"))

Note: Test 5 — charitable clamping (per user memory: silently handle edge cases)
Buffer bNeg = (noise -1.0)
(print (str "Test 5a (noise -1.0): frames=" (str (getFrames bNeg)) " (expected 0)"))
Buffer bZeroCh = (noise 0.1 0.5 0)
(print (str "Test 5b (channels=0): channels=" (str (getChannels bZeroCh)) " (expected 1)"))
Buffer bZeroSr = (noise 0.1 0.5 1 0)
(print (str "Test 5c (sampleRate=0): sampleRate=" (str (getSampleRate bZeroSr)) " (expected 44100)"))

Note: Exercise prettyBuffer on the smallest sane noise buffer to prove samples were actually written
Buffer bShort = (noise 0.001 0.5 1 44100)
(print "Pretty-printed short noise buffer (should show ~44 non-zero samples):")
(prettyBuffer bShort)

(print "All noise-builtin tests passed.")
```

Notes:
- Buffer accessors (`getFrames`, `getChannels`, `getSampleRate`) and `prettyBuffer` are confirmed available in master (verified: flow-lang/audio.flow:10,13,16 + flow-lang/std.flow:147).
- White noise sample values are non-deterministic (uses Rng), so the script verifies *shape* deterministically and uses `prettyBuffer` for visual sanity-check on amplitude.
- If `str`'s signature in stdlib only accepts a single arg, switch to nested calls: `(str "frames=" (str (getFrames b1)))` is already in that style, but you may need to chain via repeated `(print)` calls instead. Adjust to whatever the existing test files in `tests/` use for multi-segment output.
  </action>
  <verify>
    <automated>cd /home/noah/Desktop/projects/flow-sharp && dotnet run --project flow-interpreter tests/test_noise_builtin.flow 2>&1 | tail -40</automated>
  </verify>
  <done>
    - Script exits 0
    - Output contains "All noise-builtin tests passed."
    - Output contains all five Test header lines (Test 1, Test 2, Test 3, Test 4, Test 5a/5b/5c)
    - prettyBuffer output for the short buffer shows non-zero samples (i.e., the noise actually wrote into the buffer, proving the SynthUtils wiring is live)
    - No "Error:" or "Exception" strings in stdout/stderr
  </done>
</task>

</tasks>

<verification>
After both tasks complete:

1. **Build clean:** `dotnet build` produces 0 errors, 0 new warnings related to noise.
2. **New test passes:** `dotnet run --project flow-interpreter tests/test_noise_builtin.flow` exits 0.
3. **Existing tests still pass (regression smoke — pick 3 representative tests):**
   ```bash
   for t in tests/test_comprehensive.flow tests/test_audio.flow tests/test_buffer.flow; do
     [ -f "$t" ] && dotnet run --project flow-interpreter "$t" > /dev/null 2>&1 \
       && echo "PASS $t" || echo "FAIL $t"
   done
   ```
   (Pick whichever audio-adjacent tests actually exist in `tests/`; the point is to confirm pure-additive change didn't break neighbors.)
4. **Multi-source coverage:**
   - GOAL: "Add a `noise` builtin" → covered by Task 1 (4 overloads registered)
   - REQ: QUICK-OIB → covered by Tasks 1 + 2
   - CONTEXT: charitable interpretation memory → covered by clamping logic + Test 5
   - All four overloads from `<task_context>` requirements → Noise1 / Noise2 / Noise3 / Noise (4-arity core)
</verification>

<success_criteria>
- All four `noise` overloads callable from .flow scripts via S-expression syntax
- `tests/test_noise_builtin.flow` runs end-to-end and exits 0
- prettyBuffer output for a noise buffer shows non-zero samples (visual proof of wiring)
- No edits to CreateClip / GenerateSine / Value.ToString (constraint compliance)
- Charitable clamping holds for negative seconds, zero channels, zero sampleRate (no thrown exceptions)
- Pure additive: `git diff --stat` shows additions only in the four target files
</success_criteria>

<output>
After completion, create `.planning/quick/260502-oib-add-noise-builtin-noise-seconds-overload/260502-oib-01-SUMMARY.md` documenting:
- The four registered signatures
- The defaults chosen (44100 Hz, mono, amplitude 1.0)
- The clamping decisions (negative seconds → 0, channels < 1 → 1, sampleRate <= 0 → 44100)
- Confirmation that BuiltInDocs.cs:97 description ("Generates a white-noise buffer.") is now backed by an actual implementation
</output>
