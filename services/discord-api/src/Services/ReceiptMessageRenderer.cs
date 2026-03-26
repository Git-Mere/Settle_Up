using Discord;

public static class ReceiptMessageRenderer
{
    public static RenderedReceiptMessage RenderReceiptMessage(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var renderContext = ReceiptRenderContext.Create(session);

        if (!session.IsDraftReady)
        {
            return new RenderedReceiptMessage(RenderPendingEmbed(session));
        }

        if (session.IsConfirmed)
        {
            return new RenderedReceiptMessage(RenderConfirmedEmbed(session, renderContext));
        }

        return new RenderedReceiptMessage(RenderCheckEmbed(session, renderContext));
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

    private static Embed RenderCheckEmbed(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var builder = CreateHeaderBuilder(session, "Settlement Check", new Color(52, 152, 219), renderContext.ItemsTotal);
        builder.AddField("Shared", BuildSharedSection(session, renderContext), inline: false);
        builder.AddField("Individual", BuildIndividualSection(session, renderContext), inline: false);
        builder.AddField("Unassigned", BuildUnassignedSection(session, renderContext), inline: false);
        if ((session.Tax ?? 0m) > 0m || (session.Sst ?? 0m) > 0m || (session.Slt ?? 0m) > 0m)
        {
            builder.AddField("Tax", BuildTaxSection(session, renderContext), inline: false);
        }

        if ((session.Tip ?? 0m) > 0m)
        {
            builder.AddField("Tip", BuildTipSection(session, renderContext), inline: false);
        }

        builder.WithFooter(ReceiptSessionStateService.GetConfirmBlockReason(session)
            ?? "모든 아이템이 배정되어 confirm 가능합니다.");
        return builder.Build();
    }

    private static Embed RenderConfirmedEmbed(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var builder = CreateHeaderBuilder(session, "Settlement Confirmed", new Color(46, 204, 113), renderContext.ItemsTotal);
        builder.AddField("Pay to", session.PaymentContact ?? "정산 수단이 입력되지 않았습니다.", inline: false);

        var settlementLines = renderContext.SettlementLines;
        builder.AddField(
            "Settlement",
            settlementLines.Count == 0
                ? $"• {session.UploadedByDisplayName ?? session.UploadedByUserId ?? "Unknown"} - {FormatMoney(0m, session.Currency)}"
                : BuildConfirmedSettlementSection(session, renderContext),
            inline: false);

        builder.WithFooter($"Confirmed at {session.ConfirmedAtUtc?.ToString("yyyy-MM-dd HH:mm")} UTC");
        return builder.Build();
    }

    private static EmbedBuilder CreateHeaderBuilder(ReceiptSessionState session, string title, Color color, decimal itemsTotal)
    {
        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(color)
            .AddField("Seller Name", session.MerchantName ?? "Unknown", inline: true)
            .AddField("Purchase Date", session.TransactionDate?.ToString("yyyy-MM-dd") ?? "Unknown", inline: true)
            .AddField("Buyer Name", session.UploadedByDisplayName ?? session.UploadedByUserId ?? "Unknown", inline: true)
            .AddField("Item Total Price", FormatMoney(itemsTotal, session.Currency), inline: true);

        if ((session.Tax ?? 0m) > 0m)
        {
            builder.AddField("Tax", FormatMoney(session.Tax!.Value, session.Currency), inline: true);
        }
        else
        {
            AddInlineSpacer(builder);
        }

        builder.AddField("Total Price", session.Total is decimal total ? FormatMoney(total, session.Currency) : "Unknown", inline: true);

        var hasTip = (session.Tip ?? 0m) > 0m;
        var hasSst = (session.Sst ?? 0m) > 0m;
        var hasSlt = (session.Slt ?? 0m) > 0m;
        if (hasTip || hasSst || hasSlt)
        {
            if (hasTip)
            {
                builder.AddField("Tip", FormatMoney(session.Tip!.Value, session.Currency), inline: true);
            }
            else
            {
                AddInlineSpacer(builder);
            }

            if (hasSst)
            {
                builder.AddField("SST", FormatMoney(session.Sst!.Value, session.Currency), inline: true);
            }
            else
            {
                AddInlineSpacer(builder);
            }

            if (hasSlt)
            {
                builder.AddField("SLT", FormatMoney(session.Slt!.Value, session.Currency), inline: true);
            }
            else
            {
                AddInlineSpacer(builder);
            }
        }

        return builder;
    }

    private static string BuildSharedSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var groups = session.Items
            .Select(item => new
            {
                Item = item,
                Users = renderContext.UsersByItemId.GetValueOrDefault(item.Id) ?? []
            })
            .Where(entry => entry.Users.Count > 1)
            .GroupBy(entry => new SharedGroupingKey(
                entry.Item.GroupKey,
                entry.Item.GroupDisplayName,
                string.Join('|', entry.Users),
                entry.Item.Amount,
                entry.Item.IsAlcohol));

        var lines = groups
            .Select(group =>
            {
                var users = group.First().Users.Select(renderContext.ResolveUserDisplayName);
                return $"• {FormatItemName(group.Key.Name, group.Key.IsAlcohol)} x{group.Count()} - {FormatMoney(group.Key.Amount * group.Count(), session.Currency)} | {string.Join(", ", users)}";
            })
            .ToArray();

        return lines.Length == 0 ? "• None" : string.Join('\n', lines);
    }

