using Discord;
using Discord.WebSocket;

public sealed class ReceiptInteractionService
{
    private static readonly TimeSpan[] HistorySaveRetryDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1500)
    ];

    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptSessionLockManager _lockManager;
    private readonly ReceiptMainMessageService _mainMessageService;
    private readonly ReceiptMainMessageDebounceService _debounceService;
    private readonly SettlementHistoryRepositoryProvider _settlementHistoryRepositoryProvider;
    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<ReceiptInteractionService> _logger;

    public ReceiptInteractionService(
        ReceiptSessionStore sessionStore,
        ReceiptSessionLockManager lockManager,
        ReceiptMainMessageService mainMessageService,
        ReceiptMainMessageDebounceService debounceService,
        SettlementHistoryRepositoryProvider settlementHistoryRepositoryProvider,
        UserLanguagePreferenceStore languagePreferenceStore,
        ILogger<ReceiptInteractionService> logger)
    {
        _sessionStore = sessionStore;
        _lockManager = lockManager;
        _mainMessageService = mainMessageService;
        _debounceService = debounceService;
        _settlementHistoryRepositoryProvider = settlementHistoryRepositoryProvider;
        _languagePreferenceStore = languagePreferenceStore;
        _logger = logger;
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
                ReceiptInteractionCustomIds.MarkAlcoholButtonPrefix,
                out var alcoholReceiptId))
        {
            return await HandleOpenSelectionAsync(component, alcoholReceiptId, ReceiptSelectionMode.Alcohol, page: 0);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.TipModeProportionalButtonPrefix,
                out var tipProportionalReceiptId))
        {
            return await HandleTipModeChangeAsync(component, tipProportionalReceiptId, TipSplitMode.Proportional);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.TipModeEqualButtonPrefix,
                out var tipEqualReceiptId))
        {
            return await HandleTipModeChangeAsync(component, tipEqualReceiptId, TipSplitMode.Equal);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.ConfirmButtonPrefix,
                out var confirmReceiptId))
        {
            return await HandleConfirmAsync(component, confirmReceiptId);
        }

        if (ReceiptInteractionCustomIds.TryGetReceiptId(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.CancelButtonPrefix,
                out var cancelReceiptId))
        {
            return await HandleCancelAsync(component, cancelReceiptId);
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

        if (ReceiptInteractionCustomIds.TryParseSelectMenu(
                component.Data.CustomId,
                ReceiptInteractionCustomIds.AlcoholSelectMenuPrefix,
                out receiptId,
                out page))
        {
            return await HandleAlcoholSelectionAsync(component, receiptId, page);
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
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await RespondOrUpdateAsync(component, updateExistingMessage, DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), null);
                return "session_not_found";
            }

            if (!session.IsDraftReady && mode != ReceiptSelectionMode.Assign)
            {
                await RespondOrUpdateAsync(component, updateExistingMessage, DiscordUiText.DraftNotReady(GetLanguage(component.User.Id)), null);
                return "draft_not_ready";
            }

            if ((mode == ReceiptSelectionMode.Remove || mode == ReceiptSelectionMode.Edit || mode == ReceiptSelectionMode.Alcohol) &&
                !IsOwner(session, component.User.Id))
            {
                await RespondOrUpdateAsync(component, updateExistingMessage, DiscordUiText.OwnerOnlyFeature(GetLanguage(component.User.Id)), null);
                return "forbidden_user";
            }

            UpsertUserDisplayName(session, component.User);
            _sessionStore.AddOrUpdate(session);

            if (!updateExistingMessage)
            {
                await ReplaceExistingPrivatePanelAsync(session, component.User.Id, mode);
            }

            await RespondOrUpdateAsync(
                component,
                updateExistingMessage,
                BuildSelectionPrompt(mode, page, ReceiptSessionStateService.GetTotalPages(session), GetLanguage(component.User.Id)),
                BuildSelectionComponents(session, component.User.Id.ToString(), mode, page));

            if (!updateExistingMessage)
            {
                session.ActivePrivatePanelInteractions[BuildPrivatePanelKey(component.User.Id, mode)] = component;
                _sessionStore.AddOrUpdate(session);
            }

            return mode switch
            {
                ReceiptSelectionMode.Assign => "selection_menu_opened",
                ReceiptSelectionMode.Remove => "remove_menu_opened",
                ReceiptSelectionMode.Edit => "edit_menu_opened",
                ReceiptSelectionMode.Alcohol => "alcohol_menu_opened",
                _ => "menu_opened"
            };
        });
    }

    private async Task<string> HandleAssignmentSelectionAsync(SocketMessageComponent component, string receiptId, int page)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
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
                properties.Content = BuildSelectionPrompt(ReceiptSelectionMode.Assign, page, ReceiptSessionStateService.GetTotalPages(session), GetLanguage(component.User.Id));
                properties.Components = BuildSelectionComponents(session, component.User.Id.ToString(), ReceiptSelectionMode.Assign, page);
            });

            _debounceService.ScheduleRefresh(receiptId);
            return "selection_updated";
        });
    }

    private async Task<string> HandleRemoveSelectionAsync(SocketMessageComponent component, string receiptId, int page)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, component.User.Id))
            {
                await component.RespondAsync(DiscordUiText.OwnerOnlyRemove(GetLanguage(component.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            var itemId = component.Data.Values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(itemId) || !ReceiptSessionStateService.RemoveItem(session, itemId))
            {
                await component.RespondAsync(DiscordUiText.RemoveItemNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "remove_item_not_found";
            }

            _sessionStore.AddOrUpdate(session);
            var nextPage = Math.Min(page, ReceiptSessionStateService.GetTotalPages(session) - 1);

            await component.UpdateAsync(properties =>
            {
                properties.Content = BuildSelectionPrompt(ReceiptSelectionMode.Remove, nextPage, ReceiptSessionStateService.GetTotalPages(session), GetLanguage(component.User.Id));
                properties.Components = BuildSelectionComponents(session, component.User.Id.ToString(), ReceiptSelectionMode.Remove, nextPage);
            });

            _debounceService.ScheduleRefresh(receiptId);
            return "item_removed";
        });
    }

    private async Task<string> HandleEditSelectionAsync(SocketMessageComponent component, string receiptId, int page)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, component.User.Id))
            {
                await component.RespondAsync(DiscordUiText.OwnerOnlyEdit(GetLanguage(component.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            var itemId = component.Data.Values.FirstOrDefault();
            var item = session.Items.SingleOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
            if (item is null)
            {
                await component.RespondAsync(DiscordUiText.EditItemNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "edit_item_not_found";
            }

            var language = GetLanguage(component.User.Id);
            var modal = new ModalBuilder()
                .WithTitle(DiscordUiText.EditItemModalTitle(language))
                .WithCustomId($"{ReceiptInteractionCustomIds.EditItemModalPrefix}:{receiptId}:{CreateEditToken(session, item.Id)}")
                .AddTextInput(
                    label: DiscordUiText.ItemNameLabel(language),
                    customId: ReceiptInteractionCustomIds.ItemNameInputCustomId,
                    style: TextInputStyle.Short,
                    required: true,
                    value: item.Name,
                    maxLength: 100)
                .AddTextInput(
                    label: DiscordUiText.ItemPriceLabel(language),
                    customId: ReceiptInteractionCustomIds.ItemPriceInputCustomId,
                    style: TextInputStyle.Short,
                    required: true,
                    value: item.Amount.ToString("0.00"),
                    maxLength: 20);

            await component.RespondWithModalAsync(modal.Build());
            return "edit_modal_opened";
        });
    }

    private async Task<string> HandleAlcoholSelectionAsync(SocketMessageComponent component, string receiptId, int page)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, component.User.Id))
            {
                await component.RespondAsync(DiscordUiText.OwnerOnlyAlcohol(GetLanguage(component.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            var pageItems = ReceiptSessionStateService.GetPageItems(session, page);
            ReceiptSessionStateService.ReplaceAlcoholFlagsForPage(
                session,
                pageItems.Select(item => item.Id).ToArray(),
                component.Data.Values);

            _sessionStore.AddOrUpdate(session);

            await component.UpdateAsync(properties =>
            {
                properties.Content = BuildSelectionPrompt(ReceiptSelectionMode.Alcohol, page, ReceiptSessionStateService.GetTotalPages(session), GetLanguage(component.User.Id));
                properties.Components = BuildSelectionComponents(session, component.User.Id.ToString(), ReceiptSelectionMode.Alcohol, page);
            });

            _debounceService.ScheduleRefresh(receiptId);
            return "alcohol_selection_updated";
        });
    }

    private async Task<string> HandleAddItemButtonAsync(SocketMessageComponent component, string receiptId)
    {
        if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
        {
            await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
            return "session_not_found";
        }

        if (!IsOwner(session, component.User.Id))
        {
            await component.RespondAsync(DiscordUiText.OwnerOnlyAdd(GetLanguage(component.User.Id)), ephemeral: true);
            return "forbidden_user";
        }

        var language = GetLanguage(component.User.Id);
        var modal = new ModalBuilder()
            .WithTitle(DiscordUiText.AddItemModalTitle(language))
            .WithCustomId($"{ReceiptInteractionCustomIds.AddItemModalPrefix}:{receiptId}")
            .AddTextInput(
                label: DiscordUiText.ItemNameLabel(language),
                customId: ReceiptInteractionCustomIds.ItemNameInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                maxLength: 100)
            .AddTextInput(
                label: DiscordUiText.ItemPriceLabel(language),
                customId: ReceiptInteractionCustomIds.ItemPriceInputCustomId,
                style: TextInputStyle.Short,
                required: true,
                placeholder: DiscordUiText.ItemPricePlaceholder(language),
                maxLength: 20)
            .AddTextInput(
                label: DiscordUiText.ItemQuantityLabel(language),
                customId: ReceiptInteractionCustomIds.ItemQuantityInputCustomId,
                style: TextInputStyle.Short,
                required: false,
                placeholder: DiscordUiText.ItemQuantityPlaceholder(language),
                value: "1",
                maxLength: 2);

        await component.RespondWithModalAsync(modal.Build());
        return "add_modal_opened";
    }

    private async Task<string> HandleAddItemModalAsync(SocketModal modal, string receiptId)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await modal.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(modal.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, modal.User.Id))
            {
                await modal.RespondAsync(DiscordUiText.OwnerOnlyAdd(GetLanguage(modal.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            var itemName = GetModalValue(modal, ReceiptInteractionCustomIds.ItemNameInputCustomId);
            var itemPriceText = GetModalValue(modal, ReceiptInteractionCustomIds.ItemPriceInputCustomId);
            var itemQuantityText = GetModalValue(modal, ReceiptInteractionCustomIds.ItemQuantityInputCustomId);

            if (string.IsNullOrWhiteSpace(itemName))
            {
                await modal.RespondAsync(DiscordUiText.InvalidItemName(GetLanguage(modal.User.Id)), ephemeral: true);
                return "invalid_item_name";
            }

            if (!decimal.TryParse(itemPriceText, out var itemPrice) || itemPrice < 0)
            {
                await modal.RespondAsync(DiscordUiText.InvalidItemPrice(GetLanguage(modal.User.Id)), ephemeral: true);
                return "invalid_item_price";
            }

            var quantity = 1;
            if (!string.IsNullOrWhiteSpace(itemQuantityText) &&
                (!int.TryParse(itemQuantityText, out quantity) || quantity <= 0))
            {
                await modal.RespondAsync(DiscordUiText.InvalidItemQuantity(GetLanguage(modal.User.Id)), ephemeral: true);
                return "invalid_item_quantity";
            }

            await modal.DeferAsync(ephemeral: true);
            ReceiptSessionStateService.AddManualItem(session, itemName.Trim(), itemPrice, quantity);
            _sessionStore.AddOrUpdate(session);
            _debounceService.ScheduleRefresh(receiptId);

            return "item_added";
        });
    }

    private async Task<string> HandleEditItemModalAsync(SocketModal modal, string receiptId, string editToken)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await modal.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(modal.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, modal.User.Id))
            {
                await modal.RespondAsync(DiscordUiText.OwnerOnlyEdit(GetLanguage(modal.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            if (!session.PendingEditItemIds.TryGetValue(editToken, out var itemId))
            {
                await modal.RespondAsync(DiscordUiText.EditItemTokenMissing(GetLanguage(modal.User.Id)), ephemeral: true);
                return "edit_item_token_not_found";
            }

            var itemName = GetModalValue(modal, ReceiptInteractionCustomIds.ItemNameInputCustomId);
            var itemPriceText = GetModalValue(modal, ReceiptInteractionCustomIds.ItemPriceInputCustomId);

            if (string.IsNullOrWhiteSpace(itemName))
            {
                await modal.RespondAsync(DiscordUiText.InvalidItemName(GetLanguage(modal.User.Id)), ephemeral: true);
                return "invalid_item_name";
            }

            if (!decimal.TryParse(itemPriceText, out var itemPrice) || itemPrice < 0)
            {
                await modal.RespondAsync(DiscordUiText.InvalidItemPrice(GetLanguage(modal.User.Id)), ephemeral: true);
                return "invalid_item_price";
            }

            if (!ReceiptSessionStateService.UpdateItem(session, itemId, itemName.Trim(), itemPrice))
            {
                await modal.RespondAsync(DiscordUiText.EditItemNotFound(GetLanguage(modal.User.Id)), ephemeral: true);
                return "edit_item_not_found";
            }

            session.PendingEditItemIds.Remove(editToken);
            await modal.DeferAsync(ephemeral: true);
            _sessionStore.AddOrUpdate(session);
            _debounceService.ScheduleRefresh(receiptId);
            await modal.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = DiscordUiText.ItemEdited(GetLanguage(modal.User.Id));
            });

            return "item_edited";
        });
    }

    private async Task<string> HandleConfirmAsync(SocketMessageComponent component, string receiptId)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, component.User.Id))
            {
                await component.RespondAsync(DiscordUiText.OwnerOnlyConfirm(GetLanguage(component.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            var confirmBlockReason = ReceiptSessionStateService.GetConfirmBlockReason(session);
            if (confirmBlockReason is not null)
            {
                await component.RespondAsync(confirmBlockReason, ephemeral: true);
                return "confirm_blocked";
            }

            await component.DeferAsync();
            _debounceService.CancelRefresh(receiptId);
            session.IsConfirmed = true;
            session.ConfirmedAtUtc = DateTimeOffset.UtcNow;
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;
            _sessionStore.AddOrUpdate(session);
            await _mainMessageService.RefreshAsync(session);
            await ClosePrivatePanelsAsync(session);

            if (_settlementHistoryRepositoryProvider.Repository is not null)
            {
                var historyDocument = ConfirmedSettlementHistoryDocument.FromSession(session);
                _ = SaveHistoryInBackgroundAsync(component, historyDocument);
            }

            return "confirmed";
        });
    }

    private async Task SaveHistoryInBackgroundAsync(
        SocketMessageComponent component,
        ConfirmedSettlementHistoryDocument historyDocument)
    {
        var repository = _settlementHistoryRepositoryProvider.Repository;
        if (repository is null)
        {
            return;
        }

        Exception? lastException = null;
        for (var attempt = 1; attempt <= HistorySaveRetryDelays.Length + 1; attempt++)
        {
            try
            {
                await repository.SaveAsync(historyDocument);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Settlement history save failed. ReceiptId={ReceiptId} Attempt={Attempt}",
                    historyDocument.ReceiptId,
                    attempt);

                if (attempt > HistorySaveRetryDelays.Length)
                {
                    break;
                }

                await Task.Delay(HistorySaveRetryDelays[attempt - 1]);
            }
        }

        _logger.LogError(
            lastException,
            "Settlement history save exhausted retries. ReceiptId={ReceiptId} HistoryId={HistoryId}",
            historyDocument.ReceiptId,
            historyDocument.Id);

        try
        {
            await component.FollowupAsync(DiscordUiText.HistorySaveFailed(GetLanguage(component.User.Id)), ephemeral: true);
        }
        catch (Exception followupEx)
        {
            _logger.LogWarning(
                followupEx,
                "Failed to send settlement history failure followup. ReceiptId={ReceiptId}",
                historyDocument.ReceiptId);
        }
    }

    private async Task<string> HandleCancelAsync(SocketMessageComponent component, string receiptId)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, component.User.Id))
            {
                await component.RespondAsync(DiscordUiText.OwnerOnlyCancel(GetLanguage(component.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            await component.DeferAsync(ephemeral: true);
            _debounceService.CancelRefresh(receiptId);
            await ClosePrivatePanelsAsync(session);

            try
            {
                await _mainMessageService.DeleteAsync(session);
            }
            catch
            {
                // Ignore delete failures for already removed or stale main messages.
            }

            _sessionStore.Remove(receiptId, out _);

            return "cancelled";
        });
    }

    private async Task<string> HandleTipModeChangeAsync(SocketMessageComponent component, string receiptId, TipSplitMode tipSplitMode)
    {
        return await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                await component.RespondAsync(DiscordUiText.SessionNotFound(GetLanguage(component.User.Id)), ephemeral: true);
                return "session_not_found";
            }

            if (!IsOwner(session, component.User.Id))
            {
                await component.RespondAsync(DiscordUiText.OwnerOnlyTipMode(GetLanguage(component.User.Id)), ephemeral: true);
                return "forbidden_user";
            }

            if ((session.Tip ?? 0m) <= 0m)
            {
                await component.RespondAsync(DiscordUiText.TipNotAvailable(GetLanguage(component.User.Id)), ephemeral: true);
                return "tip_not_available";
            }

            await component.DeferAsync();
            session.TipSplitMode = tipSplitMode;
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;
            _sessionStore.AddOrUpdate(session);
            _debounceService.CancelRefresh(receiptId);
            await _mainMessageService.RefreshAsync(session);
            return "tip_mode_updated";
        });
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
        var language = GetLanguage(userId);

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
                var instanceIndex = GetInstanceIndex(session, item);
                selectMenu.AddOption(
                    label: $"{item.Name} #{instanceIndex}{(item.IsAlcohol ? " [Alcohol]" : string.Empty)}",
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

    private static async Task ClosePrivatePanelsAsync(ReceiptSessionState session)
    {
        if (session.ActivePrivatePanelInteractions.Count == 0)
        {
            return;
        }

        foreach (var interaction in session.ActivePrivatePanelInteractions.Values.Distinct().ToArray())
        {
            try
            {
                await interaction.DeleteOriginalResponseAsync();
            }
            catch
            {
                // Ignore cleanup failures for stale/expired interaction tokens.
            }
        }

        session.ActivePrivatePanelInteractions.Clear();
    }

    private static async Task ReplaceExistingPrivatePanelAsync(
        ReceiptSessionState session,
        ulong userId,
        ReceiptSelectionMode mode)
    {
        var key = BuildPrivatePanelKey(userId, mode);
        if (!session.ActivePrivatePanelInteractions.TryGetValue(key, out var existingInteraction))
        {
            return;
        }

        try
        {
            await existingInteraction.DeleteOriginalResponseAsync();
        }
        catch
        {
            // Ignore cleanup failures for stale/expired interaction tokens.
        }

        session.ActivePrivatePanelInteractions.Remove(key);
    }

    private static string BuildPrivatePanelKey(ulong userId, ReceiptSelectionMode mode)
    {
        return $"{mode}:{userId}";
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

    private AppLanguage GetLanguage(ulong userId)
    {
        return _languagePreferenceStore.GetLanguage(userId.ToString());
    }

    private AppLanguage GetLanguage(string userId)
    {
        return _languagePreferenceStore.GetLanguage(userId);
    }
}
