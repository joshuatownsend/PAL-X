using System.ComponentModel;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteBaselinesListCommand : AsyncCommand<RemoteBaselinesListCommand.Settings>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public sealed class Settings : RemoteSettings
    {
        [CommandOption("-t|--type")]
        [Description("Filter by baseline type: machine, role, workload, release")]
        public string? Type { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var url = settings.Type is not null
                ? $"analysis/baselines?type={Uri.EscapeDataString(settings.Type)}"
                : "analysis/baselines";

            var resp = await client.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<BaselinesResponse>(JsonOptions);

            if (body?.Items is null || body.Items.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No baselines designated yet.[/]");
                return ExitCodes.Success;
            }

            var table = new Table()
                .AddColumn("Job")
                .AddColumn("Type")
                .AddColumn("Label")
                .AddColumn("Created")
                .AddColumn("Packs");

            foreach (var b in body.Items)
            {
                table.AddRow(
                    Markup.Escape(b.Id.ToString("N")[..8]),
                    Markup.Escape(b.BaselineType ?? "—"),
                    Markup.Escape(b.BaselineLabel ?? "—"),
                    Markup.Escape(b.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm")),
                    Markup.Escape(string.Join(", ", b.Packs.Select(p => $"{p.PackId} v{p.PackVersion}"))));
            }

            AnsiConsole.Write(table);
            return ExitCodes.Success;
        });

    private sealed record BaselinesResponse(List<BaselineItem> Items);

    private sealed record BaselineItem(
        Guid Id,
        string? BaselineType,
        string? BaselineLabel,
        DateTimeOffset CreatedAt,
        List<PackRef> Packs);

    private sealed record PackRef(string PackId, string PackVersion);
}
