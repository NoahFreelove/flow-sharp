using Flow.Audio;
using Flow.Music.Model;
using System.Text;
using System.Text.Json;

// Native host proof: score -> bounded PCM blocks -> PCM16 WAV, without a Flow engine.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: MusicHost OUTPUT.wav");
    return 2;
}
var sequence = new SequenceSnapshot(Guid.NewGuid(), "melody", 4,
[
    new(Guid.NewGuid(), "bar/voice", 0, 1, new('A', 4, 0, null, 69, 440)),
    new(Guid.NewGuid(), "bar/voice", 1, 1, new('C', 5, 0, null, 72, 523.2511306011972)),
    new(Guid.NewGuid(), "bar/voice", 2, 2, new('E', 5, 0, null, 76, 659.2551138257398)),
]);
var first = new SectionSnapshot(Guid.NewGuid(), "verse", new(Bpm: 120, Pan: -0.2), [sequence]);
var slower = new SectionSnapshot(Guid.NewGuid(), "outro", new(Bpm: 90, Pan: 0.2), [sequence]);
var composition = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), first, 2), new(Guid.NewGuid(), slower)]);
var options = new RenderOptions();
long frames = SineCompositionRenderer.GetFrameCount(composition, options);
long dataBytes = checked(frames * 4); // stereo PCM16
if (dataBytes > uint.MaxValue - 36) throw new InvalidOperationException("Example WAV writer supports RIFF files smaller than 4 GiB");
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    // Caller owns the file. Cancellation/failure can leave partial output.
    using var stream = File.Create(args[0]);
    using var writer = new BinaryWriter(stream, Encoding.ASCII);
    writer.Write("RIFF"u8); writer.Write((uint)dataBytes + 36); writer.Write("WAVEfmt "u8);
    writer.Write(16); writer.Write((short)1); writer.Write((short)2);
    writer.Write(options.SampleRate); writer.Write(options.SampleRate * 4);
    writer.Write((short)4); writer.Write((short)16); writer.Write("data"u8); writer.Write((uint)dataBytes);
    long delivered = 0;
    SineCompositionRenderer.Render(composition, block =>
    {
        // Deliberately simple PCM16 example: saturation, no dither. Legacy WAV APIs are unchanged.
        foreach (float sample in block.Span)
            writer.Write((short)Math.Round(Math.Clamp(sample, -1f, 1f) * 32767));
        delivered += block.Length / 2;
    }, options, cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(new { frames = delivered, sampleRate = options.SampleRate,
        channels = 2, output = Path.GetFullPath(args[0]),
        assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name)
            .Where(n => n is not null && !n.StartsWith("System") && n != "netstandard").Order().ToArray() }));
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Render cancelled; output may be partial.");
    return 130;
}
