using Pal.Engine.Model;

namespace Pal.Ingestion;

public interface IDatasetCollector
{
    bool CanHandle(string filePath);
    CollectResult Collect(string filePath, string? machineName = null, string? timeZone = null);
}

public sealed class CollectResult
{
    public required Dataset Dataset { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required string InputDigest { get; init; }
}
