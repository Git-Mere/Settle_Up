using System.Net;

namespace receipt_parser.Services;

public sealed class DiscordApiDraftDeliveryException : Exception
{
    public DiscordApiDraftDeliveryException(
        string message,
        int attemptCount,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        AttemptCount = attemptCount;
        StatusCode = statusCode;
    }

    public int AttemptCount { get; }

    public HttpStatusCode? StatusCode { get; }
}
