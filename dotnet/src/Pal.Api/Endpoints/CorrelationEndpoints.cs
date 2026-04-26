using Pal.Application.Correlation;

namespace Pal.Api.Endpoints;

public static class CorrelationEndpoints
{
    public static void MapCorrelationEndpoints(this IEndpointRouteBuilder app)
    {
        // /correlations/data avoids route conflict with Blazor @page "/correlations"
        app.MapGet("/correlations/data", async (int? last, CorrelationService correlations) =>
            Results.Ok(await correlations.ComputeAsync(last ?? 10)))
        .WithName("GetCorrelations")
        .WithTags("Correlations");
    }
}
