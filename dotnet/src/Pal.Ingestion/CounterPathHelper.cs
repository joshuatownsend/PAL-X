namespace Pal.Ingestion;

internal static class CounterPathHelper
{
    // Format: \\MACHINE\Object(Instance)\Counter or \Object\Counter
    internal static string? ExtractMachineFromPath(string counterPath)
    {
        if (!counterPath.StartsWith(@"\\")) return null;
        var rest = counterPath[2..];
        int slash = rest.IndexOf('\\');
        return slash > 0 ? rest[..slash] : null;
    }

    internal static string? ExtractInstance(string counterPath)
    {
        // The instance lives in the object segment: \\MACHINE\Object(Instance)\Counter.
        // The counter name (after the final '\') may itself contain parentheses — e.g.
        // "I/O Database Reads (Attached) Average Latency" or "Lock Wait Time (ms)" — which
        // must NOT be mistaken for the instance. So search only the object segment.
        int lastSlash = counterPath.LastIndexOf('\\');
        string objectSegment = lastSlash >= 0 ? counterPath[..lastSlash] : counterPath;
        int open = objectSegment.LastIndexOf('(');
        int close = objectSegment.LastIndexOf(')');
        if (open < 0 || close <= open) return null;
        string inst = objectSegment[(open + 1)..close];
        return string.IsNullOrEmpty(inst) ? null : inst;
    }

    internal static string SanitizePath(string path) =>
        new string(path.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray())
            .Trim('_');

    internal static string MakeDatasetId(string inputDigest) => "ds_" + inputDigest[..16];
}
