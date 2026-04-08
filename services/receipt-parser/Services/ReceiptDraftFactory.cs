using receipt_parser.Models;

namespace receipt_parser.Services;

public static class ReceiptDraftFactory
{
    private const string ParsedStatus = "Parsed";

    public static ReceiptDocument BuildReceiptDocument(
        ParsedReceiptResult parsed,
        string? uploadedByUserIdOverride = null)
    {
        var now = DateTimeOffset.UtcNow;
        var totalTax = (parsed.Tax ?? 0m) + (parsed.Sst ?? 0m) + (parsed.Slt ?? 0m);
        var total = parsed.Total ?? (parsed.Subtotal is decimal subtotal
            ? subtotal + totalTax + (parsed.Tip ?? 0m)
            : null);

        return new ReceiptDocument
        {
            Id = parsed.ReceiptId,
            Status = ParsedStatus,
            BlobUrl = parsed.BlobUrl,
            UploadedByUserId = uploadedByUserIdOverride ?? TryExtractUploadedByUserId(parsed.BlobUrl),
            MerchantName = parsed.MerchantName,
            Currency = parsed.Currency,
            TransactionDate = parsed.TransactionDate,
            Subtotal = parsed.Subtotal,
            Tax = parsed.Tax,
            Sst = parsed.Sst,
            Slt = parsed.Slt,
            Tip = parsed.Tip,
            UnattributedDiscount = parsed.UnattributedDiscount,
            Total = total,
            Items = [.. parsed.Items],
            ParseMetadata = parsed.ParseMetadata,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public static DiscordDraftNotificationPayload BuildNotificationPayload(ReceiptDocument document)
    {
        return new DiscordDraftNotificationPayload(
            Id: document.Id,
            BlobUrl: document.BlobUrl,
            Status: document.Status,
            UploadedByUserId: document.UploadedByUserId,
            MerchantName: document.MerchantName,
            TransactionDate: document.TransactionDate,
            Currency: document.Currency,
            Subtotal: document.Subtotal,
            Tax: document.Tax,
            Sst: document.Sst,
            Slt: document.Slt,
            Tip: document.Tip,
            UnattributedDiscount: document.UnattributedDiscount,
            Total: document.Total,
            Items: document.Items,
            ParseMetadata: document.ParseMetadata,
            CreatedAtUtc: document.CreatedAtUtc,
            UpdatedAtUtc: document.UpdatedAtUtc);
    }

    public static string? TryExtractUploadedByUserId(string blobUrl)
    {
        if (!Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 2)
        {
            return null;
        }

        // Current upload pattern:
        //   <container>/{yyyy}/{MM}/{dd}/{userId}/{file}
        if (segments.Length >= 6 && IsYearSegment(segments[1]))
        {
            return segments[4];
        }

        // Backward-compatible fallback for an older expected pattern:
        //   <container>/receipts/{yyyy}/{MM}/{dd}/{userId}/{file}
        if (segments.Length >= 7 &&
            string.Equals(segments[1], "receipts", StringComparison.OrdinalIgnoreCase) &&
            IsYearSegment(segments[2]))
        {
            return segments[5];
        }

        return null;
    }

    private static bool IsYearSegment(string value)
    {
        return value.Length == 4 && value.All(char.IsDigit);
    }
}
