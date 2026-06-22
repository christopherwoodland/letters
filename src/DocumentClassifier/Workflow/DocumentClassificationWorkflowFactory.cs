using DocumentClassifier.Models;
using DocumentClassifier.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Options;

namespace DocumentClassifier.Workflow;

public class WorkflowOptions
{
    public double ConfidenceThreshold { get; set; } = 0.7;
}

/// <summary>
/// Builds and provides the document classification workflow graph.
/// 
/// Graph topology:
///   StoreDocument → ExtractText → Classify → [high confidence] → IndexDocument
///                                           → [low confidence]  → HumanReview
/// </summary>
public class DocumentClassificationWorkflowFactory
{
    private readonly IDocumentStorageService _storage;
    private readonly ITextExtractionService _extraction;
    private readonly IClassificationService _classification;
    private readonly IProfileStore _profileStore;
    private readonly ISearchIndexingService _search;
    private readonly IRagService _rag;
    private readonly IReviewQueueStore _reviewQueue;
    private readonly WorkflowOptions _options;

    public double ConfidenceThreshold => _options.ConfidenceThreshold;

    public DocumentClassificationWorkflowFactory(
        IDocumentStorageService storage,
        ITextExtractionService extraction,
        IClassificationService classification,
        IProfileStore profileStore,
        ISearchIndexingService search,
        IRagService rag,
        IReviewQueueStore reviewQueue,
        IOptions<WorkflowOptions> options)
    {
        _storage = storage;
        _extraction = extraction;
        _classification = classification;
        _profileStore = profileStore;
        _search = search;
        _rag = rag;
        _reviewQueue = reviewQueue;
        _options = options.Value;
    }

    /// <summary>
    /// Creates a new instance of the document classification workflow graph.
    /// </summary>
    public Microsoft.Agents.AI.Workflows.Workflow Build()
    {
        // Create executor instances
        var store = new StoreDocumentExecutor(_storage);
        var extract = new ExtractTextExecutor(_extraction);
        var classify = new ClassifyExecutor(_classification, _profileStore, _rag);
        var index = new IndexDocumentExecutor(_search);
        var review = new HumanReviewExecutor(_search, _reviewQueue);

        // Build the graph
        var workflow = new WorkflowBuilder(store)
            .AddEdge(store, extract)
            .AddEdge(extract, classify)
            // High confidence → index directly
            .AddEdge<ClassificationResult>(classify, index, (ClassificationResult? result) =>
                result is not null && double.IsFinite(result.Confidence) && result.Confidence >= ConfidenceThreshold)
            // Low confidence or any edge case (NaN, null) → human review (catch-all)
            .AddEdge<ClassificationResult>(classify, review, (ClassificationResult? result) =>
                result is null || !double.IsFinite(result.Confidence) || result.Confidence < ConfidenceThreshold)
            .WithOutputFrom(index, review)
            .Build();

        return workflow;
    }
}
