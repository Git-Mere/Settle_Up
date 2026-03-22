namespace receipt_parser.Models;

public sealed record ParsedReceiptItem(
    string Id,
    string? Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TotalPrice,
    bool? IsGeneralTaxable = null,
    bool? IsSpirits = null,
    decimal? VolumeLiters = null,
    decimal? DirectSpiritsLiterTax = null);
