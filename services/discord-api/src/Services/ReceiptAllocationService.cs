public static class ReceiptAllocationService
{
    public static ReceiptAllocationResult Calculate(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var itemUsers = session.Items.ToDictionary(
            item => item.Id,
            item => ReceiptSessionStateService.GetUsersForItem(session, item.Id),
            StringComparer.Ordinal);

        var itemTaxAllocations = BuildItemTaxAllocations(session);
        var participantBreakdowns = new Dictionary<string, ParticipantReceiptBreakdown>(StringComparer.Ordinal);

        foreach (var item in session.Items)
        {
            if (!itemUsers.TryGetValue(item.Id, out var assignedUsers) || assignedUsers.Count == 0)
            {
                continue;
            }

            var perUserBase = SplitAmount(assignedUsers, item.Amount);
            var itemTaxes = itemTaxAllocations.GetValueOrDefault(item.Id) ?? ReceiptItemTaxBreakdown.Zero;
            var perUserGeneralTax = SplitAmount(assignedUsers, itemTaxes.GeneralTax);
            var perUserSst = SplitAmount(assignedUsers, itemTaxes.Sst);
            var perUserSlt = SplitAmount(assignedUsers, itemTaxes.Slt);

            foreach (var userId in assignedUsers)
            {
                var current = participantBreakdowns.GetValueOrDefault(userId) ?? ParticipantReceiptBreakdown.Empty(userId);
                participantBreakdowns[userId] = current with
                {
                    Subtotal = current.Subtotal + perUserBase.GetValueOrDefault(userId),
                    GeneralTax = current.GeneralTax + perUserGeneralTax.GetValueOrDefault(userId),
                    Sst = current.Sst + perUserSst.GetValueOrDefault(userId),
                    Slt = current.Slt + perUserSlt.GetValueOrDefault(userId)
                };
            }
        }

        ApplyTipAllocation(session, participantBreakdowns);

        var settlementLines = participantBreakdowns.Values
            .Where(line => line.Total > 0m)
            .ToDictionary(line => line.UserId, line => line.Total, StringComparer.Ordinal);

        var taxLines = participantBreakdowns.Values
            .Where(line => line.GeneralTax > 0m || line.Sst > 0m || line.Slt > 0m)
            .ToDictionary(
                line => line.UserId,
                line => new ParticipantTaxLine(line.UserId, line.GeneralTax, line.Sst, line.Slt),
                StringComparer.Ordinal);

        var tipLines = participantBreakdowns.Values
            .Where(line => line.Tip > 0m)
            .ToDictionary(line => line.UserId, line => line.Tip, StringComparer.Ordinal);

        return new ReceiptAllocationResult(
            ItemTaxAllocations: itemTaxAllocations,
            ParticipantBreakdowns: participantBreakdowns.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            SettlementTotals: settlementLines,
            TaxLines: taxLines,
            TipLines: tipLines);
    }

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> CalculateParticipantItemShares(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var participantItemShares = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);

        foreach (var item in session.Items)
        {
            var assignedUsers = ReceiptSessionStateService.GetUsersForItem(session, item.Id);
            if (assignedUsers.Count == 0)
            {
                continue;
            }

            var perUserShares = SplitAmount(assignedUsers, item.Amount);
            foreach (var pair in perUserShares)
            {
                if (!participantItemShares.TryGetValue(pair.Key, out var itemShares))
                {
                    itemShares = new Dictionary<string, decimal>(StringComparer.Ordinal);
                    participantItemShares[pair.Key] = itemShares;
                }

                itemShares[item.Id] = pair.Value;
            }
        }

        return participantItemShares.ToDictionary(
            participant => participant.Key,
            participant => (IReadOnlyDictionary<string, decimal>)participant.Value,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ReceiptItemTaxBreakdown> BuildItemTaxAllocations(ReceiptSessionState session)
    {
        var result = session.Items.ToDictionary(
            item => item.Id,
            _ => ReceiptItemTaxBreakdown.Zero,
            StringComparer.Ordinal);

        ApplyAllocation(
            result,
            AllocateProportionally(session.Items, session.Tax ?? 0m, item => item.Amount),
            TaxKind.GeneralTax);

        var alcoholItems = session.Items.Where(item => item.IsAlcohol).ToArray();

        ApplyAllocation(
            result,
            AllocateProportionally(alcoholItems, session.Sst ?? 0m, item => item.Amount),
            TaxKind.Sst);

        ApplyAllocation(
            result,
            AllocateProportionally(alcoholItems, session.Slt ?? 0m, item => item.Amount),
            TaxKind.Slt);

        return result;
    }

    private static void ApplyTipAllocation(
        ReceiptSessionState session,
        Dictionary<string, ParticipantReceiptBreakdown> participantBreakdowns)
    {
        var tip = session.Tip ?? 0m;
        if (tip <= 0m || participantBreakdowns.Count == 0)
        {
            return;
        }

        Dictionary<string, decimal> tipAllocation;
        if (session.TipSplitMode == TipSplitMode.Equal)
        {
            tipAllocation = SplitAmount(participantBreakdowns.Keys.OrderBy(userId => userId, StringComparer.Ordinal).ToArray(), tip);
        }
        else
        {
            var participants = participantBreakdowns.Values
                .Where(line => line.Subtotal > 0m)
                .OrderBy(line => line.UserId, StringComparer.Ordinal)
                .ToArray();

            tipAllocation = AllocateProportionally(
                participants,
                tip,
                participant => participant.Subtotal,
                participant => participant.UserId);
        }

        foreach (var pair in tipAllocation)
        {
            var current = participantBreakdowns[pair.Key];
            participantBreakdowns[pair.Key] = current with { Tip = current.Tip + pair.Value };
        }
    }

    private static void ApplyAllocation(
        Dictionary<string, ReceiptItemTaxBreakdown> allocations,
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
                TaxKind.GeneralTax => current with { GeneralTax = pair.Value },
                TaxKind.Sst => current with { Sst = pair.Value },
                TaxKind.Slt => current with { Slt = pair.Value },
                _ => current
            };
        }
    }

    private static Dictionary<string, decimal> AllocateProportionally(
        IReadOnlyList<ReceiptLineItemState> items,
        decimal totalAmount,
        Func<ReceiptLineItemState, decimal> weightSelector)
    {
        return AllocateProportionally(items, totalAmount, weightSelector, item => item.Id);
    }

    private static Dictionary<string, decimal> AllocateProportionally<T>(
        IReadOnlyList<T> items,
        decimal totalAmount,
        Func<T, decimal> weightSelector,
        Func<T, string> keySelector)
    {
        if (items.Count == 0 || totalAmount <= 0m)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var weighted = items
            .Select(item => new
            {
                Key = keySelector(item),
                Weight = Math.Max(0m, weightSelector(item))
            })
            .ToArray();

        var totalWeight = weighted.Sum(entry => entry.Weight);
        if (totalWeight <= 0m)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var rawShares = weighted
            .Select(entry => new RemainderShare<string>(entry.Key, totalAmount * entry.Weight / totalWeight))
            .ToArray();

        return RoundWithLargestRemainder(rawShares);
    }

    private static Dictionary<string, decimal> SplitAmount(IReadOnlyList<string> userIds, decimal amount)
    {
        if (userIds.Count == 0 || amount <= 0m)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var rawShares = userIds
            .Select(userId => new RemainderShare<string>(userId, amount / userIds.Count))
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

    private enum TaxKind
    {
        GeneralTax,
        Sst,
        Slt
    }

    private sealed record RemainderShare<TKey>(TKey Key, decimal RawAmount);
}

