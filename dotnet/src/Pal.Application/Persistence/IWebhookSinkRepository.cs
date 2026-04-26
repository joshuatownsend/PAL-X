namespace Pal.Application.Persistence;

public interface IWebhookSinkRepository
{
    Task<IReadOnlyList<WebhookSinkDto>> ListAsync(CancellationToken ct = default);
    Task<WebhookSinkDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task CreateAsync(WebhookSinkDto sink, CancellationToken ct = default);
    Task<bool> UpdateAsync(WebhookSinkDto sink, bool updateSecret, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookSinkDto>> ListEnabledForEventAsync(string eventName, CancellationToken ct = default);
}
