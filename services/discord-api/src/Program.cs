using Discord;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using SettleUp.Observability;

LoadDotEnvIfExists();
ApplyEnvironmentAliases();

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSettleUpLogging(builder.Configuration);
builder.WebHost.UseUrls(builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5000");

var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "discord-api";
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

builder.Services.AddSingleton(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
});
builder.Services.AddSingleton(sp => new DiscordSocketClient(sp.GetRequiredService<DiscordSocketConfig>()));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<BlobUploaderProvider>();
builder.Services.AddSingleton<UserLanguagePreferenceStore>();
builder.Services.Configure<SettlementHistoryOptions>(builder.Configuration.GetSection(SettlementHistoryOptions.SectionName));
builder.Services.AddSingleton<ReceiptSessionStore>();
builder.Services.AddSingleton<ReceiptSessionLockManager>();
builder.Services.AddSingleton<ReceiptDraftTestDataLoader>();
builder.Services.AddSingleton<ReceiptMainMessageService>();
builder.Services.AddSingleton<ReceiptMainMessageDebounceService>();
builder.Services.AddSingleton<ReceiptPrivatePanelService>();
builder.Services.AddSingleton<ReceiptSelectionPanelService>();
builder.Services.AddSingleton<ReceiptSessionLifetimeService>();
builder.Services.AddSingleton<ReceiptDraftSessionService>();
builder.Services.AddSingleton<SettlementHistoryRepositoryProvider>();
builder.Services.AddSingleton<SettlementHistoryPersistenceService>();
builder.Services.AddSingleton<ReceiptInteractionService>();
builder.Services.AddSingleton<SettleUpCommandHandler>();
builder.Services.AddSingleton<CustomReceiptCommandHandler>();
builder.Services.AddSingleton<PingTestCommandHandler>();
builder.Services.AddSingleton<TestReceiptCommandHandler>();
builder.Services.AddSingleton<LanguageCommandHandler>();
builder.Services.AddSingleton<HistoryCommandHandler>();
builder.Services.AddHostedService<BlobUploaderWarmupService>();
builder.Services.AddHostedService<ReceiptSessionExpiryService>();
builder.Services.AddHostedService<DiscordBotWorker>();

var app = builder.Build();

app.MapPost("/getting_draft", GettingDraftEndpoint.HandleAsync);

await app.RunAsync();

static void LoadDotEnvIfExists()
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env")
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

static void ApplyEnvironmentAliases()
{
    CopyEnvironmentVariableIfMissing(
        targetName: "DISCORD_BOT_TOKEN",
        sourceName: "discord-bot-token");

    CopyEnvironmentVariableIfMissing(
        targetName: "APPLICATIONINSIGHTS_CONNECTION_STRING",
        sourceName: "applicationinsights-connection-string");
}

static void CopyEnvironmentVariableIfMissing(string targetName, string sourceName)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(targetName)))
    {
        return;
    }

    var sourceValue = Environment.GetEnvironmentVariable(sourceName);
    if (string.IsNullOrWhiteSpace(sourceValue))
    {
        return;
    }

    Environment.SetEnvironmentVariable(targetName, sourceValue);
}
