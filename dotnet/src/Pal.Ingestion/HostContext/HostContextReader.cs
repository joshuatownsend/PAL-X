using System.Text.Json;
using Pal.Engine.Model;

namespace Pal.Ingestion.HostContext;

public static class HostContextReader
{
    public static Engine.Model.HostContext Read(
        double? memoryMb,
        int? logicalProcessors,
        string? sidecarPath)
    {
        if (memoryMb.HasValue || logicalProcessors.HasValue)
        {
            return new Engine.Model.HostContext
            {
                TotalPhysicalMemoryMb = memoryMb,
                LogicalProcessorCount = logicalProcessors
            };
        }

        if (sidecarPath is not null && File.Exists(sidecarPath))
        {
            var sidecar = JsonSerializer.Deserialize<HostContextSidecar>(
                File.ReadAllText(sidecarPath));
            if (sidecar is not null)
            {
                return new Engine.Model.HostContext
                {
                    TotalPhysicalMemoryMb = sidecar.TotalPhysicalMemoryMb,
                    LogicalProcessorCount = sidecar.LogicalProcessorCount
                };
            }
        }

        return Engine.Model.HostContext.Unknown;
    }

    private sealed class HostContextSidecar
    {
        public double? TotalPhysicalMemoryMb { get; init; }
        public int? LogicalProcessorCount { get; init; }
    }
}
