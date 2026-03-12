using Discord;
using Discord.WebSocket;

public sealed class ReceiptInteractionService
{
    private const string SelectItemsButtonPrefix = "receipt-select-items";
    private const string SelectMenuPrefix = "receipt-item-menu";
    private const string PreviousPageButtonPrefix = "receipt-prev-page";
    private const string NextPageButtonPrefix = "receipt-next-page";
    private const string SelectItemsLabel = "Select Items";
    private const string ConfirmLabel = "Confirm";
    private const string PreviousPageLabel = "Previous Page";
    private const string NextPageLabel = "Next Page";

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

        var mergedItems = ReceiptItemMergeService.MergeForUi(ReceiptItemMergeService.BuildOcrItems(payload.Items));
        var session = ReceiptSessionStateService.CreateSession(
            receiptId,
            mergedItems,
            merchantName: payload.MerchantName,
            uploadedByUserId: payload.UploadedByUserId);

        var user = await _discordClient.Rest.GetUserAsync(userId);
        if (user is null)
        {
            throw new InvalidOperationException($"Discord user '{payload.UploadedByUserId}' could not be resolved.");
        }

        session.UserDisplayNames[user.Id.ToString()] = user.GlobalName ?? user.Username;

        var channel = targetChannel ?? await user.CreateDMChannelAsync(new RequestOptions { CancelToken = cancellationToken });
        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        var sentMessage = await channel.SendMessageAsync(
            embed: renderedMessage.Embed,
            components: BuildMainMessageComponents(session.ReceiptId),
            options: new RequestOptions { CancelToken = cancellationToken });

        session.MainMessageId = sentMessage.Id;
        session.MainChannelId = channel.Id;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _sessionStore.AddOrUpdate(session);

