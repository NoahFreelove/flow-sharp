using FlowLang.Core;

namespace FlowCli.Doc;

// Phase 41 Plan 41-03 DOC-02 — executes each `///` code example in-process and
// annotates failures.
//
// Per CONTEXT D-10 the doc-example default form is a BARE EXPRESSION that simply
// "runs without error" (RESEARCH Open Q3 recommended default). We reuse the
// proven in-process pattern from flow-cli/Commands/TestCommand.cs:79-107:
//
//   using var engine = new FlowEngine(verbose: false);
//   bool ok = engine.Execute(source, "<doc-example>");
//   if (!ok || engine.ErrorReporter.HasErrors) { /* [example failed] */ }
//
// A fresh FlowEngine per example IS the hermetic isolation — every TestRegistry,
// musical-context stack, voice pool, PRNG, and binding table is engine-scoped,
// so two examples cannot leak into each other (the same isolation guarantee the
// Phase 35 TestRunner provides per (test ...) block, here at engine granularity).
// We do NOT build a second isolation framework (D-10).
//
// Per D-10, audio/MIDI examples are judged by SUCCESSFUL RENDER (no error), NOT
// byte output — `(play ...)` / `(writeWav ...)` / `(writeMidi ...)` in an example
// is a pass if the engine executed it without accumulating an error. We never
// compare bytes (platform-portable).
//
// stdout from an example's own `(print ...)` is suppressed during generation so
// `flow doc` output stays clean; stderr advisories likewise. Only the pass/fail
// signal flows back into the DocModel.
//
// T-41-03-DOS mitigation: each example runs under a wall-clock budget
// (DefaultExampleTimeoutMs) on a worker thread; a runaway example is annotated
// `[example failed] timed out` rather than hanging the whole `flow doc` run.
public sealed class DocExampleRunner
{
    public const int DefaultExampleTimeoutMs = 30_000;

    private readonly int _timeoutMs;

    public DocExampleRunner(int timeoutMs = DefaultExampleTimeoutMs)
    {
        _timeoutMs = timeoutMs <= 0 ? DefaultExampleTimeoutMs : timeoutMs;
    }

    /// <summary>
    /// Returns the same models with each model's ExampleFailures populated: a
    /// per-example nullable list of the same length as Examples (null = pass,
    /// non-null = the [example failed] annotation text). Both emitters index
    /// ExampleFailures[i] directly under Examples[i] so a failure is always
    /// rendered beneath the example that caused it, regardless of how many
    /// other examples pass.
    /// </summary>
    public DocModel[] RunAll(IReadOnlyList<DocModel> models)
    {
        var result = new DocModel[models.Count];
        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            if (model.Examples.Count == 0)
            {
                result[i] = model;
                continue;
            }

            // Build a per-example nullable list: same length as Examples,
            // null at index j = example j passed, non-null = failure text.
            var perExample = new string?[model.Examples.Count];
            bool anyFailed = false;
            for (int j = 0; j < model.Examples.Count; j++)
            {
                var failure = RunOne(model.Examples[j]);
                perExample[j] = failure;
                if (failure is not null)
                    anyFailed = true;
            }

            if (!anyFailed)
            {
                result[i] = model;
                continue;
            }

            // Convert to IReadOnlyList<string> using empty string for passes
            // so emitters can use the index directly without a null check.
            // The emitters already guard `!string.IsNullOrEmpty` / present
            // the annotation only when non-empty.
            var failures = new string[model.Examples.Count];
            for (int j = 0; j < model.Examples.Count; j++)
                failures[j] = perExample[j] ?? string.Empty;

            result[i] = model.WithFailures(failures);
        }
        return result;
    }

    /// <summary>
    /// Execute a single example. Returns null on success, or the
    /// `[example failed]` annotation text (the formatted errors / timeout note)
    /// on failure.
    /// </summary>
    public string? RunOne(string exampleSource)
    {
        string? failure = null;
        Exception? thrown = null;

        var worker = new Thread(() =>
        {
            // The worker ONLY runs the engine. The Console redirect/restore is owned
            // by the CALLING thread below (CR-03 fix) so it is always
            // restored, including on the timeout path where this worker keeps running
            // in the background. Doing the redirect here risked leaving the host
            // process's Console.Out pointed at TextWriter.Null whenever a worker timed
            // out before its finally ran (silently swallowing all later flow-doc
            // output), plus a restore race between concurrent RunOne workers.
            try
            {
                using var engine = new FlowEngine(verbose: false);
                bool ok = engine.Execute(exampleSource, "<doc-example>");
                if (!ok || engine.ErrorReporter.HasErrors)
                {
                    var formatted = engine.ErrorReporter.FormatErrors();
                    failure = string.IsNullOrWhiteSpace(formatted)
                        ? "[example failed]"
                        : "[example failed] " + Flatten(formatted);
                }
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        })
        {
            IsBackground = true,
        };

        // CR-03: redirect + restore on the CALLING thread so the host process's
        // Console.Out/Error is ALWAYS restored — including the timeout path, where the
        // worker is abandoned to the background and its own finally would never run on
        // this thread. The example's own print/advisory output during the join window
        // is suppressed; a timed-out worker that prints afterward writes to the
        // already-restored host stream, which is harmless (flow doc output is clean).
        var savedOut = Console.Out;
        var savedErr = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            worker.Start();
            if (!worker.Join(_timeoutMs))
            {
                // Best-effort: we cannot preempt the worker (Thread.Abort is gone in
                // modern .NET), but it is a background thread so it dies with the
                // process. Annotate and move on.
                return $"[example failed] timed out after {_timeoutMs} ms";
            }
        }
        finally
        {
            Console.SetOut(savedOut);
            Console.SetError(savedErr);
        }

        if (thrown is not null)
            return "[example failed] " + Flatten(thrown.Message);
        return failure;
    }

    private static string Flatten(string s) =>
        s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
}
