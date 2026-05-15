using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pal.Application.Persistence;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class PackEndpointsTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private IPackRepository PackRepo => factory.Services.GetRequiredService<IPackRepository>();

    private const string MinimalValidPackYaml = """
        schema_version: "pal.pack/v1"
        pack_id: test-pack
        pack_name: "Test Pack"
        version: "1.0.0"
        description: "Integration test fixture pack"
        applicability:
          always: true
        rules: []
        """;

    [Fact]
    public async Task Get_Packs_ReturnsOk_WithItemsArray()
    {
        var resp = await _client.GetAsync("/packs", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Get_PackVersions_UnknownPack_Returns404()
    {
        var resp = await _client.GetAsync("/packs/nonexistent-pack-id/versions", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_PackVersions_KnownPack_ReturnsItems()
    {
        var packId = $"test-versions-{Guid.NewGuid():N}";
        var tmpYaml = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpYaml, MinimalValidPackYaml, TestContext.Current.CancellationToken);
            await PackRepo.UpsertPackAsync(packId, "1.0.0", "Test Pack", tmpYaml, TestContext.Current.CancellationToken);

            var resp = await _client.GetAsync($"/packs/{packId}/versions", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.True(body.TryGetProperty("items", out var items));
            Assert.True(items.GetArrayLength() >= 1);
        }
        finally { File.Delete(tmpYaml); }
    }

    [Fact]
    public async Task Get_PackValidation_UnknownPack_Returns404()
    {
        var resp = await _client.GetAsync("/packs/nonexistent-pack-id/versions/1.0.0/validation", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_PackValidation_ValidPack_Returns200_IsValidTrue()
    {
        var packId = $"test-valid-{Guid.NewGuid():N}";
        var tmpYaml = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpYaml, MinimalValidPackYaml, TestContext.Current.CancellationToken);
            await PackRepo.UpsertPackAsync(packId, "1.0.0", "Test Pack", tmpYaml, TestContext.Current.CancellationToken);

            var resp = await _client.GetAsync($"/packs/{packId}/versions/1.0.0/validation", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            Assert.True(body.GetProperty("isValid").GetBoolean());
            Assert.Equal(0, body.GetProperty("errors").GetArrayLength());
        }
        finally { File.Delete(tmpYaml); }
    }
}
