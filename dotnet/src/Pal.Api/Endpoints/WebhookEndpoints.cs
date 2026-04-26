using Pal.Application.Webhooks;

namespace Pal.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // /webhooks/data avoids route conflict with Blazor @page "/webhooks"
        app.MapGet("/webhooks/data", async (IWebhookSinkService webhooks) =>
            Results.Ok(new { items = (await webhooks.ListAsync()).Select(ToResponse) }))
        .WithName("ListWebhooks")
        .WithTags("Webhooks");

        app.MapGet("/webhooks/{id:guid}", async (Guid id, IWebhookSinkService webhooks) =>
        {
            var sink = await webhooks.GetAsync(id);
            return sink is null ? Results.NotFound() : Results.Ok(ToResponse(sink));
        })
        .WithName("GetWebhook")
        .WithTags("Webhooks");

        app.MapPost("/webhooks", async (CreateWebhookRequest req, IWebhookSinkService webhooks) =>
        {
            var err = ValidateRequest(req.Name, req.Url, req.Events);
            if (err is not null) return Results.BadRequest(new { error = err });

            var sink = await webhooks.CreateAsync(req.Name!, req.Url!, req.Secret, req.Enabled, req.Events ?? []);
            return Results.Created($"/webhooks/{sink.Id}", ToResponse(sink));
        })
        .WithName("CreateWebhook")
        .WithTags("Webhooks");

        app.MapPut("/webhooks/{id:guid}", async (Guid id, UpdateWebhookRequest req, IWebhookSinkService webhooks) =>
        {
            var err = ValidateRequest(req.Name, req.Url, req.Events);
            if (err is not null) return Results.BadRequest(new { error = err });

            var ok = await webhooks.UpdateAsync(id, req.Name!, req.Url!, req.Secret, req.Enabled, req.Events!,
                updateSecret: req.UpdateSecret);
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
            catch (Exception)
            {
                return Results.Problem("Webhook delivery failed. Check server logs for details.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            if (status is null) return Results.NotFound();
            return status is >= 200 and < 300
                ? Results.Ok(new { delivered = true, httpStatus = status })
                : Results.Problem($"Webhook endpoint returned HTTP {status}", statusCode: StatusCodes.Status502BadGateway);
        })
        .WithName("TestWebhook")
        .WithTags("Webhooks");
    }

    private static string? ValidateRequest(string? name, string? url, IReadOnlyList<string>? events)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(url)) return "Url is required.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "Url must be an absolute http(s) URI.";
        if (events is null || events.Count == 0) return "At least one event is required.";
        return null;
    }

    private static WebhookSinkResponse ToResponse(Pal.Application.Persistence.WebhookSinkDto s) => new(
        s.Id, s.Name, s.Url, s.Secret is not null, s.Enabled, s.Events, s.CreatedAt, s.UpdatedAt);

    private sealed record WebhookSinkResponse(
        Guid Id, string Name, string Url, bool HasSecret, bool Enabled,
        IReadOnlyList<string> Events, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    private sealed record CreateWebhookRequest(string? Name, string? Url, string? Secret, bool Enabled, IReadOnlyList<string>? Events);
    private sealed record UpdateWebhookRequest(string? Name, string? Url, string? Secret, bool UpdateSecret, bool Enabled, IReadOnlyList<string>? Events);
}
