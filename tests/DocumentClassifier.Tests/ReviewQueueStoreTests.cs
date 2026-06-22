using DocumentClassifier.Services;
using Xunit;

namespace DocumentClassifier.Tests;

public class ReviewQueueStoreTests
{
    [Fact]
    public async Task EnqueueAndReadBack_PersistsItem()
    {
        var store = new FileBackedReviewQueueStore();
        var item = new ReviewQueueItem
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            FileName = "test.pdf",
            ProfileName = "relief_request_binary",
            Category = "asks_for_relief",
            Confidence = 0.42,
            Reasoning = "Low confidence example"
        };

        await store.EnqueueAsync(item);
        var all = await store.GetAllAsync();

        Assert.Contains(all, x => x.DocumentId == item.DocumentId);
    }

    [Fact]
    public async Task UpdateStatus_WhenItemExists_UpdatesStatus()
    {
        var store = new FileBackedReviewQueueStore();
        var id = Guid.NewGuid().ToString("N");

        await store.EnqueueAsync(new ReviewQueueItem
        {
            DocumentId = id,
            FileName = "test.pdf",
            ProfileName = "relief_request_binary",
            Category = "asks_for_relief",
            Confidence = 0.35,
            Reasoning = "Needs review"
        });

        var updated = await store.UpdateStatusAsync(id, "approved");
        var all = await store.GetAllAsync();
        var match = all.First(x => x.DocumentId == id);

        Assert.True(updated);
        Assert.Equal("approved", match.Status, ignoreCase: true);
        Assert.NotNull(match.UpdatedAt);
    }
}
