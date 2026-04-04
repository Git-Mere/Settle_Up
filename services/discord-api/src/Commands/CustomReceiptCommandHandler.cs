using Discord;
using Discord.WebSocket;

sealed class CustomReceiptCommandHandler
{
    public const string CommandName = "custom";
    private const string PaymentContactOptionName = "payment_contact";

    private readonly ReceiptDraftSessionService _receiptDraftSessionService;
    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<CustomReceiptCommandHandler> _logger;

    public CustomReceiptCommandHandler(
        ReceiptDraftSessionService receiptDraftSessionService,
        UserLanguagePreferenceStore languagePreferenceStore,
        ILogger<CustomReceiptCommandHandler> logger)
    {
        _receiptDraftSessionService = receiptDraftSessionService;
        _languagePreferenceStore = languagePreferenceStore;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription(DiscordUiText.CustomCommandDescription(AppLanguage.English))
            .AddOption(PaymentContactOptionName, ApplicationCommandOptionType.String, DiscordUiText.CustomPaymentContactDescription(AppLanguage.English), isRequired: false)
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var language = _languagePreferenceStore.GetLanguage(command.User.Id.ToString());
        var paymentContact = command.Data.Options
            .FirstOrDefault(option => string.Equals(option.Name, PaymentContactOptionName, StringComparison.Ordinal))
            ?.Value?.ToString();

        try
        {
            await command.DeferAsync();

            await _receiptDraftSessionService.CreateCustomSessionAsync(
                command.User.Id.ToString(),
                command.User.GlobalName ?? command.User.Username,
                paymentContact,
                command,
                CancellationToken.None);

            _logger.LogInformation("Custom receipt session created. UserId={UserId}", command.User.Id);
            return "custom_session_created";
        }
        catch (Exception ex)
        {
            await command.FollowupAsync(DiscordUiText.CustomCommandError(language));
            _logger.LogError(ex, "Custom receipt session creation failed. UserId={UserId}", command.User.Id);
            return "error";
        }
    }
}
