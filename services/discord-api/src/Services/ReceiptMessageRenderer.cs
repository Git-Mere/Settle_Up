using Discord;

public static class ReceiptMessageRenderer
{
    public static RenderedReceiptMessage RenderReceiptMessage(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.CachedRenderedMessage is not null &&
            session.CachedRenderedAtUtc is not null &&
            session.CachedRenderedAtUtc >= session.UpdatedAtUtc)
        {
            return session.CachedRenderedMessage;
        }

        var renderContext = ReceiptRenderContext.Create(session);
        RenderedReceiptMessage renderedMessage;

        if (!session.IsDraftReady)
        {
            renderedMessage = new RenderedReceiptMessage(RenderPendingEmbed(session));
        }
        else if (session.IsConfirmed)
        {
            renderedMessage = new RenderedReceiptMessage(RenderConfirmedEmbed(session, renderContext));
        }
        else
        {
            renderedMessage = new RenderedReceiptMessage(RenderCheckEmbed(session, renderContext));
        }

        session.CachedRenderedMessage = renderedMessage;
        session.CachedRenderedAtUtc = DateTimeOffset.UtcNow;
        return renderedMessage;
    }

    private static Embed RenderPendingEmbed(ReceiptSessionState session)
    {
        var language = session.PublicLanguage;
        return new EmbedBuilder()
            .WithTitle(DiscordUiText.SettlementCheckTitle(language))
            .WithColor(new Color(230, 126, 34))
            .AddField(DiscordUiText.StatusField(language), DiscordUiText.PendingStatusText(language), inline: false)
            .AddField(DiscordUiText.BuyerNameField(language), session.UploadedByDisplayName ?? session.UploadedByUserId ?? DiscordUiText.Unknown(language), inline: true)
            .AddField(DiscordUiText.SellerNameField(language), session.MerchantName ?? DiscordUiText.Pending(language), inline: true)
            .AddField(DiscordUiText.PurchaseDateField(language), session.TransactionDate?.ToString("yyyy-MM-dd") ?? DiscordUiText.Pending(language), inline: true)
            .WithFooter("Settle Up")
            .Build();
    }

    private static Embed RenderCheckEmbed(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var language = session.PublicLanguage;
        var builder = CreateHeaderBuilder(session, DiscordUiText.SettlementCheckTitle(language), new Color(52, 152, 219), renderContext.ItemsTotal);
        builder.AddField(DiscordUiText.SharedField(language), BuildSharedSection(session, renderContext), inline: false);
        builder.AddField(DiscordUiText.IndividualField(language), BuildIndividualSection(session, renderContext), inline: false);
        builder.AddField(DiscordUiText.UnassignedField(language), BuildUnassignedSection(session, renderContext), inline: false);
        if ((session.Tax ?? 0m) > 0m || (session.Sst ?? 0m) > 0m || (session.Slt ?? 0m) > 0m)
        {
            builder.AddField(DiscordUiText.TaxField(language), BuildTaxSection(session, renderContext), inline: false);
        }

        if ((session.Tip ?? 0m) > 0m)
        {
            builder.AddField(DiscordUiText.TipField(language), BuildTipSection(session, renderContext), inline: false);
        }

        builder.WithFooter(ReceiptSessionStateService.GetConfirmBlockReason(session)
            ?? DiscordUiText.ConfirmReadyFooter(language));
        return builder.Build();
    }

    private static Embed RenderConfirmedEmbed(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var language = session.PublicLanguage;
        var builder = CreateHeaderBuilder(session, DiscordUiText.SettlementConfirmedTitle(language), new Color(46, 204, 113), renderContext.ItemsTotal);
        builder.AddField(DiscordUiText.PayToField(language), session.PaymentContact ?? DiscordUiText.PaymentContactMissing(language), inline: false);

        var settlementLines = renderContext.SettlementLines;
        builder.AddField(
            DiscordUiText.SettlementField(language),
            settlementLines.Count == 0
                ? $"• {session.UploadedByDisplayName ?? session.UploadedByUserId ?? DiscordUiText.Unknown(language)} - {FormatMoney(0m, session.Currency)}"
                : BuildConfirmedSettlementSection(session, renderContext),
            inline: false);

        builder.WithFooter(DiscordUiText.ConfirmedAtFooter(language, session.ConfirmedAtUtc));
        return builder.Build();
    }

    private static EmbedBuilder CreateHeaderBuilder(ReceiptSessionState session, string title, Color color, decimal itemsTotal)
    {
        var language = session.PublicLanguage;
        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(color)
            .AddField(DiscordUiText.SellerNameField(language), session.MerchantName ?? DiscordUiText.Unknown(language), inline: true)
            .AddField(DiscordUiText.PurchaseDateField(language), session.TransactionDate?.ToString("yyyy-MM-dd") ?? DiscordUiText.Unknown(language), inline: true)
            .AddField(DiscordUiText.BuyerNameField(language), session.UploadedByDisplayName ?? session.UploadedByUserId ?? DiscordUiText.Unknown(language), inline: true)
            .AddField(DiscordUiText.ItemTotalPriceField(language), FormatMoney(itemsTotal, session.Currency), inline: true);

        if ((session.Tax ?? 0m) > 0m)
        {
            builder.AddField(DiscordUiText.TaxField(language), FormatMoney(session.Tax!.Value, session.Currency), inline: true);
        }
        else
        {
            AddInlineSpacer(builder);
        }

        builder.AddField(DiscordUiText.TotalPriceField(language), session.Total is decimal total ? FormatMoney(total, session.Currency) : DiscordUiText.Unknown(language), inline: true);

        var hasTip = (session.Tip ?? 0m) > 0m;
        var hasSst = (session.Sst ?? 0m) > 0m;
        var hasSlt = (session.Slt ?? 0m) > 0m;
        if (hasTip || hasSst || hasSlt)
        {
            if (hasTip)
            {
                builder.AddField(DiscordUiText.TipField(language), FormatMoney(session.Tip!.Value, session.Currency), inline: true);
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
        var lines = new List<string>();

        foreach (var item in session.Items)
        {
            var users = renderContext.UsersByItemId.GetValueOrDefault(item.Id) ?? [];
            if (users.Count <= 1)
            {
                continue;
            }

            var resolvedUsers = new string[users.Count];
            for (var index = 0; index < users.Count; index++)
            {
                resolvedUsers[index] = renderContext.ResolveUserDisplayName(users[index]);
            }

            lines.Add(
                $"• {FormatItemSummary(renderContext.ItemDisplayNames.GetValueOrDefault(item.Id) ?? item.GroupDisplayName, item.IsAlcohol, item.Amount, item.DiscountAmount, session.Currency)} | {string.Join(", ", resolvedUsers)}");
        }

        return lines.Count == 0 ? "• None" : string.Join('\n', lines);
    }

    private static string BuildIndividualSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var orderedUsers = renderContext.ItemsByUserId.Keys
            .OrderBy(renderContext.ResolveUserDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (orderedUsers.Length == 0)
        {
            return "• None";
        }

        var sections = new List<string>();
        foreach (var userId in orderedUsers)
        {
            var items = renderContext.ItemsByUserId.GetValueOrDefault(userId) ?? [];
            var individualItems = new List<ReceiptLineItemState>();
            var individualTotal = 0m;

            foreach (var item in items)
            {
                if ((renderContext.UsersByItemId.GetValueOrDefault(item.Id)?.Count ?? 0) != 1)
                {
                    continue;
                }

                individualItems.Add(item);
                individualTotal += item.Amount;
            }

            if (individualItems.Count == 0)
            {
                continue;
            }

            sections.Add($"{renderContext.ResolveUserDisplayName(userId)} - {FormatMoney(individualTotal, session.Currency)}");

            foreach (var item in individualItems)
            {
                sections.Add($"• {FormatItemSummary(renderContext.ItemDisplayNames.GetValueOrDefault(item.Id) ?? item.GroupDisplayName, item.IsAlcohol, item.Amount, item.DiscountAmount, session.Currency)}");
            }

            sections.Add(string.Empty);
        }

        if (sections.Count == 0)
        {
            return "• None";
        }

        while (sections.Count > 0 && string.IsNullOrWhiteSpace(sections[^1]))
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return string.Join('\n', sections);
    }

    private static string BuildUnassignedSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        if (renderContext.UnassignedItems.Count == 0)
        {
            return "• None";
        }

        var groups = new List<string>(renderContext.UnassignedItems.Count);
        foreach (var item in renderContext.UnassignedItems)
        {
            groups.Add($"• {FormatItemSummary(renderContext.ItemDisplayNames.GetValueOrDefault(item.Id) ?? item.GroupDisplayName, item.IsAlcohol, item.Amount, item.DiscountAmount, session.Currency)}");
        }

        return string.Join('\n', groups);
    }

    private static string BuildTaxSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        var lines = new List<string>();
        foreach (var entry in renderContext.TaxLines.OrderBy(entry => renderContext.ResolveUserDisplayName(entry.Key), StringComparer.OrdinalIgnoreCase))
        {
            var line = entry.Value;
            var parts = new List<string>(3);

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

            lines.Add($"{renderContext.ResolveUserDisplayName(entry.Key)} - {content}");
        }

        if (renderContext.HasSpecialTaxWithoutAlcoholItems)
        {
            lines.Add($"Unallocated alcohol tax - {FormatMoney((session.Sst ?? 0m) + (session.Slt ?? 0m), session.Currency)}");
        }

        return lines.Count == 0 ? "• None" : string.Join('\n', lines);
    }

    private static string BuildTipSection(ReceiptSessionState session, ReceiptRenderContext renderContext)
    {
        if (renderContext.TipLines.Count == 0)
        {
            return "• None";
        }

        var lines = new List<string>(renderContext.TipLines.Count);
        foreach (var entry in renderContext.TipLines.OrderBy(entry => renderContext.ResolveUserDisplayName(entry.Key), StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{renderContext.ResolveUserDisplayName(entry.Key)} - {FormatMoney(entry.Value, session.Currency)}");
        }

        return string.Join('\n', lines);
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

            var formattedItems = new string[userItems.Count];
            for (var index = 0; index < userItems.Count; index++)
            {
                var item = userItems[index];
                formattedItems[index] = FormatItemSummary(
                    renderContext.ItemDisplayNames.GetValueOrDefault(item.Id) ?? item.GroupDisplayName,
                    item.IsAlcohol,
                    renderContext.ParticipantItemShares.GetValueOrDefault(line.UserId)?.GetValueOrDefault(item.Id) ?? 0m,
                    item.DiscountAmount,
                    session.Currency);
            }

            var itemSummary = string.Join(", ", formattedItems);

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

    private static string GetDisplayItemName(ReceiptSessionState session, ReceiptLineItemState item)
    {
        var duplicateCount = session.Items.Count(candidate =>
            string.Equals(candidate.GroupKey, item.GroupKey, StringComparison.Ordinal));

        if (duplicateCount <= 1)
        {
            return item.GroupDisplayName;
        }

        var instanceIndex = session.Items
            .Where(candidate => string.Equals(candidate.GroupKey, item.GroupKey, StringComparison.Ordinal))
            .Select((candidate, index) => new { candidate.Id, Index = index + 1 })
            .First(entry => string.Equals(entry.Id, item.Id, StringComparison.Ordinal))
            .Index;

        return $"{item.GroupDisplayName} #{instanceIndex}";
    }

    private static string FormatItemSummary(string name, bool isAlcohol, decimal amount, decimal discountAmount, string? currency)
    {
        var baseText = $"{FormatItemName(name, isAlcohol)} - {FormatMoney(amount, currency)}";
        if (discountAmount <= 0m)
        {
            return baseText;
        }

        return $"{baseText} (discount -{FormatMoney(discountAmount, currency)})";
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

    private sealed class ReceiptRenderContext
    {
        public required IReadOnlyDictionary<string, IReadOnlyList<string>> UsersByItemId { get; init; }
        public required IReadOnlyDictionary<string, IReadOnlyList<ReceiptLineItemState>> ItemsByUserId { get; init; }
        public required IReadOnlyDictionary<string, string> ItemDisplayNames { get; init; }
        public required IReadOnlyList<ReceiptLineItemState> UnassignedItems { get; init; }
        public required IReadOnlyList<ReceiptSettlementLine> SettlementLines { get; init; }
        public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> ParticipantItemShares { get; init; }
        public required decimal ItemsTotal { get; init; }
        public required IReadOnlyDictionary<string, ParticipantTaxLine> TaxLines { get; init; }
        public required IReadOnlyDictionary<string, decimal> TipLines { get; init; }
        public required bool HasSpecialTaxWithoutAlcoholItems { get; init; }
        public required Func<string, string> ResolveUserDisplayName { get; init; }

        public static ReceiptRenderContext Create(ReceiptSessionState session)
        {
            var itemsByUserId = new Dictionary<string, List<ReceiptLineItemState>>(StringComparer.Ordinal);
            var readOnlyUsersByItem = ReceiptSessionStateService.BuildUsersByItemId(session);
            var itemDisplayNames = BuildItemDisplayNames(session);
            var unassignedItems = new List<ReceiptLineItemState>();

            foreach (var item in session.Items)
            {
                if (!readOnlyUsersByItem.TryGetValue(item.Id, out var assignedUsers) || assignedUsers.Count == 0)
                {
                    unassignedItems.Add(item);
                    continue;
                }

                foreach (var userId in assignedUsers)
                {
                    if (!itemsByUserId.TryGetValue(userId, out var selectedItems))
                    {
                        selectedItems = new List<ReceiptLineItemState>();
                        itemsByUserId[userId] = selectedItems;
                    }

                    selectedItems.Add(item);
                }
            }

            var readOnlyItemsByUser = itemsByUserId.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ReceiptLineItemState>)pair.Value
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

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
            var participantItemShares = ReceiptAllocationService.CalculateParticipantItemShares(readOnlyUsersByItem, session.Items);

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
                ItemDisplayNames = itemDisplayNames,
                UnassignedItems = unassignedItems,
                SettlementLines = settlementLines,
                ParticipantItemShares = participantItemShares,
                ItemsTotal = session.Items.Sum(item => item.Amount),
                TaxLines = allocation.TaxLines,
                TipLines = allocation.TipLines,
                HasSpecialTaxWithoutAlcoholItems = ReceiptSessionStateService.RequiresAlcoholSelection(session) &&
                                                   !ReceiptSessionStateService.HasAlcoholItems(session),
                ResolveUserDisplayName = ResolveDisplayName
            };
        }

        private static IReadOnlyDictionary<string, string> BuildItemDisplayNames(ReceiptSessionState session)
        {
            var duplicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in session.Items)
            {
                if (!duplicateCounts.TryAdd(item.GroupKey, 1))
                {
                    duplicateCounts[item.GroupKey]++;
                }
            }

            var currentIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var result = new Dictionary<string, string>(session.Items.Count, StringComparer.Ordinal);

            foreach (var item in session.Items)
            {
                if (duplicateCounts.GetValueOrDefault(item.GroupKey) <= 1)
                {
                    result[item.Id] = item.GroupDisplayName;
                    continue;
                }

                var nextIndex = currentIndexes.GetValueOrDefault(item.GroupKey) + 1;
                currentIndexes[item.GroupKey] = nextIndex;
                result[item.Id] = $"{item.GroupDisplayName} #{nextIndex}";
            }

            return result;
        }
    }
}
