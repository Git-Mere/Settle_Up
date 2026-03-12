using Discord;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using SettleUp.Observability;

LoadDotEnvIfExists();

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
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
});
builder.Services.AddSingleton(sp => new DiscordSocketClient(sp.GetRequiredService<DiscordSocketConfig>()));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<BlobUploaderProvider>();
builder.Services.AddSingleton<ReceiptSessionStore>();
builder.Services.AddSingleton<ReceiptDraftTestDataLoader>();
builder.Services.AddSingleton<ReceiptInteractionService>();
builder.Services.AddSingleton<SettleUpCommandHandler>();
builder.Services.AddSingleton<PingTestCommandHandler>();
builder.Services.AddSingleton<TestReceiptCommandHandler>();
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
