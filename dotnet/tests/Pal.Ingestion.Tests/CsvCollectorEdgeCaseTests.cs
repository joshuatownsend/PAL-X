using Pal.Engine.Normalization;
using Pal.Ingestion.Csv;
using Xunit;

namespace Pal.Ingestion.Tests;

public class CsvCollectorEdgeCaseTests
{
    private static CollectResult CollectCsv(string content)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content);
            var collector = new CsvCollector(MetricAliasRegistry.BuildDefault());
            return collector.Collect(path);
        }
        finally { File.Delete(path); }
    }

    // A single counter column: the PDH-CSV header cell is col 0 (timestamp slot),
    // and col 1 is the counter path. The collector skips col 0 when building series.
    private const string Header =
        "\"(PDH-CSV 4.0)\",\"\\\\HOST\\Processor(_Total)\\% Processor Time\"";

    // Two-counter header for the short-row test.
    private const string TwoCounterHeader =
        "\"(PDH-CSV 4.0)\"," +
        "\"\\\\HOST\\Processor(_Total)\\% Processor Time\"," +
        "\"\\\\HOST\\Memory\\Available MBytes\"";

    // ── 1. Header-only file throws ──────────────────────────────────────────

    [Fact]
    public void HeaderOnly_ThrowsInvalidDataException()
    {
        // After blank-line stripping there is only 1 non-blank line → < 2.
        var ex = Assert.Throws<InvalidDataException>(() =>
            CollectCsv(Header + "\n"));
        Assert.Contains("has no data rows", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 2. Leading blank lines are skipped ─────────────────────────────────

    [Fact]
    public void LeadingBlankLines_AreSkippedAndCollectionSucceeds()
    {
        string csv = "\n\n" + Header + "\n\"2026-01-01 00:00:00\",10";
        var result = CollectCsv(csv);

        Assert.Equal(1, result.Dataset.SeriesCount);
        Assert.Equal(1, result.Dataset.SampleCount);
    }

    // ── 3. Unparseable timestamp row is skipped with a warning ─────────────

    [Fact]
    public void UnparseableTimestamp_IsSkippedWithWarning()
    {
        string csv =
            Header + "\n" +
            "\"not-a-date\",99\n" +
            "\"2026-01-01 00:00:00\",10";

        var result = CollectCsv(csv);

        // The bad row is warned and skipped; the valid row is collected.
        Assert.Contains(result.Warnings,
            w => w.Contains("Could not parse timestamp", StringComparison.OrdinalIgnoreCase));

        var series = result.Dataset.Series[0];
        Assert.Single(series.Samples);
        Assert.Equal(10.0, series.Samples[0].Value);
    }

    // ── 4. All-unparseable-timestamps throws ───────────────────────────────

    [Fact]
    public void AllUnparseableTimestamps_ThrowsInvalidDataException()
    {
        string csv =
            Header + "\n" +
            "\"bad-ts-1\",10\n" +
            "\"bad-ts-2\",20";

        var ex = Assert.Throws<InvalidDataException>(() => CollectCsv(csv));
        Assert.Contains("contains no parseable data rows", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── 5. Invariant number parsing ────────────────────────────────────────

    [Fact]
    public void ValidDoubleValue_ParsedCorrectly()
    {
        string csv = Header + "\n\"2026-01-01 00:00:00\",12.5";
        var result = CollectCsv(csv);

        Assert.Equal(12.5, result.Dataset.Series[0].Samples[0].Value);
    }

    [Fact]
    public void BlankValueCell_YieldsNullSample()
    {
        // Blank value column → ParseValue returns null.
        string csv = Header + "\n\"2026-01-01 00:00:00\",";
        var result = CollectCsv(csv);

        Assert.Null(result.Dataset.Series[0].Samples[0].Value);
    }

    // ── 6. Quoted value with embedded comma is one cell ────────────────────

    [Fact]
    public void QuotedValueWithEmbeddedComma_ParsedAsSingleCell()
    {
        // "1,234" is a single quoted field; the comma is inside quotes and
        // must NOT be treated as a column delimiter.
        string csv = Header + "\n\"2026-01-01 00:00:00\",\"1,234\"";
        var result = CollectCsv(csv);

        // Should still have exactly 1 series (the one counter column).
        Assert.Equal(1, result.Dataset.SeriesCount);
    }

    // ── 7. Short row yields null sample for the missing column ─────────────

    [Fact]
    public void ShortRow_MissingColumnYieldsNullSample()
    {
        // TwoCounterHeader has 2 counter columns (cols 1 and 2).
        // A data row with only timestamp + 1 value is "short" — col 2 is absent.
        // CsvCollector reads col 2 as string.Empty → ParseValue → null.
        string csv = TwoCounterHeader + "\n\"2026-01-01 00:00:00\",55";
        var result = CollectCsv(csv);

        Assert.Equal(2, result.Dataset.SeriesCount);

        var firstSeries = result.Dataset.Series[0];
        var secondSeries = result.Dataset.Series[1];

        // The supplied value reaches the first counter series.
        Assert.Equal(55.0, firstSeries.Samples[0].Value);

        // The missing cell for the second counter series is null — no exception.
        Assert.Null(secondSeries.Samples[0].Value);
    }
}
