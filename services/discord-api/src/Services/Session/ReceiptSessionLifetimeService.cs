public sealed class ReceiptSessionLifetimeService
{
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptSessionLockManager _lockManager;
    private readonly ReceiptMainMessageService _mainMessageService;
    private readonly ReceiptMainMessageDebounceService _debounceService;
    private readonly ReceiptPrivatePanelService _privatePanelService;
    private readonly ILogger<ReceiptSessionLifetimeService> _logger;

    public ReceiptSessionLifetimeService(
        ReceiptSessionStore sessionStore,
        ReceiptSessionLockManager lockManager,
        ReceiptMainMessageService mainMessageService,
        ReceiptMainMessageDebounceService debounceService,
        ReceiptPrivatePanelService privatePanelService,
        ILogger<ReceiptSessionLifetimeService> logger)
    {
        _sessionStore = sessionStore;
        _lockManager = lockManager;
        _mainMessageService = mainMessageService;
        _debounceService = debounceService;
        _privatePanelService = privatePanelService;
        _logger = logger;
    }

    public Task CompleteSessionAsync(ReceiptSessionState session, CancellationToken cancellationToken = default)
    {
        return CleanupSessionAsync(session, deleteMainMessage: false, cancellationToken);
    }

    public Task DiscardSessionAsync(ReceiptSessionState session, CancellationToken cancellationToken = default)
    {
        return CleanupSessionAsync(session, deleteMainMessage: true, cancellationToken);
    }

    private async Task CleanupSessionAsync(
        ReceiptSessionState session,
        bool deleteMainMessage,
        CancellationToken cancellationToken)
    {
        _debounceService.CancelRefresh(session.ReceiptId);
        await _privatePanelService.CloseAllPanelsAsync(session);

        if (deleteMainMessage)
        {
            try
            {
                await _mainMessageService.DeleteAsync(session, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Receipt main message cleanup failed. ReceiptId={ReceiptId}",
                    session.ReceiptId);
            }
        }

        _sessionStore.Remove(session.ReceiptId, out _);
        _lockManager.Cleanup(session.ReceiptId);

        if (!session.IsConfirmed)
        {
            if (session.IsDraftReady)
            {
                Telemetry.ActiveReceiptSessionsCounter.Add(-1);
            }
            else
            {
                Telemetry.ActivePendingUploadSessionsCounter.Add(-1);
            }
        }
    }
}
