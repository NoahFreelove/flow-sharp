using FlowLang.Tests.Helpers;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Xunit;

namespace FlowLang.Tests.Integration.Phase47;

/// <summary>
/// Phase 47 Plan 47-04 / D-47-04 + (forward) D-48-04 — Smoke test verifying that
/// the DryWetMidi 8.0.3 APIs flow-lang uses (MidiFile.Write + MidiFile.Read on
/// SMF format-0/1 files with header chunk + tempo events + note events) are
/// reachable in BOTH FlowTarget=Desktop and FlowTarget=Web builds.
///
/// Per D-47-04 narrative: "MIDI file write via DryWetMidi 8.0.3 — pending
/// WASM compatibility verification at Plan 47-04". If THIS test fails under
/// FlowTarget=Web at compile OR runtime, Phase 48 must strip MIDI export from
/// the Web build (writeMidi becomes a parse error). Plan 47-06 closes the
/// question in VERIFICATION.md.
///
/// The Facts run under both targets per <c>[FlowTargetFact("Desktop", "Web")]</c>.
/// Status of the Web execution path is recorded in 47-04-SUMMARY.md and feeds
/// Plan 47-06's known-limitation table.
/// </summary>
public class DryWetMidiWasmCompatTests
{
    [FlowTargetFact("Desktop", "Web")]
    public void MidiFile_WriteAndRead_RoundTripsMinimalSmf()
    {
        // Build a minimal MIDI file: header + track + tempo + note on/off.
        // Exercises the smallest write/read surface flow-lang.MidiExport
        // depends on; if DryWetMidi works for this, it works for the
        // generated multi-track files in MidiExport.cs.
        var midi = new MidiFile(
            new TrackChunk(
                new SetTempoEvent(500_000), // 120 BPM
                new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100)
                {
                    Channel = (FourBitNumber)0
                },
                new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0)
                {
                    Channel = (FourBitNumber)0,
                    DeltaTime = 480
                }
            )
        );

        var tmpPath = Path.Combine(Path.GetTempPath(), $"phase47_smoke_{Guid.NewGuid():N}.mid");
        try
        {
            // Write
            midi.Write(tmpPath, format: MidiFileFormat.SingleTrack);
            Assert.True(File.Exists(tmpPath), "MidiFile.Write must produce the output file");
            var written = new FileInfo(tmpPath);
            Assert.True(written.Length > 0, "Written MIDI file must be non-empty");

            // Read back
            var loaded = MidiFile.Read(tmpPath);
            Assert.NotNull(loaded);
            Assert.NotEmpty(loaded.GetTrackChunks());
        }
        finally
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
        }
    }

    [FlowTargetFact("Desktop", "Web")]
    public void DryWetMidiAssembly_IsLoadable()
    {
        // Verifies the DryWetMidi assembly resolves in the current AppDomain.
        // If FlowTarget=Web strips or fails to link DryWetMidi (Plan 47-01
        // intentionally keeps it referenced — D-48-04 verifies WASM compat),
        // this Assert would throw a TypeLoadException at the typeof() call.
        Assert.NotNull(typeof(MidiFile).Assembly);
        Assert.Equal("Melanchall.DryWetMidi", typeof(MidiFile).Assembly.GetName().Name);
    }
}
