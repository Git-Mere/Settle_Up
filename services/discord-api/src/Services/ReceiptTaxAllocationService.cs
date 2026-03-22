public static class ReceiptTaxAllocationService
{
    private static readonly string[] SpiritsKeywords =
    [
        "alcohol",
        "beer",
        "bourbon",
        "brandy",
        "gin",
        "liquor",
        "rum",
        "scotch",
        "soju",
        "spirits",
        "tequila",
        "vodka",
        "whiskey",
        "whisky",
        "wine"
    ];

    public static ReceiptTaxAllocationResult Calculate(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var generalSalesTax = session.TaxBreakdown?.GeneralSalesTax ?? session.Tax ?? 0m;
        var spiritsSalesTax = session.TaxBreakdown?.SpiritsSalesTax ?? 0m;
        var spiritsLiterTax = session.TaxBreakdown?.SpiritsLiterTax ?? 0m;

        var perItem = session.Items.ToDictionary(
            item => item.Id,
            item => new ReceiptItemTaxAllocation(item.Id, item.Amount, 0m, 0m, 0m),
            StringComparer.Ordinal);

        var generalTaxableItems = session.Items
            .Where(IsGeneralTaxableItem)
            .ToArray();

        var explicitSpiritsItems = session.Items
            .Where(item => item.IsSpirits == true)
            .ToArray();
        var suspectedSpiritsItems = explicitSpiritsItems.Length > 0
            ? explicitSpiritsItems
            : session.Items.Where(LooksLikeSpiritsItem).ToArray();

        ApplyAllocation(
            perItem,
            AllocateProportionally(generalTaxableItems, generalSalesTax, item => item.Amount),
            TaxKind.GeneralSalesTax);

        ApplyAllocation(
            perItem,
            AllocateProportionally(suspectedSpiritsItems, spiritsSalesTax, item => item.Amount),
            TaxKind.SpiritsSalesTax);

        var directSltItems = suspectedSpiritsItems
            .Where(item => (item.DirectSpiritsLiterTax ?? 0m) > 0m)
            .ToArray();
        var directSltTotal = directSltItems.Sum(item => item.DirectSpiritsLiterTax ?? 0m);
        ApplyAllocation(
            perItem,
            directSltItems.ToDictionary(
                item => item.Id,
                item => item.DirectSpiritsLiterTax ?? 0m,
                StringComparer.Ordinal),
            TaxKind.SpiritsLiterTax);

        var remainingSlt = Math.Max(0m, spiritsLiterTax - directSltTotal);
        if (remainingSlt > 0m)
        {
            var remainingSpiritsItems = suspectedSpiritsItems
                .Where(item => (item.DirectSpiritsLiterTax ?? 0m) <= 0m)
                .ToArray();

            Dictionary<string, decimal> sltAllocation;
            var volumeItems = remainingSpiritsItems
                .Where(item => (item.VolumeLiters ?? 0m) > 0m)
                .ToArray();
            if (volumeItems.Length > 0)
            {
                sltAllocation = AllocateProportionally(volumeItems, remainingSlt, item => item.VolumeLiters ?? 0m);
            }
            else
            {
                sltAllocation = AllocateProportionally(remainingSpiritsItems, remainingSlt, item => item.Amount);
            }

            ApplyAllocation(perItem, sltAllocation, TaxKind.SpiritsLiterTax);
        }

        var participantTotals = AllocateParticipants(session, perItem);

        return new ReceiptTaxAllocationResult(
            perItem.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            participantTotals);
    }

    private static Dictionary<string, decimal> AllocateParticipants(
        ReceiptSessionState session,
        IReadOnlyDictionary<string, ReceiptItemTaxAllocation> perItem)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var item in session.Items)
        {
            var users = ReceiptSessionStateService.GetUsersForItem(session, item.Id);
            if (users.Count == 0 || !perItem.TryGetValue(item.Id, out var allocation))
            {
                continue;
            }

            var split = SplitAmount(users, allocation.ItemTotal);
            foreach (var pair in split)
            {
                totals[pair.Key] = totals.TryGetValue(pair.Key, out var current)
                    ? current + pair.Value
                    : pair.Value;
            }
        }

        return totals;
    }

    private static Dictionary<string, decimal> SplitAmount(IReadOnlyList<string> userIds, decimal amount)
    {
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (userIds.Count == 0 || amount == 0m)
        {
            return result;
        }

        var rawShares = userIds
            .Select(userId => new RemainderShare<string>(userId, amount / userIds.Count))
            .ToArray();

        return RoundWithLargestRemainder(rawShares);
    }

    private static void ApplyAllocation(
        Dictionary<string, ReceiptItemTaxAllocation> allocations,
        IReadOnlyDictionary<string, decimal> distribution,
        TaxKind kind)
    {
        foreach (var pair in distribution)
        {
            if (!allocations.TryGetValue(pair.Key, out var current))
            {
                continue;
            }

            allocations[pair.Key] = kind switch
            {
                TaxKind.GeneralSalesTax => current with { GeneralSalesTax = pair.Value },
                TaxKind.SpiritsSalesTax => current with { SpiritsSalesTax = pair.Value },
                TaxKind.SpiritsLiterTax => current with { SpiritsLiterTax = pair.Value },
                _ => current
            };
        }
    }

    private static Dictionary<string, decimal> AllocateProportionally(
        IReadOnlyList<ReceiptLineItemState> items,
        decimal totalAmount,
        Func<ReceiptLineItemState, decimal> weightSelector)
    {
        if (items.Count == 0 || totalAmount == 0m)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var weighted = items
            .Select(item => new
            {
                item.Id,
                Weight = Math.Max(0m, weightSelector(item))
            })
            .ToArray();

        var totalWeight = weighted.Sum(entry => entry.Weight);
        if (totalWeight <= 0m)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var rawShares = weighted
            .Select(entry => new RemainderShare<string>(entry.Id, totalAmount * entry.Weight / totalWeight))
            .ToArray();

        return RoundWithLargestRemainder(rawShares);
    }

    private static Dictionary<TKey, decimal> RoundWithLargestRemainder<TKey>(IReadOnlyList<RemainderShare<TKey>> shares)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, decimal>();
        if (shares.Count == 0)
        {
            return result;
        }

        var roundedDown = shares
            .Select(share =>
            {
                var cents = share.RawAmount * 100m;
                var floorCents = decimal.Floor(cents);
                return new
                {
                    share.Key,
                    Rounded = floorCents / 100m,
                    Remainder = cents - floorCents
                };
            })
            .ToArray();

        foreach (var entry in roundedDown)
        {
            result[entry.Key] = entry.Rounded;
        }

        var targetCents = (int)decimal.Round(shares.Sum(share => share.RawAmount) * 100m, 0, MidpointRounding.AwayFromZero);
        var currentCents = (int)roundedDown.Sum(entry => entry.Rounded * 100m);
        var centsToDistribute = targetCents - currentCents;

        foreach (var entry in roundedDown
                     .OrderByDescending(entry => entry.Remainder)
                     .ThenBy(entry => entry.Key?.ToString(), StringComparer.Ordinal)
                     .Take(Math.Max(0, centsToDistribute)))
        {
            result[entry.Key] += 0.01m;
        }

        return result;
    }

    private static bool IsGeneralTaxableItem(ReceiptLineItemState item)
    {
        return item.IsGeneralTaxable ?? item.IsSpirits != true;
    }

    private static bool LooksLikeSpiritsItem(ReceiptLineItemState item)
    {
        if (item.IsSpirits == true)
        {
            return true;
        }

        return SpiritsKeywords.Any(keyword => item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private enum TaxKind
    {
        GeneralSalesTax,
        SpiritsSalesTax,
        SpiritsLiterTax
    }

    private sealed record RemainderShare<TKey>(TKey Key, decimal RawAmount);
}

public sealed record ReceiptItemTaxAllocation(
    string ItemId,
    decimal BaseAmount,
    decimal GeneralSalesTax,
    decimal SpiritsSalesTax,
    decimal SpiritsLiterTax)
{
    public decimal ItemTotal => BaseAmount + GeneralSalesTax + SpiritsSalesTax + SpiritsLiterTax;
}

public sealed record ReceiptTaxAllocationResult(
    IReadOnlyDictionary<string, ReceiptItemTaxAllocation> ItemAllocations,
    IReadOnlyDictionary<string, decimal> ParticipantTotals);
