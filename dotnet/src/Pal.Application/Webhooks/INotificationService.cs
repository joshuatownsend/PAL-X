using Pal.Application.Persistence;

namespace Pal.Application.Webhooks;

public interface INotificationService
{
    Task NotifyAsync(string @event, AlertDto alert, CancellationToken ct = default);
    // Returns null if the sink was not found; otherwise the HTTP status code received from the endpoint.
    Task<int?> TestAsync(Guid sinkId, CancellationToken ct = default);
}
