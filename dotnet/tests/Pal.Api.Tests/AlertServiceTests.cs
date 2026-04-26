using Pal.Application.Alerts;
using Pal.Application.Persistence;
using Pal.Application.Webhooks;
using Pal.Engine.Model;
using Xunit;

namespace Pal.Api.Tests;

public class AlertServiceTests
{
    private static Finding MakeFinding(string ruleId, string severity, string category = "cpu", string title = "Test") =>
        new()
        {
            FindingId = ruleId,
            PackId = "windows-core",
            RuleId = ruleId,
            Severity = severity,
            Category = category,
            Title = title,
            Summary = "",
            Explanation = "",
            EvidenceMetrics = [],
            Recommendations = [],
        };

    private static Guid Job1 => new("aaaaaaaa-0000-0000-0000-000000000001");
    private static Guid Job2 => new("aaaaaaaa-0000-0000-0000-000000000002");

    // ── new finding → new open alert ──────────────────────────────────────

    [Fact]
    public async Task Evaluate_NewFinding_CreatesOpenAlert()
    {
        var repo = new FakeAlertRepository();
        var notifications = new FakeNotificationService();
        var svc = new AlertService(repo, notifications);

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);

        var alerts = await svc.ListAsync();
        var a = Assert.Single(alerts);
        Assert.Equal("cpu-high", a.RuleId);
        Assert.Equal("warning", a.Severity);
        Assert.Equal("open", a.Status);
        Assert.Equal(Job1, a.TriggeringJobId);
        Assert.Equal(Job1, a.LatestJobId);
        Assert.Single(notifications.Calls, c => c.Event == "alert.created");
    }

    // ── same finding in second run → updates existing alert ───────────────

    [Fact]
    public async Task Evaluate_SameFindingSecondRun_UpdatesLastSeen()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        await svc.EvaluateAsync(Job2, [MakeFinding("cpu-high", "warning")]);

        var alerts = await svc.ListAsync();
        var a = Assert.Single(alerts);
        Assert.Equal(Job1, a.TriggeringJobId);
        Assert.Equal(Job2, a.LatestJobId);
    }

    // ── severity escalation ───────────────────────────────────────────────

    [Fact]
    public async Task Evaluate_HigherSeverityInSecondRun_EscalatesAlert()
    {
        var repo = new FakeAlertRepository();
        var notifications = new FakeNotificationService();
        var svc = new AlertService(repo, notifications);

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        await svc.EvaluateAsync(Job2, [MakeFinding("cpu-high", "critical")]);

        var a = Assert.Single(await svc.ListAsync());
        Assert.Equal("critical", a.Severity);
        Assert.Single(notifications.Calls, c => c.Event == "alert.escalated");
    }

    [Fact]
    public async Task Evaluate_LowerSeverityInSecondRun_DoesNotDowngrade()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "critical")]);
        await svc.EvaluateAsync(Job2, [MakeFinding("cpu-high", "warning")]);

        var a = Assert.Single(await svc.ListAsync());
        Assert.Equal("critical", a.Severity);
    }

    // ── duplicate rule within one job ─────────────────────────────────────

    [Fact]
    public async Task Evaluate_DuplicateRuleIdInSameJob_CreatesOnlyOneAlertWithHighestSeverity()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [
            MakeFinding("cpu-high", "warning"),
            MakeFinding("cpu-high", "critical"),
        ]);

        var a = Assert.Single(await svc.ListAsync());
        Assert.Equal("critical", a.Severity);
    }

    // ── multiple distinct rules → multiple alerts ─────────────────────────

    [Fact]
    public async Task Evaluate_MultipleDistinctRules_CreatesOneAlertEach()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [
            MakeFinding("cpu-high", "warning"),
            MakeFinding("mem-low", "critical"),
        ]);

        Assert.Equal(2, (await svc.ListAsync()).Count);
    }

    // ── acknowledge ───────────────────────────────────────────────────────

    [Fact]
    public async Task Acknowledge_OpenAlert_TransitionsToAcknowledged()
    {
        var repo = new FakeAlertRepository();
        var notifications = new FakeNotificationService();
        var svc = new AlertService(repo, notifications);

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        var id = (await svc.ListAsync()).Single().Id;

        var ok = await svc.AcknowledgeAsync(id);
        Assert.True(ok);

        var a = await svc.GetAsync(id);
        Assert.Equal("acknowledged", a!.Status);
        Assert.NotNull(a.AcknowledgedAt);
        Assert.Single(notifications.Calls, c => c.Event == "alert.acknowledged");
    }

    [Fact]
    public async Task Acknowledge_AlreadyAcknowledged_ReturnsFalse()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        var id = (await svc.ListAsync()).Single().Id;
        await svc.AcknowledgeAsync(id);

        var ok = await svc.AcknowledgeAsync(id);
        Assert.False(ok);
    }

    // ── resolve ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_OpenAlert_TransitionsToResolved()
    {
        var repo = new FakeAlertRepository();
        var notifications = new FakeNotificationService();
        var svc = new AlertService(repo, notifications);

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        var id = (await svc.ListAsync()).Single().Id;

        var ok = await svc.ResolveAsync(id, "fixed by reboot");
        Assert.True(ok);

        var a = await svc.GetAsync(id);
        Assert.Equal("resolved", a!.Status);
        Assert.Equal("fixed by reboot", a.ResolutionNote);
        Assert.NotNull(a.ResolvedAt);
        Assert.Single(notifications.Calls, c => c.Event == "alert.resolved");
    }

    [Fact]
    public async Task Resolve_AcknowledgedAlert_Succeeds()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        var id = (await svc.ListAsync()).Single().Id;
        await svc.AcknowledgeAsync(id);

        var ok = await svc.ResolveAsync(id, null);
        Assert.True(ok);
        Assert.Equal("resolved", (await svc.GetAsync(id))!.Status);
    }

    [Fact]
    public async Task Resolve_AlreadyResolved_ReturnsFalse()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        var id = (await svc.ListAsync()).Single().Id;
        await svc.ResolveAsync(id, null);

        var ok = await svc.ResolveAsync(id, null);
        Assert.False(ok);
    }

    // ── resolved alert not re-opened by new finding ───────────────────────

    [Fact]
    public async Task Evaluate_ResolvedAlert_CreatesNewOpenAlert()
    {
        var repo = new FakeAlertRepository();
        var svc = new AlertService(repo, new FakeNotificationService());

        await svc.EvaluateAsync(Job1, [MakeFinding("cpu-high", "warning")]);
        var id = (await svc.ListAsync()).Single().Id;
        await svc.ResolveAsync(id, null);

        // Same finding reappears — should create a fresh alert, not reuse the resolved one
        await svc.EvaluateAsync(Job2, [MakeFinding("cpu-high", "warning")]);

        var all = await svc.ListAsync(status: null);
        Assert.Equal(2, all.Count);
        Assert.Single(all, a => a.Status == "resolved");
        Assert.Single(all, a => a.Status == "open");
    }

    // ── severity rank helper ──────────────────────────────────────────────

    [Theory]
    [InlineData("critical", 3)]
    [InlineData("warning", 2)]
    [InlineData("informational", 1)]
    [InlineData("unknown", 0)]
    public void SeverityRank_CorrectOrder(string sev, int expected)
    {
        Assert.Equal(expected, AlertService.SeverityRank(sev));
    }
}

