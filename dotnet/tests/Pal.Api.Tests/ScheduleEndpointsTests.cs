using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pal.Api.Tests;

[Collection("PalApi")]
public sealed class ScheduleEndpointsTests(PalApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly string Schedules = $"{PalApiFactory.WsBase}/schedules";

    private static string ValidSourceConfigJson() => JsonSerializer.Serialize(new
    {
        type = "directory",
        path = OperatingSystem.IsWindows() ? @"C:\PerfLogs\WEB-01" : "/var/perflogs/web-01",
        glob = "*.csv"
    });

    private static object MakeRequest(string? name = null, int interval = 15,
        string? sourceConfig = null, string[]? packIds = null, bool enabled = true) => new
    {
        name = name ?? $"test-schedule-{Guid.NewGuid():N}",
        intervalMinutes = interval,
        sourceConfigJson = sourceConfig ?? ValidSourceConfigJson(),
        packIds = packIds ?? new[] { "windows-core" },
        enabled
    };

    [Fact]
    public async Task Post_Create_ValidRequest_Returns201()
    {
        var resp = await _client.PostAsJsonAsync(Schedules, MakeRequest());
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal(15, body.GetProperty("intervalMinutes").GetInt32());
        Assert.True(body.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Post_Create_BadInterval_Returns400()
    {
        var resp = await _client.PostAsJsonAsync(Schedules, MakeRequest(interval: 1));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("intervalMinutes", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_Create_RelativePath_Returns400()
    {
        var bad = JsonSerializer.Serialize(new { type = "directory", path = "relative/path", glob = "*.csv" });
        var resp = await _client.PostAsJsonAsync(Schedules, MakeRequest(sourceConfig: bad));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_Create_EmptyPackIds_Returns400()
    {
        var resp = await _client.PostAsJsonAsync(Schedules, MakeRequest(packIds: Array.Empty<string>()));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Get_List_ContainsCreated()
    {
        var name = $"list-test-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync(Schedules, MakeRequest(name: name));

        var resp = await _client.GetAsync($"{Schedules}/data");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray();
        Assert.Contains(items, i => i.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Patch_Enabled_TogglesFlag()
    {
        var create = await _client.PostAsJsonAsync(Schedules, MakeRequest(enabled: true));
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var resp = await _client.PatchAsJsonAsync($"{Schedules}/{id}/enabled", new { enabled = false });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var get = await _client.GetAsync($"{Schedules}/{id}");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Delete_RemovesSchedule()
    {
        var create = await _client.PostAsJsonAsync(Schedules, MakeRequest());
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var del = await _client.DeleteAsync($"{Schedules}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await _client.GetAsync($"{Schedules}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Post_Create_DuplicateName_Returns409Conflict()
    {
        var name = $"dup-test-{Guid.NewGuid():N}";
        var first = await _client.PostAsJsonAsync(Schedules, MakeRequest(name: name));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Endpoint maps the unique (workspace_id, name) violation to 409 with a clear
        // error payload rather than letting it bubble as a 500.
        var second = await _client.PostAsJsonAsync(Schedules, MakeRequest(name: name));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("already exists", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Post_Create_LocationHeaderIsWorkspaceScoped()
    {
        var resp = await _client.PostAsJsonAsync(Schedules, MakeRequest());
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // Location must include the /api/workspaces/{wsId}/ prefix so a client following
        // the header lands on a real route, not a workspace-less GET that would 404.
        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/api/workspaces/", location);
        Assert.Contains("/schedules/", location);
    }
}
