using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Regression tests for audit-0609 WASM contract findings:
///   §5.4  — RunResult.Midi populated via in-memory MIDI capture hook (D-48-17/18)
///   §5.11 — StopFromJs reaches the engine's own WebAudioBackend (DrainInMemorySink
///            two-run cmp-clean, JSObject lock/dispose)
///   §5.12 — flow-runtime.js debug noise removal (C# side: debug stderr line gone)
/// </summary>
[Collection(FlowLang.Tests.Integration.Phase48.WasmEntryConsoleCollection.Name)]
public sealed class WasmContractTests
{
    // -----------------------------------------------------------------------
    // §5.4 — MIDI in-memory capture hook
    // -----------------------------------------------------------------------

    /// <summary>
    /// DrainInMemorySink starts null before any writeMidi call.
    /// </summary>
    [Fact]
    public void DrainInMemorySink_BeforeAnyCall_ReturnsNull()
    {
        // Clear any prior state from other tests in the serial collection.
        MidiExport.DrainInMemorySink();

        var result = MidiExport.DrainInMemorySink();
        Assert.Null(result);
    }

    /// <summary>
    /// Draining twice returns null on the second call — no phantom repeat.
    /// Two-run cmp-clean: RunFromJs clears before and after each run so
    /// a run with no writeMidi always sees null midi.
    /// </summary>
    [Fact]
    public void DrainInMemorySink_CalledTwice_SecondCallReturnsNull()
    {
        // Clear first.
        MidiExport.DrainInMemorySink();

        // Simulate: if any bytes were stored (via a previous test), they
        // should be consumed by the first drain.
        var first = MidiExport.DrainInMemorySink();   // null or leftover
        var second = MidiExport.DrainInMemorySink();  // must be null
        Assert.Null(second);
    }

    /// <summary>
    /// RunFromJs on a writeMidi script produces non-null RunResult.Midi with
    /// valid SMF header bytes (0x4D 0x54 0x68 0x64 = "MThd") and is
    /// byte-stable across two independent runs (two-run cmp-clean per D-48-16).
    /// This is the primary regression pin for §5.4.
    ///
    /// <para>Each run disposes the shared engine first (via DisposeFromJs) so
    /// the engine starts fresh — mirroring what happens on each browser page
    /// load. This avoids false failures from section/variable re-declaration
    /// errors in the long-lived singleton used by the Desktop test runner.</para>
    /// </summary>
    [Fact]
    public void RunFromJs_WriteMidiScript_MidiFieldNonNullAndByteStable()
    {
        // A minimal valid Flow script that writes a MIDI file.
        // Uses a temp path that is writable on Desktop; on WASM it
        // would be the inert Emscripten VFS — but the in-memory capture
        // happens before the file write so the bytes are always available.
        const string source =
            "use \"@audio\"\n" +
            "Sequence sq = | C4q D4q E4q |\n" +
            "section verse { sq }\n" +
            "Song s = [verse]\n" +
            "(writeMidi \"/tmp/audit0609_test.mid\" s)\n";

#pragma warning disable CA1416 // calling browser-only export from Desktop for regression test
        // Fresh engine for run 1 — mirrors a new page load in the browser.
        WasmEntry.DisposeFromJs();
        var json1 = WasmEntry.RunFromJs(source);

        // Fresh engine for run 2 — same source, independent engine, must
        // produce byte-identical midi (two-run cmp-clean contract D-48-16).
        WasmEntry.DisposeFromJs();
        var json2 = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var node1 = JsonNode.Parse(json1)!.AsObject();
        var node2 = JsonNode.Parse(json2)!.AsObject();

        // §5.4: midi field must be present and non-null.
        Assert.True(node1.ContainsKey("midi"), "§5.4: RunResult must contain 'midi' key after writeMidi");
        Assert.True(node2.ContainsKey("midi"), "§5.4: second run must also have 'midi' key");

        // Decode from base64 (RunResult.Midi is byte[] → JSON array of numbers or base64 per source-gen).
        // The FlowWasmJsonContext serializes byte[] as base64url per System.Text.Json default.
        var midiB64_1 = (string?)node1["midi"];
        var midiB64_2 = (string?)node2["midi"];

        Assert.NotNull(midiB64_1);
        Assert.NotNull(midiB64_2);

        var midiBytes1 = Convert.FromBase64String(midiB64_1!);
        var midiBytes2 = Convert.FromBase64String(midiB64_2!);

        Assert.NotEmpty(midiBytes1);

        // Standard MIDI File magic: "MThd" = 0x4D 0x54 0x68 0x64
        Assert.Equal(0x4D, midiBytes1[0]);
        Assert.Equal(0x54, midiBytes1[1]);
        Assert.Equal(0x68, midiBytes1[2]);
        Assert.Equal(0x64, midiBytes1[3]);

        // Two-run cmp-clean (D-48-16): same source → byte-identical midi.
        Assert.Equal(midiBytes1, midiBytes2);
    }

