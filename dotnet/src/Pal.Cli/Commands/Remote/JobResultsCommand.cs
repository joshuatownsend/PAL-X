using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class JobResultsCommand : AsyncCommand<JobResultsCommand.Settings>
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<job-id>")]
        [Description("Analysis job ID (GUID)")]
        public required string JobId { get; init; }

        [CommandOption("--json")]
        [Description("Print raw findings JSON")]
        public bool Json { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            if (!Guid.TryParse(settings.JobId, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid job ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase);
            var resp = await client.GetAsync($"analysis/{id}/results");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]No results for job:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Job not yet complete — check status first[/]");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();

            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(doc, IndentedOptions));
                return ExitCodes.Success;
            }

            if (doc.TryGetProperty("summaryJson", out var summaryEl))
            {
                var summary = JsonSerializer.Deserialize<JsonElement>(summaryEl.GetString()!);
                if (summary.TryGetProperty("status", out var st))
                    AnsiConsole.MarkupLine($"Overall status: [{StatusColor(st.GetString())}]{st.GetString()}[/]");
            }

            if (doc.TryGetProperty("findingsJson", out var findingsEl))
            {
                var findings = JsonSerializer.Deserialize<JsonElement[]>(findingsEl.GetString()!) ?? [];
                if (findings.Length == 0)
                {
                    AnsiConsole.MarkupLine("[green]No findings — dataset within expected parameters[/]");
                    return ExitCodes.Success;
                }

                var table = new Table().AddColumn("Sev").AddColumn("Category").AddColumn("Title");
                foreach (var f in findings)
                {
                    var sev = f.TryGetProperty("severity", out var s) ? s.GetString() ?? "" : "";
                    var cat = f.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "";
                    var title = f.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    table.AddRow($"[{StatusColor(sev)}]{sev}[/]", cat, title);
                }
                AnsiConsole.Write(table);
            }

            return ExitCodes.Success;
        });

    private static string StatusColor(string? s) => s switch
    {
        "critical" or "Critical" => "red",
        "warning" or "Warning" => "yellow",
        "healthy" or "Healthy" or "informational" or "Informational" => "green",
        _ => "white"
    };
}
