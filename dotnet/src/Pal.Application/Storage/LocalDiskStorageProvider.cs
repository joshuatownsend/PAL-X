using System.Security.Cryptography;

namespace Pal.Application.Storage;

public sealed class LocalDiskStorageProvider : IStorageProvider
{
    private readonly string _root;

    public LocalDiskStorageProvider(string root)
    {
        _root = root;
        Directory.CreateDirectory(Path.Combine(root, "uploads"));
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        Directory.CreateDirectory(Path.Combine(root, "temp"));
    }

    public async Task<(string sha256, string tempPath, long sizeBytes)> WriteToTempAsync(Stream source, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(_root, "temp", Guid.NewGuid().ToString("N"));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long sizeBytes = 0;
        var buffer = new byte[81920];

        await using var dest = File.Create(tempPath);
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read), ct);
            hash.AppendData(buffer, 0, read);
            sizeBytes += read;
        }

        var sha256 = Convert.ToHexString(hash.GetCurrentHash()).ToLowerInvariant();
        return (sha256, tempPath, sizeBytes);
    }

    public Task<string> CommitUploadAsync(string tempPath, string sha256, string fileName, CancellationToken ct = default)
    {
        var dir = Path.Combine(_root, "uploads", sha256);
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, Path.GetFileName(fileName));
        File.Move(tempPath, dest, overwrite: true);
        var relative = Path.GetRelativePath(_root, dest);
        return Task.FromResult(relative);
    }

    public string? FindExistingUpload(string sha256, string fileName)
    {
        var candidate = Path.Combine(_root, "uploads", sha256, Path.GetFileName(fileName));
        if (!File.Exists(candidate)) return null;
        return Path.GetRelativePath(_root, candidate);
    }

    public string GetAbsolutePath(string relativePath) => Path.Combine(_root, relativePath);

    public async Task<string> WriteReportAsync(Guid jobId, string format, byte[] content, CancellationToken ct = default)
    {
        string ext = format == "html" ? "html" : "json";
        var dir = Path.Combine(_root, "reports", jobId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"report.pal-report.{ext}");
        await File.WriteAllBytesAsync(path, content, ct);
        return Path.GetRelativePath(_root, path);
    }

    public Stream OpenReport(string relativePath) => File.OpenRead(Path.Combine(_root, relativePath));

    public void DeleteTemp(string tempPath)
    {
        if (File.Exists(tempPath)) File.Delete(tempPath);
    }
}
