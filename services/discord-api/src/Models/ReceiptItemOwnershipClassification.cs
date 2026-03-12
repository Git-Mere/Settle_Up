public sealed record ReceiptItemOwnershipClassification(
    IReadOnlyList<MergedReceiptUiItem> SharedItems,
    IReadOnlyList<MergedReceiptUiItem> IndividualItems,
    IReadOnlyList<MergedReceiptUiItem> UnassignedItems);
