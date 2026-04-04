using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class PingTestCommandHandler
{
    public const string CommandName = "pingtest";
    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<PingTestCommandHandler> _logger;

    public PingTestCommandHandler(UserLanguagePreferenceStore languagePreferenceStore, ILogger<PingTestCommandHandler> logger)
    {
        _languagePreferenceStore = languagePreferenceStore;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription(DiscordUiText.PingCommandDescription(AppLanguage.English))
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var language = _languagePreferenceStore.GetLanguage(command.User.Id.ToString());
        await command.RespondAsync(DiscordUiText.PingResponse(language), ephemeral: true);
        _logger.LogInformation("Ping command completed. UserId={UserId} GuildId={GuildId}", command.User.Id, command.GuildId);
        return "success";
    }
}
