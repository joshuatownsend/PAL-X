using System.Text.Json;
using System.Threading.Channels;
using Pal.Application.Persistence;
using Pal.Application.Storage;
using Pal.Persistence;

namespace Pal.Api.Worker;

/// <summary>
/// Polls enabled <see cref="IngestionScheduleDto"/>s on a fixed tick. For each schedule whose
/// NextRunAt is due, scans its configured directory, ingests new files through the same
/// upload→job pipeline used by <c>POST /uploads</c> + <c>POST /analysis</c>, and writes the
/// new job IDs to the analysis <see cref="Channel{T}"/> for the <see cref="AnalysisWorker"/>
/// to pick up. Per-schedule failures are non-fatal.
/// </summary>
public sealed class ScheduledIngestionWorker : BackgroundService
{
    private readonly IIngestionScheduleRepository _schedules;
    private readonly IUploadRepository _uploads;
    private readonly IAnalysisRepository _analysis;
    private readonly IStorageProvider _storage;
    private readonly ITenantContext _tenant;
    private readonly Channel<Guid> _channel;
    private readonly ILogger<ScheduledIngestionWorker> _logger;
    private readonly TimeSpan _tickInterval;
    private readonly TimeSpan _fileStableAge;
    private readonly int _maxFilesPerTick;
    private readonly bool _enabled;

    public ScheduledIngestionWorker(
        IIngestionScheduleRepository schedules,
        IUploadRepository uploads,
        IAnalysisRepository analysis,
        IStorageProvider storage,
        ITenantContext tenant,
        Channel<Guid> channel,
        IConfiguration config,
        ILogger<ScheduledIngestionWorker> logger)
    {
        _schedules = schedules;
        _uploads = uploads;
        _analysis = analysis;
        _storage = storage;
        _tenant = tenant;
        _channel = channel;
        _logger = logger;
        _tickInterval = TimeSpan.FromSeconds(config.GetValue("Schedules:TickIntervalSeconds", 30));
        _fileStableAge = TimeSpan.FromSeconds(config.GetValue("Schedules:FileStableAgeSeconds", 30));
        _maxFilesPerTick = config.GetValue("Schedules:MaxFilesPerTick", 10);
        _enabled = config.GetValue("Schedules:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ScheduledIngestionWorker: disabled via Schedules:Enabled=false");
            return;
        }

        // Brief startup delay so migrations + pack sync finish before we touch the DB
        // or scan filesystems. Mirrors RetentionWorker's stabilization pause.
        await Task.Delay(TimeSpan.FromSeconds(10), ct);

