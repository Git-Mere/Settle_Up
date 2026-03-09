using System.Text.Json;
using Azure;
using receipt_parser.Models;
using receipt_parser.Services;

namespace receipt_parser.tests.LocalUploadTest;

public static class LocalUploadParseTestEndpoint
{
    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        DocumentIntelligenceReceiptParser parser,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("LocalUploadParseTestEndpoint");

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "multipart/form-data 요청이 필요합니다. field name은 'file'입니다." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "업로드 파일이 없습니다. form-data의 key를 'file'로 보내주세요." });
        }

        await using var stream = file.OpenReadStream();
        var binaryData = await BinaryData.FromStreamAsync(stream, cancellationToken);
        var result = await parser.ParseFromBinaryAsync(binaryData, $"local-upload:{file.FileName}", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var payload = new ReceiptParsedEventPayload(
            Id: result.ReceiptId,
            BlobUrl: result.BlobUrl,
            Status: "Parsed",
            UploadedByUserId: "local-test-user",
            MerchantName: result.MerchantName,
            TransactionDate: result.TransactionDate,
            Currency: result.Currency,
            Subtotal: result.Subtotal,
            Tax: result.Tax,
            Total: result.Total,
            Items: result.Items,
            ParseMetadata: result.ParseMetadata,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);

        Console.WriteLine("===== Parsed Receipt Payload =====");
        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("===== End Parsed Receipt Payload =====");

        logger.LogInformation(
            "로컬 업로드 테스트 파싱 완료. FileName={FileName}, ReceiptId={ReceiptId}",
            file.FileName,
            result.ReceiptId);

        return Results.Ok(payload);
    }
}
