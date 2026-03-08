using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("환경 변수 DISCORD_BOT_TOKEN 이 필요합니다.");
    return;
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

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter(Telemetry.MeterName)
    .AddConsoleExporter()
    .Build();

var client = new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
});

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

    var settleUpCommand = new SlashCommandBuilder()
        .WithName("settle-up")
        .WithDescription("정산 이미지 업로드를 시작합니다.")
        .Build();

    var pingTestCommand = new SlashCommandBuilder()
        .WithName("pingtest")
        .WithDescription("봇 응답을 테스트합니다.")
        .Build();

    await client.Rest.CreateGlobalCommand(settleUpCommand);
    await client.Rest.CreateGlobalCommand(pingTestCommand);

    Telemetry.CommandsRegisteredCounter.Add(2);
    activity?.SetTag("discord.commands_registered", 2);
    commandRegistered = true;
};

client.SlashCommandExecuted += async command =>
{
    using var activity = Telemetry.ActivitySource.StartActivity("discord.slash_command.execute");
    activity?.SetTag("discord.command.name", command.Data.Name);
    activity?.SetTag("discord.user.id", command.User.Id.ToString());
    activity?.SetTag("discord.guild.id", command.GuildId?.ToString());

    var commandStart = Stopwatch.StartNew();

    try
    {
        if (string.Equals(command.Data.Name, "settle-up", StringComparison.OrdinalIgnoreCase))
        {
            await command.RespondAsync("이미지를 업로드하십시오. (2분 내 첨부)", ephemeral: true);

            if (command.ChannelId is null)
            {
                Telemetry.SlashCommandsCounter.Add(1, new KeyValuePair<string, object?>("command", "settle-up"), new KeyValuePair<string, object?>("status", "missing_channel"));
                await command.FollowupAsync("채널 정보를 확인할 수 없습니다. 서버 채널에서 다시 시도해 주세요.", ephemeral: true);
                return;
            }

            var waitStart = Stopwatch.StartNew();
            var imageMessage = await WaitForImageUploadAsync(
                client,
                command.User.Id,
                command.ChannelId.Value,
                TimeSpan.FromMinutes(1));
            Telemetry.ImageWaitDurationMs.Record(waitStart.Elapsed.TotalMilliseconds);

            if (imageMessage is null)
            {
                Telemetry.ImageUploadTimeoutCounter.Add(1);
                Telemetry.SlashCommandsCounter.Add(1, new KeyValuePair<string, object?>("command", "settle-up"), new KeyValuePair<string, object?>("status", "timeout"));
                await command.FollowupAsync("시간이 초과되었습니다. 다시 `/settle-up`을 실행해 주세요.", ephemeral: true);
                return;
            }

            var attachment = imageMessage.Attachments.First();
            Telemetry.SlashCommandsCounter.Add(1, new KeyValuePair<string, object?>("command", "settle-up"), new KeyValuePair<string, object?>("status", "success"));
            await command.FollowupAsync($"이미지 업로드 확인: {attachment.Url}", ephemeral: true);
            return;
        }

        if (string.Equals(command.Data.Name, "pingtest", StringComparison.OrdinalIgnoreCase))
        {
            Telemetry.SlashCommandsCounter.Add(1, new KeyValuePair<string, object?>("command", "pingtest"), new KeyValuePair<string, object?>("status", "success"));
            await command.RespondAsync("pong! slash command 정상 작동 중입니다.", ephemeral: true);
            return;
        }

        Telemetry.SlashCommandsCounter.Add(1, new KeyValuePair<string, object?>("command", command.Data.Name), new KeyValuePair<string, object?>("status", "unknown"));
    }
    catch (Exception ex)
    {
        Telemetry.SlashCommandsCounter.Add(1, new KeyValuePair<string, object?>("command", command.Data.Name), new KeyValuePair<string, object?>("status", "error"));
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
        throw;
    }
    finally
    {
        Telemetry.SlashCommandDurationMs.Record(commandStart.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("command", command.Data.Name));
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

static async Task<SocketUserMessage?> WaitForImageUploadAsync(
    DiscordSocketClient client,
    ulong userId,
    ulong channelId,
    TimeSpan timeout)
{
    using var activity = Telemetry.ActivitySource.StartActivity("discord.wait_for_image");
    activity?.SetTag("discord.user.id", userId.ToString());
    activity?.SetTag("discord.channel.id", channelId.ToString());
    activity?.SetTag("timeout.seconds", timeout.TotalSeconds);

    var tcs = new TaskCompletionSource<SocketUserMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

    Task Handler(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message)
        {
            return Task.CompletedTask;
        }

        if (message.Author.IsBot || message.Author.Id != userId || message.Channel.Id != channelId)
        {
            return Task.CompletedTask;
        }

        if (message.Attachments.Any(a => a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true))
        {
            tcs.TrySetResult(message);
        }

        return Task.CompletedTask;
    }

    client.MessageReceived += Handler;

    try
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        if (completed != tcs.Task)
        {
            activity?.SetTag("result", "timeout");
            return null;
        }

        activity?.SetTag("result", "received");
        return await tcs.Task;
    }
    finally
    {
        client.MessageReceived -= Handler;
    }
}
