using System.IO;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Shared;
using Xunit;

namespace FlowLang.Tests.Integration.Phase14;

/// <summary>
/// DX-08 regression: verifies that `dynamics` / `crescendo` / `decrescendo` / `swell`
/// write per-note velocity through to MIDI output bytes (1..127 range). The chain is
/// Interpreter → MusicalContext → NoteStreamCompiler → MidiExport; this Fact pins the
/// observable byte sequence at the output end (Phase 13 D-11 observable-value pin).
///
/// Pass 1 (CONTEXT D-13 two-pass strict): drafted from REQUIREMENTS.md DX-08 wording
/// alone. Pass 2 reconciles against real code. If Pass 2 RED, minimal gap-fix bundles
/// into the same plan with a Divergence entry in 14-03-SUMMARY.md.
/// </summary>
[Collection("FlowScripts")]
public class DynamicsMidiVelocityTests
{
    [Fact]
    public void Crescendo_EmitsExpectedVelocityGradient()
    {
        using var runner = new FlowEngineRunner();

        // FindTestsRoot returns <repo>/tests; its parent is the repo root.
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));

        string testScript = Path.Combine(repoRoot, "tests", "test_dynamics_midi_velocity.flow");
        string outputMidi = Path.Combine(repoRoot, "tests", "output", "dynamics_velocity.mid");

        // Ensure clean slate — avoid passing a stale file through subsequent runs.
        if (File.Exists(outputMidi)) File.Delete(outputMidi);

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            // writeMidi resolves its path argument against CWD. Set to repo root so the
            // .flow script's relative path "tests/output/dynamics_velocity.mid" writes
            // where this Fact expects to read from.
            Environment.CurrentDirectory = repoRoot;

            // Safety net: pre-create the output dir in case auto-mkdir doesn't cover writeMidi.
            Directory.CreateDirectory(Path.Combine(repoRoot, "tests", "output"));

            var (success, stdout, stderr, errorCount) = runner.RunFile(testScript);

            Assert.True(success, $"Script failed: stderr={stderr}");
            Assert.Equal(0, errorCount);
            Assert.True(File.Exists(outputMidi), $"MIDI file not written: {outputMidi}");

            byte[] velocities = MidiReadHelpers.GetVelocityBytes(outputMidi);

            // Expected: crescendo(0.25, 0.75) linear over 5 notes → bytes at MIDI emit.
            //   v_i = 0.25 + i * 0.125; byte_i = (int)(v_i * 127)
            //   31, 47, 63, 79, 95
            // Pin from RESEARCH §"Expected velocity bytes for verification test" lines 519-531.
            Assert.Equal(new byte[] { 31, 47, 63, 79, 95 }, velocities);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