    /// <summary>
    /// RunFromJs on a non-writeMidi script produces null midi (omitted from JSON).
    /// Guards the null-omission invariant so we don't accidentally always emit
    /// the field.
    /// </summary>
    [Fact]
    public void RunFromJs_NoWriteMidiScript_MidiFieldAbsent()
    {
        const string source = "(print \"no midi\")";

#pragma warning disable CA1416 // calling browser-only export from Desktop for regression test
        var json = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var node = JsonNode.Parse(json)!.AsObject();

        // D-48-14 null-omission: midi absent when not emitted.
        Assert.False(node.ContainsKey("midi"),
            "RunResult.midi should be absent (null-omitted) when no writeMidi was called");
    }

    // -----------------------------------------------------------------------
    // §5.11 — StopFromJs / engine backend (Desktop-only seam test)
    // -----------------------------------------------------------------------

    /// <summary>
    /// StopFromJs is idempotent before any playback — must not throw even when
    /// no engine or backend has been initialized.
    /// </summary>
    [Fact]
    public void StopFromJs_BeforeAnyPlayback_DoesNotThrow()
    {
        // WasmEntry keeps a static _sharedEngine.  We test the exported
        // boundary on Desktop where [JSExport] is a no-op marshal.
#pragma warning disable CA1416 // calling browser-only export from Desktop for idempotency check
        var ex = Record.Exception(() => WasmEntry.StopFromJs());
#pragma warning restore CA1416
        Assert.Null(ex);
    }

    /// <summary>
    /// StopFromJs called twice in a row does not throw (idempotent contract).
    /// </summary>
    [Fact]
    public void StopFromJs_CalledTwice_DoesNotThrow()
    {
#pragma warning disable CA1416
        var ex1 = Record.Exception(() => WasmEntry.StopFromJs());
        var ex2 = Record.Exception(() => WasmEntry.StopFromJs());
#pragma warning restore CA1416
        Assert.Null(ex1);
        Assert.Null(ex2);
    }

    // -----------------------------------------------------------------------
    // §5.12 — debug stderr noise removal
    // -----------------------------------------------------------------------

    /// <summary>
    /// RunFromJs must NOT write debug "[flow-audio-cs] ..." lines to stderr.
    /// The debug log was emitted on EVERY Play call and leaked into RunResult.stderr,
    /// polluting the playground's advisory channel.  This test runs a script that
    /// exercises the audio path; the per-run stderr must not contain the removed tag.
    /// </summary>
    [Fact]
    public void RunFromJs_AudioScript_StderrDoesNotContainDebugAudioTag()
    {
        // A script that calls (play ...) — the path that previously emitted
        // "[flow-audio-cs] ..." on every invocation.
        const string source =
            "use \"@audio\"\n" +
            "Buffer b = (createSineTone 440Hz 0.1 0.3)\n" +
            "(play b)\n";

#pragma warning disable CA1416
        var json = WasmEntry.RunFromJs(source);
#pragma warning restore CA1416

        var node = JsonNode.Parse(json)!.AsObject();
        var stderr = (string?)node["stderr"] ?? string.Empty;

        Assert.DoesNotContain("[flow-audio-cs]", stderr);
    }
}
