using Discord;
using Discord.WebSocket;

public sealed class ReceiptDraftSessionService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptMainMessageService _mainMessageService;
    private readonly ILogger<ReceiptDraftSessionService> _logger;

    public ReceiptDraftSessionService(
        DiscordSocketClient discordClient,
        ReceiptSessionStore sessionStore,
        ReceiptMainMessageService mainMessageService,
        ILogger<ReceiptDraftSessionService> logger)
    {
        _discordClient = discordClient;
        _sessionStore = sessionStore;
        _mainMessageService = mainMessageService;
        _logger = logger;
    }

    public async Task CreatePendingUploadSessionAsync(
        string blobUrl,
        string uploadedByUserId,
        string uploadedByDisplayName,
        string? paymentContact,
        IMessageChannel targetChannel,
        CancellationToken cancellationToken)
    {
        var tempReceiptId = $"pending-{Guid.NewGuid():N}";
        var session = ReceiptSessionStateService.CreatePendingUploadSession(
            tempReceiptId,
            blobUrl,
            uploadedByUserId,
            uploadedByDisplayName,
            paymentContact);

        session.UserDisplayNames[uploadedByUserId] = uploadedByDisplayName;

        await _mainMessageService.SendToChannelAsync(session, targetChannel, cancellationToken);

        _logger.LogInformation(
            "Pending receipt session created. ReceiptId={ReceiptId} UserId={UserId} ChannelId={ChannelId} MessageId={MessageId}",
            session.ReceiptId,
            uploadedByUserId,
            session.MainChannelId,
            session.MainMessageId);
    }

    public Task CreateOrUpdateSessionFromDraftAsync(
        ReceiptDraftNotificationRequest payload,
        CancellationToken cancellationToken,
        IMessageChannel? targetChannel = null)
    {
        return UpsertDraftSessionAsync(payload, cancellationToken, targetChannel, slashCommand: null);
    }

    public Task CreateOrUpdateSessionFromDraftAsync(
        ReceiptDraftNotificationRequest payload,
        SocketSlashCommand command,
        CancellationToken cancellationToken,
        IMessageChannel? targetChannel = null)
    {
        return UpsertDraftSessionAsync(payload, cancellationToken, targetChannel, command);
    }

    private async Task UpsertDraftSessionAsync(
        ReceiptDraftNotificationRequest payload,
        CancellationToken cancellationToken,
        IMessageChannel? targetChannel,
        SocketSlashCommand? slashCommand)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var receiptId = payload.ResolvedDraftId
            ?? throw new InvalidOperationException("draftId is required.");
        var uploadedByUserId = payload.UploadedByUserId
            ?? throw new InvalidOperationException("uploadedByUserId is required.");

        var displayName = await ResolveUploadedByDisplayNameAsync(uploadedByUserId);
        var session = FindExistingSession(payload, receiptId, out var previousReceiptId, out var previousBlobUrl)
            ?? ReceiptSessionStateService.CreateSessionFromDraft(payload, displayName);

        ReceiptSessionStateService.ApplyDraftPayload(session, payload, displayName);
        session.UserDisplayNames[uploadedByUserId] = displayName;
        session.UploadedByDisplayName = displayName;
        session.MainChannel ??= targetChannel;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (session.MainChannelId is not null && session.MainMessageId is not null)
        {
            await _mainMessageService.RefreshAsync(session);
        }
        else if (slashCommand is not null)
        {
            session.MainChannel ??= _mainMessageService.ResolveSlashCommandChannel(slashCommand);
            await _mainMessageService.SendToSlashCommandAsync(session, slashCommand);
        }
        else if (targetChannel is not null)
        {
            await _mainMessageService.SendToChannelAsync(session, targetChannel, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("기존 채널 세션이 없으면 draft 메시지를 채널에 보낼 수 없습니다.");
        }

        _sessionStore.AddOrUpdate(session, previousReceiptId, previousBlobUrl);

        _logger.LogInformation(
            "Receipt session upserted from draft. ReceiptId={ReceiptId} UserId={UserId} ChannelId={ChannelId} MessageId={MessageId} ItemCount={ItemCount}",
            session.ReceiptId,
            uploadedByUserId,
            session.MainChannelId,
            session.MainMessageId,
            session.Items.Count);
    }

    private ReceiptSessionState? FindExistingSession(
        ReceiptDraftNotificationRequest payload,
        string receiptId,
        out string? previousReceiptId,
        out string? previousBlobUrl)
    {
        previousReceiptId = null;
        previousBlobUrl = null;

        if (!string.IsNullOrWhiteSpace(payload.BlobUrl) &&
            _sessionStore.TryGetByBlobUrl(payload.BlobUrl, out var existingByBlob) &&
            existingByBlob is not null)
        {
            previousReceiptId = existingByBlob.ReceiptId;
            previousBlobUrl = existingByBlob.BlobUrl;
            return existingByBlob;
        }

        if (_sessionStore.TryGet(receiptId, out var existingByReceiptId) &&
            existingByReceiptId is not null)
        {
            previousBlobUrl = existingByReceiptId.BlobUrl;
            return existingByReceiptId;
        }

        return null;
    }

    private async Task<string> ResolveUploadedByDisplayNameAsync(string uploadedByUserId)
    {
        if (!ulong.TryParse(uploadedByUserId, out var userId))
        {
            throw new InvalidOperationException("uploadedByUserId must be a valid Discord user id.");
        }

        var user = await _discordClient.Rest.GetUserAsync(userId);
        if (user is null)
        {
            throw new InvalidOperationException($"Discord user '{uploadedByUserId}' could not be resolved.");
        }

        return user.GlobalName ?? user.Username;
    }
}
