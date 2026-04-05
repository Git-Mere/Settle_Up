namespace receipt_parser.Services;

public sealed class ParserWarmupService : IHostedService
{
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(20);

    private readonly DocumentIntelligenceReceiptParser _parser;
    private readonly CosmosReceiptRepository _repository;
    private readonly ILogger<ParserWarmupService> _logger;

    public ParserWarmupService(
        DocumentIntelligenceReceiptParser parser,
        CosmosReceiptRepository repository,
        ILogger<ParserWarmupService> logger)
    {
        _parser = parser;
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(WarmupTimeout);

        try
        {
            _logger.LogInformation("Receipt parser warm-up started.");
            await _parser.WarmUpAsync(timeoutCts.Token);
            await _repository.EnsureReadyAsync(timeoutCts.Token);
            _logger.LogInformation("Receipt parser warm-up completed.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Receipt parser warm-up timed out after {TimeoutSeconds} seconds.", WarmupTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Receipt parser warm-up failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
