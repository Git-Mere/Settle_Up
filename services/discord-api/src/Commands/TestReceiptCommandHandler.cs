using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class TestReceiptCommandHandler
{
    public const string CommandName = "test";

    private readonly ReceiptDraftTestDataLoader _testDataLoader;
    private readonly ReceiptDraftSessionService _receiptDraftSessionService;
    private readonly ILogger<TestReceiptCommandHandler> _logger;

    public TestReceiptCommandHandler(
        ReceiptDraftTestDataLoader testDataLoader,
        ReceiptDraftSessionService receiptDraftSessionService,
        ILogger<TestReceiptCommandHandler> logger)
    {
        _testDataLoader = testDataLoader;
        _receiptDraftSessionService = receiptDraftSessionService;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription("테스트 영수증 UI를 생성합니다.")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("scenario")
                .WithDescription("Test scenario")
                .WithRequired(false)
                .WithType(ApplicationCommandOptionType.String)
                .AddChoice("general", ReceiptDraftTestDataLoader.DefaultScenario)
                .AddChoice("liquor", ReceiptDraftTestDataLoader.LiquorScenario)
                .AddChoice("tax-exempt", ReceiptDraftTestDataLoader.TaxExemptScenario))
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        try
        {
            var scenario = command.Data.Options
                .FirstOrDefault(option => string.Equals(option.Name, "scenario", StringComparison.Ordinal))
                ?.Value?.ToString();

            var normalizedScenario = ReceiptDraftTestDataLoader.NormalizeScenario(scenario);
            var payload = await _testDataLoader.LoadAsync(
                command.User.Id.ToString(),
                command.User.GlobalName ?? command.User.Username,
                normalizedScenario);
            await _receiptDraftSessionService.CreateOrUpdateSessionFromDraftAsync(
                payload,
                command,
                CancellationToken.None);

            _logger.LogInformation(
                "Test receipt session created. UserId={UserId} DraftId={DraftId} Scenario={Scenario}",
                command.User.Id,
                payload.ResolvedDraftId,
                normalizedScenario);
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
