namespace DocumentClassifier.Models;

public record DocumentRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string FileName { get; init; } = string.Empty;
    public string? BlobUri { get; init; }
    public string? LocalPath { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ExtractedText { get; init; }
    public ClassificationResult? Classification { get; init; }
    public DocumentStatus Status { get; init; } = DocumentStatus.Uploaded;
}

public enum DocumentStatus
{
    Uploaded,
    Extracting,
    Extracted,
    Classifying,
    Classified,
    NeedsReview,
    Failed
}

public record ClassificationResult
{
    public string ProfileName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string Reasoning { get; init; } = string.Empty;
    public Dictionary<string, string> Metadata { get; init; } = new();
    public DateTimeOffset ClassifiedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record ClassificationProfile
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
    public List<string> Categories { get; init; } = new();
    public Dictionary<string, List<string>> CategoryExamples { get; init; } = new();
    public string? OutputInstructions { get; init; }
}
