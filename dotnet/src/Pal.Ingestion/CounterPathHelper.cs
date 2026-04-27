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
        int open = counterPath.LastIndexOf('(');
        int close = counterPath.LastIndexOf(')');
        if (open < 0 || close <= open) return null;
        string inst = counterPath[(open + 1)..close];
        return string.IsNullOrEmpty(inst) ? null : inst;
    }

    internal static string SanitizePath(string path) =>
        new string(path.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray())
            .Trim('_');

    internal static string MakeDatasetId(string inputDigest) => "ds_" + inputDigest[..16];
}
