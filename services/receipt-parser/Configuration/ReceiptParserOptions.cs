namespace receipt_parser.Configuration;

public sealed class ReceiptParserOptions
{
    public const string SectionName = "ReceiptParser";

    public string? DocumentIntelligenceEndpoint { get; init; }
    public string? DocumentIntelligenceApiKey { get; init; }
    public string ModelId { get; init; } = "prebuilt-receipt";

    public string? CosmosAccountEndpoint { get; init; }
    public string CosmosDatabaseId { get; init; } = "settle-up";
    public string CosmosContainerId { get; init; } = "receipts";

    public string? DownstreamEventGridTopicEndpoint { get; init; }
    public string? DownstreamEventGridTopicKey { get; init; }
    public string DownstreamEventType { get; init; } = "SettleUp.ReceiptParsed";

    public bool EnableLocalUploadTestEndpoint { get; init; } = false;
}
