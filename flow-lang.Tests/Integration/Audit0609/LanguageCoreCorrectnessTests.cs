using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
using RuntimeContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 0609 — Packet D (language-core correctness) regression tests.
///
/// Each Fact fails on the pre-fix tree and passes after the corresponding fix:
///   §2.1 — quoted strings are no longer re-typed as music literals.
///   §2.2 — the overload-resolution cache is invalidated when a frame that
///          declared functions pops, so a non-ambiguously-shadowing nested proc
///          does not survive its frame.
///   §2.3 — a `return` inside a section / musical-context / tuning block (or a
///          called parameterized section) no longer silently truncates the rest
///          of the program.
///   §2.4 — a bare function name on the `~>` RHS dispatches the overload that
///          matches the unpacked argument types instead of overloads[0] / a
///          zero-arg auto-invoke.
///   §2.6 — a healthy import after an earlier soft error succeeds (no false
///          "Failed to import").
///   §2.8 — PushFrame validates before incrementing, so a depth-overflow does
///          not permanently drift the call-depth counter in a long-lived engine.
///
/// (§2.9 is reported as skipped — its fix would regress the malformed-but-shipped
/// `oscSend` vararg signature, which lives in a non-owned file.)
/// </summary>
[Trait("Category", "Audit0609")]
[Collection("FlowScripts")] // serialize Console.SetOut (RESEARCH Pitfall 4)
public class LanguageCoreCorrectnessTests
{
    // ===================================================================
    // §2.1 — quoted strings stay String; music-literal tokens still resolve.
    // ===================================================================

    [Fact]
    public void Section2_1_QuotedTimeString_StaysString()
    {
        using var runner = new FlowEngineRunner();
        // Pre-fix: "10s" routed through TryParseSpecialLiteral → Second(10) →
        //          "Cannot assign Second to variable of type String".
        var (success, stdout, _, errorCount) = runner.RunSource("String s = \"10s\"\n(print s)");
        Assert.True(success, "assigning a quoted-time string to a String must succeed");
        Assert.Equal(0, errorCount);
        Assert.Contains("10s", stdout);
        var v = runner.GetVariable("s");
        Assert.IsType<StringType>(v.Type);
        Assert.Equal("10s", v.As<string>());
    }

    [Fact]
    public void Section2_1_QuotedNoteLetter_StaysString()
    {
        using var runner = new FlowEngineRunner();
        // Pre-fix: "a" → NoteType.Parse accepts it → Note A4 →
        //          "Cannot assign Note to variable of type String".
        var (success, _, _, errorCount) = runner.RunSource("String s = \"a\"\n(print s)");
        Assert.True(success);
        Assert.Equal(0, errorCount);
        var v = runner.GetVariable("s");
        Assert.IsType<StringType>(v.Type);
        Assert.Equal("a", v.As<string>());
    }

