using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Pal.Api.Services;
using Pal.Application.Persistence;
using Pal.Persistence;
using Xunit;

namespace Pal.Api.Tests;

public class NotificationServiceTests
{
    private static WebhookSinkDto MakeSink(bool enabled = true, string[]? events = null, string? secret = null) => new()
    {
        Id = Guid.NewGuid(), Name = "test-sink",
        Url = "http://localhost:9999/hook",
        Secret = secret, Enabled = enabled,
        Events = events ?? ["alert.created", "alert.escalated", "alert.acknowledged", "alert.resolved"],
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static AlertDto MakeAlert() => new()
    {
        Id = Guid.NewGuid(), WorkspaceId = DefaultTenant.WorkspaceId,
        RuleId = "cpu-high", Severity = "critical",
        Category = "cpu", Title = "High CPU", Status = "open",
        TriggeringJobId = Guid.NewGuid(), LatestJobId = Guid.NewGuid(),
        TriggeredAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow,
    };

    private static (NotificationService svc, FakeHttpMessageHandler handler) Build(params WebhookSinkDto[] sinks)
    {
        var repo = new FakeWebhookSinkRepository(sinks);
        var handler = new FakeHttpMessageHandler();
        var http = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(http);
        var svc = new NotificationService(repo, factory, new TenantContext(), NullLogger<NotificationService>.Instance);
        return (svc, handler);
    }

    // ── delivery ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Notify_EnabledMatchingSink_DeliversPOST()
    {
        var (svc, handler) = Build(MakeSink());
        await svc.NotifyAsync("alert.created", MakeAlert(), TestContext.Current.CancellationToken);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task Notify_DisabledSink_SkipsDelivery()
    {
        var (svc, handler) = Build(MakeSink(enabled: false));
        await svc.NotifyAsync("alert.created", MakeAlert(), TestContext.Current.CancellationToken);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Notify_SinkNotSubscribedToEvent_SkipsDelivery()
    {
        var (svc, handler) = Build(MakeSink(events: ["alert.resolved"]));
        await svc.NotifyAsync("alert.created", MakeAlert(), TestContext.Current.CancellationToken);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Notify_NoSinks_NoHttpCalls()
    {
        var (svc, handler) = Build(); // no sinks at all
        await svc.NotifyAsync("alert.created", MakeAlert(), TestContext.Current.CancellationToken);
        Assert.Null(handler.LastRequest);
    }

    // ── payload ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Notify_Payload_ContainsEventAndAlert()
    {
        var (svc, handler) = Build(MakeSink());
        var alert = MakeAlert();
        await svc.NotifyAsync("alert.created", alert, TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("alert.created", body.RootElement.GetProperty("event").GetString());
        Assert.Equal(alert.RuleId, body.RootElement.GetProperty("alert").GetProperty("ruleId").GetString());
    }

    // ── HMAC signature ────────────────────────────────────────────────────────

    [Fact]
    public async Task Notify_WithSecret_IncludesCorrectHmacSignature()
    {
        var secret = "super-secret";
        var (svc, handler) = Build(MakeSink(secret: secret));
        await svc.NotifyAsync("alert.created", MakeAlert(), TestContext.Current.CancellationToken);

        var bodyBytes = Encoding.UTF8.GetBytes(handler.LastBody!);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = $"sha256={Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant()}";

        Assert.True(handler.LastRequest!.Headers.TryGetValues("X-PAL-Signature", out var values));
        Assert.Equal(expected, values!.Single());
    }

    [Fact]
    public async Task Notify_WithoutSecret_NoSignatureHeader()
    {
        var (svc, handler) = Build(MakeSink(secret: null));
        await svc.NotifyAsync("alert.created", MakeAlert(), TestContext.Current.CancellationToken);
        Assert.False(handler.LastRequest!.Headers.Contains("X-PAL-Signature"));
    }

    // ── test endpoint ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TestAsync_SinkNotFound_ReturnsNull()
    {
        var (svc, _) = Build(); // no sinks
        var result = await svc.TestAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task TestAsync_SinkFound_DeliversAndReturnsHttpStatus()
    {
        var sink = MakeSink();
        var repo = new FakeWebhookSinkRepository(sink);
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var http = new HttpClient(handler);
        var factory = new FakeHttpClientFactory(http);
        var svc = new NotificationService(repo, factory, new TenantContext(), NullLogger<NotificationService>.Instance);

        var result = await svc.TestAsync(sink.Id, TestContext.Current.CancellationToken);

        Assert.Equal(200, result);
        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task TestAsync_SinkFound_PayloadEventIsWebhookTest()
    {
        var sink = MakeSink();
        var repo = new FakeWebhookSinkRepository(sink);
        var handler = new FakeHttpMessageHandler();
        var svc = new NotificationService(repo, new FakeHttpClientFactory(new HttpClient(handler)),
            new TenantContext(), NullLogger<NotificationService>.Instance);

        await svc.TestAsync(sink.Id, TestContext.Current.CancellationToken);

        var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("webhook.test", body.RootElement.GetProperty("event").GetString());
    }
}

// ── Fakes ──────────────────────────────────────────────────────────────────────

internal sealed class FakeHttpMessageHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        LastBody = await request.Content!.ReadAsStringAsync(ct);
        return new HttpResponseMessage(status);
    }
}

internal sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed class FakeWebhookSinkRepository(params WebhookSinkDto[] sinks) : IWebhookSinkRepository
{
    private readonly List<WebhookSinkDto> _store = [..sinks];

    public Task<IReadOnlyList<WebhookSinkDto>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebhookSinkDto>>(_store.ToList());

    public Task<WebhookSinkDto?> GetAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(s => s.Id == id));

    public Task CreateAsync(WebhookSinkDto sink, CancellationToken ct = default)
    {
        _store.Add(sink);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(WebhookSinkDto sink, bool updateSecret, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(s => s.Id == sink.Id);
        if (idx < 0) return Task.FromResult(false);
        if (!updateSecret)
        {
            var existing = _store[idx];
            _store[idx] = new WebhookSinkDto
            {
                Id = sink.Id, Name = sink.Name, Url = sink.Url, Secret = existing.Secret,
                Enabled = sink.Enabled, Events = sink.Events, CreatedAt = sink.CreatedAt, UpdatedAt = sink.UpdatedAt,
            };
        }
        else
        {
            _store[idx] = sink;
        }
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(s => s.Id == id);
        if (idx < 0) return Task.FromResult(false);
        _store.RemoveAt(idx);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<WebhookSinkDto>> ListEnabledForEventAsync(string eventName, Guid workspaceId, CancellationToken ct = default)
    {
        var matches = _store
            .Where(s => s.Enabled && s.Events.Contains(eventName))
            .ToList();
        return Task.FromResult<IReadOnlyList<WebhookSinkDto>>(matches);
    }
}
