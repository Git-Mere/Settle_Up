using Discord.WebSocket;

public sealed class SettlementHistoryPersistenceService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1500)
    ];

    private readonly SettlementHistoryRepositoryProvider _settlementHistoryRepositoryProvider;
    private readonly ILogger<SettlementHistoryPersistenceService> _logger;

    public SettlementHistoryPersistenceService(
        SettlementHistoryRepositoryProvider settlementHistoryRepositoryProvider,
        ILogger<SettlementHistoryPersistenceService> logger)
    {
        _settlementHistoryRepositoryProvider = settlementHistoryRepositoryProvider;
        _logger = logger;
    }

    public Task SaveInBackgroundAsync(
        SocketMessageComponent component,
        ConfirmedSettlementHistoryDocument historyDocument,
        string failureMessage)
    {
        return SaveInternalAsync(component, historyDocument, failureMessage);
    }

    private async Task SaveInternalAsync(
        SocketMessageComponent component,
        ConfirmedSettlementHistoryDocument historyDocument,
        string failureMessage)
    {
        var repository = _settlementHistoryRepositoryProvider.Repository;
        if (repository is null)
        {
            return;
        }

        Exception? lastException = null;
        for (var attempt = 1; attempt <= RetryDelays.Length + 1; attempt++)
        {
            try
            {
                await repository.SaveAsync(historyDocument);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Settlement history save failed. ReceiptId={ReceiptId} Attempt={Attempt}",
                    historyDocument.ReceiptId,
                    attempt);

                if (attempt > RetryDelays.Length)
                {
                    break;
                }

                await Task.Delay(RetryDelays[attempt - 1]);
            }
        }

        _logger.LogError(
            lastException,
            "Settlement history save exhausted retries. ReceiptId={ReceiptId} HistoryId={HistoryId}",
            historyDocument.ReceiptId,
            historyDocument.Id);

        try
        {
            await component.FollowupAsync(failureMessage, ephemeral: true);
        }
        catch (Exception followupEx)
        {
            _logger.LogWarning(
                followupEx,
                "Failed to send settlement history failure followup. ReceiptId={ReceiptId}",
                historyDocument.ReceiptId);
        }
    }
}
