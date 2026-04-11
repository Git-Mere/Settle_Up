using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace receipt_parser.Observability;

public static class Telemetry
{
    public const string ActivitySourceName = "SettleUp.ReceiptParser";
    public const string MeterName = "SettleUp.ReceiptParser";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ReceiptParseSucceededCounter =
        Meter.CreateCounter<long>("receipt_parse_succeeded_total");

    public static readonly Counter<long> ReceiptParseFailedCounter =
        Meter.CreateCounter<long>("receipt_parse_failed_total");

    public static readonly Counter<long> DiscordCallbackSucceededCounter =
        Meter.CreateCounter<long>("discord_callback_succeeded_total");

    public static readonly Counter<long> DiscordCallbackFailedCounter =
        Meter.CreateCounter<long>("discord_callback_failed_total");

    public static readonly Counter<long> DiscordCallbackRetryCounter =
        Meter.CreateCounter<long>("discord_callback_retry_total");

    public static readonly Histogram<double> ReceiptParseDurationMs =
        Meter.CreateHistogram<double>("receipt_parse_duration_ms");

    public static readonly Histogram<double> DiscordCallbackDurationMs =
        Meter.CreateHistogram<double>("discord_callback_duration_ms");

    public static readonly Histogram<double> CosmosWriteDurationMs =
        Meter.CreateHistogram<double>("cosmos_write_duration_ms");
}
