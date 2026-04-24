namespace Pal.Persistence.Entities;

public sealed class PackVersionEntity
{
    public required string PackId { get; set; }
    public required string Version { get; set; }
    public required string StoragePath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public PackEntity Pack { get; set; } = null!;
}
