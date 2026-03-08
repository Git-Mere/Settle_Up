using Discord;
using Discord.WebSocket;

sealed class PingTestCommandHandler
{
    public const string CommandName = "pingtest";

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
        return "success";
    }
}
