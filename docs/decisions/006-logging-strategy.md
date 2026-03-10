# 006. Observability and Logging Strategy

- **Status**: Accepted
- **Date**: 2026-03-09

## Context

The Settle Up project currently has multiple services, including:

- `discord-api`
- `receipt-parser`

As observability was added, the console output became too noisy. In particular:

- Discord.Net internal logs
- OpenTelemetry `HttpClient` instrumentation output
- custom application `Activity` traces
- general application logs

were all appearing together in the console.

This made local debugging harder, because low-level HTTP tracing and raw activity dumps were mixed with high-value application events such as:

- service startup
- Discord ready
- slash command execution
- blob event processing
- Cosmos DB writes
- failures and warnings

We also want a structure that scales cleanly as new services are added later.

Additionally, we want to use Azure Monitor / Application Insights for tracing, and we already have an environment variable available for that integration:

- `APPLICATIONINSIGHTS_CONNECTION_STRING`

## Decision

We will separate **application logging** from **observability tracing**.

### 1. `ILogger` is the standard for human-readable application logs

`ILogger` will be used for logs intended for developers/operators to read directly.

These logs should include meaningful application events such as:

- service starting
- service ready
- slash command started/completed/failed
- blob event received
- receipt parsing started/completed/failed
- Cosmos DB writes started/completed/failed
- warnings and errors

These logs should be:

- concise
- structured
- readable in console output
- consistent across services

`Console.WriteLine` should be avoided in favor of `ILogger` unless there is a very specific reason.

### 2. OpenTelemetry is the standard for tracing and dependency observability

OpenTelemetry will be used for:

- custom `Activity` traces
- dependency tracing
- `HttpClient` instrumentation
- future cross-service trace correlation

This includes service-level custom activity names such as:

- `discord.ready`
- `discord.slash_command.execute`
- `receipt_parser.blob_event.process`
- `receipt_parser.cosmos.upsert`

Activity names should follow a consistent naming pattern:

`<service>.<operation>`

### 3. Azure Monitor / Application Insights is the primary destination for OpenTelemetry traces

OpenTelemetry traces should be exported to Azure Monitor / Application Insights.

The exporter must use:

- `APPLICATIONINSIGHTS_CONNECTION_STRING`

from environment variables.

The connection string must not be hardcoded.

If the environment variable is missing:

- the service must not crash
- local logging must continue working
- the Azure exporter should be skipped
- a warning log may be emitted

### 4. Console output should prioritize readable application logs

Console output should remain useful during development and deployment debugging.

Therefore:

- `ILogger` logs should remain visible in console
- raw OpenTelemetry activity dumps should not flood the console
- verbose `System.Net.Http` trace output should be minimized or removed from console output
- if `ConsoleExporter` is used at all, it should be limited to essential information

The preferred model is:

- **console** → readable application logs
- **Azure Monitor / Application Insights** → detailed traces and dependencies

### 5. The same logging pattern must be shared across services

This observability structure must be applied consistently to:

- `discord-api`
- `receipt-parser`

Future services should follow the same pattern.

To support that, common setup should be abstracted where practical, for example through:

- shared extension methods
- shared bootstrap helpers
- shared observability configuration

## Consequences

### Positive

- Console output becomes much easier to read
- Application logs and tracing each have a clear responsibility
- Azure Monitor receives detailed telemetry without cluttering console output
- The project gains a scalable observability pattern for future services
- Cross-service tracing becomes easier to support later

### Negative

- Initial setup is slightly more complex than using console output only
- Developers must understand the distinction between `ILogger` and OpenTelemetry
- Shared observability setup introduces a small amount of project structure overhead

## Implementation Notes

- Add Azure Monitor / Application Insights OpenTelemetry exporter support
- Read `APPLICATIONINSIGHTS_CONNECTION_STRING` from environment variables
- Keep `ILogger` as the main console-facing logging mechanism
- Reduce or remove noisy OpenTelemetry console exporter output
- Preserve existing functionality while refactoring observability setup
- Apply the same style to both current services and future services

## Summary

Settle Up will use a two-layer observability strategy:

- **`ILogger` for human-readable application logs**
- **OpenTelemetry for tracing and dependency observability**

Detailed traces will go to Azure Monitor / Application Insights, while console output will remain focused on important application events.