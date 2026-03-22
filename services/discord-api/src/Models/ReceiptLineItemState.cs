public sealed class ReceiptLineItemState
{
    public string Id { get; init; } = default!;
    public string Name { get; set; } = default!;
    public string NormalizedName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string GroupKey { get; set; } = default!;
    public string GroupDisplayName { get; set; } = default!;
    public bool IsManuallyAdded { get; init; }
    public bool? IsGeneralTaxable { get; set; }
    public bool? IsSpirits { get; set; }
    public decimal? VolumeLiters { get; set; }
    public decimal? DirectSpiritsLiterTax { get; set; }
}
