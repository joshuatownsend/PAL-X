using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteAlertAcknowledgeCommand : AsyncCommand<RemoteAlertAcknowledgeCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Alert ID (GUID)")]
        public required string Id { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            if (!Guid.TryParse(settings.Id, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid alert ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.PatchAsync($"alerts/{id}/acknowledge", content: null);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]Alert not found:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Alert is not in 'open' state[/]");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            AnsiConsole.MarkupLine($"[green]Alert acknowledged:[/] {id.ToString("N")[..8]}");
            return ExitCodes.Success;
        });
}
