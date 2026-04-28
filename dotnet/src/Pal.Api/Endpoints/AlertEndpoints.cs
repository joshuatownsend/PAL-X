using Pal.Api.Auth;
using Pal.Application.Alerts;

namespace Pal.Api.Endpoints;

public static class AlertEndpoints
{
    public static void MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        // /alerts/data avoids route conflict with Blazor @page "/alerts"
        app.MapGet("/alerts/data", async (string? status, string? severity, IAlertService alerts) =>
        {
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status;
            var normalizedSeverity = string.IsNullOrWhiteSpace(severity) ? null : severity;
            return Results.Ok(new { items = await alerts.ListAsync(normalizedStatus, normalizedSeverity) });
        })
        .WithName("ListAlerts")
        .WithTags("Alerts");

        app.MapGet("/alerts/{id:guid}", async (Guid id, IAlertService alerts) =>
        {
            var alert = await alerts.GetAsync(id);
            return alert is null ? Results.NotFound() : Results.Ok(alert);
        })
        .WithName("GetAlert")
        .WithTags("Alerts");

        app.MapPatch("/alerts/{id:guid}/acknowledge", async (Guid id, IAlertService alerts) =>
        {
            var alert = await alerts.GetAsync(id);
            if (alert is null) return Results.NotFound();
            var ok = await alerts.AcknowledgeAsync(id);
            return ok ? Results.NoContent() : Results.Conflict("Alert is not in 'open' state");
        })
        .WithName("AcknowledgeAlert")
        .WithTags("Alerts")
        .RequireAuthorization(Roles.Analyst);

        app.MapPatch("/alerts/{id:guid}/resolve", async (Guid id, ResolveAlertRequest? req, IAlertService alerts) =>
        {
            var alert = await alerts.GetAsync(id);
            if (alert is null) return Results.NotFound();
            var ok = await alerts.ResolveAsync(id, req?.Note);
            return ok ? Results.NoContent() : Results.Conflict("Alert is already resolved");
        })
        .WithName("ResolveAlert")
        .WithTags("Alerts")
        .RequireAuthorization(Roles.Analyst);

        app.MapPatch("/alerts/{id:guid}/snooze", async (Guid id, SnoozeAlertRequest req, IAlertService alerts) =>
        {
            var alert = await alerts.GetAsync(id);
            if (alert is null) return Results.NotFound();

            // until must be a future absolute timestamp; clients send their own clock so we
            // tolerate a 30s skew window before rejecting. Cap at 30 days to prevent
            // accidental "forever" snoozes.
            var now = DateTimeOffset.UtcNow;
            if (req.Until <= now.AddSeconds(-30))
                return Results.BadRequest(new { error = "until must be in the future" });
            if (req.Until > now.AddDays(30))
                return Results.BadRequest(new { error = "until cannot be more than 30 days in the future" });

            var ok = await alerts.SetSnoozedUntilAsync(id, req.Until);
            return ok ? Results.NoContent() : Results.Conflict("Alert is resolved and cannot be snoozed");
        })
        .WithName("SnoozeAlert")
        .WithTags("Alerts")
        .RequireAuthorization(Roles.Analyst);

        app.MapDelete("/alerts/{id:guid}/snooze", async (Guid id, IAlertService alerts) =>
        {
            var alert = await alerts.GetAsync(id);
            if (alert is null) return Results.NotFound();
            var ok = await alerts.SetSnoozedUntilAsync(id, null);
            return ok ? Results.NoContent() : Results.Conflict("Alert is resolved");
        })
        .WithName("UnsnoozeAlert")
        .WithTags("Alerts")
        .RequireAuthorization(Roles.Analyst);
    }

    private sealed record ResolveAlertRequest(string? Note);
    private sealed record SnoozeAlertRequest(DateTimeOffset Until);
}
