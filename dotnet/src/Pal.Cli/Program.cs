using Spectre.Console.Cli;
using Pal.Cli.Commands;
using Pal.Cli.Commands.Packs;
using Pal.Cli.Commands.Remote;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("pal");
    config.SetApplicationVersion("2026.2.0");

    config.AddCommand<AnalyzeCommand>("analyze")
        .WithDescription("Analyze one input dataset and generate report artifacts");

    config.AddCommand<ValidatePackCommand>("validate-pack")
        .WithDescription("Validate one pack or a directory of packs");

    config.AddCommand<InspectDatasetCommand>("inspect-dataset")
        .WithDescription("Import and inspect a dataset without running rules");

    config.AddCommand<ListPacksCommand>("list-packs")
        .WithDescription("List all packs available on the search path");

    config.AddBranch("packs", packs =>
    {
        packs.SetDescription("Commands for working with PAL packs");
        packs.AddCommand<SignPackCommand>("sign")
            .WithDescription("Sign a pack directory, producing pack.yaml.sig");
    });

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

        remote.AddCommand<RemoteValidatePackCommand>("validate-pack")
            .WithDescription("Validate a stored pack version on the server");

        remote.AddCommand<RemoteDatasetCommand>("dataset")
            .WithDescription("Download the normalized dataset artifact for a completed job");

        remote.AddCommand<CompareCommand>("compare")
            .WithDescription("Compare two completed analysis jobs and show a finding diff");

        remote.AddCommand<RemoteDiagnosticsCommand>("diagnostics")
            .WithDescription("Show guided diagnostics insights for a completed job");

        remote.AddBranch("baselines", b =>
        {
            b.SetDescription("Commands for managing baseline designations");
            b.AddCommand<RemoteBaselinesListCommand>("list")
                .WithDescription("List designated baselines, optionally filtered by type");
            b.AddCommand<RemoteBaselineSetCommand>("set")
                .WithDescription("Designate or clear a baseline for a completed job");
        });

        remote.AddCommand<TrendsCommand>("trends")
            .WithDescription("Show finding trends across the last N completed analysis jobs");

        remote.AddCommand<CorrelationsCommand>("correlations")
            .WithDescription("Show co-occurring finding pairs across the last N completed analysis jobs");

        remote.AddBranch("alerts", a =>
        {
            a.SetDescription("Commands for managing Phase 4 alerts");
            a.AddCommand<RemoteAlertsListCommand>("list")
                .WithDescription("List alerts, optionally filtered by status or severity");
            a.AddCommand<RemoteAlertAcknowledgeCommand>("acknowledge")
                .WithDescription("Mark an alert as acknowledged (open → acknowledged)");
            a.AddCommand<RemoteAlertResolveCommand>("resolve")
                .WithDescription("Resolve an alert with an optional resolution note");
            a.AddCommand<RemoteAlertSnoozeCommand>("snooze")
                .WithDescription("Suppress notifications for an alert until a specified time");
            a.AddCommand<RemoteAlertUnsnoozeCommand>("unsnooze")
                .WithDescription("Clear an active snooze on an alert");
        });

        remote.AddBranch("schedules", s =>
        {
            s.SetDescription("Commands for managing Phase 4 ingestion schedules");
            s.AddCommand<RemoteSchedulesListCommand>("list")
                .WithDescription("List ingestion schedules in the current workspace");
            s.AddCommand<RemoteScheduleCreateCommand>("create")
                .WithDescription("Create a new directory-poll ingestion schedule");
            s.AddCommand<RemoteScheduleEnableCommand>("enable")
                .WithDescription("Enable a schedule");
            s.AddCommand<RemoteScheduleDisableCommand>("disable")
                .WithDescription("Disable a schedule (worker stops polling it)");
            s.AddCommand<RemoteScheduleDeleteCommand>("delete")
                .WithDescription("Delete a schedule permanently");
        });
    });
});

return app.Run(args);
