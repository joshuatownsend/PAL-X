using System.ComponentModel;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class SubmitCommand : AsyncCommand<SubmitCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandOption("-f|--file")]
        [Description("Path to the CSV or BLG file to analyze")]
        public required string File { get; init; }

        [CommandOption("-p|--pack")]
        [Description("Pack ID(s) to run (repeatable)")]
        public string[] Packs { get; init; } = ["windows-core"];

        [CommandOption("--include-dataset")]
        [Description("Persist normalized dataset artifact for later download")]
        public bool IncludeDataset { get; init; }

        [CommandOption("--baseline")]
        [Description("Baseline job ID (GUID) — auto-compares against this baseline on completion")]
        public string? BaselineId { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => RemoteCommand.RunAsync(cancellationToken, async ct =>
        {
            if (!System.IO.File.Exists(settings.File))
            {
                AnsiConsole.MarkupLine($"[red]File not found:[/] {settings.File}");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);

            Guid uploadId;
            await using (var fs = System.IO.File.OpenRead(settings.File))
            {
                var form = new MultipartFormDataContent();
                form.Add(new StreamContent(fs), "file", Path.GetFileName(settings.File));

                AnsiConsole.MarkupLine($"[grey]Uploading {Path.GetFileName(settings.File)}…[/]");
                var uploadResp = await client.PostAsync("uploads", form, ct);
                if (!uploadResp.IsSuccessStatusCode)
                {
                    AnsiConsole.MarkupLine($"[red]Upload failed:[/] {uploadResp.StatusCode}");
                    return ExitCodes.GeneralFailure;
                }

                var uploadBody = await uploadResp.Content.ReadFromJsonAsync<UploadResponse>(ct);
                uploadId = uploadBody!.UploadId;
                AnsiConsole.MarkupLine($"[grey]Upload id:[/] {uploadId}");
            }

            Guid? selectedBaselineId = null;
            if (settings.BaselineId is not null)
            {
                if (!Guid.TryParse(settings.BaselineId, out var bId))
                {
                    AnsiConsole.MarkupLine("[red]Invalid baseline ID — expected a GUID[/]");
                    return ExitCodes.InvalidArguments;
                }
                selectedBaselineId = bId;
            }

            var jobResp = await client.PostAsJsonAsync("analysis", new
            {
                uploadId,
                packs = settings.Packs,
                includeDataset = settings.IncludeDataset,
                selectedBaselineId
            }, ct);

            if (!jobResp.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[red]Job creation failed:[/] {jobResp.StatusCode}");
                return ExitCodes.GeneralFailure;
            }

            var job = await jobResp.Content.ReadFromJsonAsync<JobCreatedResponse>(ct);
            AnsiConsole.MarkupLine($"[green]Job queued:[/] {job!.AnalysisId}");
            AnsiConsole.MarkupLine($"[grey]Poll status:[/] pal remote status {job.AnalysisId} --api {settings.ApiBase}");
            return ExitCodes.Success;
        });

    private sealed record UploadResponse(Guid UploadId, string FileName, string SourceType);
    private sealed record JobCreatedResponse(Guid AnalysisId, string Status);
}
