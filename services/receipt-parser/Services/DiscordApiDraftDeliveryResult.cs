using System.Net;

namespace receipt_parser.Services;

public sealed record DiscordApiDraftDeliveryResult(
    int AttemptCount,
    HttpStatusCode StatusCode);
