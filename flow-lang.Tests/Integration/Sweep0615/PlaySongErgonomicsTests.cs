using System;
using System.IO;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0615;

/// <summary>
/// play-song (#9) — owner-requested (play Song) + writeWav(String, Song) ergonomics.
///
/// Before this feature only play(Buffer) + play(Sequence) and writeWav(String, Buffer)
/// existed, so a composer had to write <c>(play (renderSong song "piano"))</c> by hand.
/// This wave adds:
///   - <c>(play Song)</c> — per-sequence instrument routing + mix
///   - <c>(play Song String)</c> — force one synth for the whole song
///   - <c>(writeWav String Song)</c> / <c>(writeWav String Song String)</c> exports
///
/// These tests verify the DOCUMENTED CALL FORMS produce non-silent audio, the
/// charitable default-synth advisory fires for unknown sequence names, and the
/// export path stays two-run cmp-clean (byte-identical).
/// </summary>
[Collection("FlowScripts")]
public class PlaySongErgonomicsTests : IDisposable
{
    public PlaySongErgonomicsTests()
    {
        RenderingDiagnostics.ResetForTesting();
        // CaptureMode is auto-enabled in this assembly (FLOW_SUPPRESS_PLAYBACK=1
        // via TestAssemblyInit) so (play ...) routes the mix into the capture
        // buffer instead of PulseAudio.
    }

    public void Dispose() => RenderingDiagnostics.ResetForTesting();

    private const string SongScript = """
        use "@audio"
        tempo 120 {
            section verse {
                Sequence piano = | C4 E4 G4 C5 |
                Sequence bass  = | C3 G3 C3 G3 |
                Sequence drums = | C2 C2 C2 C2 |
            }
        }
        Song mySong = [verse verse]
        """;

    private static bool IsNonSilent(AudioBuffer? buf)
    {
        if (buf is null || buf.Frames == 0) return false;
        return buf.Data.Any(s => Math.Abs(s) > 0.001f);
    }

    [Fact]
    public void PlaySong_AutoRoute_CapturesNonSilentBuffer()
    {
        using var engine = new FlowEngine();
        Assert.True(engine.AudioManager.CaptureMode, "CaptureMode should be auto-enabled in tests");

        bool ok = engine.Execute(SongScript + "\n(play mySong)\n", "<play-song-auto>");
        Assert.True(ok, "script must execute without error");

        var captured = engine.AudioManager.GetCapturedBuffer();
        Assert.True(IsNonSilent(captured), "(play song) must produce non-silent audio");
        // Per-sequence routing mixes a stereo result (SongRenderer stereo path).
        Assert.Equal(2, captured!.Channels);
    }

    [Fact]
    public void PlaySong_ForcedSynth_CapturesNonSilentBuffer()
    {
        using var engine = new FlowEngine();

        bool ok = engine.Execute(SongScript + "\n(play mySong \"sine\")\n", "<play-song-sine>");
        Assert.True(ok, "script must execute without error");

        var captured = engine.AudioManager.GetCapturedBuffer();
        Assert.True(IsNonSilent(captured), "(play song \"sine\") must produce non-silent audio");
    }

