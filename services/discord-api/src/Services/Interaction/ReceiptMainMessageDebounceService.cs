using System.Collections.Concurrent;

public sealed class ReceiptMainMessageDebounceService
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingRefreshes = new(StringComparer.Ordinal);
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptSessionLockManager _lockManager;
    private readonly ReceiptMainMessageService _mainMessageService;
    private readonly ILogger<ReceiptMainMessageDebounceService> _logger;

    public ReceiptMainMessageDebounceService(
        ReceiptSessionStore sessionStore,
        ReceiptSessionLockManager lockManager,
        ReceiptMainMessageService mainMessageService,
        ILogger<ReceiptMainMessageDebounceService> logger)
    {
        _sessionStore = sessionStore;
        _lockManager = lockManager;
        _mainMessageService = mainMessageService;
        _logger = logger;
    }

    public void ScheduleRefresh(string receiptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptId);

        var cancellationTokenSource = new CancellationTokenSource();
        if (_pendingRefreshes.TryRemove(receiptId, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        _pendingRefreshes[receiptId] = cancellationTokenSource;

        _ = RunDelayedRefreshAsync(receiptId, cancellationTokenSource);
    }

    public void CancelRefresh(string receiptId)
    {
        if (string.IsNullOrWhiteSpace(receiptId))
        {
            return;
        }

        if (_pendingRefreshes.TryRemove(receiptId, out var pending))
        {
            pending.Cancel();
            pending.Dispose();
        }
    }

    private async Task RunDelayedRefreshAsync(string receiptId, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await Task.Delay(RefreshDelay, cancellationTokenSource.Token);

            await _lockManager.ExecuteAsync(receiptId, async () =>
            {
                if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
                {
                    return;
                }

                if (session.MainChannelId is null || session.MainMessageId is null)
                {
                    return;
                }

                await _mainMessageService.RefreshAsync(session);
            }, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Debounced receipt main message refresh failed. ReceiptId={ReceiptId}", receiptId);
        }
        finally
        {
            if (_pendingRefreshes.TryGetValue(receiptId, out var current) && ReferenceEquals(current, cancellationTokenSource))
            {
                _pendingRefreshes.TryRemove(receiptId, out _);
            }

            cancellationTokenSource.Dispose();
        }
    }
}
