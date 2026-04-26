namespace Pal.Persistence.Entities;

public sealed class WebhookSinkEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Url { get; set; }
    // Stored plaintext in v1 (single-tenant self-hosted). Encrypt at rest in the multi-tenancy phase.
    public string? Secret { get; set; }
    public bool Enabled { get; set; }
    public required string Events { get; set; } // comma-separated: "alert.created,alert.escalated"
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
