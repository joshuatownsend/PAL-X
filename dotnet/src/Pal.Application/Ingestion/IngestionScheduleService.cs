using System.Text.Json;
using Pal.Application.Persistence;

namespace Pal.Application.Ingestion;

public sealed class IngestionScheduleService : IIngestionScheduleService
{
    private readonly IIngestionScheduleRepository _repo;

    public IngestionScheduleService(IIngestionScheduleRepository repo) => _repo = repo;

    public Task<IReadOnlyList<IngestionScheduleDto>> ListAsync(CancellationToken ct = default)
        => _repo.ListAsync(ct);

    public Task<IngestionScheduleDto?> GetAsync(Guid id, CancellationToken ct = default)
        => _repo.GetAsync(id, ct);

    public async Task<IngestionScheduleDto> CreateAsync(
        string name,
        int intervalMinutes,
        string sourceConfigJson,
        IReadOnlyList<string> packIds,
        bool enabled,
        CancellationToken ct = default)
    {
        Validate(name, intervalMinutes, sourceConfigJson, packIds);

        var now = DateTimeOffset.UtcNow;
        var schedule = new IngestionScheduleDto
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.Empty, // repository fills this from ITenantContext
            Name = name.Trim(),
            IntervalMinutes = intervalMinutes,
            SourceConfigJson = sourceConfigJson,
            PackIds = packIds,
            Enabled = enabled,
            LastRunAt = null,
            NextRunAt = enabled ? now : null,  // run on next tick if created enabled
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _repo.CreateAsync(schedule, ct);
        return schedule;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string name,
        int intervalMinutes,
        string sourceConfigJson,
        IReadOnlyList<string> packIds,
        bool enabled,
        CancellationToken ct = default)
    {
        Validate(name, intervalMinutes, sourceConfigJson, packIds);

        var existing = await _repo.GetAsync(id, ct);
        if (existing is null) return false;

        return await _repo.UpdateAsync(new IngestionScheduleDto
        {
            Id = id,
            WorkspaceId = existing.WorkspaceId,
            Name = name.Trim(),
            IntervalMinutes = intervalMinutes,
            SourceConfigJson = sourceConfigJson,
            PackIds = packIds,
            Enabled = enabled,
            LastRunAt = existing.LastRunAt,
            NextRunAt = existing.NextRunAt,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        }, ct);
    }

    public Task<bool> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default)
        => _repo.SetEnabledAsync(id, enabled, DateTimeOffset.UtcNow, ct);

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => _repo.DeleteAsync(id, ct);

    /// <summary>
    /// Reject schedule input that would either fail at runtime or constitute an obvious
    /// foot-gun (DoS interval, malformed source config, empty pack list).
    /// Throws <see cref="IngestionScheduleValidationException"/>; endpoints translate to 400.
    /// </summary>
    private static void Validate(string name, int intervalMinutes, string sourceConfigJson, IReadOnlyList<string> packIds)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new IngestionScheduleValidationException("name must be 1–100 non-whitespace characters");

        if (intervalMinutes is < 5 or > 1440)
            throw new IngestionScheduleValidationException("intervalMinutes must be between 5 and 1440 (1 day)");

        if (packIds.Count == 0 ||
            packIds.Any(p => string.IsNullOrWhiteSpace(p)) ||
            packIds.Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != packIds.Count)
            throw new IngestionScheduleValidationException("packIds must contain at least one non-empty, non-duplicate pack ID");

        ValidateSourceConfig(sourceConfigJson);
    }

    private static void ValidateSourceConfig(string sourceConfigJson)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(sourceConfigJson); }
        catch (JsonException ex) { throw new IngestionScheduleValidationException($"sourceConfigJson is not valid JSON: {ex.Message}"); }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new IngestionScheduleValidationException("sourceConfigJson must be a JSON object");

            var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type != "directory")
                throw new IngestionScheduleValidationException($"unsupported source type '{type ?? "(missing)"}' — v1 supports only 'directory'");

            var path = doc.RootElement.TryGetProperty("path", out var p) ? p.GetString() : null;
            if (string.IsNullOrWhiteSpace(path))
                throw new IngestionScheduleValidationException("source 'path' is required and must be non-empty");
            if (!Path.IsPathRooted(path) || path.Contains(".."))
                throw new IngestionScheduleValidationException("source 'path' must be absolute and free of '..' segments");

            var glob = doc.RootElement.TryGetProperty("glob", out var g) ? g.GetString() : null;
            if (string.IsNullOrWhiteSpace(glob))
                throw new IngestionScheduleValidationException("source 'glob' is required and must be non-empty");
        }
    }
}
