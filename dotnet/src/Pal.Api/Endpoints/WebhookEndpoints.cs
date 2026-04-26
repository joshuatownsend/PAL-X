using Pal.Application.Webhooks;

namespace Pal.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/webhooks", async (IWebhookSinkService webhooks) =>
            Results.Ok(new { items = await webhooks.ListAsync() }))
        .WithName("ListWebhooks")
        .WithTags("Webhooks");

        app.MapGet("/webhooks/{id:guid}", async (Guid id, IWebhookSinkService webhooks) =>
        {
            var sink = await webhooks.GetAsync(id);
            return sink is null ? Results.NotFound() : Results.Ok(sink);
        })
        .WithName("GetWebhook")
        .WithTags("Webhooks");

        app.MapPost("/webhooks", async (CreateWebhookRequest req, IWebhookSinkService webhooks) =>
        {
            var sink = await webhooks.CreateAsync(req.Name, req.Url, req.Secret, req.Enabled, req.Events);
            return Results.Created($"/webhooks/{sink.Id}", sink);
        })
        .WithName("CreateWebhook")
        .WithTags("Webhooks");

        app.MapPut("/webhooks/{id:guid}", async (Guid id, UpdateWebhookRequest req, IWebhookSinkService webhooks) =>
        {
            var ok = await webhooks.UpdateAsync(id, req.Name, req.Url, req.Secret, req.Enabled, req.Events);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateWebhook")
        .WithTags("Webhooks");

        app.MapDelete("/webhooks/{id:guid}", async (Guid id, IWebhookSinkService webhooks) =>
        {
            var ok = await webhooks.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteWebhook")
        .WithTags("Webhooks");

        app.MapPost("/webhooks/{id:guid}/test", async (Guid id, INotificationService notifications) =>
        {
            int? status;
            try
            {
                status = await notifications.TestAsync(id);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Delivery error: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
            }

            if (status is null) return Results.NotFound();
            return status is >= 200 and < 300
                ? Results.Ok(new { delivered = true, httpStatus = status })
                : Results.Problem($"Webhook endpoint returned HTTP {status}", statusCode: StatusCodes.Status502BadGateway);
        })
        .WithName("TestWebhook")
        .WithTags("Webhooks");
    }

    private sealed record CreateWebhookRequest(string Name, string Url, string? Secret, bool Enabled, IReadOnlyList<string> Events);
    private sealed record UpdateWebhookRequest(string Name, string Url, string? Secret, bool Enabled, IReadOnlyList<string> Events);
}
