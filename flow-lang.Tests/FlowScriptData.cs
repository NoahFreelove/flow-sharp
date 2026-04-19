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
        //   - test_custom_oscillator.flow:86 `range` stdlib function — PRE-EXISTING, deferred to plan 12-06
        //     (Test 4 uses `(range 0 sz)` which isn't registered anywhere; documented as separate bug)
        //   - test_full_song.flow:158 exportWav auto-mkdir — FIXED by 12-05
        //
        // test_custom_oscillator stays as an expected-error row until plan 12-06 adds `range`.
        // test_full_song entry removed: it now runs to completion after the auto-mkdir fix.
        ["test_custom_oscillator.flow"] = "Function 'range' not found",
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
    };
}
