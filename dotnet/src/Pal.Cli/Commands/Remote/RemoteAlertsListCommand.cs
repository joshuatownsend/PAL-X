using System.ComponentModel;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteAlertsListCommand : AsyncCommand<RemoteAlertsListCommand.Settings>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public sealed class Settings : RemoteSettings
    {
        [CommandOption("-s|--status")]
        [Description("Filter by status: open, acknowledged, resolved")]
        public string? Status { get; init; }

        [CommandOption("--severity")]
        [Description("Filter by severity: critical, warning, informational")]
        public string? Severity { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);

            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(settings.Status)) query.Add($"status={Uri.EscapeDataString(settings.Status!)}");
            if (!string.IsNullOrWhiteSpace(settings.Severity)) query.Add($"severity={Uri.EscapeDataString(settings.Severity!)}");
            var url = "alerts/data" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

            var resp = await client.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<AlertsResponse>(JsonOptions);
            if (body?.Items is null || body.Items.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No alerts.[/]");
                return ExitCodes.Success;
            }

            var table = new Table()
                .AddColumn("ID")
                .AddColumn("Severity")
                .AddColumn("Status")
                .AddColumn("Rule")
                .AddColumn("Title")
                .AddColumn("Last seen")
                .AddColumn("Snooze")
                .AddColumn("Policy");

            foreach (var a in body.Items)
            {
                var sevMarkup = a.Severity switch
                {
                    "critical" => $"[red]{a.Severity}[/]",
                    "warning" => $"[yellow]{a.Severity}[/]",
                    _ => $"[grey]{a.Severity}[/]"
                };
                var snooze = a.SnoozedUntil is { } until && until > DateTimeOffset.UtcNow
                    ? until.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                    : "—";
                table.AddRow(
                    a.Id.ToString("N")[..8],
                    sevMarkup,
                    Markup.Escape(a.Status),
                    Markup.Escape(a.RuleId),
                    Markup.Escape(a.Title),
                    a.LastSeenAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                    snooze,
                    Markup.Escape(a.PolicyApplied ?? "—"));
            }

            AnsiConsole.Write(table);
            return ExitCodes.Success;
        });

    private sealed record AlertsResponse(List<AlertItem> Items);
    private sealed record AlertItem(
        Guid Id, string RuleId, string Severity, string Category, string Title, string Status,
        DateTimeOffset LastSeenAt, DateTimeOffset? SnoozedUntil, string? PolicyApplied);
}
