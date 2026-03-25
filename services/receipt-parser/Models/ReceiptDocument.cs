using Newtonsoft.Json;

namespace receipt_parser.Models;

public sealed class ReceiptDocument
{
    [JsonProperty("id")]
    public string Id { get; init; } = default!;
    [JsonProperty("Id")]
    public string CosmosPartitionKey => Id;
    public string Status { get; init; } = "Parsed";
    public string BlobUrl { get; init; } = default!;
    public string? UploadedByUserId { get; init; }
    public string? MerchantName { get; init; }
    public DateOnly? TransactionDate { get; init; }
    public string? Currency { get; init; }
    public decimal? Subtotal { get; init; }
    public decimal? Tax { get; init; }
    public decimal? Total { get; init; }
    public List<ParsedReceiptItem> Items { get; init; } = [];
    public ParseMetadata ParseMetadata { get; init; } = new("prebuilt-receipt", null, null);
    public string NotificationStatus { get; init; } = NotificationStatuses.Pending;
    public int NotificationAttemptCount { get; init; }
    public DateTimeOffset? LastNotificationAttemptAt { get; init; }
    public DateTimeOffset? NotificationSentAtUtc { get; init; }
    public string? LastNotificationError { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