// ── Fake notification service (no-op, records calls) ─────────────────────────

internal sealed class FakeNotificationService : INotificationService
{
    public List<(string Event, AlertDto Alert)> Calls { get; } = [];

    public Task NotifyAsync(string @event, AlertDto alert, CancellationToken ct = default)
    {
        Calls.Add((@event, alert));
        return Task.CompletedTask;
    }

    public Task<int?> TestAsync(Guid sinkId, CancellationToken ct = default)
        => Task.FromResult<int?>(200);
}

// ── Fake repository (in-memory, no EF) ───────────────────────────────────────

internal sealed class FakeAlertRepository : IAlertRepository
{
    private readonly List<AlertDto> _store = [];

    public Task<AlertDto?> FindActiveByRuleIdAsync(string ruleId, CancellationToken ct = default)
    {
        var match = _store.FirstOrDefault(a => a.RuleId == ruleId && a.Status != "resolved");
        return Task.FromResult(match);
    }

    public Task CreateAsync(AlertDto alert, CancellationToken ct = default)
    {
        _store.Add(alert);
        return Task.CompletedTask;
    }

    public Task UpdateLatestAsync(Guid id, Guid latestJobId, string severity, DateTimeOffset lastSeenAt, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(a => a.Id == id);
        if (idx < 0) return Task.CompletedTask;
        var a = _store[idx];
        _store[idx] = new AlertDto
        {
            Id = a.Id, RuleId = a.RuleId, Category = a.Category, Title = a.Title,
            Status = a.Status, TriggeringJobId = a.TriggeringJobId,
            LatestJobId = latestJobId, Severity = severity,
            TriggeredAt = a.TriggeredAt, LastSeenAt = lastSeenAt,
            AcknowledgedAt = a.AcknowledgedAt, ResolvedAt = a.ResolvedAt, ResolutionNote = a.ResolutionNote,
        };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AlertDto>> ListAsync(string? status, string? severity, CancellationToken ct = default)
    {
        var q = _store.AsEnumerable();
        if (status is not null) q = q.Where(a => a.Status == status);
        if (severity is not null) q = q.Where(a => a.Severity == severity);
        return Task.FromResult<IReadOnlyList<AlertDto>>(q.OrderByDescending(a => a.LastSeenAt).ToList());
    }

    public Task<AlertDto?> GetAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(a => a.Id == id));

    public Task<bool> AcknowledgeAsync(Guid id, DateTimeOffset now, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(a => a.Id == id && a.Status == "open");
        if (idx < 0) return Task.FromResult(false);
        var a = _store[idx];
        _store[idx] = new AlertDto
        {
            Id = a.Id, RuleId = a.RuleId, Severity = a.Severity, Category = a.Category, Title = a.Title,
            Status = "acknowledged", TriggeringJobId = a.TriggeringJobId, LatestJobId = a.LatestJobId,
            TriggeredAt = a.TriggeredAt, LastSeenAt = a.LastSeenAt,
            AcknowledgedAt = now, ResolvedAt = a.ResolvedAt, ResolutionNote = a.ResolutionNote,
        };
        return Task.FromResult(true);
    }

    public Task<bool> ResolveAsync(Guid id, string? note, DateTimeOffset now, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(a => a.Id == id && a.Status != "resolved");
        if (idx < 0) return Task.FromResult(false);
        var a = _store[idx];
        _store[idx] = new AlertDto
        {
            Id = a.Id, RuleId = a.RuleId, Severity = a.Severity, Category = a.Category, Title = a.Title,
            Status = "resolved", TriggeringJobId = a.TriggeringJobId, LatestJobId = a.LatestJobId,
            TriggeredAt = a.TriggeredAt, LastSeenAt = a.LastSeenAt,
            AcknowledgedAt = a.AcknowledgedAt, ResolvedAt = now, ResolutionNote = note,
        };
        return Task.FromResult(true);
    }
}
