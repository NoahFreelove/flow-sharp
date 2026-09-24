using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.Contracts;

/// <summary>
/// The Phase 3 gate "the language artifact's dependency closure excludes music",
/// checked on the artifact itself. flow-language.dll (lexer, parser, types, values,
/// interpreter, extension contracts) references nothing but the BCL — no music,
/// audio, IO or native packages — and every language-namespace type lives in it,
/// except the host glue listed in <see cref="DependencyDirectionTests.HostTypes"/>.
/// </summary>
public class LanguageClosureTests
{
    private static readonly string LanguagePath = typeof(FlowLang.Runtime.Value).Assembly.Location;
    private static readonly string MusicPath = typeof(FlowLang.Core.FlowEngine).Assembly.Location;

    [Fact]
    public void LanguageAssemblyReferencesOnlyTheBcl()
    {
        using var module = ModuleDefinition.ReadModule(LanguagePath);
        Assert.Equal("flow-language", module.Assembly.Name.Name);
        var nonBcl = module.AssemblyReferences
            .Select(r => r.Name)
            .Where(n => !(n.StartsWith("System", StringComparison.Ordinal) || n is "netstandard" or "mscorlib"))
            .ToList();
        Assert.True(nonBcl.Count == 0,
            "flow-language.dll must reference only the BCL; it references: " + string.Join(", ", nonBcl));
    }

    [Fact]
    public void LanguageAssemblyHasNoMusicOrPlatformTypes()
    {
        using var module = ModuleDefinition.ReadModule(LanguagePath);
        var misplaced = module.Types
            .Where(t => DependencyDirectionTests.ForbiddenNamespaces.Any(f => t.Namespace == f || t.Namespace.StartsWith(f + ".", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToList();
        Assert.True(misplaced.Count == 0, "Music/platform types in flow-language.dll:\n  " + string.Join("\n  ", misplaced));
    }

    [Fact]
    public void LanguageCodeLivesInTheLanguageAssembly()
    {
        using var module = ModuleDefinition.ReadModule(MusicPath);
        var stray = module.Types
            .Where(t => DependencyDirectionTests.LanguageNamespaces.Any(l => t.Namespace == l || t.Namespace.StartsWith(l + ".", StringComparison.Ordinal)))
            .Where(t => !t.Namespace.StartsWith("FlowLang.TypeSystem.SpecialTypes", StringComparison.Ordinal))
            .Where(t => !DependencyDirectionTests.HostTypes.Contains(t.FullName))
            .Select(t => t.FullName)
            .ToList();
        Assert.True(stray.Count == 0,
            "Language-namespace types in flow-lang.dll (move them to flow-language, or list real host glue in DependencyDirectionTests.HostTypes):\n  "
            + string.Join("\n  ", stray));
    }
}