public sealed record ReceiptItemTaxBreakdown(decimal GeneralTax, decimal Sst, decimal Slt)
{
    public static ReceiptItemTaxBreakdown Zero => new(0m, 0m, 0m);
    public decimal Total => GeneralTax + Sst + Slt;
}

public sealed record ParticipantTaxLine(string UserId, decimal GeneralTax, decimal Sst, decimal Slt)
{
    public decimal Total => GeneralTax + Sst + Slt;
}

public sealed record ParticipantReceiptBreakdown(
    string UserId,
    decimal Subtotal,
    decimal GeneralTax,
    decimal Sst,
    decimal Slt,
    decimal Tip)
{
    public static ParticipantReceiptBreakdown Empty(string userId) => new(userId, 0m, 0m, 0m, 0m, 0m);
    public decimal Total => Subtotal + GeneralTax + Sst + Slt + Tip;
}

public sealed record ReceiptAllocationResult(
    IReadOnlyDictionary<string, ReceiptItemTaxBreakdown> ItemTaxAllocations,
    IReadOnlyDictionary<string, ParticipantReceiptBreakdown> ParticipantBreakdowns,
    IReadOnlyDictionary<string, decimal> SettlementTotals,
    IReadOnlyDictionary<string, ParticipantTaxLine> TaxLines,
    IReadOnlyDictionary<string, decimal> TipLines);
