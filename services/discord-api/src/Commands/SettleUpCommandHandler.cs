using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class SettleUpCommandHandler
{
    public const string CommandName = "settle-up";
    private const string UploadButtonPrefix = "settleup-upload";
    private const string UploadModalPrefix = "settleup-upload-modal";
    private const string UploadFileCustomId = "receipt_image";

    private readonly BlobUploaderProvider _blobUploaderProvider;
    private readonly ILogger<SettleUpCommandHandler> _logger;

    public SettleUpCommandHandler(
        BlobUploaderProvider blobUploaderProvider,
        ILogger<SettleUpCommandHandler> logger)
    {
        _blobUploaderProvider = blobUploaderProvider;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription("정산 이미지 업로드를 시작합니다.")
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        _logger.LogInformation("Settle-up command accepted. UserId={UserId} GuildId={GuildId}", command.User.Id, command.GuildId);

        var buttonCustomId = $"{UploadButtonPrefix}:{command.User.Id}";
        var component = new ComponentBuilder()
            .WithButton(
                label: "영수증 업로드",
                customId: buttonCustomId,
                style: ButtonStyle.Primary)
            .Build();

        await command.RespondAsync(
            "아래 버튼을 누르면 이미지 업로드를 시작합니다.",
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
            await component.RespondAsync("버튼 정보가 올바르지 않습니다. `/settle-up`을 다시 실행해 주세요.", ephemeral: true);
            return "invalid_custom_id";
        }

        if (component.User.Id != ownerId)
        {
            await component.RespondAsync("이 버튼은 명령어를 실행한 사용자만 사용할 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        if (_blobUploaderProvider.Uploader is null)
        {
            await component.RespondAsync("Blob 저장소 설정이 비어 있어 업로드할 수 없습니다. 환경변수 설정을 확인해 주세요.", ephemeral: true);
            _logger.LogWarning("Settle-up upload blocked because blob uploader is not configured. UserId={UserId} Reason={Reason}", component.User.Id, _blobUploaderProvider.InitializationError);
            return "blob_not_configured";
        }

        var modalCustomId = $"{UploadModalPrefix}:{component.User.Id}";
        var modal = new ModalBuilder()
            .WithTitle("영수증 업로드")
            .WithCustomId(modalCustomId)
            .AddFileUpload(
                label: "이미지 파일",
                customId: UploadFileCustomId,
                minValues: 1,
                maxValues: 1,
                isRequired: true,
                description: "jpg 또는 png 파일을 업로드해 주세요.")
            .Build();

        await component.RespondWithModalAsync(modal);
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
            await modal.RespondAsync("모달 정보가 올바르지 않습니다. `/settle-up`을 다시 실행해 주세요.", ephemeral: true);
            return "invalid_modal_id";
        }

        if (modal.User.Id != ownerId)
        {
            await modal.RespondAsync("이 모달은 명령어를 실행한 사용자만 제출할 수 있습니다.", ephemeral: true);
            return "forbidden_user";
        }

        if (_blobUploaderProvider.Uploader is null)
        {
            await modal.RespondAsync("Blob 저장소 설정이 비어 있어 업로드할 수 없습니다. 환경변수 설정을 확인해 주세요.", ephemeral: true);
            _logger.LogWarning("Modal upload blocked because blob uploader is not configured. UserId={UserId} Reason={Reason}", modal.User.Id, _blobUploaderProvider.InitializationError);
            return "blob_not_configured";
        }

        var attachment = modal.Data.Attachments.FirstOrDefault();
        if (attachment is null)
        {
            await modal.RespondAsync("업로드된 파일을 찾을 수 없습니다. 다시 시도해 주세요.", ephemeral: true);
            return "missing_attachment";
        }

        BlobUploadResult uploadResult;
        try
        {
            _logger.LogInformation("Blob upload started. UserId={UserId} FileName={FileName}", modal.User.Id, attachment.Filename);
            uploadResult = await _blobUploaderProvider.Uploader.UploadReceiptImageAsync(attachment, modal.User.Id);
        }
        catch (InvalidOperationException invalidEx)
        {
            await modal.RespondAsync(invalidEx.Message, ephemeral: true);
            _logger.LogWarning("Blob upload rejected. UserId={UserId} FileName={FileName} Reason={Reason}", modal.User.Id, attachment.Filename, invalidEx.Message);
            return "invalid_image";
        }
        catch (Exception ex)
        {
            await modal.RespondAsync("Blob 업로드 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.", ephemeral: true);
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

        await modal.RespondAsync(
            $"Blob 업로드 완료\nContainer: `{uploadResult.ContainerName}`\nBlob: `{uploadResult.BlobName}`\nURL: {uploadResult.BlobUri}",
            ephemeral: true);

        return "success";
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
}
