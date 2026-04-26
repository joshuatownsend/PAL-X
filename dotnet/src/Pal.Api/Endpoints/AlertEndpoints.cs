using Pal.Application.Alerts;

namespace Pal.Api.Endpoints;

public static class AlertEndpoints
{
    public static void MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/alerts", async (string? status, string? severity, IAlertService alerts) =>
            Results.Ok(new { items = await alerts.ListAsync(status, severity) }))
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
            var ok = await alerts.AcknowledgeAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AcknowledgeAlert")
        .WithTags("Alerts");

        app.MapPatch("/alerts/{id:guid}/resolve", async (Guid id, ResolveAlertRequest? req, IAlertService alerts) =>
        {
            var ok = await alerts.ResolveAsync(id, req?.Note);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("ResolveAlert")
        .WithTags("Alerts");
    }

    private sealed record ResolveAlertRequest(string? Note);
}
