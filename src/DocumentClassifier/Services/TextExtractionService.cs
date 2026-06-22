using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using DocumentClassifier.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace DocumentClassifier.Services;

public class DocumentIntelligenceOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    /// <summary>
    /// Extraction strategy: "auto" (try local first, fallback to DI), "document_intelligence" (always use DI), "local" (local only).
    /// </summary>
    public string ExtractionMethod { get; set; } = "auto";
    /// <summary>
    /// Minimum characters from local extraction before we consider it successful.
    /// If local extraction yields fewer chars, fall back to Document Intelligence.
    /// </summary>
    public int MinLocalExtractionChars { get; set; } = 50;
}

public interface ITextExtractionService
{
    Task<string> ExtractTextAsync(Stream documentStream, string fileName, CancellationToken ct = default);
}

public class TextExtractionService : ITextExtractionService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly DocumentIntelligenceOptions _options;
    private readonly ResilienceOptions _resilienceOptions;
    private readonly ILogger<TextExtractionService> _logger;

    public TextExtractionService(
        IOptions<DocumentIntelligenceOptions> options,
        IOptions<ResilienceOptions> resilienceOptions,
        ILogger<TextExtractionService> logger)
    {
        _logger = logger;
        _options = options.Value;
        _resilienceOptions = resilienceOptions.Value;

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _client = new DocumentIntelligenceClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey));
        }
        else
        {
            _client = new DocumentIntelligenceClient(new Uri(_options.Endpoint), new DefaultAzureCredential());
        }
    }

    public async Task<string> ExtractTextAsync(Stream documentStream, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Extracting text from {FileName} using method {Method}", fileName, _options.ExtractionMethod);

        // Read stream into memory once (needed for potential fallback)
        using var ms = new MemoryStream();
        await documentStream.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();

        var method = _options.ExtractionMethod.ToLowerInvariant();
        var isPdf = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        string text;

        if (method == "local" && isPdf)
        {
            text = ExtractTextLocal(fileBytes, fileName);
        }
        else if (method == "document_intelligence")
        {
            text = await ExtractWithDocumentIntelligenceAsync(fileBytes, ct);
        }
        else // "auto" - try local first for PDFs, fall back to Document Intelligence
        {
            if (isPdf)
            {
                text = ExtractTextLocal(fileBytes, fileName);
                if (text.Length < _options.MinLocalExtractionChars || IsGarbledText(text))
                {
                    _logger.LogInformation(
                        "Local extraction yielded {Quality} for {FileName} ({Chars} chars), falling back to Document Intelligence",
                        text.Length < _options.MinLocalExtractionChars ? "too few chars" : "garbled text",
                        fileName, text.Length);
                    text = await ExtractWithDocumentIntelligenceAsync(fileBytes, ct);
                }
            }
            else
            {
                // Non-PDF files go straight to Document Intelligence (handles images, docx, etc.)
                text = await ExtractWithDocumentIntelligenceAsync(fileBytes, ct);
            }
        }

        _logger.LogInformation("Extracted {Length} characters from {FileName}", text.Length, fileName);
        return text;
    }

    /// <summary>
    /// Fast local PDF text extraction using PdfPig. Works well for text-based (digital) PDFs.
    /// Will return minimal/empty text for scanned documents.
    /// </summary>
    private string ExtractTextLocal(byte[] fileBytes, string fileName)
    {
        try
        {
            using var document = PdfDocument.Open(fileBytes);
            var pages = new List<string>();

            foreach (var page in document.GetPages())
            {
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    pages.Add(pageText);
                }
            }

            var result = string.Join("\n\n", pages);
            _logger.LogInformation("Local extraction got {Chars} chars from {FileName}", result.Length, fileName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local PDF extraction failed for {FileName}", fileName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Azure Document Intelligence extraction using prebuilt-read model.
    /// Handles scanned documents, complex layouts, images, and protected PDFs.
    /// </summary>
    private async Task<string> ExtractWithDocumentIntelligenceAsync(byte[] fileBytes, CancellationToken ct)
    {
        return await Resilience.ExecuteWithRetryAsync(async retryCt =>
        {
            var binaryData = BinaryData.FromBytes(fileBytes);

            var operation = await _client.AnalyzeDocumentAsync(
                Azure.WaitUntil.Completed,
                "prebuilt-read",
                binaryData,
                cancellationToken: retryCt);

            var result = operation.Value;
            return result.Content;
        }, _logger, "Document Intelligence extraction", _resilienceOptions, ct);
    }

    /// <summary>
    /// Detects garbled/corrupted text from PDFs with broken text layers.
    /// </summary>
    private bool IsGarbledText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 100)
            return false;

        var sample = text.Length > 1000 ? text.Substring(0, 1000) : text;

        int printable = 0;
        int alphaNum = 0;
        foreach (var c in sample)
        {
            if ((c >= ' ' && c <= '~') || char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                printable++;
            if (char.IsLetterOrDigit(c))
                alphaNum++;
        }

        double printableRatio = (double)printable / sample.Length;
        double alphaNumRatio = (double)alphaNum / sample.Length;

        if (printableRatio < 0.70 || alphaNumRatio < 0.30)
        {
            _logger.LogInformation(
                "Text quality check: printable={Printable:P0}, alphaNum={AlphaNum:P0} - garbled",
                printableRatio, alphaNumRatio);
            return true;
        }

        int specialRuns = 0;
        int consecutive = 0;
        foreach (var c in sample)
        {
            if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
                consecutive++;
            else
            {
                if (consecutive >= 5)
                    specialRuns++;
                consecutive = 0;
            }
        }

        if (specialRuns > sample.Length / 50)
        {
            _logger.LogInformation(
                "Text quality check: {Runs} long special-char runs - garbled", specialRuns);
            return true;
        }

        return false;
    }
}
