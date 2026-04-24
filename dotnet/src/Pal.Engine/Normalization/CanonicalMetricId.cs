namespace Pal.Engine.Normalization;

public static class CanonicalMetricId
{
    public static bool IsValid(string id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id == id.ToLowerInvariant() &&
        !id.Contains(' ') &&
        !id.Contains('%');

    public static string ExtractInstance(string counterPath)
    {
        var open = counterPath.LastIndexOf('(');
        var close = counterPath.LastIndexOf(')');
        if (open < 0 || close <= open) return string.Empty;
        return counterPath[(open + 1)..close];
    }

    public static string StripInstance(string counterPath)
    {
        var open = counterPath.LastIndexOf('(');
        if (open < 0) return counterPath;
        var obj = counterPath[..open];
        var counter = counterPath[(counterPath.LastIndexOf(')') + 1)..];
        return obj + counter;
    }
}
