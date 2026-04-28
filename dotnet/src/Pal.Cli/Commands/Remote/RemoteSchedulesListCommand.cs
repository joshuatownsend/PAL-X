using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteSchedulesListCommand : AsyncCommand<RemoteSchedulesListCommand.Settings>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public sealed class Settings : RemoteSettings { }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.GetAsync("schedules/data");
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<SchedulesResponse>(JsonOptions);
            if (body?.Items is null || body.Items.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No schedules.[/]");
                return ExitCodes.Success;
            }

            var table = new Table()
                .AddColumn("ID")
                .AddColumn("Name")
                .AddColumn("Enabled")
                .AddColumn("Interval")
                .AddColumn("Packs")
                .AddColumn("Last run")
                .AddColumn("Next run");

            foreach (var s in body.Items)
            {
                table.AddRow(
                    s.Id.ToString("N")[..8],
                    Markup.Escape(s.Name),
                    s.Enabled ? "[green]yes[/]" : "[grey]no[/]",
                    $"{s.IntervalMinutes}m",
                    Markup.Escape(string.Join(",", s.PackIds)),
                    s.LastRunAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "—",
                    s.NextRunAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "—");
            }

            AnsiConsole.Write(table);
            return ExitCodes.Success;
        });

    private sealed record SchedulesResponse(List<ScheduleItem> Items);
    private sealed record ScheduleItem(
        Guid Id, string Name, int IntervalMinutes, string SourceConfigJson,
        IReadOnlyList<string> PackIds, bool Enabled,
        DateTimeOffset? LastRunAt, DateTimeOffset? NextRunAt,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}
