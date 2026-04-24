using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Pal.Application.Analysis;
using Pal.Application.Persistence;
using Pal.Application.Storage;
using Pal.Engine.Scoring;
using Pal.Reporting.Html;
using Pal.Reporting.Json;

namespace Pal.Api.Worker;

public sealed class AnalysisWorker : BackgroundService
{
    private static readonly JsonSerializerOptions FindingsJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly Channel<Guid> _channel;
    private readonly IAnalysisRepository _analysisRepo;
    private readonly IUploadRepository _uploadRepo;
    private readonly IStorageProvider _storage;
    private readonly IAnalysisRunner _runner;
    private readonly string _packsDirectory;
    private readonly ILogger<AnalysisWorker> _logger;

    public AnalysisWorker(
        Channel<Guid> channel,
        IAnalysisRepository analysisRepo,
        IUploadRepository uploadRepo,
        IStorageProvider storage,
        IAnalysisRunner runner,
        IConfiguration config,
        ILogger<AnalysisWorker> logger)
    {
        _channel = channel;
        _analysisRepo = analysisRepo;
        _uploadRepo = uploadRepo;
        _storage = storage;
        _runner = runner;
        _packsDirectory = config["Packs:Directory"] ?? "packs/thresholds";
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        await _analysisRepo.ResetOrphanedJobsAsync(ct);
        var queued = await _analysisRepo.GetQueuedJobIdsAsync(ct);
        foreach (var id in queued)
            _channel.Writer.TryWrite(id);

        _logger.LogInformation("AnalysisWorker started; re-queued {Count} pending job(s)", queued.Count);
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var jobId in _channel.Reader.ReadAllAsync(ct))
        {
            try { await ProcessJobAsync(jobId, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error processing job {JobId}", jobId);
            }
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        if (!await _analysisRepo.TryClaimJobAsync(jobId, ct))
        {
            _logger.LogDebug("Job {JobId} already claimed or completed, skipping", jobId);
            return;
        }

        _logger.LogInformation("Processing job {JobId}", jobId);

        try
        {
            var job = await _analysisRepo.GetJobAsync(jobId, ct)
                ?? throw new InvalidOperationException($"Job {jobId} not found");
            var upload = await _uploadRepo.GetAsync(job.UploadId, ct)
                ?? throw new InvalidOperationException($"Upload {job.UploadId} not found");

            var requestedPacks = ParseRequestedPacks(job);
            var inputPath = _storage.GetAbsolutePath(upload.StoragePath);

            var format = Path.GetExtension(upload.FileName).TrimStart('.').ToLowerInvariant();

            var runResult = _runner.Run(new AnalysisRunRequest
            {
                InputPath = inputPath,
                InputFormat = format,
                PackIds = requestedPacks,
                PackDirs = [_packsDirectory],
                AutoResolvePacks = false,
                HostContextSidecarPath = null
            });

            var packVersions = runResult.PackResolutions.Select(r => new JobPackDto
            {
                PackId = r.PackId,
                PackVersion = r.Version
            }).ToList();
            await _analysisRepo.SetJobPackVersionsAsync(jobId, packVersions, ct);

            var summaryJson = BuildSummaryJson(runResult);
            var findingsJson = JsonSerializer.Serialize(runResult.Findings, FindingsJsonOptions);
            await _analysisRepo.SaveResultAsync(jobId, summaryJson, findingsJson, ct);

            await GenerateAndStoreReportsAsync(jobId, runResult, upload, ct);

            await _analysisRepo.MarkCompletedAsync(jobId, ct);
            _logger.LogInformation("Job {JobId} completed: {Count} finding(s)", jobId, runResult.Findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
            await _analysisRepo.MarkFailedAsync(jobId, ex.Message, ct);
        }
    }

    private IReadOnlyList<string> ParseRequestedPacks(AnalysisJobDto job)
    {
        if (job.OptionsJson is null) return [];
        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(job.OptionsJson);
            if (doc.TryGetProperty("requestedPacks", out var arr))
                return arr.EnumerateArray().Select(e => e.GetString()!).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse OptionsJson for job {JobId}", job.Id);
        }
        return [];
    }

    private static string BuildSummaryJson(AnalysisRunResult result)
    {
        var c = result.Findings.Count(f => f.Severity == "critical");
        var w = result.Findings.Count(f => f.Severity == "warning");
        var i = result.Findings.Count(f => f.Severity == "informational");
        var status = StatusClassifier.ClassifyOverall(result.Findings).ToString().ToLowerInvariant();
        return JsonSerializer.Serialize(new
        {
            finding_counts = new { critical = c, warning = w, informational = i },
            highest_severity = status
        });
    }

    private async Task GenerateAndStoreReportsAsync(
        Guid jobId, AnalysisRunResult result, UploadDto upload, CancellationToken ct)
    {
        var stem = Path.GetFileNameWithoutExtension(upload.FileName);
        var dummyJsonPath = $"{stem}.pal-report.json";
        var dummyHtmlPath = $"{stem}.pal-report.html";

        var writeInput = new JsonReportWriter.WriteInput
        {
            Dataset = result.Dataset,
            Findings = result.Findings,
            PackResolutions = result.PackResolutions,
            EngineWarnings = result.EngineWarnings,
            CollectorWarnings = result.CollectorWarnings,
            InputPath = upload.FileName,
            OutputPath = dummyJsonPath,
            HtmlReportPath = dummyHtmlPath,
            DurationMs = 0,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        using var jsonMs = new MemoryStream();
        new JsonReportWriter().WriteToStream(writeInput, jsonMs);
        var jsonBytes = jsonMs.ToArray();
        var jsonPath = await _storage.WriteReportAsync(jobId, "json", jsonBytes, ct);
        await _analysisRepo.SaveReportAsync(jobId, "json", jsonPath, jsonBytes.Length, ct);

        using var htmlMs = new MemoryStream();
        HtmlReportWriter.WriteToStream(writeInput, htmlMs);
        var htmlBytes = htmlMs.ToArray();
        var htmlPath = await _storage.WriteReportAsync(jobId, "html", htmlBytes, ct);
        await _analysisRepo.SaveReportAsync(jobId, "html", htmlPath, htmlBytes.Length, ct);
    }
}
