using System.Diagnostics;
using Flow.Audio;
using Flow.Audio.Graph;
using FlowLang.StandardLibrary.Daw;
using Flow.Studio.Model;
using FlowLang.Core;
using FlowLang.Music;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Hosting;

public sealed record GeneratorBuildRequest(GeneratorDescriptor Descriptor, long SourceRevision,
    string Source, GenerationContext Context, TimeSpan TimeLimit, PluginPackage? Plugin = null, PluginAudioInput? AudioInput = null, PluginNoteInput? NoteInput = null,
    GeneratorAssets? Assets = null);

/// <summary>Worker-only authoring adapter. Uses a fresh engine and one shared time
/// budget for initialization, invocation and score detachment. Cooperative execution
/// is not a sandbox or a hard memory/process limit. Never call on the audio thread.</summary>
public static class FlowDawGenerator
{
    public const string Template = """
        use "@flowDaw"
        proc generate (Dict<String, Double>: context)
            section phrase { Sequence melody = | A4q C5q E5h | }
            Song score = [phrase]
            (dawResult "main" score)
        end proc
        """;

    public static JobResult<GeneratedSourceOutput> Build(GeneratorBuildRequest request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Descriptor);
        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentOutOfRangeException.ThrowIfNegative(request.SourceRevision);
        if (request.Source.Length > 2 * 1024 * 1024 || request.TimeLimit <= TimeSpan.Zero || request.TimeLimit > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(request));
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(request.TimeLimit);
        var elapsed = Stopwatch.StartNew();
        try
        {
            budget.Token.ThrowIfCancellationRequested();
            bool offlineAudio = request.Plugin?.Manifest.Kind == FlowPluginKind.OfflineAudio;
            bool noteTransform = request.Plugin?.Manifest.Kind == FlowPluginKind.NoteTransform;
            if (offlineAudio != (request.AudioInput is not null) || noteTransform != (request.NoteInput is not null))
                throw new ArgumentException("Processor input must match the declared plugin kind");
            if (offlineAudio)
            {
                var manifest = request.Plugin!.Manifest;
                if (manifest.AudioInputs != request.AudioInput!.Buses.Count) throw new ArgumentException("Offline input bus count differs from manifest");
                manifest.ValidateProcessing(request.AudioInput.SampleRate, 256);
            }
            if (offlineAudio || noteTransform)
                foreach (var parameter in request.Plugin!.Manifest.Parameters)
                    _ = parameter.ToNormalized(request.Context.Parameters.GetValueOrDefault(parameter.Id, parameter.Default));
            var allowed = new HashSet<FunctionSignature>();
            using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null,
                AllowImplicitSampleFiles = false, NativeFunctionPolicy = signature => allowed.Contains(signature) });
            if (request.Plugin is null)
            {
                GeneratorNativePolicy.Populate(allowed, engine.Context);
                GeneratorStyles.Install(engine.Context, budget.Token);
            }
            else PluginNativePolicy.Populate(allowed, engine.Context);
            GeneratorTuning.Install(engine.Context, request.Context.Tuning);
            FlowLang.StandardLibrary.Daw.DawContextFunctions.Install(engine.Context.InternalRegistry,
                request.Context, request.Descriptor.SourceId, request.SourceRevision, request.TimeLimit);
            // Imports cannot grant arbitrary filesystem access to a DAW build.
            // Packaged sources replace this with their captured dependency resolver.
            engine.ModuleLoader.SourceResolver = PluginModuleSources.ForGenerator();
            engine.Context.InternalRegistry.ReplaceAll("dawAssetSample", new FunctionSignature("dawAssetSample", [StringType.Instance]), args =>
            {
                budget.Token.ThrowIfCancellationRequested();
                var id = Guid.Parse(args[0].As<string>());
                if (request.Assets is null || !request.Assets.Samples.TryGetValue(id, out var sample))
                    throw new ArgumentException("Sample was not supplied by the project host");
                return new Value(sample, DawAudioType.Instance);
            });
            if (request.Plugin is { } plugin)
            {
                if (request.Source != plugin.Source || request.Descriptor.EntryPoint != plugin.Manifest.Builder)
                    throw new ArgumentException("Plugin package differs from build request");
                engine.ModuleLoader.SourceResolver = PluginModuleSources.Create(plugin);
                PluginSampleAssets.Install(engine.Context.InternalRegistry, plugin, budget.Token);
            }
            string identity = $"generator-{request.Descriptor.SourceId:N}.flow";
            var setup = engine.Evaluate(request.Source, identity, Options());
            if (!setup.Succeeded) return Failure(setup);
            // Values enter through the host API, never source interpolation. A fresh
            // private identifier avoids collisions with user-declared variables.
            string argument = "__dawContext_" + Guid.NewGuid().ToString("N");
            engine.Context.DeclareVariable(argument, ContextValue(request.Context, offlineAudio || noteTransform ? request.Plugin!.Manifest : null));
            string inputArgument = "";
            if (request.AudioInput is { } input)
            {
                string name = "__dawInput_" + Guid.NewGuid().ToString("N");
                var values = input.Buses.Select(asset =>
                {
                    var buffer = new FlowLang.StandardLibrary.Audio.AudioBuffer(asset.Frames, 2, asset.SampleRate);
                    asset.CopyTo(buffer.Data);
                    return MusicValue.Buffer(buffer);
                }).ToList();
                engine.Context.DeclareVariable(name, new Value(values, new ArrayType(BufferType.Instance)));
                inputArgument = " " + name;
            }
            else if (request.NoteInput is { } noteInput)
            {
                string name = "__dawNotes_" + Guid.NewGuid().ToString("N");
                engine.Context.DeclareVariable(name, new Value(noteInput, DawNoteSequenceType.Instance));
                inputArgument = " " + name;
            }
            var invocation = engine.Evaluate($"({request.Descriptor.EntryPoint} {argument}{inputArgument})", identity + ".entry", Options());
            if (!invocation.Succeeded) return Failure(invocation);
            var output = DawResultData.From(invocation.LastValue);
            var layers = new List<GeneratedScoreLayer>();
            var audio = new List<GeneratedAudioLayer>();
            var graphs = new List<GeneratedGraphLayer>();
            var instruments = new List<GeneratedInstrumentLayer>();
            long authoredNotes = 0;
            foreach (var pair in output.Outputs)
            {
                budget.Token.ThrowIfCancellationRequested();
                string layer = pair.Id;
                ArgumentException.ThrowIfNullOrWhiteSpace(layer);
                if (pair.Content.Data is PluginNoteInput notes)
                {
                    authoredNotes += notes.Notes.Count;
                    if (authoredNotes > 1_000_000) throw new InvalidOperationException("Generator exceeds authored note budget");
                    layers.Add(new(layer, notes.ToComposition(request.Descriptor.SourceId, layer, request.Context.Tempo.Changes[0].Bpm)));
                    continue;
                }
                if (noteTransform) throw new ArgumentException("Note processors must return a detached DawNoteSequence result");
                if (pair.Content.Data is PcmAsset asset) { audio.Add(new(layer, asset)); continue; }
                if (pair.Content.Data is AudioGraphDefinition graph) { graph.GetProcessingOrder(); graphs.Add(new(layer, graph)); continue; }
                if (pair.Content.Data is SineVoiceSettings instrument) { instrument.Validate(); instruments.Add(new(layer, instrument)); continue; }
                var score = CompositionCompiler.Compile(pair.Content.As<SongData>(),
                    $"daw/{request.Descriptor.SourceId:N}/layer/{Uri.EscapeDataString(layer)}", budget.Token);
                if (score.Placements.Count > 10_000) throw new InvalidOperationException("Generator exceeds placement budget");
                foreach (var section in score.Placements.Select(p => p.Section).Distinct())
                    authoredNotes += section.Sequences.Sum(s => (long)s.Notes.Count);
                if (authoredNotes > 1_000_000) throw new InvalidOperationException("Generator exceeds authored note budget");
                layers.Add(new(layer, score));
            }
            budget.Token.ThrowIfCancellationRequested();
            var detached = new GeneratedSourceOutput(request.Descriptor.SourceId, request.SourceRevision, request.Context, layers, audio, graphs, instruments);
            if (offlineAudio) request.Plugin!.ValidateOutput(detached, request.AudioInput!.SampleRate, 256);
            else if (noteTransform) request.Plugin!.ValidateOutput(detached, 1, 1);
            return new(JobStatus.Succeeded, detached);

            EvaluationOptions Options()
            {
                budget.Token.ThrowIfCancellationRequested();
                var remaining = request.TimeLimit - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero) throw new OperationCanceledException();
                return new() { Cancellation = budget.Token, TimeLimit = remaining };
            }
            JobResult<GeneratedSourceOutput> Failure(EvaluationResult result) => new(
                cancellation.IsCancellationRequested ? JobStatus.Cancelled :
                budget.IsCancellationRequested || result.Outcome == EvaluationOutcome.TimedOut ? JobStatus.TimedOut : JobStatus.Failed,
                null, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        }
        catch (OperationCanceledException)
        {
            return new(cancellation.IsCancellationRequested ? JobStatus.Cancelled : JobStatus.TimedOut, null);
        }
        catch (Exception error) { return new(JobStatus.Failed, null, error.Message); }
    }

    private static Value ContextValue(GenerationContext context, PluginManifest? processor = null)
    {
        var value = DictData.Empty(new DictType(StringType.Instance, DoubleType.Instance));
        void Add(string key, double number) => value = value.WithSet(Value.String(key), Value.Double(number));
        Add("tempoBpm", context.Tempo.Changes[0].Bpm);
        Add("meterNumerator", context.Meter.Changes[0].Numerator);
        Add("meterDenominator", context.Meter.Changes[0].Denominator);
        Add("seed", context.Seed);
        foreach (var parameter in context.Parameters) Add("parameter:" + parameter.Key, parameter.Value);
        if (processor is not null)
            foreach (var parameter in processor.Parameters)
                Add("parameter:" + parameter.Id, context.Parameters.GetValueOrDefault(parameter.Id, parameter.Default));
        return Value.Dict(value);
    }
}

