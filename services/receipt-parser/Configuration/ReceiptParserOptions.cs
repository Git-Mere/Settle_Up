using Microsoft.Extensions.Configuration;

namespace receipt_parser.Configuration;

public sealed class ReceiptParserOptions
{
    public const string SectionName = "ReceiptParser";

    public string? DocumentIntelligenceEndpoint { get; init; }
    public string? DocumentIntelligenceApiKey { get; init; }
    public string ModelId { get; init; } = "prebuilt-receipt";

    public string? CosmosConnectionString { get; init; }
    public string? CosmosAccountEndpoint { get; init; }
    public string CosmosDatabaseId { get; init; } = "draft-receipt-db";
    public string CosmosContainerId { get; init; } = "draft-receipt";
    public string? DiscordApiUrl { get; init; }

    [ConfigurationKeyName("DiscordApiUrl_local_test")]
    public string? DiscordApiUrlLocalTest { get; init; }

    public bool EnableLocalUploadTestEndpoint { get; init; } = false;
}
