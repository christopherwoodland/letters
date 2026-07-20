using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using DocumentClassifier.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentClassifier.Services;

public class SearchOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string IndexName { get; set; } = "court-documents";
    public string? EmbeddingDeploymentName { get; set; }
    public string? OpenAIEndpoint { get; set; }
}

public interface ISearchIndexingService
{
    Task EnsureIndexExistsAsync(CancellationToken ct = default);
    Task IndexDocumentAsync(IndexedDocument document, CancellationToken ct = default);
    Task<List<IndexedDocument>> SearchSimilarAsync(string query, string? category = null, int top = 5, CancellationToken ct = default);
}

public class IndexedDocument
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? ProfileName { get; set; }
    public double? Confidence { get; set; }
    public string? Reasoning { get; set; }
    public DateTimeOffset IndexedAt { get; set; } = DateTimeOffset.UtcNow;

    // Chunked content for large documents
    public string? ChunkId { get; set; }
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
}

public class SearchIndexingService : ISearchIndexingService
{
    private readonly SearchClient? _searchClient;
    private readonly SearchIndexClient? _indexClient;
    private readonly SearchOptions _options;
    private readonly ResilienceOptions _resilienceOptions;
    private readonly ILogger<SearchIndexingService> _logger;
    private readonly bool _isConfigured;

    public SearchIndexingService(
        IOptions<SearchOptions> options,
        IOptions<ResilienceOptions> resilienceOptions,
        TokenCredential credential,
        ILogger<SearchIndexingService> logger)
    {
        _options = options.Value;
        _resilienceOptions = resilienceOptions.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            _isConfigured = false;
            _logger.LogInformation("Search endpoint not configured. Search indexing and retrieval will be disabled.");
            return;
        }

