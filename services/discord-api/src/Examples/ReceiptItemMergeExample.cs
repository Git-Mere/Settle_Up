public static class ReceiptItemMergeExample
{
    public static IReadOnlyList<ReceiptLineItemState> BuildSampleItems()
    {
        var sampleItems = new[]
        {
            new ReceiptDraftNotificationItem("item-1", "Pizza Slice", 1, 3.00m, 3.00m),
            new ReceiptDraftNotificationItem("item-2", "PIZZA SLICE", 1, 3.00m, 3.00m),
            new ReceiptDraftNotificationItem("item-3", "pizza  slice", 1, 3.00m, 3.00m),
            new ReceiptDraftNotificationItem("item-4", "Coke", 1, 2.00m, 2.00m),
            new ReceiptDraftNotificationItem("item-5", "coke", 1, 2.00m, 2.00m),
            new ReceiptDraftNotificationItem("item-6", "Beer", 1, 5.00m, 5.00m)
        };

        return ReceiptSessionStateService.ExpandItems(sampleItems);
    }
}
