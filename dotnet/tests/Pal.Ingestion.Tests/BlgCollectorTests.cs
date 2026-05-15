using System.Runtime.Versioning;
using Pal.Engine.Normalization;
using Pal.Ingestion.Blg;
using Xunit;

namespace Pal.Ingestion.Tests;

[Trait("Category", "Windows")]
[SupportedOSPlatform("windows")]
public class BlgCollectorTests
{
    private static ITestOutputHelper Output => TestContext.Current.TestOutputHelper!;

    private static string FixtureRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "fixtures"));

    private static string BlgPath => Path.Combine(FixtureRoot, "cpu-pressure-blg", "input.blg");

    private static bool IsGitHubActions =>
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    private static void SkipIfNotWindows()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("BLG ingestion is Windows-only");
    }

    private static void SkipIfNoBlgFixture()
    {
        if (!File.Exists(BlgPath)) Assert.Skip($"BLG fixture missing at {BlgPath}");
    }

    private static void SkipIfCannotRunPdh()
    {
        SkipIfNotWindows();
        SkipIfNoBlgFixture();
    }

    [Fact]
    public void Collect_BlgFixture_ReturnsDataset()
    {
        SkipIfCannotRunPdh();

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new BlgCollector(registry);
        var result = collector.Collect(BlgPath);

        Assert.NotNull(result.Dataset);
        Assert.True(result.Dataset.SeriesCount > 0, "Expected at least one series");
        Assert.True(result.Dataset.SampleCount > 0, "Expected at least one sample");
        Assert.False(string.IsNullOrEmpty(result.InputDigest));
    }

    [Fact]
    public void Collect_BlgFixture_SamplesHaveUtcOffset()
    {
        SkipIfCannotRunPdh();

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new BlgCollector(registry);
        var result = collector.Collect(BlgPath);

        var samples = result.Dataset.Series
            .SelectMany(s => s.Samples)
            .Where(s => s.Value.HasValue)
            .ToList();

        Assert.NotEmpty(samples);
        Assert.Equal(TimeSpan.Zero, samples[0].Timestamp.Offset);
    }

    [Fact]
    public void Collect_BlgFixture_NormalizesProcessorMetric()
    {
        SkipIfCannotRunPdh();

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new BlgCollector(registry);
        var result = collector.Collect(BlgPath);

        // The BLG contains \Processor(_Total)\% Processor Time which maps to processor.percent_processor_time
        var cpuSeries = result.Dataset.Series
            .FirstOrDefault(s => s.CanonicalMetric == "processor.percent_processor_time");
        Assert.NotNull(cpuSeries);
    }

    [Fact]
    public void Collect_BlgFixture_ProducesValidTimeRange()
    {
        SkipIfCannotRunPdh();

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new BlgCollector(registry);
        var result = collector.Collect(BlgPath);

        Assert.True(result.Dataset.EndTimeUtc > result.Dataset.StartTimeUtc);
        Assert.True(result.Dataset.SampleIntervalSeconds > 0);
    }

    [Fact]
    public void CanHandle_BlgExtension_ReturnsTrue()
    {
        SkipIfNotWindows();
        var collector = new BlgCollector(MetricAliasRegistry.BuildDefault());
        Assert.True(collector.CanHandle("server.blg"));
        Assert.True(collector.CanHandle(@"C:\logs\server.BLG"));
        Assert.False(collector.CanHandle("data.csv"));
    }

    // Issue #41 diagnostic — runs the collector only on GHA and dumps the
    // observed failure mode (exception type/message or warnings + series count)
    // via ITestOutputHelper so CI logs surface the root cause. Always ends in
    // Assert.Skip so the test outcome is "skipped" rather than pass/fail —
    // we're collecting evidence, not asserting behavior.
    [Fact]
    public void Diagnostic_PdhInterop_OnGitHubActions()
    {
        SkipIfCannotRunPdh();
        if (!IsGitHubActions) Assert.Skip("Diagnostic runs only on GitHub Actions");

        Output.WriteLine($"[issue-41] OS: {Environment.OSVersion}");
        Output.WriteLine($"[issue-41] BLG fixture: {BlgPath}");
        Output.WriteLine($"[issue-41] BLG size: {new FileInfo(BlgPath).Length} bytes");
        Output.WriteLine($"[issue-41] Process arch: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new BlgCollector(registry);
        try
        {
            var result = collector.Collect(BlgPath);
            Output.WriteLine($"[issue-41] Collect succeeded — series={result.Dataset.SeriesCount}, samples={result.Dataset.SampleCount}");
            Output.WriteLine($"[issue-41] Warnings ({result.Warnings.Count}):");
            foreach (var w in result.Warnings) Output.WriteLine($"  - {w}");
            Output.WriteLine($"[issue-41] First 5 canonical metrics:");
            foreach (var s in result.Dataset.Series.Take(5))
                Output.WriteLine($"  - {s.CanonicalMetric} (path={s.CounterPathOriginal})");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"[issue-41] Collect threw {ex.GetType().FullName}: {ex.Message}");
            if (ex.InnerException is { } inner)
                Output.WriteLine($"[issue-41] Inner: {inner.GetType().FullName}: {inner.Message}");
            Output.WriteLine($"[issue-41] Stack:\n{ex.StackTrace}");
        }

        Assert.Skip("Diagnostic-only test for issue #41 — see output above");
    }
}
