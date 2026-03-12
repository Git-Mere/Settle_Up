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

        return new ReceiptDraftNotificationRequest
        {
            Id = payload.Id,
            DraftId = payload.DraftId,
            BlobUrl = payload.BlobUrl,
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
