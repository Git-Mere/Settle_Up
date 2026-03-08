using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using DotNetEnv;

Env.Load("../.env");

var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("환경 변수 DISCORD_BOT_TOKEN 이 필요합니다.");
    return;
}

using var httpClient = new HttpClient();
var blobUploader = BlobImageUploader.CreateFromEnvironment(httpClient, out var blobConfigError);
if (blobUploader is null)
{
    Console.Error.WriteLine($"Blob 업로더 비활성화: {blobConfigError}");
}

var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "discord-api";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0";

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion);

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(Telemetry.ActivitySourceName)
    .AddHttpClientInstrumentation()
    .AddConsoleExporter()
    .Build();

var client = new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
});

var settleUpHandler = new SettleUpCommandHandler(blobUploader);
var pingTestHandler = new PingTestCommandHandler();

var commandRegistered = false;

client.Log += message =>
{
    Console.WriteLine(message.ToString());
    return Task.CompletedTask;
};

client.Ready += async () =>
{
    using var activity = Telemetry.ActivitySource.StartActivity("discord.ready");
    activity?.SetTag("discord.command_registration_attempted", true);

    if (commandRegistered)
    {
        activity?.SetTag("discord.command_registration_skipped", true);
        return;
    }

    await client.Rest.CreateGlobalCommand(SettleUpCommandHandler.BuildCommand());
    await client.Rest.CreateGlobalCommand(PingTestCommandHandler.BuildCommand());

    activity?.SetTag("discord.commands_registered", 2);
    commandRegistered = true;
};

client.SlashCommandExecuted += async command =>
{
    using var activity = Telemetry.ActivitySource.StartActivity("discord.slash_command.execute");
    activity?.SetTag("discord.command.name", command.Data.Name);
    activity?.SetTag("discord.user.id", command.User.Id.ToString());
    activity?.SetTag("discord.guild.id", command.GuildId?.ToString());

    try
    {
        if (string.Equals(command.Data.Name, SettleUpCommandHandler.CommandName, StringComparison.OrdinalIgnoreCase))
        {
            var status = await settleUpHandler.HandleSlashCommandAsync(command);
            activity?.SetTag("discord.command.status", status);
            return;
        }

        if (string.Equals(command.Data.Name, PingTestCommandHandler.CommandName, StringComparison.OrdinalIgnoreCase))
        {
            var status = await pingTestHandler.HandleSlashCommandAsync(command);
            activity?.SetTag("discord.command.status", status);
            return;
        }

        activity?.SetTag("discord.command.status", "unknown");
    }
    catch (Exception ex)
    {
        activity?.SetTag("discord.command.status", "error");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
        throw;
    }
};

client.ButtonExecuted += async component =>
{
    using var activity = Telemetry.ActivitySource.StartActivity("discord.button.execute");
    activity?.SetTag("discord.component.custom_id", component.Data.CustomId);
    activity?.SetTag("discord.user.id", component.User.Id.ToString());
    activity?.SetTag("discord.guild.id", component.GuildId?.ToString());

    try
    {
        var status = await settleUpHandler.HandleButtonAsync(component);
        if (!string.IsNullOrWhiteSpace(status))
        {
            activity?.SetTag("discord.button.status", status);
        }
    }
    catch (Exception ex)
    {
        activity?.SetTag("discord.button.status", "error");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
        throw;
    }
};

client.ModalSubmitted += async modal =>
{
    using var activity = Telemetry.ActivitySource.StartActivity("discord.modal.submit");
    activity?.SetTag("discord.modal.custom_id", modal.Data.CustomId);
    activity?.SetTag("discord.user.id", modal.User.Id.ToString());
    activity?.SetTag("discord.guild.id", modal.GuildId?.ToString());

    try
    {
        var status = await settleUpHandler.HandleModalAsync(modal);
        if (!string.IsNullOrWhiteSpace(status))
        {
            activity?.SetTag("discord.modal.status", status);
        }
    }
    catch (Exception ex)
    {
        activity?.SetTag("discord.modal.status", "error");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
        throw;
    }
};

client.MessageReceived += async message =>
{
    if (message.Author.IsBot)
    {
        return;
    }

    if (string.Equals(message.Content.Trim(), "ping", StringComparison.OrdinalIgnoreCase))
    {
        using var activity = Telemetry.ActivitySource.StartActivity("discord.message.ping");
        activity?.SetTag("discord.channel.id", message.Channel.Id.ToString());
        await message.Channel.SendMessageAsync("pong");
    }
};

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();
await Task.Delay(Timeout.InfiniteTimeSpan);
