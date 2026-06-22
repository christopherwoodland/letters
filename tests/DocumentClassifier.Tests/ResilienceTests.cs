using DocumentClassifier.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentClassifier.Tests;

public class ResilienceTests
{
    [Fact]
    public async Task ExecuteWithRetryAsync_RetriesThenSucceeds()
    {
        var options = new ResilienceOptions
        {
            MaxRetries = 3,
            BaseDelayMs = 1,
            MaxDelayMs = 2,
            OperationTimeoutSeconds = 5
        };

        var attempts = 0;
        var result = await Resilience.ExecuteWithRetryAsync(async _ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new InvalidOperationException("transient");
            }

            await Task.Yield();
            return "ok";
        }, NullLogger.Instance, "test-op", options, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ThrowsAfterMaxRetries()
    {
        var options = new ResilienceOptions
        {
            MaxRetries = 2,
            BaseDelayMs = 1,
            MaxDelayMs = 1,
            OperationTimeoutSeconds = 5
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Resilience.ExecuteWithRetryAsync<string>(_ => throw new Exception("boom"), NullLogger.Instance, "test-op", options, CancellationToken.None));
    }
}
