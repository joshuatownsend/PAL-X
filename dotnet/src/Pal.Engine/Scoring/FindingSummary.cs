using Pal.Engine.Model;

namespace Pal.Engine.Scoring;

/// <summary>Shared, single-source severity tally used by every report writer.</summary>
public static class FindingSummary
{
    public readonly record struct SeverityCounts(int Critical, int Warning, int Informational);

    /// <summary>
    /// Counts findings by severity. Anything that is neither "critical" nor
    /// "warning" is tallied as informational — matching the JSON report's
    /// historical behavior so output stays byte-identical.
    /// </summary>
    public static SeverityCounts CountSeverities(IReadOnlyList<Finding> findings)
    {
        int c = 0, w = 0, i = 0;
        foreach (var f in findings)
            if (f.Severity == "critical") c++;
            else if (f.Severity == "warning") w++;
            else i++;
        return new SeverityCounts(c, w, i);
    }
}
