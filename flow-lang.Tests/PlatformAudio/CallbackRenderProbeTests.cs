using System.Runtime.InteropServices;
using Flow.Audio;
using Flow.Music.Model;
using Flow.Platform.Linux;
using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.PlatformAudio;

public class CallbackRenderProbeTests
{
    private static PreparedSinePlayback Source()
    {
        var section = new SectionSnapshot(Guid.NewGuid(), "tone", new(Bpm: 120),
            [new(Guid.NewGuid(), "melody", 4,
                [new(Guid.NewGuid(), "voice", 0, 4, new('A', 4, 0, null, 69, 440))])]);
        return PreparedSinePlayback.Prepare(new(Guid.NewGuid(), [new(Guid.NewGuid(), section)]), new(48000, 128));
    }

    [Fact]
    public void PlatformArtifactDoesNotReferenceLanguageOrMusicHost()
    {
        using var module = ModuleDefinition.ReadModule(typeof(PortAudioOutput).Assembly.Location);
        Assert.All(module.AssemblyReferences, r => Assert.True(r.Name == "Flow.Audio" || r.Name == "netstandard" || r.Name.StartsWith("System", StringComparison.Ordinal), r.Name));
        Assert.DoesNotContain(module.GetTypeReferences(), t => t.Namespace.StartsWith("FlowLang", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCallbackFillsStereoAndRecordsUnderflowWithoutChangingRender(bool mute)
    {
        var queued = new QueuedSinePlayback(Source());
        var probe = new CallbackRenderProbe(queued, 8, mute);
        queued.TryPlay();
        nint memory = Marshal.AllocHGlobal(256 * sizeof(float));
        try
        {
            Assert.Equal(0, probe.ProcessNative(memory, 128, 4));
            var actual = new float[256];
            Marshal.Copy(memory, actual, 0, actual.Length);
            var expected = new float[256];
            if (!mute) Source().Read(expected);
            Assert.Equal(expected, actual);
            Assert.Equal(128, queued.PositionFrames);
            var sample = Assert.Single(probe.Capture().Samples);
            Assert.True(sample.OutputUnderflow);
            Assert.Null(sample.NativeTiming);
            Assert.Equal(4UL, sample.StatusFlags);
            Assert.Equal(128, sample.Frames);
            Assert.True(sample.ElapsedTicks >= 0);
            Assert.False(probe.Faulted);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    [Fact]
    public void NativeTimesAreCopiedBeforeReturnAndNonfiniteValuesAreMissing()
    {
        var probe = new CallbackRenderProbe(new QueuedSinePlayback(Source()), 8);
        nint output = Marshal.AllocHGlobal(256 * sizeof(float));
        nint times = Marshal.AllocHGlobal(3 * sizeof(double));
        try
        {
            Marshal.Copy(new double[] { 7, 33, 33.025 }, 0, times, 3);
            Assert.Equal(0, probe.ProcessNative(output, 128, 20, times));
            Marshal.Copy(new double[] { 9, 44, 44.015 }, 0, times, 3);
            Assert.Equal(0, probe.ProcessNative(output, 128, 0, times));
            Marshal.Copy(new double[] { 0, double.NaN, 2 }, 0, times, 3);
            Assert.Equal(0, probe.ProcessNative(output, 128, 0, times));
            Marshal.Copy(new double[] { 0, 1, double.PositiveInfinity }, 0, times, 3);
            Assert.Equal(0, probe.ProcessNative(output, 128, 0, times));
            var samples = probe.Capture().Samples;
            Assert.Equal(new NativeCallbackTiming(33, 33.025), samples[0].NativeTiming);
            Assert.Equal(new NativeCallbackTiming(44, 44.015), samples[1].NativeTiming);
            Assert.Equal(20UL, samples[0].StatusFlags);
            Assert.True(samples[0].OutputUnderflow);
            Assert.Null(samples[2].NativeTiming);
            Assert.Null(samples[3].NativeTiming);
        }
        finally { Marshal.FreeHGlobal(times); Marshal.FreeHGlobal(output); }
    }

    [Fact]
    public void NativeBoundarySilencesFaultAndReturnsAbortInsteadOfThrowing()
    {
        var queued = new QueuedSinePlayback(Source());
        var probe = new CallbackRenderProbe(queued);
        queued.TryPlay();
        var initial = Enumerable.Repeat(42f, 258).ToArray();
        nint memory = Marshal.AllocHGlobal(initial.Length * sizeof(float));
        try
        {
            Marshal.Copy(initial, 0, memory, initial.Length);
            Assert.Equal(2, probe.ProcessNative(memory, 129, 0));
            Marshal.Copy(memory, initial, 0, initial.Length);
            Assert.All(initial, sample => Assert.Equal(0, sample));
            Assert.True(probe.Faulted);
            Assert.Equal(0, queued.PositionFrames);
            Assert.Empty(probe.Capture().Samples);
            Assert.Equal(2, probe.ProcessNative(0, 128, 0));
            Assert.Equal(2, probe.ProcessNative(memory, (nuint)int.MaxValue, 0));
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    [Fact]
    public void TimingStorageIsBoundedAndReportsDroppedSamples()
    {
        var queued = new QueuedSinePlayback(Source());
        var probe = new CallbackRenderProbe(queued, 2);
        var block = new float[256];
        for (int i = 0; i < 5; i++) probe.Process(block);
        var capture = probe.Capture();
        Assert.Equal(2, capture.Samples.Length);
        Assert.Equal(3, capture.Dropped);
        Assert.Equal(0, queued.PositionFrames);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CallbackRenderProbe(queued, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CallbackRenderProbe(queued, 2_000_001));
        Assert.Throws<ArgumentNullException>(() => new CallbackRenderProbe(null!));
    }

    [Fact]
    public void InstrumentedNativeCallbackAllocatesNothingAfterWarmup()
    {
        var queued = new QueuedSinePlayback(Source());
        queued.TrySetLoop(0, queued.TotalFrames);
        queued.TryPlay();
        var probe = new CallbackRenderProbe(queued, 400);
        nint memory = Marshal.AllocHGlobal(256 * sizeof(float));
        nint times = Marshal.AllocHGlobal(3 * sizeof(double));
        Marshal.Copy(new double[] { 0, 1, 1.025 }, 0, times, 3);
        try
        {
            for (int i = 0; i < 100; i++) probe.ProcessNative(memory, 128, 0, times);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) probe.ProcessNative(memory, 128, 0, times);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
            Assert.All(probe.Capture().Samples.Skip(100), s => Assert.Equal(0, s.AllocatedBytes));
            Assert.False(probe.Faulted);
        }
        finally { Marshal.FreeHGlobal(times); Marshal.FreeHGlobal(memory); }
    }

    [Fact]
    public void DiagnosticStallIsOnceOnlyMutedAndDoesNotInventUnderflowFlags()
    {
        var probe = new CallbackRenderProbe(new QueuedSinePlayback(Source()), 4,
            stallAfterCallbacks: 1, stallMilliseconds: 1);
        var output = new float[256];
        for (int i = 0; i < 4; i++) probe.Process(output);
        var samples = probe.Capture().Samples;
        Assert.Equal(1, probe.InjectedStalls);
        Assert.Equal(new[] { false, true, false, false }, samples.Select(s => s.InjectedStall));
        Assert.All(samples, s => { Assert.False(s.OutputUnderflow); Assert.Equal(0UL, s.StatusFlags); });
        Assert.All(output, s => Assert.Equal(0f, s));
    }

    [Theory]
    [InlineData(false, 1, 1)]
    [InlineData(true, -2, 1)]
    [InlineData(true, -1, 1)]
    [InlineData(true, 1, 0)]
    [InlineData(true, 1, 1001)]
    public void DiagnosticStallRejectsUnsafeOrInconsistentConfiguration(bool mute, int index, int milliseconds)
    {
        Assert.Throws<ArgumentException>(() => new CallbackRenderProbe(new QueuedSinePlayback(Source()),
            4, mute, index, milliseconds));
    }

}
