using System;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.TestFramework;

/// <summary>
/// Phase 35 Plan 35-04 TEST-01 — registers the (test ...) special-form
/// builtin + the five assertion primitives against the
/// <see cref="InternalFunctionRegistry"/>. Called once per FlowEngine
/// construction by <c>BuiltInFunctions.RegisterAllImplementations</c>.
///
/// <para>
/// The <c>test</c> builtin's body parameter is signed as
/// <c>LazyType(VoidType.Instance)</c> — RESEARCH §Pitfall 10 LOAD-BEARING.
/// Without the Lazy wrap the body argument evaluates eagerly at the
/// registration call site and hermetic isolation is meaningless. The
/// precedent is the <c>if</c> builtin at <c>BuiltInFunctions.cs:339</c>.
/// </para>
///
/// <para>
/// Assertion primitives use concrete typed signatures where the parameter
/// shape is known (<c>BoolType</c>, <c>BufferType</c>, <c>SequenceType</c>,
/// <c>DecibelType</c>) and the <see cref="VoidType"/> wildcard for
/// (assertEq a b) — matches the existing <c>(equals a b)</c> shape at
/// <c>BuiltInFunctions.cs:371-374</c>.
/// </para>
/// </summary>
public static class TestFunctions
{
    public static void RegisterTestFramework(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));
        if (context is null) throw new ArgumentNullException(nameof(context));

        // (test "name" body) — body deferred via LazyType wrap (Pitfall 10).
        var testSig = new FunctionSignature(
            "test",
            [StringType.Instance, new LazyType(VoidType.Instance)],
            ParameterNames: ["name", "body"]);
        registry.Register("test", testSig, args =>
        {
            var name = args[0].As<string>();
            var bodyThunk = args[1].As<Thunk>();
            context.TestRegistry.Add(new TestRecord(name, bodyThunk, Span.Unknown));
            return Value.Void();
        });

        // (assert cond) — single bool arg; throws AssertionException on false.
        var assertSig = new FunctionSignature(
            "assert", [BoolType.Instance],
            ParameterNames: ["cond"]);
        registry.Register("assert", assertSig, args =>
        {
            AssertionHelpers.AssertOrThrow(args[0].As<bool>());
            return Value.Void();
        });

        // (assertEq a b) — Void-wildcard pair per the (equals a b) precedent.
        var assertEqSig = new FunctionSignature(
            "assertEq",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["actual", "expected"]);
        registry.Register("assertEq", assertEqSig, args =>
        {
            AssertionHelpers.AssertEqOrThrow(args[0], args[1]);
            return Value.Void();
        });

        // (assertNotesMatch seqA seqB) — structural Sequence equality.
        var assertNotesMatchSig = new FunctionSignature(
            "assertNotesMatch",
            [SequenceType.Instance, SequenceType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("assertNotesMatch", assertNotesMatchSig, args =>
        {
            var a = args[0].As<SequenceData>();
            var b = args[1].As<SequenceData>();
            AssertionHelpers.AssertNotesMatchOrThrow(a, b);
            return Value.Void();
        });

        // (assertBytesEqual buf1 buf2) — PCM sample-for-sample equality.
        var assertBytesEqualSig = new FunctionSignature(
            "assertBytesEqual",
            [BufferType.Instance, BufferType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("assertBytesEqual", assertBytesEqualSig, args =>
        {
            var a = args[0].As<AudioBuffer>();
            var b = args[1].As<AudioBuffer>();
            AssertionHelpers.AssertBytesEqualOrThrow(a, b);
            return Value.Void();
        });

        // (assertWithinDb buf1 buf2 toleranceDb) — SPEC-8 100 ms RMS window.
        var assertWithinDbSig = new FunctionSignature(
            "assertWithinDb",
            [BufferType.Instance, BufferType.Instance, DecibelType.Instance],
            ParameterNames: ["a", "b", "tolerance"]);
        registry.Register("assertWithinDb", assertWithinDbSig, args =>
        {
            var a = args[0].As<AudioBuffer>();
            var b = args[1].As<AudioBuffer>();
            var tolerance = args[2].As<double>(); // Decibel coerces to Double
            AssertionHelpers.AssertWithinDbOrThrow(a, b, tolerance);
            return Value.Void();
        });
    }
}
