namespace Pal.Application.Storage;

public interface IStorageProvider
{
    /// <summary>
    /// Tees the stream into a temp file while computing SHA-256. Returns the hash and temp file path.
    /// </summary>
    Task<(string sha256, string tempPath, long sizeBytes)> WriteToTempAsync(Stream source, CancellationToken ct = default);

    /// <summary>
    /// Moves the temp file into permanent upload storage under uploads/{sha256}/{fileName}.
    /// Returns the relative storage path.
    /// </summary>
    Task<string> CommitUploadAsync(string tempPath, string sha256, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Checks if an upload with this SHA-256 already exists. Returns its storage path, or null.
    /// </summary>
    string? FindExistingUpload(string sha256, string fileName);

    /// <summary>
    /// Returns the absolute path for a stored upload.
    /// </summary>
    string GetAbsolutePath(string relativePath);

    /// <summary>
    /// Writes a report file into reports/{jobId}/{format}.pal-report.{ext}. Returns relative path.
    /// </summary>
    Task<string> WriteReportAsync(Guid jobId, string format, byte[] content, CancellationToken ct = default);

    /// <summary>
    /// Opens a report file for streaming to the HTTP response.
    /// </summary>
    Stream OpenReport(string relativePath);

    /// <summary>
    /// Deletes a temp file (called when SHA-256 dedup finds an existing upload).
    /// </summary>
    void DeleteTemp(string tempPath);

    /// <summary>
    /// Deletes the report directory for a completed job (reports/{jobId:N}/).
    /// No-ops if the directory does not exist.
    /// </summary>
    void DeleteJobReportDirectory(Guid jobId);

    /// <summary>
    /// Deletes the upload directory for a given SHA-256 (uploads/{sha256}/).
    /// No-ops if the directory does not exist.
    /// </summary>
    void DeleteUploadDirectory(string sha256);
}
