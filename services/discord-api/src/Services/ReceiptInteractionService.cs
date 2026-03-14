using Discord;
using Discord.WebSocket;

public sealed class ReceiptInteractionService
{
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptMainMessageService _mainMessageService;

    public ReceiptInteractionService(
        ReceiptSessionStore sessionStore,
        ReceiptMainMessageService mainMessageService)
    {
        _sessionStore = sessionStore;
        _mainMessageService = mainMessageService;
    }

    public async Task<string?> HandleButtonAsync(SocketMessageComponent component)
    {
        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.SelectItemsButtonPrefix,
                out var selectReceiptId))
        {
            return await HandleOpenSelectionAsync(component, selectReceiptId, ReceiptSelectionMode.Assign, page: 0);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.AddItemButtonPrefix,
                out var addReceiptId))
        {
            return await HandleAddItemButtonAsync(component, addReceiptId);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.RemoveItemButtonPrefix,
                out var removeReceiptId))
        {
            return await HandleOpenSelectionAsync(component, removeReceiptId, ReceiptSelectionMode.Remove, page: 0);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.EditItemButtonPrefix,
                out var editReceiptId))
        {
            return await HandleOpenSelectionAsync(component, editReceiptId, ReceiptSelectionMode.Edit, page: 0);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.ConfirmButtonPrefix,
                out var confirmReceiptId))
        {
            return await HandleConfirmAsync(component, confirmReceiptId);
        }

        if (ReceiptInteractionCustomIds.TryParsePageButton(component.Data.CustomId, out var pageReceiptId, out var mode, out var page))
        {
            return await HandleOpenSelectionAsync(component, pageReceiptId, mode, page, updateExistingMessage: true);
        }

        return null;
    }

    public async Task<string?> HandleSelectMenuAsync(SocketMessageComponent component)
    {
        if (ReceiptInteractionCustomIds.TryParseSelectMenu(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.AssignSelectMenuPrefix,
                out var receiptId,
                out var page))
        {
            return await HandleAssignmentSelectionAsync(component, receiptId, page);
        }

        if (ReceiptInteractionCustomIds.TryParseSelectMenu(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.RemoveSelectMenuPrefix,
                out receiptId,
                out page))
        {
            return await HandleRemoveSelectionAsync(component, receiptId, page);
        }

        if (ReceiptInteractionCustomIds.TryParseSelectMenu(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.EditSelectMenuPrefix,
                out receiptId,
                out page))
        {
            return await HandleEditSelectionAsync(component, receiptId, page);
        }

        return null;
    }

    public async Task<string?> HandleModalAsync(SocketModal modal)
    {
        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                modal.Data.CustomId,
                ReceiptInteractionCustomIds.AddItemModalPrefix,
                out var addReceiptId))
        {
            return await HandleAddItemModalAsync(modal, addReceiptId);
        }

        if (ReceiptInteractionCustomIds.TryParseEditModal(modal.Data.CustomId, out var editReceiptId, out var editToken))
        {
            return await HandleEditItemModalAsync(modal, editReceiptId, editToken);
        }

        return null;
    }

    private async Task<string> HandleOpenSelectionAsync(
        SocketMessageComponent component,
        string receiptId,
        ReceiptSelectionMode mode,
        int page,
        bool updateExistingMessage = false)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await RespondOrUpdateAsync(component, updateExistingMessage, "해당 영수증 세션을 찾을 수 없습니다.", null);
            return "session_not_found";
        }

        if (!session.IsDraftReady && mode != ReceiptSelectionMode.Assign)
        {
            await RespondOrUpdateAsync(component, updateExistingMessage, "영수증 분석이 아직 끝나지 않았습니다.", null);
            return "draft_not_ready";
        }

        if ((mode == ReceiptSelectionMode.Remove || mode == ReceiptSelectionMode.Edit) &&
            !IsOwner(session, component.User.Id))
        {
            await RespondOrUpdateAsync(component, updateExistingMessage, "정산자만 이 기능을 사용할 수 있습니다.", null);
            return "forbidden_user";
        }

        UpsertUserDisplayName(session, component.User);
        _sessionStore.AddOrUpdate(session);

        await RespondOrUpdateAsync(
            component,
            updateExistingMessage,
            BuildSelectionPrompt(mode, session, page),
            BuildSelectionComponents(session, component.User.Id.ToString(), mode, page));

        return mode switch
        {
            ReceiptSelectionMode.Assign => "selection_menu_opened",
            ReceiptSelectionMode.Remove => "remove_menu_opened",
            ReceiptSelectionMode.Edit => "edit_menu_opened",
            _ => "menu_opened"
        };
    }

    private async Task<string> HandleAssignmentSelectionAsync(SocketMessageComponent component, string receiptId, int page)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        UpsertUserDisplayName(session, component.User);
        var pageItems = ReceiptSessionStateService.GetPageItems(session, page);
        ReceiptSessionStateService.ReplaceSelectionsForPage(
            session,
            component.User.Id.ToString(),
            pageItems.Select(item => item.Id).ToArray(),
            component.Data.Values);

        _sessionStore.AddOrUpdate(session);

        await component.UpdateAsync(properties =>
        {
            properties.Content = BuildSelectionPrompt(ReceiptSelectionMode.Assign, session, page);
            properties.Components = BuildSelectionComponents(session, component.User.Id.ToString(), ReceiptSelectionMode.Assign, page);
        });

        await _mainMessageService.PublishForComponentAsync(session, component);
        return "selection_updated";
    }

    private async Task<string> HandleRemoveSelectionAsync(SocketMessageComponent component, string receiptId, int page)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        if (!IsOwner(session, component.User.Id))
        {
            await component.RespondAsync("정산자만 아이템을 제거할 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        var itemId = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(itemId) || !ReceiptSessionStateService.RemoveItem(session, itemId))
        {
            await component.RespondAsync("제거할 아이템을 찾을 수 없습니다.", ephemeral: true);
            return "remove_item_not_found";
        }

        _sessionStore.AddOrUpdate(session);
        var nextPage = Math.Min(page, ReceiptSessionStateService.GetTotalPages(session) - 1);

        await component.UpdateAsync(properties =>
        {
            properties.Content = BuildSelectionPrompt(ReceiptSelectionMode.Remove, session, nextPage);
            properties.Components = BuildSelectionComponents(session, component.User.Id.ToString(), ReceiptSelectionMode.Remove, nextPage);
        });

        await _mainMessageService.PublishForComponentAsync(session, component);
        return "item_removed";
    }

    private async Task<string> HandleEditSelectionAsync(SocketMessageComponent component, string receiptId, int page)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        if (!IsOwner(session, component.User.Id))
        {
            await component.RespondAsync("정산자만 아이템을 수정할 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        var itemId = component.Data.Values.FirstOrDefault();
        var item = session.Items.SingleOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
        if (item is null)
        {
            await component.RespondAsync("수정할 아이템을 찾을 수 없습니다.", ephemeral: true);
            return "edit_item_not_found";
        }

        var modal = new ModalBuilder()
            .WithTitle("아이템 수정")
            .WithCustomId($"{ReceiptInteractionCustomIds.EditItemModalPrefix}:{receiptId}:{CreateEditToken(session, item.Id)}")
            .AddTextInput(
                label: "아이템 이름",
                customId: ReceiptInteractionCustomIds.ItemNameInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                value: item.Name,
                maxLength: 100)
            .AddTextInput(
                label: "아이템 가격",
                customId: ReceiptInteractionCustomIds.ItemPriceInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                value: item.Amount.ToString("0.00"),
                maxLength: 20);

        await component.RespondWithModalAsync(modal.Build());
        return "edit_modal_opened";
    }

    private async Task<string> HandleAddItemButtonAsync(SocketMessageComponent component, string receiptId)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        if (!IsOwner(session, component.User.Id))
        {
            await component.RespondAsync("정산자만 아이템을 추가할 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        var modal = new ModalBuilder()
            .WithTitle("아이템 추가")
            .WithCustomId($"{ReceiptInteractionCustomIds.AddItemModalPrefix}:{receiptId}")
            .AddTextInput(
                label: "아이템 이름",
                customId: ReceiptInteractionCustomIds.ItemNameInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                maxLength: 100)
            .AddTextInput(
                label: "아이템 가격",
                customId: ReceiptInteractionCustomIds.ItemPriceInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                placeholder: "예: 12.50",
                maxLength: 20)
            .AddTextInput(
                label: "수량",
                customId: ReceiptInteractionCustomIds.ItemQuantityInputCustomId,
                style: TextInputStyle.Short,
                required: false,
                placeholder: "기본값 1",
                value: "1",
                maxLength: 2);

        await component.RespondWithModalAsync(modal.Build());
        return "add_modal_opened";
    }

    private async Task<string> HandleAddItemModalAsync(SocketModal modal, string receiptId)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await modal.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        if (!IsOwner(session, modal.User.Id))
        {
            await modal.RespondAsync("정산자만 아이템을 추가할 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        var itemName = GetModalValue(modal, ReceiptInteractionCustomIds.ItemNameInputCustomId);
        var itemPriceText = GetModalValue(modal, ReceiptInteractionCustomIds.ItemPriceInputCustomId);
        var itemQuantityText = GetModalValue(modal, ReceiptInteractionCustomIds.ItemQuantityInputCustomId);

        if (string.IsNullOrWhiteSpace(itemName))
        {
            await modal.RespondAsync("아이템 이름을 입력해 주세요.", ephemeral: true);
            return "invalid_item_name";
        }

        if (!decimal.TryParse(itemPriceText, out var itemPrice) || itemPrice < 0)
        {
            await modal.RespondAsync("아이템 가격은 0 이상의 숫자로 입력해 주세요.", ephemeral: true);
            return "invalid_item_price";
        }

        var quantity = 1;
        if (!string.IsNullOrWhiteSpace(itemQuantityText) &&
            (!int.TryParse(itemQuantityText, out quantity) || quantity <= 0))
        {
            await modal.RespondAsync("수량은 1 이상의 정수로 입력해 주세요.", ephemeral: true);
            return "invalid_item_quantity";
        }

        await modal.DeferAsync(ephemeral: true);
        ReceiptSessionStateService.AddManualItem(session, itemName.Trim(), itemPrice, quantity);
        _sessionStore.AddOrUpdate(session);
        await _mainMessageService.PublishForModalAsync(session, modal);
        await modal.DeleteOriginalResponseAsync();

        return "item_added";
    }

    private async Task<string> HandleEditItemModalAsync(SocketModal modal, string receiptId, string editToken)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await modal.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        if (!IsOwner(session, modal.User.Id))
        {
            await modal.RespondAsync("정산자만 아이템을 수정할 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        if (!session.PendingEditItemIds.TryGetValue(editToken, out var itemId))
        {
            await modal.RespondAsync("수정 대상 아이템 정보를 찾을 수 없습니다. 다시 시도해 주세요.", ephemeral: true);
            return "edit_item_token_not_found";
        }

        var itemName = GetModalValue(modal, ReceiptInteractionCustomIds.ItemNameInputCustomId);
        var itemPriceText = GetModalValue(modal, ReceiptInteractionCustomIds.ItemPriceInputCustomId);

        if (string.IsNullOrWhiteSpace(itemName))
        {
            await modal.RespondAsync("아이템 이름을 입력해 주세요.", ephemeral: true);
            return "invalid_item_name";
        }

        if (!decimal.TryParse(itemPriceText, out var itemPrice) || itemPrice < 0)
        {
            await modal.RespondAsync("아이템 가격은 0 이상의 숫자로 입력해 주세요.", ephemeral: true);
            return "invalid_item_price";
        }

        if (!ReceiptSessionStateService.UpdateItem(session, itemId, itemName.Trim(), itemPrice))
        {
            await modal.RespondAsync("수정할 아이템을 찾을 수 없습니다.", ephemeral: true);
            return "edit_item_not_found";
        }

        session.PendingEditItemIds.Remove(editToken);
        await modal.DeferAsync(ephemeral: true);
        _sessionStore.AddOrUpdate(session);
        await _mainMessageService.PublishForModalAsync(session, modal);
        await modal.DeleteOriginalResponseAsync();

        return "item_edited";
    }

    private async Task<string> HandleConfirmAsync(SocketMessageComponent component, string receiptId)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        if (!IsOwner(session, component.User.Id))
        {
            await component.RespondAsync("confirm은 정산자만 누를 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        if (!ReceiptSessionStateService.CanConfirm(session))
        {
            await component.RespondAsync("Unassigned 아이템이 모두 배정되어야 confirm할 수 있습니다.", ephemeral: true);
            return "confirm_blocked";
        }

        await component.DeferAsync(ephemeral: true);
        session.IsConfirmed = true;
        session.ConfirmedAtUtc = DateTimeOffset.UtcNow;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _sessionStore.AddOrUpdate(session);
        await _mainMessageService.PublishForComponentAsync(session, component);
        await component.DeleteOriginalResponseAsync();

        return "confirmed";
    }

    private MessageComponent BuildSelectionComponents(
        ReceiptSessionState session,
        string userId,
        ReceiptSelectionMode mode,
        int page)
    {
        var safePage = Math.Clamp(page, 0, ReceiptSessionStateService.GetTotalPages(session) - 1);
        var pageItems = ReceiptSessionStateService.GetPageItems(session, safePage);
        var builder = new ComponentBuilder();

        if (pageItems.Count > 0)
        {
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId(ReceiptInteractionCustomIds.BuildSelectMenuCustomId(mode, session.ReceiptId, safePage))
                .WithPlaceholder(GetSelectPlaceholder(mode))
                .WithMinValues(mode == ReceiptSelectionMode.Assign ? 0 : 1)
                .WithMaxValues(mode == ReceiptSelectionMode.Assign ? Math.Max(1, pageItems.Count) : 1);

            var selectedIds = ReceiptSessionStateService.GetItemsForUser(session, userId)
                .Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var item in pageItems)
            {
                var instanceIndex = GetInstanceIndex(session, item);
                selectMenu.AddOption(
                    label: $"{item.Name} #{instanceIndex}",
                    value: item.Id,
                    description: $"{FormatMoney(item.Amount, session.Currency)}",
                    isDefault: mode == ReceiptSelectionMode.Assign && selectedIds.Contains(item.Id));
            }

            builder.WithSelectMenu(selectMenu, row: 0);
        }

        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        if (totalPages > 1)
        {
            builder.WithButton(
                "Previous Page",
                ReceiptInteractionCustomIds.BuildPageButtonCustomId(mode, session.ReceiptId, safePage - 1),
                ButtonStyle.Secondary,
                disabled: safePage == 0,
                row: 1);

            builder.WithButton(
                "Next Page",
                ReceiptInteractionCustomIds.BuildPageButtonCustomId(mode, session.ReceiptId, safePage + 1),
                ButtonStyle.Secondary,
                disabled: safePage >= totalPages - 1,
                row: 1);
        }

        return builder.Build();
    }

    private static async Task RespondOrUpdateAsync(
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

    private static string BuildSelectionPrompt(ReceiptSelectionMode mode, ReceiptSessionState session, int page)
    {
        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        var safePage = Math.Clamp(page, 0, totalPages - 1);

        return mode switch
        {
            ReceiptSelectionMode.Assign => $"아이템을 선택해서 정산에 참가하세요. (Page {safePage + 1}/{totalPages})",
            ReceiptSelectionMode.Remove => $"제거할 아이템을 선택하세요. (Page {safePage + 1}/{totalPages})",
            ReceiptSelectionMode.Edit => $"수정할 아이템을 선택하세요. (Page {safePage + 1}/{totalPages})",
            _ => $"아이템을 선택하세요. (Page {safePage + 1}/{totalPages})"
        };
    }

    private static string GetSelectPlaceholder(ReceiptSelectionMode mode)
    {
        return mode switch
        {
            ReceiptSelectionMode.Assign => "아이템 선택",
            ReceiptSelectionMode.Remove => "제거할 아이템 선택",
            ReceiptSelectionMode.Edit => "수정할 아이템 선택",
            _ => "아이템 선택"
        };
    }

    private static bool IsOwner(ReceiptSessionState session, ulong userId)
    {
        return string.Equals(session.UploadedByUserId, userId.ToString(), StringComparison.Ordinal);
    }

    private static void UpsertUserDisplayName(ReceiptSessionState session, SocketUser user)
    {
        session.UserDisplayNames[user.Id.ToString()] = user.GlobalName ?? user.Username;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string? GetModalValue(SocketModal modal, string customId)
    {
        return modal.Data.Components
            .FirstOrDefault(component => string.Equals(component.CustomId, customId, StringComparison.Ordinal))
            ?.Value;
    }

    private static string CreateEditToken(ReceiptSessionState session, string itemId)
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        session.PendingEditItemIds[token] = itemId;
        return token;
    }

    private static int GetInstanceIndex(ReceiptSessionState session, ReceiptLineItemState item)
    {
        return session.Items
            .Where(candidate => string.Equals(candidate.GroupKey, item.GroupKey, StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select((candidate, index) => new { candidate.Id, Index = index + 1 })
            .First(entry => string.Equals(entry.Id, item.Id, StringComparison.Ordinal))
            .Index;
    }

    private static string FormatMoney(decimal amount, string? currency)
    {
        return string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(currency)
            ? $"${amount:0.00}"
            : $"{amount:0.00} {currency}";
    }
}
