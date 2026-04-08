public sealed class SettlementHistoryRepositoryProvider
{
    public SettlementHistoryRepositoryProvider(
        IConfiguration configuration,
        ILogger<SettlementHistoryRepositoryProvider> logger,
        ILoggerFactory loggerFactory)
    {
        try
        {
            var options = configuration.GetSection(SettlementHistoryOptions.SectionName).Get<SettlementHistoryOptions>()
                ?? new SettlementHistoryOptions();

            if (string.IsNullOrWhiteSpace(options.CosmosConnectionString) &&
                string.IsNullOrWhiteSpace(options.CosmosAccountEndpoint))
            {
                InitializationError = "Cosmos history storage is not configured.";
                logger.LogWarning("Settlement history storage is disabled. Reason={Reason}", InitializationError);
                return;
            }

            Repository = new SettlementHistoryCosmosRepository(
                Microsoft.Extensions.Options.Options.Create(options),
                loggerFactory.CreateLogger<SettlementHistoryCosmosRepository>());

            logger.LogInformation("Settlement history storage is enabled.");
        }
        catch (Exception ex)
        {
            InitializationError = ex.Message;
            logger.LogWarning(ex, "Settlement history storage is disabled. Reason={Reason}", InitializationError);
        }
    }

    public SettlementHistoryCosmosRepository? Repository { get; }
    public string? InitializationError { get; }
}
