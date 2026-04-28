using Pal.Api.Auth;
using Pal.Application.Ingestion;

namespace Pal.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        // /schedules/data avoids future route conflict with a Blazor @page "/schedules"
        app.MapGet("/schedules/data", async (IIngestionScheduleService svc) =>
            Results.Ok(new { items = (await svc.ListAsync()).Select(ToResponse) }))
        .WithName("ListSchedules")
        .WithTags("Schedules");

        app.MapGet("/schedules/{id:guid}", async (Guid id, IIngestionScheduleService svc) =>
        {
            var s = await svc.GetAsync(id);
            return s is null ? Results.NotFound() : Results.Ok(ToResponse(s));
        })
        .WithName("GetSchedule")
        .WithTags("Schedules");

        app.MapPost("/schedules", async (CreateScheduleRequest req, IIngestionScheduleService svc) =>
        {
            try
            {
                var s = await svc.CreateAsync(
                    req.Name ?? "",
                    req.IntervalMinutes,
                    req.SourceConfigJson ?? "",
                    req.PackIds ?? Array.Empty<string>(),
                    req.Enabled);
                return Results.Created($"/schedules/{s.Id}", ToResponse(s));
            }
            catch (IngestionScheduleValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateSchedule")
        .WithTags("Schedules")
        .RequireAuthorization(Roles.Admin);

        app.MapPut("/schedules/{id:guid}", async (Guid id, UpdateScheduleRequest req, IIngestionScheduleService svc) =>
        {
            try
            {
                var ok = await svc.UpdateAsync(
                    id,
                    req.Name ?? "",
                    req.IntervalMinutes,
                    req.SourceConfigJson ?? "",
                    req.PackIds ?? Array.Empty<string>(),
                    req.Enabled);
                return ok ? Results.NoContent() : Results.NotFound();
            }
            catch (IngestionScheduleValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpdateSchedule")
        .WithTags("Schedules")
        .RequireAuthorization(Roles.Admin);

        app.MapPatch("/schedules/{id:guid}/enabled", async (Guid id, EnableScheduleRequest req, IIngestionScheduleService svc) =>
        {
            var ok = await svc.SetEnabledAsync(id, req.Enabled);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("SetScheduleEnabled")
        .WithTags("Schedules")
        .RequireAuthorization(Roles.Admin);

        app.MapDelete("/schedules/{id:guid}", async (Guid id, IIngestionScheduleService svc) =>
        {
            var ok = await svc.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteSchedule")
        .WithTags("Schedules")
        .RequireAuthorization(Roles.Admin);
    }

    private static ScheduleResponse ToResponse(Pal.Application.Persistence.IngestionScheduleDto s) => new(
        s.Id, s.Name, s.IntervalMinutes, s.SourceConfigJson, s.PackIds, s.Enabled,
        s.LastRunAt, s.NextRunAt, s.CreatedAt, s.UpdatedAt);

    private sealed record ScheduleResponse(
        Guid Id, string Name, int IntervalMinutes, string SourceConfigJson,
        IReadOnlyList<string> PackIds, bool Enabled,
        DateTimeOffset? LastRunAt, DateTimeOffset? NextRunAt,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    private sealed record CreateScheduleRequest(
        string? Name, int IntervalMinutes, string? SourceConfigJson,
        IReadOnlyList<string>? PackIds, bool Enabled);

    private sealed record UpdateScheduleRequest(
        string? Name, int IntervalMinutes, string? SourceConfigJson,
        IReadOnlyList<string>? PackIds, bool Enabled);

    private sealed record EnableScheduleRequest(bool Enabled);
}
