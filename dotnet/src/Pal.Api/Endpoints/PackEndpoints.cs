using Pal.Application.Persistence;
using Pal.Packs;
using Pal.Packs.Signing;

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

        app.MapGet("/packs/{id}/versions", async (string id, IPackRepository packs) =>
        {
            var versions = await packs.ListVersionsAsync(id);
            return versions.Count == 0 ? Results.NotFound() : Results.Ok(new { items = versions });
        })
        .WithName("ListPackVersions")
        .WithTags("Packs");

        app.MapGet("/packs/{id}/versions/{version}/validation", async (string id, string version, IPackRepository packs) =>
        {
            var yamlPath = await packs.GetVersionYamlPathAsync(id, version);
            if (yamlPath is null) return Results.NotFound();

            try
            {
                var pack = new PackLoader().Load(yamlPath, SignatureRequirement.Optional);
                var result = new PackValidator().Validate(pack);
                return Results.Ok(new
                {
                    isValid = result.IsValid,
                    errors = result.Errors,
                    warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to load pack for validation: {ex.Message}", statusCode: 422);
            }
        })
        .WithName("ValidatePackVersion")
        .WithTags("Packs");
    }
}
