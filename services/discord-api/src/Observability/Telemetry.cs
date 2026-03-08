using System.Diagnostics;
using System.Diagnostics.Metrics;

static class Telemetry
{
    public const string ActivitySourceName = "SettleUp.DiscordApi";
    public const string MeterName = "SettleUp.DiscordApi";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> CommandsRegisteredCounter =
        Meter.CreateCounter<long>("discord_commands_registered_total");

    public static readonly Counter<long> SlashCommandsCounter =
        Meter.CreateCounter<long>("discord_slash_commands_total");

    public static readonly Counter<long> ImageUploadTimeoutCounter =
        Meter.CreateCounter<long>("discord_image_upload_timeout_total");

    public static readonly Histogram<double> SlashCommandDurationMs =
        Meter.CreateHistogram<double>("discord_slash_command_duration_ms");

    public static readonly Histogram<double> ImageWaitDurationMs =
        Meter.CreateHistogram<double>("discord_image_wait_duration_ms");
}
