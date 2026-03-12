public sealed class ReceiptSessionState
{
    public string ReceiptId { get; init; } = default!;
    public string? MerchantName { get; init; }
    public string? UploadedByUserId { get; init; }
    public IReadOnlyList<MergedReceiptUiItem> MergedItems { get; init; } = [];
    public Dictionary<string, HashSet<string>> UserSelections { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> UserCurrentPages { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> UserDisplayNames { get; init; } = new(StringComparer.Ordinal);
    public ulong? MainMessageId { get; set; }
    public ulong? MainChannelId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public int PageSize { get; init; } = 20;
}
