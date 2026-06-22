using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocumentClassifier.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentClassifier.Services;

public interface IRagService
{
    /// <summary>
    /// Query the document corpus with RAG - retrieves relevant docs and generates a grounded answer.
    /// </summary>
    Task<RagResponse> QueryAsync(string question, string? categoryFilter = null, CancellationToken ct = default);

    /// <summary>
    /// Get similar classified documents to use as few-shot examples for classification.
    /// </summary>
    Task<List<ClassificationExample>> GetClassificationExamplesAsync(string documentText, string profileName, int count = 3, CancellationToken ct = default);
}

public record RagResponse
{
    public string Answer { get; init; } = string.Empty;
    public List<RagSource> Sources { get; init; } = new();
}

public record RagSource
{
    public string FileName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
}

public record ClassificationExample
{
    public string Snippet { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Reasoning { get; init; } = string.Empty;
}

public class RagService : IRagService
{
    private readonly ISearchIndexingService _search;
    private readonly ChatClient _chatClient;
    private readonly ResilienceOptions _resilienceOptions;
    private readonly ILogger<RagService> _logger;

    public RagService(
        ISearchIndexingService search,
        IOptions<AzureOpenAIOptions> openAIOptions,
        IOptions<ResilienceOptions> resilienceOptions,
        ILogger<RagService> logger)
    {
        _search = search;
        _resilienceOptions = resilienceOptions.Value;
        _logger = logger;

        var opts = openAIOptions.Value;
        AzureOpenAIClient aoaiClient;
        if (!string.IsNullOrEmpty(opts.ApiKey))
        {
            aoaiClient = new AzureOpenAIClient(new Uri(opts.Endpoint), new Azure.AzureKeyCredential(opts.ApiKey));
        }
        else
        {
            aoaiClient = new AzureOpenAIClient(new Uri(opts.Endpoint), new DefaultAzureCredential());
        }
        _chatClient = aoaiClient.GetChatClient(opts.DeploymentName);
    }

    public async Task<RagResponse> QueryAsync(string question, string? categoryFilter = null, CancellationToken ct = default)
    {
        _logger.LogInformation("RAG query: {Question}", question);

        // Retrieve relevant documents
        var searchResults = await _search.SearchSimilarAsync(question, categoryFilter, top: 5, ct);

        if (searchResults.Count == 0)
        {
            return new RagResponse
            {
                Answer = "No relevant documents found in the index to answer this question.",
                Sources = new()
            };
        }

        // Build context from retrieved documents
        var context = string.Join("\n\n---\n\n", searchResults.Select(doc =>
            $"[{doc.FileName}] (Category: {doc.Category})\n{doc.Content[..Math.Min(doc.Content.Length, 2000)]}"));

        var systemPrompt = """
            You are a legal document analysis assistant for a court filing system.
            Answer the user's question based ONLY on the provided document context.
            If the context doesn't contain enough information to answer, say so.
            Always cite which document(s) you're referencing.
            """;

        var userPrompt = $"""
            Context documents:
            {context}

            Question: {question}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var completion = await Resilience.ExecuteWithRetryAsync(
            retryCt => _chatClient.CompleteChatAsync(messages, cancellationToken: retryCt),
            _logger,
            "RAG answer generation",
            _resilienceOptions,
            ct);
        var answer = completion.Value.Content[0].Text;

        return new RagResponse
        {
            Answer = answer,
            Sources = searchResults.Select(doc => new RagSource
            {
                FileName = doc.FileName,
                Category = doc.Category ?? "unclassified",
                Snippet = doc.Content[..Math.Min(doc.Content.Length, 200)]
            }).ToList()
        };
    }

    public async Task<List<ClassificationExample>> GetClassificationExamplesAsync(
        string documentText, string profileName, int count = 3, CancellationToken ct = default)
    {
        // Search for similar documents that have already been classified with this profile
        var snippet = documentText[..Math.Min(documentText.Length, 500)];
        var results = await _search.SearchSimilarAsync(snippet, category: null, top: count * 2, ct);

        // Filter to documents classified with the same profile
        var examples = results
            .Where(r => r.ProfileName == profileName && !string.IsNullOrEmpty(r.Category))
            .Take(count)
            .Select(r => new ClassificationExample
            {
                Snippet = r.Content[..Math.Min(r.Content.Length, 300)],
                Category = r.Category!,
                Reasoning = r.Reasoning ?? ""
            })
            .ToList();

        _logger.LogInformation("Found {Count} classification examples for profile {Profile}", examples.Count, profileName);
        return examples;
    }
}
