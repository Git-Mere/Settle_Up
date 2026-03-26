using Microsoft.Extensions.Configuration;

public sealed class SettlementHistoryOptions
{
    public const string SectionName = "SettlementHistory";

    public string? CosmosConnectionString { get; init; }
    public string? CosmosAccountEndpoint { get; init; }
    public string CosmosDatabaseId { get; init; } = "draft-receipt-db";
    public string CosmosContainerId { get; init; } = "settlement-history";
}