    private static string BuildIndividualSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var userGroups = session.UserSelections
            .OrderBy(entry => renderContext.ResolveUserDisplayName(entry.Key), StringComparer.OrdinalIgnoreCase)
            .Select(entry => new
            {
                UserId = entry.Key,
                DisplayName = renderContext.ResolveUserDisplayName(entry.Key),
                Items = renderContext.ItemsByUserId.GetValueOrDefault(entry.Key) ?? []
            })
            .Where(entry => entry.Items.Any(item => (renderContext.UsersByItemId.GetValueOrDefault(item.Id)?.Count ?? 0) == 1))
            .ToArray();

        if (userGroups.Length == 0)
        {
            return "• None";
        }

        var sections = new List<string>();
        foreach (var userGroup in userGroups)
        {
            var individualTotal = userGroup.Items
                .Where(item => (renderContext.UsersByItemId.GetValueOrDefault(item.Id)?.Count ?? 0) == 1)
                .Sum(item => item.Amount);
            sections.Add($"{userGroup.DisplayName} - {FormatMoney(individualTotal, session.Currency)}");

            foreach (var itemGroup in userGroup.Items
                         .Where(item => (renderContext.UsersByItemId.GetValueOrDefault(item.Id)?.Count ?? 0) == 1)
                         .GroupBy(item => new ItemGroupingKey(item.GroupKey, item.GroupDisplayName, item.Amount, item.IsAlcohol)))
            {
                sections.Add($"• {FormatItemName(itemGroup.Key.Name, itemGroup.Key.IsAlcohol)} x{itemGroup.Count()} - {FormatMoney(itemGroup.Key.Amount * itemGroup.Count(), session.Currency)}");
            }

            sections.Add(string.Empty);
        }

