using Azure;
using Azure.Messaging.EventGrid;
using System.Text.Json;
using receipt_parser.Models;
using receipt_parser.Observability;

namespace receipt_parser.Services;

public sealed class ReceiptProcessingService
{
    private const string ParsedStatus = "Parsed";

    private readonly DocumentIntelligenceReceiptParser _parser;
    private readonly CosmosReceiptRepository _repository;
    private readonly DiscordApiDraftClient _discordApiDraftClient;
    private readonly ILogger<ReceiptProcessingService> _logger;

    public ReceiptProcessingService(
        DocumentIntelligenceReceiptParser parser,
        CosmosReceiptRepository repository,
        DiscordApiDraftClient discordApiDraftClient,
        ILogger<ReceiptProcessingService> logger)
    {
        _parser = parser;
        _repository = repository;
        _discordApiDraftClient = discordApiDraftClient;
        _logger = logger;
    }

    public async Task ProcessBlobCreatedEventAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.blob_event.process");
        activity?.SetTag("event.id", eventGridEvent.Id);
        activity?.SetTag("event.type", eventGridEvent.EventType);

        var blobUrl = TryGetBlobUrl(eventGridEvent.Data);
        if (string.IsNullOrWhiteSpace(blobUrl))
        {
            _logger.LogWarning("BlobCreated 이벤트에서 url 추출 실패. EventId={EventId}", eventGridEvent.Id);
            return;
        }

        _logger.LogInformation("Blob event received. EventId={EventId} BlobUrl={BlobUrl}", eventGridEvent.Id, blobUrl);

        try
        {
            var parsed = await _parser.ParseFromBlobAsync(blobUrl, cancellationToken);
            await SaveAndSendDraftAsync(parsed, cancellationToken, preferLocalTestUrl: false);
            _logger.LogInformation("Blob event processing completed. EventId={EventId} ReceiptId={ReceiptId}", eventGridEvent.Id, parsed.ReceiptId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Blob event processing failed. EventId={EventId} BlobUrl={BlobUrl}", eventGridEvent.Id, blobUrl);
            throw;
        }
    }

    public async Task<DiscordDraftNotificationPayload> ProcessLocalUploadAsync(
        BinaryData binaryData,
        string source,
        string uploadedByUserId,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.local_upload.process");
        activity?.SetTag("receipt.source", source);
        activity?.SetTag("receipt.uploaded_by", uploadedByUserId);

        _logger.LogInformation("Local upload processing started. Source={Source} UploadedByUserId={UploadedByUserId}", source, uploadedByUserId);
        try
        {
            var parsed = await _parser.ParseFromBinaryAsync(binaryData, source, cancellationToken);
            var payload = await SaveAndSendDraftAsync(parsed, cancellationToken, preferLocalTestUrl: true, uploadedByUserId);
            _logger.LogInformation("Local upload processing completed. Source={Source} ReceiptId={ReceiptId}", source, payload.Id);
            return payload;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Local upload processing failed. Source={Source}", source);
            throw;
        }
    }

    private async Task<DiscordDraftNotificationPayload> SaveAndSendDraftAsync(
        ParsedReceiptResult parsed,
        CancellationToken cancellationToken,
        bool preferLocalTestUrl,
        string? uploadedByUserIdOverride = null)
    {
        var document = BuildReceiptDocument(parsed, uploadedByUserIdOverride);
        await _repository.SaveAsync(document, cancellationToken);

        var payload = BuildDiscordDraftNotificationPayload(document);

        try
        {
            await _discordApiDraftClient.SendDraftAsync(payload, preferLocalTestUrl, cancellationToken);
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Draft delivery failed. ReceiptId={ReceiptId}", document.Id);
            throw;
        }
    }

    private static string? TryGetBlobUrl(BinaryData? eventData)
    {
        if (eventData is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(eventData.ToMemory());
        if (doc.RootElement.TryGetProperty("url", out var urlProperty))
        {
            return urlProperty.GetString();
        }

        return null;
    }

    private static string? TryExtractUploadedByUserId(string blobUrl)
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

    private static ReceiptDocument BuildReceiptDocument(
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

    private static DiscordDraftNotificationPayload BuildDiscordDraftNotificationPayload(ReceiptDocument document)
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
}
