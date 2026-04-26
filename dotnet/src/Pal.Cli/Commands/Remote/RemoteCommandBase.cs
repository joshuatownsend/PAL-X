using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public abstract class RemoteSettings : CommandSettings
{
    [CommandOption("--api")]
    [Description("Base URL of the PAL API server")]
    [DefaultValue("http://localhost:8080")]
    public string ApiBase { get; init; } = "http://localhost:8080";

    [CommandOption("--api-key")]
    [Description("Personal access token for authentication (pal_...)")]
    public string? ApiKey { get; init; }
}

internal static class RemoteHttpClient
{
    public static HttpClient Create(string apiBase, string? apiKey = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(apiBase.TrimEnd('/') + "/") };
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }
}

internal static class RemoteMarkup
{
    internal static string FormatDirection(string dir) => dir switch
    {
        "worsening" => $"[red]{dir}[/]",
        "appearing" => $"[red]{dir}[/]",
        "stable" => $"[yellow]{dir}[/]",
        "intermittent" => $"[yellow]{dir}[/]",
        "de-escalating" => $"[green]{dir}[/]",
        "resolving" => $"[green]{dir}[/]",
        _ => Markup.Escape(dir)
    };

    internal static string FormatWindowDate(string raw) =>
        DateTimeOffset.TryParse(raw, out var dt) ? dt.ToString("yyyy-MM-dd") : Markup.Escape(raw);
}

internal static class RemoteCommand
{
    internal static async Task<int> RunAsync(Func<Task<int>> body)
    {
        try { return await body(); }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode is { } statusCode)
                AnsiConsole.MarkupLine($"[red]Request failed ({(int)statusCode} {statusCode}):[/] {Markup.Escape(ex.Message)}");
            else
                AnsiConsole.MarkupLine($"[red]API unreachable:[/] {Markup.Escape(ex.Message)}");
            return ExitCodes.GeneralFailure;
        }
        catch (TaskCanceledException)
        {
            AnsiConsole.MarkupLine("[red]Request timed out[/]");
            return ExitCodes.GeneralFailure;
        }
    }
}