        while (sections.Count > 0 && string.IsNullOrWhiteSpace(sections[^1]))
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return string.Join('\n', sections);
    }

    private static string BuildUnassignedSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var groups = renderContext.UnassignedItems
            .GroupBy(item => new ItemGroupingKey(item.GroupKey, item.GroupDisplayName, item.Amount, item.IsAlcohol))
            .Select(group => $"• {FormatItemName(group.Key.Name, group.Key.IsAlcohol)} x{group.Count()} - {FormatMoney(group.Key.Amount * group.Count(), session.Currency)}")
            .ToArray();

        return groups.Length == 0 ? "• None" : string.Join('\n', groups);
    }

    private static string BuildTaxSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var lines = renderContext.TaxLines
            .OrderBy(entry => renderContext.ResolveUserDisplayName(entry.Key), StringComparer.OrdinalIgnoreCase)
            .Select(entry =>
            {
                var line = entry.Value;
                var parts = new List<string>();

                if (line.GeneralTax > 0m)
                {
                    parts.Add(FormatMoney(line.GeneralTax, session.Currency));
                }

                if (line.Sst > 0m)
                {
                    parts.Add($"{FormatMoney(line.Sst, session.Currency)}(SST)");
                }

                if (line.Slt > 0m)
                {
                    parts.Add($"{FormatMoney(line.Slt, session.Currency)}(SLT)");
                }

                if (parts.Count == 0)
                {
                    parts.Add(FormatMoney(0m, session.Currency));
                }

                var content = string.Join(" + ", parts);
                if (parts.Count > 1)
                {
                    content += $" = {FormatMoney(line.Total, session.Currency)}";
                }

                return $"{renderContext.ResolveUserDisplayName(entry.Key)} - {content}";
            })
            .ToList();

        if (renderContext.HasSpecialTaxWithoutAlcoholItems)
        {
            lines.Add($"Unallocated alcohol tax - {FormatMoney((session.Sst ?? 0m) + (session.Slt ?? 0m), session.Currency)}");
        }

        return lines.Count == 0 ? "• None" : string.Join('\n', lines);
    }

    private static string BuildTipSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var lines = renderContext.TipLines
            .OrderBy(entry => renderContext.ResolveUserDisplayName(entry.Key), StringComparer.OrdinalIgnoreCase)
            .Select(entry => $"{renderContext.ResolveUserDisplayName(entry.Key)} - {FormatMoney(entry.Value, session.Currency)}")
            .ToArray();

        return lines.Length == 0 ? "• None" : string.Join('\n', lines);
    }

    private static string BuildConfirmedSettlementSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var sections = new List<string>();

        foreach (var line in renderContext.SettlementLines)
        {
            sections.Add($"• {line.DisplayName} - {FormatMoney(line.Amount, session.Currency)}");

            var userItems = renderContext.ItemsByUserId.GetValueOrDefault(line.UserId) ?? [];
            if (userItems.Count == 0)
            {
                sections.Add("Items: None");
                sections.Add(string.Empty);
                continue;
            }

            var itemSummary = string.Join(
                ", ",
                userItems
                    .GroupBy(item => new ItemGroupingKey(item.GroupKey, item.GroupDisplayName, item.Amount, item.IsAlcohol))
                    .Select(itemGroup => $"{FormatItemName(itemGroup.Key.Name, itemGroup.Key.IsAlcohol)} x{itemGroup.Count()}"));

            sections.Add($"Items: {itemSummary}");

            sections.Add(string.Empty);
        }

        while (sections.Count > 0 && string.IsNullOrWhiteSpace(sections[^1]))
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return string.Join('\n', sections);
    }

    private static string FormatItemName(string name, bool isAlcohol)
    {
        return isAlcohol ? $"{name} 🥃" : name;
    }

    private static string FormatMoney(decimal amount, string? currency)
    {
        return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(currency)
            ? $"${amount:0.00}"
            : $"{amount:0.00} {currency}";
    }

    private static void AddInlineSpacer(EmbedBuilder builder)
    {
        builder.AddField("\u200B", "\u200B", inline: true);
    }

    private sealed record SharedGroupingKey(string GroupKey, string Name, string UsersKey, decimal Amount, bool IsAlcohol);

    private sealed record ItemGroupingKey(string GroupKey, string Name, decimal Amount, bool IsAlcohol);

    private sealed class ReceiptRenderContext
    {
        public required Dictionary<string, IReadOnlyList<string>> UsersByItemId { get; init; }
        public required Dictionary<string, IReadOnlyList<ReceiptLineItemState>> ItemsByUserId { get; init; }
        public required IReadOnlyList<ReceiptLineItemState> UnassignedItems { get; init; }
        public required IReadOnlyList<ReceiptSettlementLine> SettlementLines { get; init; }
        public required decimal ItemsTotal { get; init; }
        public required IReadOnlyDictionary<string, ParticipantTaxLine> TaxLines { get; init; }
        public required IReadOnlyDictionary<string, decimal> TipLines { get; init; }
        public required bool HasSpecialTaxWithoutAlcoholItems { get; init; }
        public required Func<string, string> ResolveUserDisplayName { get; init; }

        public static ReceiptRenderContext Create(ReceiptSessionState session)
        {
            var usersByItemId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var itemsByUserId = new Dictionary<string, List<ReceiptLineItemState>>(StringComparer.Ordinal);

            foreach (var userSelection in session.UserSelections)
            {
                var selectedItems = new List<ReceiptLineItemState>();
                foreach (var item in session.Items)
                {
                    if (!userSelection.Value.Contains(item.Id))
                    {
                        continue;
                    }

                    selectedItems.Add(item);

                    if (!usersByItemId.TryGetValue(item.Id, out var users))
                    {
                        users = new List<string>();
                        usersByItemId[item.Id] = users;
                    }

                    users.Add(userSelection.Key);
                }

                if (selectedItems.Count > 0)
                {
                    itemsByUserId[userSelection.Key] = selectedItems;
                }
            }

            var readOnlyUsersByItem = usersByItemId.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.OrderBy(userId => userId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

            var readOnlyItemsByUser = itemsByUserId.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ReceiptLineItemState>)pair.Value,
                StringComparer.Ordinal);

            var unassignedItems = session.Items
                .Where(item => !readOnlyUsersByItem.ContainsKey(item.Id))
                .ToArray();

            var displayNameCache = new Dictionary<string, string>(StringComparer.Ordinal);
            string ResolveDisplayName(string userId)
            {
                if (displayNameCache.TryGetValue(userId, out var cached))
                {
                    return cached;
                }

                var resolved = ReceiptSessionStateService.ResolveUserDisplayName(session, userId);
                displayNameCache[userId] = resolved;
                return resolved;
            }

            var allocation = ReceiptAllocationService.Calculate(session);

            var settlementLines = allocation.SettlementTotals
                .Where(entry => entry.Value > 0)
                .Select(entry => new ReceiptSettlementLine(
                    entry.Key,
                    ResolveDisplayName(entry.Key),
                    decimal.Round(entry.Value, 2, MidpointRounding.AwayFromZero)))
                .OrderBy(line => line.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new ReceiptRenderContext
            {
                UsersByItemId = readOnlyUsersByItem,
                ItemsByUserId = readOnlyItemsByUser,
                UnassignedItems = unassignedItems,
                SettlementLines = settlementLines,
                ItemsTotal = session.Items.Sum(item => item.Amount),
                TaxLines = allocation.TaxLines,
                TipLines = allocation.TipLines,
                HasSpecialTaxWithoutAlcoholItems = ReceiptSessionStateService.RequiresAlcoholSelection(session) &&
                                                   !ReceiptSessionStateService.HasAlcoholItems(session),
                ResolveUserDisplayName = ResolveDisplayName
            };
        }
    }
}
