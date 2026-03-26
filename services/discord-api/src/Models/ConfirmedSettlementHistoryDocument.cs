using Discord;
using Newtonsoft.Json;

public sealed class ConfirmedSettlementHistoryDocument
{
    [JsonProperty("id")]
    public string Id { get; init; } = default!;

    [JsonProperty("type")]
    public string Type { get; init; } = "confirmed_settlement";

    [JsonProperty("receiptId")]
    public string ReceiptId { get; init; } = default!;

    [JsonProperty("blobUrl")]
    public string? BlobUrl { get; init; }

    [JsonProperty("guildId")]
    public string? GuildId { get; init; }

    [JsonProperty("channelId")]
    public string? ChannelId { get; init; }

    [JsonProperty("messageId")]
    public string? MessageId { get; init; }

    [JsonProperty("uploadedByUserId")]
    public string UploadedByUserId { get; init; } = default!;

    [JsonProperty("uploadedByDisplayName")]
    public string? UploadedByDisplayName { get; init; }

    [JsonProperty("merchantName")]
    public string? MerchantName { get; init; }

    [JsonProperty("transactionDate")]
    public DateOnly? TransactionDate { get; init; }

    [JsonProperty("currency")]
    public string? Currency { get; init; }

    [JsonProperty("subtotal")]
    public decimal? Subtotal { get; init; }

    [JsonProperty("tax")]
    public decimal? Tax { get; init; }

    [JsonProperty("sst")]
    public decimal? Sst { get; init; }

    [JsonProperty("slt")]
    public decimal? Slt { get; init; }

    [JsonProperty("tip")]
    public decimal? Tip { get; init; }

    [JsonProperty("total")]
    public decimal? Total { get; init; }

    [JsonProperty("tipSplitMode")]
    public string TipSplitMode { get; init; } = default!;

    [JsonProperty("paymentContact")]
    public string? PaymentContact { get; init; }

    [JsonProperty("participants")]
    public IReadOnlyList<ConfirmedSettlementParticipantDocument> Participants { get; init; } = [];

    [JsonProperty("confirmedAtUtc")]
    public DateTimeOffset ConfirmedAtUtc { get; init; }

    [JsonProperty("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonProperty("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; init; }

    public static ConfirmedSettlementHistoryDocument FromSession(ReceiptSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(session.UploadedByUserId))
        {
            throw new InvalidOperationException("uploadedByUserId is required to create settlement history.");
        }

        if (session.ConfirmedAtUtc is not DateTimeOffset confirmedAtUtc)
        {
            throw new InvalidOperationException("confirmedAtUtc is required to create settlement history.");
        }

        var allocation = ReceiptAllocationService.Calculate(session);
        var participants = allocation.ParticipantBreakdowns.Values
            .Where(participant => participant.Total > 0m)
            .OrderBy(participant => ReceiptSessionStateService.ResolveUserDisplayName(session, participant.UserId), StringComparer.OrdinalIgnoreCase)
            .Select(participant => new ConfirmedSettlementParticipantDocument
            {
                UserId = participant.UserId,
                DisplayName = ReceiptSessionStateService.ResolveUserDisplayName(session, participant.UserId),
                Amount = decimal.Round(participant.Total, 2, MidpointRounding.AwayFromZero),
                TaxAmount = decimal.Round(participant.GeneralTax, 2, MidpointRounding.AwayFromZero),
                SstAmount = decimal.Round(participant.Sst, 2, MidpointRounding.AwayFromZero),
                SltAmount = decimal.Round(participant.Slt, 2, MidpointRounding.AwayFromZero),
                TipAmount = decimal.Round(participant.Tip, 2, MidpointRounding.AwayFromZero),
                Items = BuildParticipantItems(session, participant.UserId)
            })
            .ToArray();

        return new ConfirmedSettlementHistoryDocument
        {
            Id = $"history_{Guid.NewGuid():N}",
            ReceiptId = session.ReceiptId,
            BlobUrl = session.BlobUrl,
            GuildId = session.MainGuildId?.ToString(),
            ChannelId = session.MainChannelId?.ToString(),
            MessageId = session.MainMessageId?.ToString(),
            UploadedByUserId = session.UploadedByUserId,
            UploadedByDisplayName = session.UploadedByDisplayName,
            MerchantName = session.MerchantName,
            TransactionDate = session.TransactionDate,
            Currency = session.Currency,
            Subtotal = session.Subtotal,
            Tax = NullIfZero(session.Tax),
            Sst = NullIfZero(session.Sst),
            Slt = NullIfZero(session.Slt),
            Tip = NullIfZero(session.Tip),
            Total = session.Total,
            TipSplitMode = session.TipSplitMode.ToString(),
            PaymentContact = session.PaymentContact,
            Participants = participants,
            ConfirmedAtUtc = confirmedAtUtc,
            CreatedAtUtc = confirmedAtUtc,
            UpdatedAtUtc = confirmedAtUtc
        };
    }

    private static IReadOnlyList<ConfirmedSettlementItemDocument> BuildParticipantItems(ReceiptSessionState session, string userId)
    {
        return ReceiptSessionStateService.GetItemsForUser(session, userId)
            .GroupBy(item => new { item.GroupKey, item.GroupDisplayName, item.Amount, item.IsAlcohol })
            .OrderBy(group => group.Key.GroupDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ConfirmedSettlementItemDocument
            {
                Name = group.Key.GroupDisplayName,
                Quantity = group.Count(),
                Amount = decimal.Round(group.Key.Amount * group.Count(), 2, MidpointRounding.AwayFromZero),
                IsAlcohol = group.Key.IsAlcohol
            })
            .ToArray();
    }

    private static decimal? NullIfZero(decimal? value)
    {
        return value is decimal amount && amount > 0m ? amount : null;
    }
}

public sealed class ConfirmedSettlementParticipantDocument
{
    [JsonProperty("userId")]
    public string UserId { get; init; } = default!;

    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = default!;

    [JsonProperty("amount")]
    public decimal Amount { get; init; }

    [JsonProperty("taxAmount")]
    public decimal TaxAmount { get; init; }

    [JsonProperty("sstAmount")]
    public decimal SstAmount { get; init; }

    [JsonProperty("sltAmount")]
    public decimal SltAmount { get; init; }

    [JsonProperty("tipAmount")]
    public decimal TipAmount { get; init; }

    [JsonProperty("items")]
    public IReadOnlyList<ConfirmedSettlementItemDocument> Items { get; init; } = [];
}

public sealed class ConfirmedSettlementItemDocument
{
    [JsonProperty("name")]
    public string Name { get; init; } = default!;

    [JsonProperty("quantity")]
    public int Quantity { get; init; }

    [JsonProperty("amount")]
    public decimal Amount { get; init; }

    [JsonProperty("isAlcohol")]
    public bool IsAlcohol { get; init; }
}
