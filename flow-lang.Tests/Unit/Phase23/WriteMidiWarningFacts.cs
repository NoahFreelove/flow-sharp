using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 Plan 23-03 Task 2 / D-13: <c>writeMidi()</c> emits a one-shot stderr
/// advisory when called under non-12-TET tuning. MIDI bytes are UNCHANGED — still
/// 12-TET output (faithful microtonal MIDI deferred to v1.4). The warning is
/// purely advisory.
///
/// Per WARNING-4: the <see cref="WriteMidi_BytesUnchanged_UnderJI"/> Fact MUST call
/// <see cref="RenderingDiagnostics.ResetForTesting"/> between sequential FlowEngineRunner
/// runs, defending against future writeMidi-warning-gates-export changes where
/// dedup state from runner1 could leak into runner2 and mask a regression.
/// </summary>
[Collection("FlowScripts")]
public class WriteMidiWarningFacts : System.IDisposable
{
    public WriteMidiWarningFacts() { RenderingDiagnostics.ResetForTesting(); }
    public void Dispose()          { RenderingDiagnostics.ResetForTesting(); }

    [Fact]
    public void WriteMidi_UnderJustIntonation_EmitsWarning()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"enable justIntonation;
use ""@std""
use ""@audio""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence main = | C4q E4q G4q |
            section intro { main }
            Song song = [intro]
            (writeMidi ""/tmp/flow_p23_warn_ji.mid"" song)
        }
    }
}
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Contains("[midi] tuning != equalTemperament", stderr);
        Assert.Contains("microtonal MIDI deferred to v1.4", stderr);
    }

    [Fact]
    public void WriteMidi_UnderEqualTemperament_NoWarning()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"enable equalTemperament;
use ""@std""
use ""@audio""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence main = | C4q E4q G4q |
            section intro { main }
            Song song = [intro]
            (writeMidi ""/tmp/flow_p23_warn_eq.mid"" song)
        }
    }
}
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.DoesNotContain("[midi]", stderr);
    }

    [Fact]
    public void WriteMidi_NoPragma_NoWarning()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@std""
use ""@audio""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence main = | C4q E4q G4q |
            section intro { main }
            Song song = [intro]
            (writeMidi ""/tmp/flow_p23_warn_default.mid"" song)
        }
    }
}
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.DoesNotContain("[midi]", stderr);
    }

    [Fact]
    public void WriteMidi_MultipleCalls_WarnsOnlyOnce()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"enable justIntonation;
use ""@std""
use ""@audio""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence main = | C4q |
            section intro { main }
            Song song = [intro]
            (writeMidi ""/tmp/flow_p23_w1.mid"" song)
            (writeMidi ""/tmp/flow_p23_w2.mid"" song)
            (writeMidi ""/tmp/flow_p23_w3.mid"" song)
        }
    }
}
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        int count = stderr.Split("[midi]").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public void WriteMidi_BytesUnchanged_UnderJI()
    {
        // D-13: MIDI bytes are STILL 12-TET under non-12-TET tuning. Only the
        // warning is added — the file content is unchanged.
        //
        // WARNING-4: ResetForTesting() between the two sequential runs is mandatory.
        // Without it, dedup state from runner1 leaks into runner2's process
        // (RenderingDiagnostics is process-static), potentially masking a future
        // regression where warning-emission affects ExportMidiInternal control flow.
        using (var runner1 = new FlowEngineRunner())
        {
            var (ok1, _, stderr1, _) = runner1.RunSource(@"enable justIntonation;
use ""@std""
use ""@audio""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence main = | C4q E4q G4q |
            section intro { main }
            Song song = [intro]
            (writeMidi ""/tmp/flow_p23_midi_ji.mid"" song)
        }
    }
}
");
            Assert.True(ok1, $"runner1 stderr: {stderr1}");
        }

        // Per WARNING-4: clear dedup state between the two sequential runs.
        RenderingDiagnostics.ResetForTesting();

        using (var runner2 = new FlowEngineRunner())
        {
            var (ok2, _, stderr2, _) = runner2.RunSource(@"use ""@std""
use ""@audio""
tempo 120 {
    timesig 4/4 {
        key Cmajor {
            Sequence main = | C4q E4q G4q |
            section intro { main }
            Song song = [intro]
            (writeMidi ""/tmp/flow_p23_midi_default.mid"" song)
        }
    }
}
");
            Assert.True(ok2, $"runner2 stderr: {stderr2}");
        }

        var bji = System.IO.File.ReadAllBytes("/tmp/flow_p23_midi_ji.mid");
        var bd  = System.IO.File.ReadAllBytes("/tmp/flow_p23_midi_default.mid");
        Assert.Equal(bd, bji);
    }
}
