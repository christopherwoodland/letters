using DocumentClassifier.Models;
using DocumentClassifier.Services;
using Microsoft.Agents.AI.Workflows;

namespace DocumentClassifier.Workflow;

/// <summary>
/// Shared state key constants for the document classification workflow.
/// </summary>
public static class WorkflowState
{
    public const string Scope = "DocumentProcessing";
    public const string DocumentId = "DocumentId";
    public const string FileName = "FileName";
    public const string LocalPath = "LocalPath";
    public const string BlobUri = "BlobUri";
    public const string ExtractedText = "ExtractedText";
    public const string ProfileName = "ProfileName";
    public const string ClassificationResultKey = "ClassificationResult";
}

/// <summary>
/// Input message to start the document workflow.
/// </summary>
public record DocumentWorkflowInput
{
    public required byte[] FileBytes { get; init; }
    public required string FileName { get; init; }
    public required string ProfileName { get; init; }
}

/// <summary>
/// Step 1: Store the uploaded document locally (and optionally to blob).
/// </summary>
public sealed class StoreDocumentExecutor : Executor<DocumentWorkflowInput, string>
{
    private readonly IDocumentStorageService _storage;

    public StoreDocumentExecutor(IDocumentStorageService storage) : base("StoreDocument")
    {
        _storage = storage;
    }

    public override async ValueTask<string> HandleAsync(
        DocumentWorkflowInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(message.FileBytes);
        var (localPath, blobUri) = await _storage.StoreAsync(stream, message.FileName, cancellationToken);

        var documentId = Guid.NewGuid().ToString();

        // Persist state for downstream steps
        await context.QueueStateUpdateAsync(WorkflowState.DocumentId, documentId, scopeName: WorkflowState.Scope, cancellationToken);
        await context.QueueStateUpdateAsync(WorkflowState.FileName, message.FileName, scopeName: WorkflowState.Scope, cancellationToken);
        await context.QueueStateUpdateAsync(WorkflowState.LocalPath, localPath, scopeName: WorkflowState.Scope, cancellationToken);
        await context.QueueStateUpdateAsync(WorkflowState.BlobUri, blobUri ?? "", scopeName: WorkflowState.Scope, cancellationToken);
        await context.QueueStateUpdateAsync(WorkflowState.ProfileName, message.ProfileName, scopeName: WorkflowState.Scope, cancellationToken);

        // Pass local path forward for extraction
        return localPath;
    }
}

/// <summary>
/// Step 2: Extract text from the stored document using local OCR or Azure Document Intelligence.
/// </summary>
public sealed class ExtractTextExecutor : Executor<string, string>
{
    private readonly ITextExtractionService _extraction;

    public ExtractTextExecutor(ITextExtractionService extraction) : base("ExtractText")
    {
        _extraction = extraction;
    }

    public override async ValueTask<string> HandleAsync(
        string localPath, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var fileName = await context.ReadStateAsync<string>(WorkflowState.FileName, scopeName: WorkflowState.Scope, cancellationToken)
            ?? "document.pdf";

        using var stream = File.OpenRead(localPath);
        var text = await _extraction.ExtractTextAsync(stream, fileName, cancellationToken);

        await context.QueueStateUpdateAsync(WorkflowState.ExtractedText, text, scopeName: WorkflowState.Scope, cancellationToken);
        return text;
    }
}

/// <summary>
/// Step 3: Classify the extracted text using the configured profile + RAG few-shot examples.
/// </summary>
public sealed class ClassifyExecutor : Executor<string, ClassificationResult>
{
    private readonly IClassificationService _classification;
    private readonly IProfileStore _profileStore;
    private readonly IRagService _rag;

    public ClassifyExecutor(
        IClassificationService classification,
        IProfileStore profileStore,
        IRagService rag) : base("Classify")
    {
        _classification = classification;
        _profileStore = profileStore;
        _rag = rag;
    }

    public override async ValueTask<ClassificationResult> HandleAsync(
        string text, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var profileName = await context.ReadStateAsync<string>(WorkflowState.ProfileName, scopeName: WorkflowState.Scope, cancellationToken)
            ?? "relief_request_binary";

        var profile = _profileStore.GetProfile(profileName)
            ?? throw new InvalidOperationException($"Classification profile '{profileName}' not found.");

        // Get RAG few-shot examples from previously classified documents
        var examples = await _rag.GetClassificationExamplesAsync(text, profileName, count: 3, cancellationToken);

        var result = await _classification.ClassifyAsync(text, profile, examples, cancellationToken);

        await context.QueueStateUpdateAsync(WorkflowState.ClassificationResultKey, result, scopeName: WorkflowState.Scope, cancellationToken);
        return result;
    }
}

