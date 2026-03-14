using Discord;
using Discord.WebSocket;

public sealed class ReceiptInteractionService
{
    private const string SelectItemsButtonPrefix = "receipt-select-items";
    private const string AddItemButtonPrefix = "receipt-add-item";
    private const string RemoveItemButtonPrefix = "receipt-remove-item";
    private const string EditItemButtonPrefix = "receipt-edit-item";
    private const string ConfirmButtonPrefix = "receipt-confirm";
    private const string PageButtonPrefix = "receipt-page";
    private const string AssignSelectMenuPrefix = "receipt-item-menu";
    private const string RemoveSelectMenuPrefix = "receipt-remove-menu";
    private const string EditSelectMenuPrefix = "receipt-edit-menu";
    private const string AddItemModalPrefix = "receipt-add-item-modal";
    private const string EditItemModalPrefix = "receipt-edit-item-modal";
    private const string ItemNameInputCustomId = "item_name";
    private const string ItemPriceInputCustomId = "item_price";
    private const string ItemQuantityInputCustomId = "item_quantity";

    private readonly DiscordSocketClient _discordClient;
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ILogger<ReceiptInteractionService> _logger;

    public ReceiptInteractionService(
        DiscordSocketClient discordClient,
        ReceiptSessionStore sessionStore,
        ILogger<ReceiptInteractionService> logger)
    {
        _discordClient = discordClient;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task CreatePendingUploadSessionAsync(
        string blobUrl,
        string uploadedByUserId,
        string uploadedByDisplayName,
        string? paymentContact,
        IMessageChannel targetChannel,
        CancellationToken cancellationToken)
    {
        var tempReceiptId = $"pending-{Guid.NewGuid():N}";
        var session = ReceiptSessionStateService.CreatePendingUploadSession(
            tempReceiptId,
            blobUrl,
            uploadedByUserId,
            uploadedByDisplayName,
            paymentContact);

        session.UserDisplayNames[uploadedByUserId] = uploadedByDisplayName;

        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        var sentMessage = await targetChannel.SendMessageAsync(
            embed: renderedMessage.Embed,
            components: BuildMainMessageComponents(session),
            options: new RequestOptions { CancelToken = cancellationToken });

        session.MainMessageId = sentMessage.Id;
        session.MainChannelId = targetChannel.Id;
        session.MainChannel = targetChannel;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _sessionStore.AddOrUpdate(session);

        _logger.LogInformation(
            "Pending receipt session created. ReceiptId={ReceiptId} UserId={UserId} ChannelId={ChannelId} MessageId={MessageId}",
            session.ReceiptId,
            uploadedByUserId,
            targetChannel.Id,
            sentMessage.Id);
    }

    public async Task CreateOrUpdateSessionFromDraftAsync(
        ReceiptDraftNotificationRequest payload,
        CancellationToken cancellationToken,
        IMessageChannel? targetChannel = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var receiptId = payload.ResolvedDraftId
            ?? throw new InvalidOperationException("draftId is required.");

        if (!ulong.TryParse(payload.UploadedByUserId, out var userId))
        {
            throw new InvalidOperationException("uploadedByUserId must be a valid Discord user id.");
        }

        var user = await _discordClient.Rest.GetUserAsync(userId);
        if (user is null)
        {
            throw new InvalidOperationException($"Discord user '{payload.UploadedByUserId}' could not be resolved.");
        }

        var displayName = user.GlobalName ?? user.Username;

        ReceiptSessionState session;
        string? previousReceiptId = null;
        string? previousBlobUrl = null;

        if (!string.IsNullOrWhiteSpace(payload.BlobUrl) &&
            _sessionStore.TryGetByBlobUrl(payload.BlobUrl, out var existingByBlob) &&
            existingByBlob is not null)
        {
            session = existingByBlob;
            previousReceiptId = session.ReceiptId;
            previousBlobUrl = session.BlobUrl;
            ReceiptSessionStateService.ApplyDraftPayload(session, payload, displayName);
        }
        else if (_sessionStore.TryGet(receiptId, out var existingByReceiptId) &&
                 existingByReceiptId is not null)
        {
            session = existingByReceiptId;
            previousBlobUrl = session.BlobUrl;
            ReceiptSessionStateService.ApplyDraftPayload(session, payload, displayName);
        }
        else
        {
            session = ReceiptSessionStateService.CreateSessionFromDraft(payload, displayName);
        }

        session.UserDisplayNames[user.Id.ToString()] = displayName;
        session.UploadedByDisplayName = displayName;

        if (session.MainChannelId is not null && session.MainMessageId is not null)
        {
            await RefreshMainMessageAsync(session);
        }
        else
        {
            var channel = targetChannel;
            if (channel is null)
            {
                throw new InvalidOperationException("기존 채널 세션이 없으면 draft 메시지를 채널에 보낼 수 없습니다.");
            }

            var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
            var sentMessage = await channel.SendMessageAsync(
                embed: renderedMessage.Embed,
                components: BuildMainMessageComponents(session),
                options: new RequestOptions { CancelToken = cancellationToken });

            session.MainMessageId = sentMessage.Id;
            session.MainChannelId = channel.Id;
            session.MainChannel = channel;
        }

        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _sessionStore.AddOrUpdate(session, previousReceiptId, previousBlobUrl);

        _logger.LogInformation(
            "Receipt session upserted from draft. ReceiptId={ReceiptId} UserId={UserId} ChannelId={ChannelId} MessageId={MessageId} ItemCount={ItemCount}",
            session.ReceiptId,
            payload.UploadedByUserId,
            session.MainChannelId,
            session.MainMessageId,
            session.Items.Count);
    }

    public async Task CreateOrUpdateSessionFromDraftAsync(
        ReceiptDraftNotificationRequest payload,
        SocketSlashCommand command,
        IMessageChannel? targetChannel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var receiptId = payload.ResolvedDraftId
            ?? throw new InvalidOperationException("draftId is required.");

        if (!ulong.TryParse(payload.UploadedByUserId, out var userId))
        {
            throw new InvalidOperationException("uploadedByUserId must be a valid Discord user id.");
        }

        var commandChannel = targetChannel ?? ResolveSlashCommandChannel(command);
        var user = await _discordClient.Rest.GetUserAsync(userId)
            ?? throw new InvalidOperationException($"Discord user '{payload.UploadedByUserId}' could not be resolved.");

        var displayName = user.GlobalName ?? user.Username;

        ReceiptSessionState session;
        string? previousReceiptId = null;
        string? previousBlobUrl = null;

        if (!string.IsNullOrWhiteSpace(payload.BlobUrl) &&
            _sessionStore.TryGetByBlobUrl(payload.BlobUrl, out var existingByBlob) &&
            existingByBlob is not null)
        {
            session = existingByBlob;
            previousReceiptId = session.ReceiptId;
            previousBlobUrl = session.BlobUrl;
            ReceiptSessionStateService.ApplyDraftPayload(session, payload, displayName);
            await RefreshMainMessageAsync(session);
        }
        else if (_sessionStore.TryGet(receiptId, out var existingByReceiptId) &&
                 existingByReceiptId is not null)
        {
            session = existingByReceiptId;
            previousBlobUrl = session.BlobUrl;
            ReceiptSessionStateService.ApplyDraftPayload(session, payload, displayName);
            await RefreshMainMessageAsync(session);
        }
        else
        {
            session = ReceiptSessionStateService.CreateSessionFromDraft(payload, displayName);
            session.UserDisplayNames[user.Id.ToString()] = displayName;
            session.UploadedByDisplayName = displayName;
            session.MainChannel = commandChannel;

            var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
            var sentMessage = await command.FollowupAsync(
                embed: renderedMessage.Embed,
                components: BuildMainMessageComponents(session),
                ephemeral: false);

            session.MainMessageId = sentMessage.Id;
            session.MainChannelId = command.ChannelId;
            session.MainChannel = sentMessage.Channel as IMessageChannel ?? commandChannel;
        }

        session.UserDisplayNames[user.Id.ToString()] = displayName;
        session.UploadedByDisplayName = displayName;
        session.MainChannel ??= commandChannel;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _sessionStore.AddOrUpdate(session, previousReceiptId, previousBlobUrl);
    }

    public async Task<string?> HandleButtonAsync(SocketMessageComponent component)
    {
        if (TryGetReceiptId(component.Data.CustomId, SelectItemsButtonPrefix, out var selectReceiptId))
        {
            return await HandleOpenSelectionAsync(component, selectReceiptId, mode: SelectionMode.Assign, page: 0);
        }

        if (TryGetReceiptId(component.Data.CustomId, AddItemButtonPrefix, out var addReceiptId))
        {
            return await HandleAddItemButtonAsync(component, addReceiptId);
        }

        if (TryGetReceiptId(component.Data.CustomId, RemoveItemButtonPrefix, out var removeReceiptId))
        {
            return await HandleOpenSelectionAsync(component, removeReceiptId, mode: SelectionMode.Remove, page: 0);
        }

        if (TryGetReceiptId(component.Data.CustomId, EditItemButtonPrefix, out var editReceiptId))
        {
            return await HandleOpenSelectionAsync(component, editReceiptId, mode: SelectionMode.Edit, page: 0);
        }

        if (TryGetReceiptId(component.Data.CustomId, ConfirmButtonPrefix, out var confirmReceiptId))
        {
            return await HandleConfirmAsync(component, confirmReceiptId);
        }

        if (TryParsePageButton(component.Data.CustomId, out var pageReceiptId, out var mode, out var page))
        {
            return await HandleOpenSelectionAsync(component, pageReceiptId, mode, page, updateExistingMessage: true);
        }

        return null;
    }

    public async Task<string?> HandleSelectMenuAsync(SocketMessageComponent component)
    {
        if (TryParseSelectMenu(component.Data.CustomId, AssignSelectMenuPrefix, out var receiptId, out var page))
        {
            return await HandleAssignmentSelectionAsync(component, receiptId, page);
        }

        if (TryParseSelectMenu(component.Data.CustomId, RemoveSelectMenuPrefix, out receiptId, out page))
        {
            return await HandleRemoveSelectionAsync(component, receiptId, page);
        }

        if (TryParseSelectMenu(component.Data.CustomId, EditSelectMenuPrefix, out receiptId, out page))
        {
            return await HandleEditSelectionAsync(component, receiptId, page);
        }

        return null;
    }

    public async Task<string?> HandleModalAsync(SocketModal modal)
    {
        if (TryGetReceiptId(modal.Data.CustomId, AddItemModalPrefix, out var addReceiptId))
        {
            return await HandleAddItemModalAsync(modal, addReceiptId);
        }

        if (TryParseEditModal(modal.Data.CustomId, out var editReceiptId, out var itemId))
        {
            return await HandleEditItemModalAsync(modal, editReceiptId, itemId);
        }

        return null;
    }

    private async Task<string> HandleOpenSelectionAsync(
        SocketMessageComponent component,
        string receiptId,
        SelectionMode mode,
        int page,
        bool updateExistingMessage = false)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await RespondOrUpdateAsync(component, updateExistingMessage, "해당 영수증 세션을 찾을 수 없습니다.", null);
            return "session_not_found";
        }

