using Discord;

public static class ReceiptMessageRenderer
{
    public static RenderedReceiptMessage RenderReceiptMessage(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var classification = ReceiptSessionStateService.ClassifyItems(session);
        var embed = new EmbedBuilder()
            .WithTitle($"Receipt: {session.MerchantName ?? "Unknown Merchant"}")
            .WithColor(Color.Blue)
            .AddField("Summary", BuildSummary(session), inline: false)
            .AddField("Shared", BuildShared(session, classification.SharedItems), inline: false)
            .AddField("Individual", BuildIndividual(session, classification.IndividualItems), inline: false)
            .AddField("Unassigned", BuildItems(classification.UnassignedItems), inline: false)
            .AddField("Status", "Select Items button to update selections.", inline: false)
            .WithFooter("Settle Up")
            .Build();

        return new RenderedReceiptMessage(embed);
    }

    private static string BuildSummary(ReceiptSessionState session)
    {
        var participants = session.UserSelections.Keys
            .Select(userId => ResolveUserDisplayName(session, userId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join('\n', new[]
        {
            $"• Total Items: {session.MergedItems.Count}",
            $"• Participants: {participants.Length}",
            $"• Selected Users: {(participants.Length == 0 ? "None" : string.Join(", ", participants))}"
        });
    }

    private static string BuildShared(ReceiptSessionState session, IReadOnlyList<MergedReceiptUiItem> items)
    {
        if (items.Count == 0)
        {
            return "• None";
        }

        return string.Join('\n', items
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item =>
            {
                var users = ReceiptSessionStateService.GetUsersForItem(session, item.Id)
                    .Select(userId => ResolveUserDisplayName(session, userId));
                return $"• {FormatItem(item)} — {string.Join(", ", users)}";
            }));
    }

    private static string BuildIndividual(ReceiptSessionState session, IReadOnlyList<MergedReceiptUiItem> items)
    {
        if (items.Count == 0)
        {
            return "• None";
        }

        var grouped = items
            .Select(item => new
            {
                Item = item,
                UserId = ReceiptSessionStateService.GetUsersForItem(session, item.Id).Single()
            })
            .GroupBy(entry => entry.UserId)
            .OrderBy(group => ResolveUserDisplayName(session, group.Key), StringComparer.OrdinalIgnoreCase);

        var lines = new List<string>();
        foreach (var group in grouped)
        {
            lines.Add(ResolveUserDisplayName(session, group.Key));
            lines.AddRange(group
                .OrderBy(entry => entry.Item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Item.Id, StringComparer.Ordinal)
                .Select(entry => $"• {FormatItem(entry.Item)}"));
            lines.Add(string.Empty);
        }

        while (lines.Count > 0 && string.IsNullOrEmpty(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }

    private static string BuildItems(IReadOnlyList<MergedReceiptUiItem> items)
    {
        if (items.Count == 0)
        {
            return "• None";
        }

        return string.Join('\n', items
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => $"• {FormatItem(item)}"));
    }

    private static string FormatItem(MergedReceiptUiItem item)
    {
        var baseName = item.DisplayName.EndsWith($"({item.Quantity})", StringComparison.Ordinal)
            ? item.DisplayName[..^($" ({item.Quantity})".Length)]
            : item.DisplayName;

        return $"{baseName} ×{item.Quantity}";
    }

    private static string ResolveUserDisplayName(ReceiptSessionState session, string userId)
    {
        return session.UserDisplayNames.TryGetValue(userId, out var displayName) &&
               !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : userId;
    }
}
