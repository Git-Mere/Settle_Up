public static class ReceiptItemMergeExample
{
    public static IReadOnlyList<MergedReceiptUiItem> BuildSampleMergedItems()
    {
        var sampleItems = new[]
        {
            new OcrReceiptItem("item-1", "Pizza Slice", "pizza slice", 3.00m, 1, 3.00m, 0),
            new OcrReceiptItem("item-2", "PIZZA SLICE", "pizza slice", 3.00m, 1, 3.00m, 1),
            new OcrReceiptItem("item-3", "pizza  slice", "pizza slice", 3.00m, 1, 3.00m, 2),
            new OcrReceiptItem("item-4", "Coke", "coke", 2.00m, 1, 2.00m, 3),
            new OcrReceiptItem("item-5", "coke", "coke", 2.00m, 1, 2.00m, 4),
            new OcrReceiptItem("item-6", "Beer", "beer", 5.00m, 1, 5.00m, 5)
        };

        return ReceiptItemMergeService.MergeForUi(sampleItems);
    }
}
