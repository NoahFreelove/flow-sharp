using System.IO;
using FlowLang.Tests;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-03 Task 2 — backfill completeness gate for the universal
/// named-argument call surface (D-36-11). For each in-scope standard-library
/// source file, asserts that every non-varargs `registry.Register(` call site
/// declares a `ParameterNames: [...]` on its accompanying FunctionSignature.
///
/// Authored at Plan 36-03 Task 2 with the COMPLETE [InlineData] roster for
/// BOTH 36-03's file scope and 36-04's file scope. Plan 36-04 will NOT edit
/// this file — its source-file backfill flips the 36-04 rows from RED to
/// GREEN one at a time. This single-author convention eliminates the
/// parallel-write conflict on this test file (CONTEXT D-36-11 + plan
/// checker review).
///
/// Counting protocol:
///   - Exclude commented-out call sites (lines starting with `//` after
///     optional leading whitespace).
///   - `registerCount` = count of `registry.Register(` occurrences on
///     non-comment lines.
///   - `paramNamesCount` = count of `ParameterNames` occurrences on
///     non-comment lines (the resolver-recognized field).
///   - `varArgsCount` = count of `IsVarArgs: true` occurrences on
///     non-comment lines (these are exempt per RESEARCH Open Question 2
///     and Plan 36-02 Test 10).
///   - Invariant: registerCount == paramNamesCount + varArgsCount.
///
/// Files that do not yet exist (e.g., DSP/CompressorFunctions.cs which Plan
/// 36-04 may create as part of its work) are silently treated as zero-on-zero
/// rather than skipped — this keeps the roster complete and lets the test
/// flip GREEN automatically when the file appears.
///
/// Mirrors the file-scan shape of `Integration/Phase29/LicenseAuditTests`.
/// </summary>
public class ParameterNamesCoverageTest
{
    // ===== Plan 36-03 scope (GREEN after Plan 36-03 Tasks 1 + 2 land) =====
    [Theory]
    [InlineData("StandardLibrary/BuiltInFunctions.cs")]
    [InlineData("StandardLibrary/StdLib.cs")]
    [InlineData("StandardLibrary/Collections.cs")]
    [InlineData("StandardLibrary/Bars.cs")]
    [InlineData("StandardLibrary/Collections/DictFunctions.cs")]

    // ===== Plan 36-04 scope (RED at Plan 36-03 closure; GREEN after Plan 36-04 backfill) =====
    [InlineData("StandardLibrary/Audio/EffectsFunctions.cs")]
    [InlineData("StandardLibrary/Audio/SignalGeneration.cs")]
    [InlineData("StandardLibrary/Audio/FileIO.cs")]
    [InlineData("StandardLibrary/Audio/PanningFunctions.cs")]
    [InlineData("StandardLibrary/Audio/PlaybackFunctions.cs")]
    [InlineData("StandardLibrary/Audio/AudioCore.cs")]
    [InlineData("StandardLibrary/Audio/ClassicalComposition.cs")]
    [InlineData("StandardLibrary/Audio/MidiExport.cs")]
    [InlineData("StandardLibrary/Audio/DSP/CompressorFunctions.cs")]
    [InlineData("StandardLibrary/Audio/DSP/ReverbFunctions.cs")]
    [InlineData("StandardLibrary/Audio/DSP/FilterFunctions.cs")]
    [InlineData("StandardLibrary/Audio/DSP/DelayFunctions.cs")]
    [InlineData("StandardLibrary/Audio/Tuning/ScalaBuiltins.cs")]
    [InlineData("StandardLibrary/Audio/Sfz/SfzBuiltins.cs")]
    [InlineData("StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs")]
    [InlineData("StandardLibrary/Transforms/TransformFunctions.cs")]
    [InlineData("StandardLibrary/Composition/VariationFunctions.cs")]
    [InlineData("StandardLibrary/Composition/PolyrhythmFunctions.cs")]
    [InlineData("StandardLibrary/Composition/SongFunctions.cs")]
    [InlineData("StandardLibrary/Harmony/HarmonyFunctions.cs")]
    [InlineData("StandardLibrary/Harmony/Voicings.cs")]
    [InlineData("StandardLibrary/TestFramework/TestFunctions.cs")]
    [InlineData("StandardLibrary/VisualizationFunctions.cs")]
    public void EveryRegisterCallSite_DeclaresParameterNames(string relativePath)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string absPath = Path.Combine(repoRoot, "flow-lang", relativePath);

        // Files that do not yet exist are treated as zero-on-zero. This keeps the
        // roster complete and lets the test row flip GREEN automatically when the
        // file appears (e.g., Plan 36-04 may add DSP/*Functions.cs split files).
        if (!File.Exists(absPath))
        {
            return;
        }

        string[] lines = File.ReadAllLines(absPath);

        int registerCount = 0;
        int paramNamesCount = 0;
        int varArgsCount = 0;

        foreach (var rawLine in lines)
        {
            // Skip single-line comments: any line whose first non-whitespace
            // characters are `//`. Inline (mid-line) comments are NOT stripped —
            // none of the in-scope sources put `registry.Register(` inside a
            // comment fragment, so the conservative line-level filter suffices.
            var trimmed = rawLine.TrimStart();
            if (trimmed.StartsWith("//"))
                continue;

            registerCount += CountOccurrences(rawLine, "registry.Register(");
            paramNamesCount += CountOccurrences(rawLine, "ParameterNames");
            varArgsCount += CountOccurrences(rawLine, "IsVarArgs: true");
        }

        Assert.True(
            registerCount == paramNamesCount + varArgsCount,
            $"Backfill incomplete in flow-lang/{relativePath}: " +
            $"{registerCount} registry.Register call(s) but " +
            $"{paramNamesCount} ParameterNames + {varArgsCount} IsVarArgs " +
            $"= {paramNamesCount + varArgsCount}. " +
            $"Every non-varargs Register site must declare ParameterNames " +
            $"per Phase 36 D-36-11.");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, System.StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
