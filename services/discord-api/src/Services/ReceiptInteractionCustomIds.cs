using Discord;

public static class ReceiptInteractionCustomIds
{
    public const string SelectItemsButtonPrefix = "receipt-select-items";
    public const string AddItemButtonPrefix = "receipt-add-item";
    public const string RemoveItemButtonPrefix = "receipt-remove-item";
    public const string EditItemButtonPrefix = "receipt-edit-item";
    public const string ConfirmButtonPrefix = "receipt-confirm";
    public const string PageButtonPrefix = "receipt-page";
    public const string AssignSelectMenuPrefix = "receipt-item-menu";
    public const string RemoveSelectMenuPrefix = "receipt-remove-menu";
    public const string EditSelectMenuPrefix = "receipt-edit-menu";
    public const string AddItemModalPrefix = "receipt-add-item-modal";
    public const string EditItemModalPrefix = "receipt-edit-item-modal";
    public const string ItemNameInputCustomId = "item_name";
    public const string ItemPriceInputCustomId = "item_price";
    public const string ItemQuantityInputCustomId = "item_quantity";

    public static MessageComponent BuildMainMessageComponents(ReceiptSessionState session)
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
            .WithButton(
                "Confirm",
                $"{ConfirmButtonPrefix}:{session.ReceiptId}",
                ButtonStyle.Success,
                disabled: !ReceiptSessionStateService.CanConfirm(session),
                row: 1)
            .Build();
    }

    public static string BuildPageButtonCustomId(ReceiptSelectionMode mode, string receiptId, int page)
    {
        return $"{PageButtonPrefix}:{mode.ToString().ToLowerInvariant()}:{receiptId}:{page}";
    }

    public static string BuildSelectMenuCustomId(ReceiptSelectionMode mode, string receiptId, int page)
    {
        var prefix = mode switch
        {
            ReceiptSelectionMode.Assign => AssignSelectMenuPrefix,
            ReceiptSelectionMode.Remove => RemoveSelectMenuPrefix,
            ReceiptSelectionMode.Edit => EditSelectMenuPrefix,
            _ => AssignSelectMenuPrefix
        };

        return $"{prefix}:{receiptId}:{page}";
    }

    public static bool TryParsePageButton(string customId, out string receiptId, out ReceiptSelectionMode mode, out int page)
    {
        receiptId = string.Empty;
        mode = ReceiptSelectionMode.Assign;
        page = 0;

        var parts = customId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], PageButtonPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Enum.TryParse(parts[1], ignoreCase: true, out mode) ||
            !int.TryParse(parts[3], out page))
        {
            return false;
        }

        receiptId = parts[2];
        return !string.IsNullOrWhiteSpace(receiptId);
    }

    public static bool TryParseSelectMenu(string customId, string prefix, out string receiptId, out int page)
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

    public static bool TryGetReceiptId(string customId, string prefix, out string receiptId)
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

    public static bool TryParseEditModal(string customId, out string receiptId, out string itemId)
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
}

public enum ReceiptSelectionMode
{
    Assign,
    Remove,
    Edit
}
