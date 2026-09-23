using FlowLang.StandardLibrary.Harmony;
using FlowLang.Syntax;
using FlowLang.TypeSystem;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// The grammar's lexical tables (language layer) and the music layer's semantic
/// tables must agree: every chord quality the lexer accepts has intervals, and every
/// grammar type name is bound when the music layer is installed.
/// </summary>
public class GrammarConsistencyTests
{
    [Fact]
    public void ChordQualitiesMatchTheIntervalTable()
    {
        Assert.Equal(
            ChordSyntax.Qualities.Order(StringComparer.Ordinal),
            ChordParser.QualityNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryGrammarTypeNameIsBoundWithTheMusicLayer()
    {
        var unbound = TypeNames.Named.Concat(TypeNames.Pluralizable)
            .Where(name => TypeCatalog.Default.TryResolve(name) is null)
            .Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Empty(unbound);
    }
}
