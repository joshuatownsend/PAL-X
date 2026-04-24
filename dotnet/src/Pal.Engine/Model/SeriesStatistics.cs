namespace Pal.Engine.Model;

public sealed class SeriesStatistics
{
    public required int Count { get; init; }
    public required int MissingSampleCount { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public required double Avg { get; init; }
    public required double Median { get; init; }
    public required double P90 { get; init; }
    public required double P95 { get; init; }
    public required double P99 { get; init; }
    public required double StdDev { get; init; }
    public required double TrendPerHour { get; init; }
}
