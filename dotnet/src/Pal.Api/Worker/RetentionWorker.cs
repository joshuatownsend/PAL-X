using Pal.Application.Persistence;
using Pal.Application.Storage;

namespace Pal.Api.Worker;

public sealed class RetentionWorker : BackgroundService
{
    private readonly IRetentionRepository _repo;
    private readonly IStorageProvider _storage;
    private readonly int _jobRetentionDays;
    private readonly int _auditRetentionDays;
    private readonly ILogger<RetentionWorker> _logger;

    public RetentionWorker(
        IRetentionRepository repo,
        IStorageProvider storage,
        IConfiguration config,
        ILogger<RetentionWorker> logger)
    {
        _repo = repo;
        _storage = storage;
        _jobRetentionDays = config.GetValue<int>("Retention:JobRetentionDays", 0);
        _auditRetentionDays = config.GetValue<int>("Retention:AuditEventRetentionDays", 0);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (_jobRetentionDays == 0 && _auditRetentionDays == 0)
        {
            _logger.LogInformation("RetentionWorker: retention disabled (both settings are 0); exiting");
            return;
        }

        // Brief startup delay so migrations and pack sync finish before we touch the DB.
        await Task.Delay(TimeSpan.FromMinutes(5), ct);

        while (!ct.IsCancellationRequested)
        {
            await RunOnceAsync(ct);
            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "RetentionWorker: starting run (job_retention_days={J}, audit_retention_days={A})",
            _jobRetentionDays, _auditRetentionDays);

        try
        {
            if (_jobRetentionDays > 0)
            {
                var result = await _repo.PurgeJobsAsync(_jobRetentionDays, ct);

                if (result.JobsDeleted > 0 || result.DeletedUploadSha256s.Count > 0)
                {
                    // Delete storage after the DB commit so we never lose DB records for files
                    // that are still on disk. Orphaned files are cheaper than orphaned DB rows.
                    foreach (var jobId in result.DeletedJobIds)
                        TryDeleteJobStorage(jobId);

                    foreach (var sha256 in result.DeletedUploadSha256s)
                        TryDeleteUploadStorage(sha256);
                }

                _logger.LogInformation(
                    "RetentionWorker: purged {Jobs} job(s), {Compare} compare result(s), {Uploads} upload(s)",
                    result.JobsDeleted, result.CompareResultsDeleted, result.DeletedUploadSha256s.Count);
            }

            if (_auditRetentionDays > 0)
            {
                var auditDeleted = await _repo.PurgeAuditEventsAsync(_auditRetentionDays, ct);
                if (auditDeleted > 0)
                    _logger.LogInformation("RetentionWorker: purged {Count} audit event(s)", auditDeleted);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "RetentionWorker: run failed");
        }
    }

    private void TryDeleteJobStorage(Guid jobId)
    {
        try { _storage.DeleteJobReportDirectory(jobId); }
        catch (Exception ex) { _logger.LogWarning(ex, "RetentionWorker: failed to delete report dir for job {JobId}", jobId); }
    }

    private void TryDeleteUploadStorage(string sha256)
    {
        try { _storage.DeleteUploadDirectory(sha256); }
        catch (Exception ex) { _logger.LogWarning(ex, "RetentionWorker: failed to delete upload dir for sha256 {Sha256}", sha256); }
    }
}
