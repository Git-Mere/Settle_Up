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

    public async Task<IReadOnlyList<ConfirmedSettlementHistoryDocument>> GetRecentForUserAsync(
        string uploadedByUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedByUserId);

        var container = await GetOrCreateContainerAsync(cancellationToken);
        var safeTake = Math.Clamp(take, 1, 100);
        var query = new QueryDefinition(
            $"SELECT TOP {safeTake} * FROM c WHERE c.uploadedByUserId = @userId ORDER BY c.confirmedAtUtc DESC")
            .WithParameter("@userId", uploadedByUserId);

        var iterator = container.GetItemQueryIterator<ConfirmedSettlementHistoryDocument>(
            queryDefinition: query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(uploadedByUserId)
            });

        var results = new List<ConfirmedSettlementHistoryDocument>();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                results.Add(item);
            }
        }

        return results;
    }

    public async Task<ConfirmedSettlementHistoryDocument?> GetByRecencyIndexForUserAsync(
        string uploadedByUserId,
        int index,
        CancellationToken cancellationToken = default)
    {
        var histories = await GetRecentForUserAsync(uploadedByUserId, index, cancellationToken);
        if (histories.Count < index)
        {
            return null;
        }

        return histories[index - 1];
    }

    private async Task<Container> GetOrCreateContainerAsync(CancellationToken cancellationToken)
    {
        var database = _cosmosClient.GetDatabase(_options.CosmosDatabaseId);
        var containerResponse = await database.CreateContainerIfNotExistsAsync(
            id: _options.CosmosContainerId,
            partitionKeyPath: "/uploadedByUserId",
            cancellationToken: cancellationToken);

        return containerResponse.Container;
    }
}
