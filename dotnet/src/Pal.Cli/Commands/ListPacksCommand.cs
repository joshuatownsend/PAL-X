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
    public override int Execute(CommandContext context, ListPacksSettings settings)
    {
        var resolver = new PackResolver();
        var result = resolver.Resolve([], settings.PackDirs, autoResolve: false);

        AnsiConsole.MarkupLine("[bold]Available packs[/]");
        if (result.Resolutions.Count == 0)
        {
            AnsiConsole.MarkupLine("  (none found on search path)");
        }
        else
        {
            foreach (var p in result.Resolutions)
                AnsiConsole.MarkupLine($"  - [cyan]{p.PackId,-20}[/] {p.Version}");
        }

        if (settings.JsonOutput is not null)
        {
            File.WriteAllText(settings.JsonOutput,
                JsonSerializer.Serialize(result.Resolutions, new JsonSerializerOptions { WriteIndented = true }),
                new System.Text.UTF8Encoding(false));
        }

        return ExitCodes.Success;
    }
}
