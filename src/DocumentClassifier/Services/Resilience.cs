namespace DocumentClassifier.Services;

public class ResilienceOptions
{
    public int MaxRetries { get; set; } = 3;
    public int BaseDelayMs { get; set; } = 250;
    public int MaxDelayMs { get; set; } = 2000;
    public int OperationTimeoutSeconds { get; set; } = 90;
}

public static class Resilience
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> action,
        ILogger logger,
        string operation,
        ResilienceOptions options,
        CancellationToken ct)
    {
        Exception? last = null;

        for (var attempt = 1; attempt <= options.MaxRetries; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.OperationTimeoutSeconds));

            try
            {
                return await action(timeoutCts.Token);
            }
            catch (Exception ex) when (attempt < options.MaxRetries)
            {
                last = ex;
                var delay = Math.Min(options.MaxDelayMs, options.BaseDelayMs * (int)Math.Pow(2, attempt - 1));
                logger.LogWarning(ex, "{Operation} failed on attempt {Attempt}/{Max}. Retrying in {DelayMs} ms.", operation, attempt, options.MaxRetries, delay);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                last = ex;
                break;
            }
        }

        throw new InvalidOperationException($"{operation} failed after {options.MaxRetries} attempts.", last);
    }
}
