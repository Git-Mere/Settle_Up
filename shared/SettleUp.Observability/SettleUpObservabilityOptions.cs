namespace SettleUp.Observability;

public sealed class SettleUpObservabilityOptions
{
    public required string ServiceName { get; init; }
    public required string ServiceVersion { get; init; }
    public required string ActivitySourceName { get; init; }
    public bool IncludeAspNetCoreInstrumentation { get; init; }
}
