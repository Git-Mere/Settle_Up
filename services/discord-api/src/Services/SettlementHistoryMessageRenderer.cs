using Discord;

public static class SettlementHistoryMessageRenderer
{
    public static Embed RenderList(IReadOnlyList<ConfirmedSettlementHistoryDocument> histories)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Settlement History")
            .WithColor(new Color(52, 152, 219));

        foreach (var (history, index) in histories.Select((history, index) => (history, index)))
        {
            builder.AddField(
                $"{index + 1}. {history.MerchantName ?? "Unknown"}",
                BuildListLine(history),
                inline: false);
        }

        builder.WithFooter("상세 조회는 /history detail index:<번호> 를 사용하세요.");
        return builder.Build();
    }

    public static Embed RenderDetail(ConfirmedSettlementHistoryDocument history)
    {
        var builder = new EmbedBuilder()
            .WithTitle("Settlement History Detail")
            .WithColor(new Color(46, 204, 113))
            .AddField("Seller Name", history.MerchantName ?? "Unknown", inline: true)
            .AddField("Purchase Date", history.TransactionDate?.ToString("yyyy-MM-dd") ?? "Unknown", inline: true)
            .AddField("Buyer Name", history.UploadedByDisplayName ?? history.UploadedByUserId, inline: true)
            .AddField("Item Total Price", FormatMoney(history.Subtotal ?? 0m, history.Currency), inline: true);

        if ((history.Tax ?? 0m) > 0m)
        {
            builder.AddField("Tax", FormatMoney(history.Tax!.Value, history.Currency), inline: true);
        }
        else
        {
            AddInlineSpacer(builder);
        }

        builder.AddField("Total Price", history.Total is decimal total ? FormatMoney(total, history.Currency) : "Unknown", inline: true);

        var hasTip = (history.Tip ?? 0m) > 0m;
        var hasSst = (history.Sst ?? 0m) > 0m;
        var hasSlt = (history.Slt ?? 0m) > 0m;
        if (hasTip || hasSst || hasSlt)
        {
            if (hasTip)
            {
                builder.AddField("Tip", FormatMoney(history.Tip!.Value, history.Currency), inline: true);
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

        builder.AddField("Pay to", history.PaymentContact ?? "정산 수단이 입력되지 않았습니다.", inline: false);
        builder.AddField("Settlement", BuildSettlementSection(history), inline: false);
        builder.WithFooter($"Confirmed at {history.ConfirmedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        return builder.Build();
    }

    private static string BuildListLine(ConfirmedSettlementHistoryDocument history)
    {
        var participantSummary = history.Participants.Count == 0
            ? "None"
            : string.Join(", ", history.Participants.Select(participant => $"{participant.DisplayName} {FormatMoney(participant.Amount, history.Currency)}"));

        var purchaseLabel = history.TransactionDate?.ToString("M/d/yyyy") ?? "Unknown";
        return $"{purchaseLabel} (purchase) | {FormatMoney(history.Total ?? 0m, history.Currency)}\n{participantSummary}";
    }

    private static string BuildSettlementSection(ConfirmedSettlementHistoryDocument history)
    {
        var sections = new List<string>();
        foreach (var participant in history.Participants)
        {
            sections.Add($"• {participant.DisplayName} - {FormatMoney(participant.Amount, history.Currency)}");

            var itemSummary = participant.Items.Count == 0
                ? "None"
                : string.Join(", ", participant.Items.Select(item => $"{FormatItemName(item.Name, item.IsAlcohol)} x{item.Quantity} - {FormatMoney(item.Amount, history.Currency)}"));

            sections.Add($"Items: {itemSummary}");
            sections.Add(string.Empty);
        }

        while (sections.Count > 0 && string.IsNullOrWhiteSpace(sections[^1]))
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return sections.Count == 0 ? "• None" : string.Join('\n', sections);
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
