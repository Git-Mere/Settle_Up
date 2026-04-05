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
    private readonly Lazy<Task<Container>> _containerTask;

    public CosmosReceiptRepository(
        IOptions<ReceiptParserOptions> options,
        ILogger<CosmosReceiptRepository> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.CosmosConnectionString))
        {
            _cosmosClient = new CosmosClient(_options.CosmosConnectionString);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_options.CosmosAccountEndpoint))
            {
                throw new InvalidOperationException(
                    "ReceiptParser:CosmosConnectionString 또는 ReceiptParser:CosmosAccountEndpoint 설정이 필요합니다.");
            }

            _cosmosClient = new CosmosClient(
                accountEndpoint: _options.CosmosAccountEndpoint,
                tokenCredential: new DefaultAzureCredential());
        }

        _containerTask = new Lazy<Task<Container>>(
            () => InitializeContainerAsync(CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task SaveAsync(ReceiptDocument document, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.cosmos.upsert");
        activity?.SetTag("receipt.id", document.Id);
        _logger.LogInformation(
            "Cosmos write started. ReceiptId={ReceiptId} DatabaseId={DatabaseId} ContainerId={ContainerId}",
            document.Id,
            _options.CosmosDatabaseId,
            _options.CosmosContainerId);

        try
        {
            var container = await GetContainerAsync(cancellationToken);
            await container.UpsertItemAsync(
                item: document,
                partitionKey: new PartitionKey(document.Id),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Cosmos write failed. ReceiptId={ReceiptId}", document.Id);
            throw;
        }

        _logger.LogInformation("Cosmos write completed. ReceiptId={ReceiptId}", document.Id);
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Cosmos repository warm-up started. DatabaseId={DatabaseId} ContainerId={ContainerId}",
            _options.CosmosDatabaseId,
            _options.CosmosContainerId);

        await GetContainerAsync(cancellationToken);

        _logger.LogInformation(
            "Cosmos repository warm-up completed. DatabaseId={DatabaseId} ContainerId={ContainerId}",
            _options.CosmosDatabaseId,
            _options.CosmosContainerId);
    }

    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken == CancellationToken.None)
        {
            return await _containerTask.Value;
        }

        if (_containerTask.IsValueCreated)
        {
            return await _containerTask.Value.WaitAsync(cancellationToken);
        }

        return await InitializeContainerAsync(cancellationToken);
    }

    private async Task<Container> InitializeContainerAsync(CancellationToken cancellationToken)
    {
        var database = _cosmosClient.GetDatabase(_options.CosmosDatabaseId);
        var containerResponse = await database.CreateContainerIfNotExistsAsync(
            id: _options.CosmosContainerId,
            partitionKeyPath: "/Id",
            cancellationToken: cancellationToken);

        return containerResponse.Container;
    }
}
