using Pal.Engine.Model;

namespace Pal.Engine.Scoring;

public static class StatusClassifier
{
    public static ReportStatus ClassifyOverall(IReadOnlyList<Finding> findings)
    {
        if (findings.Any(f => f.Severity == "critical")) return ReportStatus.Critical;
        if (findings.Any(f => f.Severity == "warning")) return ReportStatus.Warning;
        return ReportStatus.Healthy;
    }

    public static IReadOnlyDictionary<string, ReportStatus> ClassifyByCategory(IReadOnlyList<Finding> findings)
    {
        return findings
            .GroupBy(f => f.Category)
            .ToDictionary(
                g => g.Key,
                g => ClassifyOverall(g.ToList()));
    }
}
