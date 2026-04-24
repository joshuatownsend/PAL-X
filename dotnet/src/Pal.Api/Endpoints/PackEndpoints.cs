using Pal.Application.Persistence;

namespace Pal.Api.Endpoints;

public static class PackEndpoints
{
    public static void MapPackEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/packs", async (IPackRepository packs) =>
        {
            var list = await packs.ListPacksAsync();
            return Results.Ok(new { items = list });
        })
        .WithName("ListPacks")
        .WithTags("Packs");
    }
}
