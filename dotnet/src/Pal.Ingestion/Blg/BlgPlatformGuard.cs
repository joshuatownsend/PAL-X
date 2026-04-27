namespace Pal.Ingestion.Blg;

public sealed class BlgPlatformGuard : IDatasetCollector
{
    public bool CanHandle(string filePath) => false;

    public CollectResult Collect(string filePath, string? machineName = null, string? timeZone = null)
    {
        string stem = Path.GetFileNameWithoutExtension(filePath);
        string csvPath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", stem + ".csv");
        throw new PlatformNotSupportedException(
            $"BLG ingestion requires Windows (PDH interop). Convert your log first:\n" +
            $"  relog -f CSV \"{filePath}\" -o \"{csvPath}\"\n" +
            $"Then re-run with the CSV file.");
    }
}
