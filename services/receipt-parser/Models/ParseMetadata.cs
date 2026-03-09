namespace receipt_parser.Models;

public sealed record ParseMetadata(
    string ModelId,
    float? MerchantConfidence,
    float? TotalConfidence);
