using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class TestReceiptCommandHandler
{
    public const string CommandName = "test";

    private readonly ReceiptDraftTestDataLoader _testDataLoader;
    private readonly ReceiptInteractionService _receiptInteractionService;
    private readonly ILogger<TestReceiptCommandHandler> _logger;

    public TestReceiptCommandHandler(
        ReceiptDraftTestDataLoader testDataLoader,
        ReceiptInteractionService receiptInteractionService,
        ILogger<TestReceiptCommandHandler> logger)
    {
        _testDataLoader = testDataLoader;
        _receiptInteractionService = receiptInteractionService;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription("테스트 영수증 UI를 생성합니다.")
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        try
        {
            var payload = await _testDataLoader.LoadAsync(command.User.Id.ToString(), command.User.GlobalName ?? command.User.Username);
            await _receiptInteractionService.CreateOrUpdateSessionFromDraftAsync(
                payload,
                command,
                targetChannel: null,
                cancellationToken: CancellationToken.None);

            _logger.LogInformation("Test receipt session created. UserId={UserId} DraftId={DraftId}", command.User.Id, payload.ResolvedDraftId);
            return "success";
        }
        catch (Exception ex)
        {
            await command.FollowupAsync("테스트 영수증 세션 생성 중 오류가 발생했습니다. 로그를 확인해 주세요.");

            _logger.LogError(ex, "Test receipt session creation failed. UserId={UserId}", command.User.Id);
            return "error";
        }
    }
}
