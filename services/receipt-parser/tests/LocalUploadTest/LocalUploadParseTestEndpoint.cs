using System.Text.Json;
using Azure;
using receipt_parser.Services;

namespace receipt_parser.tests.LocalUploadTest;

public static class LocalUploadParseTestEndpoint
{
    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        ReceiptProcessingService processingService,
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
        var payload = await processingService.ProcessLocalUploadAsync(
            binaryData,
            source: $"local-upload:{file.FileName}",
            uploadedByUserId: "local-test-user",
            cancellationToken);

        Console.WriteLine("===== Parsed Receipt Payload =====");
        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("===== End Parsed Receipt Payload =====");

        logger.LogInformation(
            "로컬 업로드 테스트 처리 완료. FileName={FileName}, ReceiptId={ReceiptId}",
            file.FileName,
            payload.Id);

        return Results.Ok(payload);
    }
}
