using Discord;

public sealed class ReceiptSessionState
{
    public string ReceiptId { get; set; } = default!;
    public string? BlobUrl { get; set; }
    public string? MerchantName { get; set; }
    public string? UploadedByUserId { get; set; }
    public string? UploadedByDisplayName { get; set; }
    public string? PaymentContact { get; set; }
    public DateOnly? TransactionDate { get; set; }
    public string? Currency { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Total { get; set; }
    public List<ReceiptLineItemState> Items { get; set; } = [];
    public Dictionary<string, HashSet<string>> UserSelections { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> UserDisplayNames { get; init; } = new(StringComparer.Ordinal);
    public IMessageChannel? MainChannel { get; set; }
    public ulong? MainMessageId { get; set; }
    public ulong? MainChannelId { get; set; }
    public bool IsDraftReady { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int PageSize { get; init; } = 20;
}
