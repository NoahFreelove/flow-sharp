# Deferred Items — Phase 46

## Plan 46-02 — out-of-scope, NOT fixed (pre-existing)

- **`WasmDeterminismTests.SameSource_TwoRuns_IdenticalStdout` + `SameSource_TwoRuns_IdenticalRunResultJson`** fail ONLY in the whole-suite `dotnet test` run; both PASS in isolation (`--filter FullyQualifiedName~Phase48.WasmDeterminismTests` → 2/2 PASS). Root cause is a Phase-48 test-isolation issue (a prior test's `Console.Out` redirection leaks into `WasmEntry`'s static shared-engine stdout capture). Documented as a known transient in `45-06-SUMMARY.md` ("2 transient whole-suite xUnit failures unrelated to Phase 45"). Zero reference to TimelineMap / SongRenderer / BarRenderer / SequenceRenderer — entirely independent of plan 46-02's dead-code removal. SCOPE BOUNDARY: not fixed here.
