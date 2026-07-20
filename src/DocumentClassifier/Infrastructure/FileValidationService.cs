using System.Security.Cryptography;

namespace DocumentClassifier.Infrastructure;

/// <summary>
/// Service for validating file content integrity using magic numbers (file signatures).
/// Prevents malicious files from being uploaded by verifying content matches the declared type.
/// </summary>
public interface IFileValidationService
{
    /// <summary>
    /// Validates that file content matches the declared file extension.
    /// </summary>
    /// <param name="stream">File stream to validate</param>
    /// <param name="fileName">Original file name with extension</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateFileContentAsync(Stream stream, string fileName);
    
    /// <summary>
    /// Gets the detected MIME type based on file magic numbers.
    /// </summary>
    string GetDetectedMimeType(ReadOnlySpan<byte> fileSignature);
}

/// <summary>
/// File validation service using magic number verification.
/// </summary>
public class FileValidationService : IFileValidationService
{
    // File magic numbers (signatures) for supported file types
    private static readonly Dictionary<byte[], string> MagicNumbers = new()
    {
        // PDF: %PDF
        { new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf" },
        
        // JPEG: FF D8 FF
        { new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg" },
        
        // PNG: 89 50 4E 47
        { new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png" },
        
        // TIFF (little-endian): 49 49 2A 00
        { new byte[] { 0x49, 0x49, 0x2A, 0x00 }, "image/tiff" },
        
        // TIFF (big-endian): 4D 4D 00 2A
        { new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, "image/tiff" },
        
        // DOCX/Office: 50 4B 03 04 (ZIP header) - followed by [Content_Types].xml
        { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
    };

    private const int SignatureReadLength = 512; // Read first 512 bytes for magic number detection

    public async Task<bool> ValidateFileContentAsync(Stream stream, string fileName)
    {
        if (stream == null || stream.Length == 0)
            return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Text files don't have magic numbers; just validate extension
        if (extension == ".txt")
            return true;

        // Read file signature
        var buffer = new byte[SignatureReadLength];
        var bytesRead = await stream.ReadAsync(buffer, 0, SignatureReadLength);
        stream.Position = 0; // Reset stream position

        var signature = buffer.AsSpan(0, bytesRead);
        var detectedMimeType = GetDetectedMimeType(signature);

        // Validate based on file extension
        return extension switch
        {
            ".pdf" => detectedMimeType == "application/pdf",
            ".jpg" or ".jpeg" => detectedMimeType == "image/jpeg",
            ".png" => detectedMimeType == "image/png",
            ".tiff" or ".tif" => detectedMimeType == "image/tiff",
            ".docx" => detectedMimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => false
        };
    }

    public string GetDetectedMimeType(ReadOnlySpan<byte> fileSignature)
    {
        foreach (var (magic, mimeType) in MagicNumbers)
        {
            if (fileSignature.Length >= magic.Length &&
                fileSignature.Slice(0, magic.Length).SequenceEqual(magic))
            {
                return mimeType;
            }
        }

        return "application/octet-stream";
    }
}
