public static class ReceiptItemMergeService
{
    public static IReadOnlyList<OcrReceiptItem> BuildOcrItems(IReadOnlyList<ReceiptDraftNotificationItem>? receiptItems)
    {
        if (receiptItems is null || receiptItems.Count == 0)
        {
            return [];
        }

        var items = new List<OcrReceiptItem>(receiptItems.Count);
        for (var index = 0; index < receiptItems.Count; index++)
        {
            var receiptItem = receiptItems[index];
            var originalName = receiptItem.Description ?? string.Empty;
            var normalizedName = ReceiptItemNameNormalizer.Normalize(originalName);

            items.Add(new OcrReceiptItem(
                Id: string.IsNullOrWhiteSpace(receiptItem.Id) ? $"item-{index + 1}" : receiptItem.Id,
                OriginalName: originalName,
                NormalizedName: normalizedName,
                Price: receiptItem.TotalPrice ?? receiptItem.UnitPrice,
                Quantity: receiptItem.Quantity,
                UnitPrice: receiptItem.UnitPrice,
                SourceIndex: index));
        }

        return items;
    }

    public static IReadOnlyList<MergedReceiptUiItem> MergeForUi(IReadOnlyList<OcrReceiptItem> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var mergedItems = new List<MergedReceiptUiItem>();
        foreach (var group in items.GroupBy(GetMergeKey))
        {
            var groupedItems = group.ToList();
            var sourceItemIds = groupedItems.Select(item => item.Id).ToArray();
            var representative = groupedItems[0];
            var baseDisplayName = ReceiptItemNameNormalizer.CleanDisplayName(representative.OriginalName);
            var quantity = groupedItems.Count;
            var totalPrice = SumPrices(groupedItems);

            mergedItems.Add(new MergedReceiptUiItem(
                Id: BuildMergedItemId(group.Key, sourceItemIds),
                DisplayName: $"{baseDisplayName} ({quantity})",
                NormalizedName: representative.NormalizedName,
                Quantity: quantity,
                TotalPrice: totalPrice,
                SourceItemIds: sourceItemIds));
        }

        return mergedItems
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetMergeKey(OcrReceiptItem item)
    {
        return string.IsNullOrWhiteSpace(item.NormalizedName)
            ? $"__unmerged__:{item.Id}"
            : item.NormalizedName;
    }

    private static decimal? SumPrices(IReadOnlyList<OcrReceiptItem> items)
    {
        decimal total = 0;
        var hasPrice = false;

        foreach (var item in items)
        {
            if (item.Price is null)
            {
                continue;
            }

            total += item.Price.Value;
            hasPrice = true;
        }

        return hasPrice ? total : null;
    }

    private static string BuildMergedItemId(string mergeKey, IReadOnlyList<string> sourceItemIds)
    {
        var sanitizedKey = new string(
            mergeKey
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
                .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(sanitizedKey))
        {
            sanitizedKey = "merged-item";
        }

        return $"{sanitizedKey}-{sourceItemIds.Count}-{sourceItemIds[0]}";
    }
}
