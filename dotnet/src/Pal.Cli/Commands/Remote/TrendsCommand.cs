using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class TrendsCommand : AsyncCommand<TrendsCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandOption("--last")]
        [Description("Number of most-recent completed jobs to include in the trend window")]
        [DefaultValue(10)]
        public int Last { get; init; } = 10;

        [CommandOption("--json")]
        [Description("Print raw trends JSON")]
        public bool Json { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.GetAsync($"trends/data?last={settings.Last}");
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();

            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
                return ExitCodes.Success;
            }

            int jobCount = doc.GetProperty("jobCount").GetInt32();
            if (jobCount < 2)
            {
                AnsiConsole.MarkupLine("[yellow]At least 2 completed runs needed for trend analysis.[/]");
                return ExitCodes.Success;
            }

            string start = RemoteMarkup.FormatWindowDate(doc.GetProperty("windowStart").GetString() ?? "");
            string end = RemoteMarkup.FormatWindowDate(doc.GetProperty("windowEnd").GetString() ?? "");
            AnsiConsole.MarkupLine($"[bold]Trends[/]  {jobCount} runs  {start} → {end}");

            var trends = doc.GetProperty("trends");
            if (trends.GetArrayLength() == 0)
            {
                AnsiConsole.MarkupLine("[green]No findings in the selected window.[/]");
                return ExitCodes.Success;
            }

            var table = new Table()
                .AddColumn("Direction")
                .AddColumn("Rule")
                .AddColumn("Metric")
                .AddColumn("Latest sev")
                .AddColumn("Runs");

            foreach (var t in trends.EnumerateArray())
            {
                var dir = t.GetProperty("direction").GetString() ?? "";
                var key = t.GetProperty("correlationKey").GetString() ?? "";
                var parts = key.Split(':', 2);
                string rule = parts[0];
                string metric = parts.Length > 1 ? parts[1] : "";
                var latestSev = t.TryGetProperty("latestSeverity", out var ls) && ls.ValueKind != JsonValueKind.Null
                    ? ls.GetString() ?? "—" : "—";
                int runCount = t.GetProperty("runCount").GetInt32();
                int totalRuns = t.GetProperty("totalRuns").GetInt32();

                table.AddRow(RemoteMarkup.FormatDirection(dir), Markup.Escape(rule), Markup.Escape(metric),
                    Markup.Escape(latestSev), $"{runCount}/{totalRuns}");
            }

            AnsiConsole.Write(table);
            return ExitCodes.Success;
        });
}
