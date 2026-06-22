using DocumentClassifier.Models;
using DocumentClassifier.Services;
using DocumentClassifier.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace DocumentClassifier.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentWorkflow _workflow;
    private readonly IReviewQueueStore _reviewQueue;
    private readonly DocumentClassificationWorkflowFactory _workflowFactory;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentWorkflow workflow,
        IReviewQueueStore reviewQueue,
        DocumentClassificationWorkflowFactory workflowFactory,
        Microsoft.Extensions.Options.IOptions<StorageOptions> storageOptions,
        ILogger<DocumentsController> logger)
    {
        _workflow = workflow;
        _reviewQueue = reviewQueue;
        _workflowFactory = workflowFactory;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Full pipeline: upload document, extract text, classify, and index for RAG.
    /// </summary>
    [HttpPost("process")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProcessDocumentResponse>> Process(
        IFormFile file,
        [FromQuery] string profileName = "relief_request_binary",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (file.Length > _storageOptions.MaxUploadBytes)
            return BadRequest($"File exceeds max size of {_storageOptions.MaxUploadBytes} bytes.");

        if (!IsSupportedFileType(file.FileName, file.ContentType))
            return BadRequest("Unsupported file type. Allowed: PDF, TXT, DOCX, JPG, PNG, TIFF.");

        using var stream = file.OpenReadStream();
        var result = await _workflow.ProcessAsync(stream, file.FileName, profileName, ct);
        return Ok(result);
    }

    /// <summary>
    /// Extract text only from an uploaded document.
    /// </summary>
    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> Extract(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (file.Length > _storageOptions.MaxUploadBytes)
            return BadRequest($"File exceeds max size of {_storageOptions.MaxUploadBytes} bytes.");

        if (!IsSupportedFileType(file.FileName, file.ContentType))
            return BadRequest("Unsupported file type. Allowed: PDF, TXT, DOCX, JPG, PNG, TIFF.");

        using var stream = file.OpenReadStream();
        var storage = HttpContext.RequestServices.GetRequiredService<IDocumentStorageService>();
        var extraction = HttpContext.RequestServices.GetRequiredService<ITextExtractionService>();

        var (localPath, _) = await storage.StoreAsync(stream, file.FileName, ct);
        using var extractStream = System.IO.File.OpenRead(localPath);
        var text = await extraction.ExtractTextAsync(extractStream, file.FileName, ct);

        return Ok(new { fileName = file.FileName, text, characterCount = text.Length });
    }

    /// <summary>
    /// Classify already-extracted text with a specific profile.
    /// </summary>
    [HttpPost("classify")]
    public async Task<ActionResult<ClassificationResult>> Classify(
        [FromBody] ClassifyTextRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text is required.");

        var result = await _workflow.ClassifyAsync(request.Text, request.ProfileName, ct);
        return Ok(result);
    }

    /// <summary>
    /// RAG query across the indexed document corpus.
    /// </summary>
    [HttpPost("query")]
    public async Task<ActionResult<RagResponse>> Query(
        [FromBody] QueryRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        var result = await _workflow.QueryAsync(request.Question, request.CategoryFilter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns queued low-confidence classifications for human review.
    /// </summary>
    [HttpGet("review-queue")]
    public async Task<ActionResult<IReadOnlyList<ReviewQueueItem>>> GetReviewQueue(CancellationToken ct = default)
    {
        var items = await _reviewQueue.GetAllAsync(ct);
        return Ok(items.OrderByDescending(i => i.QueuedAt).ToList());
    }

    /// <summary>
    /// List all indexed documents from Azure Cognitive Search.
    /// </summary>
    [HttpGet("indexed")]
    public async Task<ActionResult<object>> GetIndexedDocuments([FromQuery] int top = 50, CancellationToken ct = default)
    {
        var search = HttpContext.RequestServices.GetRequiredService<ISearchIndexingService>();
        var results = await search.SearchSimilarAsync("*", category: null, top: top, ct: ct);

        // Group by ChunkId to deduplicate chunks back to documents
        var documents = results
            .GroupBy(r => r.ChunkId ?? r.Id)
            .Select(g => g.OrderBy(d => d.ChunkIndex).First())
            .OrderByDescending(d => d.IndexedAt)
            .Select(d => new
            {
                d.Id,
                d.FileName,
                d.Category,
                d.Confidence,
                d.Reasoning,
                d.ProfileName,
                d.IndexedAt,
                d.TotalChunks,
                contentPreview = d.Content.Length > 300 ? d.Content[..300] + "..." : d.Content
            })
            .ToList();

        return Ok(new { documents, total = documents.Count });
    }

    /// <summary>
    /// Serve the original uploaded file for preview/download.
    /// </summary>
    [HttpGet("file/{fileName}")]
    public async Task<ActionResult> GetFile(string fileName, CancellationToken ct = default)
    {
        var storage = HttpContext.RequestServices.GetRequiredService<IDocumentStorageService>();
        try
        {
            var stream = await storage.RetrieveAsync("", fileName, ct);
            var contentType = GetContentType(fileName);
            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound($"File not found: {fileName}");
        }
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Updates status of a review queue item.
    /// </summary>
    [HttpPost("review-queue/{documentId}/status")]
    public async Task<ActionResult> UpdateReviewQueueStatus(string documentId, [FromBody] UpdateReviewStatusRequest request, CancellationToken ct = default)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pending", "in_review", "approved", "rejected", "disputed" };
        if (string.IsNullOrWhiteSpace(request.Status) || !allowed.Contains(request.Status))
            return BadRequest("Status must be one of: pending, in_review, approved, rejected, disputed.");

        var updated = await _reviewQueue.UpdateStatusAsync(documentId, request.Status, ct);
        if (!updated)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Returns the current workflow routing configuration and a Mermaid graph.
    /// </summary>
    [HttpGet("workflow")]
    public ActionResult<object> GetWorkflow()
    {
        var threshold = _workflowFactory.ConfidenceThreshold;
        var mermaid = $"""
            flowchart TD
                A[StoreDocument] --> B[ExtractText]
                B --> C[Classify]
                C -->|confidence >= {threshold:0.##}| D[IndexDocument]
                C -->|confidence < {threshold:0.##}| E[HumanReview]
            """;

        return Ok(new
        {
            confidenceThreshold = threshold,
            graph = mermaid
        });
    }

    private static bool IsSupportedFileType(string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".txt", ".docx", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        if (allowed.Contains(extension))
            return true;

        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
    }
}

public record ClassifyTextRequest
{
    public string Text { get; init; } = string.Empty;
    public string ProfileName { get; init; } = "relief_request_binary";
}

public record QueryRequest
{
    public string Question { get; init; } = string.Empty;
    public string? CategoryFilter { get; init; }
}

public record UpdateReviewStatusRequest
{
    public string Status { get; init; } = string.Empty;
}
