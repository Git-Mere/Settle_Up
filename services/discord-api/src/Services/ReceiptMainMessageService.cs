using System.Net;
using Discord;
using Discord.WebSocket;

public sealed class ReceiptMainMessageService
{
    private const int MaxDiscordRetryAttempts = 3;
    private readonly DiscordSocketClient _discordClient;
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ILogger<ReceiptMainMessageService> _logger;

    public ReceiptMainMessageService(
        DiscordSocketClient discordClient,
        ReceiptSessionStore sessionStore,
        ILogger<ReceiptMainMessageService> logger)
    {
        _discordClient = discordClient;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task SendToChannelAsync(
        ReceiptSessionState session,
        IMessageChannel targetChannel,
        CancellationToken cancellationToken)
    {
        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        var sentMessage = await ExecuteDiscordRetryAsync(
            operationName: "send_main_message_to_channel",
            operation: () => targetChannel.SendMessageAsync(
                embed: renderedMessage.Embed,
                components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
                options: new RequestOptions { CancelToken = cancellationToken }),
            cancellationToken);

        ApplyPublishedMessageMetadata(session, sentMessage, targetChannel);
        _sessionStore.AddOrUpdate(session);
    }

    public async Task SendToSlashCommandAsync(ReceiptSessionState session, SocketSlashCommand command)
    {
        var sentMessage = await ExecuteDiscordRetryAsync(
            operationName: "send_main_message_to_slash_followup",
            operation: () => command.FollowupAsync(
                embed: ReceiptMessageRenderer.RenderReceiptMessage(session).Embed,
                components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
                ephemeral: false));

        ApplyPublishedMessageMetadata(
            session,
            sentMessage,
            sentMessage.Channel as IMessageChannel ?? ResolveSlashCommandChannel(command));

        _sessionStore.AddOrUpdate(session);
    }

    public async Task RefreshAsync(ReceiptSessionState session)
    {
        if (session.MainChannelId is null || session.MainMessageId is null)
        {
            throw new InvalidOperationException("Receipt session is missing main message metadata.");
        }

        var channel = await ResolveMainChannelAsync(session);
        var message = session.MainMessage;
        if (message is null)
        {
            message = await ExecuteDiscordRetryAsync(
                operationName: "resolve_main_message",
                operation: async () => await channel.GetMessageAsync(session.MainMessageId.Value) as IUserMessage);
        }

        if (message is null)
        {
            throw new InvalidOperationException("Main message could not be resolved.");
        }

        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        await ExecuteDiscordRetryAsync(
            operationName: "refresh_main_message",
            operation: () => message.ModifyAsync(properties =>
            {
                properties.Embed = renderedMessage.Embed;
                properties.Components = ReceiptInteractionCustomIds.BuildMainMessageComponents(session);
            }));

        session.MainChannel = channel;
        session.MainMessage = message;
        _sessionStore.AddOrUpdate(session);
    }

    public async Task DeleteAsync(ReceiptSessionState session, CancellationToken cancellationToken = default)
    {
        if (session.MainMessageId is null)
        {
            return;
        }

        var message = session.MainMessage;
        if (message is null)
        {
            if (session.MainChannelId is null)
            {
                return;
            }

            var channel = await ResolveMainChannelAsync(session);
            message = await ExecuteDiscordRetryAsync(
                operationName: "resolve_main_message_for_delete",
                operation: async () => await channel.GetMessageAsync(session.MainMessageId.Value) as IUserMessage,
                cancellationToken);

            if (message is null)
            {
                return;
            }
        }

        await ExecuteDiscordRetryAsync(
            operationName: "delete_main_message",
            operation: () => message.DeleteAsync(new RequestOptions { CancelToken = cancellationToken }),
            cancellationToken);
    }

    public async Task PublishForComponentAsync(ReceiptSessionState session, SocketMessageComponent component)
    {
        var replacement = await ExecuteDiscordRetryAsync(
            operationName: "publish_main_message_for_component",
            operation: () => component.FollowupAsync(
                embed: ReceiptMessageRenderer.RenderReceiptMessage(session).Embed,
                components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
                ephemeral: false));

        ApplyPublishedMessageMetadata(
            session,
            replacement,
            replacement.Channel as IMessageChannel ?? ResolveInteractionChannel(component) ?? session.MainChannel);

        _sessionStore.AddOrUpdate(session);
    }

    public async Task PublishForModalAsync(ReceiptSessionState session, SocketModal modal)
    {
        var replacement = await ExecuteDiscordRetryAsync(
            operationName: "publish_main_message_for_modal",
            operation: () => modal.FollowupAsync(
                embed: ReceiptMessageRenderer.RenderReceiptMessage(session).Embed,
                components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
                ephemeral: false));

        ApplyPublishedMessageMetadata(
            session,
            replacement,
            replacement.Channel as IMessageChannel ?? ResolveInteractionChannel(modal) ?? session.MainChannel);

        _sessionStore.AddOrUpdate(session);
    }

    public IMessageChannel? ResolveSlashCommandChannel(SocketSlashCommand command)
    {
        if (command.Channel is IMessageChannel directChannel)
        {
            return directChannel;
        }

        if (command.ChannelId is ulong channelId &&
            _discordClient.GetChannel(channelId) is IMessageChannel cachedChannel)
        {
            return cachedChannel;
        }

        return null;
    }

    public static IMessageChannel? ResolveInteractionChannel(SocketMessageComponent component)
    {
        return component.Channel as IMessageChannel
            ?? component.Message.Channel as IMessageChannel;
    }

    public static IMessageChannel? ResolveInteractionChannel(SocketModal modal)
    {
        return modal.Channel as IMessageChannel;
    }

    private void ApplyPublishedMessageMetadata(
        ReceiptSessionState session,
        IUserMessage message,
        IMessageChannel? fallbackChannel)
    {
        var resolvedChannel = message.Channel as IMessageChannel ?? fallbackChannel ?? session.MainChannel;
        var guildId = resolvedChannel is IGuildChannel guildChannel
            ? guildChannel.GuildId
            : session.MainGuildId;

        session.MainMessageId = message.Id;
        session.MainChannel = resolvedChannel;
        session.MainMessage = message;
        session.MainChannelId = resolvedChannel?.Id ?? session.MainChannelId;
        session.MainGuildId = guildId;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private async Task<IMessageChannel> ResolveMainChannelAsync(ReceiptSessionState session)
    {
        if (session.MainChannel is not null)
        {
            return session.MainChannel;
        }

        if (session.MainChannelId is ulong channelId &&
            _discordClient.GetChannel(channelId) is IMessageChannel cachedChannel)
        {
            return cachedChannel;
        }

        if (session.MainChannelId is not ulong restChannelId)
        {
            throw new InvalidOperationException("Receipt session is missing main channel metadata.");
        }

        try
        {
            var restChannel = await _discordClient.Rest.GetChannelAsync(restChannelId) as IMessageChannel;
            if (restChannel is null)
            {
                throw new InvalidOperationException("Main message channel could not be resolved.");
            }

            return restChannel;
        }
        catch (Discord.Net.HttpException ex) when ((int?)ex.DiscordCode == 50001)
        {
            throw new InvalidOperationException(
                $"메인 메시지 채널에 접근할 수 없습니다. ChannelId={restChannelId}",
                ex);
        }
    }

    private async Task ExecuteDiscordRetryAsync(
        string operationName,
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteDiscordRetryAsync<object?>(
            operationName,
            async () =>
            {
                await operation();
                return null;
            },
            cancellationToken);
    }

    private async Task<T> ExecuteDiscordRetryAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation();
            }
            catch (Discord.Net.HttpException ex) when (IsRetryableDiscordHttpException(ex) && attempt < MaxDiscordRetryAttempts)
            {
                var delay = GetRetryDelay(attempt);
                _logger.LogWarning(
                    ex,
                    "Discord API call failed temporarily. Operation={Operation} Attempt={Attempt} DelayMs={DelayMs} HttpCode={HttpCode} DiscordCode={DiscordCode}",
                    operationName,
                    attempt,
                    delay.TotalMilliseconds,
                    ex.HttpCode,
                    ex.DiscordCode);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsRetryableDiscordHttpException(Discord.Net.HttpException ex)
    {
        return ex.HttpCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        return attempt switch
        {
            1 => TimeSpan.FromMilliseconds(400),
            2 => TimeSpan.FromMilliseconds(1200),
            _ => TimeSpan.FromSeconds(2)
        };
    }
}
