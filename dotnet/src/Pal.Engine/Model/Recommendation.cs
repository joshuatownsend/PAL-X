namespace Pal.Engine.Model;

public sealed class Recommendation
{
    public required string Id { get; init; }
    public required string Priority { get; init; }
    public required string Text { get; init; }
    public string? Rationale { get; init; }
    public IReadOnlyList<string> Links { get; init; } = [];
}
