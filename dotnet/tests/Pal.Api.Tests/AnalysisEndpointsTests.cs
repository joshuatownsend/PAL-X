using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class AnalysisEndpointsTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_Analysis_Returns202_WithJobId()
    {
        var uploadId = await CreateUploadAsync("job-test.csv", "col\n1");

        var resp = await _client.PostAsJsonAsync("/analysis", new
        {
            uploadId,
            packs = new[] { "windows-core" }
        });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("analysisId", out var jobId));
        Assert.NotEqual(Guid.Empty, Guid.Parse(jobId.GetString()!));
        Assert.Equal("queued", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Post_Analysis_UnknownUpload_Returns404()
    {
        var resp = await _client.PostAsJsonAsync("/analysis", new
        {
            uploadId = Guid.NewGuid(),
            packs = new[] { "windows-core" }
        });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_Analysis_NoPacks_Returns400()
    {
        var uploadId = await CreateUploadAsync("no-packs.csv", "col\n1");
        var resp = await _client.PostAsJsonAsync("/analysis", new
        {
            uploadId,
            packs = Array.Empty<string>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Analysis_List_ReturnsItems()
    {
        var resp = await _client.GetAsync("/analysis");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Get_Analysis_ById_UnknownId_Returns404()
    {
        var resp = await _client.GetAsync($"/analysis/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Analysis_Results_IncompleteJob_Returns409()
    {
        var uploadId = await CreateUploadAsync("pending.csv", "col\n1");
        var jobResp = await _client.PostAsJsonAsync("/analysis", new
        {
            uploadId,
            packs = new[] { "windows-core" }
        });
        var jobBody = await jobResp.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = jobBody.GetProperty("analysisId").GetString();

        var resultsResp = await _client.GetAsync($"/analysis/{jobId}/results");
        Assert.Equal(HttpStatusCode.Conflict, resultsResp.StatusCode);
    }

    private async Task<Guid> CreateUploadAsync(string name, string csv)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(csv), "file", name);
        var resp = await _client.PostAsync("/uploads", form);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("uploadId").GetString()!);
    }
}
