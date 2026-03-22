namespace receipt_parser.Models;

public sealed record ReceiptTaxBreakdown(
    decimal? GeneralSalesTax,
    decimal? SpiritsSalesTax,
    decimal? SpiritsLiterTax);
