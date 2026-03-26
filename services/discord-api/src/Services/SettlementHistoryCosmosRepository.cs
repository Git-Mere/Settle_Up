using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

public sealed class SettlementHistoryCosmosRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly SettlementHistoryOptions _options;
    private readonly ILogger<SettlementHistoryCosmosRepository> _logger;

    public SettlementHistoryCosmosRepository(
        IOptions<SettlementHistoryOptions> options,
        ILogger<SettlementHistoryCosmosRepository> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.CosmosConnectionString))
        {
            _cosmosClient = new CosmosClient(_options.CosmosConnectionString);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.CosmosAccountEndpoint))
        {
            throw new InvalidOperationException(
                "SettlementHistory:CosmosConnectionString 또는 SettlementHistory:CosmosAccountEndpoint 설정이 필요합니다.");
        }

        _cosmosClient = new CosmosClient(
            accountEndpoint: _options.CosmosAccountEndpoint,
            tokenCredential: new DefaultAzureCredential());
    }

    public async Task SaveAsync(ConfirmedSettlementHistoryDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var database = _cosmosClient.GetDatabase(_options.CosmosDatabaseId);
        _logger.LogInformation(
            "Settlement history write started. ReceiptId={ReceiptId} DatabaseId={DatabaseId} ContainerId={ContainerId}",
            document.ReceiptId,
            _options.CosmosDatabaseId,
            _options.CosmosContainerId);

        var containerResponse = await database.CreateContainerIfNotExistsAsync(
            id: _options.CosmosContainerId,
            partitionKeyPath: "/uploadedByUserId",
            cancellationToken: cancellationToken);

        await containerResponse.Container.UpsertItemAsync(
            item: document,
            partitionKey: new PartitionKey(document.UploadedByUserId),
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Settlement history write completed. ReceiptId={ReceiptId} HistoryId={HistoryId}",
            document.ReceiptId,
            document.Id);
    }
}
