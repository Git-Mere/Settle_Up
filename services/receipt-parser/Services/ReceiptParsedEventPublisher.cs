using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Options;
using receipt_parser.Configuration;
using receipt_parser.Models;
using receipt_parser.Observability;

namespace receipt_parser.Services;

public sealed class ReceiptParsedEventPublisher
{
    private readonly ILogger<ReceiptParsedEventPublisher> _logger;
    private readonly ReceiptParserOptions _options;
    private readonly EventGridPublisherClient? _publisherClient;

    public ReceiptParsedEventPublisher(
        IOptions<ReceiptParserOptions> options,
        ILogger<ReceiptParsedEventPublisher> logger)
    {
        _logger = logger;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.DownstreamEventGridTopicEndpoint) &&
            !string.IsNullOrWhiteSpace(_options.DownstreamEventGridTopicKey))
        {
            _publisherClient = new EventGridPublisherClient(
                new Uri(_options.DownstreamEventGridTopicEndpoint),
                new AzureKeyCredential(_options.DownstreamEventGridTopicKey));
        }
    }

    public async Task PublishAsync(ReceiptParsedEventPayload payload, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.event.publish");
        activity?.SetTag("receipt.id", payload.Id);

        if (_publisherClient is null)
        {
            _logger.LogWarning("Receipt parsed event publish skipped; downstream Event Grid is not configured. ReceiptId={ReceiptId}", payload.Id);
            return;
        }

        _logger.LogInformation("Receipt parsed event publish started. ReceiptId={ReceiptId} EventType={EventType}", payload.Id, _options.DownstreamEventType);

        var eventGridEvent = new EventGridEvent(
            subject: $"receipts/{payload.Id}",
            eventType: _options.DownstreamEventType,
            dataVersion: "1.0",
            data: payload);

        try
        {
            await _publisherClient.SendEventAsync(eventGridEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Receipt parsed event publish failed. ReceiptId={ReceiptId}", payload.Id);
            throw;
        }

        _logger.LogInformation("Receipt parsed event publish completed. ReceiptId={ReceiptId}", payload.Id);
    }
}
