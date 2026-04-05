using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class HistoryCommandHandler
{
    public const string CommandName = "history";
    private const string IndexOptionName = "index";
    private const int MaxHistoryEntries = 10;

    private readonly SettlementHistoryRepositoryProvider _settlementHistoryRepositoryProvider;
    private readonly UserLanguagePreferenceStore _languagePreferenceStore;
    private readonly ILogger<HistoryCommandHandler> _logger;

    public HistoryCommandHandler(
        SettlementHistoryRepositoryProvider settlementHistoryRepositoryProvider,
        UserLanguagePreferenceStore languagePreferenceStore,
        ILogger<HistoryCommandHandler> logger)
    {
        _settlementHistoryRepositoryProvider = settlementHistoryRepositoryProvider;
        _languagePreferenceStore = languagePreferenceStore;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription(DiscordUiText.HistoryCommandDescription(AppLanguage.English))
            .AddOption(IndexOptionName, ApplicationCommandOptionType.Integer, DiscordUiText.HistoryIndexDescription(AppLanguage.English), isRequired: false)
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        var language = _languagePreferenceStore.GetLanguage(command.User.Id.ToString());

        var repository = _settlementHistoryRepositoryProvider.Repository;
        if (repository is null)
        {
            await command.FollowupAsync(DiscordUiText.HistoryStorageNotConfigured(language), ephemeral: true);
            return "history_storage_not_configured";
        }

        var index = command.Data.Options
            .FirstOrDefault(option => string.Equals(option.Name, IndexOptionName, StringComparison.Ordinal))
            ?.Value as long?;

        if (index is long rawIndex)
        {
            if (rawIndex <= 0 || rawIndex > MaxHistoryEntries)
            {
                await command.FollowupAsync(DiscordUiText.HistoryIndexRange(language), ephemeral: true);
                return "invalid_index";
            }

            var history = await repository.GetByRecencyIndexForUserAsync(command.User.Id.ToString(), (int)rawIndex);
            if (history is null)
            {
                await command.FollowupAsync(DiscordUiText.HistoryNotFound(language, rawIndex), ephemeral: true);
                return "history_detail_not_found";
            }

            await command.FollowupAsync(
                embed: SettlementHistoryMessageRenderer.RenderDetail(history, language),
                ephemeral: true);

            _logger.LogInformation(
                "History detail returned. UserId={UserId} Index={Index} HistoryId={HistoryId}",
                command.User.Id,
                rawIndex,
                history.Id);

            return "history_detail_returned";
        }

        var histories = await repository.GetRecentForUserAsync(command.User.Id.ToString(), MaxHistoryEntries);
        if (histories.Count == 0)
        {
            await command.FollowupAsync(DiscordUiText.HistoryEmpty(language), ephemeral: true);
            return "history_empty";
        }

        await command.FollowupAsync(
            embed: SettlementHistoryMessageRenderer.RenderList(histories, language),
            ephemeral: true);

        _logger.LogInformation(
            "History list returned. UserId={UserId} Count={Count}",
            command.User.Id,
            histories.Count);

        return "history_list_returned";
    }
}