        if (!session.IsDraftReady && mode != SelectionMode.Assign)
        {
            await RespondOrUpdateAsync(component, updateExistingMessage, "영수증 분석이 아직 끝나지 않았습니다.", null);
            return "draft_not_ready";
        }

        if ((mode == SelectionMode.Remove || mode == SelectionMode.Edit) &&
            !IsOwner(session, component.User.Id))
        {
            await RespondOrUpdateAsync(component, updateExistingMessage, "정산자만 이 기능을 사용할 수 있습니다.", null);
            return "forbidden_user";
        }

        UpsertUserDisplayName(session, component.User);
        var prompt = BuildSelectionPrompt(mode, session, page);
        var components = BuildSelectionComponents(session, component.User.Id.ToString(), mode, page);

        await RespondOrUpdateAsync(component, updateExistingMessage, prompt, components);
        return mode switch
        {
            SelectionMode.Assign => "selection_menu_opened",
            SelectionMode.Remove => "remove_menu_opened",
            SelectionMode.Edit => "edit_menu_opened",
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
            properties.Content = BuildSelectionPrompt(SelectionMode.Assign, session, page);
            properties.Components = BuildSelectionComponents(session, component.User.Id.ToString(), SelectionMode.Assign, page);
        });

        await PublishMainMessageAsync(session, component);

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
            properties.Content = BuildSelectionPrompt(SelectionMode.Remove, session, nextPage);
            properties.Components = BuildSelectionComponents(session, component.User.Id.ToString(), SelectionMode.Remove, nextPage);
        });

        await PublishMainMessageAsync(session, component);

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
            .WithCustomId($"{EditItemModalPrefix}:{receiptId}:{itemId}")
            .AddTextInput(
                label: "아이템 이름",
                customId: ItemNameInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                value: item.Name,
                maxLength: 100)
            .AddTextInput(
                label: "아이템 가격",
                customId: ItemPriceInputCustomId,
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
            .WithCustomId($"{AddItemModalPrefix}:{receiptId}")
            .AddTextInput(
                label: "아이템 이름",
                customId: ItemNameInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                maxLength: 100)
            .AddTextInput(
                label: "아이템 가격",
                customId: ItemPriceInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                placeholder: "예: 12.50",
                maxLength: 20)
            .AddTextInput(
                label: "수량",
                customId: ItemQuantityInputCustomId,
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

        var itemName = GetModalValue(modal, ItemNameInputCustomId);
        var itemPriceText = GetModalValue(modal, ItemPriceInputCustomId);
        var itemQuantityText = GetModalValue(modal, ItemQuantityInputCustomId);

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
        await PublishMainMessageAsync(session, modal);

        await modal.DeleteOriginalResponseAsync();
        return "item_added";
    }

    private async Task<string> HandleEditItemModalAsync(SocketModal modal, string receiptId, string itemId)
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

        var itemName = GetModalValue(modal, ItemNameInputCustomId);
        var itemPriceText = GetModalValue(modal, ItemPriceInputCustomId);
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

        await modal.DeferAsync(ephemeral: true);
        _sessionStore.AddOrUpdate(session);
        await PublishMainMessageAsync(session, modal);

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
        await PublishMainMessageAsync(session, component);

        await component.DeleteOriginalResponseAsync();
        return "confirmed";
    }

    private async Task RefreshMainMessageAsync(
        ReceiptSessionState session,
        IUserMessage? knownMessage = null,
        IMessageChannel? fallbackChannel = null)
    {
        if (knownMessage is not null &&
            session.MainMessageId is not null &&
            knownMessage.Id == session.MainMessageId.Value)
        {
            var renderedKnownMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
            await knownMessage.ModifyAsync(properties =>
            {
                properties.Embed = renderedKnownMessage.Embed;
                properties.Components = BuildMainMessageComponents(session);
            });
            return;
        }

        await RefreshMainMessageAsync(
            session,
            fallbackChannel ?? knownMessage?.Channel as IMessageChannel);
    }

    private async Task RefreshMainMessageAsync(
        ReceiptSessionState session,
        IMessageChannel? fallbackChannel)
    {
        if (session.MainChannelId is null || session.MainMessageId is null)
        {
            throw new InvalidOperationException("Receipt session is missing main message metadata.");
        }

        var channel = fallbackChannel;
        if (channel is null && session.MainChannel is not null)
        {
            channel = session.MainChannel;
        }

        if (channel is null && _discordClient.GetChannel(session.MainChannelId.Value) is IMessageChannel cachedChannel)
        {
            channel = cachedChannel;
        }

        if (channel is null)
        {
            try
            {
                channel = await _discordClient.Rest.GetChannelAsync(session.MainChannelId.Value) as IMessageChannel;
            }
            catch (Discord.Net.HttpException ex) when ((int?)ex.DiscordCode == 50001)
            {
                throw new InvalidOperationException(
                    $"메인 메시지 채널에 접근할 수 없습니다. ChannelId={session.MainChannelId.Value}",
                    ex);
            }
        }

        if (channel is null)
        {
            throw new InvalidOperationException("Main message channel could not be resolved.");
        }

        var message = await channel.GetMessageAsync(session.MainMessageId.Value) as IUserMessage;
        if (message is null)
        {
            throw new InvalidOperationException("Main message could not be resolved.");
        }

        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        await message.ModifyAsync(properties =>
        {
            properties.Embed = renderedMessage.Embed;
            properties.Components = BuildMainMessageComponents(session);
        });
        session.MainChannel = channel;
    }

    private MessageComponent BuildMainMessageComponents(ReceiptSessionState session)
    {
        if (!session.IsDraftReady || session.IsConfirmed)
        {
            return new ComponentBuilder().Build();
        }

        return new ComponentBuilder()
            .WithButton("Select Item", $"{SelectItemsButtonPrefix}:{session.ReceiptId}", ButtonStyle.Primary, row: 0)
            .WithButton("Add item", $"{AddItemButtonPrefix}:{session.ReceiptId}", ButtonStyle.Secondary, row: 0)
            .WithButton("Remove item", $"{RemoveItemButtonPrefix}:{session.ReceiptId}", ButtonStyle.Secondary, row: 0)
            .WithButton("Edit item", $"{EditItemButtonPrefix}:{session.ReceiptId}", ButtonStyle.Secondary, row: 0)
            .WithButton("Confirm", $"{ConfirmButtonPrefix}:{session.ReceiptId}", ButtonStyle.Success, disabled: !ReceiptSessionStateService.CanConfirm(session), row: 1)
            .Build();
    }

    private MessageComponent BuildSelectionComponents(
        ReceiptSessionState session,
        string userId,
        SelectionMode mode,
        int page)
    {
        var safePage = Math.Clamp(page, 0, ReceiptSessionStateService.GetTotalPages(session) - 1);
        var pageItems = ReceiptSessionStateService.GetPageItems(session, safePage);
        var builder = new ComponentBuilder();

        if (pageItems.Count > 0)
        {
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId(BuildSelectMenuCustomId(mode, session.ReceiptId, safePage))
                .WithPlaceholder(GetSelectPlaceholder(mode))
                .WithMinValues(mode == SelectionMode.Assign ? 0 : 1)
                .WithMaxValues(mode == SelectionMode.Assign ? Math.Max(1, pageItems.Count) : 1);

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
                    isDefault: mode == SelectionMode.Assign && selectedIds.Contains(item.Id));
            }

            builder.WithSelectMenu(selectMenu, row: 0);
        }

        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        if (totalPages > 1)
        {
            builder.WithButton(
                "Previous Page",
                BuildPageButtonCustomId(mode, session.ReceiptId, safePage - 1),
                ButtonStyle.Secondary,
                disabled: safePage == 0,
                row: 1);

            builder.WithButton(
                "Next Page",
                BuildPageButtonCustomId(mode, session.ReceiptId, safePage + 1),
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

    private static string BuildSelectionPrompt(SelectionMode mode, ReceiptSessionState session, int page)
    {
        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        var safePage = Math.Clamp(page, 0, totalPages - 1);
        return mode switch
        {
            SelectionMode.Assign => $"아이템을 선택해서 정산에 참가하세요. (Page {safePage + 1}/{totalPages})",
            SelectionMode.Remove => $"제거할 아이템을 선택하세요. (Page {safePage + 1}/{totalPages})",
            SelectionMode.Edit => $"수정할 아이템을 선택하세요. (Page {safePage + 1}/{totalPages})",
            _ => $"아이템을 선택하세요. (Page {safePage + 1}/{totalPages})"
        };
    }

    private static string GetSelectPlaceholder(SelectionMode mode)
    {
        return mode switch
        {
            SelectionMode.Assign => "아이템 선택",
            SelectionMode.Remove => "제거할 아이템 선택",
            SelectionMode.Edit => "수정할 아이템 선택",
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

    private static string BuildPageButtonCustomId(SelectionMode mode, string receiptId, int page)
    {
        return $"{PageButtonPrefix}:{mode.ToString().ToLowerInvariant()}:{receiptId}:{page}";
    }

    private static string BuildSelectMenuCustomId(SelectionMode mode, string receiptId, int page)
    {
        var prefix = mode switch
        {
            SelectionMode.Assign => AssignSelectMenuPrefix,
            SelectionMode.Remove => RemoveSelectMenuPrefix,
            SelectionMode.Edit => EditSelectMenuPrefix,
            _ => AssignSelectMenuPrefix
        };

        return $"{prefix}:{receiptId}:{page}";
    }

    private static bool TryParsePageButton(string customId, out string receiptId, out SelectionMode mode, out int page)
    {
        receiptId = string.Empty;
        mode = SelectionMode.Assign;
        page = 0;

        var parts = customId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], PageButtonPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Enum.TryParse<SelectionMode>(parts[1], ignoreCase: true, out mode) ||
            !int.TryParse(parts[3], out page))
        {
            return false;
        }

        receiptId = parts[2];
        return !string.IsNullOrWhiteSpace(receiptId);
    }

    private static bool TryParseSelectMenu(string customId, string prefix, out string receiptId, out int page)
    {
        receiptId = string.Empty;
        page = 0;

        var parts = customId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], prefix, StringComparison.Ordinal))
        {
            return false;
        }

        receiptId = parts[1];
        return !string.IsNullOrWhiteSpace(receiptId) && int.TryParse(parts[2], out page);
    }

    private static bool TryGetReceiptId(string customId, string prefix, out string receiptId)
    {
        receiptId = string.Empty;
        var parts = customId.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !string.Equals(parts[0], prefix, StringComparison.Ordinal))
        {
            return false;
        }

        receiptId = parts[1];
        return !string.IsNullOrWhiteSpace(receiptId);
    }

    private static bool TryParseEditModal(string customId, out string receiptId, out string itemId)
    {
        receiptId = string.Empty;
        itemId = string.Empty;

        var parts = customId.Split(':', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], EditItemModalPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        receiptId = parts[1];
        itemId = parts[2];
        return !string.IsNullOrWhiteSpace(receiptId) && !string.IsNullOrWhiteSpace(itemId);
    }

    private static string? GetModalValue(SocketModal modal, string customId)
    {
        return modal.Data.Components
            .FirstOrDefault(component => string.Equals(component.CustomId, customId, StringComparison.Ordinal))
            ?.Value;
    }

    private static IMessageChannel? ResolveInteractionChannel(SocketMessageComponent component)
    {
        return component.Channel as IMessageChannel
            ?? component.Message.Channel as IMessageChannel;
    }

    private static IMessageChannel? ResolveInteractionChannel(SocketModal modal)
    {
        return modal.Channel as IMessageChannel;
    }

    private IMessageChannel? ResolveSlashCommandChannel(SocketSlashCommand command)
    {
        if (command.Channel is IMessageChannel directChannel)
        {
            return directChannel;
        }

        if (command.ChannelId is ulong channelId &&
            _discordClient.GetChannel(channelId) is IMessageChannel cachedChannel)
        {
            return cachedChannel;
        }

        return null;
    }

    private async Task PublishMainMessageAsync(ReceiptSessionState session, SocketMessageComponent component)
    {
        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        var replacement = await component.FollowupAsync(
            embed: renderedMessage.Embed,
            components: BuildMainMessageComponents(session),
            ephemeral: false);

        session.MainMessageId = replacement.Id;
        session.MainChannelId = replacement.Channel?.Id
            ?? session.MainChannelId
            ?? ResolveInteractionChannel(component)?.Id;
        session.MainChannel = replacement.Channel as IMessageChannel
            ?? session.MainChannel
            ?? ResolveInteractionChannel(component);
        _sessionStore.AddOrUpdate(session);
    }

    private async Task PublishMainMessageAsync(ReceiptSessionState session, SocketModal modal)
    {
        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        var interactionChannel = ResolveInteractionChannel(modal);
        var replacement = await modal.FollowupAsync(
            embed: renderedMessage.Embed,
            components: BuildMainMessageComponents(session),
            ephemeral: false);

        session.MainMessageId = replacement.Id;
        session.MainChannelId = replacement.Channel?.Id
            ?? interactionChannel?.Id
            ?? session.MainChannelId;
        session.MainChannel = replacement.Channel as IMessageChannel
            ?? interactionChannel
            ?? session.MainChannel;
        _sessionStore.AddOrUpdate(session);
    }

    private static async Task AcknowledgeAndDeleteAsync(SocketMessageComponent component)
    {
        await component.DeferAsync(ephemeral: true);
        await component.DeleteOriginalResponseAsync();
    }

    private static async Task AcknowledgeAndDeleteAsync(SocketModal modal)
    {
        await modal.DeferAsync(ephemeral: true);
        await modal.DeleteOriginalResponseAsync();
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

    private enum SelectionMode
    {
        Assign,
        Remove,
        Edit
    }
}
