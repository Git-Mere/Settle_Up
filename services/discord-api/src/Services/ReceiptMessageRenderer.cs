using Discord;

public static class ReceiptMessageRenderer
{
    public static RenderedReceiptMessage RenderReceiptMessage(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsDraftReady)
        {
            return new RenderedReceiptMessage(RenderPendingEmbed(session));
        }

        if (session.IsConfirmed)
        {
            return new RenderedReceiptMessage(RenderConfirmedEmbed(session));
        }

        return new RenderedReceiptMessage(RenderCheckEmbed(session));
    }

    private static Embed RenderPendingEmbed(ReceiptSessionState session)
    {
        return new EmbedBuilder()
            .WithTitle("Settlement Check")
            .WithColor(new Color(230, 126, 34))
            .AddField("Status", "영수증을 분석 중입니다. 파싱이 끝나면 같은 채널 메시지가 자동으로 갱신됩니다.", inline: false)
            .AddField("Buyer Name", session.UploadedByDisplayName ?? session.UploadedByUserId ?? "Unknown", inline: true)
            .AddField("Seller Name", session.MerchantName ?? "Pending", inline: true)
            .AddField("Purchase Date", session.TransactionDate?.ToString("yyyy-MM-dd") ?? "Pending", inline: true)
            .WithFooter("Settle Up")
            .Build();
    }

    private static Embed RenderCheckEmbed(ReceiptSessionState session)
    {
        var builder = CreateHeaderBuilder(session, "Settlement Check", new Color(52, 152, 219));
        builder.AddField("Shared", BuildSharedSection(session), inline: false);
        builder.AddField("Individual", BuildIndividualSection(session), inline: false);
        builder.AddField("Unassigned", BuildUnassignedSection(session), inline: false);
        builder.WithFooter(ReceiptSessionStateService.CanConfirm(session)
            ? "모든 아이템이 배정되어 confirm 가능합니다."
            : "Unassigned 아이템이 모두 배정되어야 confirm 가능합니다.");
        return builder.Build();
    }

    private static Embed RenderConfirmedEmbed(ReceiptSessionState session)
    {
        var builder = CreateHeaderBuilder(session, "Settlement Confirmed", new Color(46, 204, 113));
        builder.AddField("Payment", session.PaymentContact ?? "정산 수단이 입력되지 않았습니다.", inline: false);

        var settlementLines = ReceiptSessionStateService.BuildSettlementLines(session);
        builder.AddField(
            "Settlement",
            settlementLines.Count == 0
                ? $"• {session.UploadedByDisplayName ?? session.UploadedByUserId ?? "Unknown"} - {FormatMoney(0m, session.Currency)}"
                : string.Join('\n', settlementLines.Select(line => $"• {line.DisplayName} - {FormatMoney(line.Amount, session.Currency)}")),
            inline: false);

        builder.WithFooter($"Confirmed at {session.ConfirmedAtUtc?.ToString("yyyy-MM-dd HH:mm")} UTC");
        return builder.Build();
    }

    private static EmbedBuilder CreateHeaderBuilder(ReceiptSessionState session, string title, Color color)
    {
        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(color)
            .AddField("Seller Name", session.MerchantName ?? "Unknown", inline: true)
            .AddField("Purchase Date", session.TransactionDate?.ToString("yyyy-MM-dd") ?? "Unknown", inline: true)
            .AddField("Buyer Name", session.UploadedByDisplayName ?? session.UploadedByUserId ?? "Unknown", inline: true)
            .AddField("Item Total Price", FormatMoney(ReceiptSessionStateService.GetItemsTotal(session), session.Currency), inline: true)
            .AddField("Tax", session.Tax is decimal tax ? FormatMoney(tax, session.Currency) : "Unknown", inline: true)
            .AddField("Total Price", session.Total is decimal total ? FormatMoney(total, session.Currency) : "Unknown", inline: true);

        return builder;
    }

    private static string BuildSharedSection(ReceiptSessionState session)
    {
        var groups = session.Items
            .Select(item => new
            {
                Item = item,
                Users = ReceiptSessionStateService.GetUsersForItem(session, item.Id)
            })
            .Where(entry => entry.Users.Count > 1)
            .GroupBy(entry => new SharedGroupingKey(
                entry.Item.GroupKey,
                entry.Item.GroupDisplayName,
                string.Join('|', entry.Users),
                entry.Item.Amount))
            .OrderBy(group => group.Key.Name, StringComparer.OrdinalIgnoreCase);

        var lines = groups
            .Select(group =>
            {
                var users = group.First().Users.Select(userId => ReceiptSessionStateService.ResolveUserDisplayName(session, userId));
                return $"• {group.Key.Name} x{group.Count()} - {FormatMoney(group.Key.Amount * group.Count(), session.Currency)} | {string.Join(", ", users)}";
            })
            .ToArray();

        return lines.Length == 0 ? "• None" : string.Join('\n', lines);
    }

    private static string BuildIndividualSection(ReceiptSessionState session)
    {
        var userGroups = session.UserSelections
            .OrderBy(entry => ReceiptSessionStateService.ResolveUserDisplayName(session, entry.Key), StringComparer.OrdinalIgnoreCase)
            .Select(entry => new
            {
                UserId = entry.Key,
                DisplayName = ReceiptSessionStateService.ResolveUserDisplayName(session, entry.Key),
                Items = session.Items.Where(item => entry.Value.Contains(item.Id)).ToArray()
            })
            .Where(entry => entry.Items.Any(item => ReceiptSessionStateService.GetUsersForItem(session, item.Id).Count == 1))
            .ToArray();

        if (userGroups.Length == 0)
        {
            return "• None";
        }

        var sections = new List<string>();
        foreach (var userGroup in userGroups)
        {
            var individualTotal = ReceiptSessionStateService.GetIndividualTotalForUser(session, userGroup.UserId);
            sections.Add($"{userGroup.DisplayName} - {FormatMoney(individualTotal, session.Currency)}");

            foreach (var itemGroup in userGroup.Items
                         .Where(item => ReceiptSessionStateService.GetUsersForItem(session, item.Id).Count == 1)
                         .GroupBy(item => new ItemGroupingKey(item.GroupKey, item.GroupDisplayName, item.Amount))
                         .OrderBy(group => group.Key.Name, StringComparer.OrdinalIgnoreCase))
            {
                sections.Add($"• {itemGroup.Key.Name} x{itemGroup.Count()} - {FormatMoney(itemGroup.Key.Amount * itemGroup.Count(), session.Currency)}");
            }

            sections.Add(string.Empty);
        }

        while (sections.Count > 0 && string.IsNullOrWhiteSpace(sections[^1]))
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return string.Join('\n', sections);
    }

    private static string BuildUnassignedSection(ReceiptSessionState session)
    {
        var groups = ReceiptSessionStateService.GetUnassignedItems(session)
            .GroupBy(item => new ItemGroupingKey(item.GroupKey, item.GroupDisplayName, item.Amount))
            .OrderBy(group => group.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"• {group.Key.Name} x{group.Count()} - {FormatMoney(group.Key.Amount * group.Count(), session.Currency)}")
            .ToArray();

        return groups.Length == 0 ? "• None" : string.Join('\n', groups);
    }

    private static string FormatMoney(decimal amount, string? currency)
    {
        return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(currency)
            ? $"${amount:0.00}"
            : $"{amount:0.00} {currency}";
    }

    private sealed record SharedGroupingKey(string GroupKey, string Name, string UsersKey, decimal Amount);

    private sealed record ItemGroupingKey(string GroupKey, string Name, decimal Amount);
}