/// <summary>One source's cooperative build lane. Superseded context/parameter/code
/// requests cannot publish. One engine at a time, including cancellation-ignoring
/// work; a newer job never escapes the semaphore after the coordinator's grace.</summary>
public sealed class FlowDawGeneratorHost : IDisposable
{
    private readonly Guid _sourceId;
#if !FLOW_WEB
    private readonly ProcessGeneratorWorker? _isolatedWorker;
    /// <summary>Transfers worker ownership to this source host. Recommended for the DAW.</summary>
    public FlowDawGeneratorHost(Guid sourceId, ProcessGeneratorWorker worker) : this(sourceId)
    {
        ArgumentNullException.ThrowIfNull(worker);
        _isolatedWorker = worker;
    }
#endif
    private readonly SemaphoreSlim _lane = new(1, 1);
    private readonly LatestRequestCoordinator<GeneratedSourceOutput> _jobs = new();
    public GeneratedSourceOutput? LastGood => _jobs.LastGood;
    public FlowDawGeneratorHost(Guid sourceId)
    {
        if (sourceId == Guid.Empty) throw new ArgumentException("Source ID is required", nameof(sourceId));
        _sourceId = sourceId;
    }
    public Task<JobCompletion<GeneratedSourceOutput>> Submit(GeneratorBuildRequest request)
    {
        if (request.Descriptor.SourceId != _sourceId) throw new ArgumentException("Request belongs to another source", nameof(request));
#if !FLOW_WEB
        if (_isolatedWorker is not null) return _jobs.SubmitAsync(token => _isolatedWorker.BuildAsync(request, token));
#endif
        return _jobs.Submit(token =>
        {
            _lane.Wait(token);
            try { return FlowDawGenerator.Build(request, token); }
            finally { _lane.Release(); }
        });
    }
    // The semaphore remains usable if cooperative work outlives disposal's grace.
    public void Dispose()
    {
        _jobs.Dispose();
#if !FLOW_WEB
        _isolatedWorker?.Dispose();
#endif
    }
}
