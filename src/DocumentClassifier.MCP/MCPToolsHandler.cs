using System.Text.Json;
using DocumentClassifier.Models;
using DocumentClassifier.Services;
using Microsoft.Extensions.Logging;

namespace DocumentClassifier.MCP;

/// <summary>
/// Implements MCP tools for the document classification workflow.
/// Each tool returns a structured result that can be serialized for MCP clients.
/// </summary>
public class MCPToolsHandler
{
    private readonly ISearchIndexingService _search;
    private readonly IClassificationService _classification;
    private readonly IRagService _rag;
    private readonly IProfileStore _profiles;
    private readonly IReviewQueueStore _reviewQueue;
    private readonly ILogger<MCPToolsHandler> _logger;

    public MCPToolsHandler(
        ISearchIndexingService search,
        IClassificationService classification,
        IRagService rag,
        IProfileStore profiles,
        IReviewQueueStore reviewQueue,
        ILogger<MCPToolsHandler> logger)
    {
        _search = search;
        _classification = classification;
        _rag = rag;
        _profiles = profiles;
        _reviewQueue = reviewQueue;
        _logger = logger;
    }

    /// <summary>
    /// Lists all available classification profiles.
    /// </summary>
    public async Task<MCPToolResult> ListProfilesAsync(CancellationToken ct = default)
    {
        try
        {
            var profiles = _profiles.GetAll();
            return MCPToolResult.FromSuccess(
                new
                {
                    profiles = profiles.Select(p => new { p.Name, p.Description }).ToList()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing profiles");
            return MCPToolResult.FromError($"Failed to list profiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Classifies text using the specified profile.
    /// </summary>
    public async Task<MCPToolResult> ClassifyTextAsync(
        string text,
        string profileName,
        int exampleCount = 3,
        CancellationToken ct = default)
    {
        try
        {
            var profile = _profiles.GetProfile(profileName);
            if (profile == null)
                return MCPToolResult.FromError($"Profile '{profileName}' not found");

            // Get examples
            var examples = await _rag.GetClassificationExamplesAsync(text, profileName, exampleCount, ct);

            // Classify
            var result = await _classification.ClassifyAsync(text, profile, examples, ct);

            return MCPToolResult.FromSuccess(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying text with profile {Profile}", profileName);
            return MCPToolResult.FromError($"Classification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches indexed documents by query and optional category.
    /// </summary>
    public async Task<MCPToolResult> SearchDocumentsAsync(
        string query,
        string? category = null,
        int topResults = 5,
        CancellationToken ct = default)
    {
        try
        {
            var results = await _search.SearchSimilarAsync(query, category, topResults, ct);
            return MCPToolResult.FromSuccess(
                new
                {
                    count = results.Count,
                    results = results.Select(r => new
                    {
                        r.Id,
                        r.FileName,
                        r.Category,
                        r.Confidence,
                        ContentPreview = r.Content?.Length > 200 ? r.Content[..200] + "..." : r.Content
                    }).ToList()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return MCPToolResult.FromError($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves items in the review queue that didn't meet confidence threshold.
    /// </summary>
    public async Task<MCPToolResult> GetReviewQueueAsync(CancellationToken ct = default)
    {
        try
        {
            var items = await _reviewQueue.GetAllAsync(ct);
            return MCPToolResult.FromSuccess(
                new
                {
                    count = items.Count,
                    items = items.Select(i => new
                    {
                        i.DocumentId,
                        i.FileName,
                        i.ProfileName,
                        i.Category,
                        i.Confidence,
                        i.Reasoning,
                        i.QueuedAt,
                        i.Status,
                        i.UpdatedAt
                    }).ToList()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review queue");
            return MCPToolResult.FromError($"Failed to get review queue: {ex.Message}");
        }
    }
}

/// <summary>
/// Standardized result format for MCP tool responses.
/// </summary>
public class MCPToolResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }

    public static MCPToolResult FromSuccess(object data) =>
        new() { Success = true, Data = data };

    public static MCPToolResult FromError(string error) =>
        new() { Success = false, Error = error };

    public string ToJson() =>
        JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}
