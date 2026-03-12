public sealed record OcrReceiptItem(
    string Id,
    string OriginalName,
    string NormalizedName,
    decimal? Price,
    decimal? Quantity,
    decimal? UnitPrice,
    int SourceIndex);
