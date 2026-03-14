using Discord;
using Discord.WebSocket;

public sealed class ReceiptMainMessageService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly ReceiptSessionStore _sessionStore;

    public ReceiptMainMessageService(
        DiscordSocketClient discordClient,
        ReceiptSessionStore sessionStore)
    {
        _discordClient = discordClient;
        _sessionStore = sessionStore;
    }

    public async Task SendToChannelAsync(
        ReceiptSessionState session,
        IMessageChannel targetChannel,
        CancellationToken cancellationToken)
    {
        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        var sentMessage = await targetChannel.SendMessageAsync(
            embed: renderedMessage.Embed,
            components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
            options: new RequestOptions { CancelToken = cancellationToken });

        ApplyPublishedMessageMetadata(session, sentMessage, targetChannel);
        _sessionStore.AddOrUpdate(session);
    }

    public async Task SendToSlashCommandAsync(ReceiptSessionState session, SocketSlashCommand command)
    {
        var sentMessage = await command.FollowupAsync(
            embed: ReceiptMessageRenderer.RenderReceiptMessage(session).Embed,
            components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
            ephemeral: false);

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
        var message = await channel.GetMessageAsync(session.MainMessageId.Value) as IUserMessage;
        if (message is null)
        {
            throw new InvalidOperationException("Main message could not be resolved.");
        }

        var renderedMessage = ReceiptMessageRenderer.RenderReceiptMessage(session);
        await message.ModifyAsync(properties =>
        {
            properties.Embed = renderedMessage.Embed;
            properties.Components = ReceiptInteractionCustomIds.BuildMainMessageComponents(session);
        });

        session.MainChannel = channel;
        _sessionStore.AddOrUpdate(session);
    }

    public async Task PublishForComponentAsync(ReceiptSessionState session, SocketMessageComponent component)
    {
        var replacement = await component.FollowupAsync(
            embed: ReceiptMessageRenderer.RenderReceiptMessage(session).Embed,
            components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
            ephemeral: false);

        ApplyPublishedMessageMetadata(
            session,
            replacement,
            replacement.Channel as IMessageChannel ?? ResolveInteractionChannel(component) ?? session.MainChannel);

        _sessionStore.AddOrUpdate(session);
    }

    public async Task PublishForModalAsync(ReceiptSessionState session, SocketModal modal)
    {
        var replacement = await modal.FollowupAsync(
            embed: ReceiptMessageRenderer.RenderReceiptMessage(session).Embed,
            components: ReceiptInteractionCustomIds.BuildMainMessageComponents(session),
            ephemeral: false);

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

        session.MainMessageId = message.Id;
        session.MainChannel = resolvedChannel;
        session.MainChannelId = resolvedChannel?.Id ?? session.MainChannelId;
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
}
