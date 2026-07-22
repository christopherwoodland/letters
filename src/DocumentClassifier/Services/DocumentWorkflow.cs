using DocumentClassifier.Models;
using DocumentClassifier.Workflow;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentClassifier.Services;

/// <summary>
/// Orchestrates document processing using Microsoft Agent Framework workflow graph:
/// StoreDocument → ExtractText → Classify → [high confidence] → IndexDocument
///                                        → [low confidence]  → HumanReview
/// Also provides standalone Extract, Classify, and RAG Query operations.
/// </summary>
public interface IDocumentWorkflow
{
    Task<ProcessDocumentResponse> ProcessAsync(Stream documentStream, string fileName, string profileName, CancellationToken ct = default);
    Task<string> ExtractAsync(string documentId, string fileName, CancellationToken ct = default);
    Task<ClassificationResult> ClassifyAsync(string text, string profileName, CancellationToken ct = default);
    Task<RagResponse> QueryAsync(string question, string? categoryFilter = null, CancellationToken ct = default);
}

public class DocumentWorkflow : IDocumentWorkflow
{
    private readonly IDocumentStorageService _storage;
    private readonly ITextExtractionService _extraction;
    private readonly IClassificationService _classification;
    private readonly IProfileStore _profileStore;
    private readonly IRagService _rag;
    private readonly ISearchIndexingService _search;
    private readonly IReviewQueueStore _reviewQueue;
    private readonly WorkflowOptions _options;
    private readonly ILogger<DocumentWorkflow> _logger;

    public DocumentWorkflow(
        IDocumentStorageService storage,
        ITextExtractionService extraction,
        IClassificationService classification,
        IProfileStore profileStore,
        IRagService rag,
        ISearchIndexingService search,
        IReviewQueueStore reviewQueue,
        IOptions<WorkflowOptions> options,
        ILogger<DocumentWorkflow> logger)
    {
        _storage = storage;
        _extraction = extraction;
        _classification = classification;
        _profileStore = profileStore;
        _rag = rag;
        _search = search;
        _reviewQueue = reviewQueue;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Full pipeline: store → extract → classify → index/review.
    /// </summary>
    public async Task<ProcessDocumentResponse> ProcessAsync(Stream documentStream, string fileName, string profileName, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting workflow for {FileName} with profile {Profile}", fileName, profileName);

        // Step 1: Store document
        var (localPath, blobUri) = await _storage.StoreAsync(documentStream, fileName, ct);
        var documentId = Guid.NewGuid().ToString();

        // Step 2: Extract text
        using var extractStream = File.OpenRead(localPath);
        var text = await _extraction.ExtractTextAsync(extractStream, fileName, ct);

        // Step 3: Get RAG examples and classify
        var profile = _profileStore.GetProfile(profileName)
            ?? throw new InvalidOperationException($"Classification profile '{profileName}' not found.");

        var examples = _options.EnableRagExamples
            ? await _rag.GetClassificationExamplesAsync(text, profileName, count: 3, ct)
            : new List<ClassificationExample>();
        var classification = await _classification.ClassifyAsync(text, profile, examples, ct);

        // Step 4: Route based on confidence
        if (classification.Confidence >= _options.ConfidenceThreshold)
        {
            // High confidence → index directly
            if (_options.EnableRagIndexing)
            {
                await _search.IndexDocumentAsync(new IndexedDocument
                {
                    Id = documentId,
                    FileName = fileName,
                    Content = text,
                    Category = classification.Category,
                    ProfileName = profileName,
                    Confidence = classification.Confidence,
                    Reasoning = classification.Reasoning,
                    IndexedAt = DateTimeOffset.UtcNow
                }, ct);
            }

            _logger.LogInformation("Workflow complete for {FileName}: {Category} ({Confidence:P0})",
                fileName, classification.Category, classification.Confidence);

            return new ProcessDocumentResponse
            {
                DocumentId = documentId,
                FileName = fileName,
                ExtractedText = text,
                Classification = classification,
                Status = DocumentStatus.Classified
            };
        }
        else
        {
            // Low confidence → human review
            if (_options.EnableRagIndexing)
            {
                await _search.IndexDocumentAsync(new IndexedDocument
                {
                    Id = documentId,
                    FileName = fileName,
                    Content = text,
                    Category = $"REVIEW:{classification.Category}",
                    ProfileName = profileName,
                    Confidence = classification.Confidence,
                    Reasoning = $"[LOW CONFIDENCE - NEEDS HUMAN REVIEW] {classification.Reasoning}",
                    IndexedAt = DateTimeOffset.UtcNow
                }, ct);
            }

            await _reviewQueue.EnqueueAsync(new ReviewQueueItem
            {
                DocumentId = documentId,
                FileName = fileName,
                ProfileName = profileName,
                Category = classification.Category,
                Confidence = classification.Confidence,
                Reasoning = classification.Reasoning
            }, ct);

            _logger.LogInformation("Workflow complete for {FileName}: REVIEW:{Category} ({Confidence:P0})",
                fileName, classification.Category, classification.Confidence);

            return new ProcessDocumentResponse
            {
                DocumentId = documentId,
                FileName = fileName,
                ExtractedText = text,
                Classification = classification with { Category = $"REVIEW:{classification.Category}" },
                Status = DocumentStatus.NeedsReview
            };
        }
    }

    /// <summary>
    /// Extract text only (standalone, not through the graph).
    /// </summary>
    public async Task<string> ExtractAsync(string documentId, string fileName, CancellationToken ct = default)
    {
        using var stream = await _storage.RetrieveAsync(documentId, fileName, ct);
        return await _extraction.ExtractTextAsync(stream, fileName, ct);
    }

    /// <summary>
    /// Classify already-extracted text with a specific profile (standalone).
    /// </summary>
    public async Task<ClassificationResult> ClassifyAsync(string text, string profileName, CancellationToken ct = default)
    {
        var profile = _profileStore.GetProfile(profileName)
            ?? throw new InvalidOperationException($"Classification profile '{profileName}' not found.");

        var examples = _options.EnableRagExamples
            ? await _rag.GetClassificationExamplesAsync(text, profileName, count: 3, ct)
            : new List<ClassificationExample>();
        return await _classification.ClassifyAsync(text, profile, examples, ct);
    }

    /// <summary>
    /// RAG query across the indexed document corpus.
    /// </summary>
    public async Task<RagResponse> QueryAsync(string question, string? categoryFilter = null, CancellationToken ct = default)
    {
        if (!_options.EnableRagQuery)
        {
            return new RagResponse
            {
                Answer = "RAG query is disabled by configuration (Workflow:EnableRagQuery=false).",
                Sources = new List<RagSource>()
            };
        }

        return await _rag.QueryAsync(question, categoryFilter, ct);
    }
}
