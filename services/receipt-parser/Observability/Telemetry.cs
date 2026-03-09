using System.Diagnostics;

namespace receipt_parser.Observability;

public static class Telemetry
{
    public const string ActivitySourceName = "SettleUp.ReceiptParser";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
