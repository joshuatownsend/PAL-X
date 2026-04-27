using Pal.Engine.Normalization;
using Pal.Ingestion;
using Pal.Ingestion.Blg;
using Pal.Ingestion.Csv;
using Xunit;

namespace Pal.Ingestion.Tests;

public class CsvCollectorTests
{
    private static string FixtureRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "fixtures"));

    [Fact]
    public void Collect_CpuPressureFixture_ParsesExpectedSeriesCount()
    {
        var csvPath = Path.Combine(FixtureRoot, "cpu-pressure", "input.csv");
        if (!File.Exists(csvPath))
        {
            // Skip if fixtures not present (build machine may not have them at relative path)
            return;
        }

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new CsvCollector(registry);
        var result = collector.Collect(csvPath);

        Assert.Equal(4, result.Dataset.SeriesCount); // 4 counter columns
        Assert.Equal(20, result.Dataset.SampleCount / 4); // 20 rows per series
    }

    [Fact]
    public void Collect_CpuPressureFixture_NormalizesProcessorMetric()
    {
        var csvPath = Path.Combine(FixtureRoot, "cpu-pressure", "input.csv");
        if (!File.Exists(csvPath)) return;

        var registry = MetricAliasRegistry.BuildDefault();
        var collector = new CsvCollector(registry);
        var result = collector.Collect(csvPath);

        var cpuSeries = result.Dataset.Series
            .FirstOrDefault(s => s.CanonicalMetric == "processor.percent_processor_time");
        Assert.NotNull(cpuSeries);
        Assert.Equal("_Total", cpuSeries.Instance, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Collect_InvalidFile_ThrowsInvalidDataException()
    {
        var tmpPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpPath, "");
            var registry = MetricAliasRegistry.BuildDefault();
            var collector = new CsvCollector(registry);
            Assert.Throws<InvalidDataException>(() => collector.Collect(tmpPath));
        }
        finally { File.Delete(tmpPath); }
    }

    [Fact]
    public void BlgPlatformGuard_Collect_ThrowsPlatformNotSupported()
    {
        var guard = new BlgPlatformGuard();
        Assert.Throws<PlatformNotSupportedException>(() => guard.Collect("server.blg"));
    }

    [Fact]
    public void BlgPlatformGuard_ErrorMessageContainsRelogCommand()
    {
        var guard = new BlgPlatformGuard();
        var ex = Assert.Throws<PlatformNotSupportedException>(() =>
            guard.Collect(@"C:\logs\server.blg"));
        Assert.Contains("relog", ex.Message);
        Assert.Contains("-f CSV", ex.Message);
    }

    [Fact]
    public void CollectorFactory_CsvFormat_ReturnsCsvCollector()
    {
        var factory = CollectorFactory.Create("csv", MetricAliasRegistry.BuildDefault());
        Assert.IsType<CsvCollector>(factory);
    }

    [Fact]
    public void CollectorFactory_BlgFormat_OnWindows_ReturnsBlgCollector()
    {
        if (!OperatingSystem.IsWindows()) return;
        var factory = CollectorFactory.Create("blg", MetricAliasRegistry.BuildDefault());
        Assert.Equal("BlgCollector", factory.GetType().Name);
    }
}
