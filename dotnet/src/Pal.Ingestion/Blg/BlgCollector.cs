using System.Runtime.Versioning;
using System.Security.Cryptography;
using Pal.Engine.Model;
using Pal.Engine.Normalization;

namespace Pal.Ingestion.Blg;

[SupportedOSPlatform("windows")]
public sealed class BlgCollector : IDatasetCollector
{
    private readonly MetricAliasRegistry _registry;

    public BlgCollector(MetricAliasRegistry registry) => _registry = registry;

    public bool CanHandle(string filePath) =>
        Path.GetExtension(filePath).Equals(".blg", StringComparison.OrdinalIgnoreCase);

    public CollectResult Collect(string filePath, string? machineName = null, string? timeZone = null)
    {
        int hr = PdhInterop.PdhBindInputDataSourceW(out IntPtr hDataSource, filePath);
        PdhInterop.ThrowIfFailed(hr, "PdhBindInputDataSourceW");

        try
        {
            return CollectCore(hDataSource, filePath, machineName, timeZone);
        }
        finally
        {
            PdhInterop.PdhCloseLog(hDataSource, 0);
        }
    }

    private CollectResult CollectCore(IntPtr hDataSource, string filePath, string? machineName, string? timeZone)
    {
        int hr = PdhInterop.PdhOpenQueryH(hDataSource, IntPtr.Zero, out IntPtr hQuery);
        PdhInterop.ThrowIfFailed(hr, "PdhOpenQueryH");

        try
        {
            return RunQuery(hDataSource, hQuery, filePath, machineName, timeZone);
        }
        finally
        {
            PdhInterop.PdhCloseQuery(hQuery);
        }
    }

