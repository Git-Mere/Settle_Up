using System.Text.Json;

public sealed class ReceiptDraftTestDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWebHostEnvironment _environment;

    public ReceiptDraftTestDataLoader(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<ReceiptDraftNotificationRequest> LoadAsync(string uploadedByUserId, string? uploadedByDisplayName)
    {
        var filePath = Path.Combine(_environment.ContentRootPath, "TestData", "sample-receipt-draft.json");
        await using var stream = File.OpenRead(filePath);

        var payload = await JsonSerializer.DeserializeAsync<ReceiptDraftNotificationRequest>(stream, JsonOptions);
        if (payload is null)
        {
            throw new InvalidOperationException("테스트 영수증 JSON을 읽을 수 없습니다.");
        }

        var uniqueDraftId = $"test-receipt-ui-{Guid.NewGuid():N}";
        var blobUrl = payload.BlobUrl;
        if (!string.IsNullOrWhiteSpace(blobUrl))
        {
            blobUrl = blobUrl.Replace("test-receipt-ui-001", uniqueDraftId, StringComparison.Ordinal);
        }

        return new ReceiptDraftNotificationRequest
        {
            Id = uniqueDraftId,
            DraftId = uniqueDraftId,
            BlobUrl = blobUrl,
            Status = payload.Status,
            UploadedByUserId = uploadedByUserId,
            MerchantName = string.IsNullOrWhiteSpace(payload.MerchantName) ? uploadedByDisplayName : payload.MerchantName,
            TransactionDate = payload.TransactionDate,
            Currency = payload.Currency,
            Subtotal = payload.Subtotal,
            Tax = payload.Tax,
            Total = payload.Total,
            Items = payload.Items,
            ParseMetadata = payload.ParseMetadata,
            CreatedAtUtc = payload.CreatedAtUtc,
            UpdatedAtUtc = payload.UpdatedAtUtc
        };
    }
}
