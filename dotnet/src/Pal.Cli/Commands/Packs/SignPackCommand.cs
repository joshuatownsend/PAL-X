using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Pal.Packs.Signing;

namespace Pal.Cli.Commands.Packs;

public sealed class SignPackSettings : CommandSettings
{
    [CommandOption("--pack <path>")]
    [Description("Path to the pack directory containing pack.yaml")]
    public required string Pack { get; init; }

    [CommandOption("--key <path>")]
    [Description("Path to the RSA private key PEM file (PKCS#8 or traditional format)")]
    public required string Key { get; init; }
}

public sealed class SignPackCommand : Command<SignPackSettings>
{
    public override int Execute(CommandContext context, SignPackSettings settings)
    {
        try
        {
            var signer = new PackSigner();
            signer.Sign(settings.Pack, settings.Key);
            AnsiConsole.MarkupLine($"[green]Signed:[/] {Path.Combine(settings.Pack, "pack.yaml.sig")} written");
            return ExitCodes.Success;
        }
        catch (PackSignatureException ex)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return ExitCodes.PackValidationFailure;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return ExitCodes.GeneralFailure;
        }
    }
}
