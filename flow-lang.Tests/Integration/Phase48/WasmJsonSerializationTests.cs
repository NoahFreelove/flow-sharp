using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-06 (fix) — regression net for the
/// <c>JsonSerializerIsReflectionDisabled</c> defect class. See
/// <c>.planning/debug/wasm-boot-no-app-bundle.md</c>.
///
/// <para><b>Why this test exists:</b> the <c>FlowTarget=Web</c> publish sets
/// <c>&lt;TrimMode&gt;full&lt;/TrimMode&gt;</c>, which disables System.Text.Json's
/// reflection-based serializer in the trimmed WASM build. A plain
/// <c>JsonSerializer.Serialize(obj, options)</c> therefore throws
/// <c>JsonSerializerIsReflectionDisabled</c> at runtime in the browser — yet the
/// identical call SUCCEEDS in the Desktop in-process runner (where reflection JSON
/// is enabled). <see cref="WasmDeterminismTests"/> calls
/// <see cref="WasmEntry.RunFromJs"/> in-process and so did NOT catch the defect:
/// the serializer path it exercised was reflection-backed on Desktop.</para>
///
/// <para><b>What this test pins instead:</b> serialization MUST go through the
/// SOURCE-GENERATED <see cref="FlowWasmJsonContext"/> (which carries its own
/// compile-time metadata and is trim-safe) and MUST still produce the D-48-14/15
/// shape — camelCase property names + null-omission for <c>wav</c>/<c>midi</c>.
/// Asserting through the generated <c>FlowWasmJsonContext.Default.RunResult</c>
/// metadata is the strongest available browser-free proxy: if the source-gen
/// context regresses (wrong casing, missing null-omission, or removal) this test
/// fails on the Desktop runner BEFORE a browser smoke.</para>
///
/// <para><b>Still required for full closure:</b> a real-browser re-smoke (serve
/// the AppBundle, click Run, expect an audible 440 Hz tone + structured stdout/
/// errors with NO <c>JsonSerializerIsReflectionDisabled</c>). This test cannot
/// drive a browser; it proves the serialization CONTRACT, not the live boot.</para>
/// </summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class WasmJsonSerializationTests
{
    /// <summary>
    /// The source-gen context serializes a populated <see cref="RunResult"/> with
    /// camelCase keys, and the null <c>wav</c>/<c>midi</c> fields are omitted —
    /// the exact D-48-14/15 shape JS (and <see cref="WasmDeterminismTests"/>) parse.
    /// </summary>
    [Fact]
    public void SourceGenContext_Serializes_CamelCase_WithNullOmission()
    {
        var result = new RunResult
        {
            Wav = null,
            Midi = null,
            Stdout = "hello flow\n",
            Stderr = "[advisory] something\n",
            Errors = new[]
            {
                new RunError("eval", "boom", 3, 7, null),
            },
            DurationMs = 12,
        };

        // Serialize through the SAME source-gen path RunFromJs uses. If reflection
        // were required here this would be a different (reflection) code path; by
        // going through FlowWasmJsonContext.Default we pin the trim-safe one.
        var json = JsonSerializer.Serialize(result, FlowWasmJsonContext.Default.RunResult);

        var node = JsonNode.Parse(json)!.AsObject();

        // camelCase keys present (D-48-15).
        Assert.True(node.ContainsKey("stdout"), "expected camelCase 'stdout'");
        Assert.True(node.ContainsKey("stderr"), "expected camelCase 'stderr'");
        Assert.True(node.ContainsKey("errors"), "expected camelCase 'errors'");
        Assert.True(node.ContainsKey("durationMs"), "expected camelCase 'durationMs'");

        // PascalCase MUST NOT leak.
        Assert.False(node.ContainsKey("Stdout"), "PascalCase 'Stdout' leaked");
        Assert.False(node.ContainsKey("DurationMs"), "PascalCase 'DurationMs' leaked");

        // null-omission (D-48-14): wav/midi were null → omitted entirely.
        Assert.False(node.ContainsKey("wav"), "null 'wav' should be omitted");
        Assert.False(node.ContainsKey("midi"), "null 'midi' should be omitted");

        // Payload round-trips.
        Assert.Equal("hello flow\n", (string?)node["stdout"]);
        Assert.Equal(12, (int?)node["durationMs"]);

        var err = node["errors"]!.AsArray()[0]!.AsObject();
        Assert.Equal("eval", (string?)err["kind"]);
        Assert.Equal("boom", (string?)err["message"]);
        Assert.Equal(3, (int?)err["line"]);
        Assert.Equal(7, (int?)err["column"]);
        // SourceSnippet was null → omitted.
        Assert.False(err.ContainsKey("sourceSnippet"), "null 'sourceSnippet' should be omitted");
    }

    /// <summary>
    /// End-to-end via the actual export: <see cref="WasmEntry.RunFromJs"/> returns
    /// JSON with the pinned camelCase shape. This is the integration mirror of the
    /// unit assertion above — it proves the export wires the source-gen context in.
    /// </summary>
    [Fact]
    public void RunFromJs_Produces_CamelCase_RunResult_Shape()
    {
#pragma warning disable CA1416 // browser-only export; the Execute path is platform-agnostic on Desktop
        var json = WasmEntry.RunFromJs("(print \"hi\")");
#pragma warning restore CA1416

        var node = JsonNode.Parse(json)!.AsObject();

        Assert.True(node.ContainsKey("stdout"));
        Assert.True(node.ContainsKey("durationMs"));
        Assert.True(node.ContainsKey("errors"));
        Assert.False(node.ContainsKey("Stdout"), "PascalCase leaked from RunFromJs");
        // No render → wav/midi omitted.
        Assert.False(node.ContainsKey("wav"));
        Assert.False(node.ContainsKey("midi"));
        Assert.Contains("hi", (string?)node["stdout"] ?? string.Empty);
    }
}
