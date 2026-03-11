using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using receipt_parser.Configuration;
using receipt_parser.Models;
using receipt_parser.Observability;

namespace receipt_parser.Services;

public sealed class DiscordApiDraftClient
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8)
    ];

    private const int MaxRetries = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ReceiptParserOptions _options;
    private readonly ILogger<DiscordApiDraftClient> _logger;

    public DiscordApiDraftClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ReceiptParserOptions> options,
        ILogger<DiscordApiDraftClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DiscordApiDraftDeliveryResult> SendDraftAsync(
        DiscordDraftNotificationPayload payload,
        bool preferLocalTestUrl,
        CancellationToken cancellationToken)
    {
        var targetUri = ResolveTargetUri(preferLocalTestUrl);
        var displayTarget = GetDisplayTarget(targetUri);
        var maxAttempts = MaxRetries + 1;

        using var activity = Telemetry.ActivitySource.StartActivity("receipt_parser.discord_api.send");
        activity?.SetTag("receipt.id", payload.Id);
        activity?.SetTag("http.method", "POST");
        activity?.SetTag("http.url", displayTarget);

        _logger.LogInformation(
            "Discord API send started. ReceiptId={ReceiptId} TargetUrl={TargetUrl} PreferLocalTestUrl={PreferLocalTestUrl}",
            payload.Id,
            displayTarget,
            preferLocalTestUrl);

        using var httpClient = _httpClientFactory.CreateClient("discord-api-draft");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, targetUri)
                {
                    Content = JsonContent.Create(payload)
                };

                using var response = await httpClient.SendAsync(request, cancellationToken);
                activity?.SetTag("http.status_code", (int)response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Discord API send succeeded. ReceiptId={ReceiptId} StatusCode={StatusCode} Attempt={Attempt} TargetUrl={TargetUrl}",
                        payload.Id,
                        (int)response.StatusCode,
                        attempt,
                        displayTarget);

                    return new DiscordApiDraftDeliveryResult(attempt, response.StatusCode);
                }

                if (!IsRetryableStatusCode(response.StatusCode))
                {
                    _logger.LogError(
                        "Discord API send failed without retry. ReceiptId={ReceiptId} StatusCode={StatusCode} Attempt={Attempt} TargetUrl={TargetUrl}",
                        payload.Id,
                        (int)response.StatusCode,
                        attempt,
                        displayTarget);

                    throw new DiscordApiDraftDeliveryException(
                        $"discord-api returned non-retryable status code {(int)response.StatusCode}.",
                        attempt,
                        response.StatusCode);
                }

                if (attempt == maxAttempts)
                {
                    _logger.LogError(
                        "Discord API send failed after retries exhausted. ReceiptId={ReceiptId} StatusCode={StatusCode} Attempt={Attempt} TargetUrl={TargetUrl}",
                        payload.Id,
                        (int)response.StatusCode,
                        attempt,
                        displayTarget);

                    throw new DiscordApiDraftDeliveryException(
                        $"discord-api returned retryable status code {(int)response.StatusCode} after retries were exhausted.",
                        attempt,
                        response.StatusCode);
                }

                var delay = RetryDelays[attempt - 1];
                _logger.LogWarning(
                    "Discord API send will retry after response failure. ReceiptId={ReceiptId} StatusCode={StatusCode} Attempt={Attempt} NextAttempt={NextAttempt} DelaySeconds={DelaySeconds} TargetUrl={TargetUrl}",
                    payload.Id,
                    (int)response.StatusCode,
                    attempt,
                    attempt + 1,
                    delay.TotalSeconds,
                    displayTarget);

                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Discord API send timed out after retries exhausted. ReceiptId={ReceiptId} Attempt={Attempt} TargetUrl={TargetUrl}",
                        payload.Id,
                        attempt,
                        displayTarget);

                    throw new DiscordApiDraftDeliveryException(
                        "discord-api request timed out after retries were exhausted.",
                        attempt,
                        innerException: ex);
                }

                var delay = RetryDelays[attempt - 1];
                _logger.LogWarning(
                    ex,
                    "Discord API send timed out and will retry. ReceiptId={ReceiptId} Attempt={Attempt} NextAttempt={NextAttempt} DelaySeconds={DelaySeconds} TargetUrl={TargetUrl}",
                    payload.Id,
                    attempt,
                    attempt + 1,
                    delay.TotalSeconds,
                    displayTarget);

                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                if (attempt == maxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Discord API send failed after retries exhausted. ReceiptId={ReceiptId} Attempt={Attempt} TargetUrl={TargetUrl}",
                        payload.Id,
                        attempt,
                        displayTarget);

                    throw new DiscordApiDraftDeliveryException(
                        "discord-api request failed after retries were exhausted.",
                        attempt,
                        innerException: ex);
                }

                var delay = RetryDelays[attempt - 1];
                _logger.LogWarning(
                    ex,
                    "Discord API send failed and will retry. ReceiptId={ReceiptId} Attempt={Attempt} NextAttempt={NextAttempt} DelaySeconds={DelaySeconds} TargetUrl={TargetUrl}",
                    payload.Id,
                    attempt,
                    attempt + 1,
                    delay.TotalSeconds,
                    displayTarget);

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new DiscordApiDraftDeliveryException(
            "discord-api request failed before a response was received.",
            maxAttempts);
    }

    private Uri ResolveTargetUri(bool preferLocalTestUrl)
    {
        var configuredUrl = preferLocalTestUrl
            ? _options.DiscordApiUrlLocalTest ?? _options.DiscordApiUrl
            : _options.DiscordApiUrl ?? _options.DiscordApiUrlLocalTest;

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            throw new InvalidOperationException(
                "ReceiptParser:DiscordApiUrl 또는 ReceiptParser:DiscordApiUrl_local_test 설정이 필요합니다.");
        }

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var targetUri))
        {
            throw new InvalidOperationException("discord-api 대상 URL이 올바른 절대 URL이 아닙니다.");
        }

        return targetUri;
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        var numericCode = (int)statusCode;
        return numericCode >= 500 ||
               statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests;
    }

    private static string GetDisplayTarget(Uri targetUri)
    {
        return targetUri.GetLeftPart(UriPartial.Path);
    }
}
