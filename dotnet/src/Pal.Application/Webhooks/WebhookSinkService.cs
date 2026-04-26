using Pal.Application.Persistence;

namespace Pal.Application.Webhooks;

public sealed class WebhookSinkService : IWebhookSinkService
{
    private readonly IWebhookSinkRepository _repo;

    public WebhookSinkService(IWebhookSinkRepository repo) => _repo = repo;

    public Task<IReadOnlyList<WebhookSinkDto>> ListAsync(CancellationToken ct = default) => _repo.ListAsync(ct);

    public Task<WebhookSinkDto?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetAsync(id, ct);

    public async Task<WebhookSinkDto> CreateAsync(string name, string url, string? secret, bool enabled, IReadOnlyList<string> events, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sink = new WebhookSinkDto
        {
            Id = Guid.NewGuid(), Name = name, Url = url, Secret = secret,
            Enabled = enabled, Events = events, CreatedAt = now, UpdatedAt = now,
        };
        await _repo.CreateAsync(sink, ct);
        return sink;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string url, string? secret, bool enabled, IReadOnlyList<string> events, CancellationToken ct = default)
    {
        var existing = await _repo.GetAsync(id, ct);
        if (existing is null) return false;
        return await _repo.UpdateAsync(new WebhookSinkDto
        {
            Id = id, Name = name, Url = url, Secret = secret,
            Enabled = enabled, Events = events,
            CreatedAt = existing.CreatedAt, UpdatedAt = DateTimeOffset.UtcNow,
        }, ct);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
