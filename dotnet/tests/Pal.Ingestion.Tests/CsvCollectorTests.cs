using Pal.Engine.Normalization;
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
    public void Collect_BlgFile_StubThrowsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            Pal.Ingestion.Blg.BlgCollectorStub.ThrowNotSupported("server.blg"));
    }

    [Fact]
    public void BlgStub_ErrorMessageContainsRelogCommand()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            Pal.Ingestion.Blg.BlgCollectorStub.ThrowNotSupported(@"C:\logs\server.blg"));
        Assert.Contains("relog", ex.Message);
        Assert.Contains("-f CSV", ex.Message);
    }
}
