using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentClassifier.Services;

public class StorageOptions
{
    public string LocalStoragePath { get; set; } = "./documents";
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
    public string? BlobConnectionString { get; set; }
    public string? BlobContainerName { get; set; } = "court-documents";
    public string? BlobAccountUri { get; set; }
}

public interface IDocumentStorageService
{
    Task<(string localPath, string? blobUri)> StoreAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<Stream> RetrieveAsync(string documentId, string fileName, CancellationToken ct = default);
}

public class DocumentStorageService : IDocumentStorageService
{
    private readonly StorageOptions _options;
    private readonly BlobContainerClient? _blobContainer;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(IOptions<StorageOptions> options, ILogger<DocumentStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        Directory.CreateDirectory(_options.LocalStoragePath);

        if (!string.IsNullOrEmpty(_options.BlobConnectionString))
        {
            _blobContainer = new BlobContainerClient(_options.BlobConnectionString, _options.BlobContainerName);
        }
        else if (!string.IsNullOrEmpty(_options.BlobAccountUri))
        {
            var serviceClient = new BlobServiceClient(new Uri(_options.BlobAccountUri), new DefaultAzureCredential());
            _blobContainer = serviceClient.GetBlobContainerClient(_options.BlobContainerName);
        }
    }

    public async Task<(string localPath, string? blobUri)> StoreAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var sanitizedName = SanitizeFileName(fileName);
        var localPath = Path.Combine(_options.LocalStoragePath, sanitizedName);

        // Store locally
        using (var fileStream = File.Create(localPath))
        {
            await content.CopyToAsync(fileStream, ct);
        }
        _logger.LogInformation("Stored document locally at {Path}", localPath);

        // Store in blob if configured
        string? blobUri = null;
        if (_blobContainer is not null)
        {
            await _blobContainer.CreateIfNotExistsAsync(cancellationToken: ct);
            var blobClient = _blobContainer.GetBlobClient(sanitizedName);

            using var uploadStream = File.OpenRead(localPath);
            await blobClient.UploadAsync(uploadStream, overwrite: true, cancellationToken: ct);
            blobUri = blobClient.Uri.ToString();
            _logger.LogInformation("Stored document in blob at {Uri}", blobUri);
        }

        return (localPath, blobUri);
    }

    public Task<Stream> RetrieveAsync(string documentId, string fileName, CancellationToken ct = default)
    {
        var sanitizedName = SanitizeFileName(fileName);
        var localPath = Path.Combine(_options.LocalStoragePath, sanitizedName);

        if (!File.Exists(localPath))
            throw new FileNotFoundException($"Document not found: {fileName}");

        return Task.FromResult<Stream>(File.OpenRead(localPath));
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
