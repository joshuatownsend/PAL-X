using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public sealed class RemoteValidatePackCommand : AsyncCommand<RemoteValidatePackCommand.Settings>
{
    public sealed class Settings : RemoteSettings
    {
        [CommandArgument(0, "<pack-id>")]
        [Description("Pack ID registered on the server")]
        public required string PackId { get; init; }

        [CommandArgument(1, "<version>")]
        [Description("Pack version to validate (e.g. 1.0.0)")]
        public required string Version { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        => RemoteCommand.RunAsync(async () =>
        {
            using var client = RemoteHttpClient.Create(settings.ApiBase, settings.ApiKey);
            var resp = await client.GetAsync($"packs/{settings.PackId}/versions/{settings.Version}/validation");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AnsiConsole.MarkupLine($"[red]Pack not found:[/] {settings.PackId} v{settings.Version}");
                return ExitCodes.GeneralFailure;
            }
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
            bool isValid = doc.TryGetProperty("isValid", out var v) && v.GetBoolean();

            if (isValid)
            {
                AnsiConsole.MarkupLine($"[green]✔ Valid[/]  {settings.PackId} v{settings.Version}");

                if (doc.TryGetProperty("warnings", out var warns) && warns.GetArrayLength() > 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Warnings:[/]");
                    foreach (var w in warns.EnumerateArray())
                        AnsiConsole.MarkupLine($"  [yellow]·[/] {Markup.Escape(w.GetString() ?? "")}");
                }
                return ExitCodes.Success;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✘ Invalid[/]  {settings.PackId} v{settings.Version}");
                if (doc.TryGetProperty("errors", out var errs))
                    foreach (var e in errs.EnumerateArray())
                        AnsiConsole.MarkupLine($"  [red]·[/] {Markup.Escape(e.GetString() ?? "")}");
                return ExitCodes.PackValidationFailure;
            }
        });
}
