using Discord;
using Discord.WebSocket;

sealed class LanguageCommandHandler
{
    public const string CommandName = "language";
    private const string LanguageOptionName = "language";

    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ReceiptSessionStore _sessionStore;
    private readonly ReceiptMainMessageService _mainMessageService;
    private readonly ILogger<LanguageCommandHandler> _logger;

    public LanguageCommandHandler(
        UserLanguagePreferenceStore languagePreferenceStore,
        ReceiptSessionStore sessionStore,
        ReceiptMainMessageService mainMessageService,
        ILogger<LanguageCommandHandler> logger)
    {
        _languagePreferenceStore = languagePreferenceStore;
        _sessionStore = sessionStore;
        _mainMessageService = mainMessageService;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription(DiscordUiText.LanguageCommandDescription(AppLanguage.English))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(LanguageOptionName)
                .WithDescription(DiscordUiText.LanguageOptionDescription(AppLanguage.English))
                .WithRequired(true)
                .WithType(ApplicationCommandOptionType.String)
                .AddChoice("English", "en")
                .AddChoice("Korean", "ko"))
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var optionValue = command.Data.Options
            .FirstOrDefault(option => string.Equals(option.Name, LanguageOptionName, StringComparison.Ordinal))
            ?.Value?.ToString();

        if (!AppLanguageParser.TryParse(optionValue, out var selectedLanguage))
        {
            selectedLanguage = AppLanguage.English;
        }

        _languagePreferenceStore.SetLanguage(command.User.Id.ToString(), selectedLanguage);

        foreach (var session in _sessionStore.GetAll()
                     .Where(session => string.Equals(session.UploadedByUserId, command.User.Id.ToString(), StringComparison.Ordinal)))
        {
            session.PublicLanguage = selectedLanguage;
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;
            _sessionStore.AddOrUpdate(session);

            if (session.MainMessageId is not null && !session.IsConfirmed)
            {
                try
                {
                    await _mainMessageService.RefreshAsync(session);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to refresh session after language update. ReceiptId={ReceiptId}", session.ReceiptId);
                }
            }
        }

        await command.RespondAsync(
            DiscordUiText.LanguageUpdated(selectedLanguage, selectedLanguage),
            ephemeral: true);

        _logger.LogInformation("Language updated. UserId={UserId} Language={Language}", command.User.Id, selectedLanguage);
        return "language_updated";
    }
}
