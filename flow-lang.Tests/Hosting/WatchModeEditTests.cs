using FlowInterpreter;
using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Hosting;

/// <summary>
/// Phase 2 gate, in watch mode itself: only the newest successful render is staged
/// for playback; a broken or runaway edit leaves the previous version in place, and
/// a runaway render is stopped when superseded rather than left running.
/// </summary>
[Collection("FlowScripts")]
public class WatchModeEditTests
{
    private const string Good = "use \"@std\"\nuse \"@audio\"\n(createSineTone 440Hz 0.1 0.5)";

    private sealed class Harness : LiveReloadManager
    {
        public Harness(string path) : base(path) { }

        public int Staged;

        public Task Render() => RenderEditForTesting();

        protected override void StagePendingBuffers(
            Dictionary<int, LiveBlockBuffer> newBuffers, FlowEngine engine,
            IReadOnlyDictionary<int, LiveBlockRegistration> newBlocks)
        {
            Interlocked.Increment(ref Staged);
            base.StagePendingBuffers(newBuffers, engine, newBlocks);
        }
    }

    [Fact]
    public async Task FailedEditKeepsThePreviousVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"watch-{Guid.NewGuid():N}.flow");
        try
        {
            using var watch = new Harness(path);

            File.WriteAllText(path, Good);
            await watch.Render().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(1, watch.Staged);

            File.WriteAllText(path, "use \"@std\"\n(undefinedThing 1)");
            await watch.Render().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(1, watch.Staged);   // nothing replaced the good version
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NewerEditCancelsARunawayRender()
    {
        var path = Path.Combine(Path.GetTempPath(), $"watch-{Guid.NewGuid():N}.flow");
        try
        {
            using var watch = new Harness(path);
            File.WriteAllText(path, "use \"@std\"\n(setMaxIterations 1000000)\nwhile true {\n  for Int i in (range 0 1000) { (Nothing) }\n}");
            var runaway = watch.Render();
            await Task.Delay(500);

            File.WriteAllText(path, Good);
            var fixedEdit = watch.Render();
            await Task.WhenAll(runaway, fixedEdit).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(1, watch.Staged);   // only the newest (good) render was staged
        }
        finally
        {
            File.Delete(path);
        }
    }
}
