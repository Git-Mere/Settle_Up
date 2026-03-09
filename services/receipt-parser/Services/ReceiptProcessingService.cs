using System.Text.Json;
using Azure.Messaging.EventGrid;
using Azure;
using receipt_parser.Models;
using receipt_parser.Observability;

namespace receipt_parser.Services;

public sealed class ReceiptProcessingService
{
    private const string ParsedStatus = "Parsed";

    private readonly DocumentIntelligenceReceiptParser _parser;
    private readonly CosmosReceiptRepository _repository;
    private readonly ReceiptParsedEventPublisher _publisher;
    private readonly ILogger<ReceiptProcessingService> _logger;

    public ReceiptProcessingService(
        DocumentIntelligenceReceiptParser parser,
        CosmosReceiptRepository repository,
        ReceiptParsedEventPublisher publisher,
        ILogger<ReceiptProcessingService> logger)
    {
        _parser = parser;
        _repository = repository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task ProcessBlobCreatedEventAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt.process.blob_created");
        activity?.SetTag("event.id", eventGridEvent.Id);
        activity?.SetTag("event.type", eventGridEvent.EventType);

        var blobUrl = TryGetBlobUrl(eventGridEvent.Data);
        if (string.IsNullOrWhiteSpace(blobUrl))
        {
            _logger.LogWarning("BlobCreated 이벤트에서 url 추출 실패. EventId={EventId}", eventGridEvent.Id);
            return;
        }

        var parsed = await _parser.ParseFromBlobAsync(blobUrl, cancellationToken);
        await SaveAndPublishAsync(parsed, cancellationToken);
    }

    public async Task<ReceiptParsedEventPayload> ProcessLocalUploadAsync(
        BinaryData binaryData,
        string source,
        string uploadedByUserId,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt.process.local_upload");
        activity?.SetTag("receipt.source", source);
        activity?.SetTag("receipt.uploaded_by", uploadedByUserId);

        var parsed = await _parser.ParseFromBinaryAsync(binaryData, source, cancellationToken);
        return await SaveAndPublishAsync(parsed, cancellationToken, uploadedByUserId);
    }

    private async Task<ReceiptParsedEventPayload> SaveAndPublishAsync(
        ParsedReceiptResult parsed,
        CancellationToken cancellationToken,
        string? uploadedByUserIdOverride = null)
    {
        var document = BuildReceiptDocument(parsed, uploadedByUserIdOverride);
        await _repository.SaveAsync(document, cancellationToken);

        var payload = BuildReceiptParsedEventPayload(document);
        await _publisher.PublishAsync(payload, cancellationToken);
        return payload;
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

        // Expected pattern: <container>/receipts/{yyyy}/{MM}/{dd}/{userId}/{file}
        if (segments.Length >= 7 && string.Equals(segments[1], "receipts", StringComparison.OrdinalIgnoreCase))
        {
            return segments[5];
        }

        return null;
    }

    private static ReceiptDocument BuildReceiptDocument(
        ParsedReceiptResult parsed,
        string? uploadedByUserIdOverride = null)
    {
        var now = DateTimeOffset.UtcNow;
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
            Total = parsed.Total,
            Items = parsed.Items.ToList(),
            ParseMetadata = parsed.ParseMetadata,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static ReceiptParsedEventPayload BuildReceiptParsedEventPayload(ReceiptDocument document)
    {
        return new ReceiptParsedEventPayload(
            Id: document.Id,
            BlobUrl: document.BlobUrl,
            Status: document.Status,
            UploadedByUserId: document.UploadedByUserId,
            MerchantName: document.MerchantName,
            TransactionDate: document.TransactionDate,
            Currency: document.Currency,
            Subtotal: document.Subtotal,
            Tax: document.Tax,
            Total: document.Total,
            Items: document.Items,
            ParseMetadata: document.ParseMetadata,
            CreatedAtUtc: document.CreatedAtUtc,
            UpdatedAtUtc: document.UpdatedAtUtc);
    }
}
