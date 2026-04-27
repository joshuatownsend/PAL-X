using Pal.Engine.Normalization;
using Pal.Ingestion.Blg;
using Xunit;

namespace Pal.Ingestion.Tests;

[Trait("Category", "Windows")]
public class BlgCollectorTests
{
    private static string FixtureRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "fixtures"));

    private static string BlgPath => Path.Combine(FixtureRoot, "cpu-pressure-blg", "input.blg");

    [Fact]
    public void Collect_BlgFixture_ReturnsDataset()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!File.Exists(BlgPath)) return;

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
        if (!OperatingSystem.IsWindows()) return;
        if (!File.Exists(BlgPath)) return;

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
        if (!OperatingSystem.IsWindows()) return;
        if (!File.Exists(BlgPath)) return;

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
        if (!OperatingSystem.IsWindows()) return;
        if (!File.Exists(BlgPath)) return;

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new BlgCollector(registry);
        var result = collector.Collect(BlgPath);

        Assert.True(result.Dataset.EndTimeUtc > result.Dataset.StartTimeUtc);
        Assert.True(result.Dataset.SampleIntervalSeconds > 0);
    }

    [Fact]
    public void CanHandle_BlgExtension_ReturnsTrue()
    {
        if (!OperatingSystem.IsWindows()) return;
        var collector = new BlgCollector(MetricAliasRegistry.BuildDefault());
        Assert.True(collector.CanHandle("server.blg"));
        Assert.True(collector.CanHandle(@"C:\logs\server.BLG"));
        Assert.False(collector.CanHandle("data.csv"));
    }
}
