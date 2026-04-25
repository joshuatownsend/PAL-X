using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class CompareCommand : AsyncCommand<CompareCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandOption("--baseline")]
        [Description("Job ID of the baseline run")]
        public required string BaselineJobId { get; init; }

        [CommandOption("--candidate")]
        [Description("Job ID of the candidate run to compare against baseline")]
        public required string CandidateJobId { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            if (!Guid.TryParse(settings.BaselineJobId, out var baselineId))
            {
                AnsiConsole.MarkupLine("[red]Invalid baseline job ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }
            if (!Guid.TryParse(settings.CandidateJobId, out var candidateId))
            {
                AnsiConsole.MarkupLine("[red]Invalid candidate job ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase);
            var body = JsonSerializer.Serialize(new
            {
                baselineJobId = baselineId,
                candidateJobId = candidateId
            });
            var resp = await client.PostAsync("compare",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();

            var summary = doc.GetProperty("summary");
            int newF = summary.GetProperty("newFindings").GetInt32();
            int resolved = summary.GetProperty("resolvedFindings").GetInt32();
            int unchanged = summary.GetProperty("unchangedFindings").GetInt32();
            int sevChanged = summary.GetProperty("severityChanges").GetInt32();

            AnsiConsole.MarkupLine($"[bold]Compare[/]  baseline=[cyan]{baselineId.ToString("N")[..8]}[/]  candidate=[cyan]{candidateId.ToString("N")[..8]}[/]");
            AnsiConsole.MarkupLine($"  [red]+{newF} new[/]   [green]-{resolved} resolved[/]   [yellow]~{sevChanged} severity changed[/]   {unchanged} unchanged");

            if (!doc.TryGetProperty("diffs", out var diffs) || diffs.GetArrayLength() == 0)
                return ExitCodes.Success;

            var table = new Table()
                .AddColumn("Status")
                .AddColumn("Rule")
                .AddColumn("Metric")
                .AddColumn("Baseline sev")
                .AddColumn("Candidate sev");

            foreach (var diff in diffs.EnumerateArray())
            {
                var status = diff.GetProperty("status").GetString() ?? "";
                var key = diff.GetProperty("correlationKey").GetString() ?? "";
                var parts = key.Split(':', 2);
                string rule = parts[0];
                string metric = parts.Length > 1 ? parts[1] : "";

                string bSev = diff.TryGetProperty("baselineFinding", out var bf) && bf.ValueKind != JsonValueKind.Null
                    ? bf.GetProperty("severity").GetString() ?? "" : "—";
                string cSev = diff.TryGetProperty("candidateFinding", out var cf) && cf.ValueKind != JsonValueKind.Null
                    ? cf.GetProperty("severity").GetString() ?? "" : "—";

                string statusMarkup = status switch
                {
                    "new" => "[red]new[/]",
                    "resolved" => "[green]resolved[/]",
                    "severity_changed" => "[yellow]sev_changed[/]",
                    _ => "unchanged"
                };
                table.AddRow(statusMarkup, Markup.Escape(rule), Markup.Escape(metric),
                    Markup.Escape(bSev), Markup.Escape(cSev));
            }

            AnsiConsole.Write(table);
            return ExitCodes.Success;
        });
}
