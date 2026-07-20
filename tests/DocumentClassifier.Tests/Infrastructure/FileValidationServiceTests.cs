using DocumentClassifier.Infrastructure;
using Xunit;

namespace DocumentClassifier.Tests.Infrastructure;

public class FileValidationServiceTests
{
    private readonly FileValidationService _service = new();

    [Theory]
    [InlineData(".pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }, true)]       // Valid PDF
    [InlineData(".pdf", new byte[] { 0xFF, 0xD8, 0xFF }, false)]            // JPEG magic, PDF extension
    [InlineData(".jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]       // Valid JPEG
    [InlineData(".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }, true)]       // Valid PNG
    [InlineData(".txt", new byte[] { 0x48, 0x65, 0x6C, 0x6C }, true)]       // Valid text
    [InlineData(".docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 }, true)]      // Valid DOCX (ZIP)
    public async Task ValidateFileContentAsync_WithValidContent_ReturnsTrue(string extension, byte[] content, bool expected)
    {
        // Arrange
        var stream = new MemoryStream(content);
        var fileName = $"test{extension}";

        // Act
        var result = await _service.ValidateFileContentAsync(stream, fileName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_WithMissingExtension_ReturnsFalse()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var fileName = "noextension";

        // Act
        var result = await _service.ValidateFileContentAsync(stream, fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_WithEmptyStream_ReturnsFalse()
    {
        // Arrange
        var stream = new MemoryStream(Array.Empty<byte>());
        var fileName = "empty.pdf";

        // Act
        var result = await _service.ValidateFileContentAsync(stream, fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateFileContentAsync_WithContentMismatch_ReturnsFalse()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });  // JPEG magic
        var fileName = "fake.pdf";  // But claimed as PDF

        // Act
        var result = await _service.ValidateFileContentAsync(stream, fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetDetectedMimeType_WithValidMagic_ReturnsCorrectType()
    {
        // Arrange
        var pdfMagic = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        // Act
        var mimeType = _service.GetDetectedMimeType(pdfMagic);

        // Assert
        Assert.Equal("application/pdf", mimeType);
    }

    [Fact]
    public void GetDetectedMimeType_WithUnknownMagic_ReturnsOctetStream()
    {
        // Arrange
        var unknownMagic = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

        // Act
        var mimeType = _service.GetDetectedMimeType(unknownMagic);

        // Assert
        Assert.Equal("application/octet-stream", mimeType);
    }
}
