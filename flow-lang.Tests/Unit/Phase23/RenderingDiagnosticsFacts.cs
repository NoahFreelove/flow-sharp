using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 Plan 23-03 Task 1 / Pitfall 5 dedup contract Facts for
/// <see cref="RenderingDiagnostics"/>. The dedup HashSet is process-static, so
/// all Facts that consume <c>WarnOnce</c> live under
/// <c>[Collection("FlowScripts")]</c> for serialized execution + clear the dedup
/// set in ctor + Dispose.
/// </summary>
[Collection("FlowScripts")]
public class RenderingDiagnosticsFacts : System.IDisposable
{
    public RenderingDiagnosticsFacts()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void WarnOnce_FirstCall_WritesToStderr()
    {
        var sw = new System.IO.StringWriter();
        var oldErr = System.Console.Error;
        System.Console.SetError(sw);
        try
        {
            RenderingDiagnostics.WarnOnce("test-key-1", "hello world");
        }
        finally { System.Console.SetError(oldErr); }
        Assert.Contains("hello world", sw.ToString());
    }

    [Fact]
    public void WarnOnce_SameKeySecondCall_NoOp()
    {
        // First call consumes the dedup slot.
        var sw1 = new System.IO.StringWriter();
        var oldErr = System.Console.Error;
        System.Console.SetError(sw1);
        try { RenderingDiagnostics.WarnOnce("test-key-2", "first"); }
        finally { System.Console.SetError(oldErr); }
        Assert.Contains("first", sw1.ToString());

        // Second call MUST be silent — same sentinel key.
        var sw2 = new System.IO.StringWriter();
        System.Console.SetError(sw2);
        try { RenderingDiagnostics.WarnOnce("test-key-2", "second"); }
        finally { System.Console.SetError(oldErr); }
        Assert.Equal("", sw2.ToString());
    }

    [Fact]
    public void WarnOnce_DifferentKey_WritesAgain()
    {
        var sw = new System.IO.StringWriter();
        var oldErr = System.Console.Error;
        System.Console.SetError(sw);
        try
        {
            RenderingDiagnostics.WarnOnce("key-A", "msg-A");
            RenderingDiagnostics.WarnOnce("key-B", "msg-B");
        }
        finally { System.Console.SetError(oldErr); }
        Assert.Contains("msg-A", sw.ToString());
        Assert.Contains("msg-B", sw.ToString());
    }

    [Fact]
    public void ResetForTesting_ClearsDedup_AllowsReEmit()
    {
        // Sink first emission so it doesn't leak to the test runner stderr.
        var sink1 = new System.IO.StringWriter();
        var oldErr = System.Console.Error;
        System.Console.SetError(sink1);
        try { RenderingDiagnostics.WarnOnce("reset-key", "first"); }
        finally { System.Console.SetError(oldErr); }
        Assert.Contains("first", sink1.ToString());

        RenderingDiagnostics.ResetForTesting();

        var sw = new System.IO.StringWriter();
        System.Console.SetError(sw);
        try { RenderingDiagnostics.WarnOnce("reset-key", "second"); }
        finally { System.Console.SetError(oldErr); }
        Assert.Contains("second", sw.ToString());
    }

    [Fact]
    public void ThreadSafe_NoExceptionsUnderConcurrentCalls()
    {
        // Hammer WarnOnce from many threads with 5 distinct sentinel keys. The
        // dedup contract guarantees each key emits at most once. With 200
        // iterations across 5 keys, we expect AT MOST 5 stderr lines and zero
        // exceptions. Capture stderr so the test runner output stays clean.
        var sink = new System.IO.StringWriter();
        var oldErr = System.Console.Error;
        System.Console.SetError(sink);
        try
        {
            System.Threading.Tasks.Parallel.For(0, 200, i =>
            {
                RenderingDiagnostics.WarnOnce("concurrent-" + (i % 5), "msg-key-" + (i % 5));
            });
        }
        finally { System.Console.SetError(oldErr); }
        // No exception thrown means thread-safety PASS. Verify that each of the 5
        // distinct sentinel keys emitted at LEAST once and at MOST once (≤ 5 lines).
        string output = sink.ToString();
        int lineCount = output.Split('\n', System.StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(lineCount <= 5,
            $"expected ≤5 emitted lines under dedup; got {lineCount}. Output:\n{output}");
        for (int k = 0; k < 5; k++)
        {
            Assert.Contains("msg-key-" + k, output);
        }
    }
}
