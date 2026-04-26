using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pal.Application.Persistence;
using Pal.Application.Webhooks;

namespace Pal.Api.Services;

public sealed class NotificationService : INotificationService
{
    private readonly IWebhookSinkRepository _repo;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<NotificationService> _logger;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public NotificationService(IWebhookSinkRepository repo, IHttpClientFactory httpFactory, ILogger<NotificationService> logger)
    {
        _repo = repo;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task NotifyAsync(string @event, AlertDto alert, CancellationToken ct = default)
    {
        var sinks = await _repo.ListEnabledForEventAsync(@event, ct);
        if (sinks.Count == 0) return;

        var payload = BuildPayload(@event, alert);
        foreach (var sink in sinks)
        {
            try
            {
                await DeliverAsync(sink, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Webhook delivery failed for sink {SinkId} ({SinkName}) on event {Event}",
                    sink.Id, sink.Name, @event);
            }
        }
    }

    public async Task<int?> TestAsync(Guid sinkId, CancellationToken ct = default)
    {
        var sink = await _repo.GetAsync(sinkId, ct);
        if (sink is null) return null;

        var payload = BuildPayload("webhook.test", new AlertDto
        {
            Id = Guid.Empty, RuleId = "test-rule", Severity = "informational",
            Category = "system", Title = "PAL Webhook Test", Status = "open",
            TriggeringJobId = Guid.Empty, LatestJobId = Guid.Empty,
            TriggeredAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow,
        });
        return (int)await DeliverAsync(sink, payload, ct);
    }

    private async Task<System.Net.HttpStatusCode> DeliverAsync(WebhookSinkDto sink, byte[] payload, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, sink.Url)
        {
            Content = new ByteArrayContent(payload)
        };
        req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8");

        if (!string.IsNullOrEmpty(sink.Secret))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sink.Secret));
            req.Headers.Add("X-PAL-Signature",
                $"sha256={Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant()}");
        }

        using var resp = await _httpFactory.CreateClient("pal-webhook").SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            _logger.LogWarning("Sink {SinkName} returned HTTP {StatusCode}", sink.Name, (int)resp.StatusCode);
        return resp.StatusCode;
    }

    private static byte[] BuildPayload(string @event, AlertDto alert) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { @event, timestamp = DateTimeOffset.UtcNow, alert }, _json));
}
