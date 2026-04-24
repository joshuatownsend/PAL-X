using Spectre.Console.Cli;
using Pal.Cli.Commands;
using Pal.Cli.Commands.Remote;

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

    config.AddBranch("remote", remote =>
    {
        remote.SetDescription("Commands for interacting with a running PAL API server");

        remote.AddCommand<SubmitCommand>("submit")
            .WithDescription("Upload a file and queue an analysis job on the server");

        remote.AddCommand<JobStatusCommand>("status")
            .WithDescription("Poll the status of an analysis job");

        remote.AddCommand<JobResultsCommand>("results")
            .WithDescription("Show findings from a completed analysis job");

        remote.AddCommand<JobReportCommand>("report")
            .WithDescription("Download the HTML or JSON report for a completed job");

        remote.AddCommand<RemotePacksCommand>("packs")
            .WithDescription("List packs registered on the server");
    });
});

return app.Run(args);