    [Fact]
    public void WriteWavSong_AutoRoute_WritesNonSilentFile()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "flow_playsong_auto_" + Guid.NewGuid() + ".wav");
        try
        {
            using var engine = new FlowEngine();
            bool ok = engine.Execute(SongScript + $"\n(writeWav \"{tmp}\" mySong)\n", "<writewav-song>");
            Assert.True(ok, "script must execute without error");

            Assert.True(File.Exists(tmp), "writeWav(String, Song) must create the file");
            Assert.True(WavIsNonSilent(tmp), "exported WAV must be non-silent");
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void WriteWavSong_ForcedSynth_WritesNonSilentFile()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "flow_playsong_sine_" + Guid.NewGuid() + ".wav");
        try
        {
            using var engine = new FlowEngine();
            bool ok = engine.Execute(SongScript + $"\n(writeWav \"{tmp}\" mySong \"sine\")\n", "<writewav-song-sine>");
            Assert.True(ok, "script must execute without error");

            Assert.True(File.Exists(tmp), "writeWav(String, Song, String) must create the file");
            Assert.True(WavIsNonSilent(tmp), "exported WAV must be non-silent");
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void WriteWavSong_TwoRuns_ByteIdentical()
    {
        var a = Path.Combine(Path.GetTempPath(), "flow_playsong_det_a_" + Guid.NewGuid() + ".wav");
        var b = Path.Combine(Path.GetTempPath(), "flow_playsong_det_b_" + Guid.NewGuid() + ".wav");
        try
        {
            using (var e1 = new FlowEngine())
                Assert.True(e1.Execute(SongScript + $"\n(writeWav \"{a}\" mySong)\n", "<det-a>"));
            using (var e2 = new FlowEngine())
                Assert.True(e2.Execute(SongScript + $"\n(writeWav \"{b}\" mySong)\n", "<det-b>"));

            var bytesA = File.ReadAllBytes(a);
            var bytesB = File.ReadAllBytes(b);
            Assert.Equal(bytesA, bytesB);
        }
        finally
        {
            try { File.Delete(a); } catch { }
            try { File.Delete(b); } catch { }
        }
    }

    [Theory]
    [InlineData("piano", "piano")]
    [InlineData("pianoLead", "piano")]
    [InlineData("drums", "drums")]
    [InlineData("drumKit", "drums")]
    [InlineData("brass", "brass")]
    [InlineData("horn", "brass")]
    [InlineData("sax", "sax")]
    [InlineData("flute", "flute")]
    [InlineData("strings", "strings")]
    [InlineData("string", "strings")]
    [InlineData("organ", "organ")]
    [InlineData("bell", "bell")]
    [InlineData("sine", "sine")]
    [InlineData("saw", "saw")]
    [InlineData("square", "square")]
    [InlineData("triangle", "triangle")]
    [InlineData("sampler:piano", "piano")]
    public void ResolveSynthForSequenceName_MapsKnownNames(string sequenceName, string expectedSynth)
    {
        Assert.Equal(expectedSynth, SongRenderer.ResolveSynthForSequenceName(sequenceName));
    }

    [Theory]
    [InlineData("bass")]
    [InlineData("violin")]
    [InlineData("guitar")]
    [InlineData("lead")]
    [InlineData("pad")]
    public void ResolveSynthForSequenceName_UnknownFallsBackToPiano_AndWarns(string sequenceName)
    {
        RenderingDiagnostics.ResetForTesting();
        var result = SongRenderer.ResolveSynthForSequenceName(sequenceName);
        Assert.Equal("piano", result);
        Assert.True(
            RenderingDiagnostics.WasWarnedForTesting($"play-song:default-synth:{sequenceName.ToLowerInvariant()}"),
            "unknown sequence names must emit the one-shot default-synth advisory");
    }

    /// <summary>
    /// Minimal 16-bit PCM WAV non-silence probe — reads the data chunk and looks
    /// for any sample whose magnitude clears a small threshold.
    /// </summary>
    private static bool WavIsNonSilent(string path)
    {
        var bytes = File.ReadAllBytes(path);
        // Find "data" chunk.
        int dataIdx = -1;
        for (int i = 12; i + 8 <= bytes.Length; )
        {
            string id = System.Text.Encoding.ASCII.GetString(bytes, i, 4);
            int size = BitConverter.ToInt32(bytes, i + 4);
            if (id == "data") { dataIdx = i + 8; break; }
            i += 8 + size + (size & 1);
        }
        if (dataIdx < 0) return false;

        for (int i = dataIdx; i + 1 < bytes.Length; i += 2)
        {
            short sample = BitConverter.ToInt16(bytes, i);
            if (Math.Abs((int)sample) > 64) return true; // ~0.002 of full scale
        }
        return false;
    }
}
