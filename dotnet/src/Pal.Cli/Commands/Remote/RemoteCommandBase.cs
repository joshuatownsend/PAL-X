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
}

internal static class RemoteHttpClient
{
    public static HttpClient Create(string apiBase) =>
        new() { BaseAddress = new Uri(apiBase.TrimEnd('/') + "/") };
}

internal static class RemoteCommand
{
    internal static async Task<int> RunAsync(Func<Task<int>> body)
    {
        try { return await body(); }
        catch (HttpRequestException ex)
        {
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
