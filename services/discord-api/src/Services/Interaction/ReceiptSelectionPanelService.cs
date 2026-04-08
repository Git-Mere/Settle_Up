using Discord;
using Discord.WebSocket;

public sealed class ReceiptSelectionPanelService
{
    public ReceiptSelectionPanel BuildPanel(
        ReceiptSessionState session,
        string userId,
        ReceiptSelectionMode mode,
        AppLanguage language,
        int page)
    {
        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        return new ReceiptSelectionPanel(
            BuildSelectionPrompt(mode, page, totalPages, language),
            BuildSelectionComponents(session, userId, mode, page, language));
    }

    public async Task RespondOrUpdateAsync(
        SocketMessageComponent component,
        bool updateExistingMessage,
        string content,
        MessageComponent? components)
    {
        if (updateExistingMessage)
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = content;
                properties.Components = components;
            });
            return;
        }

        await component.RespondAsync(content, components: components, ephemeral: true);
    }

    private MessageComponent BuildSelectionComponents(
        ReceiptSessionState session,
        string userId,
        ReceiptSelectionMode mode,
        int page,
        AppLanguage language)
    {
        var safePage = Math.Clamp(page, 0, ReceiptSessionStateService.GetTotalPages(session) - 1);
        var pageItems = ReceiptSessionStateService.GetPageItems(session, safePage);
        var builder = new ComponentBuilder();

        if (pageItems.Count > 0)
        {
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId(ReceiptInteractionCustomIds.BuildSelectMenuCustomId(mode, session.ReceiptId, safePage))
                .WithPlaceholder(GetSelectPlaceholder(mode, language))
                .WithMinValues(mode is ReceiptSelectionMode.Assign or ReceiptSelectionMode.Alcohol ? 0 : 1)
                .WithMaxValues(mode is ReceiptSelectionMode.Assign or ReceiptSelectionMode.Alcohol ? Math.Max(1, pageItems.Count) : 1);

            var selectedIds = ReceiptSessionStateService.GetItemsForUser(session, userId)
                .Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            var selectedAlcoholIds = session.Items
                .Where(item => item.IsAlcohol)
                .Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var item in pageItems)
            {
                var displayName = GetSelectionDisplayName(session, item);
                selectMenu.AddOption(
                    label: $"{displayName}{(item.IsAlcohol ? " [Alcohol]" : string.Empty)}",
                    value: item.Id,
                    description: $"{FormatMoney(item.Amount, session.Currency)}",
                    isDefault: mode switch
                    {
                        ReceiptSelectionMode.Assign => selectedIds.Contains(item.Id),
                        ReceiptSelectionMode.Alcohol => selectedAlcoholIds.Contains(item.Id),
                        _ => false
                    });
            }

            builder.WithSelectMenu(selectMenu, row: 0);
        }

        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        if (totalPages > 1)
        {
            builder.WithButton(
                DiscordUiText.PreviousPageButton(language),
                ReceiptInteractionCustomIds.BuildPageButtonCustomId(mode, session.ReceiptId, safePage - 1),
                ButtonStyle.Secondary,
                disabled: safePage == 0,
                row: 1);

            builder.WithButton(
                DiscordUiText.NextPageButton(language),
                ReceiptInteractionCustomIds.BuildPageButtonCustomId(mode, session.ReceiptId, safePage + 1),
                ButtonStyle.Secondary,
                disabled: safePage >= totalPages - 1,
                row: 1);
        }

        return builder.Build();
    }

    private static string BuildSelectionPrompt(ReceiptSelectionMode mode, int page, int totalPages, AppLanguage language)
    {
        var safePage = Math.Clamp(page, 0, totalPages - 1);

        return mode switch
        {
            ReceiptSelectionMode.Assign => DiscordUiText.AssignPrompt(language, safePage + 1, totalPages),
            ReceiptSelectionMode.Remove => DiscordUiText.RemovePrompt(language, safePage + 1, totalPages),
            ReceiptSelectionMode.Edit => DiscordUiText.EditPrompt(language, safePage + 1, totalPages),
            ReceiptSelectionMode.Alcohol => DiscordUiText.AlcoholPrompt(language, safePage + 1, totalPages),
            _ => DiscordUiText.AssignPrompt(language, safePage + 1, totalPages)
        };
    }

    private static string GetSelectPlaceholder(ReceiptSelectionMode mode, AppLanguage language)
    {
        return mode switch
        {
            ReceiptSelectionMode.Assign => DiscordUiText.AssignPlaceholder(language),
            ReceiptSelectionMode.Remove => DiscordUiText.RemovePlaceholder(language),
            ReceiptSelectionMode.Edit => DiscordUiText.EditPlaceholder(language),
            ReceiptSelectionMode.Alcohol => DiscordUiText.AlcoholPlaceholder(language),
            _ => DiscordUiText.AssignPlaceholder(language)
        };
    }

    private static int GetInstanceIndex(ReceiptSessionState session, ReceiptLineItemState item)
    {
        return session.Items
            .Where(candidate => string.Equals(candidate.GroupKey, item.GroupKey, StringComparison.Ordinal))
            .Select((candidate, index) => new { candidate.Id, Index = index + 1 })
            .First(entry => string.Equals(entry.Id, item.Id, StringComparison.Ordinal))
            .Index;
    }

    private static string GetSelectionDisplayName(ReceiptSessionState session, ReceiptLineItemState item)
    {
        var duplicateCount = session.Items.Count(candidate =>
            string.Equals(candidate.GroupKey, item.GroupKey, StringComparison.Ordinal));

        if (duplicateCount <= 1)
        {
            return item.Name;
        }

        return $"{item.Name} #{GetInstanceIndex(session, item)}";
    }

    private static string FormatMoney(decimal amount, string? currency)
    {
        return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(currency)
            ? $"${amount:0.00}"
            : $"{amount:0.00} {currency}";
    }
}

public sealed record ReceiptSelectionPanel(string Content, MessageComponent Components);
