namespace receipt_parser.Models;

public sealed record ParsedReceiptResult(
    string ReceiptId,
    string BlobUrl,
    string? MerchantName,
    string? Currency,
    DateOnly? TransactionDate,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Sst,
    decimal? Slt,
    decimal? Tip,
    decimal? UnattributedDiscount,
    decimal? Total,
    ParseMetadata ParseMetadata,
    IReadOnlyList<ParsedReceiptItem> Items);
