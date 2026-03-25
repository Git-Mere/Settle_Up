using System.Text.Json.Serialization;

public sealed class ReceiptDraftNotificationRequest
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("draftId")]
    public string? DraftId { get; init; }

    public string? BlobUrl { get; init; }
    public string? Status { get; init; }
    public string? UploadedByUserId { get; init; }
    public string? MerchantName { get; init; }
    public DateOnly? TransactionDate { get; init; }
    public string? Currency { get; init; }
    public decimal? Subtotal { get; init; }
    public decimal? Tax { get; init; }
    public decimal? Total { get; init; }
    public IReadOnlyList<ReceiptDraftNotificationItem>? Items { get; init; }
    public ReceiptDraftParseMetadata? ParseMetadata { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }

    [JsonIgnore]
    public string? ResolvedDraftId => string.IsNullOrWhiteSpace(DraftId) ? Id : DraftId;
}

public sealed record ReceiptDraftNotificationItem(
    string Id,
    string? Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TotalPrice);

public sealed record ReceiptDraftParseMetadata(
    string ModelId,
    decimal? MerchantConfidence,
    decimal? TotalConfidence);
