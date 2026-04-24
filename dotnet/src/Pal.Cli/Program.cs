using Spectre.Console.Cli;
using Pal.Cli.Commands;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("pal");
    config.SetApplicationVersion("2026.1.0");

    config.AddCommand<AnalyzeCommand>("analyze")
        .WithDescription("Analyze one input dataset and generate report artifacts");

    config.AddCommand<ValidatePackCommand>("validate-pack")
        .WithDescription("Validate one pack or a directory of packs");

    config.AddCommand<InspectDatasetCommand>("inspect-dataset")
        .WithDescription("Import and inspect a dataset without running rules");

    config.AddCommand<ListPacksCommand>("list-packs")
        .WithDescription("List all packs available on the search path");
});

return app.Run(args);
