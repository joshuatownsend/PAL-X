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
}

// ─── Stub Server ─────────────────────────────────────────────────────────────

internal sealed class StubApiServer : IAsyncDisposable
{
    public static readonly Guid KnownJobId = new("aaaaaaaa-0000-0000-0000-000000000000");

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
            Guid.TryParse(id, out var guid) && guid == KnownJobId
                ? Results.Ok(new { status = "completed" })
                : Results.NotFound());

        app.MapGet("/analysis/{id}/results", (string id) =>
            Guid.TryParse(id, out var guid) && guid == KnownJobId
                ? Results.Ok(new { summaryJson = "{\"status\":\"healthy\"}", findingsJson = "[]" })
                : Results.Conflict());

        app.MapGet("/analysis/{id}/report", (string id, string? format) =>
            Guid.TryParse(id, out var guid) && guid == KnownJobId
                ? Results.Content("<html><body>PAL Report</body></html>", "text/html")
                : Results.Conflict());

        await app.StartAsync();
        return new StubApiServer(app, app.Urls.First());
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}
