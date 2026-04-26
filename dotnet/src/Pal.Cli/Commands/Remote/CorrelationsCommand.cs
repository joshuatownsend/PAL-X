using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class CorrelationsCommand : AsyncCommand<CorrelationsCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandOption("--last")]
        [Description("Number of most-recent completed jobs to include in the correlation window")]
        [DefaultValue(10)]
        public int Last { get; init; } = 10;

        [CommandOption("--json")]
        [Description("Print raw correlations JSON")]
        public bool Json { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            using var client = RemoteHttpClient.Create(settings.ApiBase);
            var resp = await client.GetAsync($"correlations/data?last={settings.Last}");
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
                AnsiConsole.MarkupLine("[yellow]At least 2 completed runs needed for correlation analysis.[/]");
                return ExitCodes.Success;
            }

            string start = RemoteMarkup.FormatWindowDate(doc.GetProperty("windowStart").GetString() ?? "");
            string end = RemoteMarkup.FormatWindowDate(doc.GetProperty("windowEnd").GetString() ?? "");
            AnsiConsole.MarkupLine($"[bold]Correlations[/]  {jobCount} runs  {start} → {end}");

            var pairs = doc.GetProperty("pairs");
            if (pairs.GetArrayLength() == 0)
            {
                AnsiConsole.MarkupLine("[green]No co-occurring findings in the selected window.[/]");
                return ExitCodes.Success;
            }

            var table = new Table()
                .AddColumn("Signal A")
                .AddColumn("Dir A")
                .AddColumn("Signal B")
                .AddColumn("Dir B")
                .AddColumn("Co-runs")
                .AddColumn("Score");

            foreach (var p in pairs.EnumerateArray())
            {
                string keyA = Markup.Escape(p.GetProperty("keyA").GetString() ?? "");
                string keyB = Markup.Escape(p.GetProperty("keyB").GetString() ?? "");
                string dirA = p.GetProperty("directionA").GetString() ?? "";
                string dirB = p.GetProperty("directionB").GetString() ?? "";
                int coRuns = p.GetProperty("coRunCount").GetInt32();
                int totalRuns = p.GetProperty("totalRuns").GetInt32();
                double coScore = p.GetProperty("coScore").GetDouble();

                table.AddRow(keyA, RemoteMarkup.FormatDirection(dirA), keyB, RemoteMarkup.FormatDirection(dirB),
                    $"{coRuns}/{totalRuns}", $"{coScore:P0}");
            }

            AnsiConsole.Write(table);
            return ExitCodes.Success;
        });
}
