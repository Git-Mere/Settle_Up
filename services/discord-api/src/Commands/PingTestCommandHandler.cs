using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

sealed class PingTestCommandHandler
{
    public const string CommandName = "pingtest";
    private readonly ILogger<PingTestCommandHandler> _logger;

    public PingTestCommandHandler(ILogger<PingTestCommandHandler> logger)
    {
        _logger = logger;
    }

    public static SlashCommandProperties BuildCommand()
    {
        return new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription("봇 응답을 테스트합니다.")
            .Build();
    }

    public async Task<string> HandleSlashCommandAsync(SocketSlashCommand command)
    {
        await command.RespondAsync("pong! slash command 정상 작동 중입니다.", ephemeral: true);
        _logger.LogInformation("Ping command completed. UserId={UserId} GuildId={GuildId}", command.User.Id, command.GuildId);
        return "success";
    }
}
