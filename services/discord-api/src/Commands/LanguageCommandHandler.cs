using Discord;
using Discord.WebSocket;

sealed class LanguageCommandHandler
{
    public const string CommandName = "language";
    private const string LanguageOptionName = "language";

    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<LanguageCommandHandler> _logger;

    public LanguageCommandHandler(
        UserLanguagePreferenceStore languagePreferenceStore,
        ILogger<LanguageCommandHandler> logger)
    {
        _languagePreferenceStore = languagePreferenceStore;
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

        await command.RespondAsync(
            DiscordUiText.LanguageUpdated(selectedLanguage, selectedLanguage),
            ephemeral: true);

        _logger.LogInformation("Language updated. UserId={UserId} Language={Language}", command.User.Id, selectedLanguage);
        return "language_updated";
    }
}