        _logger.LogInformation(
            "Receipt session created. ReceiptId={ReceiptId} UserId={UserId} MessageId={MessageId} ChannelId={ChannelId} MergedItemCount={MergedItemCount}",
            session.ReceiptId,
            payload.UploadedByUserId,
            sentMessage.Id,
            channel.Id,
            session.MergedItems.Count);
    }

    public async Task<string?> HandleButtonAsync(SocketMessageComponent component)
    {
        if (!component.Data.CustomId.StartsWith($"{SelectItemsButtonPrefix}:", StringComparison.Ordinal))
        {
            return await HandlePaginationButtonAsync(component);
        }

        if (!TryGetReceiptId(component.Data.CustomId, SelectItemsButtonPrefix, out var receiptId))
        {
            await component.RespondAsync("버튼 정보가 올바르지 않습니다.", ephemeral: true);
            return "invalid_receipt_button";
        }

        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        UpsertUserDisplayName(session, component.User);
        ReceiptSessionStateService.SetUserCurrentPage(session, component.User.Id.ToString(), 0);

        await component.RespondAsync(
            BuildSelectionPrompt(session, component.User.Id.ToString()),
            components: BuildSelectionMenuComponents(session, component.User.Id.ToString()),
            ephemeral: true);

        _logger.LogInformation("Select items menu opened. ReceiptId={ReceiptId} UserId={UserId}", receiptId, component.User.Id);
        return "selection_menu_opened";
    }

    public async Task<string?> HandleSelectMenuAsync(SocketMessageComponent component)
    {
        if (!component.Data.CustomId.StartsWith($"{SelectMenuPrefix}:", StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryGetReceiptId(component.Data.CustomId, SelectMenuPrefix, out var receiptId))
        {
            await component.RespondAsync("선택 메뉴 정보가 올바르지 않습니다.", ephemeral: true);
            return "invalid_select_menu";
        }

        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        UpsertUserDisplayName(session, component.User);
        var userId = component.User.Id.ToString();
        var currentPage = ReceiptSessionStateService.GetUserCurrentPage(session, userId);
        var pageItems = ReceiptSessionStateService.GetPageItems(session, currentPage);
        ReceiptSessionStateService.ReplaceSelectionsForPage(
            session,
            userId,
            pageItems.Select(item => item.Id).ToArray(),
            component.Data.Values);

        await RefreshMainMessageAsync(session);

        await component.UpdateAsync(properties =>
        {
            properties.Content = BuildSelectionPrompt(session, userId);
            properties.Components = BuildSelectionMenuComponents(session, userId);
        });

        _logger.LogInformation(
            "Receipt selections updated. ReceiptId={ReceiptId} UserId={UserId} SelectedItemCount={SelectedItemCount}",
            receiptId,
            component.User.Id,
            component.Data.Values.Count);

        return "selection_updated";
    }

    private static void UpsertUserDisplayName(ReceiptSessionState session, SocketUser user)
    {
        session.UserDisplayNames[user.Id.ToString()] = user.GlobalName ?? user.Username;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private async Task<string?> HandlePaginationButtonAsync(SocketMessageComponent component)
    {
        var isPrevious = component.Data.CustomId.StartsWith($"{PreviousPageButtonPrefix}:", StringComparison.Ordinal);
        var isNext = component.Data.CustomId.StartsWith($"{NextPageButtonPrefix}:", StringComparison.Ordinal);
        if (!isPrevious && !isNext)
        {
            return null;
        }

        var prefix = isPrevious ? PreviousPageButtonPrefix : NextPageButtonPrefix;
        if (!TryGetReceiptId(component.Data.CustomId, prefix, out var receiptId))
        {
            await component.RespondAsync("페이지 정보가 올바르지 않습니다.", ephemeral: true);
            return "invalid_page_button";
        }

        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync("해당 영수증 세션을 찾을 수 없습니다.", ephemeral: true);
            return "session_not_found";
        }

        var userId = component.User.Id.ToString();
        UpsertUserDisplayName(session, component.User);

        var currentPage = ReceiptSessionStateService.GetUserCurrentPage(session, userId);
        var nextPage = isPrevious ? currentPage - 1 : currentPage + 1;
        ReceiptSessionStateService.SetUserCurrentPage(session, userId, nextPage);

        await component.UpdateAsync(properties =>
        {
            properties.Content = BuildSelectionPrompt(session, userId);
            properties.Components = BuildSelectionMenuComponents(session, userId);
        });

        _logger.LogInformation(
            "Selection page changed. ReceiptId={ReceiptId} UserId={UserId} Page={Page}",
            receiptId,
            component.User.Id,
            ReceiptSessionStateService.GetUserCurrentPage(session, userId) + 1);

        return isPrevious ? "selection_page_previous" : "selection_page_next";
    }

    private async Task RefreshMainMessageAsync(ReceiptSessionState session)
    {
        if (session.MainChannelId is null || session.MainMessageId is null)
        {
            throw new InvalidOperationException("Receipt session is missing main message metadata.");
        }

        var channel = _discordClient.GetChannel(session.MainChannelId.Value) as IMessageChannel;
        if (channel is null)
        {
            channel = await _discordClient.Rest.GetChannelAsync(session.MainChannelId.Value) as IMessageChannel;
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
            properties.Components = BuildMainMessageComponents(session.ReceiptId);
        });
    }

    private static MessageComponent BuildMainMessageComponents(string receiptId)
    {
        return new ComponentBuilder()
            .WithButton(
                label: SelectItemsLabel,
                customId: $"{SelectItemsButtonPrefix}:{receiptId}",
                style: ButtonStyle.Primary)
            .WithButton(
                label: ConfirmLabel,
                customId: $"receipt-confirm:{receiptId}",
                style: ButtonStyle.Success,
                disabled: true)
            .Build();
    }

    private static MessageComponent BuildSelectionMenuComponents(ReceiptSessionState session, string userId)
    {
        var selectedItemIds = ReceiptSessionStateService.GetItemsForUser(session, userId)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var currentPage = ReceiptSessionStateService.GetUserCurrentPage(session, userId);
        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        var pageItems = ReceiptSessionStateService.GetPageItems(session, currentPage);

        var selectMenu = new SelectMenuBuilder()
            .WithCustomId($"{SelectMenuPrefix}:{session.ReceiptId}")
            .WithPlaceholder("Choose items you shared")
            .WithMinValues(0)
            .WithMaxValues(Math.Max(1, pageItems.Count));

        foreach (var item in pageItems)
        {
            selectMenu.AddOption(
                label: item.DisplayName,
                value: item.Id,
                description: item.TotalPrice is null ? null : $"Total ${item.TotalPrice:0.00}",
                isDefault: selectedItemIds.Contains(item.Id));
        }

        var builder = new ComponentBuilder()
            .WithSelectMenu(selectMenu, row: 0);

        if (totalPages > 1)
        {
            builder.WithButton(
                label: PreviousPageLabel,
                customId: $"{PreviousPageButtonPrefix}:{session.ReceiptId}",
                style: ButtonStyle.Secondary,
                disabled: currentPage == 0,
                row: 1);

            builder.WithButton(
                label: NextPageLabel,
                customId: $"{NextPageButtonPrefix}:{session.ReceiptId}",
                style: ButtonStyle.Secondary,
                disabled: currentPage >= totalPages - 1,
                row: 1);
        }

        return builder.Build();
    }

    private static string BuildSelectionPrompt(ReceiptSessionState session, string userId)
    {
        var currentPage = ReceiptSessionStateService.GetUserCurrentPage(session, userId);
        var totalPages = ReceiptSessionStateService.GetTotalPages(session);
        return $"Choose items you shared (Page {currentPage + 1}/{totalPages})";
    }

    private static bool TryGetReceiptId(string customId, string prefix, out string receiptId)
    {
        receiptId = string.Empty;
        var tokens = customId.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 2 || !string.Equals(tokens[0], prefix, StringComparison.Ordinal))
        {
            return false;
        }

        receiptId = tokens[1];
        return !string.IsNullOrWhiteSpace(receiptId);
    }
}
