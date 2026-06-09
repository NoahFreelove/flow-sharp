using FlowLang.Tests.Helpers;
using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.Integration.Phase47;

/// <summary>
/// Phase 47 Plan 47-05 / D-47-14 — Web-target invariant test. Reflectively
/// scans the Web-compiled flow-lang.dll for references to stripped namespaces.
///
/// Catches drift: if a future PR adds <c>using Rug.Osc;</c> in a non-stripped
/// file (e.g., FlowEngine.cs WITHOUT the <c>#if !FLOW_WEB</c> guard), the Web
/// build's flow-lang.dll gains a stale type-reference. This test fires RED at
/// that point — invariant broken; PR cannot merge until fixed.
///
/// The test runs ONLY under FlowTarget=Web. On Desktop it skips with
/// documented reason (Desktop assembly LEGITIMATELY references Rug.Osc /
/// FileSystemWatcher / etc., so scanning it would always fail — wrong target).
/// </summary>
public class AssemblyReferenceScanTests
{
    /// <summary>
    /// Forbidden type-reference prefixes. Must NOT appear in the Web-compiled
    /// flow-lang.dll's GetTypeReferences() output. Per D-47-14 strip list +
    /// Phase 40 RtMidi.Core forward-look.
    /// </summary>
    private static readonly string[] ForbiddenTypeRefPrefixes = new[]
    {
        "Rug.Osc",
        "RtMidi.Core",
        // Phase 40 D-40-04 / T-40-03: JackSharp (best-effort JACK transport,
        // Linux-only native dep) must never reach the Web closure either.
        // Added by Plan 40-01 Task 1 even though JACK ships best-effort later —
        // the forbidden-prefix gate stands ready so a future @jack file cannot
        // leak into the WASM build. RtMidi.Core was added in the D-47-14
        // forward-look; do NOT duplicate it.
        "JackSharp",
        // Phase 41 Plan 41-04 WASAPI-01 / T-41-04-WEBDRIFT / Pitfall 3: NAudio.Wasapi
        // (Windows WASAPI COM, Desktop-only native dep) must never reach the Web
        // closure. Added in the SAME commit as the NAudio.Wasapi PackageReference +
        // WasapiBackend.cs Compile-Remove so the gate and the reference ship
        // together. Catches "NAudio" / "NAudio.Wasapi" / "NAudio.CoreAudioApi" alike.
        "NAudio",
        "System.IO.FileSystemWatcher",
        // Audit 2026-06-09 §8.4: Pidgin was a dead dependency (zero `using Pidgin`
        // anywhere — SimpleLexer/Parser are manual) removed from flow-lang.csproj.
        // Gate it so it cannot quietly return on any target.
        "Pidgin",
    };

    /// <summary>
    /// Forbidden P/Invoke module-name substrings. Must NOT appear in any
    /// MethodDefinition.PInvokeInfo.Module.Name across the Web assembly.
    /// </summary>
    private static readonly string[] ForbiddenPInvokeSubstrings = new[]
    {
        "libpulse",      // PulseAudioSimpleBackend + PulseAudioCaptureBackend
        "AudioToolbox",  // CoreAudioBackend
    };

    [FlowTargetFact("Web")]
    public void WebBuild_HasNoRefsToStrippedNamespaces()
    {
        // Anchor: locate flow-lang.dll via a public type FlowLang.Core.FlowEngine.
        // typeof(FlowEngine).Assembly.Location returns the loaded assembly's
        // file path — same .dll the test runner just loaded.
        var asmPath = typeof(FlowLang.Core.FlowEngine).Assembly.Location;
        Assert.True(File.Exists(asmPath),
            $"Anchor assembly not found at expected path: {asmPath}");

        using var asm = AssemblyDefinition.ReadAssembly(asmPath);
        var module = asm.MainModule;

        // ----- Pass 1: type-reference scan -----
        var typeRefs = module.GetTypeReferences()
            .Select(tr => tr.FullName)
            .ToList();
        var leakedTypeRefs = new List<string>();
        foreach (var bad in ForbiddenTypeRefPrefixes)
        {
            foreach (var tr in typeRefs)
            {
                if (tr.StartsWith(bad, StringComparison.Ordinal))
                {
                    leakedTypeRefs.Add($"{bad} ← {tr}");
                }
            }
        }
        Assert.True(leakedTypeRefs.Count == 0,
            "Web build leaked stripped type references:\n  " +
            string.Join("\n  ", leakedTypeRefs));

        // ----- Pass 2: P/Invoke string scan -----
        var leakedPInvokes = new List<string>();
        foreach (var t in module.Types)
        {
            foreach (var m in t.Methods)
            {
                if (m.PInvokeInfo == null) continue;
                var modName = m.PInvokeInfo.Module.Name;
                foreach (var bad in ForbiddenPInvokeSubstrings)
                {
                    if (modName.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        leakedPInvokes.Add($"{bad} ← {t.FullName}.{m.Name} → DllImport(\"{modName}\")");
                    }
                }
            }
        }
        Assert.True(leakedPInvokes.Count == 0,
            "Web build leaked stripped P/Invoke targets:\n  " +
            string.Join("\n  ", leakedPInvokes));
    }

    [FlowTargetFact("Web")]
    public void WebBuild_RetainsLegitimateRefs()
    {
        // Negative-check assertion: the Web assembly DOES still reference its
        // legitimate non-stripped deps. Catches the inverse pathology — if
        // someone over-strips (e.g. accidentally drops DryWetMidi from the Web
        // build OR strips the entire FlowLang.Diagnostics namespace), this
        // test fires RED. Prevents Plan 47-05 from being a one-way ratchet
        // that allows arbitrary trimming.
        var asmPath = typeof(FlowLang.Core.FlowEngine).Assembly.Location;
        using var asm = AssemblyDefinition.ReadAssembly(asmPath);
        var module = asm.MainModule;

        var typeRefs = module.GetTypeReferences()
            .Select(tr => tr.FullName)
            .ToList();

        // FlowLang.Diagnostics types must remain — they're consumed everywhere.
        // (Sanity check the test is doing what it thinks: this assertion
        // should NEVER fire on a healthy Web build.)
        Assert.Contains(typeRefs, t => t.StartsWith("System.", StringComparison.Ordinal));
    }
}
