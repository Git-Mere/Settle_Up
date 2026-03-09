using System.Text.Json;
using Azure.Messaging.EventGrid;
using receipt_parser.Services;

namespace receipt_parser.Endpoints;

public static class EventGridWebhookEndpoint
{
    private const string SubscriptionValidationEventType = "Microsoft.EventGrid.SubscriptionValidationEvent";
    private const string BlobCreatedEventType = "Microsoft.Storage.BlobCreated";
    private const string AegEventTypeHeaderName = "aeg-event-type";
    private const string SubscriptionValidationHeaderValue = "SubscriptionValidation";

    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("EventGridWebhookEndpoint");
        var payload = await TryReadPayloadAsync(request, logger, cancellationToken);
        if (payload is null)
        {
            return Results.BadRequest(new { message = "Invalid Event Grid payload" });
        }

        var aegEventType = request.Headers[AegEventTypeHeaderName].ToString();
        if (string.Equals(aegEventType, SubscriptionValidationHeaderValue, StringComparison.OrdinalIgnoreCase))
        {
            return TryBuildValidationResponse(payload, logger);
        }

        EventGridEvent[] events = TryParseEvents(payload, logger);
        if (events.Length == 0)
        {
            return Results.BadRequest(new { message = "Invalid Event Grid payload" });
        }

        foreach (var eventGridEvent in events)
        {
            if (eventGridEvent.EventType == SubscriptionValidationEventType)
            {
                return TryBuildValidationResponse(payload, logger);
            }
        }

        var processingService = serviceProvider.GetRequiredService<ReceiptProcessingService>();

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

    private static async Task<BinaryData?> TryReadPayloadAsync(
        HttpRequest request,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await BinaryData.FromStreamAsync(request.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Event Grid payload 읽기 실패");
            return null;
        }
    }

    private static EventGridEvent[] TryParseEvents(
        BinaryData payload,
        ILogger logger)
    {
        try
        {
            return EventGridEvent.ParseMany(payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Event Grid payload 파싱 실패");
            return [];
        }
    }

    private static IResult TryBuildValidationResponse(
        BinaryData payload,
        ILogger logger)
    {
        var validationCode = TryGetValidationCode(payload);
        if (string.IsNullOrWhiteSpace(validationCode))
        {
            logger.LogWarning("Subscription validation code 누락");
            return Results.BadRequest(new { message = "Missing validation code" });
        }

        logger.LogInformation("Subscription validation 처리 완료");
        return Results.Ok(new { validationResponse = validationCode });
    }

    private static string? TryGetValidationCode(BinaryData payload)
    {
        using var doc = JsonDocument.Parse(payload.ToMemory());

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var validationCode = TryGetValidationCodeFromEventElement(element);
                if (!string.IsNullOrWhiteSpace(validationCode))
                {
                    return validationCode;
                }
            }

            return null;
        }

        return doc.RootElement.ValueKind == JsonValueKind.Object
            ? TryGetValidationCodeFromEventElement(doc.RootElement)
            : null;
    }

    private static string? TryGetValidationCodeFromEventElement(JsonElement eventElement)
    {
        if (!eventElement.TryGetProperty("data", out var dataElement) ||
            dataElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return dataElement.TryGetProperty("validationCode", out var codeProperty)
            ? codeProperty.GetString()
            : null;
    }
}
