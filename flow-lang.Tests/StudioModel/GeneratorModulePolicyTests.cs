using System.Text.Json;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class GeneratorModulePolicyTests
{
    [Fact]
    public async Task OrdinaryDawBuildCannotExecuteAnArbitraryModuleButGeneralEngineCan()
    {
        string path = Path.Combine(Path.GetTempPath(), "flow-module-policy-" + Guid.NewGuid() + ".flow");
        File.WriteAllText(path, "use \"@core\"\nproc marker () 123 end proc\n");
        try
        {
            var context = new GenerationContext(0, 7, new([new(0, 120)]), new([new(1, 4, 4)]));
            var request = new GeneratorBuildRequest(new(1, Guid.NewGuid(), "generate"), 1,
                $"use {JsonSerializer.Serialize(path)}\n" + FlowDawGenerator.Template, context, TimeSpan.FromSeconds(10));
            var rejected = FlowDawGenerator.Build(request);
            Assert.Equal(JobStatus.Failed, rejected.Status);
            Assert.Contains("supported bundled DAW", rejected.Error);
#if !FLOW_WEB
            await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
            var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(JobStatus.Failed, isolated.Status);
            Assert.Contains("supported bundled DAW", isolated.Error);
#else
            await Task.CompletedTask;
#endif
            using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
            var ordinary = engine.Evaluate($"use {JsonSerializer.Serialize(path)}\n(marker)");
            Assert.True(ordinary.Succeeded, string.Join("\n", ordinary.Errors));
            Assert.Equal(123, ordinary.LastValue!.As<int>());
        }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData("../outside.flow")]
    [InlineData("@../outside")]
    [InlineData("@osc")]
    [InlineData("@sfz")]
    public void UnownedModuleNamesAreRejected(string module)
    {
        var request = new GeneratorBuildRequest(new(1, Guid.NewGuid(), "generate"), 1,
            $"use {JsonSerializer.Serialize(module)}\n" + FlowDawGenerator.Template,
            new(0, 7, new([new(0, 120)]), new([new(1, 4, 4)])), TimeSpan.FromSeconds(10));
        var result = FlowDawGenerator.Build(request);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Contains("supported bundled DAW", result.Error);
    }
}
