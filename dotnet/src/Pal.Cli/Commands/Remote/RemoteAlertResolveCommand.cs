using System.ComponentModel;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteAlertResolveCommand : AsyncCommand<RemoteAlertResolveCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Alert ID (GUID)")]
        public required string Id { get; init; }

        [CommandOption("-n|--note")]
        [Description("Resolution note (optional, free text)")]
        public string? Note { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => RemoteCommand.RunAsync(cancellationToken, async ct =>
        {
            if (!Guid.TryParse(settings.Id, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid alert ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.PatchAsJsonAsync($"alerts/{id}/resolve", new { note = settings.Note }, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]Alert not found:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Alert is already resolved[/]");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            AnsiConsole.MarkupLine($"[green]Alert resolved:[/] {id.ToString("N")[..8]}");
            return ExitCodes.Success;
        });
}
