using System;
using System.Linq;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase28;

/// <summary>
/// Phase 28 (SPEC-3) — gate test that <see cref="Articulation.Legato"/> exists in the
/// Articulation enum. The `leg` token in note streams must produce this value; if the
/// enum is rolled back without updating the parser, this test fails before the rest of
/// the Phase 28 suite. Distinct from the Phase 22 legato() transform (DurationOverlap).
/// </summary>
public class LegatoEnumTests
{
    [Fact]
    public void Legato_EnumValueExists()
    {
        var values = Enum.GetValues(typeof(Articulation)).Cast<Articulation>().ToArray();
        Assert.Contains(Articulation.Legato, values);
    }
}