    [Fact]
    public void Section2_1_DictKeyedByTimeString_RoundTrips()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errorCount) = runner.RunSource(
            "use \"@std\"\nDict<String, Int> d = (dict \"10s\" 42)\n(print (str (get d \"10s\")))");
        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.Contains("42", stdout);
    }

    [Fact]
    public void Section2_1_MusicLiteralTokens_StillResolve()
    {
        using var runner = new FlowEngineRunner();
        // The bare (un-quoted) music-literal tokens must keep resolving to their
        // music Value — the discriminator only suppresses re-typing of QUOTED
        // strings, never of music-literal tokens.
        var (success, _, _, errorCount) = runner.RunSource(
            "Second sec = 2.5s\nHertz hz = 440Hz\nDecibel db = -6dB");
        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.IsType<SecondType>(runner.GetVariable("sec").Type);
        Assert.IsType<HertzType>(runner.GetVariable("hz").Type);
        Assert.IsType<DecibelType>(runner.GetVariable("db").Type);
    }

    // ===================================================================
    // §2.2 — overload cache invalidated when a function-declaring frame pops.
    // ===================================================================

    [Fact]
    public void Section2_2_ShadowOverload_OuterResolvesAfterFramePops()
    {
        using var runner = new FlowEngineRunner();
        // Non-ambiguous shadow (verifier shape (a)): global f(Double:) + nested
        // f(Int:). Inside g, (f 5) resolves the local Int overload and CACHES it
        // under ("f",[Int]). After g returns, the top-level (f 5) must re-resolve
        // to the visible GLOBAL f — pre-fix it hit the stale cache and ran the
        // popped local body ("local-int").
        var src =
            "proc f (Double: x)\n" +
            "    (print \"global-double\")\n" +
            "end proc\n" +
            "proc g ()\n" +
            "    proc f (Int: x)\n" +
            "        (print \"local-int\")\n" +
            "    end proc\n" +
            "    (f 5)\n" +
            "end proc\n" +
            "(g)\n" +
            "(f 5)";
        var (success, stdout, _, _) = runner.RunSource(src);
        Assert.True(success);
        // Order: inner call prints local-int, then the top-level call MUST print
        // global-double (not a second "local-int").
        Assert.Equal("local-int", FirstLine(stdout));
        Assert.Equal("global-double", LastNonEmptyLine(stdout));
        Assert.DoesNotContain("local-int", AfterFirstLine(stdout));
    }

    // ===================================================================
    // §2.3 — `return` inside a definitional / context block does not truncate.
    // ===================================================================

    [Fact]
    public void Section2_3_ReturnInTopLevelTempoBlock_DoesNotSkipRest()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, errorCount) = runner.RunSource(
            "tempo 120 { return 5 }\n(print \"AFTER-BLOCK\")");
        Assert.Contains("AFTER-BLOCK", stdout);
        Assert.True(errorCount >= 1);
        Assert.Contains("'return' is not allowed inside a musical-context block", stderr);
    }

    [Fact]
    public void Section2_3_ReturnInSection_DoesNotSkipRest()
    {
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, errorCount) = runner.RunSource(
            "section v { return 5 }\n(print \"AFTER-SECTION\")");
        Assert.Contains("AFTER-SECTION", stdout);
        Assert.True(errorCount >= 1);
        Assert.Contains("section 'v'", stderr);
    }

    [Fact]
    public void Section2_3_ReturnInTuningBlock_DoesNotSkipRest()
    {
        using var runner = new FlowEngineRunner();
        // Load a real .scl fixture so the tuning value resolves and the BODY of
        // the tuning block actually executes; the inner `return` must not skip
        // the program tail (same transparent-wrapper rule as ExecuteMusicalContext).
        string scl = FixturePath("carlos_alpha.scl").Replace("\\", "/");
        var src =
            "use \"@std\"\n" +
            $"tuning \"{scl}\" {{ return 5 }}\n" +
            "(print \"AFTER-TUNING\")";
        var (_, stdout, stderr, errorCount) = runner.RunSource(src);
        Assert.Contains("AFTER-TUNING", stdout);
        Assert.True(errorCount >= 1);
        Assert.Contains("'return' is not allowed inside a tuning block", stderr);
    }

    [Fact]
    public void Section2_3_ReturnFlagDoesNotLeakAcrossExecuteEvals()
    {
        // The Interpreter is reused across REPL-style evals. A leaked return flag
        // from one eval must not silence the next. First eval leaks a return out
        // of a section; the second eval on the SAME engine must still run its
        // print. RunSource returns the cumulative captured stdout.
        using var runner = new FlowEngineRunner();
        runner.RunSource("section v { return 5 }", "<eval-1>");
        var (ok, stdout, _, _) = runner.RunSource(
            "use \"@std\"\n(print \"SECOND-EVAL-RAN\")", "<eval-2>");
        Assert.True(ok);
        Assert.Contains("SECOND-EVAL-RAN", stdout);
    }

    // ===================================================================
    // §2.4 — `~>` bare-name RHS resolves against the unpacked arg types.
    // ===================================================================

    [Fact]
    public void Section2_4_TupleUnpackFlow_DispatchesCorrectOverload()
    {
        using var runner = new FlowEngineRunner();
        var src =
            "proc describe (Int: a, Int: b)\n" +
            "    (print (concat \"int-pair \" (str (add a b))))\n" +
            "end proc\n" +
            "proc describe (String: a, String: b)\n" +
            "    (print (concat \"str-pair \" (concat a b)))\n" +
            "end proc\n" +
            "use \"@std\"\n" +
            "Tuple<<Int, Int>> ints = <<3, 4>>\n" +
            "Tuple<<String, String>> strs = <<\"x\", \"y\">>\n" +
            "ints ~> describe\n" +
            "strs ~> describe";
        var (success, stdout, _, _) = runner.RunSource(src);
        Assert.True(success);
        // Pre-fix: both lines dispatch overloads[0] (the Int-pair, registered
        // first), so the String tuple would throw an internal cast error.
        Assert.Contains("int-pair 7", stdout);
        Assert.Contains("str-pair xy", stdout);
    }

    [Fact]
    public void Section2_4_TupleUnpackFlow_BareName_NoZeroArgAutoInvoke()
    {
        using var runner = new FlowEngineRunner();
        // `f` has BOTH a zero-arg and a one-arg overload. `5 ~> f` must call the
        // one-arg overload with 5, not auto-invoke the zero-arg overload during
        // RHS evaluation.
        var src =
            "proc f ()\n" +
            "    (print \"zero-arg\")\n" +
            "end proc\n" +
            "proc f (Int: x)\n" +
            "    (print (concat \"one-arg \" (str x)))\n" +
            "end proc\n" +
            "use \"@std\"\n" +
            "5 ~> f";
        var (success, stdout, _, _) = runner.RunSource(src);
        Assert.True(success);
        Assert.Contains("one-arg 5", stdout);
        Assert.DoesNotContain("zero-arg", stdout);
    }

    // ===================================================================
    // §2.6 — healthy import after an earlier soft error succeeds.
    // ===================================================================

    [Fact]
    public void Section2_6_HealthyImportAfterSoftError_Succeeds()
    {
        using var runner = new FlowEngineRunner();
        // The unknown-identifier error makes the GLOBAL reporter HasErrors==true
        // BEFORE the import. Pre-fix, LoadModule returned Error on HasErrors and
        // ExecuteImport piled on a misleading "Failed to import '@audio'".
        var (_, stdout, stderr, _) = runner.RunSource(
            "(print nonexistentVariable)\nuse \"@audio\"\n(print \"IMPORT-OK-AUDIO\")");
        Assert.Contains("IMPORT-OK-AUDIO", stdout);
        Assert.DoesNotContain("Failed to import", stderr);
    }

    // ===================================================================
    // §2.8 — PushFrame does not drift _callDepth on overflow.
    // ===================================================================

    [Fact]
    public void Section2_8_PushFrameOverflow_DoesNotDriftCounter()
    {
        // Exercise PushFrame's overflow path directly (no real CLR-stack-deep
        // recursion — a 1000-deep proc chain blows the actual native stack before
        // the logical limit fires). Push until the limit throws, then verify the
        // counter did NOT drift past the maximum (pre-fix it incremented BEFORE
        // throwing, leaving a permanent +1), and that a full unwind returns to 0.
        var ctx = new RuntimeContext(
            new FlowLang.Diagnostics.ErrorReporter(),
            new FlowLang.StandardLibrary.InternalFunctionRegistry());
        int max = MaxCallDepthConst();

        int pushed = 0;
        bool threw = false;
        try
        {
            // The global frame already occupies depth 0; PushFrame can succeed
            // exactly `max` times before the (max+1)-th throws.
            for (int i = 0; i < max + 5; i++) { ctx.PushFrame(); pushed++; }
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw, "PushFrame must throw once the depth limit is exceeded");
        // Pre-fix: the failed push incremented _callDepth to max+1 (never rolled
        // back). Post-fix: validate-before-increment leaves it at exactly max.
        Assert.Equal(max, ReadCallDepth(ctx));
        Assert.Equal(max, pushed);

        // Unwind every successful push; the counter must return to 0 with no drift.
        for (int i = 0; i < pushed; i++) ctx.PopFrame();
        Assert.Equal(0, ReadCallDepth(ctx));
    }

    // ===================================================================
    // Helpers.
    // ===================================================================

    private static string FirstLine(string s)
    {
        foreach (var line in s.Replace("\r\n", "\n").Split('\n'))
            if (line.Trim().Length > 0) return line.Trim();
        return string.Empty;
    }

    private static string AfterFirstLine(string s)
    {
        var lines = s.Replace("\r\n", "\n").Split('\n');
        bool seenFirst = false;
        var sb = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (!seenFirst)
            {
                if (line.Trim().Length > 0) { seenFirst = true; }
                continue;
            }
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private static string LastNonEmptyLine(string s)
    {
        var lines = s.Replace("\r\n", "\n").Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
            if (lines[i].Trim().Length > 0) return lines[i].Trim();
        return string.Empty;
    }

    /// <summary>
    /// Reads the private _callDepth field via reflection (no public accessor).
    /// The §2.8 invariant: after every push has its matched pop, the counter is 0.
    /// </summary>
    private static int ReadCallDepth(RuntimeContext ctx)
    {
        var field = typeof(RuntimeContext).GetField(
            "_callDepth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        return (int)field!.GetValue(ctx)!;
    }

    /// <summary>Resolves an in-repo Scala fixture (mirrors Phase 32 tests).</summary>
    private static string FixturePath(string name)
        => System.IO.Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", name);

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not locate repo root (flow-lang.Tests/fixtures)");
    }

    /// <summary>Reads the private const MaxCallDepth (no public accessor).</summary>
    private static int MaxCallDepthConst()
    {
        var field = typeof(RuntimeContext).GetField(
            "MaxCallDepth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);
        return (int)field!.GetRawConstantValue()!;
    }
}