        _isConfigured = true;
        var endpoint = new Uri(_options.Endpoint);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            var apiKeyCredential = new AzureKeyCredential(_options.ApiKey);
            _indexClient = new SearchIndexClient(endpoint, apiKeyCredential);
            _searchClient = new SearchClient(endpoint, _options.IndexName, apiKeyCredential);
        }
        else
        {
            _indexClient = new SearchIndexClient(endpoint, credential);
            _searchClient = new SearchClient(endpoint, _options.IndexName, credential);
        }
    }

    public async Task EnsureIndexExistsAsync(CancellationToken ct = default)
    {
        if (!_isConfigured || _indexClient is null)
        {
            _logger.LogDebug("EnsureIndexExistsAsync skipped because search is not configured.");
            return;
        }

        var fields = new List<SearchField>
        {
            new SimpleField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SearchableField("FileName") { IsFilterable = true, IsSortable = true },
            new SearchableField("Content") { AnalyzerName = LexicalAnalyzerName.EnLucene },
            new SimpleField("Category", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("ProfileName", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("Confidence", SearchFieldDataType.Double) { IsSortable = true },
            new SearchableField("Reasoning"),
            new SimpleField("IndexedAt", SearchFieldDataType.DateTimeOffset) { IsSortable = true, IsFilterable = true },
            new SimpleField("ChunkId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("ChunkIndex", SearchFieldDataType.Int32) { IsSortable = true },
            new SimpleField("TotalChunks", SearchFieldDataType.Int32),
        };

        // Add vector field if embedding deployment is configured
        if (!string.IsNullOrEmpty(_options.EmbeddingDeploymentName))
        {
            fields.Add(new VectorSearchField("ContentVector", 1536, "vector-profile"));
        }

        var index = new SearchIndex(_options.IndexName, fields);

        if (!string.IsNullOrEmpty(_options.EmbeddingDeploymentName))
        {
            index.VectorSearch = new VectorSearch
            {
                Profiles = { new VectorSearchProfile("vector-profile", "vector-algorithm") },
                Algorithms = { new HnswAlgorithmConfiguration("vector-algorithm") },
                Vectorizers =
                {
                    new AzureOpenAIVectorizer("openai-vectorizer")
                    {
                        Parameters = new AzureOpenAIVectorizerParameters
                        {
                            ResourceUri = new Uri(_options.OpenAIEndpoint ?? _options.Endpoint),
                            DeploymentName = _options.EmbeddingDeploymentName,
                            ModelName = "text-embedding-ada-002"
                        }
                    }
                }
            };
        }

        await Resilience.ExecuteWithRetryAsync(
            async retryCt =>
            {
                await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: retryCt);
                return true;
            },
            _logger,
            "Search index create/update",
            _resilienceOptions,
            ct);
        _logger.LogInformation("Search index '{IndexName}' ensured", _options.IndexName);
    }

    public async Task IndexDocumentAsync(IndexedDocument document, CancellationToken ct = default)
    {
        if (!_isConfigured || _searchClient is null)
        {
            _logger.LogDebug("IndexDocumentAsync skipped for {Id} because search is not configured.", document.Id);
            return;
        }

        // Chunk large documents (>8000 chars) for better retrieval
        var chunks = ChunkContent(document.Content, document.Id, maxChunkSize: 4000, overlap: 200);

        var documents = chunks.Select((chunk, i) => new IndexedDocument
        {
            Id = chunks.Count == 1 ? document.Id : $"{document.Id}_chunk_{i}",
            FileName = document.FileName,
            Content = chunk,
            Category = document.Category,
            ProfileName = document.ProfileName,
            Confidence = document.Confidence,
            Reasoning = document.Reasoning,
            IndexedAt = document.IndexedAt,
            ChunkId = document.Id,
            ChunkIndex = i,
            TotalChunks = chunks.Count
        }).ToList();

        var batch = IndexDocumentsBatch.Upload(documents);
        await Resilience.ExecuteWithRetryAsync(
            async retryCt =>
            {
                await _searchClient.IndexDocumentsAsync(batch, cancellationToken: retryCt);
                return true;
            },
            _logger,
            "Search indexing",
            _resilienceOptions,
            ct);

        _logger.LogInformation("Indexed document {Id} ({Chunks} chunks) into search", document.Id, chunks.Count);
    }

    public async Task<List<IndexedDocument>> SearchSimilarAsync(string query, string? category = null, int top = 5, CancellationToken ct = default)
    {
        if (!_isConfigured || _searchClient is null)
        {
            _logger.LogDebug("SearchSimilarAsync skipped because search is not configured.");
            return new List<IndexedDocument>();
        }

        var searchOptions = new Azure.Search.Documents.SearchOptions
        {
            Size = top,
            Select = { "Id", "FileName", "Content", "Category", "ProfileName", "Confidence", "Reasoning", "IndexedAt" }
        };

        if (!string.IsNullOrEmpty(category))
        {
            searchOptions.Filter = $"Category eq '{category}'";
        }

        var response = await Resilience.ExecuteWithRetryAsync(
            retryCt => _searchClient.SearchAsync<IndexedDocument>(query, searchOptions, retryCt),
            _logger,
            "Search query",
            _resilienceOptions,
            ct);

        var results = new List<IndexedDocument>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(result.Document);
        }

        _logger.LogInformation("Search returned {Count} results for query", results.Count);
        return results;
    }

    private static List<string> ChunkContent(string content, string documentId, int maxChunkSize = 4000, int overlap = 200)
    {
        if (content.Length <= maxChunkSize)
            return new List<string> { content };

        var chunks = new List<string>();
        var position = 0;

        while (position < content.Length)
        {
            var length = Math.Min(maxChunkSize, content.Length - position);
            var chunk = content.Substring(position, length);

            // Try to break at sentence boundary
            if (position + length < content.Length)
            {
                var lastPeriod = chunk.LastIndexOf(". ", StringComparison.Ordinal);
                if (lastPeriod > maxChunkSize / 2)
                {
                    chunk = chunk[..(lastPeriod + 1)];
                    length = lastPeriod + 1;
                }
            }

            chunks.Add(chunk);

            // Advance position, ensuring we always move forward
            var advance = length - overlap;
            if (advance <= 0) advance = length;
            position += advance;
        }

        return chunks;
    }
}
