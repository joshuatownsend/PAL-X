using System.ComponentModel;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteScheduleEnableCommand : AsyncCommand<RemoteScheduleEnableCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Schedule ID (GUID)")]
        public required string Id { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => Toggle(settings, enable: true, cancellationToken);

    internal static Task<int> Toggle(Settings settings, bool enable, CancellationToken cancellationToken)
        => RemoteCommand.RunAsync(cancellationToken, async ct =>
        {
            if (!Guid.TryParse(settings.Id, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid schedule ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.PatchAsJsonAsync($"schedules/{id}/enabled", new { enabled = enable }, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]Schedule not found:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            AnsiConsole.MarkupLine(enable
                ? $"[green]Enabled:[/] {id.ToString("N")[..8]}"
                : $"[yellow]Disabled:[/] {id.ToString("N")[..8]}");
            return ExitCodes.Success;
        });
}

public sealed class RemoteScheduleDisableCommand : AsyncCommand<RemoteScheduleEnableCommand.Settings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, RemoteScheduleEnableCommand.Settings settings, CancellationToken cancellationToken)
        => RemoteScheduleEnableCommand.Toggle(settings, enable: false, cancellationToken);
}
