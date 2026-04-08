using Discord;
using Discord.WebSocket;

public sealed class ReceiptDraftSessionService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptSessionLockManager _lockManager;
    private readonly ReceiptMainMessageService _mainMessageService;
    private readonly ReceiptMainMessageDebounceService _debounceService;
    private readonly ReceiptSessionLifetimeService _sessionLifetimeService;
    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<ReceiptDraftSessionService> _logger;

    public ReceiptDraftSessionService(
        DiscordSocketClient discordClient,
        ReceiptSessionStore sessionStore,
        ReceiptSessionLockManager lockManager,
        ReceiptMainMessageService mainMessageService,
        ReceiptMainMessageDebounceService debounceService,
        ReceiptSessionLifetimeService sessionLifetimeService,
        UserLanguagePreferenceStore languagePreferenceStore,
        ILogger<ReceiptDraftSessionService> logger)
    {
        _discordClient = discordClient;
        _sessionStore = sessionStore;
        _lockManager = lockManager;
        _mainMessageService = mainMessageService;
        _debounceService = debounceService;
        _sessionLifetimeService = sessionLifetimeService;
        _languagePreferenceStore = languagePreferenceStore;
        _logger = logger;
    }

    public async Task<ReceiptSessionState> CreatePendingUploadSessionAndReturnAsync(
        string uploadedByUserId,
        string uploadedByDisplayName,
        string? paymentContact,
        IMessageChannel targetChannel,
        CancellationToken cancellationToken)
    {
        var tempReceiptId = $"pending-{Guid.NewGuid():N}";
        ReceiptSessionState? createdSession = null;

        await _lockManager.ExecuteAsync(tempReceiptId, async () =>
        {
            var session = ReceiptSessionStateService.CreatePendingUploadSession(
                tempReceiptId,
                blobUrl: null,
                uploadedByUserId,
                uploadedByDisplayName,
                paymentContact);
            session.PublicLanguage = _languagePreferenceStore.GetLanguage(uploadedByUserId);

            session.UserDisplayNames[uploadedByUserId] = uploadedByDisplayName;

            await _mainMessageService.SendToChannelAsync(session, targetChannel, cancellationToken);
            _sessionStore.AddOrUpdate(session);
            createdSession = session;

            _logger.LogInformation(
                "Pending receipt session created before upload. ReceiptId={ReceiptId} UserId={UserId} ChannelId={ChannelId} MessageId={MessageId}",
                session.ReceiptId,
                uploadedByUserId,
                session.MainChannelId,
                session.MainMessageId);
        }, cancellationToken);

        return createdSession ?? throw new InvalidOperationException("Pending session creation failed.");
    }

    public async Task AttachBlobUrlToPendingSessionAsync(
        string receiptId,
        string blobUrl,
        CancellationToken cancellationToken)
    {
        await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                throw new InvalidOperationException("Pending receipt session could not be found.");
            }

            var previousBlobUrl = session.BlobUrl;
            session.BlobUrl = blobUrl;
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;
            _sessionStore.AddOrUpdate(session, previousBlobUrl: previousBlobUrl);
            await Task.CompletedTask;
        }, cancellationToken);
    }

    public async Task DeletePendingUploadSessionAsync(string receiptId, CancellationToken cancellationToken)
    {
        await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            if (!_sessionStore.TryGet(receiptId, out var session) || session is null)
            {
                return;
            }

            await _sessionLifetimeService.DiscardSessionAsync(session, cancellationToken);
        }, cancellationToken);
    }

    public async Task<ReceiptSessionState> CreateCustomSessionAsync(
        string uploadedByUserId,
        string uploadedByDisplayName,
        string? paymentContact,
        SocketSlashCommand command,
        CancellationToken cancellationToken)
    {
        var receiptId = $"custom-{Guid.NewGuid():N}";
        ReceiptSessionState? createdSession = null;

        await _lockManager.ExecuteAsync(receiptId, async () =>
        {
            var session = ReceiptSessionStateService.CreateCustomSession(
                receiptId,
                uploadedByUserId,
                uploadedByDisplayName,
                paymentContact,
                _languagePreferenceStore.GetLanguage(uploadedByUserId));

            session.UserDisplayNames[uploadedByUserId] = uploadedByDisplayName;
            session.MainChannel ??= _mainMessageService.ResolveSlashCommandChannel(command);

            await _mainMessageService.SendToSlashCommandAsync(session, command);
            _sessionStore.AddOrUpdate(session);
            createdSession = session;

            _logger.LogInformation(
                "Custom receipt session created. ReceiptId={ReceiptId} UserId={UserId} ChannelId={ChannelId} MessageId={MessageId}",
                session.ReceiptId,
                uploadedByUserId,
                session.MainChannelId,
                session.MainMessageId);
        }, cancellationToken);

        return createdSession ?? throw new InvalidOperationException("Custom session creation failed.");
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

        var existingSession = FindExistingSession(payload, receiptId, out _, out _);
        var publicLanguage = _languagePreferenceStore.GetLanguage(uploadedByUserId);
        var lockKey = existingSession?.ReceiptId ?? receiptId;

        await _lockManager.ExecuteAsync(lockKey, async () =>
        {
            var session = FindExistingSession(payload, receiptId, out var previousReceiptId, out var previousBlobUrl);
            var displayName = await ResolveUploadedByDisplayNameAsync(uploadedByUserId, session);
            session ??= ReceiptSessionStateService.CreateSessionFromDraft(payload, displayName);
            var shouldReplacePendingMessage =
                !string.IsNullOrWhiteSpace(previousReceiptId) &&
                !string.Equals(previousReceiptId, receiptId, StringComparison.Ordinal) &&
                session.MainChannelId is not null &&
                session.MainMessageId is not null;
            var previousMainMessage = shouldReplacePendingMessage
                ? CreateMainMessageSnapshot(session)
                : null;

            ReceiptSessionStateService.ApplyDraftPayload(session, payload, displayName);
            session.UserDisplayNames[uploadedByUserId] = displayName;
            session.UploadedByDisplayName = displayName;
            session.PublicLanguage = publicLanguage;
            session.MainChannel ??= targetChannel;
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;

            if (shouldReplacePendingMessage)
            {
                var publishChannel = session.MainChannel ?? targetChannel
                    ?? throw new InvalidOperationException("A target channel is required to replace the pending message.");

                _debounceService.CancelRefresh(previousReceiptId!);
                await _mainMessageService.SendToChannelAsync(session, publishChannel, cancellationToken);

                if (previousMainMessage is not null)
                {
                    try
                    {
                        await _mainMessageService.DeleteAsync(previousMainMessage, cancellationToken);
                    }
                    catch
                    {
                        // Ignore cleanup failures for already removed or stale pending messages.
                    }
                }
            }
            else if (session.MainChannelId is not null && session.MainMessageId is not null)
            {
                _debounceService.CancelRefresh(session.ReceiptId);
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
                throw new InvalidOperationException("Cannot publish a draft message without an existing channel session.");
            }

            _sessionStore.AddOrUpdate(session, previousReceiptId, previousBlobUrl);

            if (!string.IsNullOrWhiteSpace(previousReceiptId) &&
                !string.Equals(previousReceiptId, session.ReceiptId, StringComparison.Ordinal))
            {
                _lockManager.Cleanup(previousReceiptId);
            }

            _logger.LogInformation(
                "Receipt session upserted from draft. ReceiptId={ReceiptId} UserId={UserId} ChannelId={ChannelId} MessageId={MessageId} ItemCount={ItemCount}",
                session.ReceiptId,
                uploadedByUserId,
                session.MainChannelId,
                session.MainMessageId,
                session.Items.Count);
        }, cancellationToken);
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

    private async Task<string> ResolveUploadedByDisplayNameAsync(
        string uploadedByUserId,
        ReceiptSessionState? existingSession)
    {
        if (existingSession is not null)
        {
            if (string.Equals(existingSession.UploadedByUserId, uploadedByUserId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(existingSession.UploadedByDisplayName))
            {
                return existingSession.UploadedByDisplayName;
            }

            if (existingSession.UserDisplayNames.TryGetValue(uploadedByUserId, out var cachedDisplayName) &&
                !string.IsNullOrWhiteSpace(cachedDisplayName))
            {
                return cachedDisplayName;
            }
        }

        return await ResolveUploadedByDisplayNameFromDiscordAsync(uploadedByUserId);
    }

    private async Task<string> ResolveUploadedByDisplayNameFromDiscordAsync(string uploadedByUserId)
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

    private static ReceiptSessionState CreateMainMessageSnapshot(ReceiptSessionState session)
    {
        return new ReceiptSessionState
        {
            ReceiptId = session.ReceiptId,
            BlobUrl = session.BlobUrl,
            MainChannel = session.MainChannel,
            MainMessage = session.MainMessage,
            MainMessageId = session.MainMessageId,
            MainChannelId = session.MainChannelId,
            MainGuildId = session.MainGuildId,
            CreatedAtUtc = session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc
        };
    }
}
