using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Spectre.Console.Cli;
using Pal.Cli.Commands.Remote;
using Xunit;

namespace Pal.Cli.Tests;

public class RemoteCommandTests
{
    // ─── packs ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemotePacks_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<RemotePacksCommand>().RunAsync(["--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.Success, code);
    }

    [Fact]
    public async Task RemotePacks_ServerUnreachable_ReturnsGeneralFailure()
    {
        string apiUrl;
        await using (var server = await StubApiServer.StartAsync())
            apiUrl = server.BaseUrl;

        // server is disposed — port is now closed, connection will be refused
        var code = await new CommandApp<RemotePacksCommand>().RunAsync(["--api", apiUrl]);
        Assert.Equal(ExitCodes.GeneralFailure, code);
    }

    // ─── status ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task JobStatus_InvalidGuid_ReturnsInvalidArguments()
    {
        var code = await new CommandApp<JobStatusCommand>().RunAsync(["not-a-guid"]);
        Assert.Equal(ExitCodes.InvalidArguments, code);
    }

    [Fact]
    public async Task JobStatus_JobNotFound_ReturnsGeneralFailure()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<JobStatusCommand>().RunAsync(
            [Guid.NewGuid().ToString(), "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.GeneralFailure, code);
    }

    [Fact]
    public async Task JobStatus_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<JobStatusCommand>().RunAsync(
            [StubApiServer.KnownJobId.ToString(), "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.Success, code);
    }

    // ─── submit ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_FileNotFound_ReturnsInvalidArguments()
    {
        var code = await new CommandApp<SubmitCommand>().RunAsync(
            ["--file", "nonexistent-file.csv", "--api", "http://127.0.0.1:59999"]);
        Assert.Equal(ExitCodes.InvalidArguments, code);
    }

    [Fact]
    public async Task Submit_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, "(PDH-CSV 4.0),(\\SERVER\\Processor(_Total)\\% Processor Time)\n" +
                "\"1/1/2026 0:00:00.000\",\"10.0\"\n");
            var code = await new CommandApp<SubmitCommand>().RunAsync(
                ["--file", tmpFile, "--pack", "windows-core", "--api", server.BaseUrl]);
            Assert.Equal(ExitCodes.Success, code);
        }
        finally { File.Delete(tmpFile); }
    }

    // ─── results ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task JobResults_JobNotReady_ReturnsGeneralFailure()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<JobResultsCommand>().RunAsync(
            [Guid.NewGuid().ToString(), "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.GeneralFailure, code);
    }

    [Fact]
    public async Task JobResults_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<JobResultsCommand>().RunAsync(
            [StubApiServer.KnownJobId.ToString(), "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.Success, code);
    }

    [Fact]
    public async Task JobResults_Verbose_WithRecommendations_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<JobResultsCommand>().RunAsync(
            [StubApiServer.KnownJobWithRecsId.ToString(), "--verbose", "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.Success, code);
    }

    // ─── report ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task JobReport_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var tmpOut = Path.GetTempFileName();
        try
        {
            var code = await new CommandApp<JobReportCommand>().RunAsync(
                [StubApiServer.KnownJobId.ToString(), "--format", "html", "--output", tmpOut, "--api", server.BaseUrl]);
            Assert.Equal(ExitCodes.Success, code);
            Assert.True(new FileInfo(tmpOut).Length > 0, "Report file should be non-empty");
        }
        finally { if (File.Exists(tmpOut)) File.Delete(tmpOut); }
    }

    // ─── validate-pack ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemoteValidatePack_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<RemoteValidatePackCommand>().RunAsync(
            ["windows-core", "1.0.0", "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.Success, code);
    }

    [Fact]
    public async Task RemoteValidatePack_Invalid_ReturnsPackValidationFailure()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<RemoteValidatePackCommand>().RunAsync(
            [StubApiServer.InvalidPackId, "1.0.0", "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.PackValidationFailure, code);
    }

    [Fact]
    public async Task RemoteValidatePack_NotFound_ReturnsGeneralFailure()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<RemoteValidatePackCommand>().RunAsync(
            ["nonexistent-pack", "9.9.9", "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.GeneralFailure, code);
    }

    // ─── dataset ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoteDataset_InvalidGuid_ReturnsInvalidArguments()
    {
        var code = await new CommandApp<RemoteDatasetCommand>().RunAsync(
            ["not-a-guid", "--output", "out.json.gz", "--api", "http://127.0.0.1:59999"]);
        Assert.Equal(ExitCodes.InvalidArguments, code);
    }

    [Fact]
    public async Task RemoteDataset_Success()
    {
        await using var server = await StubApiServer.StartAsync();
        var tmpOut = Path.GetTempFileName();
        try
        {
            var code = await new CommandApp<RemoteDatasetCommand>().RunAsync(
                [StubApiServer.KnownJobId.ToString(), "--output", tmpOut, "--api", server.BaseUrl]);
            Assert.Equal(ExitCodes.Success, code);
            Assert.True(new FileInfo(tmpOut).Length > 0, "Dataset file should be non-empty");
        }
        finally { if (File.Exists(tmpOut)) File.Delete(tmpOut); }
    }

    [Fact]
    public async Task RemoteDataset_NotFound_ReturnsGeneralFailure()
    {
        await using var server = await StubApiServer.StartAsync();
        var code = await new CommandApp<RemoteDatasetCommand>().RunAsync(
            [Guid.NewGuid().ToString(), "--output", "out.json.gz", "--api", server.BaseUrl]);
        Assert.Equal(ExitCodes.GeneralFailure, code);
    }
}

// ─── Stub Server ─────────────────────────────────────────────────────────────

internal sealed class StubApiServer : IAsyncDisposable
{
    public static readonly Guid KnownJobId = new("aaaaaaaa-0000-0000-0000-000000000000");
    public static readonly Guid KnownJobWithRecsId = new("bbbbbbbb-0000-0000-0000-000000000000");
    public const string InvalidPackId = "broken-pack";

    private static readonly byte[] MinimalGzip = CreateMinimalGzip();

    private readonly WebApplication _app;
    public string BaseUrl { get; }

    private StubApiServer(WebApplication app, string url)
    {
        _app = app;
        BaseUrl = url;
    }

    public static async Task<StubApiServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        app.MapGet("/packs", () => Results.Ok(new
        {
            items = new[] { new { id = "windows-core", currentVersion = "1.0.0", title = "Windows Core", status = "active" } }
        }));

        app.MapGet("/packs/{id}/versions/{version}/validation", (string id, string version) =>
        {
            if (id == InvalidPackId)
                return Results.Ok(new
                {
                    isValid = false,
                    errors = new[] { "pack_id is not valid kebab-case" },
                    warnings = Array.Empty<string>()
                });
            if (id == "nonexistent-pack")
                return Results.NotFound();
            return Results.Ok(new { isValid = true, errors = Array.Empty<string>(), warnings = Array.Empty<string>() });
        });

        app.MapPost("/uploads", () => Results.Ok(new
        {
            uploadId = KnownJobId,
            fileName = "test.csv",
            sourceType = "csv"
        }));

        app.MapPost("/analysis", () => Results.Ok(new
        {
            analysisId = KnownJobId,
            status = "queued"
        }));

        app.MapGet("/analysis/{id}", (string id) =>
            Guid.TryParse(id, out var guid) && (guid == KnownJobId || guid == KnownJobWithRecsId)
                ? Results.Ok(new { status = "completed" })
                : Results.NotFound());

        app.MapGet("/analysis/{id}/results", (string id) =>
        {
            if (!Guid.TryParse(id, out var guid)) return Results.Conflict();
            if (guid == KnownJobId)
                return Results.Ok(new { summaryJson = "{\"status\":\"healthy\"}", findingsJson = "[]" });
            if (guid == KnownJobWithRecsId)
            {
                const string findingsWithRecs =
                    "[{\"severity\":\"warning\",\"category\":\"cpu\",\"title\":\"High CPU\"," +
                    "\"pack_id\":\"windows-core\",\"summary\":\"CPU usage is high.\"," +
                    "\"recommendations\":[{\"priority\":\"high\",\"text\":\"Identify top processes.\"}]}]";
                return Results.Ok(new { summaryJson = "{\"status\":\"warning\"}", findingsJson = findingsWithRecs });
            }
            return Results.Conflict();
        });

        app.MapGet("/analysis/{id}/report", (string id, string? format) =>
            Guid.TryParse(id, out var guid) && guid == KnownJobId
                ? Results.Content("<html><body>PAL Report</body></html>", "text/html")
                : Results.Conflict());

        app.MapGet("/analysis/{id}/dataset", (string id) =>
            Guid.TryParse(id, out var guid) && guid == KnownJobId
                ? Results.Bytes(MinimalGzip, "application/gzip", $"pal-dataset-{id}.json.gz")
                : Results.NotFound());

        await app.StartAsync();
        return new StubApiServer(app, app.Urls.First());
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static byte[] CreateMinimalGzip()
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest))
            gz.Write("{\"series\":[]}"u8.ToArray());
        return ms.ToArray();
    }
}
