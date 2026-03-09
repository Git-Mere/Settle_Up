namespace receipt_parser.Models;

public sealed record ReceiptParsedEventPayload(
    string Id,
    string BlobUrl,
    string Status,
    string? UploadedByUserId,
    string? MerchantName,
    DateOnly? TransactionDate,
    string? Currency,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Total,
    IReadOnlyList<ParsedReceiptItem> Items,
    ParseMetadata ParseMetadata,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
