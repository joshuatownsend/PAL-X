using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Pal.Packs;

namespace Pal.Cli.Commands;

public sealed class ListPacksSettings : CommandSettings
{
    [CommandOption("--pack-dir <path>")]
    [Description("Additional search path (repeatable)")]
    public string[] PackDirs { get; init; } = [];

    [CommandOption("--json-output <path>")]
    [Description("Write pack list as JSON to this path")]
    public string? JsonOutput { get; init; }
}

public sealed class ListPacksCommand : Command<ListPacksSettings>
{
    protected override int Execute(CommandContext context, ListPacksSettings settings, CancellationToken cancellationToken)
    {
        var resolver = new PackResolver();
        var result = resolver.ListAvailable(settings.PackDirs);

        AnsiConsole.MarkupLine("[bold]Available packs[/]");
        if (result.Packs.Count == 0)
        {
            AnsiConsole.MarkupLine("  (none found on search path)");
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Pack");
            table.AddColumn("Version");
            table.AddColumn(new TableColumn("Rules").RightAligned());
            table.AddColumn("Auto-loads when");

            foreach (var p in result.Packs)
            {
                table.AddRow(
                    $"[cyan]{Markup.Escape(p.PackId)}[/]",
                    Markup.Escape(p.Version),
                    p.RuleCount.ToString(),
                    Markup.Escape(Abbreviate(p.Applicability)));
            }

            AnsiConsole.Write(table);
        }

        foreach (var error in result.Errors)
            AnsiConsole.MarkupLine($"[yellow]warning:[/] {Markup.Escape(error)}");

        if (settings.JsonOutput is not null)
        {
            File.WriteAllText(settings.JsonOutput,
                JsonSerializer.Serialize(result.Packs, new JsonSerializerOptions { WriteIndented = true }),
                new System.Text.UTF8Encoding(false));
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Keeps the applicability column readable. Some packs gate on dozens of counters
    /// (dynamics-crm and skype-for-business each list 60+), which would otherwise wrap the
    /// table across hundreds of terminal lines. `--json-output` always carries the full list.
    /// </summary>
    private const int MaxListedMetrics = 2;

    internal static string Abbreviate(string applicability)
    {
        var separator = applicability.IndexOf(": ", StringComparison.Ordinal);
        if (separator < 0) return applicability;

        var prefix = applicability[..separator];
        var metrics = applicability[(separator + 2)..].Split(", ", StringSplitOptions.RemoveEmptyEntries);
        if (metrics.Length <= MaxListedMetrics) return applicability;

        var shown = string.Join(", ", metrics.Take(MaxListedMetrics));
        return $"{prefix}: {shown} (+{metrics.Length - MaxListedMetrics} more)";
    }
}
