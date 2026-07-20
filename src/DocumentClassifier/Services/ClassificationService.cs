using System.Linq;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using DocumentClassifier.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocumentClassifier.Services;

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
}

public interface IClassificationService
{
    Task<ClassificationResult> ClassifyAsync(string text, ClassificationProfile profile, List<ClassificationExample>? examples = null, CancellationToken ct = default);
}

public class ClassificationService : IClassificationService
{
    private readonly ChatClient? _chatClient;
    private readonly ResilienceOptions _resilienceOptions;
    private readonly ILogger<ClassificationService> _logger;
    private readonly bool _isConfigured;

    public ClassificationService(
        IOptions<AzureOpenAIOptions> options,
        IOptions<ResilienceOptions> resilienceOptions,
        TokenCredential credential,
        ILogger<ClassificationService> logger)
    {
        _logger = logger;
        _resilienceOptions = resilienceOptions.Value;
        var opts = options.Value;

        _isConfigured = !string.IsNullOrWhiteSpace(opts.Endpoint) && !string.IsNullOrWhiteSpace(opts.DeploymentName);
        if (!_isConfigured)
        {
            _logger.LogWarning("Azure OpenAI is not configured. Classification will use local heuristic fallback.");
            return;
        }

        AzureOpenAIClient aoaiClient;
        if (!string.IsNullOrEmpty(opts.ApiKey))
        {
            aoaiClient = new AzureOpenAIClient(new Uri(opts.Endpoint), new Azure.AzureKeyCredential(opts.ApiKey));
        }
        else
        {
            aoaiClient = new AzureOpenAIClient(new Uri(opts.Endpoint), credential);
        }

        _chatClient = aoaiClient.GetChatClient(opts.DeploymentName);
    }

