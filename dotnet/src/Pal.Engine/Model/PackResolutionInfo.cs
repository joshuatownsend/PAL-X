namespace Pal.Engine.Model;

public sealed class PackResolutionInfo
{
    public required string PackId { get; init; }
    public required string PackName { get; init; }
    public required string Version { get; init; }
    public required string ResolutionMode { get; init; }
}
