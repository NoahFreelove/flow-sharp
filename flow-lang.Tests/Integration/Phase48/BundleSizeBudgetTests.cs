using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-05 — Bundle-size budget gate. Measures the actual Brotli-
/// compressed total of the FlowTarget=Web publish output and pins acceptance
/// against the D-48-05 ≤15 MB target / 20 MB hard cap (15 MB target + 5 MB
/// v1 latitude). Plan 48-01 measured 10.8 MB uncompressed (~5 MB Brotli'd
/// estimate); Plan 48-05 nails down the actual compressed size and writes
/// the measurement record to <c>48-BUNDLE-SIZE.md</c> as a planning artifact.
///
/// <para>Two xUnit Facts:</para>
/// <list type="number">
///   <item><c>CompressedBundle_BelowTargetSize</c> — soft-prints + hard-asserts
///         the compressed bundle total. Fails fact 1 with an actionable
///         escalation message when over the 20 MB hard cap.</item>
///   <item><c>BundleSizeReport_WrittenToDisk</c> — sorts files by compressed
///         size descending, takes top 20, writes a Markdown report to
///         <c>.planning/phases/48-wasm-runtime-webaudio-backend/48-BUNDLE-SIZE.md</c>
///         with the totals table + per-file table + auto-selected D-48-05
///         decision branch. Asserts the file exists + is non-empty.</item>
/// </list>
///
/// <para>Both Facts shell out to <c>dotnet publish</c> per the Plan 48-01
/// <see cref="WasmBuildPipelineTests"/> precedent. Each Fact runs its own
/// publish (warm cache after first; ~8s each). xUnit IClassFixture would
/// halve that but adds ceremony; explicit doubled-publish accepted per
/// T-48-19 in the threat register.</para>
///
/// <para>D-48-05 decision branches (selected at test-run time):</para>
/// <list type="bullet">
///   <item>compressed &lt; 15 MB — MONOLITHIC SHIP (Plan 48-05 closes;
///         v1.6 may revisit if stdlibs grow)</item>
///   <item>15 MB ≤ compressed ≤ 20 MB — SHIP WITH FOLLOW-UP (Plan 48-07
///         closer schedules v1.6 lazy-load)</item>
///   <item>compressed &gt; 20 MB — ESCALATION REQUIRED (Fact 1 fails;
///         Plan 48-05.1 split needed)</item>
/// </list>
/// </summary>
public class BundleSizeBudgetTests
{
    /// <summary>D-48-05 — soft target. Below this we ship monolithic.</summary>
    private const long TargetCompressedBytes = 15L * 1024L * 1024L;

