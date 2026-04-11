using Discord.WebSocket;
using System.Diagnostics;

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
        var startedAt = Stopwatch.GetTimestamp();
        for (var attempt = 1; attempt <= RetryDelays.Length + 1; attempt++)
        {
            try
            {
                await repository.SaveAsync(historyDocument);
                var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                Telemetry.HistorySaveDurationMs.Record(durationMs);
                _logger.LogInformation(
                    "Settlement history saved. ReceiptId={ReceiptId} HistoryId={HistoryId} Attempt={Attempt} DurationMs={DurationMs}",
                    historyDocument.ReceiptId,
                    historyDocument.Id,
                    attempt,
                    durationMs);
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

        Telemetry.HistorySaveFailedCounter.Add(1);
        Telemetry.HistorySaveDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
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
