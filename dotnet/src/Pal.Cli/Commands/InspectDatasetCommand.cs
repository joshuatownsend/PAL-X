using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Pal.Engine.Normalization;
using Pal.Ingestion.Csv;

namespace Pal.Cli.Commands;

public sealed class InspectDatasetSettings : CommandSettings
{
    [CommandOption("--input <path>")]
    [Description("Path to dataset")]
    public required string Input { get; init; }

    [CommandOption("--format <format>")]
    [Description("Input format: auto, blg, csv")]
    public string Format { get; init; } = "auto";

    [CommandOption("--output <path>")]
    [Description("Write JSON inspection metadata to this path")]
    public string? Output { get; init; }

    [CommandOption("--machine-name <name>")]
    public string? MachineName { get; init; }

    [CommandOption("--time-zone <tz>")]
    public string? TimeZone { get; init; }
}

public sealed class InspectDatasetCommand : Command<InspectDatasetSettings>
{
    public override int Execute(CommandContext context, InspectDatasetSettings settings)
    {
        string format = settings.Format == "auto"
            ? Path.GetExtension(settings.Input).TrimStart('.').ToLowerInvariant()
            : settings.Format;

        if (format == "blg")
        {
            Pal.Ingestion.Blg.BlgCollectorStub.ThrowNotSupported(settings.Input);
        }

        try
        {
            var registry = MetricAliasRegistry.BuildDefault();
            var collector = new CsvCollector(registry);
            var result = collector.Collect(settings.Input, settings.MachineName, settings.TimeZone);
            var ds = result.Dataset;

            AnsiConsole.MarkupLine("[bold]Dataset inspection[/]");
            AnsiConsole.MarkupLine($"Source type:     [cyan]{format}[/]");
            AnsiConsole.MarkupLine($"Machine name:    [cyan]{ds.MachineName ?? "(unknown)"}[/]");
            AnsiConsole.MarkupLine($"Time range:      [cyan]{ds.StartTimeUtc:yyyy-MM-ddTHH:mm:ssZ} to {ds.EndTimeUtc:yyyy-MM-ddTHH:mm:ssZ}[/]");
            AnsiConsole.MarkupLine($"Sample interval: [cyan]{ds.SampleIntervalSeconds:F0}s[/]");
            AnsiConsole.MarkupLine($"Series count:    [cyan]{ds.SeriesCount}[/]");
            AnsiConsole.MarkupLine($"Gap count:       [cyan]{ds.GapCount}[/]");

            AnsiConsole.MarkupLine("Top metrics:");
            foreach (var s in ds.Series.Take(10))
                AnsiConsole.MarkupLine($"  - [dim]{s.CanonicalMetric}[/]");

            if (settings.Output is not null)
            {
                var meta = new
                {
                    source_type = format,
                    source_path = settings.Input,
                    machine_name = ds.MachineName,
                    time_zone = ds.TimeZone,
                    start_time_utc = ds.StartTimeUtc,
                    end_time_utc = ds.EndTimeUtc,
                    sample_interval_seconds = ds.SampleIntervalSeconds,
                    series_count = ds.SeriesCount,
                    sample_count = ds.SampleCount,
                    gap_count = ds.GapCount,
                    warnings = result.Warnings
                };
                File.WriteAllText(settings.Output,
                    JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }),
                    new System.Text.UTF8Encoding(false));
                AnsiConsole.MarkupLine($"\nWrote: [cyan]{settings.Output}[/]");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {ex.Message}");
            return ExitCodes.InputCollectorFailure;
        }
    }
}
