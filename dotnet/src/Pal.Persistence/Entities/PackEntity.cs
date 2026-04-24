namespace Pal.Persistence.Entities;

public sealed class PackEntity
{
    public required string Id { get; set; }
    public required string CurrentVersion { get; set; }
    public required string Title { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<PackVersionEntity> Versions { get; set; } = [];
}
