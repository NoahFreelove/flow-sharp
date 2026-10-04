using FlowLang.Core;
using FlowLang.Music;
using FlowLang.Runtime;
using FlowLang.TypeSystem;

namespace FlowLang.Hosting;

/// <summary>DAW style initialization never discovers or evaluates ambient style files.</summary>
internal static class GeneratorStyles
{
    internal static void Install(FlowLang.Runtime.ExecutionContext target, CancellationToken cancellation)
    {
        var allowed = new HashSet<FunctionSignature>();
        using var engine = new FlowEngine(new EngineOptions
        {
            Output = TextWriter.Null, Diagnostics = TextWriter.Null,
            AllowImplicitSampleFiles = false, NativeFunctionPolicy = signature => allowed.Contains(signature)
        });
        GeneratorNativePolicy.Populate(allowed, engine.Context);
        engine.ModuleLoader.SourceResolver = PluginModuleSources.ForGenerator();
        // Both engines have explicit initialization; native style calls must never
        // enter StyleRegistry's standalone-script filesystem discovery path.
        engine.Context.Music.StylePacksLoaded = true;
        foreach (string name in new[] { "blues", "classical", "jazz" })
        {
            cancellation.ThrowIfCancellationRequested();
            using var stream = typeof(FlowEngine).Assembly.GetManifestResourceStream($"FlowLang.Stdlib.improv/styles/{name}.flow")
                ?? throw new InvalidOperationException($"Missing bundled style {name}");
            using var reader = new StreamReader(stream);
            var result = engine.Evaluate(reader.ReadToEnd(), $"/__flow_styles__/{name}.flow",
                new EvaluationOptions { Cancellation = cancellation });
            cancellation.ThrowIfCancellationRequested();
            if (!result.Succeeded) throw new InvalidOperationException($"Bundled style {name} failed: {string.Join("; ", result.Errors)}");
        }
        foreach (var (name, pack) in engine.Context.StyleRegistry)
            target.StyleRegistry[Value.Symbol(name.As<string>(), target)] = pack;
        target.Music.StylePacksLoaded = true;
    }
}
