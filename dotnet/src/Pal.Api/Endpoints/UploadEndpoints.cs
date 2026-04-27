using Pal.Application.Persistence;
using Pal.Application.Storage;

namespace Pal.Api.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/uploads", async (HttpRequest request, Guid workspaceId, IStorageProvider storage, IUploadRepository uploads) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data");

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null)
                return Results.BadRequest("Missing 'file' field");

            var sourceType = form["sourceType"].FirstOrDefault()
                ?? Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();

            await using var stream = file.OpenReadStream();
            var (sha256, tempPath, sizeBytes) = await storage.WriteToTempAsync(stream);

            // SHA-256 dedup
            var existing = await uploads.FindBySha256Async(sha256);
            if (existing is not null)
            {
                storage.DeleteTemp(tempPath);
                return Results.Ok(new { uploadId = existing.Id, fileName = existing.FileName, sourceType = existing.SourceType });
            }

            var relativePath = await storage.CommitUploadAsync(tempPath, sha256, file.FileName);
            var upload = await uploads.CreateAsync(file.FileName, sourceType, sizeBytes, sha256, relativePath);

            return Results.Created($"/api/workspaces/{workspaceId}/uploads/{upload.Id}", new
            {
                uploadId = upload.Id,
                fileName = upload.FileName,
                sourceType = upload.SourceType
            });
        })
        .WithName("CreateUpload")
        .WithTags("Uploads")
        .DisableAntiforgery();
    }
}
