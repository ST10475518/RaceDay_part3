using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>Stores uploaded images and returns the public URL to save in the database.</summary>
public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder,
                             CancellationToken ct = default);
}

/// <summary>Production implementation: uploads to an Azure Blob Storage container.</summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialised;

    public AzureBlobStorageService(IConfiguration config)
    {
        var connectionString = config["AzureBlob:ConnectionString"]
            ?? throw new InvalidOperationException("AzureBlob:ConnectionString is not configured.");
        var containerName = config["AzureBlob:ContainerName"] ?? "raceday-images";
        _container = new BlobContainerClient(connectionString, containerName);
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_initialised) return;
        await _lock.WaitAsync(ct);
        try
        {
            if (_initialised) return;
            // Blob-level public read so <img src="..."> works in the browser.
            await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
            _initialised = true;
        }
        finally { _lock.Release(); }
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder,
                                          CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        var blobName = $"{folder}/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
        return blob.Uri.ToString();
    }
}

/// <summary>
/// Fallback used when no Azure connection string is configured (local development / demos).
/// Saves to an "uploads" folder and serves it from /uploads.
/// </summary>
public class LocalFileStorageService(IWebHostEnvironment env) : IBlobStorageService
{
    public static string UploadsPath(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "uploads");

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType, string folder,
                                          CancellationToken ct = default)
    {
        var dir = Path.Combine(UploadsPath(env), folder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        await using var fs = File.Create(Path.Combine(dir, name));
        await content.CopyToAsync(fs, ct);
        return $"/uploads/{folder}/{name}";
    }
}

public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    /// <summary>Returns an error message, or null when the file is an acceptable image.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Please choose an image file.";
        if (file.Length > MaxBytes) return "Image must be 5 MB or smaller.";
        if (!Types.Contains(file.ContentType) || !Extensions.Contains(Path.GetExtension(file.FileName)))
            return "Only JPG, PNG, WEBP or GIF images are allowed.";
        return null;
    }
}
