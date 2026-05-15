using System.ComponentModel;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteBaselineSetCommand : AsyncCommand<RemoteBaselineSetCommand.Settings>
{
    private static readonly HashSet<string> ValidTypes = ["machine", "role", "workload", "release"];

    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<job-id>")]
        [Description("Job ID (GUID) to designate as a baseline")]
        public required string JobId { get; init; }

        [CommandOption("-l|--label")]
        [Description("Human-readable baseline label (e.g. WEB-01)")]
        public string? Label { get; init; }

        [CommandOption("-t|--type")]
        [Description("Baseline type: machine, role, workload, release")]
        public string? Type { get; init; }

        [CommandOption("-c|--context")]
        [Description("Context JSON (e.g. '{\"machine\":\"WEB-01\"}')")]
        public string? ContextJson { get; init; }

        [CommandOption("--clear")]
        [Description("Remove the baseline designation from this job")]
        public bool Clear { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => RemoteCommand.RunAsync(cancellationToken, async ct =>
        {
            if (!Guid.TryParse(settings.JobId, out var jobId))
            {
                AnsiConsole.MarkupLine("[red]Invalid job ID — expected a GUID[/]");
                return ExitCodes.InvalidArguments;
            }

            var normalizedType = settings.Type?.ToLowerInvariant();
            if (normalizedType is not null && !ValidTypes.Contains(normalizedType))
            {
                AnsiConsole.MarkupLine($"[red]Invalid type '{settings.Type}' — must be one of: machine, role, workload, release[/]");
                return ExitCodes.InvalidArguments;
            }

            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);

            var payload = new
            {
                isBaseline = !settings.Clear,
                label = settings.Clear ? null : settings.Label,
                type = settings.Clear ? null : normalizedType,
                contextJson = settings.Clear ? null : settings.ContextJson
            };

            var resp = await client.PatchAsJsonAsync($"analysis/{jobId}/baseline", payload, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                AnsiConsole.MarkupLine($"[red]Failed:[/] {resp.StatusCode} — {Markup.Escape(body)}");
                return ExitCodes.GeneralFailure;
            }

            if (settings.Clear)
                AnsiConsole.MarkupLine($"[grey]Baseline designation cleared for job {jobId.ToString("N")[..8]}[/]");
            else
            {
                var parts = new List<string>();
                if (normalizedType is not null) parts.Add($"type={normalizedType}");
                if (settings.Label is not null) parts.Add($"label={settings.Label}");
                var detail = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
                AnsiConsole.MarkupLine($"[green]Baseline set:[/] {jobId.ToString("N")[..8]}{detail}");
            }

            return ExitCodes.Success;
        });
}
