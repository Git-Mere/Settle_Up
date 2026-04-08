using Discord;

public static class SettlementHistoryMessageRenderer
{
    public static Embed RenderList(IReadOnlyList<ConfirmedSettlementHistoryDocument> histories, AppLanguage language)
    {
        var builder = new EmbedBuilder()
            .WithTitle(DiscordUiText.SettlementHistoryTitle(language))
            .WithColor(new Color(52, 152, 219));

        foreach (var (history, index) in histories.Select((history, index) => (history, index)))
        {
            builder.AddField(
                $"{index + 1}. {history.MerchantName ?? DiscordUiText.Unknown(language)}",
                BuildListLine(history, language),
                inline: false);
        }

        builder.WithFooter(DiscordUiText.HistoryListFooter(language));
        return builder.Build();
    }

    public static Embed RenderDetail(ConfirmedSettlementHistoryDocument history, AppLanguage language)
    {
        var builder = new EmbedBuilder()
            .WithTitle(DiscordUiText.SettlementHistoryDetailTitle(language))
            .WithColor(new Color(46, 204, 113))
            .AddField(DiscordUiText.SellerNameField(language), history.MerchantName ?? DiscordUiText.Unknown(language), inline: true)
            .AddField(DiscordUiText.PurchaseDateField(language), history.TransactionDate?.ToString("yyyy-MM-dd") ?? DiscordUiText.Unknown(language), inline: true)
            .AddField(DiscordUiText.BuyerNameField(language), history.UploadedByDisplayName ?? history.UploadedByUserId, inline: true)
            .AddField(DiscordUiText.ItemTotalPriceField(language), FormatMoney(history.Subtotal ?? 0m, history.Currency), inline: true);

        if ((history.Tax ?? 0m) > 0m)
        {
            builder.AddField(DiscordUiText.TaxField(language), FormatMoney(history.Tax!.Value, history.Currency), inline: true);
        }
        else
        {
            AddInlineSpacer(builder);
        }

        builder.AddField(DiscordUiText.TotalPriceField(language), history.Total is decimal total ? FormatMoney(total, history.Currency) : DiscordUiText.Unknown(language), inline: true);

        var hasTip = (history.Tip ?? 0m) > 0m;
        var hasSst = (history.Sst ?? 0m) > 0m;
        var hasSlt = (history.Slt ?? 0m) > 0m;
        if (hasTip || hasSst || hasSlt)
        {
            if (hasTip)
            {
                builder.AddField(DiscordUiText.TipField(language), FormatMoney(history.Tip!.Value, history.Currency), inline: true);
            }
            else
            {
                AddInlineSpacer(builder);
            }

            if (hasSst)
            {
                builder.AddField("SST", FormatMoney(history.Sst!.Value, history.Currency), inline: true);
            }
            else
            {
                AddInlineSpacer(builder);
            }

            if (hasSlt)
            {
                builder.AddField("SLT", FormatMoney(history.Slt!.Value, history.Currency), inline: true);
            }
            else
            {
                AddInlineSpacer(builder);
            }
        }

        builder.AddField(DiscordUiText.PayToField(language), history.PaymentContact ?? DiscordUiText.PaymentContactMissing(language), inline: false);
        builder.AddField(DiscordUiText.SettlementField(language), BuildSettlementSection(history, language), inline: false);
        builder.WithFooter(DiscordUiText.HistoryDetailFooter(language, history.ConfirmedAtUtc));
        return builder.Build();
    }

    private static string BuildListLine(ConfirmedSettlementHistoryDocument history, AppLanguage language)
    {
        var participantSummary = history.Participants.Count == 0
            ? DiscordUiText.None(language)
            : string.Join(", ", history.Participants.Select(participant => $"{participant.DisplayName} {FormatMoney(participant.Amount, history.Currency)}"));

        var purchaseLabel = history.TransactionDate?.ToString("M/d/yyyy") ?? DiscordUiText.Unknown(language);
        return $"{purchaseLabel} ({DiscordUiText.PurchaseLabel(language)}) | {FormatMoney(history.Total ?? 0m, history.Currency)}\n{participantSummary}";
    }

    private static string BuildSettlementSection(ConfirmedSettlementHistoryDocument history, AppLanguage language)
    {
        var sections = new List<string>();
        foreach (var participant in history.Participants)
        {
            sections.Add($"• {participant.DisplayName} - {FormatMoney(participant.Amount, history.Currency)}");

            var itemSummary = participant.Items.Count == 0
                ? DiscordUiText.None(language)
                : string.Join(", ", participant.Items.Select(item => $"{FormatItemName(item.Name, item.IsAlcohol)} x{item.Quantity} - {FormatMoney(item.Amount, history.Currency)}"));

            sections.Add($"{DiscordUiText.ItemsLabel(language)}: {itemSummary}");
            sections.Add(string.Empty);
        }

        while (sections.Count > 0 && string.IsNullOrWhiteSpace(sections[^1]))
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return sections.Count == 0 ? $"• {DiscordUiText.None(language)}" : string.Join('\n', sections);
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
}
