sealed class BlobUploaderWarmupService : IHostedService
{
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(15);

    private readonly BlobUploaderProvider _blobUploaderProvider;
    private readonly ILogger<BlobUploaderWarmupService> _logger;

    public BlobUploaderWarmupService(
        BlobUploaderProvider blobUploaderProvider,
        ILogger<BlobUploaderWarmupService> logger)
    {
        _blobUploaderProvider = blobUploaderProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_blobUploaderProvider.Uploader is null)
        {
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(WarmupTimeout);

        try
        {
            _logger.LogInformation("Blob uploader warm-up started.");
            await _blobUploaderProvider.Uploader.EnsureReadyAsync(timeoutCts.Token);
            _logger.LogInformation("Blob uploader warm-up completed.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Blob uploader warm-up timed out after {TimeoutSeconds} seconds.", WarmupTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blob uploader warm-up failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
