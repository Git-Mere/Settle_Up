using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SettleUp.Observability;

public static class SettleUpConsoleLoggingExtensions
{
    public static ILoggingBuilder AddSettleUpLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            options.IncludeScopes = false;
        });

        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        logging.AddFilter("Azure.Core", LogLevel.Warning);
        logging.AddFilter("Azure.Identity", LogLevel.Warning);

        var appInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            logging.AddApplicationInsights(
                configureTelemetryConfiguration: telemetryConfiguration =>
                {
                    telemetryConfiguration.ConnectionString = appInsightsConnectionString;
                },
                configureApplicationInsightsLoggerOptions: options =>
                {
                    options.IncludeScopes = true;
                    options.TrackExceptionsAsExceptionTelemetry = true;
                });

            logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>(
                string.Empty,
                LogLevel.Information);
        }

        return logging;
    }
}