    public async Task<ClassificationResult> ClassifyAsync(string text, ClassificationProfile profile, List<ClassificationExample>? examples = null, CancellationToken ct = default)
    {
        if (!_isConfigured || _chatClient is null)
            return ClassifyWithHeuristic(text, profile);

        try
        {
            _logger.LogInformation("Classifying document with profile {Profile} ({ExampleCount} few-shot examples)", profile.Name, examples?.Count ?? 0);

            var systemMessage = BuildSystemMessage(profile, examples);
            var userMessage = $"Classify the following document:\n\n---\n{text}\n---";

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "classification_result",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "category": { "type": "string" },
                            "confidence": { "type": "number" },
                            "reasoning": { "type": "string" },
                            "metadata": { "type": "object", "additionalProperties": { "type": "string" } }
                        },
                        "required": ["category", "confidence", "reasoning"],
                        "additionalProperties": false
                    }
                    """))
            };

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(userMessage)
            };

            var completion = await Resilience.ExecuteWithRetryAsync(
                retryCt => _chatClient.CompleteChatAsync(messages, options, retryCt),
                _logger,
                "Classification chat completion",
                _resilienceOptions,
                ct);
            var responseText = completion.Value.Content[0].Text;

            var parsed = JsonSerializer.Deserialize<JsonElement>(responseText);
            var confidence = parsed.GetProperty("confidence").GetDouble();
            var category = parsed.GetProperty("category").GetString() ?? "unknown";

            _logger.LogInformation("Classification complete for profile {Profile}: {Category} ({Confidence:P0})", profile.Name, category, confidence);

            // Guard against NaN/Infinity which would deadlock MAF edge routing
            if (!double.IsFinite(confidence))
            {
                _logger.LogError("Classification returned invalid confidence ({RawConfidence}) for profile {Profile}. Raw response: {Response}",
                    confidence, profile.Name, responseText);
                throw new InvalidOperationException($"Classification returned invalid confidence value for profile '{profile.Name}'");
            }
            // Normalize percentage values (e.g., 95 → 0.95)
            if (confidence > 1.0)
                confidence /= 100.0;

            return new ClassificationResult
            {
                ProfileName = profile.Name,
                Category = category,
                Confidence = confidence,
                Reasoning = parsed.GetProperty("reasoning").GetString() ?? string.Empty,
                Metadata = parsed.TryGetProperty("metadata", out var meta)
                    ? FlattenMetadata(meta)
                    : new(),
                ClassifiedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI classification failed for profile {Profile}. Falling back to heuristic classification.", profile.Name);
            return ClassifyWithHeuristic(text, profile) with
            {
                Reasoning = "Azure OpenAI classification failed; used local keyword-based fallback classifier."
            };
        }
    }

    private ClassificationResult ClassifyWithHeuristic(string text, ClassificationProfile profile)
    {
        var normalized = text.ToLowerInvariant();
        var reliefSignals = new[] { "request", "requests", "asks", "ask", "grant", "order", "dismiss", "extension", "relief", "motion" };
        var reliefScore = reliefSignals.Count(token => normalized.Contains(token, StringComparison.Ordinal));

        var asksCategory = profile.Categories.FirstOrDefault(c => c.Contains("ask", StringComparison.OrdinalIgnoreCase) || c.Contains("relief", StringComparison.OrdinalIgnoreCase));
        var noReliefCategory = profile.Categories.FirstOrDefault(c => c.Contains("no", StringComparison.OrdinalIgnoreCase) || c.Contains("not", StringComparison.OrdinalIgnoreCase));

        var category = reliefScore > 0
            ? asksCategory ?? profile.Categories.FirstOrDefault() ?? "unclassified"
            : noReliefCategory ?? profile.Categories.Skip(1).FirstOrDefault() ?? profile.Categories.FirstOrDefault() ?? "unclassified";

        var confidence = reliefScore > 0 ? 0.72 : 0.68;
        _logger.LogInformation("Heuristic classification used for profile {Profile}: {Category} ({Confidence:P0})", profile.Name, category, confidence);

        return new ClassificationResult
        {
            ProfileName = profile.Name,
            Category = category,
            Confidence = confidence,
            Reasoning = "Azure OpenAI is not configured; used local keyword-based fallback classifier.",
            Metadata = new()
            {
                ["fallback"] = "heuristic",
                ["relief_signal_count"] = reliefScore.ToString()
            },
            ClassifiedAt = DateTimeOffset.UtcNow
        };
    }

    private static Dictionary<string, string> FlattenMetadata(JsonElement meta)
    {
        var result = new Dictionary<string, string>();
        foreach (var prop in meta.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? "",
                JsonValueKind.Array => string.Join(", ", prop.Value.EnumerateArray().Select(e => e.ToString())),
                _ => prop.Value.GetRawText()
            };
        }
        return result;
    }

    private static string BuildSystemMessage(ClassificationProfile profile, List<ClassificationExample>? examples)
    {
        var categories = string.Join(", ", profile.Categories.Select(c => $"\"{c}\""));
        var examplesSection = "";
        var profileExamplesSection = "";

        if (examples is { Count: > 0 })
        {
            var exampleLines = examples.Select(ex =>
                $"- Document snippet: \"{ex.Snippet[..Math.Min(ex.Snippet.Length, 150)]}...\"\n  Category: {ex.Category}\n  Reasoning: {ex.Reasoning}");
            examplesSection = $"\n\nHere are examples of previously classified documents:\n{string.Join("\n\n", exampleLines)}\n";
        }

        if (profile.CategoryExamples.Count > 0)
        {
            var categoryExampleLines = profile.CategoryExamples.Select(kvp =>
                $"- {kvp.Key}: {string.Join(" | ", kvp.Value.Select(v => $"\"{v}\""))}");
            profileExamplesSection = $"\n\nHere are curated examples for each category:\n{string.Join("\n", categoryExampleLines)}\n";
        }

        return $"""
            {profile.SystemPrompt}

            You must classify the document into exactly one of these categories: [{categories}]
            {profileExamplesSection}
            {examplesSection}
            {profile.OutputInstructions ?? ""}

            Respond with a JSON object containing:
            - "category": one of the allowed categories
            - "confidence": a number between 0 and 1
            - "reasoning": brief explanation of why this category was chosen
            - "metadata": optional object with any additional extracted information
            """;
    }
}
