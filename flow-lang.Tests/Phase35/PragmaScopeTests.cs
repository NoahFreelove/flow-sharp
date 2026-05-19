using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-06 Wave 0 — Pitfall 4 + Phase 21 PRAG-02 per-file pragma
/// scope.
///
/// Pins:
///   - <c>enable matchExhaustive;</c> declared in mod1.flow does NOT propagate
///     across <c>use "mod1"</c> into the importer.
///   - mod2.flow (the importer) with its own non-exhaustive match still
///     follows the CHARITABLE policy (WARN-only, no error) when mod2 itself
///     does NOT declare the pragma.
///
/// RED state: Until Plan 35-06 ships pragma threading, neither file's match
/// triggers an error AND no WARN is emitted (Plan 35-05's policy is silent).
/// Task 4 makes the contract observable.
/// </summary>
[Collection("RenderingDiagnostics")]
public class PragmaScopeTests
{
    [Fact]
    public void PragmaPerFileDoesNotPropagateViaUse()
    {
        RenderingDiagnostics.ResetForTesting();

        // mod1.flow: declares the pragma + has a non-exhaustive match. The pragma
        // is intra-file, so this file's match WOULD promote to error if Test 1
        // independently parsed it — but here we're calling mod1 via `use` from
        // mod2, and mod2 (the importer) does not declare the pragma.
        // The contract: mod2's match is CHARITABLE (Void + WARN); mod1's match
        // (if reached during module load) is STRICT (error).
        //
        // For the load-bearing PRAG-02 assertion, we need mod2 to import mod1
        // AND have its OWN non-exhaustive match. mod2's match must NOT error.
        var tempDir = Path.Combine(Path.GetTempPath(),
            "flow-pragma-scope-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mod1Path = Path.Combine(tempDir, "mod1.flow");
            var mod2Path = Path.Combine(tempDir, "mod2.flow");

            // mod1 declares the pragma; it does NOT itself contain a match
            // expression — its job is purely to be imported. (Plan 21 D-06
            // semantic test: pragma-set is per-file; declaring it in mod1
            // must not bleed into mod2.)
            File.WriteAllText(mod1Path, "enable matchExhaustive;\n");

            // mod2 imports mod1, then evaluates a non-exhaustive match.
            // Per Pitfall 4 + PRAG-02 the importer's PragmaSet is its OWN
            // (empty here), so mod2's match must follow the charitable policy.
            File.WriteAllText(mod2Path,
                "use \"" + mod1Path.Replace('\\', '/') + "\"\n"
                + "(match 5 | 1 => \"one\" | 2 => \"two\")\n");

            using var engine = new FlowEngine(verbose: false);
            engine.Execute(File.ReadAllText(mod2Path), mod2Path);

            // The importer (mod2) had no pragma → its non-exhaustive match
            // stays charitable → no error level diagnostic emitted.
            var nonExhaustiveErrors = 0;
            foreach (var diag in engine.ErrorReporter.Diagnostics)
            {
                if (diag.Level == DiagnosticLevel.Error
                    && diag.Message.Contains("non-exhaustive", System.StringComparison.OrdinalIgnoreCase))
                    nonExhaustiveErrors++;
            }
            foreach (var err in engine.ErrorReporter.Errors)
            {
                if (err.Message.Contains("non-exhaustive", System.StringComparison.OrdinalIgnoreCase))
                    nonExhaustiveErrors++;
            }
            Assert.Equal(0, nonExhaustiveErrors);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
