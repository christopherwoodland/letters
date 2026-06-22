using System.Text.Json;

namespace DocumentClassifier.Services;

public record ReviewQueueItem
{
    public string DocumentId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ProfileName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string Reasoning { get; init; } = string.Empty;
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; init; } = "pending";
    public DateTimeOffset? UpdatedAt { get; init; }
}

public interface IReviewQueueStore
{
    Task EnqueueAsync(ReviewQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewQueueItem>> GetAllAsync(CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(string documentId, string status, CancellationToken ct = default);
}

public class FileBackedReviewQueueStore : IReviewQueueStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileBackedReviewQueueStore()
    {
        var appData = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(appData);
        _path = Path.Combine(appData, "review-queue.json");
    }

    public async Task EnqueueAsync(ReviewQueueItem item, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAllInternalAsync(ct)).ToList();
            list.Add(item);
            await WriteAllInternalAsync(list, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ReviewQueueItem>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await ReadAllInternalAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> UpdateStatusAsync(string documentId, string status, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var list = (await ReadAllInternalAsync(ct)).ToList();
            var index = list.FindIndex(i => i.DocumentId.Equals(documentId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;

            list[index] = list[index] with
            {
                Status = status,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await WriteAllInternalAsync(list, ct);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<ReviewQueueItem>> ReadAllInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
            return Array.Empty<ReviewQueueItem>();

        await using var fs = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<ReviewQueueItem>>(fs, JsonOptions, ct)
            ?? new List<ReviewQueueItem>();
    }

    private async Task WriteAllInternalAsync(List<ReviewQueueItem> items, CancellationToken ct)
    {
        await using var fs = File.Create(_path);
        await JsonSerializer.SerializeAsync(fs, items, JsonOptions, ct);
    }
}
