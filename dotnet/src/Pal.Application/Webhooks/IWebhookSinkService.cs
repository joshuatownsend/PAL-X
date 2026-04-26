using Pal.Application.Persistence;

namespace Pal.Application.Webhooks;

public interface IWebhookSinkService
{
    Task<IReadOnlyList<WebhookSinkDto>> ListAsync(CancellationToken ct = default);
    Task<WebhookSinkDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<WebhookSinkDto> CreateAsync(string name, string url, string? secret, bool enabled, IReadOnlyList<string> events, CancellationToken ct = default);
    Task<bool> UpdateAsync(Guid id, string name, string url, string? secret, bool enabled, IReadOnlyList<string> events, bool updateSecret = true, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
