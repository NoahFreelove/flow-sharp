using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0614;

/// <summary>
/// Regression tests for the sweep-0614 wasm-web group findings against
/// <see cref="WasmEntry.RunFromJs"/>:
///
/// <list type="bullet">
///   <item><b>Fresh engine per run</b> — re-running a declaring script through
///         the runtime (the common playground edit→Run loop) must NOT throw
///         "Variable X already declared" on the second run. Previously the
///         shared engine kept a persistent GlobalFrame + SectionRegistry.</item>
///   <item><b>WarnOnce reset per run (D-48-16)</b> — an advisory-emitting source
///         run twice must produce byte-identical RunResult JSON (incl. stderr).
///         Previously WarnOnce dedup was process-static so run 2 dropped the
///         advisory and stderr diverged.</item>
///   <item><b>SourceSnippet populated</b> — a parse error must carry the quoted
///         source line for the Rust-style diagnostic box. Previously always
///         null.</item>
///   <item><b>Last-MIDI typed-array getter</b> — <see cref="WasmEntry.GetLastMidiBytes"/>
///         returns the raw SMF bytes (a real Uint8Array across the JS boundary),
///         not the base64 STRING System.Text.Json puts in RunResult.midi.</item>
/// </list>
///
/// Shares the Phase 48 Console-redirection serial collection because RunFromJs
/// redirects process-wide Console.Out/Error.
/// </summary>
[Collection(FlowLang.Tests.Integration.Phase48.WasmEntryConsoleCollection.Name)]
public sealed class WasmRunFromJsSweepTests
{
    private static string StripDurationMs(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("durationMs");
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    // -----------------------------------------------------------------------
    // Fresh engine per run — top-level declarations must not collide.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A script with a top-level Buffer declaration run TWICE through the runtime
    /// (NO dispose between runs — exactly what the playground does on edit→Run)
    /// must succeed both times. Before the fix the second run reported
    /// "Variable t already declared in this scope".
    /// </summary>
    [Fact]
    public void RunFromJs_DeclaringScript_RunTwiceWithoutDispose_NoAlreadyDeclaredError()
    {
        const string source =
            "use \"@audio\"\n" +
            "Buffer t = (createSineTone 440Hz 0.1 0.3)\n" +
            "(play t)\n";

#pragma warning disable CA1416 // browser-only export invoked from Desktop for regression
        // Fresh process state: dispose any engine a prior test left behind.
        WasmEntry.DisposeFromJs();

        var json1 = WasmEntry.RunFromJs(source);
        // CRITICAL: no DisposeFromJs() here — reuse the runtime like the playground.
        var json2 = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var errors1 = (JsonNode.Parse(json1)!.AsObject()["errors"] as JsonArray)!;
        var errors2 = (JsonNode.Parse(json2)!.AsObject()["errors"] as JsonArray)!;

        Assert.Empty(errors1);
        Assert.Empty(errors2);
    }

    /// <summary>
    /// Same guard for a script with a top-level section + Song — the section
    /// registry must also be fresh each run.
    /// </summary>
    [Fact]
    public void RunFromJs_SectionAndSong_RunTwiceWithoutDispose_NoRedeclareError()
    {
        const string source =
            "use \"@audio\"\n" +
            "Sequence sq = | C4q D4q E4q |\n" +
            "section verse { sq }\n" +
            "Song s = [verse]\n";

#pragma warning disable CA1416
        WasmEntry.DisposeFromJs();
        var json1 = WasmEntry.RunFromJs(source);
        var json2 = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var errors1 = (JsonNode.Parse(json1)!.AsObject()["errors"] as JsonArray)!;
        var errors2 = (JsonNode.Parse(json2)!.AsObject()["errors"] as JsonArray)!;

        Assert.Empty(errors1);
        Assert.Empty(errors2);
    }

    // -----------------------------------------------------------------------
    // WarnOnce reset per run — stderr byte-identical across two runs (D-48-16).
    // -----------------------------------------------------------------------

    /// <summary>
    /// An advisory-emitting source (a degenerate @patterns combinator) run twice
    /// must produce byte-identical RunResult JSON including the stderr field.
    /// Before the fix, WarnOnce dedup was process-static so the SECOND run
    /// suppressed the advisory and stderr (and thus the JSON) diverged.
    /// </summary>
    [Fact]
    public void RunFromJs_AdvisoryEmittingSource_TwoRuns_StderrByteIdentical()
    {
        // `(fast s 0.0)` is degenerate (factor must be > 0) → emits a one-shot
        // [fast] advisory to stderr via RenderingDiagnostics.WarnOnce.
        const string source =
            "use \"@patterns\"\n" +
            "Sequence s = | C4q D4q E4q F4q |\n" +
            "(fast s 0.0)\n";

#pragma warning disable CA1416
        WasmEntry.DisposeFromJs();
        var json1 = WasmEntry.RunFromJs(source);
        var json2 = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var stderr1 = (string?)JsonNode.Parse(json1)!.AsObject()["stderr"] ?? string.Empty;
        var stderr2 = (string?)JsonNode.Parse(json2)!.AsObject()["stderr"] ?? string.Empty;

        // Defense against vacuous pass: the advisory MUST actually be present.
        Assert.Contains("[fast]", stderr1);

        // The core D-48-16 pin: both runs emit the SAME stderr.
        Assert.Equal(stderr1, stderr2);

        // And the whole RunResult JSON (minus durationMs) is byte-identical.
        var bytes1 = Encoding.UTF8.GetBytes(StripDurationMs(json1));
        var bytes2 = Encoding.UTF8.GetBytes(StripDurationMs(json2));
        Assert.Equal(bytes1, bytes2);
    }

    // -----------------------------------------------------------------------
    // SourceSnippet populated for errors carrying a line.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A parse error must carry the quoted offending source line in
    /// sourceSnippet so the playground can render the Rust-style diagnostic box.
    /// Before the fix sourceSnippet was hard-coded null.
    /// </summary>
    [Fact]
    public void RunFromJs_ParseError_PopulatesSourceSnippet()
    {
        // Unterminated call → parse error with a real line/column.
        const string source = "(print";

#pragma warning disable CA1416
        WasmEntry.DisposeFromJs();
        var json = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var errors = (JsonNode.Parse(json)!.AsObject()["errors"] as JsonArray)!;
        Assert.NotEmpty(errors);

        var first = errors[0]!.AsObject();
        var snippet = (string?)first["sourceSnippet"];
        var line = (int?)first["line"];

        Assert.NotNull(line);
        Assert.NotNull(snippet);
        // The snippet must be the actual source line, not empty.
        Assert.Contains("(print", snippet!);
    }

    /// <summary>
    /// A multi-line source's error snippet must quote the CORRECT line.
    /// </summary>
    [Fact]
    public void RunFromJs_MultiLineSource_SnippetIsOffendingLine()
    {
        const string source =
            "(print \"ok\")\n" +   // line 1 — fine
            "(print";              // line 2 — unterminated

#pragma warning disable CA1416
        WasmEntry.DisposeFromJs();
        var json = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var errors = (JsonNode.Parse(json)!.AsObject()["errors"] as JsonArray)!;
        Assert.NotEmpty(errors);

        var snippet = (string?)errors[0]!.AsObject()["sourceSnippet"];
        Assert.NotNull(snippet);
        // Must be line 2's text, not line 1.
        Assert.DoesNotContain("ok", snippet!);
    }

    // -----------------------------------------------------------------------
    // GetLastMidiBytes — raw bytes (Uint8Array across JS boundary).
    // -----------------------------------------------------------------------

    /// <summary>
    /// After a writeMidi run, GetLastMidiBytes returns the raw SMF bytes (not the
    /// base64 string RunResult.midi serializes to). First 4 bytes are "MThd".
    /// </summary>
    [Fact]
    public void GetLastMidiBytes_AfterWriteMidiRun_ReturnsRawSmfBytes()
    {
        const string source =
            "use \"@audio\"\n" +
            "Sequence sq = | C4q D4q E4q |\n" +
            "section verse { sq }\n" +
            "Song s = [verse]\n" +
            "(writeMidi \"/tmp/audit0614_test.mid\" s)\n";

#pragma warning disable CA1416
        WasmEntry.DisposeFromJs();
        var json = WasmEntry.RunFromJs(source);
        var bytes = WasmEntry.GetLastMidiBytes();
#pragma warning restore CA1416

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        // Standard MIDI File magic "MThd" = 0x4D 0x54 0x68 0x64.
        Assert.Equal(0x4D, bytes[0]);
        Assert.Equal(0x54, bytes[1]);
        Assert.Equal(0x68, bytes[2]);
        Assert.Equal(0x64, bytes[3]);

        // The raw bytes must equal the base64-decoded RunResult.midi — proving
        // GetLastMidiBytes is the SAME content, just delivered as a typed array.
        var midiB64 = (string?)JsonNode.Parse(json)!.AsObject()["midi"];
        Assert.NotNull(midiB64);
        var decoded = Convert.FromBase64String(midiB64!);
        Assert.Equal(decoded, bytes);
    }

    /// <summary>
    /// After a non-writeMidi run, GetLastMidiBytes returns an empty array
    /// (never null) so the JS side always receives a typed array.
    /// </summary>
    [Fact]
    public void GetLastMidiBytes_AfterNonMidiRun_ReturnsEmpty()
    {
        const string source = "(print \"no midi\")";

#pragma warning disable CA1416
        WasmEntry.DisposeFromJs();
        WasmEntry.RunFromJs(source);
        var bytes = WasmEntry.GetLastMidiBytes();
#pragma warning restore CA1416

        Assert.NotNull(bytes);
        Assert.Empty(bytes);
    }
}
