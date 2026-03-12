public static class ReceiptSessionStateExample
{
    public static ReceiptSessionState BuildSampleSession()
    {
        var mergedItems = ReceiptItemMergeExample.BuildSampleMergedItems();
        var session = ReceiptSessionStateService.CreateSession("receipt-123", mergedItems, merchantName: "Corner Market");

        session.UserDisplayNames["user-alice"] = "Sam";
        session.UserDisplayNames["user-bob"] = "Joy";
        session.UserDisplayNames["user-charlie"] = "Alex";

        var pizzaId = mergedItems.Single(item => item.NormalizedName == "pizza slice").Id;
        var cokeId = mergedItems.Single(item => item.NormalizedName == "coke").Id;

        ReceiptSessionStateService.AddSelection(session, "user-alice", pizzaId);
        ReceiptSessionStateService.AddSelection(session, "user-bob", pizzaId);
        ReceiptSessionStateService.AddSelection(session, "user-charlie", cokeId);

        return session;
    }

    public static ReceiptItemOwnershipClassification BuildSampleClassification()
    {
        var session = BuildSampleSession();
        return ReceiptSessionStateService.ClassifyItems(session);
    }

    public static RenderedReceiptMessage BuildSampleRenderedMessage()
    {
        var session = BuildSampleSession();
        return ReceiptMessageRenderer.RenderReceiptMessage(session);
    }
}
