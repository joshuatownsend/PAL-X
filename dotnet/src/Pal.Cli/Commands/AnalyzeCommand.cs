using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Pal.Application.Analysis;
using Pal.Engine.Rules;
using Pal.Reporting.Json;

namespace Pal.Cli.Commands;

public sealed class AnalyzeSettings : CommandSettings
{
    [CommandOption("--input <path>")]
    [Description("Path to input dataset (CSV or BLG)")]
    public required string Input { get; init; }

    [CommandOption("--output <dir>")]
    [Description("Output directory for report artifacts")]
    public required string Output { get; init; }

    [CommandOption("--format <format>")]
    [Description("Input format: auto, blg, csv (default: auto)")]
    public string Format { get; init; } = "auto";

    [CommandOption("--pack <pack-id>")]
    [Description("Pack ID to load (repeatable)")]
    public string[] Packs { get; init; } = [];

    [CommandOption("--pack-dir <path>")]
    [Description("Additional search path for packs (repeatable)")]
    public string[] PackDirs { get; init; } = [];

    [CommandOption("--auto-resolve-packs")]
    [Description("Auto-load applicable packs based on dataset content")]
    public bool AutoResolvePacks { get; init; }

    [CommandOption("--html")]
    [Description("Emit HTML report (default: true)")]
    public bool Html { get; init; } = true;

    [CommandOption("--json")]
    [Description("Emit JSON report (default: true)")]
    public bool Json { get; init; } = true;

    [CommandOption("--html-only")]
    [Description("Emit HTML only (mutually exclusive with --json-only)")]
    public bool HtmlOnly { get; init; }

    [CommandOption("--json-only")]
    [Description("Emit JSON only (mutually exclusive with --html-only)")]
    public bool JsonOnly { get; init; }

    [CommandOption("--fail-on-warning")]
    [Description("Exit code 1 if any warning finding is produced")]
    public bool FailOnWarning { get; init; }

    [CommandOption("--machine-name <name>")]
    [Description("Override machine name from source metadata")]
    public string? MachineName { get; init; }

    [CommandOption("--time-zone <tz>")]
    [Description("Override or assign source time zone")]
    public string? TimeZone { get; init; }

    [CommandOption("--report-name <name>")]
    [Description("Base name for generated artifact files")]
    public string? ReportName { get; init; }

    [CommandOption("--include-charts")]
    [Description("Emit chart SVG artifacts")]
    public bool IncludeCharts { get; init; }

    [CommandOption("--chart-limit <n>")]
    [Description("Maximum charts to generate (default: 20)")]
    public int ChartLimit { get; init; } = 20;

    [CommandOption("--host-memory-mb <mb>")]
    [Description("Total physical memory in MB (for RAM-relative thresholds)")]
    public double? HostMemoryMb { get; init; }

    [CommandOption("--host-cpu-count <n>")]
    [Description("Logical processor count (for CPU-relative thresholds)")]
    public int? HostCpuCount { get; init; }

    [CommandOption("--now <iso>")]
    [Description("Override generation timestamp (for deterministic test output)")]
    public string? NowOverride { get; init; }

    [CommandOption("--verbose")]
    [Description("Verbose output")]
    public bool Verbose { get; init; }
}

public sealed class AnalyzeCommand : Command<AnalyzeSettings>
{
    public override ValidationResult Validate(CommandContext context, AnalyzeSettings settings)
    {
        if (settings.HtmlOnly && settings.JsonOnly)
            return ValidationResult.Error("--html-only and --json-only are mutually exclusive");

        if (string.IsNullOrWhiteSpace(settings.Input))
            return ValidationResult.Error("--input is required");

        if (!File.Exists(settings.Input))
            return ValidationResult.Error($"Input file not found: {settings.Input}");

        if (string.IsNullOrWhiteSpace(settings.Output))
            return ValidationResult.Error("--output is required");

        return ValidationResult.Success();
    }

