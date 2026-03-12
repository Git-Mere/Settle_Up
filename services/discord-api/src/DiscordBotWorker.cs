using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

sealed class DiscordBotWorker : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordBotWorker> _logger;
    private readonly PingTestCommandHandler _pingTestHandler;
    private readonly TestReceiptCommandHandler _testReceiptHandler;
    private readonly SettleUpCommandHandler _settleUpHandler;
    private readonly string? _token;
    private bool _commandRegistered;

    public DiscordBotWorker(
        DiscordSocketClient client,
        ILogger<DiscordBotWorker> logger,
        PingTestCommandHandler pingTestHandler,
        TestReceiptCommandHandler testReceiptHandler,
        SettleUpCommandHandler settleUpHandler,
        ReceiptInteractionService receiptInteractionService)
    {
        _client = client;
        _logger = logger;
        _pingTestHandler = pingTestHandler;
        _testReceiptHandler = testReceiptHandler;
        _settleUpHandler = settleUpHandler;
        _receiptInteractionService = receiptInteractionService;
        _token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");

        _client.Log += HandleDiscordLogAsync;
        _client.Ready += HandleReadyAsync;
        _client.SlashCommandExecuted += HandleSlashCommandExecutedAsync;
        _client.ButtonExecuted += HandleButtonExecutedAsync;
        _client.SelectMenuExecuted += HandleSelectMenuExecutedAsync;
        _client.ModalSubmitted += HandleModalSubmittedAsync;
        _client.MessageReceived += HandleMessageReceivedAsync;
    }

    private readonly ReceiptInteractionService _receiptInteractionService;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException("환경 변수 DISCORD_BOT_TOKEN 이 필요합니다.");
        }

        _logger.LogInformation("Bot starting.");
        _logger.LogInformation("Connecting to Discord gateway.");

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bot stopping.");

        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private Task HandleDiscordLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information
        };

        _logger.Log(level, message.Exception, "Discord client log. Source={Source} Message={Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    private async Task HandleReadyAsync()
    {
        using var activity = Telemetry.ActivitySource.StartActivity("discord.ready");

        if (_commandRegistered)
        {
            _logger.LogInformation("Discord ready; command registration skipped because commands are already registered.");
            activity?.SetTag("discord.commands.registration_skipped", true);
            return;
        }

        await _client.Rest.CreateGlobalCommand(SettleUpCommandHandler.BuildCommand());
        await _client.Rest.CreateGlobalCommand(PingTestCommandHandler.BuildCommand());
        await _client.Rest.CreateGlobalCommand(TestReceiptCommandHandler.BuildCommand());

        _commandRegistered = true;
        activity?.SetTag("discord.commands.registered", 3);
        _logger.LogInformation("Discord ready; registered {CommandCount} commands.", 3);
    }

    private async Task HandleSlashCommandExecutedAsync(SocketSlashCommand command)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("discord.slash_command.execute");
        activity?.SetTag("discord.command.name", command.Data.Name);
        activity?.SetTag("discord.user.id", command.User.Id.ToString());
        activity?.SetTag("discord.guild.id", command.GuildId?.ToString());

        var startedAt = Stopwatch.GetTimestamp();
        _logger.LogInformation(
            "Slash command started. CommandName={CommandName} UserId={UserId} GuildId={GuildId}",
            command.Data.Name,
            command.User.Id,
            command.GuildId);

        try
        {
            string status;
            if (string.Equals(command.Data.Name, SettleUpCommandHandler.CommandName, StringComparison.OrdinalIgnoreCase))
            {
                status = await _settleUpHandler.HandleSlashCommandAsync(command);
            }
            else if (string.Equals(command.Data.Name, PingTestCommandHandler.CommandName, StringComparison.OrdinalIgnoreCase))
            {
                status = await _pingTestHandler.HandleSlashCommandAsync(command);
            }
            else if (string.Equals(command.Data.Name, TestReceiptCommandHandler.CommandName, StringComparison.OrdinalIgnoreCase))
            {
                status = await _testReceiptHandler.HandleSlashCommandAsync(command);
            }
            else
            {
                status = "unknown";
            }

            var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            activity?.SetTag("discord.command.status", status);
            activity?.SetTag("discord.command.duration_ms", durationMs);
            _logger.LogInformation(
                "Slash command completed. CommandName={CommandName} UserId={UserId} Status={Status} DurationMs={DurationMs}",
                command.Data.Name,
                command.User.Id,
                status,
                durationMs);
        }
        catch (Exception ex)
        {
            var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            activity?.SetTag("discord.command.status", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(
                ex,
                "Slash command failed. CommandName={CommandName} UserId={UserId} DurationMs={DurationMs}",
                command.Data.Name,
                command.User.Id,
                durationMs);
        }
    }

    private async Task HandleButtonExecutedAsync(SocketMessageComponent component)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("discord.button.execute");
        activity?.SetTag("discord.component.custom_id", component.Data.CustomId);
        activity?.SetTag("discord.user.id", component.User.Id.ToString());
        activity?.SetTag("discord.guild.id", component.GuildId?.ToString());

        try
        {
            var status = await _settleUpHandler.HandleButtonAsync(component)
                ?? await _receiptInteractionService.HandleButtonAsync(component);
            if (!string.IsNullOrWhiteSpace(status))
            {
                activity?.SetTag("discord.button.status", status);
                _logger.LogInformation(
                    "Button interaction completed. CustomId={CustomId} UserId={UserId} Status={Status}",
                    component.Data.CustomId,
                    component.User.Id,
                    status);
            }
        }
        catch (Exception ex)
        {
            activity?.SetTag("discord.button.status", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Button interaction failed. CustomId={CustomId} UserId={UserId}", component.Data.CustomId, component.User.Id);
        }
    }

    private async Task HandleSelectMenuExecutedAsync(SocketMessageComponent component)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("discord.select_menu.execute");
        activity?.SetTag("discord.component.custom_id", component.Data.CustomId);
        activity?.SetTag("discord.user.id", component.User.Id.ToString());
        activity?.SetTag("discord.guild.id", component.GuildId?.ToString());

        try
        {
            var status = await _receiptInteractionService.HandleSelectMenuAsync(component);
            if (!string.IsNullOrWhiteSpace(status))
            {
                activity?.SetTag("discord.select_menu.status", status);
                _logger.LogInformation(
                    "Select menu interaction completed. CustomId={CustomId} UserId={UserId} Status={Status}",
                    component.Data.CustomId,
                    component.User.Id,
                    status);
            }
        }
        catch (Exception ex)
        {
            activity?.SetTag("discord.select_menu.status", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Select menu interaction failed. CustomId={CustomId} UserId={UserId}", component.Data.CustomId, component.User.Id);
        }
    }

    private async Task HandleModalSubmittedAsync(SocketModal modal)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("discord.modal.submit");
        activity?.SetTag("discord.modal.custom_id", modal.Data.CustomId);
        activity?.SetTag("discord.user.id", modal.User.Id.ToString());
        activity?.SetTag("discord.guild.id", modal.GuildId?.ToString());

        try
        {
            var status = await _settleUpHandler.HandleModalAsync(modal);
            if (!string.IsNullOrWhiteSpace(status))
            {
                activity?.SetTag("discord.modal.status", status);
                _logger.LogInformation(
                    "Modal submission completed. CustomId={CustomId} UserId={UserId} Status={Status}",
                    modal.Data.CustomId,
                    modal.User.Id,
                    status);
            }
        }
        catch (Exception ex)
        {
            activity?.SetTag("discord.modal.status", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            _logger.LogError(ex, "Modal submission failed. CustomId={CustomId} UserId={UserId}", modal.Data.CustomId, modal.User.Id);
        }
    }

    private async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot)
        {
            return;
        }

        if (!string.Equals(message.Content.Trim(), "ping", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var activity = Telemetry.ActivitySource.StartActivity("discord.message.ping");
        activity?.SetTag("discord.channel.id", message.Channel.Id.ToString());
        await message.Channel.SendMessageAsync("pong");
        _logger.LogInformation("Ping message handled. ChannelId={ChannelId} UserId={UserId}", message.Channel.Id, message.Author.Id);
    }
}
