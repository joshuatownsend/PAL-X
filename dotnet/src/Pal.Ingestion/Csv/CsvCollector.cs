using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pal.Engine.Model;
using Pal.Engine.Normalization;

namespace Pal.Ingestion.Csv;

public sealed class CsvCollector
{
    private readonly MetricAliasRegistry _registry;

    public CsvCollector(MetricAliasRegistry registry)
    {
        _registry = registry;
    }

    public sealed class CollectResult
    {
        public required Dataset Dataset { get; init; }
        public required IReadOnlyList<string> Warnings { get; init; }
    }

    public CollectResult Collect(string filePath, string? machineName = null, string? timeZone = null)
    {
        using var reader = new StreamReader(filePath, new UTF8Encoding(false));
        var lines = ReadAllLines(reader);

        if (lines.Count < 2)
            throw new InvalidDataException($"CSV file '{filePath}' has no data rows.");

        var (headers, detectedMachine) = ParseHeader(lines[0]);
        machineName ??= detectedMachine;

        var warnings = new List<string>();
        var seriesBuilders = new List<(string counterPath, string canonicalMetric, string? instance, List<Sample> samples)>();

        for (int col = 1; col < headers.Count; col++)
        {
            string path = headers[col];
            string canonical = _registry.Resolve(path) ?? "unknown." + SanitizePath(path);
            string? instance = ExtractInstance(path);
            seriesBuilders.Add((path, canonical, instance, []));
        }

        DateTimeOffset? firstTs = null, lastTs = null;
        int gapCount = 0;
        DateTimeOffset? prevTs = null;
        double? dominantInterval = null;

        for (int lineIdx = 1; lineIdx < lines.Count; lineIdx++)
        {
            var cells = SplitCsvLine(lines[lineIdx]);
            if (cells.Count == 0 || string.IsNullOrWhiteSpace(cells[0])) continue;

            if (!TryParseTimestamp(cells[0], out var ts))
            {
                warnings.Add($"Line {lineIdx + 1}: Could not parse timestamp '{cells[0]}', skipping.");
                continue;
            }

            if (prevTs.HasValue)
            {
                double interval = (ts - prevTs.Value).TotalSeconds;
                if (dominantInterval is null) dominantInterval = interval;
                else if (Math.Abs(interval - dominantInterval.Value) > dominantInterval.Value * 0.5)
                    gapCount++;
            }
            prevTs = ts;
            firstTs ??= ts;
            lastTs = ts;

            for (int col = 1; col < headers.Count && col - 1 < seriesBuilders.Count; col++)
            {
                var (_, _, _, samples) = seriesBuilders[col - 1];
                string raw = col < cells.Count ? cells[col].Trim('"') : string.Empty;
                double? value = ParseValue(raw);
                samples.Add(new Sample(ts, value));
            }
        }

        if (firstTs is null)
            throw new InvalidDataException($"CSV file '{filePath}' contains no parseable data rows.");

        var allSeries = seriesBuilders
            .Select((b, idx) => new TimeSeries
            {
                SeriesId = $"ser_{idx + 1:D3}",
                CounterPathOriginal = b.counterPath,
                CanonicalMetric = b.canonicalMetric,
                Instance = b.instance,
                Unit = null,
                Samples = b.samples
            })
            .ToList();

        string datasetId = ComputeDatasetId(filePath);

        var dataset = new Dataset
        {
            DatasetId = datasetId,
            MachineName = machineName,
            TimeZone = timeZone,
            StartTimeUtc = firstTs.Value,
            EndTimeUtc = lastTs!.Value,
            SampleIntervalSeconds = dominantInterval ?? 0,
            GapCount = gapCount,
            Series = allSeries
        };

        return new CollectResult { Dataset = dataset, Warnings = warnings };
    }

    private static (List<string> headers, string? machineName) ParseHeader(string line)
    {
        var cells = SplitCsvLine(line);
        string? machineName = null;

        if (cells.Count > 0)
        {
            var first = cells[0].Trim('"');
            // PDH-CSV format: "(PDH-CSV 4.0)" or similar
            if (first.StartsWith("(PDH-CSV", StringComparison.OrdinalIgnoreCase))
            {
                // Extract machine name from second header if present
                if (cells.Count > 1)
                {
                    var path = cells[1].Trim('"');
                    machineName = ExtractMachineFromPath(path);
                }
            }
        }

        return (cells, machineName);
    }

    private static string? ExtractMachineFromPath(string counterPath)
    {
        // Format: \\MACHINE\Object\Counter or \Object\Counter
        if (!counterPath.StartsWith(@"\\")) return null;
        var rest = counterPath[2..];
        var slash = rest.IndexOf('\\');
        return slash > 0 ? rest[..slash] : null;
    }

    private static string? ExtractInstance(string counterPath)
    {
        var open = counterPath.LastIndexOf('(');
        var close = counterPath.LastIndexOf(')');
        if (open < 0 || close <= open) return null;
        var inst = counterPath[(open + 1)..close];
        return string.IsNullOrEmpty(inst) ? null : inst;
    }

    private static bool TryParseTimestamp(string raw, out DateTimeOffset result)
    {
        raw = raw.Trim('"').Trim();
        string[] formats =
        [
            "M/d/yyyy h:mm:ss.fff tt",
            "M/d/yyyy H:mm:ss.fff",
            "M/d/yyyy H:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ"
        ];
        return DateTimeOffset.TryParseExact(raw, formats,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result)
            || DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
    }

    private static double? ParseValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            return v;
        return null;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; current.Append(c); }
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    private static List<string> ReadAllLines(StreamReader reader)
    {
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        return lines;
    }

    private static string SanitizePath(string path) =>
        new string(path.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray())
            .Trim('_');

    private static string ComputeDatasetId(string filePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(filePath)));
        return "ds_" + Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }
}
