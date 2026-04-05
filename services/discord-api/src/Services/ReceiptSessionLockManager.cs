using System.Collections.Concurrent;
using System.Threading;

public sealed class ReceiptSessionLockManager
{
    private readonly ConcurrentDictionary<string, LockEntry> _locks = new(StringComparer.Ordinal);

    public async Task ExecuteAsync(string lockKey, Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentNullException.ThrowIfNull(operation);

        var entry = _locks.GetOrAdd(lockKey, static _ => new LockEntry());
        Interlocked.Increment(ref entry.ReferenceCount);
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            entry.Gate.Release();
            ReleaseReference(lockKey, entry);
        }
    }

    public async Task<T> ExecuteAsync<T>(string lockKey, Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentNullException.ThrowIfNull(operation);

        var entry = _locks.GetOrAdd(lockKey, static _ => new LockEntry());
        Interlocked.Increment(ref entry.ReferenceCount);
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            entry.Gate.Release();
            ReleaseReference(lockKey, entry);
        }
    }

    public void Cleanup(string lockKey)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
        {
            return;
        }

        if (!_locks.TryGetValue(lockKey, out var entry))
        {
            return;
        }

        Volatile.Write(ref entry.CleanupRequested, true);
        TryRemoveEntry(lockKey, entry);
    }

    private void ReleaseReference(string lockKey, LockEntry entry)
    {
        if (Interlocked.Decrement(ref entry.ReferenceCount) < 0)
        {
            throw new InvalidOperationException("Receipt session lock reference count dropped below zero.");
        }

        TryRemoveEntry(lockKey, entry);
    }

    private void TryRemoveEntry(string lockKey, LockEntry entry)
    {
        if (!Volatile.Read(ref entry.CleanupRequested))
        {
            return;
        }

        if (Volatile.Read(ref entry.ReferenceCount) != 0)
        {
            return;
        }

        if (_locks.TryGetValue(lockKey, out var current) &&
            ReferenceEquals(current, entry) &&
            _locks.TryRemove(lockKey, out var removed))
        {
            removed.Gate.Dispose();
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int ReferenceCount;
        public bool CleanupRequested;
    }
}
