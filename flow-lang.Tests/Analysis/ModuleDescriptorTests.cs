using FlowLang.Core;
using FlowLsp;
using FlowLsp.Symbols;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace FlowLang.Tests.Analysis;

public class ModuleDescriptorTests
{
    [FlowLang.Tests.Helpers.FlowTargetFact("Desktop")]
    public void EditorMetadataProvidesCoreAndMusicSignatures()
    {
        var stdlib = new StdlibSymbolIndex(new ParseSession());
        var index = new BuiltInIndex(stdlib.Descriptors);
        foreach (var name in new[] { "print", "map", "reverb", "transpose", "chordNotes", "oscListen" })
            Assert.NotEmpty(index.Find(name)!.Signatures);
        Assert.All(stdlib.Descriptors.SelectMany(m => m.Procedures), p =>
        {
            Assert.Equal(p.Signature.InputTypes.Count, p.Signature.ParameterNames!.Count);
            Assert.NotEmpty(p.SourceId);
            Assert.True(p.Span.Start.Line > 0);
        });
        Assert.Contains(index.Items(), item => item.Label == "print" && item.Detail!.Contains("String"));
    }

    [FlowLang.Tests.Helpers.FlowTargetFact("Desktop")]
    public void DeclaredInternalSignaturesHaveRuntimeImplementations()
    {
        var stdlib = new StdlibSymbolIndex(new ParseSession());
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var missing = stdlib.Descriptors.SelectMany(m => m.Procedures).Where(p => p.IsInternal)
            .Where(p => !engine.Context.InternalRegistry.TryGetImplementation(p.Name, p.Signature, out _, out _))
            .Select(p => $"{p.SourceId}: {p.Signature}").ToArray();
        Assert.True(missing.Length == 0, string.Join("\n", missing));
    }

    [Fact]
    public void LspAssemblyDoesNotCallRuntimeRegistrationOrConstructEngineOrAudioManager()
    {
        using var module = ModuleDefinition.ReadModule(typeof(ParseSession).Assembly.Location);
        static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
        var forbidden = module.Types.SelectMany(Types).SelectMany(t => t.Methods)
            .Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Newobj)
            .Select(i => i.Operand).OfType<MethodReference>()
            .Where(m => m.DeclaringType.FullName is "FlowLang.StandardLibrary.BuiltInFunctions"
                or "FlowLang.Core.FlowEngine" or "FlowLang.Audio.AudioPlaybackManager"
                or "FlowLang.StandardLibrary.Audio.SampleCache"
                or "FlowLang.StandardLibrary.Audio.Sfz.SfzSampleCache"
                or "FlowLang.StandardLibrary.Improv.StyleRegistry")
            .Select(m => m.FullName).ToArray();
        Assert.Empty(forbidden);
    }
}
