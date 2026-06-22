namespace DocumentClassifier.Models;

public record UploadDocumentRequest
{
    public string FileName { get; init; } = string.Empty;
}

public record ClassifyRequest
{
    public string ProfileName { get; init; } = "relief_request_binary";
}

public record ProcessDocumentRequest
{
    public string FileName { get; init; } = string.Empty;
    public string ProfileName { get; init; } = "relief_request_binary";
}

public record ProcessDocumentResponse
{
    public string DocumentId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ExtractedText { get; init; } = string.Empty;
    public ClassificationResult? Classification { get; init; }
    public DocumentStatus Status { get; init; }
}
