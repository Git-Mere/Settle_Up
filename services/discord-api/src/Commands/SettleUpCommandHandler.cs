using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

sealed class SettleUpCommandHandler
{
    public const string CommandName = "settle-up";
    private const string UploadButtonPrefix = "settleup-upload";
    private const string UploadModalPrefix = "settleup-upload-modal";
    private const string UploadFileCustomId = "receipt_image";
    private const string PaymentContactCustomId = "payment_contact";

    private readonly BlobUploaderProvider _blobUploaderProvider;
    private readonly ReceiptDraftSessionService _receiptDraftSessionService;
    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<SettleUpCommandHandler> _logger;
    private readonly ConcurrentDictionary<ulong, UploadPromptInteractionEntry> _uploadPromptInteractions = new();

    public SettleUpCommandHandler(
        BlobUploaderProvider blobUploaderProvider,
        ReceiptDraftSessionService receiptDraftSessionService,
        UserLanguagePreferenceStore languagePreferenceStore,
        ILogger<SettleUpCommandHandler> logger)
    {
        _blobUploaderProvider = blobUploaderProvider;
        _receiptDraftSessionService = receiptDraftSessionService;
        _languagePreferenceStore = languagePreferenceStore;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription(DiscordUiText.SettleUpCommandDescription(AppLanguage.English))
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        _logger.LogInformation("Settle-up command accepted. UserId={UserId} GuildId={GuildId}", command.User.Id, command.GuildId);

        var language = _languagePreferenceStore.GetLanguage(command.User.Id.ToString());
        var buttonCustomId = $"{UploadButtonPrefix}:{command.User.Id}";
        var component = new ComponentBuilder()
            .WithButton(
                label: DiscordUiText.UploadReceiptButton(language),
                customId: buttonCustomId,
                style: ButtonStyle.Primary)
            .Build();

        await command.RespondAsync(
            DiscordUiText.UploadPromptText(language),
            components: component,
            ephemeral: true);

        return "awaiting_button_click";
    }

