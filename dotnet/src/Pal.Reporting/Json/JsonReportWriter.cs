using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pal.Engine.Model;
using Pal.Engine.Rules;
using Pal.Engine.Scoring;

namespace Pal.Reporting.Json;

public sealed class JsonReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public sealed class WriteInput
    {
        public required Dataset Dataset { get; init; }
        public required IReadOnlyList<Finding> Findings { get; init; }
        public required IReadOnlyList<PackResolutionInfo> PackResolutions { get; init; }
        public required IReadOnlyList<RuleEngine.EngineWarning> EngineWarnings { get; init; }
        public required IReadOnlyList<string> CollectorWarnings { get; init; }
        public required string InputPath { get; init; }
        public required string OutputPath { get; init; }
        public required string? HtmlReportPath { get; init; }
        public required long DurationMs { get; init; }
        public required DateTimeOffset GeneratedAt { get; init; }
        public required string InputDigest { get; init; }
    }

    public void Write(WriteInput input)
    {
        var doc = BuildDocument(input);
        using var stream = new FileStream(input.OutputPath, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(JsonSerializer.Serialize(doc, JsonOptions));
    }

    public void WriteToStream(WriteInput input, Stream destination)
    {
        var doc = BuildDocument(input);
        using var writer = new StreamWriter(destination, new UTF8Encoding(false), leaveOpen: true);
        writer.Write(JsonSerializer.Serialize(doc, JsonOptions));
    }

    private static object BuildDocument(WriteInput input)
    {
        var overall = StatusClassifier.ClassifyOverall(input.Findings);
        var byCategory = StatusClassifier.ClassifyByCategory(input.Findings);

        string reportId = ComputeReportId(input.InputDigest, input.PackResolutions);
        string sourceType = Path.GetExtension(input.InputPath).TrimStart('.').ToLowerInvariant();

        var warnings = new List<object>();
        foreach (var w in input.CollectorWarnings)
            warnings.Add(new { code = "collector.warning", message = w, severity = "warning" });
        foreach (var w in input.EngineWarnings)
            warnings.Add(new { code = w.Code, message = w.Message, severity = w.Severity });

        string analysisStatus = warnings.Count > 0 ? "completed_with_warnings" : "completed";
        var (nCrit, nWarn, nInfo) = CountSeverities(input.Findings);

        return new
        {
            schema_version = "pal.report/v1",
            report_id = reportId,
            generated_at_utc = input.GeneratedAt.UtcDateTime,
            engine = new
            {
                name = "PAL",
                version = "2026.2.0",
                runtime = $".NET {System.Environment.Version.Major}.{System.Environment.Version.Minor}",
                host_os = System.Environment.OSVersion.ToString(),
                execution_mode = "cli",
                duration_ms = input.DurationMs
            },
            input = new
            {
                source_type = sourceType,
                source_path = Path.GetFileName(input.InputPath),
                source_count = 1,
                collector = $"Pal.Collectors.{char.ToUpperInvariant(sourceType[0])}{sourceType[1..]}",
                collector_version = "1.0.0"
            },
            dataset = new
            {
                dataset_id = input.Dataset.DatasetId,
                machine_name = input.Dataset.MachineName,
                time_zone = input.Dataset.TimeZone,
                start_time_utc = input.Dataset.StartTimeUtc.UtcDateTime,
                end_time_utc = input.Dataset.EndTimeUtc.UtcDateTime,
                sample_interval_seconds = input.Dataset.SampleIntervalSeconds,
                series_count = input.Dataset.SeriesCount,
                sample_count = input.Dataset.SampleCount,
                gap_count = input.Dataset.GapCount
            },
            packs = input.PackResolutions.Select(p => new
            {
                pack_id = p.PackId,
                pack_name = p.PackName,
                version = p.Version,
                resolution_mode = p.ResolutionMode
            }).ToList(),
            warnings,
            summary = new
            {
                overall_status = overall.ToString().ToLowerInvariant(),
                finding_counts = new
                {
                    critical = nCrit,
                    warning = nWarn,
                    informational = nInfo
                },
                category_statuses = byCategory.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToString().ToLowerInvariant()),
                analysis_status = analysisStatus
            },
            findings = input.Findings.Select(MapFinding).ToList(),
            series_index = input.Dataset.Series.Select(MapSeries).ToList(),
            artifacts = new
            {
                json_report_path = Path.GetFileName(input.OutputPath),
                html_report_path = input.HtmlReportPath is not null ? Path.GetFileName(input.HtmlReportPath) : null
            }
        };
    }

    private static object MapFinding(Finding f) => new
    {
        finding_id = f.FindingId,
        pack_id = f.PackId,
        rule_id = f.RuleId,
        severity = f.Severity,
        category = f.Category,
        title = f.Title,
        summary = f.Summary,
        explanation = f.Explanation,
        time_window = f.WindowStart.HasValue ? new
        {
            start_time_utc = f.WindowStart.Value.UtcDateTime,
            end_time_utc = f.WindowEnd!.Value.UtcDateTime
        } : null,
        evidence = new
        {
            metrics = f.EvidenceMetrics.Select(m => new
            {
                series_id = m.SeriesId,
                canonical_metric = m.CanonicalMetric,
                statistics = MapStats(m.Statistics),
                trigger_details = m.TriggerDetails.Select(td => new
                {
                    expression = td.Expression,
                    result = td.Result,
                    actual_value = td.ActualValue,
                    expected_value = td.ExpectedValue
                }).ToList()
            }).ToList()
        },
        recommendations = f.Recommendations.Select(r => new
        {
            id = r.Id,
            priority = r.Priority,
            text = r.Text,
            rationale = r.Rationale,
            links = r.Links.Count > 0 ? (object)r.Links : null
        }).ToList()
    };

    private static object MapSeries(TimeSeries s)
    {
        var stats = s.Statistics;
        return new
        {
            series_id = s.SeriesId,
            counter_path_original = s.CounterPathOriginal,
            canonical_metric = s.CanonicalMetric,
            unit = s.Unit,
            statistics = stats is null ? null : MapStats(stats)
        };
    }

    private static object MapStats(SeriesStatistics s) => new
    {
        count = s.Count,
        min = s.Min,
        max = s.Max,
        avg = s.Avg,
        median = s.Median,
        p90 = s.P90,
        p95 = s.P95,
        p99 = s.P99,
        stddev = s.StdDev,
        trend_per_hour = s.TrendPerHour,
        missing_sample_count = s.MissingSampleCount
    };

    private static (int critical, int warning, int info) CountSeverities(IReadOnlyList<Finding> findings)
    {
        int c = 0, w = 0, i = 0;
        foreach (var f in findings)
            if (f.Severity == "critical") c++;
            else if (f.Severity == "warning") w++;
            else i++;
        return (c, w, i);
    }

    private static string ComputeReportId(string inputDigest, IReadOnlyList<PackResolutionInfo> packs)
    {
        var parts = new List<string> { inputDigest };
        parts.AddRange(packs.OrderBy(p => p.PackId).Select(p => $"{p.PackId}@{p.Version}"));
        var combined = string.Join("|", parts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return "rep_" + Convert.ToHexString(hash[..10]).ToLowerInvariant();
    }
}
