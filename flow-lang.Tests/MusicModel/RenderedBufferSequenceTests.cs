using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class RenderedBufferSequenceTests(ITestOutputHelper output)
{
    [Fact]
    public void AssemblyMatchesLegacyAppendIncludingRepeatsEmptyAndSignedZero()
    {
        var first = new AudioBuffer(3, 2, 44100);
        new float[] { 0, -0.0f, -1, 0.25f, float.Epsilon, 1 }.CopyTo(first.Data, 0);
        var second = new AudioBuffer(1, 2, 44100);
        second.Data[0] = -0.3f;
        second.Data[1] = 0.8f;
        var parts = new RenderedBufferSequence(44100, 2);
        parts.Add(first, 3);
        parts.Add(new AudioBuffer(0, 2, 44100), 10);
        parts.Add(second, -1);
        parts.Add(second, 2);
        var expected = first.Data.Concat(first.Data).Concat(first.Data).Concat(second.Data).Concat(second.Data);
        Assert.Equal(expected.Select(BitConverter.SingleToInt32Bits), parts.Build().Data.Select(BitConverter.SingleToInt32Bits));
    }

    [Fact]
    public void CopyingObservesCancellationBetweenBoundedChunks()
    {
        var parts = new RenderedBufferSequence(44100, 2);
        parts.Add(new AudioBuffer(100_000, 2, 44100), 2);
        int checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() => parts.Build(() =>
        {
            if (++checkpoints == 3) throw new OperationCanceledException();
        }));
        Assert.Equal(3, checkpoints);
    }

    [Fact]
    public void OutputOwnsItsSamplesAndRejectsUnrepresentableSizes()
    {
        var source = new AudioBuffer(1, 2, 44100);
        source.Data[0] = 0.5f;
        var parts = new RenderedBufferSequence(44100, 2);
        parts.Add(source, 1);
        var result = parts.Build();
        source.Data[0] = 0;
        Assert.Equal(0.5f, result.Data[0]);
        Assert.Throws<InvalidOperationException>(() => parts.Add(source, int.MaxValue));
        Assert.Throws<ArgumentException>(() => parts.Add(new AudioBuffer(1, 1, 44100), 1));
    }

    [Fact]
    public void LongRepeatedAssemblyAllocatesLinearlyRatherThanCopyingEveryPrefix()
    {
        var section = new AudioBuffer(2048, 2, 44100);
        const int repeats = 128;
        static AudioBuffer Legacy(AudioBuffer section, int repeats)
        {
            var result = new AudioBuffer(0, 2, 44100);
            for (int repeat = 0; repeat < repeats; repeat++)
            {
                if (result.Frames == 0) { result = section; continue; }
                var next = new AudioBuffer(result.Frames + section.Frames, 2, 44100);
                Array.Copy(result.Data, next.Data, result.Data.Length);
                Array.Copy(section.Data, 0, next.Data, result.Data.Length, section.Data.Length);
                result = next;
            }
            return result;
        }
        var start = GC.GetAllocatedBytesForCurrentThread();
        var old = Legacy(section, repeats);
        var legacyBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        var parts = new RenderedBufferSequence(44100, 2);
        parts.Add(section, repeats);
        var current = parts.Build();
        var newBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(old.Data, current.Data);
        Assert.True(newBytes < legacyBytes / 8, $"legacy={legacyBytes}, new={newBytes}");
        output.WriteLine($"128 repeats, 2048 stereo frames: legacy allocated {legacyBytes} bytes; linear assembly {newBytes} bytes.");
    }
}
