using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemotePacksCommand : AsyncCommand<RemotePacksCommand.Settings>
{
    public sealed class Settings : RemoteSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var client = settings.CreateClient();
        var resp = await client.GetAsync("packs");
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        if (!doc.TryGetProperty("items", out var items))
        {
            AnsiConsole.MarkupLine("[yellow]No packs registered[/]");
            return ExitCodes.Success;
        }

        var table = new Table().AddColumn("ID").AddColumn("Version").AddColumn("Title").AddColumn("Status");
        foreach (var p in items.EnumerateArray())
        {
            table.AddRow(
                p.TryGetProperty("id", out var pid) ? pid.GetString() ?? "" : "",
                p.TryGetProperty("currentVersion", out var ver) ? ver.GetString() ?? "" : "",
                p.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                p.TryGetProperty("status", out var st) ? st.GetString() ?? "" : ""
            );
        }
        AnsiConsole.Write(table);
        return ExitCodes.Success;
    }
}
