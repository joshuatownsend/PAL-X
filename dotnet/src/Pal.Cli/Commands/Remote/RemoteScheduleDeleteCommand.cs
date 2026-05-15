using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteScheduleDeleteCommand : AsyncCommand<RemoteScheduleDeleteCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Schedule ID (GUID)")]
        public required string Id { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => RemoteCommand.RunAsync(cancellationToken, async ct =>
        {
            if (!Guid.TryParse(settings.Id, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid schedule ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.DeleteAsync($"schedules/{id}", ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]Schedule not found:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            AnsiConsole.MarkupLine($"[green]Deleted:[/] {id.ToString("N")[..8]}");
            return ExitCodes.Success;
        });
}
