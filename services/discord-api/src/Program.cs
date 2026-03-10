using Discord;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SettleUp.Observability;

LoadDotEnvIfExists();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddSettleUpLogging(builder.Configuration);

var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "discord-api";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0";

builder.Services.AddSettleUpObservability(
    builder.Configuration,
    new SettleUpObservabilityOptions
    {
        ServiceName = serviceName,
        ServiceVersion = serviceVersion,
        ActivitySourceName = Telemetry.ActivitySourceName,
        IncludeAspNetCoreInstrumentation = false
    });

builder.Services.AddSingleton(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
});
builder.Services.AddSingleton(sp => new DiscordSocketClient(sp.GetRequiredService<DiscordSocketConfig>()));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<BlobUploaderProvider>();
builder.Services.AddSingleton<SettleUpCommandHandler>();
builder.Services.AddSingleton<PingTestCommandHandler>();
builder.Services.AddHostedService<DiscordBotWorker>();

await builder.Build().RunAsync();

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
