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
    }

    private sealed record ResolveAlertRequest(string? Note);
}
