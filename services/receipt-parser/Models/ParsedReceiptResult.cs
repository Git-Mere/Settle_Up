namespace receipt_parser.Models;

public sealed record ParsedReceiptResult(
    string ReceiptId,
    string BlobUrl,
    string? MerchantName,
    string? Currency,
    DateOnly? TransactionDate,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Total,
    ParseMetadata ParseMetadata,
    string RawResultJson,
    IReadOnlyList<ParsedReceiptItem> Items);