    public async Task<string?> HandleButtonAsync(SocketMessageComponent component)
    {
        if (!component.Data.CustomId.StartsWith($"{UploadButtonPrefix}:", StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryGetCommandOwnerId(component.Data.CustomId, out var ownerId))
        {
            await component.RespondAsync(DiscordUiText.InvalidButtonInfo(_languagePreferenceStore.GetLanguage(component.User.Id.ToString())), ephemeral: true);
            return "invalid_custom_id";
        }

        if (component.User.Id != ownerId)
        {
            await component.RespondAsync(DiscordUiText.ButtonOwnerOnly(_languagePreferenceStore.GetLanguage(component.User.Id.ToString())), ephemeral: true);
            return "forbidden_user";
        }

        if (_blobUploaderProvider.Uploader is null)
        {
            await component.RespondAsync(DiscordUiText.BlobNotConfigured(_languagePreferenceStore.GetLanguage(component.User.Id.ToString())), ephemeral: true);
            _logger.LogWarning("Settle-up upload blocked because blob uploader is not configured. UserId={UserId} Reason={Reason}", component.User.Id, _blobUploaderProvider.InitializationError);
            return "blob_not_configured";
        }

        var language = _languagePreferenceStore.GetLanguage(component.User.Id.ToString());
        var modalCustomId = $"{UploadModalPrefix}:{component.User.Id}";
        var modal = new ModalBuilder()
            .WithTitle(DiscordUiText.UploadModalTitle(language))
            .WithCustomId(modalCustomId)
            .AddFileUpload(
                label: DiscordUiText.UploadImageLabel(language),
                customId: UploadFileCustomId,
                minValues: 1,
                maxValues: 1,
                isRequired: true,
                description: DiscordUiText.UploadImageDescription(language))
            .AddTextInput(
                label: DiscordUiText.PaymentContactLabel(language),
                customId: PaymentContactCustomId,
                style: TextInputStyle.Paragraph,
                required: false,
                placeholder: DiscordUiText.PaymentContactPlaceholder(language),
                maxLength: 200)
            .Build();

        await component.RespondWithModalAsync(modal);
        _uploadPromptInteractions[component.User.Id] = new UploadPromptInteractionEntry(component, DateTimeOffset.UtcNow);
        return "modal_opened";
    }

    public async Task<string?> HandleModalAsync(SocketModal modal)
    {
        if (!modal.Data.CustomId.StartsWith($"{UploadModalPrefix}:", StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryGetCommandOwnerId(modal.Data.CustomId, out var ownerId))
        {
            await modal.RespondAsync(DiscordUiText.InvalidModalInfo(_languagePreferenceStore.GetLanguage(modal.User.Id.ToString())), ephemeral: true);
            return "invalid_modal_id";
        }

        if (modal.User.Id != ownerId)
        {
            await modal.RespondAsync(DiscordUiText.ModalOwnerOnly(_languagePreferenceStore.GetLanguage(modal.User.Id.ToString())), ephemeral: true);
            return "forbidden_user";
        }

        if (_blobUploaderProvider.Uploader is null)
        {
            await modal.RespondAsync(DiscordUiText.BlobNotConfigured(_languagePreferenceStore.GetLanguage(modal.User.Id.ToString())), ephemeral: true);
            _logger.LogWarning("Modal upload blocked because blob uploader is not configured. UserId={UserId} Reason={Reason}", modal.User.Id, _blobUploaderProvider.InitializationError);
            return "blob_not_configured";
        }

        var attachment = modal.Data.Attachments.FirstOrDefault();
        if (attachment is null)
        {
            await modal.RespondAsync(DiscordUiText.MissingAttachment(_languagePreferenceStore.GetLanguage(modal.User.Id.ToString())), ephemeral: true);
            return "missing_attachment";
        }

        var paymentContact = modal.Data.Components
            .FirstOrDefault(component => string.Equals(component.CustomId, PaymentContactCustomId, StringComparison.Ordinal))
            ?.Value;

        await modal.DeferAsync(ephemeral: true);

        ReceiptSessionState? pendingSession = null;
        if (modal.Channel is IMessageChannel pendingTargetChannel)
        {
            pendingSession = await _receiptDraftSessionService.CreatePendingUploadSessionAndReturnAsync(
                modal.User.Id.ToString(),
                modal.User.GlobalName ?? modal.User.Username,
                paymentContact,
                pendingTargetChannel,
                CancellationToken.None);
        }

        BlobUploadResult uploadResult;
        try
        {
            _logger.LogInformation("Blob upload started. UserId={UserId} FileName={FileName}", modal.User.Id, attachment.Filename);
            uploadResult = await _blobUploaderProvider.Uploader.UploadReceiptImageAsync(attachment, modal.User.Id);
        }
        catch (InvalidOperationException invalidEx)
        {
            if (pendingSession is not null)
            {
                await _receiptDraftSessionService.DeletePendingUploadSessionAsync(pendingSession.ReceiptId, CancellationToken.None);
            }

            await modal.FollowupAsync(DiscordUiText.InvalidImageFile(_languagePreferenceStore.GetLanguage(modal.User.Id.ToString())), ephemeral: true);
            _logger.LogWarning("Blob upload rejected. UserId={UserId} FileName={FileName} Reason={Reason}", modal.User.Id, attachment.Filename, invalidEx.Message);
            return "invalid_image";
        }
        catch (Exception ex)
        {
            if (pendingSession is not null)
            {
                await _receiptDraftSessionService.DeletePendingUploadSessionAsync(pendingSession.ReceiptId, CancellationToken.None);
            }

            await modal.FollowupAsync(DiscordUiText.UploadFailed(_languagePreferenceStore.GetLanguage(modal.User.Id.ToString())), ephemeral: true);
            _logger.LogError(ex, "Blob upload failed. UserId={UserId} FileName={FileName}", modal.User.Id, attachment.Filename);
            return "upload_error";
        }

        using var activity = Telemetry.ActivitySource.StartActivity("discord.blob.upload");
        activity?.SetTag("blob.container", uploadResult.ContainerName);
        activity?.SetTag("blob.name", uploadResult.BlobName);

        _logger.LogInformation(
            "Blob upload completed. UserId={UserId} ContainerName={ContainerName} BlobName={BlobName}",
            modal.User.Id,
            uploadResult.ContainerName,
            uploadResult.BlobName);

        if (pendingSession is not null)
        {
            await _receiptDraftSessionService.AttachBlobUrlToPendingSessionAsync(
                pendingSession.ReceiptId,
                uploadResult.BlobUri,
                CancellationToken.None);
        }

        await TryDeleteUploadPromptAsync(modal.User.Id);
        return "success";
    }

    private async Task TryDeleteUploadPromptAsync(ulong userId)
    {
        if (!_uploadPromptInteractions.TryRemove(userId, out var entry))
        {
            return;
        }

        try
        {
            await entry.Interaction.DeleteOriginalResponseAsync();
        }
        catch
        {
            // Ignore cleanup failures for expired interaction tokens.
        }
    }

    private static bool TryGetCommandOwnerId(string customId, out ulong ownerId)
    {
        ownerId = default;
        var tokens = customId.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 2)
        {
            return false;
        }

        return ulong.TryParse(tokens[1], out ownerId);
    }

    public async Task CleanupExpiredUploadPromptsAsync(DateTimeOffset staleBeforeUtc)
    {
        foreach (var pair in _uploadPromptInteractions)
        {
            if (pair.Value.CreatedAtUtc > staleBeforeUtc)
            {
                continue;
            }

            await TryDeleteUploadPromptAsync(pair.Key);
        }
    }

    private sealed record UploadPromptInteractionEntry(
        SocketMessageComponent Interaction,
        DateTimeOffset CreatedAtUtc);
}