    private CollectResult RunQuery(IntPtr hDataSource, IntPtr hQuery, string filePath, string? machineName, string? timeZone)
    {
        var warnings = new List<string>();

        var counterPaths = EnumerateCounterPaths(hDataSource, warnings);

        var counters = new List<(string path, IntPtr handle, string canonicalMetric, string? instance, List<Sample> samples)>();
        foreach (var path in counterPaths)
        {
            int hr = PdhInterop.PdhAddCounterW(hQuery, path, IntPtr.Zero, out IntPtr hCounter);
            if (hr != 0)
            {
                warnings.Add($"Skipped counter '{path}': 0x{hr:X8}");
                continue;
            }
            string canonical = _registry.Resolve(path) ?? "unknown." + CounterPathHelper.SanitizePath(path);
            string? instance = CounterPathHelper.ExtractInstance(path);
            counters.Add((path, hCounter, canonical, instance, []));
        }

        if (counters.Count == 0)
            throw new InvalidDataException($"BLG file '{filePath}' contains no readable counters.");

        DateTimeOffset? firstTs = null, lastTs = null;
        DateTimeOffset? prevTs = null;
        double? dominantInterval = null;
        int gapCount = 0;

        while (true)
        {
            int hr = PdhInterop.PdhCollectQueryDataWithTime(hQuery, out long fileTime);
            if (hr == PdhInterop.PDH_NO_MORE_DATA || hr == PdhInterop.PDH_NO_DATA)
                break;
            if (hr != 0)
            {
                warnings.Add($"PdhCollectQueryDataWithTime: 0x{hr:X8}");
                break;
            }

            // FILETIME → UTC: DateTime.FromFileTimeUtc produces Kind=Utc; wrap in explicit zero offset.
            var ts = new DateTimeOffset(DateTime.FromFileTimeUtc(fileTime), TimeSpan.Zero);
            firstTs ??= ts;

            if (prevTs.HasValue)
            {
                double interval = (ts - prevTs.Value).TotalSeconds;
                if (dominantInterval is null)
                    dominantInterval = interval;
                else if (Math.Abs(interval - dominantInterval.Value) > dominantInterval.Value * 0.5)
                    gapCount++;
            }
            prevTs = ts;
            lastTs = ts;

            foreach (var (_, hCounter, _, _, samples) in counters)
            {
                int status = PdhInterop.PdhGetFormattedCounterValue(
                    hCounter,
                    PdhInterop.PDH_FMT_DOUBLE | PdhInterop.PDH_FMT_NOCAP100,
                    out _,
                    out PdhInterop.PdhFmtCountervalue val);

                double? value = (status == 0 &&
                    (val.CStatus == PdhInterop.PDH_CSTATUS_VALID_DATA ||
                     val.CStatus == PdhInterop.PDH_CSTATUS_NEW_DATA))
                    ? val.doubleValue
                    : null;

                samples.Add(new Sample(ts, value));
            }
        }

        if (firstTs is null)
            throw new InvalidDataException($"BLG file '{filePath}' contains no data.");

        string inputDigest;
        using (var stream = File.OpenRead(filePath))
        {
            var hashBytes = SHA256.HashData(stream);
            inputDigest = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        if (machineName is null && counters.Count > 0)
            machineName = CounterPathHelper.ExtractMachineFromPath(counters[0].path);

        var allSeries = counters
            .Select((c, idx) => new TimeSeries
            {
                SeriesId = $"ser_{idx + 1:D3}",
                CounterPathOriginal = c.path,
                CanonicalMetric = c.canonicalMetric,
                Instance = c.instance,
                Unit = null,
                Samples = c.samples
            })
            .ToList();

        return new CollectResult
        {
            Dataset = new Dataset
            {
                DatasetId = CounterPathHelper.MakeDatasetId(inputDigest),
                MachineName = machineName,
                TimeZone = timeZone,
                StartTimeUtc = firstTs.Value,
                EndTimeUtc = lastTs!.Value,
                SampleIntervalSeconds = dominantInterval ?? 0,
                GapCount = gapCount,
                Series = allSeries
            },
            Warnings = warnings,
            InputDigest = inputDigest
        };
    }

    private static List<string> EnumerateCounterPaths(IntPtr hDataSource, List<string> warnings)
    {
        // Two wildcard patterns cover all counter types:
        // \*\*     → non-instanced (Memory, System, etc.)
        // \*(*)\*  → instanced (Processor(_Total), PhysicalDisk(_Total), etc.)
        // Probe returns PDH_INSUFFICIENT_BUFFER (0x800007D2) with required char count;
        // fill call (correctly-sized buffer) returns 0.
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExpandWildcard(hDataSource, @"\*\*", paths, warnings);
        ExpandWildcard(hDataSource, @"\*(*)\*", paths, warnings);
        return paths.ToList();
    }

    private static void ExpandWildcard(IntPtr hDataSource, string wildcard, HashSet<string> paths, List<string> warnings)
    {
        int size = 0;
        int hr = PdhInterop.PdhExpandWildCardPathHW(hDataSource, wildcard, null, ref size, 0);

        if (hr != PdhInterop.PDH_INSUFFICIENT_BUFFER && hr != PdhInterop.PDH_MORE_DATA && hr != 0)
        {
            warnings.Add($"PdhExpandWildCardPathHW('{wildcard}') probe: 0x{hr:X8}");
            return;
        }
        if (size <= 0) return;

        var buf = new char[size];
        hr = PdhInterop.PdhExpandWildCardPathHW(hDataSource, wildcard, buf, ref size, 0);
        if (hr != 0)
        {
            warnings.Add($"PdhExpandWildCardPathHW('{wildcard}') fill: 0x{hr:X8}");
            return;
        }

        foreach (var path in ParseMultiSz(buf))
            paths.Add(path);
    }

    private static List<string> ParseMultiSz(char[] buf)
    {
        var result = new List<string>();
        int start = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] == '\0')
            {
                if (i > start)
                    result.Add(new string(buf, start, i - start));
                else
                    break; // double-null terminator
                start = i + 1;
            }
        }
        return result;
    }

}
