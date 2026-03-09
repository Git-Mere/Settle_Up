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
        using var activity = Telemetry.ActivitySource.StartActivity("receipt.event.publish");
        activity?.SetTag("receipt.id", payload.Id);

        if (_publisherClient is null)
        {
            _logger.LogWarning("Downstream Event Grid 설정이 없어 이벤트 발행을 건너뜁니다. ReceiptId={ReceiptId}", payload.Id);
            return;
        }

        var eventGridEvent = new EventGridEvent(
            subject: $"receipts/{payload.Id}",
            eventType: _options.DownstreamEventType,
            dataVersion: "1.0",
            data: payload);

        await _publisherClient.SendEventAsync(eventGridEvent, cancellationToken);
        _logger.LogInformation("Downstream Event Grid 발행 완료. ReceiptId={ReceiptId}", payload.Id);
    }
}
