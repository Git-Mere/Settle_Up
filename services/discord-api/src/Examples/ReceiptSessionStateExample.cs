public static class ReceiptSessionStateExample
{
    public static ReceiptSessionState BuildSampleSession()
    {
        var session = ReceiptSessionStateService.CreatePendingUploadSession(
            "receipt-123",
            "https://example.test/blob.jpg",
            "user-alice",
            "Sam",
            "sam@example.com");

        session.MerchantName = "Corner Market";
        session.Items = ReceiptItemMergeExample.BuildSampleItems().ToList();
        session.IsDraftReady = true;

        session.UserDisplayNames["user-alice"] = "Sam";
        session.UserDisplayNames["user-bob"] = "Joy";
        session.UserDisplayNames["user-charlie"] = "Alex";

        var pizzaPage = session.Items.Where(item => item.NormalizedName == "pizza slice").Select(item => item.Id).ToArray();
        var cokePage = session.Items.Where(item => item.NormalizedName == "coke").Select(item => item.Id).ToArray();

        ReceiptSessionStateService.ReplaceSelectionsForPage(session, "user-alice", pizzaPage, [pizzaPage[0]]);
        ReceiptSessionStateService.ReplaceSelectionsForPage(session, "user-bob", pizzaPage, [pizzaPage[0]]);
        ReceiptSessionStateService.ReplaceSelectionsForPage(session, "user-charlie", cokePage, [cokePage[0]]);

        return session;
    }

    public static RenderedReceiptMessage BuildSampleRenderedMessage()
    {
        var session = BuildSampleSession();
        return ReceiptMessageRenderer.RenderReceiptMessage(session);
    }
}
