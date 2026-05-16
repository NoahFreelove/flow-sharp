using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Unit.Phase33;

/// <summary>
/// Phase 33 Plan 33-02 — facts pinning the type-system surface for the new
/// <see cref="SfzType"/> + the runtime-state surface added to
/// <see cref="ExecutionContext"/> + the <see cref="FlowConfigPoco.SfzRoot"/>
/// config key. Type-only — no parsing, no audio rendering, no I/O.
///
/// SfzType mirrors Phase 32's <see cref="TuningType"/> contract: sealed
/// singleton, specificity 150 (above all existing music types — see XML doc
/// on <see cref="SfzType"/> for the slot rationale), strict compatibility
/// (no numeric coercion, no cross-music-type equivalence). The Value.Sfz
/// factory + ExecutionContext fields + FlowConfigPoco.SfzRoot all default to
/// the empty / null state on a fresh context.
/// </summary>
public class SfzTypeFacts
{
    private static ExecutionContext NewExecutionContext()
    {
        var reporter = new ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new ExecutionContext(reporter, registry);
    }

    // ---- SfzType type-system facts ----

    [Fact]
    public void SfzType_GetSpecificity_Is150()
    {
        Assert.Equal(150, SfzType.Instance.GetSpecificity());
    }

    [Fact]
    public void SfzType_Name_IsSfz()
    {
        Assert.Equal("Sfz", SfzType.Instance.Name);
    }

    [Fact]
    public void SfzType_IsCompatibleWith_SfzType_True()
    {
        // Strict-positive case — Sfz → Sfz is allowed.
        Assert.True(SfzType.Instance.IsCompatibleWith(SfzType.Instance));
    }

    [Fact]
    public void SfzType_IsCompatibleWith_TuningType_False()
    {
        // Strict — no cross-music-type compatibility (an Sfz value must NOT
        // pass into a Tuning-typed parameter slot, and vice-versa).
        Assert.False(SfzType.Instance.IsCompatibleWith(TuningType.Instance));
    }

    [Fact]
    public void SfzType_IsCompatibleWith_StringType_False()
    {
        // Strict — no numeric/string coercion. Mirrors TuningType's reference-
        // identity contract; only Sfz is compatible with Sfz.
        Assert.False(SfzType.Instance.IsCompatibleWith(StringType.Instance));
    }

    // ---- ExecutionContext SFZ-surface defaults ----

    [Fact]
    public void ExecutionContext_FreshInstance_HasEmptySfzFields()
    {
        var ctx = NewExecutionContext();

        // SfzEnabled defaults false until __enableSfzModule runs.
        Assert.False(ctx.SfzEnabled);

        // The four collection / cache fields all start empty / null.
        Assert.Empty(ctx.SfzInstruments);
        Assert.Empty(ctx.SfzPatchRegistry);
        Assert.Empty(ctx.SfzDiagnostics);
        Assert.Null(ctx.ResolvedSfzRoot);
    }

    // ---- FlowConfigPoco.SfzRoot default ----

    [Fact]
    public void FlowConfigPoco_Default_SfzRootIsNull()
    {
        // The Defaults singleton must leave SfzRoot null — this is what the
        // first loadSfz call in a fresh context will read into ResolvedSfzRoot
        // before raising MissingSfzRootError (Plan 33-05).
        Assert.Null(FlowConfigPoco.Defaults.SfzRoot);
    }
}
