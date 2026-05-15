using System.ComponentModel;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteDiagnosticsCommand : AsyncCommand<RemoteDiagnosticsCommand.Settings>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<job-id>")]
        [Description("Analysis job ID (GUID)")]
        public required string JobId { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => RemoteCommand.RunAsync(cancellationToken, async ct =>
        {
            if (!Guid.TryParse(settings.JobId, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid job ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.GetAsync($"analysis/{id}/diagnostics", ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]Job not found:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Job not yet complete — check status first[/]");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<DiagnosticsResponse>(JsonOptions, ct);

            if (body?.Items is null || body.Items.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No diagnostics insights for this job.[/]");
                return ExitCodes.Success;
            }

            var table = new Table()
                .AddColumn("Severity")
                .AddColumn("Category")
                .AddColumn("Title")
                .AddColumn("Source")
                .AddColumn("Rules");

            foreach (var insight in body.Items)
            {
                var sevMarkup = insight.Severity switch
                {
                    "critical" => $"[red]{insight.Severity}[/]",
                    "warning" => $"[yellow]{insight.Severity}[/]",
                    _ => $"[grey]{insight.Severity}[/]"
                };
                var source = insight.SourceDirection is not null
                    ? $"{insight.SourceType}/{RemoteMarkup.FormatDirection(insight.SourceDirection)}"
                    : insight.SourceType;
                table.AddRow(
                    sevMarkup,
                    Markup.Escape(insight.Category),
                    Markup.Escape(insight.Title),
                    source,
                    Markup.Escape(string.Join(", ", insight.AffectedRuleIds)));
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[grey]{body.Items.Count} insight(s). Run [/][bold]pal remote results {id}[/][grey] for full findings.[/]");
            return ExitCodes.Success;
        });

    private sealed record DiagnosticsResponse(List<InsightItem> Items);

    private sealed record InsightItem(
        string Severity,
        string Category,
        string Title,
        string Narrative,
        List<string> Recommendations,
        List<string> AffectedRuleIds,
        string SourceType,
        string? SourceDirection);
}
