using System.Net;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class HealthEndpointsTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_Health_Returns200()
    {
        var resp = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
