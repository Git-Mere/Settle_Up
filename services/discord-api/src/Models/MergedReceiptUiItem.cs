public sealed record MergedReceiptUiItem(
    string Id,
    string DisplayName,
    string NormalizedName,
    int Quantity,
    decimal? TotalPrice,
    IReadOnlyList<string> SourceItemIds);
