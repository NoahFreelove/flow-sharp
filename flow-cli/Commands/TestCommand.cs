using System.CommandLine;
using FlowLang.Core;
using FlowLang.StandardLibrary.TestFramework;

namespace FlowCli.Commands;

// Phase 35 Plan 35-04 TEST-01 — `flow test [path]` subcommand.
//
// Discovers test_*.flow files (single file OR `Directory.GetFiles(path,
// "test_*.flow", SearchOption.TopDirectoryOnly)` when `path` is a directory).
// Defaults to `tests/` when no path argument is given so `flow test` from a
// repo root just works.
//
// For each file:
//   1. Construct a fresh FlowEngine (per-file engine — test bodies cannot
//      leak across files because every TestRegistry is engine-scoped).
//   2. Execute the source — (test "name" lazy(body)) calls accumulate
//      TestRecord entries on engine.Context.TestRegistry without running
//      bodies (Pitfall 10 LazyType deferral).
//   3. Hand the engine to TestRunner.Run, which walks the registry
//      wrapping each body invocation in a SnapshotState/RestoreState
//      guard (TEST-02 hermetic isolation).
//
// Exit code: 0 when every test passed; 1 when ANY test failed OR a file
// failed to lex/parse/execute. Mirrors CheckCommand's exit-code semantics.
//
// Argument shape: Argument<string?> with ArgumentArity.ZeroOrOne so
// `flow test` (no arg) reaches the "tests/" default. CheckCommand uses
// Argument<FileInfo> (required) — TestCommand intentionally diverges
// since the directory-mode default is the common case.
//
// Security note (threat T-35-10 — Information Disclosure): the directory
// glob is restricted to `test_*.flow` only via Directory.GetFiles + the
// SearchOption.TopDirectoryOnly flag — no recursion + name-pattern filter
// limits what files the user-supplied path can expose. Single-file mode
// reads exactly the path the user gave.
internal static class TestCommand
{
    public static Command Build()
    {
        var pathArg = new Argument<string?>("path")
        {
            Description = "Path to a test file or directory of test_*.flow files (default: tests/)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var cmd = new Command("test", "Run Flow test_*.flow files via the (test ...) framework");
        cmd.Add(pathArg);
        cmd.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArg) ?? "tests/";

            string[] files;
            if (Directory.Exists(path))
            {
                files = Directory.GetFiles(path, "test_*.flow", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    Console.Error.WriteLine($"No test_*.flow files found in directory: {path}");
                    return 1;
                }
                // Deterministic ordering — Directory.GetFiles is platform-
                // dependent (filesystem inode order on Linux; alphabetical on
                // some systems). Sort for reproducible PASS/FAIL output.
                Array.Sort(files, StringComparer.Ordinal);
            }
            else if (File.Exists(path))
            {
                files = new[] { path };
            }
            else
            {
                Console.Error.WriteLine($"Error: path not found: {path}");
                return 1;
            }

            int totalPassed = 0, totalFailed = 0;
            int filesWithExecuteErrors = 0;
            var runner = new TestRunner();
            foreach (var file in files)
            {
                using var engine = new FlowEngine(verbose: false);
                string source;
                try
                {
                    source = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  ERROR  {file}: read failed: {ex.Message}");
                    filesWithExecuteErrors++;
                    continue;
                }

                var executeOk = engine.Execute(source, file);
                if (!executeOk)
                {
                    Console.Error.WriteLine(
                        $"  ERROR  {file}: source did not execute cleanly — registration aborted");
                    Console.Error.WriteLine(engine.ErrorReporter.FormatErrors());
                    filesWithExecuteErrors++;
                    continue;
                }

                var (passed, failed) = runner.Run(engine, file);
                totalPassed += passed;
                totalFailed += failed;
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Total: {totalPassed + totalFailed}; Passed: {totalPassed}; Failed: {totalFailed}");
            return (totalFailed == 0 && filesWithExecuteErrors == 0) ? 0 : 1;
        });
        return cmd;
    }
}
