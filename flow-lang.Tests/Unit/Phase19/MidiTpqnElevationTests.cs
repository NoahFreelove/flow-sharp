using System;
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Melanchall.DryWetMidi.Core;
using Xunit;

namespace FlowLang.Tests.Unit.Phase19;

/// <summary>
/// TUP-06 — MIDI TPQN auto-elevation acceptance Facts.
/// Per CONTEXT D-05: requiredTPQN = LCM(480, 2 × union(DurationFraction.Denom)).
/// Per CONTEXT D-06: cap-error format "MIDI export requires TPQN=..., exceeds cap 9600 ...".
/// Per CONTEXT D-07: zero tuplets → TPQN stays at 480 (Phase 18 byte-identical preserved).
///
/// Each Fact runs a tiny .flow script that writes MIDI to a temp file, reads back the
/// SMF header via DryWetMidi, and asserts the TimeDivision.TicksPerQuarterNote.
/// </summary>
[Collection("FlowScripts")]
public class MidiTpqnElevationTests
{
    private static int RunAndReadTpqn(string flowSource, string testName)
    {
        string outDir = Path.Combine(Path.GetTempPath(), "flow_phase19_midi");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, $"{testName}_{Guid.NewGuid():N}.mid");
        if (File.Exists(outPath)) File.Delete(outPath);

        // Substitute the {{OUTPATH}} placeholder for the actual write path.
        string source = flowSource.Replace("{{OUTPATH}}", outPath.Replace("\\", "/"));

        using (var runner = new FlowEngineRunner())
        {
            var (success, _, stderr, errorCount) = runner.RunSource(source);
            Assert.True(success, $"Script failed: errorCount={errorCount}, stderr={stderr}");
        }

        Assert.True(File.Exists(outPath), $"MIDI not written to {outPath}");
        var midi = MidiFile.Read(outPath);
        var tpqnDiv = Assert.IsType<TicksPerQuarterNoteTimeDivision>(midi.TimeDivision);
        File.Delete(outPath);
        return tpqnDiv.TicksPerQuarterNote;
    }

    [Fact]
    public void Triplet_StaysAt480()
    {
        // {3:2 ...}q → denoms={3}, LCM(480, 6) = 480. No elevation.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    section verse {
                        | {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q |
                    }
                    Song song = [verse]
                    (writeMidi ""{{OUTPATH}}"" song)
                }
            }";
        int tpqn = RunAndReadTpqn(source, nameof(Triplet_StaysAt480));
        Assert.Equal(480, tpqn);
    }

    [Fact]
    public void Quintuplet_StaysAt480()
    {
        // {5:4 ...}q → denoms={5}, LCM(480, 10) = 480. No elevation.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    section verse {
                        | {5:4 C4 D4 E4 F4 G4}q B4q C5q D5q |
                    }
                    Song song = [verse]
                    (writeMidi ""{{OUTPATH}}"" song)
                }
            }";
        int tpqn = RunAndReadTpqn(source, nameof(Quintuplet_StaysAt480));
        Assert.Equal(480, tpqn);
    }

    [Fact]
    public void Septuplet_ElevatesTo3360()
    {
        // {7:8 ...}q → denoms={7}, LCM(480, 14) = 3360 = 480 × 7. Elevation.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    section verse {
                        | {7:8 C4 D4 E4 F4 G4 A4 B4}q B4q C5q D5q |
                    }
                    Song song = [verse]
                    (writeMidi ""{{OUTPATH}}"" song)
                }
            }";
        int tpqn = RunAndReadTpqn(source, nameof(Septuplet_ElevatesTo3360));
        Assert.Equal(3360, tpqn);
    }

    [Fact]
    public void LargeRatioCombination_RaisesCapError()
    {
        // {7:8 ...}q AND {11:13 ...}q in same song → denoms={7,11},
        // LCM(480, 14, 22) = LCM(3360, 22). gcd(3360,22)=2; LCM=3360 × 11 = 36960 > 9600 → cap error.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    section verse {
                        | {7:8 C4 D4 E4 F4 G4 A4 B4}q {11:13 C4 D4 E4 F4 G4 A4 B4 C5 D5 E5 F5}q |
                    }
                    Song song = [verse]
                    (writeMidi ""{{OUTPATH}}"" song)
                }
            }";

        string outPath = Path.Combine(Path.GetTempPath(), $"flow_phase19_capfail_{Guid.NewGuid():N}.mid");
        string substituted = source.Replace("{{OUTPATH}}", outPath.Replace("\\", "/"));

        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(substituted);

        // The cap error fires inside MidiExport.ComputeRequiredTpqn — should propagate
        // as either a runner failure (success=false) or surface the message in stderr.
        bool sawCapError =
            stderr.Contains("exceeds cap 9600") ||
            stderr.Contains("locked v1.3 D-05") ||
            stderr.Contains("Tuplet ratios in this song:");
        Assert.True(sawCapError,
            $"Expected cap-error message in stderr; got success={success}, errorCount={errorCount}, stderr={stderr}");

        // Confirm the file was NOT written (atomic per CONTEXT D-06 — no partial export)
        Assert.False(File.Exists(outPath), $"Cap error must not produce partial MIDI file at {outPath}");
    }

    [Fact]
    public void ZeroTuplets_StaysAt480()
    {
        // CONTEXT D-07: no tuplet syntax → denoms={} → return 480. Phase 18 byte-identical contract.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    section verse {
                        | C4q D4q E4q F4q |
                    }
                    Song song = [verse]
                    (writeMidi ""{{OUTPATH}}"" song)
                }
            }";
        int tpqn = RunAndReadTpqn(source, nameof(ZeroTuplets_StaysAt480));
        Assert.Equal(480, tpqn);
    }

    [Fact]
    public void PerNoteSeptuplet_ElevatesTo3360()
    {
        // TUP-08 parity: per-note /7:8 produces same DurationFraction.Denom=7 as bracket {7:8}.
        // Same TPQN math; same elevation to 3360.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    section verse {
                        | C4/7:8 D4/7:8 E4/7:8 F4/7:8 G4/7:8 A4/7:8 B4/7:8 B4q C5q D5q |
                    }
                    Song song = [verse]
                    (writeMidi ""{{OUTPATH}}"" song)
                }
            }";
        int tpqn = RunAndReadTpqn(source, nameof(PerNoteSeptuplet_ElevatesTo3360));
        Assert.Equal(3360, tpqn);
    }
}
