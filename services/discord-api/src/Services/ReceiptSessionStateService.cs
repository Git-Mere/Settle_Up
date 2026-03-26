public static class ReceiptSessionStateService
{
    public static ReceiptSessionState CreatePendingUploadSession(
        string receiptId,
        string blobUrl,
        string uploadedByUserId,
        string uploadedByDisplayName,
        string? paymentContact,
        DateTimeOffset? createdAtUtc = null)
    {
        var now = createdAtUtc ?? DateTimeOffset.UtcNow;

        return new ReceiptSessionState
        {
            ReceiptId = receiptId,
            BlobUrl = blobUrl,
            UploadedByUserId = uploadedByUserId,
            UploadedByDisplayName = uploadedByDisplayName,
            PaymentContact = NormalizeOptionalText(paymentContact),
            IsDraftReady = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static ReceiptSessionState CreateSessionFromDraft(
        ReceiptDraftNotificationRequest payload,
        string uploadedByDisplayName,
        string? paymentContact = null,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var now = createdAtUtc ?? DateTimeOffset.UtcNow;
        return new ReceiptSessionState
        {
            ReceiptId = payload.ResolvedDraftId ?? throw new InvalidOperationException("draftId is required."),
            BlobUrl = payload.BlobUrl,
            MerchantName = NormalizeOptionalText(payload.MerchantName),
            UploadedByUserId = payload.UploadedByUserId,
            UploadedByDisplayName = uploadedByDisplayName,
            PaymentContact = NormalizeOptionalText(paymentContact),
            TransactionDate = payload.TransactionDate,
            Currency = NormalizeOptionalText(payload.Currency),
            Subtotal = payload.Subtotal,
            Tax = payload.Tax,
            Sst = payload.Sst,
            Slt = payload.Slt,
            Tip = payload.Tip,
            Total = payload.Total,
            Items = ExpandItems(payload.Items).ToList(),
            IsDraftReady = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static void ApplyDraftPayload(
        ReceiptSessionState session,
        ReceiptDraftNotificationRequest payload,
        string uploadedByDisplayName)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(payload);

        session.ReceiptId = payload.ResolvedDraftId ?? session.ReceiptId;
        session.BlobUrl = payload.BlobUrl ?? session.BlobUrl;
        session.MerchantName = NormalizeOptionalText(payload.MerchantName);
        session.UploadedByUserId = payload.UploadedByUserId ?? session.UploadedByUserId;
        session.UploadedByDisplayName = uploadedByDisplayName;
        session.TransactionDate = payload.TransactionDate;
        session.Currency = NormalizeOptionalText(payload.Currency);
        session.Subtotal = payload.Subtotal;
        session.Tax = payload.Tax;
        session.Sst = payload.Sst;
        session.Slt = payload.Slt;
        session.Tip = payload.Tip;
        session.Total = payload.Total;
        session.Items = ExpandItems(payload.Items).ToList();
        session.IsDraftReady = true;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;

        RemoveSelectionsForMissingItems(session);
    }

    public static IReadOnlyList<ReceiptLineItemState> ExpandItems(IReadOnlyList<ReceiptDraftNotificationItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            return [];
        }

        var expandedItems = new List<ReceiptLineItemState>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var name = NormalizeItemName(item.Description, index);
            var normalizedName = ReceiptItemNameNormalizer.Normalize(name);
            var groupKey = string.IsNullOrWhiteSpace(normalizedName) ? $"item-{index + 1}" : normalizedName;
            var quantity = ResolveExpansionCount(item.Quantity);
            var amountPerUnit = ResolveAmountPerUnit(item, quantity);

            for (var unitIndex = 0; unitIndex < quantity; unitIndex++)
            {
                expandedItems.Add(new ReceiptLineItemState
                {
                    Id = $"{(string.IsNullOrWhiteSpace(item.Id) ? $"item-{index + 1}" : item.Id)}:{unitIndex + 1}",
                    Name = name,
                    NormalizedName = normalizedName,
                    Amount = amountPerUnit,
                    GroupKey = groupKey,
                    GroupDisplayName = name
                });
            }
        }

        return expandedItems;
    }

    public static IReadOnlyList<ReceiptLineItemState> GetPageItems(ReceiptSessionState session, int page)
    {
        ArgumentNullException.ThrowIfNull(session);

        var safePage = Math.Clamp(page, 0, GetTotalPages(session) - 1);
        return session.Items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Skip(safePage * session.PageSize)
            .Take(session.PageSize)
            .ToArray();
    }

    public static int GetTotalPages(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Items.Count == 0)
        {
            return 1;
        }

        return (int)Math.Ceiling(session.Items.Count / (double)session.PageSize);
    }

    public static void ReplaceSelectionsForPage(
        ReceiptSessionState session,
        string userId,
        IReadOnlyCollection<string> pageItemIds,
        IReadOnlyCollection<string> selectedItemIds)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var validPageItemIds = pageItemIds
            .Where(itemId => HasItem(session, itemId))
            .ToHashSet(StringComparer.Ordinal);

        if (!session.UserSelections.TryGetValue(userId, out var currentSelection))
        {
            currentSelection = new HashSet<string>(StringComparer.Ordinal);
        }

        currentSelection.ExceptWith(validPageItemIds);

        foreach (var itemId in selectedItemIds.Where(validPageItemIds.Contains))
        {
            currentSelection.Add(itemId);
        }

        if (currentSelection.Count == 0)
        {
            session.UserSelections.Remove(userId);
        }
        else
        {
            session.UserSelections[userId] = currentSelection;
        }

        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public static bool RemoveItem(ReceiptSessionState session, string itemId)
    {
        ArgumentNullException.ThrowIfNull(session);

        var removed = session.Items.RemoveAll(item => string.Equals(item.Id, itemId, StringComparison.Ordinal)) > 0;
        if (!removed)
        {
            return false;
        }

        foreach (var selection in session.UserSelections.Values)
        {
            selection.Remove(itemId);
        }

        RemoveEmptySelections(session);
        RecalculateMoney(session);
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    public static ReceiptLineItemState AddManualItem(
        ReceiptSessionState session,
        string name,
        decimal amount,
        int quantity)
    {
        ArgumentNullException.ThrowIfNull(session);

        var normalizedName = ReceiptItemNameNormalizer.Normalize(name);
        var groupKey = string.IsNullOrWhiteSpace(normalizedName)
            ? $"manual-{Guid.NewGuid():N}"
            : $"manual-{normalizedName}-{Guid.NewGuid():N}";

        ReceiptLineItemState? firstCreated = null;
        var safeQuantity = Math.Clamp(quantity, 1, 25);
        for (var index = 0; index < safeQuantity; index++)
        {
            var item = new ReceiptLineItemState
            {
                Id = $"manual-{Guid.NewGuid():N}",
                Name = name,
                NormalizedName = normalizedName,
                Amount = amount,
                GroupKey = groupKey,
                GroupDisplayName = name,
                IsManuallyAdded = true
            };

            session.Items.Add(item);
            firstCreated ??= item;
        }

        RecalculateMoney(session);
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return firstCreated!;
    }

    public static bool UpdateItem(
        ReceiptSessionState session,
        string itemId,
        string newName,
        decimal newAmount)
    {
        ArgumentNullException.ThrowIfNull(session);

        var item = session.Items.SingleOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
        if (item is null)
        {
            return false;
        }

        var normalizedName = ReceiptItemNameNormalizer.Normalize(newName);
        item.Name = newName;
        item.NormalizedName = normalizedName;
        item.Amount = newAmount;
        item.GroupDisplayName = newName;
        item.GroupKey = string.IsNullOrWhiteSpace(normalizedName)
            ? item.Id
            : $"{normalizedName}-{newAmount:0.00}";

        RecalculateMoney(session);
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    public static void ReplaceAlcoholFlagsForPage(
        ReceiptSessionState session,
        IReadOnlyCollection<string> pageItemIds,
        IReadOnlyCollection<string> selectedAlcoholItemIds)
    {
        ArgumentNullException.ThrowIfNull(session);

        var validPageItemIds = pageItemIds
            .Where(itemId => HasItem(session, itemId))
            .ToHashSet(StringComparer.Ordinal);
        var selectedIds = selectedAlcoholItemIds
            .Where(validPageItemIds.Contains)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in session.Items.Where(item => validPageItemIds.Contains(item.Id)))
        {
            item.IsAlcohol = selectedIds.Contains(item.Id);
        }

        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public static IReadOnlyList<string> GetUsersForItem(ReceiptSessionState session, string itemId)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.UserSelections
            .Where(entry => entry.Value.Contains(itemId))
            .Select(entry => entry.Key)
            .OrderBy(userId => userId, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ReceiptLineItemState> GetItemsForUser(ReceiptSessionState session, string userId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId) ||
            !session.UserSelections.TryGetValue(userId, out var selectedIds) ||
            selectedIds.Count == 0)
        {
            return [];
        }

        return session.Items
            .Where(item => selectedIds.Contains(item.Id))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ReceiptLineItemState> GetUnassignedItems(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.Items
            .Where(item => GetUsersForItem(session, item.Id).Count == 0)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool CanConfirm(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return GetConfirmBlockReason(session) is null;
    }

    public static decimal GetItemsTotal(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Items.Sum(item => item.Amount);
    }

    public static IReadOnlyList<ReceiptSettlementLine> BuildSettlementLines(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return ReceiptAllocationService.Calculate(session).SettlementTotals
            .Where(entry => entry.Value > 0)
            .Select(entry => new ReceiptSettlementLine(
                entry.Key,
                ResolveUserDisplayName(session, entry.Key),
                decimal.Round(entry.Value, 2, MidpointRounding.AwayFromZero)))
            .OrderBy(line => line.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static decimal GetIndividualTotalForUser(ReceiptSessionState session, string userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var allocation = ReceiptAllocationService.Calculate(session);
        return allocation.ParticipantBreakdowns.GetValueOrDefault(userId)?.Subtotal ?? 0m;
    }

    public static bool HasAlcoholItems(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Items.Any(item => item.IsAlcohol);
    }

    public static bool RequiresAlcoholSelection(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return (session.Sst ?? 0m) > 0m || (session.Slt ?? 0m) > 0m;
    }

    public static string? GetConfirmBlockReason(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.IsDraftReady)
        {
            return "영수증 분석이 아직 끝나지 않았습니다.";
        }

        if (session.IsConfirmed)
        {
            return "이미 confirm된 영수증입니다.";
        }

        if (session.Items.Count == 0)
        {
            return "아이템이 없어 confirm할 수 없습니다.";
        }

        if (GetUnassignedItems(session).Count > 0)
        {
            return "Unassigned 아이템이 모두 배정되어야 confirm할 수 있습니다.";
        }

        if (RequiresAlcoholSelection(session) && !HasAlcoholItems(session))
        {
            return "SST/SLT가 있는 영수증은 alcohol 아이템을 먼저 지정해야 합니다.";
        }

        return null;
    }

    public static string ResolveUserDisplayName(ReceiptSessionState session, string userId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.UserDisplayNames.TryGetValue(userId, out var displayName) &&
            !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (string.Equals(userId, session.UploadedByUserId, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(session.UploadedByDisplayName))
        {
            return session.UploadedByDisplayName;
        }

        return userId;
    }

    private static decimal ResolveAmountPerUnit(ReceiptDraftNotificationItem item, int quantity)
    {
        if (quantity <= 1)
        {
            return item.TotalPrice ?? item.UnitPrice ?? 0m;
        }

        if (item.UnitPrice is decimal unitPrice && unitPrice > 0)
        {
            return unitPrice;
        }

        if (item.TotalPrice is decimal totalPrice && totalPrice > 0)
        {
            return decimal.Round(totalPrice / quantity, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static int ResolveExpansionCount(decimal? quantity)
    {
        if (quantity is null)
        {
            return 1;
        }

        var rounded = (int)Math.Round(quantity.Value, MidpointRounding.AwayFromZero);
        if (rounded <= 0)
        {
            return 1;
        }

        if (Math.Abs(quantity.Value - rounded) > 0.001m)
        {
            return 1;
        }

        return Math.Clamp(rounded, 1, 25);
    }

    private static string NormalizeItemName(string? description, int index)
    {
        var cleaned = ReceiptItemNameNormalizer.CleanDisplayName(description ?? string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? $"Item {index + 1}" : cleaned;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasItem(ReceiptSessionState session, string itemId)
    {
        return session.Items.Any(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
    }

    private static void RemoveSelectionsForMissingItems(ReceiptSessionState session)
    {
        var validIds = session.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var selection in session.UserSelections.Values)
        {
            selection.RemoveWhere(itemId => !validIds.Contains(itemId));
        }

        RemoveEmptySelections(session);
    }

    private static void RemoveEmptySelections(ReceiptSessionState session)
    {
        var emptyUsers = session.UserSelections
            .Where(entry => entry.Value.Count == 0)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var userId in emptyUsers)
        {
            session.UserSelections.Remove(userId);
        }
    }

    private static void RecalculateMoney(ReceiptSessionState session)
    {
        var itemsTotal = session.Items.Sum(item => item.Amount);
        session.Subtotal = itemsTotal;
        session.Total = itemsTotal + (session.Tax ?? 0m) + (session.Sst ?? 0m) + (session.Slt ?? 0m) + (session.Tip ?? 0m);
    }
}
