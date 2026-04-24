namespace Pal.Application.Persistence;

public interface IUploadRepository
{
    Task<UploadDto?> FindBySha256Async(string sha256, CancellationToken ct = default);
    Task<UploadDto> CreateAsync(string fileName, string sourceType, long sizeBytes, string sha256, string storagePath, CancellationToken ct = default);
    Task<UploadDto?> GetAsync(Guid id, CancellationToken ct = default);
}
