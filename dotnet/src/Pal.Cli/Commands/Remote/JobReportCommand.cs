using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class JobReportCommand : AsyncCommand<JobReportCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<job-id>")]
        [Description("Analysis job ID (GUID)")]
        public required string JobId { get; init; }

        [CommandOption("--format")]
        [Description("Report format: html (default) or json")]
        [DefaultValue("html")]
        public string Format { get; init; } = "html";

        [CommandOption("-o|--output")]
        [Description("Save report to this file path (default: print to stdout)")]
        public string? OutputPath { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            if (!Guid.TryParse(settings.JobId, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid job ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            if (settings.Format != "html" && settings.Format != "json")
            {
                AnsiConsole.MarkupLine("[red]--format must be 'html' or 'json'[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.GetAsync($"analysis/{id}/report?format={settings.Format}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]No {settings.Format} report for job:[/] {id}");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Job not yet complete — check status first[/]");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsByteArrayAsync();

            if (settings.OutputPath is not null)
            {
                await File.WriteAllBytesAsync(settings.OutputPath, content);
                AnsiConsole.MarkupLine($"[green]Saved:[/] {settings.OutputPath}");
            }
            else
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.Write(System.Text.Encoding.UTF8.GetString(content));
            }

            return ExitCodes.Success;
        });
}
