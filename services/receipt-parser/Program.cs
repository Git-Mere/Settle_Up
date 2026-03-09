using DotNetEnv;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using receipt_parser.tests.LocalUploadTest;
using receipt_parser.Configuration;
using receipt_parser.Endpoints;
using receipt_parser.Observability;
using receipt_parser.Services;

LoadDotEnvIfExists();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReceiptParserOptions>(
    builder.Configuration.GetSection(ReceiptParserOptions.SectionName));

builder.Services.AddSingleton<DocumentIntelligenceReceiptParser>();
builder.Services.AddSingleton<CosmosReceiptRepository>();
builder.Services.AddSingleton<ReceiptParsedEventPublisher>();
builder.Services.AddSingleton<ReceiptProcessingService>();

var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "receipt-parser";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0";
var resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion: serviceVersion);

builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(resourceBuilder)
            .AddSource(Telemetry.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

var app = builder.Build();

app.MapGet("/", () => Results.Ok("receipt-parser is running"));
app.MapPost("/api/events/blob-created", EventGridWebhookEndpoint.HandleAsync);

var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReceiptParserOptions>>().Value;
if (options.EnableLocalUploadTestEndpoint)
{
    app.MapPost("/api/tests/local-upload-parse", LocalUploadParseTestEndpoint.HandleAsync);
}

app.Run();

static void LoadDotEnvIfExists()
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "services", "receipt-parser", ".env")
    };

    foreach (var path in candidates)
    {
        if (!File.Exists(path))
        {
            continue;
        }

        Env.Load(path);
        break;
    }
}
