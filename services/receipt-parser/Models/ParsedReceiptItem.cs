namespace receipt_parser.Models;

public sealed record ParsedReceiptItem(
    string Id,
    string? Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TotalPrice,
    decimal? OriginalUnitPrice = null,
    decimal? OriginalTotalPrice = null,
    decimal? DiscountAmount = null);
