using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using receipt_parser.Configuration;
using receipt_parser.Models;
using receipt_parser.Observability;

namespace receipt_parser.Services;

public sealed class CosmosReceiptRepository
{
    private readonly ILogger<CosmosReceiptRepository> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly ReceiptParserOptions _options;

    public CosmosReceiptRepository(
        IOptions<ReceiptParserOptions> options,
        ILogger<CosmosReceiptRepository> logger)
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
                "ReceiptParser:CosmosConnectionString 또는 ReceiptParser:CosmosAccountEndpoint 설정이 필요합니다.");
        }

        _cosmosClient = new CosmosClient(
            accountEndpoint: _options.CosmosAccountEndpoint,
            tokenCredential: new DefaultAzureCredential());
    }

    public async Task SaveAsync(ReceiptDocument document, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt.cosmos.save");
        activity?.SetTag("receipt.id", document.Id);

        var database = _cosmosClient.GetDatabase(_options.CosmosDatabaseId);
        var containerResponse = await database.CreateContainerIfNotExistsAsync(
            id: _options.CosmosContainerId,
            partitionKeyPath: "/Id",
            cancellationToken: cancellationToken);

        var container = containerResponse.Container;
        await container.UpsertItemAsync(
            item: document,
            partitionKey: new PartitionKey(document.Id),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Cosmos 저장 완료. ReceiptId={ReceiptId}", document.Id);
    }
}
