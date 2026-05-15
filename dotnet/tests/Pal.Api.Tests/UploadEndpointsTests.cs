using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class UploadEndpointsTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly string Uploads = $"{PalApiFactory.WsBase}/uploads";

    [Fact]
    public async Task Post_Upload_Returns201_WithUploadId()
    {
        var content = MakeCsvContent("test.csv", "small,data\n1,2");

        var resp = await _client.PostAsync(Uploads, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.TryGetProperty("uploadId", out var id));
        Assert.NotEqual(Guid.Empty, Guid.Parse(id.GetString()!));
    }

    [Fact]
    public async Task Post_Upload_DuplicateFile_Returns200_SameId()
    {
        var csv = "dupe,file\n42,99";

        var resp1 = await _client.PostAsync(Uploads, MakeCsvContent("dupe.csv", csv), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);
        var body1 = await resp1.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id1 = body1.GetProperty("uploadId").GetString();

        var resp2 = await _client.PostAsync(Uploads, MakeCsvContent("dupe.csv", csv), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var body2 = await resp2.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id2 = body2.GetProperty("uploadId").GetString();

        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task Post_Upload_NoFile_Returns400()
    {
        // Send a well-formed multipart body that has no "file" field
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("csv"), "sourceType");
        var resp = await _client.PostAsync(Uploads, form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private static MultipartFormDataContent MakeCsvContent(string fileName, string csv)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(csv), "file", fileName);
        return form;
    }
}
