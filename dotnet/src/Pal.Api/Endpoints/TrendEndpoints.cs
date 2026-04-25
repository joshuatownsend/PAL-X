using Pal.Application.Trends;

namespace Pal.Api.Endpoints;

public static class TrendEndpoints
{
    public static void MapTrendEndpoints(this IEndpointRouteBuilder app)
    {
        // /trends/data avoids route conflict with Blazor @page "/trends"
        app.MapGet("/trends/data", async (int? last, TrendService trends) =>
            Results.Ok(await trends.ComputeAsync(last ?? 10)))
        .WithName("GetTrends")
        .WithTags("Trends");
    }
}
