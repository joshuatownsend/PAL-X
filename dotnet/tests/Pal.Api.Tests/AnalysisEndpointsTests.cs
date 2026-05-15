using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class AnalysisEndpointsTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly string Uploads = $"{PalApiFactory.WsBase}/uploads";
    private static readonly string Analysis = $"{PalApiFactory.WsBase}/analysis";

    [Fact]
    public async Task Post_Analysis_Returns202_WithJobId()
    {
        var uploadId = await CreateUploadAsync("job-test.csv", "col\n1", TestContext.Current.CancellationToken);

        var resp = await _client.PostAsJsonAsync(Analysis, new
        {
            uploadId,
            packs = new[] { "windows-core" }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.TryGetProperty("analysisId", out var jobId));
        Assert.NotEqual(Guid.Empty, Guid.Parse(jobId.GetString()!));
        Assert.Equal("queued", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Post_Analysis_UnknownUpload_Returns404()
    {
        var resp = await _client.PostAsJsonAsync(Analysis, new
        {
            uploadId = Guid.NewGuid(),
            packs = new[] { "windows-core" }
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_Analysis_NoPacks_Returns400()
    {
        var uploadId = await CreateUploadAsync("no-packs.csv", "col\n1", TestContext.Current.CancellationToken);
        var resp = await _client.PostAsJsonAsync(Analysis, new
        {
            uploadId,
            packs = Array.Empty<string>()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Analysis_List_ReturnsItems()
    {
        var resp = await _client.GetAsync(Analysis, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Get_Analysis_ById_UnknownId_Returns404()
    {
        var resp = await _client.GetAsync($"{Analysis}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Analysis_Results_IncompleteJob_Returns409()
    {
        var uploadId = await CreateUploadAsync("pending.csv", "col\n1", TestContext.Current.CancellationToken);
        var jobResp = await _client.PostAsJsonAsync(Analysis, new
        {
            uploadId,
            packs = new[] { "windows-core" }
        }, TestContext.Current.CancellationToken);
        var jobBody = await jobResp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var jobId = jobBody.GetProperty("analysisId").GetString();

        var resultsResp = await _client.GetAsync($"{Analysis}/{jobId}/results", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, resultsResp.StatusCode);
    }

    private async Task<Guid> CreateUploadAsync(string name, string csv, CancellationToken ct = default)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(csv), "file", name);
        var resp = await _client.PostAsync(Uploads, form, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return Guid.Parse(body.GetProperty("uploadId").GetString()!);
    }
}
