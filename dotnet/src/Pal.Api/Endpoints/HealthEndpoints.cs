namespace Pal.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "2026.2.0" }))
           .WithName("GetHealth")
           .WithTags("Health");
    }
}
