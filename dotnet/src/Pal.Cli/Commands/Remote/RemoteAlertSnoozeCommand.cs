using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteAlertSnoozeCommand : AsyncCommand<RemoteAlertSnoozeCommand.Settings>
{
    private static readonly Regex DurationPattern = new(@"^(\d+)([mhd])$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Alert ID (GUID)")]
        public required string Id { get; init; }

        [CommandOption("-d|--duration")]
        [Description("Snooze duration: 30m, 2h, 1d (mutually exclusive with --until)")]
        public string? Duration { get; init; }

        [CommandOption("--until")]
        [Description("Absolute ISO 8601 timestamp (mutually exclusive with --duration)")]
        public string? Until { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => RemoteCommand.RunAsync(cancellationToken, async ct =>
        {
            if (!Guid.TryParse(settings.Id, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid alert ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }
            if ((settings.Duration is null) == (settings.Until is null))
            {
                AnsiConsole.MarkupLine("[red]Specify exactly one of --duration or --until[/]");
                return ExitCodes.InvalidArguments;
            }

            DateTimeOffset until;
            if (settings.Until is not null)
            {
                if (!DateTimeOffset.TryParse(settings.Until, out until))
                {
                    AnsiConsole.MarkupLine("[red]--until must be an ISO 8601 timestamp[/]");
                    return ExitCodes.InvalidArguments;
                }
            }
            else
            {
                var m = DurationPattern.Match(settings.Duration!);
                if (!m.Success
                    || !int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
                    || n <= 0
                    || n > 43200)  // 30 days in minutes — same cap the API enforces
                {
                    AnsiConsole.MarkupLine("[red]--duration must look like 30m, 2h, or 1d, with a value up to 30 days[/]");
                    return ExitCodes.InvalidArguments;
                }
                until = m.Groups[2].Value.ToLowerInvariant() switch
                {
                    "m" => DateTimeOffset.UtcNow.AddMinutes(n),
                    "h" => DateTimeOffset.UtcNow.AddHours(n),
                    "d" => DateTimeOffset.UtcNow.AddDays(n),
                    _ => DateTimeOffset.UtcNow
                };
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.PatchAsJsonAsync($"alerts/{id}/snooze", new { until }, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]Alert not found:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                AnsiConsole.MarkupLine($"[red]Snooze rejected:[/] {Markup.Escape(body)}");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Cannot snooze a resolved alert[/]");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            AnsiConsole.MarkupLine($"[green]Snoozed:[/] {id.ToString("N")[..8]} until {until.LocalDateTime:yyyy-MM-dd HH:mm}");
            return ExitCodes.Success;
        });
}
