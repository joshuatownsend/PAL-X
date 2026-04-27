namespace Pal.Application.Diagnostics;

public interface IDiagnosticsService
{
    Task<IReadOnlyList<DiagnosticInsightDto>> ForJobAsync(Guid jobId, CancellationToken ct = default);
}
