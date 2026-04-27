using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteDatasetCommand : AsyncCommand<RemoteDatasetCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<job-id>")]
        [Description("Analysis job ID (GUID)")]
        public required string JobId { get; init; }

        [CommandOption("-o|--output")]
        [Description("File path to save the dataset (e.g. dataset.json.gz)")]
        public required string OutputPath { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            if (!Guid.TryParse(settings.JobId, out var id))
            {
                AnsiConsole.MarkupLine("[red]Invalid job ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.GetAsync($"analysis/{id}/dataset", HttpCompletionOption.ResponseHeadersRead);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]No dataset artifact for job:[/] {id}");
                AnsiConsole.MarkupLine("[grey]Tip: submit with --include-dataset to persist one[/]");
                return ExitCodes.GeneralFailure;
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                AnsiConsole.MarkupLine("[yellow]Job not yet complete — check status first[/]");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            await using var responseStream = await resp.Content.ReadAsStreamAsync();
            await using (var fs = new FileStream(settings.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await responseStream.CopyToAsync(fs);

            var savedBytes = new FileInfo(settings.OutputPath).Length;
            AnsiConsole.MarkupLine($"[green]Saved:[/] {settings.OutputPath} ({savedBytes:N0} bytes)");

            return ExitCodes.Success;
        });
}
