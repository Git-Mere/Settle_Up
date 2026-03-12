public static class ReceiptSessionStateService
{
    public static ReceiptSessionState CreateSession(
        string receiptId,
        IReadOnlyList<MergedReceiptUiItem> mergedItems,
        string? merchantName = null,
        string? uploadedByUserId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        var now = createdAtUtc ?? DateTimeOffset.UtcNow;

        return new ReceiptSessionState
        {
            ReceiptId = receiptId,
            MerchantName = merchantName,
            UploadedByUserId = uploadedByUserId,
            MergedItems = mergedItems,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static bool AddSelection(ReceiptSessionState session, string userId, string itemId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (!HasMergedItem(session, itemId))
        {
            return false;
        }

        if (!session.UserSelections.TryGetValue(userId, out var itemIds))
        {
            itemIds = new HashSet<string>(StringComparer.Ordinal);
            session.UserSelections[userId] = itemIds;
        }

        var wasAdded = itemIds.Add(itemId);
        if (wasAdded)
        {
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        return wasAdded;
    }

    public static bool RemoveSelection(ReceiptSessionState session, string userId, string itemId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(itemId) ||
            !session.UserSelections.TryGetValue(userId, out var itemIds))
        {
            return false;
        }

        var wasRemoved = itemIds.Remove(itemId);
        if (!wasRemoved)
        {
            return false;
        }

        if (itemIds.Count == 0)
        {
            session.UserSelections.Remove(userId);
        }

        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    public static void ReplaceSelections(ReceiptSessionState session, string userId, IReadOnlyCollection<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var filteredItemIds = itemIds
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId) && HasMergedItem(session, itemId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (filteredItemIds.Count == 0)
        {
            session.UserSelections.Remove(userId);
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;
            return;
        }

        session.UserSelections[userId] = filteredItemIds;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
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
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId) && HasMergedItem(session, itemId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (!session.UserSelections.TryGetValue(userId, out var currentSelection))
        {
            currentSelection = new HashSet<string>(StringComparer.Ordinal);
        }

        currentSelection.ExceptWith(validPageItemIds);

        foreach (var itemId in selectedItemIds
                     .Where(itemId => validPageItemIds.Contains(itemId))
                     .Distinct(StringComparer.Ordinal))
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

    public static int GetTotalPages(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.MergedItems.Count == 0)
        {
            return 1;
        }

        return (int)Math.Ceiling(session.MergedItems.Count / (double)session.PageSize);
    }

    public static int GetUserCurrentPage(ReceiptSessionState session, string userId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        var totalPages = GetTotalPages(session);
        if (!session.UserCurrentPages.TryGetValue(userId, out var currentPage))
        {
            return 0;
        }

        return Math.Clamp(currentPage, 0, totalPages - 1);
    }

    public static void SetUserCurrentPage(ReceiptSessionState session, string userId, int page)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var totalPages = GetTotalPages(session);
        session.UserCurrentPages[userId] = Math.Clamp(page, 0, totalPages - 1);
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public static IReadOnlyList<MergedReceiptUiItem> GetPageItems(ReceiptSessionState session, int page)
    {
        ArgumentNullException.ThrowIfNull(session);

        var safePage = Math.Clamp(page, 0, GetTotalPages(session) - 1);

        return session.MergedItems
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Skip(safePage * session.PageSize)
            .Take(session.PageSize)
            .ToArray();
    }

    public static IReadOnlyList<string> GetUsersForItem(ReceiptSessionState session, string itemId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return [];
        }

        return session.UserSelections
            .Where(entry => entry.Value.Contains(itemId))
            .Select(entry => entry.Key)
            .OrderBy(userId => userId, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<MergedReceiptUiItem> GetItemsForUser(ReceiptSessionState session, string userId)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(userId) ||
            !session.UserSelections.TryGetValue(userId, out var selectedItemIds) ||
            selectedItemIds.Count == 0)
        {
            return [];
        }

        return session.MergedItems
            .Where(item => selectedItemIds.Contains(item.Id))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<MergedReceiptUiItem> GetSharedItems(ReceiptSessionState session)
    {
        return session.MergedItems
            .Where(item => GetUsersForItem(session, item.Id).Count > 1)
            .ToArray();
    }

    public static IReadOnlyList<MergedReceiptUiItem> GetIndividualItems(ReceiptSessionState session)
    {
        return session.MergedItems
            .Where(item => GetUsersForItem(session, item.Id).Count == 1)
            .ToArray();
    }

    public static IReadOnlyList<MergedReceiptUiItem> GetUnassignedItems(ReceiptSessionState session)
    {
        return session.MergedItems
            .Where(item => GetUsersForItem(session, item.Id).Count == 0)
            .ToArray();
    }

    public static ReceiptItemOwnershipClassification ClassifyItems(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var sharedItems = new List<MergedReceiptUiItem>();
        var individualItems = new List<MergedReceiptUiItem>();
        var unassignedItems = new List<MergedReceiptUiItem>();

        foreach (var item in session.MergedItems)
        {
            var selectedUserCount = GetUsersForItem(session, item.Id).Count;
            if (selectedUserCount > 1)
            {
                sharedItems.Add(item);
                continue;
            }

            if (selectedUserCount == 1)
            {
                individualItems.Add(item);
                continue;
            }

            unassignedItems.Add(item);
        }

        return new ReceiptItemOwnershipClassification(sharedItems, individualItems, unassignedItems);
    }

    private static bool HasMergedItem(ReceiptSessionState session, string itemId)
    {
        return session.MergedItems.Any(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
    }

}
