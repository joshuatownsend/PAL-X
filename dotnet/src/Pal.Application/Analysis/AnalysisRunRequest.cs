namespace Pal.Application.Analysis;

public sealed class AnalysisRunRequest
{
    public required string InputPath { get; init; }
    public required string InputFormat { get; init; }
    public required IReadOnlyList<string> PackIds { get; init; }
    public required IReadOnlyList<string> PackDirs { get; init; }
    public bool AutoResolvePacks { get; init; }
    public double? HostMemoryMb { get; init; }
    public int? HostCpuCount { get; init; }
    public string? MachineName { get; init; }
    public string? TimeZone { get; init; }
    public string? HostContextSidecarPath { get; init; }
}