    public override int Execute(CommandContext context, AnalyzeSettings settings)
    {
        var sw = Stopwatch.StartNew();
        AnsiConsole.MarkupLine("[bold]PAL 2026.1.0[/]");

        DateTimeOffset generatedAt = settings.NowOverride is not null
            ? DateTimeOffset.Parse(settings.NowOverride, System.Globalization.CultureInfo.InvariantCulture)
            : DateTimeOffset.UtcNow;

        string format = settings.Format == "auto"
            ? Path.GetExtension(settings.Input).TrimStart('.').ToLowerInvariant()
            : settings.Format;

        AnsiConsole.MarkupLine($"Input:     [cyan]{settings.Input}[/]");
        AnsiConsole.MarkupLine($"Collector: [cyan]{format.ToUpperInvariant()}[/]");
        AnsiConsole.MarkupLine($"Output:    [cyan]{settings.Output}[/]");
        AnsiConsole.Markup("Analyzing...");

        AnalysisRunResult result;
        try
        {
            result = new AnalysisRunner().Run(new AnalysisRunRequest
            {
                InputPath = settings.Input,
                InputFormat = format,
                PackIds = settings.Packs,
                PackDirs = settings.PackDirs,
                AutoResolvePacks = settings.AutoResolvePacks,
                HostMemoryMb = settings.HostMemoryMb,
                HostCpuCount = settings.HostCpuCount,
                MachineName = settings.MachineName,
                TimeZone = settings.TimeZone,
                HostContextSidecarPath = Path.Combine(
                    Path.GetDirectoryName(settings.Input) ?? ".", "host-context.json")
            });
        }
        catch (NotSupportedException ex)
        {
            AnsiConsole.MarkupLine($"\n[red]ERROR:[/] {ex.Message}");
            return ExitCodes.InputCollectorFailure;
        }
        catch (PackResolutionException ex)
        {
            foreach (var e in ex.Errors)
                AnsiConsole.MarkupLine($"\n[red]ERROR: Pack validation failed:[/] {e}");
            return ExitCodes.PackValidationFailure;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]ERROR:[/] Analysis failed: {ex.Message}");
            return ExitCodes.AnalysisExecutionFailure;
        }

        var findings = result.Findings;
        int criticals = 0, warnings = 0, infos = 0;
        foreach (var f in findings)
            if (f.Severity == "critical") criticals++;
            else if (f.Severity == "warning") warnings++;
            else infos++;

        var packNames = string.Join(", ", result.PackResolutions.Select(p => p.PackId));
        AnsiConsole.MarkupLine($" [green]done[/] — packs: {packNames}");
        AnsiConsole.MarkupLine($"Findings:  [bold]{findings.Count}[/] ({criticals} critical, {warnings} warning, {infos} informational)");

        Directory.CreateDirectory(settings.Output);
        string stem = settings.ReportName ?? Path.GetFileNameWithoutExtension(settings.Input);
        string jsonPath = Path.Combine(settings.Output, $"{stem}.pal-report.json");
        string htmlPath = Path.Combine(settings.Output, $"{stem}.pal-report.html");

        bool emitJson = !settings.HtmlOnly;
        bool emitHtml = !settings.JsonOnly;

        sw.Stop();
        var writeInput = new JsonReportWriter.WriteInput
        {
            Dataset = result.Dataset,
            Findings = findings,
            PackResolutions = result.PackResolutions,
            EngineWarnings = result.EngineWarnings,
            CollectorWarnings = result.CollectorWarnings,
            InputPath = settings.Input,
            OutputPath = jsonPath,
            HtmlReportPath = emitHtml ? htmlPath : null,
            DurationMs = settings.NowOverride is not null ? 0L : sw.ElapsedMilliseconds,
            GeneratedAt = generatedAt
        };

        try
        {
            if (emitJson)
            {
                new JsonReportWriter().Write(writeInput);
                AnsiConsole.MarkupLine($"\nWrote: [cyan]{jsonPath}[/]");
            }

            if (emitHtml)
            {
                Pal.Reporting.Html.HtmlReportWriter.Write(writeInput, htmlPath);
                AnsiConsole.MarkupLine($"       [cyan]{htmlPath}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]ERROR:[/] Report generation failed: {ex.Message}");
            return ExitCodes.ReportGenerationFailure;
        }

        AnsiConsole.MarkupLine($"\nCompleted in [bold]{sw.ElapsedMilliseconds / 1000.0:F1}s[/]");

        if (settings.FailOnWarning && (warnings > 0 || criticals > 0))
            return ExitCodes.GeneralFailure;

        return ExitCodes.Success;
    }
}
