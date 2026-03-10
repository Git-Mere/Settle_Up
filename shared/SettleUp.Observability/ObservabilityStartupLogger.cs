using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SettleUp.Observability;

internal sealed class ObservabilityStartupLogger : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ObservabilityStartupLogger> _logger;
    private readonly SettleUpObservabilityOptions _options;

    public ObservabilityStartupLogger(
        IConfiguration configuration,
        ILogger<ObservabilityStartupLogger> logger,
        SettleUpObservabilityOptions options)
    {
        _configuration = configuration;
        _logger = logger;
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            _logger.LogWarning(
                "Azure Monitor trace and log exporters are disabled for {ServiceName}; APPLICATIONINSIGHTS_CONNECTION_STRING is not configured.",
                _options.ServiceName);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Azure Monitor trace and log exporters enabled for {ServiceName}.",
            _options.ServiceName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
