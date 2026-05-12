namespace FlowLang.Tests;

public static class FlowScriptData
{
    public static IEnumerable<object[]> GetFlowScripts()
    {
        var testsRoot = FindTestsRoot();
        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.flow", SearchOption.AllDirectories))
        {
            // Skip tests/std.flow — it's a stdlib module, not a test
            if (Path.GetFileName(path) == "std.flow") continue;
            yield return new object[] { Path.GetRelativePath(testsRoot, path) };
        }
    }

    public static string FindTestsRoot()
    {
        // Walk up from AppContext.BaseDirectory (bin/Debug/net10.0/) until we find tests/
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests")))
            dir = dir.Parent;
        return dir != null ? Path.Combine(dir.FullName, "tests")
            : throw new DirectoryNotFoundException("Could not locate tests/ directory");
    }

    public static readonly Dictionary<string, string> ExpectedErrorScripts = new()
    {
        // Scripts that intentionally emit errors. Assertion: stderr contains the expected substring.
        // Paths are relative to tests/ root and use forward slashes; normalize on lookup.
        ["test_error_masking.flow"] = "Function 'nonExistentFunction' not found",
        ["test_musical_context_errors.flow"] = "Tempo must be positive",

        // Iteration-guard regression test: runaway while loops must report an iteration-limit
        // error. The script intentionally triggers 2 errors (default 10000 limit + custom 100 limit).
        ["test_iteration_guard.flow"] = "Iteration limit",

        // spike/c2 (Phase 11 Dismissed): probe script intentionally invokes a missing function
        // to prove the error path does NOT set _returnValue (i.e., statements after the error
        // continue to execute). The error IS expected in stderr.
        [Path.Combine("spike", "c2-return-value-short-circuit.flow")] = "Function 'nonExistentFn' not found",

        // spike/c1: PRE-FIX (Phase 11 commit) the body sentinels are absent → this row is RED until plan 12-04 lands.
        // Post-FIX-07a: body sentinels present, stderr still contains the tempo error → Theory row flips GREEN.
        // Plan 12-01 commits this as RED deliberately per D-11.
        [Path.Combine("spike", "c1-musical-context-body.flow")] = "Tempo must be positive",

        // Plan 12-05 status:
        //   - test_custom_oscillator.flow:42 if(Bool, String, String) overload — FIXED by 12-05
        //   - test_custom_oscillator.flow:57 if(Bool, Double, Double) overload — FIXED by 12-05
        //     (test file updated to avoid `1.0 -1.0` parser-ambiguity which tokenizes as subtraction)
        //   - test_custom_oscillator.flow:86 `range` stdlib function — FIXED by Phase 20 plan 20-01
        //     (DEFER-01 closure: range(Int, Int) + range(Int, Int, Int) registered in
        //     BuiltInFunctions.RegisterCollections; entry removed because the script now
        //     runs to completion with zero errors. Plan 20-01 acknowledged this would
        //     happen — the 20-04 closure plan loses an item from its tracked migration
        //     list, but the atomic-commit-zero-regression contract takes priority over
        //     the "do not touch" instruction. Rule 3 deviation; see 20-01-SUMMARY.md.)
        //   - test_full_song.flow:158 exportWav auto-mkdir — FIXED by 12-05

        // Phase 26.1 plan 26.1-05 (Wave 4): test_dict_type_errors.flow body is the
        // intentional-error trigger (Dict<Buffer, Int> bad = (dict)). The runner
        // expects stderr to contain "Dict key type 'Buffer' is not hashable" at the
        // ParseException site (TypeParser.ParseType § Dict<K, V> branch).
        ["test_dict_type_errors.flow"] = "Dict key type 'Buffer' is not hashable",
    };

