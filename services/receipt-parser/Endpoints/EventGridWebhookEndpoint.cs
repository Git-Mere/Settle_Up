using System.Text.Json;
using Azure.Messaging.EventGrid;
using receipt_parser.Services;

namespace receipt_parser.Endpoints;

public static class EventGridWebhookEndpoint
{
    private const string SubscriptionValidationEventType = "Microsoft.EventGrid.SubscriptionValidationEvent";
    private const string BlobCreatedEventType = "Microsoft.Storage.BlobCreated";

    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        ReceiptProcessingService processingService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("EventGridWebhookEndpoint");

        EventGridEvent[] events = await TryParseEventsAsync(request, logger, cancellationToken);
        if (events.Length == 0)
        {
            return Results.BadRequest(new { message = "Invalid Event Grid payload" });
        }

        foreach (var eventGridEvent in events)
        {
            if (eventGridEvent.EventType == SubscriptionValidationEventType)
            {
                var validationCode = TryGetValidationCode(eventGridEvent.Data);
                if (string.IsNullOrWhiteSpace(validationCode))
                {
                    logger.LogWarning("Subscription validation code 누락");
                    return Results.BadRequest(new { message = "Missing validation code" });
                }

                logger.LogInformation("Subscription validation 처리 완료");
                return Results.Ok(new { validationResponse = validationCode });
            }
        }

        foreach (var eventGridEvent in events)
        {
            if (eventGridEvent.EventType != BlobCreatedEventType)
            {
                logger.LogDebug("지원하지 않는 이벤트 타입 스킵: {EventType}", eventGridEvent.EventType);
                continue;
            }

            await processingService.ProcessBlobCreatedEventAsync(eventGridEvent, cancellationToken);
        }

        return Results.Ok(new { message = "Events processed" });
    }

    private static async Task<EventGridEvent[]> TryParseEventsAsync(
        HttpRequest request,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await BinaryData.FromStreamAsync(request.Body, cancellationToken);
            return EventGridEvent.ParseMany(payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Event Grid payload 파싱 실패");
            return [];
        }
    }

    private static string? TryGetValidationCode(BinaryData? eventData)
    {
        if (eventData is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(eventData.ToMemory());
        if (doc.RootElement.TryGetProperty("validationCode", out var codeProperty))
        {
            return codeProperty.GetString();
        }

        return null;
    }
}
