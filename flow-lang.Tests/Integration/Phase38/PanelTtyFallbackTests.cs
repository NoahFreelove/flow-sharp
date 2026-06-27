using System;
using System.Collections.Generic;
using System.IO;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-01 — Wave 0 TTY-detection fallback tests.
///
/// Asserts UI-SPEC §"TTY-Detection Fallback (locked)" lines 174-180: when
/// stdout is non-TTY (piped / NO_COLOR=1 / TERM=dumb / --no-color), the panel
/// disables ANSI cursor moves and emits one plain
/// <c>[watch] tempo=N timesig=N/N bar=N voices=N/M</c> line per state change.
///
/// The <see cref="LiveStatusPanel"/> ctor accepts <c>forceTtyMode: false</c>
/// to suppress TTY detection (so this test runner — which redirects stdout —
/// gets the plain-line path explicitly).
/// </summary>
[Collection("FlowScripts")]
public class PanelTtyFallbackTests : IDisposable
{
    /// <summary>
    /// ANSI ESC character (U+001B). Built at runtime from <c>(char)0x1B</c>
    /// so the source file stays pure ASCII and we sidestep the C# <c>\x</c>
    /// hex-escape's variable-length-when-followed-by-a-hex-digit ambiguity.
    /// </summary>
    private static readonly string Esc = new string((char)0x1B, 1);

    public PanelTtyFallbackTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact]
    public void WhenForceTtyModeFalse_EmitsPlainLineNoAnsi()
    {
        var sw = new StringWriter();
        using var panel = new LiveStatusPanel(@out: sw, forceTtyMode: false);

        panel.PublishState(
            tempo: 120,
            timesig: (4, 4),
            bar: 47,
            blocks: Array.Empty<LiveBlockDisplay>(),
            activeVoices: 8,
            poolSize: 32,
            perInstrumentCount: new Dictionary<string, int>());

        var output = sw.ToString();

        // No ANSI escape sequences (ESC = U+001B).
        Assert.False(output.Contains(Esc), $"plain-line output unexpectedly contains ESC byte: {output}");

        // Exact plain-line shape per UI-SPEC line 178.
        Assert.Contains("[watch] tempo=120 timesig=4/4 bar=47 voices=8/32", output);
    }

    [Fact]
    public void WhenNoColorEnvSet_DisablesAnsiEscapes()
    {
        using var scope = new EnvScope("NO_COLOR", "1");

        var sw = new StringWriter();
        // Even with forceTtyMode unset (default false), NO_COLOR keeps ANSI off;
        // pass forceTtyMode: true to verify NO_COLOR wins the race per the
        // color-disable detection block in UI-SPEC lines 113-118.
        using var panel = new LiveStatusPanel(@out: sw, forceTtyMode: true);

        panel.PublishState(
            tempo: 90,
            timesig: (3, 4),
            bar: 1,
            blocks: Array.Empty<LiveBlockDisplay>(),
            activeVoices: 0,
            poolSize: 32,
            perInstrumentCount: new Dictionary<string, int>());

        var output = sw.ToString();
        Assert.False(output.Contains(Esc), $"NO_COLOR=1 output unexpectedly contains ESC byte: {output}");
    }

    [Fact]
    public void PlainLineMode_EmitsOneLinePerStateChange_NotRepeatedOnSameState()
    {
        var sw = new StringWriter();
        using var panel = new LiveStatusPanel(@out: sw, forceTtyMode: false);

        // Three calls with identical state — plain-line mode emits exactly once
        // (UI-SPEC line 178 — "one plain ... line per state change").
        for (int i = 0; i < 3; i++)
        {
            panel.PublishState(
                tempo: 120,
                timesig: (4, 4),
                bar: 1,
                blocks: Array.Empty<LiveBlockDisplay>(),
                activeVoices: 0,
                poolSize: 32,
                perInstrumentCount: new Dictionary<string, int>());
        }

        var output = sw.ToString();
        int occurrences = 0;
        int idx = 0;
        const string needle = "[watch] tempo=120 timesig=4/4 bar=1";
        while ((idx = output.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            idx += needle.Length;
        }
        Assert.Equal(1, occurrences);
    }

    /// <summary>
    /// Sets / clears an environment variable for the lifetime of the using
    /// block. Restores any prior value on Dispose.
    /// </summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previous);
        }
    }
}
