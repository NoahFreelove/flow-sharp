using Flow.Audio;
using Flow.Music.Model;
using FlowLang.Core;
using FlowLang.Music;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Syntax;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.MusicModel;

[Collection("FlowScripts")]
public class SnapshotRenderingTests
{
    private static CompositionSnapshot Native(int repeats = 1, SectionSettings? settings = null) => new(Guid.NewGuid(),
        [new(Guid.NewGuid(), new(Guid.NewGuid(), "verse", settings ?? new(),
            [new(Guid.NewGuid(), "melody", 4,
                [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))])]), repeats)]);

    private static float[] Render(CompositionSnapshot score, int blockFrames = 1024)
    {
        var samples = new List<float>();
        SineCompositionRenderer.Render(score, block => samples.AddRange(block.ToArray()), new(BlockFrames: blockFrames));
        return samples.ToArray();
    }

    [Fact]
    public void AudioArtifactDependsOnlyOnModelAndBcl()
    {
        using var module = ModuleDefinition.ReadModule(typeof(SineCompositionRenderer).Assembly.Location);
        Assert.Equal("Flow.Audio", module.Assembly.Name.Name);
        Assert.All(module.AssemblyReferences, r => Assert.True(
            r.Name == "Flow.Music.Model" || r.Name == "netstandard" || r.Name.StartsWith("System", StringComparison.Ordinal), r.Name));
        Assert.DoesNotContain(module.GetTypeReferences(), t => t.Namespace.StartsWith("FlowLang", StringComparison.Ordinal));
        Assert.Equal(MusicalContext.SustainTailSeconds, NoteDuration.SustainTailSeconds);
    }

    [Theory]
    [InlineData(false, 32, false)]
    [InlineData(true, 32, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, 1, true)]
    public void SnapshotSineMatchesLegacyBitsForMusicalTiming(bool pedal, int pool, bool justIntonation)
    {
        var held = new MusicalNoteData('C', 4, 0, null, false, isTied: true,
            articulation: Articulation.Legato, durationFraction: new Fraction(2, 3),
            onsetOffset: -0.1, durationOverlap: 0.5, portamentoMs: 80);
        var rest = new MusicalNoteData('C', 4, 0, null, true, durationFraction: new Fraction(1, 3));
        var hit = new MusicalNoteData('E', 4, 0, 2, false, centOffset: 12,
            articulation: Articulation.Staccato, onsetOffset: 0.03);
        var voice = new BarData(new[] { held, rest, hit }, new(6, 8));
        var parallel = new BarData(Array.Empty<MusicalNoteData>(), new(6, 8))
            { ParallelVoices = new() { voice, new(new[] { hit, held, rest }, new(6, 8)) } };
        var sequence = new SequenceData(); sequence.AddBar(parallel); sequence.AddBar(voice);
        var context = new MusicalContext { Tempo = 137, Pan = -0.3, Gain = 0.7, SustainPedal = pedal, VoicePoolSize = pool, Key = "Cmajor" };
        if (justIntonation) context.TuningStack.Push(new(TuningSystem.JustIntonation, Mode.Major, 'C', 0));
        var silent = new SequenceData(); silent.AddBar(new(new[] { new MusicalNoteData('C', 4, 0, 0, true) }, new(4, 4)));
        var sections = new Dictionary<string, SectionData>
        {
            ["verse"] = new("verse", new() { ["melody"] = sequence, ["counter"] = sequence }, context),
            ["rest"] = new("rest", new() { ["melody"] = silent }, new MusicalContext { Tempo = 83 }),
        };
        var song = new SongData(new() { new("verse", 2), new("rest", 1), new("verse", 1) }, sections);
        var expected = SongRenderer.RenderSong([MusicValue.Song(song), Value.String("sine")]).As<AudioBuffer>();
        var snapshot = CompositionCompiler.Compile(song, "parity");
        var actual = Render(snapshot, 997);
        Assert.Equal(expected.Data.Length, actual.Length);
        Assert.Equal(expected.Data.Select(BitConverter.SingleToInt32Bits), actual.Select(BitConverter.SingleToInt32Bits));
        var playback = PreparedSinePlayback.Prepare(snapshot, new(BlockFrames: 997));
        var pulled = new List<float>();
        var block = new float[1994];
        while (playback.PositionFrames < playback.TotalFrames)
        {
            int count = playback.Read(block);
            pulled.AddRange(block.AsSpan(0, count * 2).ToArray());
        }
        Assert.Equal(expected.Data.Select(BitConverter.SingleToInt32Bits), pulled.Select(BitConverter.SingleToInt32Bits));
    }

    [Fact]
    public void FlowAndNativeScoresRenderIdenticallyAcrossBlockSizes()
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate("use \"@std\"; section verse { Sequence melody = | A4q | } Song song = [verse]; song");
        Assert.True(result.Succeeded, engine.ErrorReporter.FormatErrors());
        var snapshot = CompositionCompiler.Compile(result.LastValue!.As<SongData>(), "source");
        var native = Native();
        Assert.Equal(Render(native, 1), Render(snapshot, 997));
        Assert.Equal(Render(native, 65536), Render(snapshot, 127));
    }

    [Fact]
    public void StreamingHasBoundedBlocksAccurateProgressAndPromptCancellation()
    {
        var score = Native(100000);
        using var cancellation = new CancellationTokenSource();
        int blocks = 0;
        var updates = new List<RenderProgress>();
        Assert.Throws<OperationCanceledException>(() => SineCompositionRenderer.Render(score, block =>
        {
            Assert.InRange(block.Length, 1, 514);
            if (++blocks == 3) cancellation.Cancel();
        }, new(BlockFrames: 257), cancellation.Token, updates.Add));
        Assert.Equal(3, blocks);
        Assert.Equal(new RenderProgress(771, 8_820_000_000L), updates[^1]);
        Assert.True(updates.Zip(updates.Skip(1)).All(p => p.First.CompletedFrames < p.Second.CompletedFrames));
        Assert.Throws<OperationCanceledException>(() => SineCompositionRenderer.Render(score,
            _ => Assert.Fail("Cancelled jobs must not publish a block"), cancellation: cancellation.Token));
    }

    [Fact]
    public void UnsupportedSettingsAndBudgetsFailBeforeOutputAndSinkErrorsPropagate()
    {
        int calls = 0;
        Assert.Throws<NotSupportedException>(() => SineCompositionRenderer.Render(Native(settings: new(ReverbSeconds: 1)), _ => calls++));
        Assert.Throws<ArgumentOutOfRangeException>(() => SineCompositionRenderer.Render(Native(), _ => calls++, new(BlockFrames: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SineCompositionRenderer.Render(Native(settings: new(VoicePoolSize: 0)), _ => calls++));
        var two = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), new(Guid.NewGuid(), "two", new(),
            [Native().Placements[0].Section.Sequences[0], Native().Placements[0].Section.Sequences[0]]))]);
        Assert.Throws<InvalidOperationException>(() => SineCompositionRenderer.Render(two, _ => calls++, new(MaxNotesPerSection: 1)));
        Assert.Equal(0, calls);
        var expected = new IOException("sink failed");
        Assert.Same(expected, Assert.Throws<IOException>(() => SineCompositionRenderer.Render(Native(), _ => throw expected)));
        var progress = new List<RenderProgress>();
        SineCompositionRenderer.Render(Native(0), _ => Assert.Fail("Zero repeats produce no output"), progress: progress.Add);
        Assert.Equal(new RenderProgress(0, 0), Assert.Single(progress));
    }

    [Fact]
    public void RepeatsDoNotAllocateStorageProportionalToOutputLength()
    {
        var shortScore = Native(); var longScore = Native(256);
        var options = new RenderOptions(SampleRate: 100, BlockFrames: 127);
        static void Discard(ReadOnlyMemory<float> block) { }
        SineCompositionRenderer.Render(shortScore, Discard, options); // warm shared callbacks/JIT
        long before = GC.GetAllocatedBytesForCurrentThread();
        SineCompositionRenderer.Render(shortScore, Discard, options);
        long shortBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        SineCompositionRenderer.Render(longScore, Discard, options);
        long longBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(longBytes <= shortBytes + 1024, $"one repeat={shortBytes}; 256 repeats={longBytes}");
    }

    [Fact]
    public async Task ConcurrentNativeJobsHaveNoSharedRenderState()
    {
        var left = Native(settings: new(Bpm: 90, Pan: -1));
        var right = Native(settings: new(Bpm: 150, Pan: 1, Gain: 0.4));
        var expectedLeft = Render(left); var expectedRight = Render(right);
        var results = await Task.WhenAll(Task.Run(() => Render(left)), Task.Run(() => Render(right)));
        Assert.Equal(expectedLeft, results[0]); Assert.Equal(expectedRight, results[1]);
    }
}
