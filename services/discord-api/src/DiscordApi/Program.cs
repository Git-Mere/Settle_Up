using Discord;
using Discord.WebSocket;

var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("환경 변수 DISCORD_BOT_TOKEN 이 필요합니다.");
    return;
}

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
    if (commandRegistered)
    {
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

    commandRegistered = true;
};

client.SlashCommandExecuted += async command =>
{
    if (string.Equals(command.Data.Name, "settle-up", StringComparison.OrdinalIgnoreCase))
    {
        await command.RespondAsync("이미지를 업로드하십시오. (2분 내 첨부)", ephemeral: true);

        if (command.ChannelId is null)
        {
            await command.FollowupAsync("채널 정보를 확인할 수 없습니다. 서버 채널에서 다시 시도해 주세요.", ephemeral: true);
            return;
        }

        var imageMessage = await WaitForImageUploadAsync(
            client,
            command.User.Id,
            command.ChannelId.Value,
            TimeSpan.FromMinutes(2));

        if (imageMessage is null)
        {
            await command.FollowupAsync("시간이 초과되었습니다. 다시 `/settle-up`을 실행해 주세요.", ephemeral: true);
            return;
        }

        var attachment = imageMessage.Attachments.First();
        await command.FollowupAsync($"이미지 업로드 확인: {attachment.Url}", ephemeral: true);
        return;
    }

    if (string.Equals(command.Data.Name, "pingtest", StringComparison.OrdinalIgnoreCase))
    {
        await command.RespondAsync("pong! slash command 정상 작동 중입니다.", ephemeral: true);
        return;
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
            return null;
        }

        return await tcs.Task;
    }
    finally
    {
        client.MessageReceived -= Handler;
    }
}
