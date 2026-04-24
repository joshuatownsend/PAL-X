namespace Pal.Engine.Model;

public sealed class HostContext
{
    public static readonly HostContext Unknown = new();

    public double? TotalPhysicalMemoryMb { get; init; }
    public int? LogicalProcessorCount { get; init; }

    public bool IsUnknown => TotalPhysicalMemoryMb is null && LogicalProcessorCount is null;

    public double? Resolve(string variable) => variable switch
    {
        "total_physical_memory_mb" => TotalPhysicalMemoryMb,
        "logical_processor_count"  => LogicalProcessorCount,
        _ => null
    };
}
