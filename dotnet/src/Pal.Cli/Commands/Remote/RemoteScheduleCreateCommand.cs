using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteScheduleCreateCommand : AsyncCommand<RemoteScheduleCreateCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandOption("-n|--name")]
        [Description("Human-readable schedule name (unique within the workspace)")]
        public required string Name { get; init; }

        [CommandOption("-i|--interval")]
        [Description("Polling interval in minutes (5–1440)")]
        public required int IntervalMinutes { get; init; }

        [CommandOption("--path")]
        [Description("Absolute directory path to scan for new perfmon files")]
        public required string Path { get; init; }

        [CommandOption("--glob")]
        [Description("File glob pattern, e.g. *.csv or *.blg")]
        [DefaultValue("*.csv")]
        public string Glob { get; init; } = "*.csv";

        [CommandOption("-p|--pack")]
        [Description("Pack ID(s) to run on each ingested file (repeatable)")]
        public string[] Packs { get; init; } = ["windows-core"];

        [CommandOption("--disabled")]
        [Description("Create the schedule in disabled state")]
        public bool Disabled { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);

            var sourceConfig = JsonSerializer.Serialize(new
            {
                type = "directory",
                path = settings.Path,
                glob = settings.Glob
            });

            var resp = await client.PostAsJsonAsync("schedules", new
            {
                name = settings.Name,
                intervalMinutes = settings.IntervalMinutes,
                sourceConfigJson = sourceConfig,
                packIds = settings.Packs,
                enabled = !settings.Disabled
            });

            if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var body = await resp.Content.ReadAsStringAsync();
                AnsiConsole.MarkupLine($"[red]Validation failed:[/] {Markup.Escape(body)}");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var id = doc.RootElement.GetProperty("id").GetGuid();

            AnsiConsole.MarkupLine($"[green]Schedule created:[/] {id.ToString("N")[..8]} ({Markup.Escape(settings.Name)})");
            AnsiConsole.MarkupLine($"[grey]Interval:[/] {settings.IntervalMinutes}m  [grey]Path:[/] {Markup.Escape(settings.Path)}  [grey]Glob:[/] {Markup.Escape(settings.Glob)}");
            return ExitCodes.Success;
        });
}
