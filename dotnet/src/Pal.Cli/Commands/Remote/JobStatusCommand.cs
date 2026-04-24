using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class JobStatusCommand : AsyncCommand<JobStatusCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<job-id>")]
        [Description("Analysis job ID (GUID)")]
        public required string JobId { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!Guid.TryParse(settings.JobId, out var id))
        {
            AnsiConsole.MarkupLine("[red]Invalid job ID — expected a GUID[/]");
            return ExitCodes.InvalidArguments;
        }

        using var client = RemoteHttpClient.Create(settings.ApiBase);
        var resp = await client.GetAsync($"analysis/{id}");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            AnsiConsole.MarkupLine($"[red]Job not found:[/] {id}");
            return ExitCodes.GeneralFailure;
        }
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var status = doc.GetProperty("status").GetString() ?? "unknown";

        var color = status switch
        {
            "completed" => "green",
            "failed" => "red",
            "running" => "yellow",
            _ => "blue"
        };
        AnsiConsole.MarkupLine($"[{color}]{status}[/]  {id}");

        if (status == "failed" && doc.TryGetProperty("failureReason", out var reason))
            AnsiConsole.MarkupLine($"[red]Reason:[/] {reason.GetString()}");

        if (status == "completed")
            AnsiConsole.MarkupLine($"[grey]Get results:[/] pal remote results {id} --api {settings.ApiBase}");

        return ExitCodes.Success;
    }
}