/// <summary>
/// Step 4a: Index the classified document into Azure AI Search for RAG (high-confidence path).
/// </summary>
[YieldsOutput(typeof(ProcessDocumentResponse))]
public sealed class IndexDocumentExecutor : Executor<ClassificationResult>
{
    private readonly ISearchIndexingService _search;

    public IndexDocumentExecutor(ISearchIndexingService search) : base("IndexDocument")
    {
        _search = search;
    }

    public override async ValueTask HandleAsync(
        ClassificationResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var documentId = await context.ReadStateAsync<string>(WorkflowState.DocumentId, scopeName: WorkflowState.Scope, cancellationToken) ?? "";
        var fileName = await context.ReadStateAsync<string>(WorkflowState.FileName, scopeName: WorkflowState.Scope, cancellationToken) ?? "";
        var text = await context.ReadStateAsync<string>(WorkflowState.ExtractedText, scopeName: WorkflowState.Scope, cancellationToken) ?? "";
        var profileName = await context.ReadStateAsync<string>(WorkflowState.ProfileName, scopeName: WorkflowState.Scope, cancellationToken) ?? "";

        await _search.IndexDocumentAsync(new IndexedDocument
        {
            Id = documentId,
            FileName = fileName,
            Content = text,
            Category = message.Category,
            ProfileName = profileName,
            Confidence = message.Confidence,
            Reasoning = message.Reasoning,
            IndexedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        var response = new ProcessDocumentResponse
        {
            DocumentId = documentId,
            FileName = fileName,
            ExtractedText = text,
            Classification = message,
            Status = DocumentStatus.Classified
        };

        await context.YieldOutputAsync(response, cancellationToken);
    }
}

/// <summary>
/// Step 4b: Human review path for low-confidence classifications.
/// In the current implementation, this marks the document for review and still indexes it.
/// A real implementation would queue it for a human reviewer.
/// </summary>
[YieldsOutput(typeof(ProcessDocumentResponse))]
public sealed class HumanReviewExecutor : Executor<ClassificationResult>
{
    private readonly ISearchIndexingService _search;
    private readonly IReviewQueueStore _reviewQueue;

    public HumanReviewExecutor(ISearchIndexingService search, IReviewQueueStore reviewQueue) : base("HumanReview")
    {
        _search = search;
        _reviewQueue = reviewQueue;
    }

    public override async ValueTask HandleAsync(
        ClassificationResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var documentId = await context.ReadStateAsync<string>(WorkflowState.DocumentId, scopeName: WorkflowState.Scope, cancellationToken) ?? "";
        var fileName = await context.ReadStateAsync<string>(WorkflowState.FileName, scopeName: WorkflowState.Scope, cancellationToken) ?? "";
        var text = await context.ReadStateAsync<string>(WorkflowState.ExtractedText, scopeName: WorkflowState.Scope, cancellationToken) ?? "";
        var profileName = await context.ReadStateAsync<string>(WorkflowState.ProfileName, scopeName: WorkflowState.Scope, cancellationToken) ?? "";

        // Index with a "needs_review" flag
        await _search.IndexDocumentAsync(new IndexedDocument
        {
            Id = documentId,
            FileName = fileName,
            Content = text,
            Category = $"REVIEW:{message.Category}",
            ProfileName = profileName,
            Confidence = message.Confidence,
            Reasoning = $"[LOW CONFIDENCE - NEEDS HUMAN REVIEW] {message.Reasoning}",
            IndexedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _reviewQueue.EnqueueAsync(new ReviewQueueItem
        {
            DocumentId = documentId,
            FileName = fileName,
            ProfileName = profileName,
            Category = message.Category,
            Confidence = message.Confidence,
            Reasoning = message.Reasoning
        }, cancellationToken);

        var response = new ProcessDocumentResponse
        {
            DocumentId = documentId,
            FileName = fileName,
            ExtractedText = text,
            Classification = message with { Category = $"REVIEW:{message.Category}" },
            Status = DocumentStatus.NeedsReview
        };

        await context.YieldOutputAsync(response, cancellationToken);
    }
}
