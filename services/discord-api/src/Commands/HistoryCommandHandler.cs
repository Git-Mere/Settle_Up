using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class HistoryCommandHandler
{
    public const string CommandName = "history";
    private const string ListSubcommandName = "list";
    private const string DetailSubcommandName = "detail";
    private const string IndexOptionName = "index";
    private const int MaxHistoryEntries = 10;

    private readonly SettlementHistoryRepositoryProvider _settlementHistoryRepositoryProvider;
    private readonly ILogger<HistoryCommandHandler> _logger;

    public HistoryCommandHandler(
        SettlementHistoryRepositoryProvider settlementHistoryRepositoryProvider,
        ILogger<HistoryCommandHandler> logger)
    {
        _settlementHistoryRepositoryProvider = settlementHistoryRepositoryProvider;
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription("최근 정산 기록을 조회합니다.")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(ListSubcommandName)
                .WithDescription("최근 정산 기록 목록을 조회합니다.")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName(DetailSubcommandName)
                .WithDescription("현재 시점 기준 최신순 n번째 기록을 상세 조회합니다.")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(IndexOptionName, ApplicationCommandOptionType.Integer, "현재 시점 기준 최신순 n번째 기록", isRequired: true))
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);

        var repository = _settlementHistoryRepositoryProvider.Repository;
        if (repository is null)
        {
            await command.FollowupAsync("history 저장소가 설정되지 않았습니다.", ephemeral: true);
            return "history_storage_not_configured";
        }

        var subcommand = command.Data.Options.FirstOrDefault();
        if (subcommand is null)
        {
            await command.FollowupAsync("사용 방법: `/history list` 또는 `/history detail index:<번호>`", ephemeral: true);
            return "history_missing_subcommand";
        }

        if (string.Equals(subcommand.Name, DetailSubcommandName, StringComparison.Ordinal))
        {
            var index = subcommand.Options
                .FirstOrDefault(option => string.Equals(option.Name, IndexOptionName, StringComparison.Ordinal))
                ?.Value as long?;

            if (index is not long rawIndex)
            {
                await command.FollowupAsync("index 값이 필요합니다.", ephemeral: true);
                return "missing_index";
            }

            if (rawIndex <= 0 || rawIndex > MaxHistoryEntries)
            {
                await command.FollowupAsync("index는 1부터 10 사이여야 합니다.", ephemeral: true);
                return "invalid_index";
            }

            var history = await repository.GetByRecencyIndexForUserAsync(command.User.Id.ToString(), (int)rawIndex);
            if (history is null)
            {
                await command.FollowupAsync($"현재 {rawIndex}번째 기록을 찾을 수 없습니다.", ephemeral: true);
                return "history_detail_not_found";
            }

            await command.FollowupAsync(
                embed: SettlementHistoryMessageRenderer.RenderDetail(history),
                ephemeral: true);

            _logger.LogInformation(
                "History detail returned. UserId={UserId} Index={Index} HistoryId={HistoryId}",
                command.User.Id,
                rawIndex,
                history.Id);

            return "history_detail_returned";
        }

        if (!string.Equals(subcommand.Name, ListSubcommandName, StringComparison.Ordinal))
        {
            await command.FollowupAsync("사용 방법: `/history list` 또는 `/history detail index:<번호>`", ephemeral: true);
            return "history_unknown_subcommand";
        }

        var histories = await repository.GetRecentForUserAsync(command.User.Id.ToString(), MaxHistoryEntries);
        if (histories.Count == 0)
        {
            await command.FollowupAsync("저장된 정산 기록이 없습니다.", ephemeral: true);
            return "history_empty";
        }

        await command.FollowupAsync(
            embed: SettlementHistoryMessageRenderer.RenderList(histories),
            ephemeral: true);

        _logger.LogInformation(
            "History list returned. UserId={UserId} Count={Count}",
            command.User.Id,
            histories.Count);

        return "history_list_returned";
    }
}