    public static readonly Dictionary<string, string[]> RequiredSentinels = new()
    {
        // spike/c1 body-execution evidence. Absent pre-FIX-07a → assertion fails (RED).
        // Present post-FIX-07a → assertion passes (GREEN). Flip commit lives in plan 12-04.
        [Path.Combine("spike", "c1-musical-context-body.flow")] = new[]
        {
            "c1-probe1-body-ran",
            "c1-probe2-stmt1",
            "c1-probe2-stmt2",
            "c1-probe3-body-ran",
        },

        // Phase 13-01 (FIX-01): pin transpose-with-int success sentinel.
        // Script prints "transpose with int: ok" on success and exits 1
        // on the "No matching overload" failure path.
        ["test_transpose_int.flow"] = new[]
        {
            "transpose with int: ok",
            "test_transpose_int: PASSED",
        },

        // Phase 13-02 (DX-01): pin arithmetic outputs that follow comment lines.
        // Script exercises full-line, inline-after-code, pre-code, post-note-stream,
        // inside-proc, and empty-// comment styles. If `//` support is removed from
        // SkipWhitespaceAndComments, the inline-comment line (`Int x = 5 // inline comment`)
        // tokenizes `//` as a division operator → parse error → none of these print.
        // "note stream ok" (line 31) specifically pins the post-note-stream inline comment.
        // "42" (line 40) specifically pins the empty "//" comment (line 39) — the line
        // after must still tokenize. "All comment tests passed" (line 43) pins full-run.
        ["test_comments.flow"] = new[]
        {
            "note stream ok",
            "42",
            "All comment tests passed",
        },

        // Phase 13-02 (DX-02): pin math stdlib numeric outputs.
        // PASS-2 EMPIRICAL CAPTURE (Pitfall 5): Flow's `str` formats Doubles with
        // ~10 significant digits, NOT full Double precision. `str pi` → "3.141592654"
        // (NOT Math.PI.ToString() = "3.141592653589793"). Pass 1 drafted the latter;
        // Pass 2 captured the former from `dotnet run --project flow-interpreter
        // tests/test_math.flow` stdout. See 07-VALIDATION.md §Divergences.
        // Sentinels chosen to exercise: pi constant (DX-02), pow (double-arg math),
        // and final pass marker (whole-run gate).
        ["test_math.flow"] = new[]
        {
            "3.141592654",           // pi — empirical Flow `str` format, NOT Math.PI.ToString()
            "6.283185307",           // tau — empirical (10-sig-digit precision)
            "1024",                  // pow(2.0, 10.0) — gates pow registration
            "All math tests passed", // whole-script-ran gate
        },

        // Phase 13-02 (DX-03): pin both writeWav + exportWav alias success.
        // Script calls writeWav("path", buf) then exportWav(buf, "path"), then loads
        // both back via loadWav and asserts non-zero frames. If either signature
        // were unregistered, its PASS line would not print.
        ["test_writewav.flow"] = new[]
        {
            "PASS: writeWav(String, Buffer) succeeded",
            "PASS: exportWav(Buffer, String) backwards compat succeeded",
            "All writeWav tests passed",
        },

        // Phase 13-03 (AUDIO-05): pin mix frame count + channel count.
        // PASS-2 EMPIRICAL CAPTURE: createSineTone produces STEREO buffers
        // (Channels == 2), so mix(createSineTone ..., createSineTone ...)
        // yields a stereo output. Pass 1 drafted "mix channels: 1"; Pass 2
        // captured "mix channels: 2" from dotnet run. See §Divergences.
        // 22050 frames = 0.5s × 44100Hz — deterministic sample-math anchor.
        // "mix tests passed" = whole-run gate.
        ["test_mix.flow"] = new[]
        {
            "mix frames: 22050",
            "mix channels: 2",
            "mix tests passed",
        },

        // Phase 13-03 (AUDIO-06): pin per-section gain evaluation sentinels.
        // Script drives gain context at top-level AND nested under
        // tempo { key { gain { ... } } }, rendering a quiet section via
        // renderSong. 88200 frames = 4 quarter notes × 120bpm × 44100Hz
        // (empirically verified). If gain context parse or stack-walk were
        // removed, the nested block body would not execute and sentinels
        // would not print.
        ["test_gain_context.flow"] = new[]
        {
            "gain 0.5 block executed",
            "nested gain context executed",
            "quiet section frames: 88200",
            "gain context tests passed",
        },

        // Phase 13-03 (AUDIO-07): pin three-preset renderSequenceToVoices success.
        // Script exercises strings/organ/bell via renderSequenceToVoices and
        // renderSong. If SynthesizerFactory.Create were missing a branch, the
        // "rendered voices OK" line for that preset would not print. The Unit
        // Fact (SynthesizerFactoryTests) pins structural dispatch; this Theory
        // row pins end-to-end rendering through the stdlib.
        ["test_synth_presets.flow"] = new[]
        {
            "strings: rendered voices OK",
            "organ: rendered voices OK",
            "bell: rendered voices OK",
            "synth preset tests passed",
        },

        // Phase 13-04 (AUDIO-08): pin the ritardando/accelerando boolean outputs
        // from test_tempo_ramp.flow. The script prints three "Test N - …: true"
        // lines proving that (1) tempoRamp produces a non-zero buffer,
        // (2) ritardando (120→80 BPM) produces MORE frames than constant 120 BPM
        // (slowing down = more seconds = more samples), and (3) accelerando
        // (80→120 BPM) produces FEWER frames than constant 80 BPM. Sentinels
        // captured empirically from `dotnet run --project flow-interpreter
        // tests/test_tempo_ramp.flow` — the strings match the script's
        // `(concat "Test N - …: " (str testN))` output verbatim (Bool `str`
        // formats as "true"/"false"). If tempoRamp's integration math were
        // reverted to a naive constant-BPM render, test2 and test3 would flip
        // to "false" and the Theory row fails.
        ["test_tempo_ramp.flow"] = new[]
        {
            "Test 1 - tempoRamp produces non-zero buffer: true",
            "Test 2 - Ritardando produces more frames than constant fast: true",
            "Test 3 - Accelerando produces fewer frames than constant slow: true",
        },

        // Phase 13-05 (VOC-01/VOC-02): pin vocalization test PASS sentinels.
        // Empirical capture via `dotnet run --project flow-interpreter
        // tests/test_vocalization.flow` — strings match the script's four
        // PASS-marker prints verbatim (script at tests/test_vocalization.flow
        // lines 9, 21, 31, 39). The four sentinels gate the four
        // distinct sub-tests:
        //   - "PASS: sing ah produced audio buffer"        — Test 1, single vowel render
        //   - "PASS: all 5 vowels synthesized"             — Test 2, ee/eh/oh/oo coverage
        //   - "PASS: consonant syllables synthesized"      — Test 3, na/ta/sa coverage
        //   - "PASS: vocal mixed with instrumental"        — Test 4, mix() integration
        // If any sub-test's code path regressed (formant dispatch removed,
        // consonant onset broken, mix signature incompatible), its PASS
        // line would not print and the Theory row fails. Wider pattern
        // matches Phase 13-04's boolean-result-concat sentinel idiom.
        ["test_vocalization.flow"] = new[]
        {
            "PASS: sing ah produced audio buffer",
            "PASS: all 5 vowels synthesized",
            "PASS: consonant syllables synthesized",
            "PASS: vocal mixed with instrumental",
        },

        // Phase 15 DX-07: reverbTime parses, renders, and short-circuits at 0.
        // Wave 0 placeholder — body is a sentinel-only print; Plan 03 replaces the
        // body with a real reverbTime render while preserving these two sentinels.
        ["test_reverb_time.flow"] = new[]
        {
            "reverbTime 2.5: PASSED",
            "reverbTime 0 dry: PASSED",
        },

        // Phase 15 DX-09: euclidean 4-arg swing overload.
        // Wave 0 placeholder — Plan 06 replaces the body with a real euclidean
        // swing call while preserving this sentinel.
        ["test_euclidean_swing.flow"] = new[]
        {
            "euclidean swing: PASSED",
        },

        // Phase 15 DX-09: euclidean 6-arg humanize overload, same-seed byte-identical.
        // Wave 0 placeholder — Plan 06 replaces the body with euclidean humanize +
        // writeMidi + byte-identical-two-runs check while preserving both sentinels.
        ["test_euclidean_humanize.flow"] = new[]
        {
            "euclidean humanize seed=42: PASSED",
            "two runs byte-identical: PASSED",
        },

        // Phase 25 DEFER-06: humanizeGaussian(Sequence, Double, Int) seeded Box-Muller.
        // Wave 0 placeholder — Plan 25-02 replaces the .flow body with humanizeGaussian +
        // writeMidi + byte-identical-two-runs check while preserving both sentinels.
        ["test_humanize_gaussian.flow"] = new[]
        {
            "humanizeGaussian seed=42: PASSED",
            "two runs byte-identical: PASSED",
        },

        // Phase 20-01 (DEFER-01): pin range(Int, Int) + range(Int, Int, Int) success sentinels.
        // 2-arg form, 3-arg positive step, 3-arg negative step (via (sub 0 1) per Pitfall 4),
        // and the whole-run pass marker. If any range overload misregistered, the corresponding
        // sentinel does not print and this Theory row goes RED.
        ["test_range.flow"] = new[]
        {
            "range 0 5 ok",
            "range 0 10 2 ok",
            "range 5 0 -1 ok",
            "test_range: PASSED",
        },

        // Phase 20-02 (DEFER-04): pin multi-letter enharmonic edges (E↔Fb, F↔E#, B↔Cb, C↔B#)
        // and D/G/A naturals unchanged. Format canonical output: Fb4 → "F4-", E#4 → "E4+",
        // Cb5 → "C5-", B#3 → "B3+". If the natural-edge switch in HarmonyFunctions.Enharmonic
        // regresses, the corresponding sentinel does not print and this Theory row goes RED.
        // The Bbmajor block at the end of the script exercises the in-key chromatic
        // fall-through path: E is chromatic in Bbmajor (scale: Bb C D Eb F G A), so
        // TryEnharmonicInKey returns false and we drop into the natural-edge — same "F4-"
        // sentinel (already counted once for the no-key E4 print at the top).
        ["test_enharmonic_edges.flow"] = new[]
        {
            "F4-",
            "E4+",
            "C5-",
            "B3+",
            "DGA naturals unchanged: ok",
            "test_enharmonic_edges: PASSED",
        },

        // Phase 20-03 (DEFER-05): pin slice negative-from-end (Python-style) sentinels.
        // Negative start, negative end, both-negative, extreme-negative clamp-to-zero
        // (D-USER-D), whole-run pass marker. If the pre-clamp normalization in
        // Collections.SliceArray regresses, the sentinels do not match and this row goes RED.
        ["test_slice_negative.flow"] = new[]
        {
            "neg start ok len=3",
            "neg end ok len=4",
            "both neg ok len=2",
            "extreme neg ok len=2",
            "test_slice_negative: PASSED",
        },

        // Phase 22-01 (DX-10): pin the 4-arg arpeggio(Chord, NoteValue, String, String)
        // smoke script's PASSED sentinel. Script exercises up/down/updown directions over
        // Cmaj7 at QUARTER and EIGHTH rates. If the 4-arg overload regresses (signature
        // missing, ApplyDirection broken, or pattern arg fails type-dispatch), the sentinel
        // does not print and this Theory row goes RED.
        ["test_dx_arpeggio.flow"] = new[]
        {
            "DX-10 arpeggio: PASSED",
        },

        // Phase 22-02 (DX-15): pin the varispeed loadWav smoke script. Script writes a
        // synthetic 1s sine, reloads at +12 semitones (octave up = ratio 2.0 = ~half frames),
        // exercises the ratio overload at 1.5, and verifies the semitones=0 short-circuit.
        // If either new overload regresses or the existing 1-arg loadWav byte-identity
        // breaks, the sentinel does not print and this Theory row goes RED.
        ["test_dx_loadwav_varispeed.flow"] = new[]
        {
            "DX-15 varispeed: PASSED",
        },

        // Phase 22-03 (DX-11): pin the inversion(Chord, Int) + voicing(Chord, String) smoke
        // script. Script exercises (inversion Cmaj 1), (voicing Cmaj7 "drop2"), and the D-07
        // charitable path (voicing Cmaj "drop2") returning the input chord unchanged. If the
        // Voicings.Register wiring regresses, the sentinel does not print and this Theory
        // row goes RED.
        ["test_dx_voicings.flow"] = new[]
        {
            "DX-11 voicings: PASSED",
        },

        // Phase 22-04 (DX-12): pin the NoteValue-rate delay smoke script. Script exercises
        // (delay src EIGHTH 0.5 0.4) inside `tempo 120 { ... }` (250ms) and `tempo 240 { ... }`
        // (125ms), plus the existing ms-rate (delay src 250.0 0.5 0.4) regression gate. If
        // the new RegisterContextDependent wiring regresses or the existing Double overload
        // diverges, the sentinel does not print and this Theory row goes RED.
        ["test_dx_delay_sync.flow"] = new[]
        {
            "DX-12 delay sync: PASSED",
        },

        // Phase 22-05 (DX-13): pin the quantize(Sequence, NoteValue, strength, swing) smoke
        // script. Script exercises a humanize→quantize roundtrip at SIXTEENTH+strength=1.0,
        // plus the strength=0 identity short-circuit (Pitfall 9 byte-identical regression
        // gate). If the new RegisterContextDependent wiring regresses, OnsetOffset migration
        // breaks, or strength=0 stops short-circuiting, the sentinel does not print and this
        // Theory row goes RED.
        ["test_dx_quantize.flow"] = new[]
        {
            "DX-13 quantize: PASSED",
        },

        // Phase 22-06 (DX-14 legato): pin the legato(Sequence, Double) smoke script. Script
        // exercises (legato seq 0.5) over a QUARTER-note phrase under tempo 120 / 4/4 and
        // writes a WAV via renderSong. If the DurationOverlap migration regresses (field
        // missing, BarRenderer not reading it, or onsets accidentally moved), the sentinel
        // does not print and this Theory row goes RED.
        ["test_dx_legato.flow"] = new[]
        {
            "DX-14 legato: PASSED",
        },

        // Phase 22-06 (DX-14 portamento): pin the portamento(Sequence, Millisecond) smoke
        // script. Script exercises (portamento seq 100ms) and writes a MIDI file via
        // writeMidi. If the PortamentoMs migration regresses (field missing, MidiExport not
        // emitting CC65/CC5, or the linear ms→CC5 curve diverges), the sentinel does not
        // print and this Theory row goes RED. The accompanying PortamentoMidiFacts read the
        // generated .mid back via DryWetMidi to assert CC events present.
        ["test_dx_portamento.flow"] = new[]
        {
            "DX-14 portamento: PASSED",
        },
    };
}
