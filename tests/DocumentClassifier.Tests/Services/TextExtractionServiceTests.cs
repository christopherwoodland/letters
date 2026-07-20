using DocumentClassifier.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentClassifier.Tests.Services;

public class TextExtractionServiceTests
{
    [Fact]
    public async Task ExtractTextAsync_ExtractsTextFromPlainTextFile()
    {
        // Arrange
        var options = new Microsoft.Extensions.Options.OptionsWrapper<DocumentIntelligenceOptions>(
            new DocumentIntelligenceOptions { Endpoint = "", ExtractionMethod = "local" }
        );
        var resOptions = new Microsoft.Extensions.Options.OptionsWrapper<ResilienceOptions>(
            new ResilienceOptions { MaxRetries = 1, BaseDelayMs = 1, MaxDelayMs = 1, OperationTimeoutSeconds = 5 }
        );

        var service = new TextExtractionService(
            options,
            resOptions,
            new Azure.Identity.DefaultAzureCredential(),
            NullLogger<TextExtractionService>.Instance
        );

        var text = "This is a test document with some content.";
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var stream = new MemoryStream(bytes);

        // Act
        var result = await service.ExtractTextAsync(stream, "test.txt");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("test document", result);
    }

    [Fact]
    public async Task ExtractTextAsync_HandlesEmptyTextFile()
    {
        // Arrange
        var options = new Microsoft.Extensions.Options.OptionsWrapper<DocumentIntelligenceOptions>(
            new DocumentIntelligenceOptions { Endpoint = "", ExtractionMethod = "local" }
        );
        var resOptions = new Microsoft.Extensions.Options.OptionsWrapper<ResilienceOptions>(
            new ResilienceOptions { MaxRetries = 1, BaseDelayMs = 1, MaxDelayMs = 1, OperationTimeoutSeconds = 5 }
        );

        var service = new TextExtractionService(
            options,
            resOptions,
            new Azure.Identity.DefaultAzureCredential(),
            NullLogger<TextExtractionService>.Instance
        );

        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(""));

        // Act
        var result = await service.ExtractTextAsync(stream, "empty.txt");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractTextAsync_HandlesUnicodeContent()
    {
        // Arrange
        var options = new Microsoft.Extensions.Options.OptionsWrapper<DocumentIntelligenceOptions>(
            new DocumentIntelligenceOptions { Endpoint = "", ExtractionMethod = "local" }
        );
        var resOptions = new Microsoft.Extensions.Options.OptionsWrapper<ResilienceOptions>(
            new ResilienceOptions { MaxRetries = 1, BaseDelayMs = 1, MaxDelayMs = 1, OperationTimeoutSeconds = 5 }
        );

        var service = new TextExtractionService(
            options,
            resOptions,
            new Azure.Identity.DefaultAzureCredential(),
            NullLogger<TextExtractionService>.Instance
        );

        var text = "Unicode test: 中文, العربية, Ελληνικά";
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var stream = new MemoryStream(bytes);

        // Act
        var result = await service.ExtractTextAsync(stream, "unicode.txt");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("中文", result);
    }
}
