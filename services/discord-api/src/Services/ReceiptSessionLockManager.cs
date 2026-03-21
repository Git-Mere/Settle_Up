using System.Collections.Concurrent;

public sealed class ReceiptSessionLockManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task ExecuteAsync(string lockKey, Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentNullException.ThrowIfNull(operation);

        var gate = _locks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> ExecuteAsync<T>(string lockKey, Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentNullException.ThrowIfNull(operation);

        var gate = _locks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }
}
