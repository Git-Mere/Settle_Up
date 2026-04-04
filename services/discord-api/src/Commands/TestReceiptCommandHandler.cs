using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class TestReceiptCommandHandler
{
    public const string CommandName = "test";
    private const string ScenarioOptionName = "scenario";

    private readonly ReceiptDraftTestDataLoader _testDataLoader;
    private readonly ReceiptDraftSessionService _receiptDraftSessionService;
    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<TestReceiptCommandHandler> _logger;

    public TestReceiptCommandHandler(
        ReceiptDraftTestDataLoader testDataLoader,
        ReceiptDraftSessionService receiptDraftSessionService,
        UserLanguagePreferenceStore languagePreferenceStore,
        ILogger<TestReceiptCommandHandler> logger)
    {
        _testDataLoader = testDataLoader;
        _receiptDraftSessionService = receiptDraftSessionService;
        _languagePreferenceStore = languagePreferenceStore;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription(DiscordUiText.TestCommandDescription(AppLanguage.English))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(ScenarioOptionName)
                .WithDescription(DiscordUiText.TestScenarioDescription(AppLanguage.English))
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.String)
                .AddChoice("General Market", ReceiptDraftTestScenario.GeneralMarket)
                .AddChoice("Discount Market", ReceiptDraftTestScenario.DiscountMarket)
                .AddChoice("Stacked Discount Market", ReceiptDraftTestScenario.StackedDiscountMarket)
                .AddChoice("Liquor Tax Market", ReceiptDraftTestScenario.LiquorTaxMarket)
                .AddChoice("Restaurant Tip", ReceiptDraftTestScenario.RestaurantTip))
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();
        var language = _languagePreferenceStore.GetLanguage(command.User.Id.ToString());

        try
        {
            var scenario = command.Data.Options
                .FirstOrDefault(option => string.Equals(option.Name, ScenarioOptionName, StringComparison.Ordinal))
                ?.Value?.ToString();

            var payload = await _testDataLoader.LoadAsync(
                command.User.Id.ToString(),
                command.User.GlobalName ?? command.User.Username,
                scenario);
            await _receiptDraftSessionService.CreateOrUpdateSessionFromDraftAsync(
                payload,
                command,
                CancellationToken.None);

            _logger.LogInformation(
                "Test receipt session created. UserId={UserId} DraftId={DraftId} Scenario={Scenario}",
                command.User.Id,
                payload.ResolvedDraftId,
                scenario);
            return "success";
        }
        catch (Exception ex)
        {
            await command.FollowupAsync(DiscordUiText.TestSessionError(language));

            _logger.LogError(ex, "Test receipt session creation failed. UserId={UserId}", command.User.Id);
            return "error";
        }
    }
}
