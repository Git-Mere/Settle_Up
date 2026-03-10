using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SettleUp.Observability;

public static class SettleUpObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddSettleUpObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        SettleUpObservabilityOptions options)
    {
        var appInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        services.AddSingleton(options);
        services.AddHostedService<ObservabilityStartupLogger>();

        services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(options.ServiceName, serviceVersion: options.ServiceVersion))
                    .AddSource(options.ActivitySourceName)
                    .AddHttpClientInstrumentation();

                if (options.IncludeAspNetCoreInstrumentation)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
                {
                    tracing.AddAzureMonitorTraceExporter(exporterOptions =>
                    {
                        exporterOptions.ConnectionString = appInsightsConnectionString;
                    });
                }
            });

        return services;
    }
}
