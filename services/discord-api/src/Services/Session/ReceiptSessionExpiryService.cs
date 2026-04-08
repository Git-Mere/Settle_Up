sealed class ReceiptSessionExpiryService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PendingSessionTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ActiveSessionTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan UploadPromptTtl = TimeSpan.FromMinutes(15);

    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptSessionLockManager _lockManager;
    private readonly ReceiptSessionLifetimeService _sessionLifetimeService;
    private readonly SettleUpCommandHandler _settleUpCommandHandler;
    private readonly ILogger<ReceiptSessionExpiryService> _logger;

    public ReceiptSessionExpiryService(
        ReceiptSessionStore sessionStore,
        ReceiptSessionLockManager lockManager,
        ReceiptSessionLifetimeService sessionLifetimeService,
        SettleUpCommandHandler settleUpCommandHandler,
        ILogger<ReceiptSessionExpiryService> logger)
    {
        _sessionStore = sessionStore;
        _lockManager = lockManager;
        _sessionLifetimeService = sessionLifetimeService;
        _settleUpCommandHandler = settleUpCommandHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredStateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Receipt session expiry sweep failed.");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CleanupExpiredStateAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        await _settleUpCommandHandler.CleanupExpiredUploadPromptsAsync(now - UploadPromptTtl);

        var sessions = _sessionStore.GetAll();
        foreach (var session in sessions)
        {
            var ttl = GetSessionTtl(session);
            if (ttl is null)
            {
                continue;
            }

            if (session.UpdatedAtUtc > now - ttl.Value)
            {
                continue;
            }

            await CleanupExpiredSessionAsync(session.ReceiptId, now, ttl.Value, cancellationToken);
        }
    }

    private async Task CleanupExpiredSessionAsync(
        string receiptId,
        DateTimeOffset now,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                return;
            }

            if (session.UpdatedAtUtc > now - ttl)
            {
                return;
            }

            await _sessionLifetimeService.DiscardSessionAsync(session, cancellationToken);

            _logger.LogInformation(
                "Expired receipt session cleaned up. ReceiptId={ReceiptId} DraftReady={IsDraftReady} AgeMinutes={AgeMinutes}",
                receiptId,
                session.IsDraftReady,
                (now - session.UpdatedAtUtc).TotalMinutes);
        }, cancellationToken);
    }

    private static TimeSpan? GetSessionTtl(ReceiptSessionState session)
    {
        if (session.IsConfirmed)
        {
            return null;
        }

        return session.IsDraftReady ? ActiveSessionTtl : PendingSessionTtl;
    }
}
