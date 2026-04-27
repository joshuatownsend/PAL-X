using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Pal.Packs;
using Pal.Packs.Signing;

namespace Pal.Cli.Commands;

public sealed class ValidatePackSettings : CommandSettings
{
    [CommandOption("--path <path>")]
    [Description("Path to pack directory or pack.yaml file")]
    public required string Path { get; init; }

    [CommandOption("--strict")]
    [Description("Treat warnings as errors")]
    public bool Strict { get; init; }

    [CommandOption("--require-signature")]
    [Description("Fail if pack.yaml.sig is missing or signature verification fails")]
    public bool RequireSignature { get; init; }

    [CommandOption("--trust-key <path>")]
    [Description("Path to an additional trusted RSA public key PEM file for signature verification")]
    public string? TrustKeyPath { get; init; }

    [CommandOption("--json-output <path>")]
    [Description("Write validation results as JSON to this path")]
    public string? JsonOutput { get; init; }
}

public sealed class ValidatePackCommand : Command<ValidatePackSettings>
{
    public override int Execute(CommandContext context, ValidatePackSettings settings)
    {
        var loader = new PackLoader();
        var validator = new PackValidator();

        string packPath = settings.Path;

        try
        {
            var sigReq = settings.RequireSignature ? SignatureRequirement.Required : SignatureRequirement.Optional;
            var trustedKeys = TrustedKeys.DefaultTrusted(settings.TrustKeyPath);

            var pack = File.Exists(packPath) && packPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                ? loader.Load(packPath, sigReq, trustedKeys)
                : loader.LoadFromDirectory(packPath, sigReq, trustedKeys);

            var result = validator.Validate(pack);

            AnsiConsole.MarkupLine($"Pack:   [cyan]{pack.PackId}[/]");
            AnsiConsole.MarkupLine($"Schema: [cyan]pal.pack/v1[/]");
            AnsiConsole.MarkupLine($"Rules:  [cyan]{pack.Rules.Count}[/]");

            if (settings.JsonOutput is not null)
            {
                var jsonResult = new
                {
                    pack_id = pack.PackId,
                    version = pack.Version,
                    rule_count = pack.Rules.Count,
                    is_valid = result.IsValid,
                    errors = result.Errors,
                    warnings = result.Warnings
                };
                File.WriteAllText(settings.JsonOutput,
                    JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true }),
                    new System.Text.UTF8Encoding(false));
            }

            if (result.Errors.Count > 0)
            {
                AnsiConsole.MarkupLine($"Status: [red]INVALID[/]");
                foreach (var e in result.Errors)
                    AnsiConsole.MarkupLine($"  [red]✗[/] {e}");
                return ExitCodes.PackValidationFailure;
            }

            if (result.Warnings.Count > 0)
            {
                foreach (var w in result.Warnings)
                    AnsiConsole.MarkupLine($"  [yellow]⚠[/] {w}");

                if (settings.Strict)
                {
                    AnsiConsole.MarkupLine($"Status: [red]INVALID[/] (--strict: warnings are errors)");
                    return ExitCodes.PackValidationFailure;
                }
            }

            AnsiConsole.MarkupLine($"Status:   [green]valid[/]");
            AnsiConsole.MarkupLine($"Warnings: [yellow]{result.Warnings.Count}[/]");
            return ExitCodes.Success;
        }
        catch (PackSignatureException ex)
        {
            AnsiConsole.MarkupLine($"[red]SIGNATURE ERROR:[/] {ex.Message}");
            return ExitCodes.PackValidationFailure;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return ExitCodes.PackValidationFailure;
        }
    }
}