        _logger.LogInformation(
            "ScheduledIngestionWorker started; tick={Tick}s, file_stable_age={Stable}s, max_files_per_tick={Max}",
            _tickInterval.TotalSeconds, _fileStableAge.TotalSeconds, _maxFilesPerTick);

        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ScheduledIngestionWorker tick failed");
            }
            await Task.Delay(_tickInterval, ct);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await _schedules.ListDueAsync(now, ct);
        if (due.Count == 0) return;

        foreach (var schedule in due)
        {
            try { await ProcessScheduleAsync(schedule, now, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Per-schedule failure must not stop the tick AND must not leave the
                // schedule's NextRunAt unadvanced (which would re-fire the failing
                // path every tick forever). Best-effort persistence of the next run
                // outside ProcessScheduleAsync covers the case where the failure
                // happened before its inner UpdateNextRunAsync call.
                _logger.LogWarning(ex,
                    "Schedule {ScheduleId} ({Name}) processing failed; advancing NextRunAt to avoid tight retry loop",
                    schedule.Id, schedule.Name);
                try { await UpdateNextRunAsync(schedule, now, ct); }
                catch (Exception persistEx) when (persistEx is not OperationCanceledException)
                {
                    _logger.LogWarning(persistEx,
                        "Schedule {ScheduleId} ({Name}): failed to persist NextRunAt after processing error",
                        schedule.Id, schedule.Name);
                }
            }
        }
    }

    private async Task ProcessScheduleAsync(IngestionScheduleDto schedule, DateTimeOffset now, CancellationToken ct)
    {
        using var _ = _tenant.SetWorkspace(schedule.WorkspaceId);

        var (path, glob) = ParseSourceConfig(schedule.SourceConfigJson);

        if (!Directory.Exists(path))
        {
            _logger.LogWarning(
                "Schedule {ScheduleId} ({Name}): directory '{Path}' does not exist; skipping tick",
                schedule.Id, schedule.Name, path);
            await UpdateNextRunAsync(schedule, now, ct);
            return;
        }

        // Cursor: only consider files modified since (LastRunAt - 2 min buffer). The
        // buffer covers clock skew between the API host and the file source. On the
        // first tick (LastRunAt is null) we consider everything — operators are expected
        // to either start with an empty directory or accept that backlog drains LIFO.
        var cursor = schedule.LastRunAt is { } last
            ? last - TimeSpan.FromMinutes(2)
            : DateTimeOffset.MinValue;
        var stableThreshold = now - _fileStableAge;

        // Newest-first ordering: prevents starvation of new arrivals when a directory
        // has more than _maxFilesPerTick stable files. Trade-off: files older than the
        // cap on first tick may not be processed; SHA-256 dedup still skips already-
        // ingested files on subsequent ticks if the cursor lets them through.
        var candidateFiles = Directory.EnumerateFiles(path, glob)
            .Select(f => new FileInfo(f))
            .Where(fi => fi.Exists
                         && fi.LastWriteTimeUtc <= stableThreshold
                         && fi.LastWriteTimeUtc > cursor)
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Take(_maxFilesPerTick)
            .ToList();

        var queued = 0;
        var deduped = 0;
        foreach (var fi in candidateFiles)
        {
            try
            {
                var (status, _) = await IngestFileAsync(fi, schedule, ct);
                if (status == IngestStatus.Queued) queued++;
                else if (status == IngestStatus.Deduped) deduped++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Schedule {ScheduleId} ({Name}): file '{File}' ingestion failed; continuing",
                    schedule.Id, schedule.Name, fi.FullName);
            }
        }

        _logger.LogInformation(
            "Schedule {ScheduleId} ({Name}): scanned {Scanned}, queued {Queued}, deduped {Deduped}",
            schedule.Id, schedule.Name, candidateFiles.Count, queued, deduped);

        await UpdateNextRunAsync(schedule, now, ct);
    }

    private async Task<(IngestStatus Status, Guid? JobId)> IngestFileAsync(FileInfo file, IngestionScheduleDto schedule, CancellationToken ct)
    {
        await using var fs = file.OpenRead();
        var (sha256, tempPath, sizeBytes) = await _storage.WriteToTempAsync(fs, ct);

        var existing = await _uploads.FindBySha256Async(sha256, ct);
        if (existing is not null)
        {
            _storage.DeleteTemp(tempPath);
            return (IngestStatus.Deduped, null);
        }

        var sourceType = file.Extension.TrimStart('.').ToLowerInvariant();
        var storagePath = await _storage.CommitUploadAsync(tempPath, sha256, file.Name, ct);
        var upload = await _uploads.CreateAsync(file.Name, sourceType, sizeBytes, sha256, storagePath, ct);

        var job = await _analysis.CreateJobAsync(upload.Id, schedule.PackIds, includeDataset: false, selectedBaselineId: null, ct);
        _channel.Writer.TryWrite(job.Id);

        return (IngestStatus.Queued, job.Id);
    }

    private Task UpdateNextRunAsync(IngestionScheduleDto schedule, DateTimeOffset now, CancellationToken ct)
        => _schedules.RecordRunAsync(schedule.Id, now, now.AddMinutes(schedule.IntervalMinutes), ct);

    private static (string Path, string Glob) ParseSourceConfig(string sourceConfigJson)
    {
        // Service-layer validation already guarantees this shape on the write path; defensive
        // parse here covers schedules that pre-date a future schema change. Throws on malformed
        // configs; caller catches and logs, then advances NextRunAt to the next tick.
        using var doc = JsonDocument.Parse(sourceConfigJson);
        var path = doc.RootElement.GetProperty("path").GetString()
            ?? throw new InvalidOperationException("source 'path' missing");
        var glob = doc.RootElement.GetProperty("glob").GetString()
            ?? throw new InvalidOperationException("source 'glob' missing");
        return (path, glob);
    }

    private enum IngestStatus { Queued, Deduped }
}