    /// <summary>D-48-05 — hard cap. Above this Fact 1 fails + escalation triggered.</summary>
    private const long HardCapCompressedBytes = 20L * 1024L * 1024L;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "flow-lang", "flow-lang.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static (int exitCode, string stdout, string stderr) RunDotnetPublish(string args)
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "publish flow-lang/flow-lang.csproj " + args + " -v quiet --nologo",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(600_000))
        {
            try { p.Kill(); } catch { /* best-effort */ }
            return (-1, stdout, stderr + "\n[test] WaitForExit timed out at 10 minutes");
        }
        return (p.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Library publish produces flat <c>publish/</c> layout; Blazor-app
    /// publish nests under <c>publish/AppBundle/_framework/</c>. Mirrors
    /// the helper in <see cref="WasmBuildPipelineTests"/>.
    /// </summary>
    private static string LocateWasmFrameworkDir(string repoRoot)
    {
        var basePublish = Path.Combine(
            repoRoot, "flow-lang", "bin", "Release", "net10.0", "browser-wasm", "publish");
        if (File.Exists(Path.Combine(basePublish, "dotnet.js")))
            return basePublish;
        var appBundleFramework = Path.Combine(basePublish, "AppBundle", "_framework");
        if (File.Exists(Path.Combine(appBundleFramework, "dotnet.js")))
            return appBundleFramework;
        return basePublish;
    }

    /// <summary>
    /// Brotli-compresses the given bytes at <see cref="CompressionLevel.SmallestSize"/>
    /// (equivalent to Brotli level 11, the production setting most HTTP servers
    /// use for static asset compression). Returns the compressed length.
    /// </summary>
    private static long BrotliCompressedLength(byte[] bytes)
    {
        // Note: BrotliStream's default constructor disposes the underlying
        // stream when the BrotliStream is disposed. Pass leaveOpen=true so
        // we can read ms.Length AFTER the brotli writer flushes via Dispose.
        // (BrotliStream needs Dispose to flush its final block.)
        using var ms = new MemoryStream();
        using (var brotli = new BrotliStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(bytes, 0, bytes.Length);
        }
        return ms.Length;
    }

    /// <summary>
    /// File filter mirroring <see cref="WasmBuildPipelineTests.WasmBundle_UncompressedSize_MeasuredAndRecorded"/>:
    /// only browser-shippable artifacts contribute to the budget (.dll, .wasm,
    /// .js, .dat, .flow, .json, .md). Excluded by design: .a static archives
    /// (build-only), .c/.h/.rsp Emscripten driver scaffolding.
    /// </summary>
    private static bool IsBrowserShippedFile(FileInfo f) =>
        f.Extension == ".dll" || f.Extension == ".wasm" || f.Extension == ".js" ||
        f.Extension == ".dat" || f.Extension == ".flow" || f.Extension == ".json" ||
        f.Extension == ".md" || f.Extension == ".html";

    [Fact]
    public void CompressedBundle_BelowTargetSize()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0, $"Publish must succeed before measuring.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var frameworkDir = LocateWasmFrameworkDir(repoRoot);

        long totalUncompressed = 0;
        long totalCompressed = 0;
        foreach (var f in new DirectoryInfo(frameworkDir)
                     .EnumerateFiles("*", SearchOption.AllDirectories)
                     .Where(IsBrowserShippedFile))
        {
            byte[] bytes = File.ReadAllBytes(f.FullName);
            totalUncompressed += bytes.Length;
            totalCompressed += BrotliCompressedLength(bytes);
        }

        // Soft-print so CI captures the trend in test output.
        Console.WriteLine(
            $"[BundleSize] uncompressed total: {totalUncompressed / 1_000_000} MB ({totalUncompressed} bytes)");
        Console.WriteLine(
            $"[BundleSize] compressed total: {totalCompressed / 1_000_000} MB ({totalCompressed} bytes) — D-48-05 target ≤15 MB");

        // Hard-assert at 20 MB cap (15 MB target + 5 MB v1 latitude). Below this
        // we ship monolithic; above this Plan 48-05 escalates to a lazy-load split.
        Assert.True(totalCompressed < HardCapCompressedBytes,
            $"Compressed bundle {totalCompressed / 1_000_000} MB exceeds 20 MB hard cap " +
            $"(15 MB target + 5 MB v1 latitude). D-48-05 lazy-load REQUIRED — split " +
            $"flow-lang stdlibs into per-module WASM chunks loaded via runtime.loadStdlib(name).");
    }

    [Fact]
    public void BundleSizeReport_WrittenToDisk()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0, $"Publish must succeed before measuring.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var frameworkDir = LocateWasmFrameworkDir(repoRoot);

        // Build per-file size records (uncompressed, compressed, ratio).
        var records = new DirectoryInfo(frameworkDir)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(IsBrowserShippedFile)
            .Select(f =>
            {
                byte[] bytes = File.ReadAllBytes(f.FullName);
                long uncompressed = bytes.Length;
                long compressed = BrotliCompressedLength(bytes);
                // Relative path from frameworkDir for readable rendering.
                string relPath = Path.GetRelativePath(frameworkDir, f.FullName);
                return new { Path = relPath, Uncompressed = uncompressed, Compressed = compressed };
            })
            .OrderByDescending(r => r.Compressed)
            .ToList();

        long totalUncompressed = records.Sum(r => r.Uncompressed);
        long totalCompressed = records.Sum(r => r.Compressed);

        // Resolve the auto-selected D-48-05 decision branch.
        string decisionHeader;
        string decisionBody;
        if (totalCompressed < TargetCompressedBytes)
        {
            decisionHeader = "MONOLITHIC SHIP";
            decisionBody =
                "Compressed bundle is **under the 15 MB target**. Phase 48 ships without\n" +
                "runtime.loadStdlib(name) lazy-load. v1.6 may revisit if any single Phase 36 /\n" +
                "Phase 39 stdlib grows materially; Plan 48-05 closes the decision for v1.5.";
        }
        else if (totalCompressed <= HardCapCompressedBytes)
        {
            decisionHeader = "SHIP WITH FOLLOW-UP";
            decisionBody =
                "Compressed bundle is **over the 15 MB target but under the 20 MB hard cap**.\n" +
                "Phase 48 ships monolithic in v1.5; Plan 48-07 closer schedules a v1.6 lazy-load\n" +
                "follow-up. Track which top-N file contributors grew most between this measurement\n" +
                "and v1.6 baseline so the split lands where it actually saves bytes.";
        }
        else
        {
            decisionHeader = "ESCALATION REQUIRED";
            decisionBody =
                "Compressed bundle is **over the 20 MB hard cap**. Fact 1 fails; Plan 48-05 returns\n" +
                "`## PHASE SPLIT RECOMMENDED` to the orchestrator. Plan 48-05.1 introduces\n" +
                "`runtime.loadStdlib(name)` + per-stdlib WASM module split for @patterns,\n" +
                "@generative, @improv, @notation-io.";
        }

        // Resolve git rev for traceability — silent fall-back to "unknown" if git missing.
        string gitSha;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var gp = Process.Start(psi)!;
            string shaOut = gp.StandardOutput.ReadToEnd().Trim();
            gp.WaitForExit(5_000);
            gitSha = string.IsNullOrEmpty(shaOut) ? "unknown" : shaOut;
        }
        catch (Exception)
        {
            gitSha = "unknown";
        }

        var sb = new StringBuilder();
        sb.AppendLine("# Phase 48 — WASM Bundle Size Measurement");
        sb.AppendLine();
        sb.AppendLine("> Auto-generated by `flow-lang.Tests/Integration/Phase48/BundleSizeBudgetTests.BundleSizeReport_WrittenToDisk`.");
        sb.AppendLine("> Re-run `dotnet test --filter Phase48.BundleSizeBudgetTests` to refresh.");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine($"**Source SHA:** {gitSha}");
        sb.AppendLine($"**Publish command:** `dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release`");
        sb.AppendLine();
        sb.AppendLine("## Totals");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Uncompressed total | {totalUncompressed / 1_000_000} MB ({totalUncompressed:N0} bytes) |");
        sb.AppendLine($"| Brotli-compressed total | {totalCompressed / 1_000_000} MB ({totalCompressed:N0} bytes) |");
        sb.AppendLine($"| Compression ratio | {(totalUncompressed > 0 ? (double)totalCompressed / totalUncompressed * 100.0 : 0.0):F1}% |");
        sb.AppendLine("| D-48-05 target (compressed) | ≤15 MB |");
        sb.AppendLine("| Hard cap (compressed) | 20 MB |");
        sb.AppendLine($"| Margin to target | {(TargetCompressedBytes - totalCompressed) / 1_000_000} MB |");
        sb.AppendLine($"| Margin to hard cap | {(HardCapCompressedBytes - totalCompressed) / 1_000_000} MB |");
        sb.AppendLine($"| File count (browser-shipped) | {records.Count} |");
        sb.AppendLine();
        sb.AppendLine("## Top 20 Files by Compressed Size");
        sb.AppendLine();
        sb.AppendLine("| File | Uncompressed | Brotli | Ratio |");
        sb.AppendLine("|------|-------------:|-------:|------:|");
        foreach (var r in records.Take(20))
        {
            double ratio = r.Uncompressed > 0 ? (double)r.Compressed / r.Uncompressed * 100.0 : 0.0;
            string unc = r.Uncompressed >= 1_000_000
                ? $"{r.Uncompressed / 1_000_000.0:F2} MB"
                : $"{r.Uncompressed / 1_000.0:F1} KB";
            string cmp = r.Compressed >= 1_000_000
                ? $"{r.Compressed / 1_000_000.0:F2} MB"
                : $"{r.Compressed / 1_000.0:F1} KB";
            sb.AppendLine($"| `{r.Path}` | {unc} | {cmp} | {ratio:F1}% |");
        }
        sb.AppendLine();
        sb.AppendLine("## D-48-05 Lazy-Load Decision");
        sb.AppendLine();
        sb.AppendLine($"**Selected branch:** {decisionHeader}");
        sb.AppendLine();
        sb.AppendLine(decisionBody);
        sb.AppendLine();
        sb.AppendLine("### Decision Branches Reference");
        sb.AppendLine();
        sb.AppendLine("- **MONOLITHIC SHIP** (compressed < 15 MB): v1.6 may revisit; Plan 48-05 closes.");
        sb.AppendLine("- **SHIP WITH FOLLOW-UP** (15 MB ≤ compressed ≤ 20 MB): Plan 48-07 schedules v1.6 lazy-load.");
        sb.AppendLine("- **ESCALATION REQUIRED** (compressed > 20 MB): Fact 1 fails; Plan 48-05.1 split needed.");
        sb.AppendLine();
        sb.AppendLine("## Notes");
        sb.AppendLine();
        sb.AppendLine("- **Measurement scope:** Browser-shipped artifacts only (`.dll`, `.wasm`, `.js`, `.dat`, `.flow`, `.json`, `.md`, `.html`).");
        sb.AppendLine("- **Excluded:** Static-link archives (`.a`, build-only, ~28 MB), Emscripten driver scaffolding (`.c`/`.h`/`.rsp`).");
        sb.AppendLine("- **Compression:** `System.IO.Compression.BrotliStream` at `CompressionLevel.SmallestSize`");
        sb.AppendLine("  (equivalent to Brotli quality 11 — the production setting most HTTP servers use for static assets).");
        sb.AppendLine("- **Auto-regenerated:** Every test run overwrites this file. Git history tracks drift over time.");
        sb.AppendLine();

        var reportPath = Path.Combine(
            repoRoot,
            ".planning",
            "phases",
            "48-wasm-runtime-webaudio-backend",
            "48-BUNDLE-SIZE.md");

        File.WriteAllText(reportPath, sb.ToString());

        Assert.True(File.Exists(reportPath),
            $"BundleSizeReport: expected file written at {reportPath}");
        long writtenLen = new FileInfo(reportPath).Length;
        Assert.True(writtenLen > 0, "BundleSizeReport: expected non-empty file");
        Console.WriteLine(
            $"[BundleSize] report written: {reportPath} ({writtenLen} bytes, decision={decisionHeader})");
    }
}
