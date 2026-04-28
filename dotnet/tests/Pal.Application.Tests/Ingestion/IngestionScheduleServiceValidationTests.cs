using System.Text.Json;
using Pal.Application.Ingestion;
using Pal.Application.Persistence;
using Xunit;

namespace Pal.Application.Tests.Ingestion;

public class IngestionScheduleServiceValidationTests
{
    private static readonly string ValidSourceConfig = JsonSerializer.Serialize(new
    {
        type = "directory",
        path = OperatingSystem.IsWindows() ? @"C:\PerfLogs\WEB-01" : "/var/perflogs/web-01",
        glob = "*.csv"
    });

    private static IngestionScheduleService NewService() =>
        new(new FakeIngestionScheduleRepository());

    private static Task CreateAsync(string? name = null, int interval = 15,
        string? sourceConfig = null, IReadOnlyList<string>? packIds = null) =>
        NewService().CreateAsync(
            name: name ?? "nightly-web",
            intervalMinutes: interval,
            sourceConfigJson: sourceConfig ?? ValidSourceConfig,
            packIds: packIds ?? new[] { "windows-core" },
            enabled: true);

    [Fact]
    public async Task ValidInput_Succeeds()
    {
        // No exception = pass
        await CreateAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Name_RejectsEmptyOrWhitespace(string name)
    {
        var ex = await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(name: name));
        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public async Task Name_RejectsOver100Chars()
    {
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(name: new string('a', 101)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(1441)]
    [InlineData(int.MaxValue)]
    public async Task Interval_RejectsOutOfBand(int minutes)
    {
        var ex = await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(interval: minutes));
        Assert.Contains("intervalMinutes", ex.Message);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(1440)]
    public async Task Interval_AcceptsBoundaries(int minutes)
    {
        await CreateAsync(interval: minutes);
    }

    [Fact]
    public async Task PackIds_RejectsEmpty()
    {
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(packIds: Array.Empty<string>()));
    }

    [Fact]
    public async Task PackIds_RejectsBlankEntries()
    {
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(packIds: new[] { "windows-core", "" }));
    }

    [Fact]
    public async Task PackIds_RejectsCaseInsensitiveDuplicates()
    {
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(packIds: new[] { "windows-core", "Windows-Core" }));
    }

    [Fact]
    public async Task SourceConfig_RejectsMalformedJson()
    {
        var ex = await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(sourceConfig: "{not json"));
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public async Task SourceConfig_RejectsNonObject()
    {
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(sourceConfig: "[]"));
    }

    [Fact]
    public async Task SourceConfig_RejectsUnsupportedType()
    {
        var bad = JsonSerializer.Serialize(new { type = "url", url = "https://example.com/data.csv" });
        var ex = await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(sourceConfig: bad));
        Assert.Contains("unsupported source type", ex.Message);
    }

    [Fact]
    public async Task SourceConfig_RejectsRelativePath()
    {
        var bad = JsonSerializer.Serialize(new { type = "directory", path = "perflogs/web-01", glob = "*.csv" });
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(sourceConfig: bad));
    }

    [Fact]
    public async Task SourceConfig_RejectsTraversalSegments()
    {
        var path = OperatingSystem.IsWindows() ? @"C:\foo\..\bar" : "/foo/../bar";
        var bad = JsonSerializer.Serialize(new { type = "directory", path, glob = "*.csv" });
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(sourceConfig: bad));
    }

    [Fact]
    public async Task SourceConfig_RejectsMissingGlob()
    {
        var path = OperatingSystem.IsWindows() ? @"C:\PerfLogs" : "/var/perflogs";
        var bad = JsonSerializer.Serialize(new { type = "directory", path });
        await Assert.ThrowsAsync<IngestionScheduleValidationException>(() => CreateAsync(sourceConfig: bad));
    }

    // ── fake repo (in-memory; only Create needs to be reachable for validation tests) ──

    private sealed class FakeIngestionScheduleRepository : IIngestionScheduleRepository
    {
        public Task<IReadOnlyList<IngestionScheduleDto>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IngestionScheduleDto>>(Array.Empty<IngestionScheduleDto>());
        public Task<IngestionScheduleDto?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IngestionScheduleDto?>(null);
        public Task CreateAsync(IngestionScheduleDto schedule, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> UpdateAsync(IngestionScheduleDto schedule, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> SetEnabledAsync(Guid id, bool enabled, DateTimeOffset updatedAt, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<IngestionScheduleDto>> ListDueAsync(DateTimeOffset now, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IngestionScheduleDto>>(Array.Empty<IngestionScheduleDto>());
        public Task RecordRunAsync(Guid id, DateTimeOffset lastRunAt, DateTimeOffset nextRunAt, CancellationToken ct = default) => Task.CompletedTask;
    }
}
