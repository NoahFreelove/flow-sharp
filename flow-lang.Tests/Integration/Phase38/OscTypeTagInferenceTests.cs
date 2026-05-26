using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Network;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-06 OSC-01 + OSC-02 — charitable smallest-tag-that-fits
/// OSC type-tag inference per D-38-13. Asserts the Flow Value → CLR object
/// mapping that Rug.Osc 1.2.5's <c>OscMessage(string, params object[])</c>
/// constructor consumes (Rug.Osc encodes the OSC 1.0 type-tag string
/// directly from the boxed CLR types — see 38-RESEARCH §L lines 1180-1201).
///
/// <para>
/// Filled by Plan 38-06 Task 1 alongside
/// <c>OscFunctions.InferOscArgs</c> + <c>OscFunctions.AudioBufferToBlob</c>.
/// Task 2 will exercise the full <c>oscSend</c> dispatch path via UDP
/// loopback (<see cref="OscLoopbackTests"/>).
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class OscTypeTagInferenceTests : IDisposable
{
    public OscTypeTagInferenceTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// Send_InfersSmallestTagPerArg: pass `[Int, Float, String, Bool true]`;
    /// assert the resulting object[] holds the matching CLR boxed primitives
    /// in the expected types (int / float / string / bool true). Per D-38-13
    /// + RESEARCH §L the implied OSC type-tag string is <c>,ifsT</c>.
    /// </summary>
    [Fact]
    public void Send_InfersSmallestTagPerArg()
    {
        var flowArgs = new Value[]
        {
            Value.Int(1),
            Value.Float(1.5),
            Value.String("hi"),
            Value.Bool(true),
        };

        var oscArgs = OscFunctions.InferOscArgs(flowArgs);

        Assert.Equal(4, oscArgs.Length);
        Assert.IsType<int>(oscArgs[0]);
        Assert.Equal(1, (int)oscArgs[0]);
        Assert.IsType<float>(oscArgs[1]);
        Assert.Equal(1.5f, (float)oscArgs[1]);
        Assert.IsType<string>(oscArgs[2]);
        Assert.Equal("hi", (string)oscArgs[2]);
        Assert.IsType<bool>(oscArgs[3]);
        Assert.True((bool)oscArgs[3]);
    }

    /// <summary>
    /// Send_BufferAsBlob: pass an AudioBuffer Value; assert the resulting
    /// object is a non-empty <c>byte[]</c> blob (4 bytes per float sample).
    /// </summary>
    [Fact]
    public void Send_BufferAsBlob()
    {
        var buf = new AudioBuffer(8, 1, 44100);
        for (int i = 0; i < buf.Frames; i++) buf.Data[i] = 0.25f * i;
        var flowArgs = new Value[] { Value.Buffer(buf) };

        var oscArgs = OscFunctions.InferOscArgs(flowArgs);

        Assert.Single(oscArgs);
        var blob = Assert.IsType<byte[]>(oscArgs[0]);
        Assert.Equal(buf.Data.Length * 4, blob.Length);
    }

    /// <summary>
    /// Send_DoubleStaysDouble: pass <c>Value.Double(1.5)</c> explicitly;
    /// assert the resulting object is a <c>double</c> (not a float). The
    /// inference is type-based, not value-based — composer's explicit-cast
    /// escape hatch per D-38-13.
    /// </summary>
    [Fact]
    public void Send_DoubleStaysDouble()
    {
        var flowArgs = new Value[] { Value.Double(1.5) };

        var oscArgs = OscFunctions.InferOscArgs(flowArgs);

        Assert.Single(oscArgs);
        Assert.IsType<double>(oscArgs[0]);
        Assert.Equal(1.5, (double)oscArgs[0]);
    }

    /// <summary>
    /// Send_BoolFalse_BoxesAsFalse: assert the Bool false branch lands at
    /// boxed `false` (Rug.Osc maps to ,F tag).
    /// </summary>
    [Fact]
    public void Send_BoolFalse_BoxesAsFalse()
    {
        var flowArgs = new Value[] { Value.Bool(false) };

        var oscArgs = OscFunctions.InferOscArgs(flowArgs);

        Assert.IsType<bool>(oscArgs[0]);
        Assert.False((bool)oscArgs[0]);
    }

    /// <summary>
    /// Send_LongStaysLong: assert Long → ,h (long), not silently widened to
    /// double — type fidelity for composer's 64-bit integers.
    /// </summary>
    [Fact]
    public void Send_LongStaysLong()
    {
        var flowArgs = new Value[] { Value.Long(9999999999L) };

        var oscArgs = OscFunctions.InferOscArgs(flowArgs);

        Assert.IsType<long>(oscArgs[0]);
        Assert.Equal(9999999999L, (long)oscArgs[0]);
    }

    /// <summary>
    /// Send_UnsupportedType_Throws: pass a Sequence Value; assert
    /// <see cref="ArgumentException"/> with body matching
    /// <c>[osc] unsupported arg type at index 0</c> per RESEARCH §L line 1199.
    /// </summary>
    [Fact]
    public void Send_UnsupportedType_Throws()
    {
        // Construct a minimal SequenceData to wrap; the exact shape doesn't
        // matter since InferOscArgs rejects on FlowType match.
        var emptySeq = new SequenceData();
        var flowArgs = new Value[] { Value.Sequence(emptySeq) };

        var ex = Assert.Throws<ArgumentException>(() => OscFunctions.InferOscArgs(flowArgs));
        Assert.Contains("[osc] unsupported arg type at index 0", ex.Message);
        Assert.Contains("Sequence", ex.Message);
    }
}
