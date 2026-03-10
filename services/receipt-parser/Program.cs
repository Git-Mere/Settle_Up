using DotNetEnv;
using receipt_parser.tests.LocalUploadTest;
using receipt_parser.Configuration;
using receipt_parser.Endpoints;
using receipt_parser.Observability;
using receipt_parser.Services;
using SettleUp.Observability;

LoadDotEnvIfExists();

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSettleUpLogging(builder.Configuration);

builder.Services.Configure<ReceiptParserOptions>(
    builder.Configuration.GetSection(ReceiptParserOptions.SectionName));

builder.Services.AddSingleton<DocumentIntelligenceReceiptParser>();
builder.Services.AddSingleton<CosmosReceiptRepository>();
builder.Services.AddSingleton<ReceiptParsedEventPublisher>();
builder.Services.AddSingleton<ReceiptProcessingService>();

var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "receipt-parser";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0";
builder.Services.AddSettleUpObservability(
    builder.Configuration,
    new SettleUpObservabilityOptions
    {
        ServiceName = serviceName,
        ServiceVersion = serviceVersion,
        ActivitySourceName = Telemetry.ActivitySourceName,
        IncludeAspNetCoreInstrumentation = true
    });

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("receipt-parser");

app.MapGet("/", () => Results.Ok("receipt-parser is running"));
app.MapPost("/api/events/blob-created", EventGridWebhookEndpoint.HandleAsync);

var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReceiptParserOptions>>().Value;
if (options.EnableLocalUploadTestEndpoint)
{
    app.MapPost("/api/tests/local-upload-parse", LocalUploadParseTestEndpoint.HandleAsync);
}

app.Lifetime.ApplicationStarted.Register(() =>
    logger.LogInformation("Receipt parser service started. LocalUploadTestEnabled={LocalUploadTestEnabled}", options.EnableLocalUploadTestEndpoint));
app.Lifetime.ApplicationStopping.Register(() =>
    logger.LogInformation("Receipt parser service stopping."));

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
